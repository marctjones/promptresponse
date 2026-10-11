using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>
/// Feeds hostile and corrupted input to the reader every real file passes through.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ParserFuzzTests"/> fuzzes the typed single-document JSON serializer, and says
/// so: it skips every <c>.jsonc</c> and <c>.yaml</c> file. But a JSONC or YAML document, and
/// every stream, is read by <see cref="AprBeta6Reader"/>, whose comment stripper, record
/// splitter, YAML document splitter and feature refusals are hand-written text scanners. They
/// are where a malformed file can surprise a reader, and until this suite nothing threw
/// hostile input at them.
/// </para>
/// <para>
/// The contract is the same narrow one: for any input the reader either returns records or
/// throws <see cref="SerializationException"/>, and it terminates promptly. An exception of any
/// other type is a defect, because a caller told the format is safe to open cannot defend
/// against an exception it was never told about. The seed corpus is the specification's own
/// executable examples, in every representation. Mutations derive from them with a fixed seed,
/// so a failure reproduces exactly.
/// </para>
/// </remarks>
[Collection(Collections.HostileInput)]
public class ReaderBoundaryFuzzTests
{
    private const int BudgetMs = 5000;
    private const char RecordSeparator = '\u001e';

    // A deeper, still deterministic search for a one-off hunt:
    //   PROMPTRESPONSE_FUZZ_ROUNDS=30 dotnet test --filter ReaderBoundaryFuzzTests
    // CI runs the default of 1, which is fast and reproduces exactly.
    private static readonly int Rounds =
        int.TryParse(Environment.GetEnvironmentVariable("PROMPTRESPONSE_FUZZ_ROUNDS"), out var rounds) && rounds > 0 ? rounds : 1;

    private static string ExamplesFile => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "beta6", "spec-examples.json");

    private sealed record Seed(string Id, AprRepresentation Representation, string Document);

    private static IReadOnlyList<Seed> Seeds()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ExamplesFile));
        return file.RootElement.GetProperty("examples").EnumerateArray()
            .Select(example => new Seed(
                example.GetProperty("id").GetString()!,
                example.GetProperty("representation").GetString()!.StartsWith("yaml", StringComparison.Ordinal)
                    ? AprRepresentation.Yaml
                    : AprRepresentation.Jsonc,
                example.GetProperty("document").GetString()!))
            .ToList();
    }

    private sealed record Found(string Kind, string Where);

    private static Found? Defect(AprBeta6Reader reader, Seed seed, string mutated, string how)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            reader.ReadStream(mutated, seed.Representation);
        }
        catch (SerializationException)
        {
            // The designed outcome for bad input.
        }
        catch (Exception exception)
        {
            // The top frame says where the exception was raised, so a fix lands at the cause.
            var frame = exception.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "no stack";
            return new Found($"{exception.GetType().Name}: {exception.Message.Split('\n')[0]}", $"{seed.Id} ({how}) at {frame}");
        }
        return stopwatch.ElapsedMilliseconds >= BudgetMs
            ? new Found("took too long: a document that hangs a reader is a denial of service", $"{seed.Id} ({how}) took {stopwatch.ElapsedMilliseconds} ms")
            : null;
    }

    private static void AssertNoDefects(List<Found> defects)
    {
        // Grouped by kind so one root cause reads as one line, with the first place it was seen.
        var summary = defects.GroupBy(d => d.Kind).Select(g => $"{g.Count()}x {g.Key}  (first: {g.First().Where})").Take(12);
        defects.Should().BeEmpty("the reader must refuse bad input with SerializationException and nothing else. Kinds found:\n" + string.Join("\n", summary));
    }

    /// <summary>Every truncation of every example: an interrupted download, a full disk, a killed process.</summary>
    [Fact]
    public void Reader_EveryTruncation_IsRefusedCleanly()
    {
        var reader = new AprBeta6Reader();
        var defects = new List<Found>();
        foreach (var seed in Seeds())
        {
            var step = Math.Max(1, seed.Document.Length / (80 * Rounds));
            for (var length = 0; length < seed.Document.Length; length += step)
                if (Defect(reader, seed, seed.Document[..length], $"truncated to {length}") is { } defect) defects.Add(defect);
        }
        AssertNoDefects(defects);
    }

    /// <summary>One character replaced by something a scanner might mishandle.</summary>
    [Fact]
    public void Reader_SingleCharacterCorruption_IsRefusedCleanly()
    {
        // JSON and JSONC structure, comment openers, the record separator, a NUL, a lone
        // surrogate, and the characters YAML gives meaning to.
        const string poison = "\"{}[],:\\/*\u001e\u0000\uD800￿&!%@`#|>-?<'\n\t ";
        var reader = new AprBeta6Reader();
        var defects = new List<Found>();
        var rng = new Random(20261010);   // fixed seed: a failure must reproduce
        foreach (var seed in Seeds())
        {
            for (var i = 0; i < 80 * Rounds; i++)
            {
                var at = rng.Next(seed.Document.Length);
                var replacement = poison[rng.Next(poison.Length)];
                var mutated = string.Concat(seed.Document.AsSpan(0, at), replacement.ToString(), seed.Document.AsSpan(at + 1));
                if (Defect(reader, seed, mutated, $"char {at} replaced by U+{(int)replacement:X4}") is { } defect) defects.Add(defect);
            }
        }
        AssertNoDefects(defects);
    }

    /// <summary>Structure damaged at a stretch: a span deleted, or a record boundary inserted mid-record.</summary>
    [Fact]
    public void Reader_DeletedSpansAndStrayRecordSeparators_AreRefusedCleanly()
    {
        var reader = new AprBeta6Reader();
        var defects = new List<Found>();
        var rng = new Random(20261011);
        foreach (var seed in Seeds())
        {
            for (var i = 0; i < 40 * Rounds; i++)
            {
                var from = rng.Next(seed.Document.Length);
                var length = rng.Next(1, Math.Max(2, seed.Document.Length / 4));
                var deleted = seed.Document.Remove(from, Math.Min(length, seed.Document.Length - from));
                if (Defect(reader, seed, deleted, $"{length} chars deleted at {from}") is { } defect) defects.Add(defect);

                var stray = seed.Document.Insert(rng.Next(seed.Document.Length), RecordSeparator.ToString());
                if (Defect(reader, seed, stray, "a stray record separator inserted") is { } another) defects.Add(another);
            }
        }
        AssertNoDefects(defects);
    }
}

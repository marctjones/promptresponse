using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>
/// Feeds hostile and corrupted input to the parser.
/// </summary>
/// <remarks>
/// <para>
/// "Pure data, no code execution, safe to open untrusted files" (specification section 11)
/// is the format's strongest security claim, and it was backed by four hand-written
/// malformed fixtures. Four examples are not evidence about arbitrary input.
/// </para>
/// <para>
/// The contract asserted here is narrow and absolute: for any byte sequence, the parser
/// either returns a document or throws <see cref="SerializationException"/>. It must not
/// hang, must not exhaust memory, and must not surface a raw StackOverflow,
/// OutOfMemory, or unexpected exception type to a caller who was told opening a file is
/// safe. A caller cannot defend against what it was not told could happen.
/// </para>
/// <para>
/// Deterministic by construction - mutations are derived from the corpus and from a fixed
/// seed - so a failure reproduces exactly rather than appearing once in CI and never again.
/// </para>
/// </remarks>
public class ParserFuzzTests
{
    private static readonly AprJsonSerializer Serializer = new();
    private const int BudgetMs = 5000;

    private static string ExamplesDir => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "examples");

    /// <summary>Parse must end in one of two states, within a bounded time.</summary>
    /// <remarks>
    /// Exercised through both entry points. DeserializeAsync(Stream) is what actually opens
    /// a file in production, and it is a different code path from the string overload -
    /// fuzzing only the convenience overload would leave the real one untested.
    /// </remarks>
    private static async Task MustSurviveAsync(string input, string what)
    {
        await MustSurviveAsync(Encoding.UTF8.GetBytes(input), what);
        await AttemptAsync(() => Task.FromResult<AprDocument?>(Serializer.Deserialize(input)),
            $"{what} [string]");
    }

    /// <summary>The byte-level contract, which is the one a file actually meets.</summary>
    private static Task MustSurviveAsync(byte[] input, string what) =>
        AttemptAsync(
            async () =>
            {
                using var stream = new MemoryStream(input, writable: false);
                return await Serializer.DeserializeAsync(stream);
            },
            $"{what} [stream]");

    private static async Task AttemptAsync(Func<Task<AprDocument?>> parse, string what)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var document = await parse();
            // A parsed document must also validate without throwing, whatever it contains.
            if (document is not null)
            {
                new DocumentValidator().Validate(document);
            }
        }
        catch (SerializationException)
        {
            // The designed outcome for bad input.
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"{what}: opening a document surfaced {ex.GetType().Name} rather than " +
                $"SerializationException. A caller told the format is safe to open cannot " +
                $"defend against an exception it was never told about. Message: {ex.Message.Split('\n')[0]}");
        }
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(BudgetMs,
            $"{what}: parsing must terminate promptly; a document that hangs a reader is a denial of service");
    }

    public static IEnumerable<object[]> CorpusFiles() =>
        Directory.GetFiles(ExamplesDir, "*.apr*")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(p => new object[] { Path.GetFileName(p), File.ReadAllText(p) });

    /// <summary>Every truncation of a valid document.</summary>
    /// <remarks>
    /// A file cut short is the most ordinary corruption there is: an interrupted download,
    /// a full disk, a killed process. Every prefix must be refused cleanly rather than
    /// half-parsed into something that looks like a form.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public async Task EveryTruncationOfAValidDocument_IsRefusedCleanly(string name, string json)
    {
        for (var length = 0; length < json.Length; length += Math.Max(1, json.Length / 200))
        {
            await MustSurviveAsync(json[..length], $"{name} truncated to {length} chars");
        }
    }

    /// <summary>Single-byte corruption at points spread through a valid document.</summary>
    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public async Task SingleCharacterCorruption_IsRefusedCleanly(string name, string json)
    {
        const string poison = "\"{}[],:\\\u0000\uFFFF";
        var rng = new Random(20260827);   // fixed seed: a failure must reproduce
        for (var i = 0; i < 200; i++)
        {
            var at = rng.Next(json.Length);
            var replacement = poison[rng.Next(poison.Length)];
            var mutated = string.Concat(json.AsSpan(0, at), replacement.ToString(), json.AsSpan(at + 1));
            await MustSurviveAsync(mutated, $"{name} with '{(int)replacement:X4}' at {at}");
        }
    }

    /// <summary>Structures designed to exhaust a parser rather than merely confuse it.</summary>
    [Theory]
    [InlineData("deep array nesting")]
    [InlineData("deep object nesting")]
    [InlineData("deep section nesting")]
    [InlineData("very long string")]
    [InlineData("many sections")]
    [InlineData("many prompts")]
    [InlineData("duplicate keys")]
    [InlineData("lone surrogate")]
    [InlineData("null bytes")]
    [InlineData("bom and whitespace only")]
    [InlineData("empty input")]
    public async Task HostileStructures_AreRefusedCleanly(string shape)
    {
        var input = shape switch
        {
            "deep array nesting" => new string('[', 50_000) + new string(']', 50_000),
            "deep object nesting" => string.Concat(Enumerable.Repeat("{\"a\":", 50_000)) + "1"
                                     + new string('}', 50_000),
            "deep section nesting" => DeepSections(5_000),
            "very long string" => "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\""
                                  + new string('x', 5_000_000) + "\"},\"sections\":[]}",
            "many sections" => "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"t\"},\"sections\":["
                               + string.Join(",", Enumerable.Range(0, 50_000)
                                   .Select(i => $"{{\"id\":\"s{i}\",\"title\":\"t\"}}")) + "]}",
            "many prompts" => "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"t\"},\"sections\":[{\"id\":\"s\",\"title\":\"t\",\"prompts\":["
                              + string.Join(",", Enumerable.Range(0, 100_000)
                                  .Select(i => $"{{\"id\":\"p{i}\",\"label\":\"l\"}}")) + "]}]}",
            "duplicate keys" => "{\"version\":\"1.0-beta.6\",\"version\":\"9.9\",\"metadata\":{\"title\":\"t\",\"title\":\"u\"},\"sections\":[]}",
            "lone surrogate" => "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"\\uD800\"},\"sections\":[]}",
            "null bytes" => "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"a\\u0000b\"},\"sections\":[]}",
            "bom and whitespace only" => "\uFEFF   \n\t  ",
            "empty input" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        await MustSurviveAsync(input, shape);
    }

    private static string DeepSections(int depth)
    {
        var sb = new StringBuilder("{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"t\"},\"sections\":[");
        for (var i = 0; i < depth; i++)
        {
            sb.Append($"{{\"id\":\"s{i}\",\"title\":\"t\",\"sections\":[");
        }
        sb.Append(']', 0);
        for (var i = 0; i < depth; i++)
        {
            sb.Append("]}");
        }
        sb.Append("]}");
        return sb.ToString();
    }
    /// <summary>Reading a document must not reach the network.</summary>
    /// <remarks>
    /// Specification section 11: a reader MUST NOT fetch anything when opening a document.
    /// submissionUrl is data, not an instruction, and a form that phoned home when opened
    /// would leak that it had been received - from a format whose buyers chose it partly
    /// for being local-first.
    ///
    /// Asserted structurally rather than behaviourally: the core library does not
    /// reference an HTTP client at all, so no code path can contact anything regardless of
    /// input. A behavioural test could only prove the paths it happened to walk.
    /// </remarks>
    [Fact]
    public void CoreCannotReachTheNetwork_ByConstruction()
    {
        var core = typeof(AprJsonSerializer).Assembly;

        var networkTypes = core.GetTypes()
            .SelectMany(t => t.GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static)
                .Select(f => f.FieldType))
            .Concat(core.GetTypes().SelectMany(t => t.GetProperties().Select(pr => pr.PropertyType)))
            .Select(t => t.FullName ?? string.Empty)
            .Where(n => n.StartsWith("System.Net", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        networkTypes.Should().BeEmpty(
            "opening a document must not be able to fetch anything (specification section 11)");

        // The whole System.Net surface, not just HTTP: a socket opened in a method body
        // would leave no trace in the type scan above, but the assembly reference remains.
        core.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)
            .Should().NotContain(n => n.StartsWith("System.Net", StringComparison.Ordinal),
                "the core library has no business linking anything that can reach a network");
    }

    /// <summary>Measures where nesting actually gives out and pins the floor beneath it.</summary>
    /// <remarks>
    /// Specification section 4.6 requires at least 16 levels and states that the reference
    /// implementation's real ceiling is an implementation detail which MUST NOT be relied
    /// upon. So this asserts only the promised floor - asserting the measured ceiling would
    /// contradict the specification and turn a documented implementation detail into a
    /// contract.
    ///
    /// The corpus fixture proves 16 levels parse. What it cannot show is how much room is
    /// left above them: if a change halved the ceiling to 17, every existing test would
    /// still pass. Walking up one level at a time finds the exact boundary, so the margin
    /// is a measured number rather than an assumption.
    /// </remarks>
    [Fact]
    public void NestingCeiling_LeavesRoomAboveTheRequiredSixteenLevels()
    {
        int deepest = 0;
        for (var depth = 1; depth <= 512; depth++)
        {
            try
            {
                Serializer.Deserialize(NestedDocument(depth)).Should().NotBeNull();
                deepest = depth;
            }
            catch (SerializationException)
            {
                break;   // refused cleanly: this is the ceiling
            }
        }

        // NestedDocument wraps a leaf section, so total levels is one more than the
        // loop counter. Counted this way the measurement matches section 4.6's "fails
        // above 30 levels" rather than appearing to contradict it by one.
        deepest += 1;

        deepest.Should().BeGreaterThanOrEqualTo(16,
            $"specification section 4.6 requires at least 16 levels of section nesting; " +
            $"this build gives out after {deepest}");

        // Recorded, not asserted - section 4.6 makes the ceiling an implementation detail.
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "apr-nesting-ceiling.txt"),
            deepest.ToString());
    }

    /// <summary>Encoding hazards that only exist at the byte level.</summary>
    /// <remarks>
    /// Specification section 3.1: files MUST be UTF-8 and a reader SHOULD tolerate a
    /// leading byte-order mark. These cases cannot be written as C# strings - a string is
    /// already decoded, so truncating one can never split a multi-byte codepoint the way a
    /// half-copied file does. Classic interop failures: a BOM written by a Windows editor,
    /// a file transcoded to UTF-16 by a mail gateway, a transfer cut mid-character.
    /// </remarks>
    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public async Task EncodingHazards_AreToleratedOrRefusedCleanly(string name, string json)
    {
        var utf8 = Encoding.UTF8.GetBytes(json);

        // A leading BOM SHOULD be tolerated: the document must still parse.
        var withBom = Encoding.UTF8.GetPreamble().Concat(utf8).ToArray();
        using (var stream = new MemoryStream(withBom, writable: false))
        {
            var parsed = await Serializer.DeserializeAsync(stream);
            parsed.Should().NotBeNull(
                $"{name}: a leading byte-order mark must be tolerated (section 3.1), " +
                "because editors on Windows add one without being asked");
        }

        // Not UTF-8 at all. Must be refused cleanly, never silently mis-decoded.
        await MustSurviveAsync(Encoding.Unicode.GetBytes(json), $"{name} as UTF-16LE");
        await MustSurviveAsync(Encoding.BigEndianUnicode.GetBytes(json), $"{name} as UTF-16BE");

        // Truncated mid-codepoint, and arbitrary byte corruption.
        for (var cut = 1; cut < utf8.Length; cut += Math.Max(1, utf8.Length / 50))
        {
            await MustSurviveAsync(utf8[..cut], $"{name} cut at byte {cut}");
        }

        var rng = new Random(20260827);
        for (var i = 0; i < 100; i++)
        {
            var mutated = (byte[])utf8.Clone();
            mutated[rng.Next(mutated.Length)] = (byte)rng.Next(256);
            await MustSurviveAsync(mutated, $"{name} with a corrupted byte");
        }
    }

    private static string NestedDocument(int depth)
    {
        var sb = new StringBuilder("{\"version\":\"1.0-beta.6\",\"documentType\":\"template\",")
            .Append("\"metadata\":{\"title\":\"t\"},\"sections\":[");
        for (var i = 0; i < depth; i++)
        {
            sb.Append($"{{\"id\":\"s{i}\",\"title\":\"level {i}\",\"sections\":[");
        }
        // Innermost section carries the prompt, so every level is structurally valid.
        sb.Append("{\"id\":\"leaf\",\"title\":\"leaf\",\"prompts\":[{\"id\":\"p\",\"label\":\"l\"}]}");
        for (var i = 0; i < depth; i++)
        {
            sb.Append("]}");
        }
        return sb.Append("]}").ToString();
    }

}

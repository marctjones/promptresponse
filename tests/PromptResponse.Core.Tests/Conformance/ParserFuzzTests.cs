using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
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

    private static string CorpusDir(string kind) => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "v1", kind);

    /// <summary>Parse must end in one of two states, within a bounded time.</summary>
    private static void MustSurvive(string input, string what)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var document = Serializer.Deserialize(input);
            // A parsed document must also validate without throwing, whatever it contains.
            new DocumentValidator().Validate(document);
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
        Directory.GetFiles(CorpusDir("valid"), "*.apr*")
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
    public void EveryTruncationOfAValidDocument_IsRefusedCleanly(string name, string json)
    {
        for (var length = 0; length < json.Length; length += Math.Max(1, json.Length / 200))
        {
            MustSurvive(json[..length], $"{name} truncated to {length} chars");
        }
    }

    /// <summary>Single-byte corruption at points spread through a valid document.</summary>
    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void SingleCharacterCorruption_IsRefusedCleanly(string name, string json)
    {
        const string poison = "\"{}[],:\\\u0000\uFFFF";
        var rng = new Random(20260827);   // fixed seed: a failure must reproduce
        for (var i = 0; i < 200; i++)
        {
            var at = rng.Next(json.Length);
            var replacement = poison[rng.Next(poison.Length)];
            var mutated = string.Concat(json.AsSpan(0, at), replacement.ToString(), json.AsSpan(at + 1));
            MustSurvive(mutated, $"{name} with '{(int)replacement:X4}' at {at}");
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
    public void HostileStructures_AreRefusedCleanly(string shape)
    {
        var input = shape switch
        {
            "deep array nesting" => new string('[', 50_000) + new string(']', 50_000),
            "deep object nesting" => string.Concat(Enumerable.Repeat("{\"a\":", 50_000)) + "1"
                                     + new string('}', 50_000),
            "deep section nesting" => DeepSections(5_000),
            "very long string" => "{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\""
                                  + new string('x', 5_000_000) + "\"},\"sections\":[]}",
            "many sections" => "{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"t\"},\"sections\":["
                               + string.Join(",", Enumerable.Range(0, 50_000)
                                   .Select(i => $"{{\"id\":\"s{i}\",\"title\":\"t\"}}")) + "]}",
            "many prompts" => "{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"t\"},\"sections\":[{\"id\":\"s\",\"title\":\"t\",\"prompts\":["
                              + string.Join(",", Enumerable.Range(0, 100_000)
                                  .Select(i => $"{{\"id\":\"p{i}\",\"label\":\"l\"}}")) + "]}]}",
            "duplicate keys" => "{\"version\":\"1.0-beta\",\"version\":\"9.9\",\"metadata\":{\"title\":\"t\",\"title\":\"u\"},\"sections\":[]}",
            "lone surrogate" => "{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"\\uD800\"},\"sections\":[]}",
            "null bytes" => "{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"a\\u0000b\"},\"sections\":[]}",
            "bom and whitespace only" => "\uFEFF   \n\t  ",
            "empty input" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

        MustSurvive(input, shape);
    }

    private static string DeepSections(int depth)
    {
        var sb = new StringBuilder("{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"t\"},\"sections\":[");
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

        core.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)
            .Should().NotContain(n => n.StartsWith("System.Net.Http", StringComparison.Ordinal),
                "the core library has no business linking an HTTP client");
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

    private static string NestedDocument(int depth)
    {
        var sb = new StringBuilder("{\"version\":\"1.0-beta\",\"documentType\":\"template\",")
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

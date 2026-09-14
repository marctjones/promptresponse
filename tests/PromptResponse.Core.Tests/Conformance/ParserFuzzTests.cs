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

    /// <summary>How long a parse may run before the suite calls it non-terminating.</summary>
    /// <remarks>
    /// This is a liveness deadline, not a performance budget. APR-SEC-020 says a reader
    /// MUST terminate on every input; it says nothing about how fast, and neither does
    /// this suite. A wall-clock budget tight enough to be interesting is a budget that
    /// measures the runner rather than the reader - the 5000 ms one this replaced failed
    /// at 8994 ms under coverlet instrumentation on a change that touched no .NET code,
    /// while passing in every uninstrumented job. Instrumentation multiplies the cost of
    /// every sequence point; a machine under load multiplies it again.
    ///
    /// So the bound is set where only non-termination can reach it: two minutes, against a
    /// worst instrumented observation of 8994 ms and an uninstrumented cost, for the same
    /// five-million-character shape, of single-digit milliseconds. What a bound this loose
    /// cannot see - a reader that terminates but whose cost grows with the square of its
    /// input - is measured directly instead, by
    /// <see cref="ParseCost_GrowsWithInputSize_NotWithItsSquare"/>.
    /// </remarks>
    private static readonly TimeSpan TerminationDeadline = TimeSpan.FromMinutes(2);

    private static string ExamplesDir => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "examples");

    /// <summary>Parse must end, and must end in one of two states.</summary>
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

    /// <summary>Runs one parse and holds it to the contract: it returns a document, or it
    /// throws <see cref="SerializationException"/>, and either way it returns.</summary>
    private static async Task AttemptAsync(Func<Task<AprDocument?>> parse, string what)
    {
        // Off this thread deliberately: the string overload parses synchronously, so a
        // reader that never returns would never reach an await and there would be nothing
        // for a deadline to observe. On the pool there is.
        var attempt = Task.Run(async () =>
        {
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
        });

        try
        {
            await attempt.WaitAsync(TerminationDeadline);
        }
        catch (TimeoutException)
        {
            // Nothing can be done about the parse itself - .NET cannot abort a running
            // thread - so the runaway work outlives this assertion and the host exits
            // dirty. That is the correct report for a reader that does not come back.
            throw new Xunit.Sdk.XunitException(
                $"{what}: parsing had not returned after {TerminationDeadline.TotalSeconds:0} seconds. " +
                "A reader MUST terminate on every input (APR-SEC-020); a document that hangs a " +
                "reader is a denial of service, and a caller told the format is safe to open has " +
                "no way to take its thread back.");
        }
    }

    public static IEnumerable<object[]> CorpusFiles() =>
        Directory.GetFiles(ExamplesDir, "*.apr*")
            // JSONC and YAML examples are not strict JSON. This suite feeds every file to
            // the single-document JSON serializer, so their comments and document markers
            // would be reported as fuzz findings rather than the encoding hazards under test.
            .Where(f => !f.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
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
    /// <remarks>
    /// The second argument is the size the shape is built at. Shapes whose hazard is not a
    /// matter of size - a lone surrogate, an empty file - carry 0 and ignore it. Size is a
    /// parameter rather than a literal because the same builder is measured at two sizes by
    /// <see cref="ParseCost_GrowsWithInputSize_NotWithItsSquare"/>, and a growth measurement
    /// is only about the reader if both sizes come from the same construction.
    /// </remarks>
    [Theory]
    [InlineData("deep array nesting", 50_000)]
    [InlineData("deep object nesting", 50_000)]
    [InlineData("deep section nesting", 5_000)]
    [InlineData("very long string", 5_000_000)]
    [InlineData("many sections", 50_000)]
    [InlineData("many prompts", 100_000)]
    [InlineData("duplicate keys", 0)]
    [InlineData("lone surrogate", 0)]
    [InlineData("null bytes", 0)]
    [InlineData("bom and whitespace only", 0)]
    [InlineData("empty input", 0)]
    public async Task HostileStructures_AreRefusedCleanly(string shape, int scale)
    {
        await MustSurviveAsync(HostileInput(shape, scale), shape);
    }

    /// <summary>One hostile shape, built at a chosen size.</summary>
    private static string HostileInput(string shape, int scale)
    {
        return shape switch
        {
            "deep array nesting" => new string('[', scale) + new string(']', scale),
            "deep object nesting" => string.Concat(Enumerable.Repeat("{\"a\":", scale)) + "1"
                                     + new string('}', scale),
            "deep section nesting" => DeepSections(scale),
            "very long string" => "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\""
                                  + new string('x', scale) + "\"},\"sections\":[]}",
            "many sections" => "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"t\"},\"sections\":["
                               + string.Join(",", Enumerable.Range(0, scale)
                                   .Select(i => $"{{\"id\":\"s{i}\",\"title\":\"t\"}}")) + "]}",
            "many prompts" => "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"t\"},\"sections\":[{\"id\":\"s\",\"title\":\"t\",\"prompts\":["
                              + string.Join(",", Enumerable.Range(0, scale)
                                  .Select(i => $"{{\"id\":\"p{i}\",\"label\":\"l\"}}")) + "]}]}",
            "duplicate keys" => "{\"aprVersion\":\"1.0-beta.6\",\"version\":\"9.9\",\"metadata\":{\"title\":\"t\",\"title\":\"u\"},\"sections\":[]}",
            "lone surrogate" => "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"\\uD800\"},\"sections\":[]}",
            "null bytes" => "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"a\\u0000b\"},\"sections\":[]}",
            "bom and whitespace only" => "\uFEFF   \n\t  ",
            "empty input" => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    /// <summary>Parse cost must grow with the size of a document, not with its square.</summary>
    /// <remarks>
    /// <see cref="TerminationDeadline"/> proves a reader comes back (APR-SEC-020) but is
    /// far too loose to notice one that comes back late: quadratic scanning of a hostile
    /// array is the classic way a "safe to open" format becomes a denial of service, and
    /// it hides comfortably under any deadline generous enough to survive an instrumented
    /// build. So growth is measured rather than bounded.
    ///
    /// The measurement is a ratio between two sizes of the same shape, taken in the same
    /// process, on the same build, moments apart. Everything a wall-clock budget is
    /// hostage to - coverlet instrumentation, runner speed, a noisy neighbour, the JIT -
    /// applies to both halves and cancels. What survives is the reader's own growth curve,
    /// which is the thing the specification's claim is actually about.
    ///
    /// At eight times the input a linear reader costs about eight times as much, and a
    /// quadratic one about sixty-four. Measured here, instrumented and not: 5.7x to 8.6x
    /// for the structural shapes, and 11.8x to 14.7x for the single multi-megabyte string,
    /// which is bandwidth-bound rather than algorithm-bound - sixteen million characters
    /// do not fit the caches that two million do, so its per-character cost rises even
    /// though its per-character work does not. The ceiling sits at thirty-two: more than
    /// twice the worst figure measured, and half of what quadratic would produce. A gate
    /// that fires on noise is the defect this replaced, so the margin is deliberate.
    /// </remarks>
    [Theory]
    [InlineData("very long string", 2_000_000)]
    [InlineData("many sections", 5_000)]
    [InlineData("many prompts", 12_500)]
    public void ParseCost_GrowsWithInputSize_NotWithItsSquare(string shape, int baseScale)
    {
        const int factor = 8;
        const double ceiling = 32.0;

        var small = HostileInput(shape, baseScale);
        var large = HostileInput(shape, baseScale * factor);

        // The first parse of a process pays for JIT and tiered compilation. Charged to the
        // smaller measurement it would inflate the baseline and hide real growth; charged
        // to the larger one it would invent growth that is not there.
        ParseCostMs(small);
        ParseCostMs(large);

        var (smallMs, largeMs) = ParseCosts(small, large, runs: 5);
        var ratio = largeMs / Math.Max(smallMs, 0.001);

        // Recorded, not asserted - the absolute times are a property of the runner, and
        // only the ratio is a property of the reader. Kept so the numbers behind the
        // figures quoted above can be re-measured rather than taken on trust.
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), $"apr-parse-scaling-{shape.Replace(' ', '-')}.txt"),
            $"scale {baseScale}: {smallMs:F1}ms; scale {baseScale * factor}: {largeMs:F1}ms; " +
            $"ratio {ratio:F1}{Environment.NewLine}");

        ratio.Should().BeLessThan(ceiling,
            $"{shape}: at {factor}x the input the reader took {ratio:F1}x the time " +
            $"({smallMs:F1}ms -> {largeMs:F1}ms). Linear growth is about {factor}x and " +
            $"quadratic about {factor * factor}x, so cost here is growing with more than " +
            "the size of the document. A reader MUST terminate on every input " +
            "(APR-SEC-020) and MUST refuse cleanly at a bound it applies (APR-SEC-019); a " +
            "reader whose cost is superlinear in a hostile array honours neither in any " +
            "useful sense - it is a denial of service that happens to finish");
    }

    /// <summary>Best-of-N cost for two inputs, measured alternately.</summary>
    /// <remarks>
    /// Alternately, so that a slow stretch on a shared runner lands on both sizes rather
    /// than on one. The minimum rather than the mean, because interference can only ever
    /// add time: the fastest observation is the one least polluted by things that are not
    /// the reader.
    /// </remarks>
    private static (double Small, double Large) ParseCosts(string small, string large, int runs)
    {
        var smallBest = double.MaxValue;
        var largeBest = double.MaxValue;
        for (var i = 0; i < runs; i++)
        {
            smallBest = Math.Min(smallBest, ParseCostMs(small));
            largeBest = Math.Min(largeBest, ParseCostMs(large));
        }
        return (smallBest, largeBest);
    }

    /// <summary>Cost of one parse, from a settled heap.</summary>
    /// <remarks>
    /// These inputs allocate megabytes. Without collecting first, the debt one measurement
    /// runs up is paid inside the next one, and the ratio reports the garbage collector's
    /// scheduling rather than the reader's growth curve. Refusal is a legitimate outcome -
    /// the cost of reaching it is exactly what is being measured.
    /// </remarks>
    private static double ParseCostMs(string input)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            Serializer.Deserialize(input);
        }
        catch (SerializationException)
        {
            // Refusing is one of the two permitted endings; reaching it costs time too.
        }
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static string DeepSections(int depth)
    {
        var sb = new StringBuilder("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"t\"},\"sections\":[");
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

    /// <summary>The response length floor the specification states, and the room above it.</summary>
    /// <remarks>
    /// Specification section 4.8 requires an implementation to support a response of at
    /// least 1 MiB and states no ceiling, for the same reason section 5.6 states none for
    /// nesting: the right limit for a phone and for a batch importer are not the same
    /// number. So this asserts the floor only. The measured ceiling is recorded rather
    /// than asserted - asserting it would turn an implementation detail into a contract.
    ///
    /// Written in two-byte characters on purpose. The floor is stated in UTF-8 bytes, and
    /// a reader that counted UTF-16 code units instead would pass this test written in
    /// ASCII and then refuse a real document half the size it accepted in the fixture.
    ///
    /// Both directions, because a floor only a reader honours is not a floor: a writer
    /// that truncated on the way out would lose the response just as completely, and
    /// "any string is a valid response" (APR-MODEL-002) is the promise this number makes
    /// keepable.
    /// </remarks>
    [Fact]
    public async Task ResponseOfOneMebibyte_SurvivesReadingAndWriting()
    {
        const int floorBytes = 1024 * 1024;   // section 4.8, APR-MODEL-127

        var response = string.Concat(Enumerable.Repeat("\u00e9", floorBytes / 2));
        Encoding.UTF8.GetByteCount(response).Should().Be(floorBytes,
            "the fixture has to be a mebibyte of UTF-8, not a mebibyte of code units");

        var json = "{\"aprVersion\":\"1.0-beta.6\",\"documentType\":\"filledForm\","
                   + "\"metadata\":{\"title\":\"t\"},\"sections\":[{\"id\":\"s\",\"title\":\"t\","
                   + "\"prompts\":[{\"id\":\"p\",\"label\":\"l\",\"response\":\"" + response + "\"}]}]}";

        // The stream overload is what opens a file, so the floor has to hold there.
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        var document = await Serializer.DeserializeAsync(stream);

        document.Should().NotBeNull(
            "specification section 4.8 requires a response of at least 1 MiB to be readable");
        var read = document!.Sections[0].Prompts[0].Response;
        read.Should().Be(response,
            "a response at the floor must come back exactly as written, not truncated to fit");

        new DocumentValidator().Validate(document);

        // And out again: a writer that dropped it would lose the answer just as surely.
        var rewritten = Serializer.Deserialize(Serializer.Serialize(document));
        rewritten!.Sections[0].Prompts[0].Response.Should().Be(response,
            "a response at the floor must survive a write and a read back");
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
        var sb = new StringBuilder("{\"aprVersion\":\"1.0-beta.6\",\"documentType\":\"template\",")
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

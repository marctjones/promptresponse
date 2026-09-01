using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests;

/// <summary>
/// Runs every transcribed form file in the repository through the whole pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <b>These files are samples, not oracles.</b> Every one was produced by reading a PDF of
/// a government form and guessing at the APR that would represent it, and most were
/// generated before the table redesign, the type registry and the canonical write forms
/// existed. They are evidence of what documents look like in the wild. They are not
/// evidence of what the format requires.
/// </para>
/// <para>
/// A synthetic corpus fixture is the opposite: it carries its own verdict.
/// duplicate-section-id.aprt sits in invalid/ because it is <i>defined</i> to be invalid,
/// and fixture-plus-verdict together are the specification. Nothing about a transcribed
/// SF-86 defines anything.
/// </para>
/// <para>
/// So the assertions here are deliberately confined to claims about <b>our code</b>, which
/// any input is entitled to make: it must parse or fail cleanly, it must not be mutated by
/// being read or written, it must round-trip without drift, and it must not produce
/// duplicate ids. The one quality claim - that the document validates - is kept because
/// these files ship as examples and a broken example is worse than none, and because it is
/// a floor we control by fixing the file.
/// </para>
/// <para>
/// Everything above that floor is <i>measured</i>, not asserted, by
/// scripts/score-real-files.py. Drift from the current vocabulary belongs in a scorecard
/// that says which files to regenerate, not in a red test that pressures the specification
/// to bend back toward a stale guess.
/// </para>
/// <para>
/// What these files are actually good for is shape: depth, size, and hint combinations no
/// hand-written fixture would think to produce. Enumerated from disk, so a file added to
/// either directory is picked up without anyone remembering to list it.
/// </para>
/// </remarks>
public class RealFormFilesTests
{
    private static readonly AprJsonSerializer Serializer = new();

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    public static IEnumerable<object[]> RealFiles()
    {
        var roots = new[]
        {
            Path.Combine(RepoRoot, "examples"),
            Path.Combine(RepoRoot, "tests", "Fixtures"),
        };

        var files = roots
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.apr*", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            // JSONC and YAML examples are exercised through AprBeta6Reader below.
            // This legacy serializer suite intentionally covers only single JSON APR files.
            .Where(f => !f.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                     && !f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                "No real form files found. These tests assert against the bundled examples and " +
                "fixtures; finding none is a broken checkout, not an empty test run.");
        }

        return files.Select(f => new object[] { Path.GetRelativePath(RepoRoot, f) });
    }

    private static AprDocument Load(string relativePath) =>
        Serializer.Deserialize(File.ReadAllText(Path.Combine(RepoRoot, relativePath)));

    [Theory]
    [MemberData(nameof(RealFiles))]
    public void RealFile_Parses(string relativePath)
    {
        var document = Load(relativePath);

        document.Sections.Should().NotBeEmpty(
            $"{relativePath} is a real form and must contain sections");
    }

    /// <summary>These must be valid, not merely parseable.</summary>
    /// <remarks>
    /// The one quality claim made here, and only because it is a floor we control: a form
    /// handed to people as a model of the format must not fail the format's own validator.
    /// When this goes red the answer is to fix the file or the code that produced it -
    /// never to relax the validator, because these documents are transcriptions and have
    /// no authority over the specification.
    ///
    /// Conformance beyond validity - registered types, marked tables, applicable hints -
    /// is reported by scripts/score-real-files.py rather than asserted here.
    /// </remarks>
    [Theory]
    [MemberData(nameof(RealFiles))]
    public void RealFile_Validates(string relativePath)
    {
        var result = new DocumentValidator().Validate(Load(relativePath));

        result.IsValid.Should().BeTrue(
            $"{relativePath} ships as an example of the format. Errors: " +
            string.Join(" | ", result.Errors.Select(e => $"{e.ErrorCode} at {e.PropertyPath}: {e.Message}")));
    }

    /// <summary>Reading and writing must not change the document.</summary>
    /// <remarks>
    /// Byte-identical on the second write, not merely equivalent. A round trip that
    /// reorders members, drops an unknown one or re-stamps a timestamp would show up here
    /// as a diff, which is exactly what a user diffing two saved forms in git would see.
    /// </remarks>
    [Theory]
    [MemberData(nameof(RealFiles))]
    public void RealFile_RoundTripsWithoutDrift(string relativePath)
    {
        var once = Serializer.Serialize(Load(relativePath));
        var twice = Serializer.Serialize(Serializer.Deserialize(once));

        twice.Should().Be(once,
            $"{relativePath} must survive a load-save cycle unchanged; drift here is what " +
            "turns a saved form into a noisy diff nobody can review");
    }

    /// <summary>Serializing must not mutate the document it was handed.</summary>
    [Theory]
    [MemberData(nameof(RealFiles))]
    public void RealFile_SerializingDoesNotMutate(string relativePath)
    {
        var document = Load(relativePath);

        var first = Serializer.Serialize(document);

        Serializer.Serialize(document).Should().Be(first,
            $"serializing {relativePath} must leave it exactly as it was");
    }

    /// <summary>Responses stay strings, whatever the hints claim.</summary>
    [Theory]
    [MemberData(nameof(RealFiles))]
    public void RealFile_ResponsesAreAlwaysStrings(string relativePath)
    {
        foreach (var prompt in AllPrompts(Load(relativePath)))
        {
            prompt.Response.Should().NotBeNull(
                $"prompt '{prompt.Id}' in {relativePath}: a response is always a string, " +
                "never null, whatever expectedDataType says it was hoping for");
        }
    }

    /// <summary>Ids must be unique within their namespace, across the whole document.</summary>
    [Theory]
    [MemberData(nameof(RealFiles))]
    public void RealFile_IdsAreUniqueWithinTheirNamespace(string relativePath)
    {
        var document = Load(relativePath);
        var sectionIds = new List<string>();
        var promptIds = new List<string>();

        void Walk(Section section)
        {
            sectionIds.Add(section.Id);
            promptIds.AddRange(section.Prompts.Select(p => p.Id));
            foreach (var child in section.Sections) Walk(child);
        }
        foreach (var section in document.Sections) Walk(section);

        sectionIds.Should().OnlyHaveUniqueItems($"section ids must be unique in {relativePath}");
        promptIds.Should().OnlyHaveUniqueItems($"prompt ids must be unique in {relativePath}");
    }

    private static IEnumerable<Prompt> AllPrompts(AprDocument document)
    {
        IEnumerable<Prompt> Walk(Section section) =>
            section.Prompts.Concat(section.Sections.SelectMany(Walk));

        return document.Sections.SelectMany(Walk);
    }
}

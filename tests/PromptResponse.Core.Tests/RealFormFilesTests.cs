using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests;

/// <summary>
/// Runs every real form file in the repository through the whole pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The conformance corpus is deliberately synthetic: each fixture isolates one rule so a
/// failure names the rule it broke. That precision is also its limit. The fixtures are
/// small and tidy, and the things that break in practice - a section nested five deep, a
/// prompt with every hint set at once, a thousand fields, a label with an apostrophe in
/// it - only appear at the size and messiness of a real form.
/// </para>
/// <para>
/// These files are that: SF-86, IRS 990, IRS 1040, W-4 and the bundled starter templates,
/// including two produced by the PDF importer rather than written by hand. Enumerated from
/// disk, so a file added to either directory is picked up without anyone remembering to
/// list it here.
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

    /// <summary>Real files must be valid, not merely parseable.</summary>
    /// <remarks>
    /// These are the files shipped as examples and used as fixtures. A form we hand people
    /// as a model of the format failing our own validator would be worse than no example.
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

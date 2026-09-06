using System.Text.RegularExpressions;
using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>
/// Runs the executable examples embedded in the specification against the reference
/// reader.
/// </summary>
/// <remarks>
/// The vectors are generated from docs/APR_SPECIFICATION.md by
/// scripts/extract-spec-examples.py, so these are the specification's own claims
/// rather than a separately authored suite. Where an example and this reader
/// disagree, the specification is normative and the reader has the defect: such
/// cases are listed in <see cref="KnownDivergences"/> with the issue tracking them.
/// </remarks>
public sealed class SpecExampleTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    private static string VectorPath =>
        Path.Combine(RepoRoot, "tests", "Conformance", "beta6", "spec-examples.json");

    /// <summary>Examples this reader does not yet satisfy. Each is a reader defect.</summary>
    private static readonly Dictionary<string, string> KnownDivergences = new();

    public sealed record Example(
        string Id, string Rule, string Representation, string Expect, string Document);

    private static List<Example> ReadExamples()
    {
        using var stream = File.OpenRead(VectorPath);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("examples").EnumerateArray()
            .Select(element => new Example(
                element.GetProperty("id").GetString()!,
                element.GetProperty("rule").GetString()!,
                element.GetProperty("representation").GetString()!,
                element.GetProperty("expect").GetString()!,
                element.GetProperty("document").GetString()!))
            .ToList();
    }

    public static TheoryData<Example> Examples()
    {
        var data = new TheoryData<Example>();
        foreach (var example in ReadExamples())
        {
            data.Add(example);
        }
        return data;
    }

    /// <summary>Turns a prose stream example into the framing the format actually uses.</summary>
    /// <remarks>
    /// The specification separates records in a printed example with a <c>---</c> line,
    /// because a record separator is an invisible control character and an example
    /// nobody can read is a poor example. A reader is given the real framing, exactly as
    /// scripts/build-suite.py does when it assembles the conformance suite. Feeding a
    /// reader the prose form tests the prose, not the format.
    /// </remarks>
    private static string Framed(string document) => string.Concat(
        Regex.Split(document, "(?m)^---$")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => "\u001e" + part.Trim('\n') + "\n"));

    private static void Read(Example example)
    {
        var reader = new AprBeta6Reader();
        var representation = example.Representation.StartsWith("yaml", StringComparison.Ordinal)
            ? AprRepresentation.Yaml
            : AprRepresentation.Jsonc;

        if (example.Representation.EndsWith("-stream", StringComparison.Ordinal))
        {
            reader.ReadStream(Framed(example.Document), representation);
            return;
        }

        reader.ReadForm(example.Document, representation);
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void SpecificationExample_BehavesAsTheSpecificationSays(Example example)
    {
        if (KnownDivergences.TryGetValue(example.Id, out var reason))
        {
            // Recorded rather than skipped: the divergence is visible in the output,
            // and the reader is what has to change.
            Assert.Fail($"{example.Id}: known reader defect — {reason}");
        }

        var act = () => Read(example);

        if (example.Expect == "valid")
        {
            act.Should().NotThrow(
                $"{example.Id} demonstrates #{example.Rule} and the specification says it is valid");
            return;
        }

        if (example.Expect == "reject")
        {
            act.Should().Throw<Exception>(
                $"{example.Id} demonstrates #{example.Rule} and the specification requires rejection");
            return;
        }

        Assert.Fail($"{example.Id}: unrecognised expectation '{example.Expect}'");
    }

    [Fact]
    public void EveryExample_CitesARuleAndCarriesADocument()
    {
        foreach (var example in ReadExamples())
        {
            example.Rule.Should().NotBeNullOrWhiteSpace(
                $"{example.Id} must cite the specification anchor it demonstrates");
            example.Document.Should().NotBeNullOrWhiteSpace(
                $"{example.Id} must carry a document");
        }
    }
}

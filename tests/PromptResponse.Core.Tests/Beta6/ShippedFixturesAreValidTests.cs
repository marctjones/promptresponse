using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>Every APR document this repository ships is one the format accepts.</summary>
/// <remarks>
/// A test fixture built by hand agrees with the implementation that built it, which is
/// the failure mode the shared corpus exists to prevent: a green suite that is not
/// evidence of alignment. The .NET fixtures spelled the format version <c>version</c>
/// for the whole of the beta.6 work while the specification called it
/// <c>aprVersion</c>, and every test passed.
///
/// Discovered from disk rather than listed, so a fixture added tomorrow is covered the
/// day it is added, and an empty directory fails rather than yielding no cases.
/// </remarks>
public class ShippedFixturesAreValidTests
{
    private static readonly AprBeta6Reader Reader = new();
    private static readonly DocumentValidator Validator = new();

    private static string Root => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    /// <summary>Every tracked APR document outside the deliberately malformed corpus.</summary>
    public static IEnumerable<object[]> Documents()
    {
        var found = new List<object[]>();
        foreach (var directory in (string[])["examples", "tests/Fixtures", "tests/Conformance/beta6/forms"])
        {
            var path = Path.Combine(Root, directory);
            if (!Directory.Exists(path)) continue;
            foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).OrderBy(f => f))
            {
                // `rules/` and `malformed/` hold documents that are supposed to be
                // refused; a stream is read a different way and has its own cases.
                if (Path.GetExtension(file) is not (".apr" or ".aprt" or ".aprf" or ".jsonc" or ".yaml")) continue;
                if (file.Contains("stream", StringComparison.OrdinalIgnoreCase)) continue;
                found.Add([Path.GetRelativePath(Root, file)]);
            }
        }
        if (found.Count == 0)
        {
            throw new InvalidOperationException(
                "no APR documents found; this test asserts against them, and finding "
                + "none must fail rather than yield no cases");
        }
        return found;
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void AShippedDocument_IsReadAndAcceptedByTheShippedImplementation(string relativePath)
    {
        var path = Path.Combine(Root, relativePath);
        var representation = Path.GetExtension(path) == ".yaml"
            ? AprRepresentation.Yaml
            : AprRepresentation.Jsonc;

        var form = Reader.ReadForm(File.ReadAllText(path), representation);
        var result = Validator.Validate(form);

        result.Errors.Should().BeEmpty(
            $"{relativePath} is a document this repository ships, and a document it "
            + $"ships is one the format accepts: {string.Join("; ", result.Errors.Select(e => $"{e.ErrorCode} at {e.PropertyPath}"))}");
    }
}

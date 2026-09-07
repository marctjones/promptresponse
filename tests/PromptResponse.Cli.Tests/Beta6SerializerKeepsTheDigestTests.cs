using AwesomeAssertions;
using PromptResponse.Cli;
using PromptResponse.Core.Beta6;
using Xunit;

namespace PromptResponse.Cli.Tests;

/// <summary>
/// The command line's serializer reproduces the document it was given.
/// </summary>
/// <remarks>
/// `Beta6AprSerializer` is what every command that writes a form goes through, and it
/// wraps `AprBeta6Reader.WriteForm` — the regenerating path, not the parsed-value one the
/// conformance driver uses. So no conformance case reaches it, and the same defect that
/// made a form gain `documentType`, an empty `hints` and a `displayName` on every role
/// would reach a file `apr` wrote without anything noticing.
/// </remarks>
public class Beta6SerializerKeepsTheDigestTests
{
    private static readonly AprBeta6Reader Reader = new();

    public static TheoryData<string> ShippedForms()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(
                     Path.Combine(Root(), "tests", "Conformance", "beta6", "forms"), "*.jsonc"))
        {
            data.Add(path);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedForms))]
    public void SerializingAFormLeavesItTheSameDocument(string fixture)
    {
        var source = File.ReadAllText(fixture);
        var document = Reader.ReadForm(source, AprRepresentation.Jsonc);

        var written = new Beta6AprSerializer().Serialize(document);

        Digest(written).Should().Be(Digest(source),
            "a document {0} passes through `apr` must still be the document every "
            + "attestation over it names", Path.GetFileName(fixture));
    }

    private static string Digest(string source) =>
        AprSemanticDigest.Digest(Reader.ReadStream(source, AprRepresentation.Jsonc)
            .OfType<AprFormRecord>().First().Value);

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}

using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Opening a document and saving it without an edit leaves it the same document.
/// </summary>
/// <remarks>
/// The serializer half of this is `WriteFormPreservesPresenceTests`. This is the path a
/// person actually takes, and between reading and writing sit the file service, the
/// persistence layer, the session and the sanitizer — any of which can touch the model.
///
/// It matters because the digest is what every attestation over a form names, and what a
/// `metadata.regarding` chain points at. A save that changes it invalidates a signature
/// on a document nobody edited, which is the one failure mode that cannot be explained to
/// somebody afterwards.
/// </remarks>
public class OpenAndSaveKeepsTheDigestTests
{
    private static readonly AprBeta6Reader Reader = new();

    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(
                     Path.Combine(RepositoryRoot(), "tests", "Conformance", "beta6", "forms")))
            data.Add(path);
        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task ASaveWithNoEditKeepsTheDigest(string fixture)
    {
        var representation = fixture.EndsWith(".yaml", StringComparison.Ordinal)
            ? AprRepresentation.Yaml : AprRepresentation.Jsonc;
        var before = Digest(await File.ReadAllTextAsync(fixture), representation);

        var workspace = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(workspace);
        try
        {
            var copy = Path.Combine(workspace, Path.GetFileName(fixture));
            File.Copy(fixture, copy);

            var files = new FileService(new AprJsonSerializer());
            var document = await files.LoadFileAsync(copy);
            document.Should().NotBeNull();
            await files.SaveFileAsync(document!, copy);

            var after = await File.ReadAllTextAsync(copy);
            Digest(after, representation).Should().Be(before,
                "a save with no edit must leave every attestation over {0} resolving; it "
                + "came back as {1}", Path.GetFileName(fixture), after);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string Digest(string source, AprRepresentation representation) =>
        AprSemanticDigest.Digest(
            Reader.ReadStream(source, representation).OfType<AprFormRecord>().First().Value);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("no repository root above the test binary");
    }
}

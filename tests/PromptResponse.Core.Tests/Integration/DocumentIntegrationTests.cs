using PromptResponse.Core;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Tests.Integration;

/// <summary>
/// Shared fixtures and helpers for document-lifecycle integration tests.
/// </summary>
public partial class DocumentIntegrationTests
{
    private readonly AprJsonSerializer _serializer;

    public DocumentIntegrationTests()
    {
        _serializer = new AprJsonSerializer();
    }

    private static void CollectAllPrompts(List<Section> sections, List<Prompt> prompts)
    {
        foreach (var section in sections)
        {
            prompts.AddRange(section.Prompts);
            CollectAllPrompts(section.Sections, prompts);
        }
    }

    private static string GetExampleFilePath(string filename)
    {
        // Test fixtures live in tests/Fixtures/ (separate from examples/, which is for end-users).
        // Navigate from test output directory (bin/Debug/net8.0) up to repo root, then into tests/Fixtures/.
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var fixturesDir = Path.Combine(projectRoot, "tests", "Fixtures");
        return Path.Combine(fixturesDir, filename);
    }

    private AprDocument ReadBeta6Fixture(string filename)
    {
        // Fixtures are normalized through the active beta.6 reader/writer boundary.
        var document = _serializer.Deserialize(File.ReadAllText(GetExampleFilePath(filename)));
        document.Version = "1.0-beta.6";
        var reader = new AprBeta6Reader();
        return reader.ReadForm(reader.WriteForm(document, AprRepresentation.Jsonc), AprRepresentation.Jsonc);
    }
}

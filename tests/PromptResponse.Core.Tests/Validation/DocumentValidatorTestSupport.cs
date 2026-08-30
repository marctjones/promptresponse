using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;

namespace PromptResponse.Core.Tests.Validation;

public abstract class DocumentValidatorTestBase
{
    protected DocumentValidator Validator { get; } = new();

    protected static AprDocument CreateDocument(
        string title,
        Section? section = null,
        DocumentType documentType = DocumentType.Template,
        string version = AprFormat.CurrentVersion) =>
        new()
        {
            Version = version,
            DocumentType = documentType,
            Metadata = new Metadata { Title = title },
            Sections = section is null ? [] : [section]
        };

    protected static Section CreateSection(
        string id = "section_001",
        string title = "Section 1",
        List<Prompt>? prompts = null,
        List<Section>? childSections = null) =>
        new()
        {
            Id = id,
            Title = title,
            Prompts = prompts ?? [],
            Sections = childSections ?? []
        };
}

internal static class DocumentValidatorFixtureSupport
{
    public static string GetPath(string filename)
    {
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(projectRoot, "tests", "Fixtures", filename);
    }
}

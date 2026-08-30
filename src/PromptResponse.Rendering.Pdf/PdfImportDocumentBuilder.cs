using System.Text;
using PromptResponse.Core;
using PromptResponse.Core.Models;

namespace PromptResponse.Rendering.Pdf;

/// <summary>Builds a valid APR template from already-mapped PDF fields.</summary>
internal static class PdfImportDocumentBuilder
{
    public static AprDocument Build(string title, IEnumerable<PdfImportFieldMapping> mappings)
    {
        var sectionsByPage = new SortedDictionary<int, Section>();
        foreach (var mapping in mappings)
        {
            if (!sectionsByPage.TryGetValue(mapping.PageNumber, out var section))
            {
                section = CreateSection(mapping.PageNumber);
                sectionsByPage.Add(mapping.PageNumber, section);
            }

            section.Prompts.Add(mapping.Prompt);
        }

        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Imported form" : title;
        return new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = resolvedTitle,
                Description = "Imported from a fillable PDF (AcroForm). Review labels and field types.",
                TemplateId = Slug(resolvedTitle),
                TemplateVersion = AprFormat.CurrentVersion,
            },
            Sections = sectionsByPage.Values.Where(section => section.Prompts.Count > 0).ToList(),
        };
    }

    private static Section CreateSection(int pageNumber) => new()
    {
        Id = pageNumber > 0 ? $"page-{pageNumber}" : "fields",
        Title = pageNumber > 0 ? $"Page {pageNumber}" : "Fields",
        Description = pageNumber > 0 ? $"Fields from page {pageNumber} of the source PDF." : null,
    };

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character)) builder.Append(character);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }

        return builder.ToString().Trim('-') is { Length: > 0 } slug ? slug : "imported-form";
    }
}

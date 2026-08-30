using System.Text;
using System.Text.Json;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Cli.Commands.Export;

/// <summary>Renders the non-PDF export representations selected by the CLI workflow.</summary>
internal static class ExportContentRenderer
{
    internal static string Render(AprDocument document, ExportFormat format, bool fillable) => format switch
    {
        ExportFormat.Csv => ToCsv(document), ExportFormat.Json => ToJson(document),
        ExportFormat.Text => ToText(document), ExportFormat.Html => ToHtml(document, fillable),
        _ => throw new InvalidOperationException($"Unsupported text export format: {format}")
    };

    private static string ToCsv(AprDocument document)
    {
        var builder = new StringBuilder("Section,Subsection,Prompt ID,Label,Response,Data Type,Last Modified\n");
        foreach (var section in document.Sections) AppendCsvSection(section, section.Title, "", builder);
        return builder.ToString();
    }

    private static void AppendCsvSection(Section section, string root, string parent, StringBuilder builder)
    {
        var current = string.IsNullOrEmpty(parent) ? "" : $"{parent} / {section.Title}";
        foreach (var prompt in section.Prompts)
            builder.AppendLine(string.Join(",", new[] { root, current, prompt.Id, prompt.Label, prompt.Response, prompt.Hints.ExpectedDataType ?? "", prompt.ResponseMetadata.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "" }.Select(EscapeCsv)));
        foreach (var child in section.Sections)
            AppendCsvSection(child, root, string.IsNullOrEmpty(current) ? child.Title : $"{current} / {child.Title}", builder);
    }

    private static string EscapeCsv(string value) => value.Contains(',') || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string ToJson(AprDocument document) => JsonSerializer.Serialize(new
    {
        document.Metadata.Title, document.DocumentType, ExportDate = DateTime.UtcNow, Responses = ExtractResponses(document)
    }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static List<ResponseItem> ExtractResponses(AprDocument document)
    {
        var responses = new List<ResponseItem>();
        foreach (var section in document.Sections) AppendResponses(section, section.Title, null, responses);
        return responses;
    }

    private static void AppendResponses(Section section, string root, string? parent, List<ResponseItem> responses)
    {
        var current = parent is null ? null : $"{parent} / {section.Title}";
        foreach (var prompt in section.Prompts) responses.Add(new(root, current, prompt.Id, prompt.Label, prompt.Response, prompt.Hints.ExpectedDataType, prompt.ResponseMetadata.LastModified));
        foreach (var child in section.Sections) AppendResponses(child, root, current is null ? child.Title : $"{current} / {child.Title}", responses);
    }

    private static string ToHtml(AprDocument document, bool fillable)
    {
        IDocumentRenderer renderer = fillable ? new FillableHtmlDocumentRenderer() : new HtmlDocumentRenderer();
        using var stream = new MemoryStream();
        renderer.Render(document, RenderOptions.Default, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string ToText(AprDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine("═══════════════════════════════════════");
        builder.AppendLine($"Responses: {document.Metadata.Title}");
        builder.AppendLine($"Document Type: {document.DocumentType}");
        builder.AppendLine($"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine("═══════════════════════════════════════\n");
        foreach (var section in document.Sections) AppendTextSection(section, 2, builder);
        return builder.ToString();
    }

    private static void AppendTextSection(Section section, int level, StringBuilder builder)
    {
        builder.AppendLine($"{new string('#', level)} {section.Title}\n");
        foreach (var prompt in section.Prompts) { builder.AppendLine($"**{prompt.Label}**"); builder.AppendLine(string.IsNullOrWhiteSpace(prompt.Response) ? "(no response)" : prompt.Response); builder.AppendLine(); }
        foreach (var child in section.Sections) AppendTextSection(child, level + 1, builder);
    }

    private sealed record ResponseItem(string Section, string? Subsection, string PromptId, string Label, string Response, string? DataType, DateTime? LastModified);
}

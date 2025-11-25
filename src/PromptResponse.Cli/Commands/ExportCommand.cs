using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using System.Text;
using System.Text.Json;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Exports responses from an APR file to various formats.
/// </summary>
public class ExportCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public ExportCommand(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr export <file> [--format=<csv|json|txt>] [--output=<file>]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Formats:");
            Console.Error.WriteLine("  csv  - Comma-separated values (default)");
            Console.Error.WriteLine("  json - JSON format with prompt/response pairs");
            Console.Error.WriteLine("  txt  - Plain text format");
            return 1;
        }

        var filePath = args.FirstOrDefault(a => !a.StartsWith("--"));
        var format = GetArgValue(args, "--format") ?? "csv";
        var outputPath = GetArgValue(args, "--output");

        if (string.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("Error: File path required");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        if (!new[] { "csv", "json", "txt" }.Contains(format.ToLowerInvariant()))
        {
            Console.Error.WriteLine($"Error: Unsupported format: {format}");
            Console.Error.WriteLine("Supported formats: csv, json, txt");
            return 1;
        }

        try
        {
            // Load document
            var json = await File.ReadAllTextAsync(filePath);
            var document = _serializer.Deserialize(json);

            // Generate export
            string exportContent = format.ToLowerInvariant() switch
            {
                "csv" => ExportToCsv(document),
                "json" => ExportToJson(document),
                "txt" => ExportToText(document),
                _ => throw new InvalidOperationException($"Unsupported format: {format}")
            };

            // Output
            if (string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine(exportContent);
            }
            else
            {
                await File.WriteAllTextAsync(outputPath, exportContent);
                Console.WriteLine($"Exported to: {outputPath}");
            }

            return 0;
        }
        catch (SerializationException ex)
        {
            Console.Error.WriteLine($"Error: Failed to load APR file: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private string? GetArgValue(string[] args, string prefix)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
        return arg?.Substring(prefix.Length + 1);
    }

    private string ExportToCsv(AprDocument document)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("Section,Subsection,Prompt ID,Label,Response,Data Type,Last Modified");

        // Rows
        foreach (var section in document.Sections)
        {
            ExportSectionToCsv(section, section.Title, "", sb);
        }

        return sb.ToString();
    }

    private void ExportSectionToCsv(Section section, string rootSectionTitle, string parentPath, StringBuilder sb)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? "" : $"{parentPath} / {section.Title}";

        foreach (var prompt in section.Prompts)
        {
            sb.AppendLine(FormatCsvRow(
                rootSectionTitle,
                currentPath,
                prompt.Id,
                prompt.Label,
                prompt.Response,
                prompt.Hints.ExpectedDataType ?? "",
                prompt.ResponseMetadata.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
            ));
        }

        foreach (var childSection in section.Sections)
        {
            var childPath = string.IsNullOrEmpty(currentPath) ? childSection.Title : $"{currentPath} / {childSection.Title}";
            ExportSectionToCsv(childSection, rootSectionTitle, childPath, sb);
        }
    }

    private string FormatCsvRow(params string[] values)
    {
        return string.Join(",", values.Select(v => EscapeCsvValue(v)));
    }

    private string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private string ExportToJson(AprDocument document)
    {
        var export = new
        {
            document.Metadata.Title,
            document.DocumentType,
            ExportDate = DateTime.UtcNow,
            Responses = ExtractResponses(document)
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(export, options);
    }

    private List<ResponseItem> ExtractResponses(AprDocument document)
    {
        var responses = new List<ResponseItem>();

        foreach (var section in document.Sections)
        {
            ExtractResponsesFromSection(section, section.Title, null, responses);
        }

        return responses;
    }

    private void ExtractResponsesFromSection(Section section, string rootSectionTitle, string? parentPath, List<ResponseItem> responses)
    {
        var currentPath = parentPath == null ? null : $"{parentPath} / {section.Title}";

        foreach (var prompt in section.Prompts)
        {
            responses.Add(new ResponseItem
            {
                Section = rootSectionTitle,
                Subsection = currentPath,
                PromptId = prompt.Id,
                Label = prompt.Label,
                Response = prompt.Response,
                DataType = prompt.Hints.ExpectedDataType,
                LastModified = prompt.ResponseMetadata.LastModified
            });
        }

        foreach (var childSection in section.Sections)
        {
            var childPath = currentPath == null ? childSection.Title : $"{currentPath} / {childSection.Title}";
            ExtractResponsesFromSection(childSection, rootSectionTitle, childPath, responses);
        }
    }

    private string ExportToText(AprDocument document)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine($"Responses: {document.Metadata.Title}");
        sb.AppendLine($"Document Type: {document.DocumentType}");
        sb.AppendLine($"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine();

        foreach (var section in document.Sections)
        {
            ExportSectionToText(section, 2, sb);
        }

        return sb.ToString();
    }

    private void ExportSectionToText(Section section, int headingLevel, StringBuilder sb)
    {
        var heading = new string('#', headingLevel);
        sb.AppendLine($"{heading} {section.Title}");
        sb.AppendLine();

        foreach (var prompt in section.Prompts)
        {
            sb.AppendLine($"**{prompt.Label}**");
            if (!string.IsNullOrWhiteSpace(prompt.Response))
            {
                sb.AppendLine(prompt.Response);
            }
            else
            {
                sb.AppendLine("(no response)");
            }
            sb.AppendLine();
        }

        foreach (var childSection in section.Sections)
        {
            ExportSectionToText(childSection, headingLevel + 1, sb);
        }
    }

    private class ResponseItem
    {
        public string Section { get; set; } = "";
        public string? Subsection { get; set; }
        public string PromptId { get; set; } = "";
        public string Label { get; set; } = "";
        public string Response { get; set; } = "";
        public string? DataType { get; set; }
        public DateTime? LastModified { get; set; }
    }
}

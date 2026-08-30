using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Commands.Reporting;

/// <summary>Writes the human-readable document information report.</summary>
internal static class InfoDocumentPresenter
{
    internal static void Write(AprDocument document, string filePath)
    {
        Console.WriteLine("═══════════════════════════════════════"); Console.WriteLine($"File: {Path.GetFileName(filePath)}"); Console.WriteLine("═══════════════════════════════════════\n");
        Console.WriteLine("Document Information:"); Console.WriteLine($"  Version: {document.Version}"); Console.WriteLine($"  Type: {document.DocumentType}"); Console.WriteLine($"  Title: {document.Metadata.Title}");
        if (!string.IsNullOrWhiteSpace(document.Metadata.Description)) Console.WriteLine($"  Description: {document.Metadata.Description}");
        WriteTypeSpecificMetadata(document); if (document.Metadata.Modified.HasValue) Console.WriteLine($"  Last Modified: {document.Metadata.Modified:yyyy-MM-dd HH:mm:ss} UTC"); Console.WriteLine();
        var totals = Count(document.Sections); Console.WriteLine("Structure:"); Console.WriteLine($"  Sections: {document.Sections.Count}"); if (totals.Subsections > 0) Console.WriteLine($"  Child Sections: {totals.Subsections}"); Console.WriteLine($"  Total Prompts: {totals.Prompts}"); if (document.DocumentType == DocumentType.FilledForm) Console.WriteLine($"  Answered: {totals.Answered} ({(totals.Prompts > 0 ? totals.Answered * 100.0 / totals.Prompts : 0):F1}%)"); Console.WriteLine();
        Console.WriteLine("Sections:"); for (var index = 0; index < document.Sections.Count; index++) WriteSection(document.Sections[index], $"  {index + 1}. ", "     ");
        Console.WriteLine(); Console.WriteLine("═══════════════════════════════════════");
    }
    private static void WriteTypeSpecificMetadata(AprDocument document) { var metadata = document.Metadata; if (document.DocumentType == DocumentType.Template) { if (!string.IsNullOrWhiteSpace(metadata.Author)) Console.WriteLine($"  Author: {metadata.Author}"); if (!string.IsNullOrWhiteSpace(metadata.TemplateId)) Console.WriteLine($"  Template ID: {metadata.TemplateId}"); if (!string.IsNullOrWhiteSpace(metadata.TemplateVersion)) Console.WriteLine($"  Template Version: {metadata.TemplateVersion}"); if (metadata.Created.HasValue) Console.WriteLine($"  Created: {metadata.Created:yyyy-MM-dd HH:mm:ss} UTC"); } else { if (!string.IsNullOrWhiteSpace(metadata.TemplateId)) Console.WriteLine($"  Based on Template: {metadata.TemplateId}"); if (!string.IsNullOrWhiteSpace(metadata.TemplateVersion)) Console.WriteLine($"  Template Version: {metadata.TemplateVersion}"); if (!string.IsNullOrWhiteSpace(metadata.FilledBy)) Console.WriteLine($"  Filled By: {metadata.FilledBy}"); if (metadata.FilledDate.HasValue) Console.WriteLine($"  Filled Date: {metadata.FilledDate:yyyy-MM-dd HH:mm:ss} UTC"); } }
    private static (int Prompts, int Subsections, int Answered) Count(IEnumerable<Section> sections) { var prompts = 0; var subsections = 0; var answered = 0; foreach (var section in sections) { prompts += section.Prompts.Count; answered += section.Prompts.Count(prompt => !string.IsNullOrWhiteSpace(prompt.Response)); foreach (var child in section.Sections) { subsections++; var childTotals = Count([child]); prompts += childTotals.Prompts; subsections += childTotals.Subsections; answered += childTotals.Answered; } } return (prompts, subsections, answered); }
    private static int PromptCount(Section section) => section.Prompts.Count + section.Sections.Sum(PromptCount);
    private static void WriteSection(Section section, string prefix, string detailIndent) { Console.WriteLine($"{prefix}{section.Title}"); Console.WriteLine($"{detailIndent}ID: {section.Id}"); Console.WriteLine($"{detailIndent}Prompts: {PromptCount(section)}"); if (section.Sections.Count == 0) return; Console.WriteLine($"{detailIndent}Child Sections: {section.Sections.Count}"); foreach (var child in section.Sections) WriteChild(child, detailIndent + "  "); }
    private static void WriteChild(Section section, string indent) { Console.WriteLine($"{indent}- {section.Title} ({PromptCount(section)} prompts)"); foreach (var child in section.Sections) WriteChild(child, indent + "  "); }
}

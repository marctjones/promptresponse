using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Text;

namespace PromptResponse.Core.Serialization;

/// <summary>Normalizes authoring text while preserving filled responses and wire extensions.</summary>
internal static class AprDocumentSanitizer
{
    internal static void Sanitize(AprDocument document)
    {
        AprFormat.DropRetiredMembers(document.Extensions);
        if (document.Metadata is not null) { AprFormat.DropRetiredMembers(document.Metadata.Extensions); document.Metadata.Title = StringSanitizer.NormalizeAndStrip(document.Metadata.Title) ?? string.Empty; document.Metadata.Description = StringSanitizer.NormalizeAndStrip(document.Metadata.Description); document.Metadata.Author = StringSanitizer.NormalizeAndStrip(document.Metadata.Author); document.Metadata.FilledBy = StringSanitizer.NormalizeAndStrip(document.Metadata.FilledBy); document.Metadata.Publisher = StringSanitizer.NormalizeAndStrip(document.Metadata.Publisher); }
        foreach (var section in document.Sections) SanitizeSection(section);
    }
    private static void SanitizeSection(Section section) { AprFormat.DropRetiredMembers(section.Extensions); section.Title = StringSanitizer.NormalizeAndStrip(section.Title) ?? string.Empty; section.Description = StringSanitizer.NormalizeAndStrip(section.Description); foreach (var prompt in section.Prompts) SanitizePrompt(prompt); foreach (var nested in section.Sections) SanitizeSection(nested); }
    private static void SanitizePrompt(Prompt prompt) { AprFormat.DropRetiredMembers(prompt.Extensions); AprFormat.DropRetiredMembers(prompt.Hints?.Extensions); AprFormat.DropRetiredMembers(prompt.ResponseMetadata?.Extensions); prompt.Label = StringSanitizer.NormalizeAndStrip(prompt.Label) ?? string.Empty; prompt.SetNormalizedResponse(prompt.Response); if (prompt.Hints is not null) { prompt.Hints.HelpText = StringSanitizer.NormalizeAndStrip(prompt.Hints.HelpText); prompt.Hints.Placeholder = StringSanitizer.NormalizeAndStrip(prompt.Hints.Placeholder); } }
}

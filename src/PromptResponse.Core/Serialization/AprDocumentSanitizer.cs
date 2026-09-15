using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>Runs on every read and write. Human-facing text is reported, never rewritten.</summary>
/// <remarks>
/// This used to strip abusive code points out of every title, label, description and
/// help text as it read them. That is what APR-TEXT-011 forbids: a validator reports a
/// violation at authoring time, and a reader that meets one in a published form renders
/// it defensively and <em>never rewrites it</em>.
///
/// Rewriting had two costs. It changed the document's semantic model, so a form
/// carrying an invisible character produced a different digest here than anywhere else
/// and every attestation over it failed. And it hid the problem: a zero-width space
/// inserted to make one label look like another was removed in silence, so nobody was
/// told that somebody had tried.
///
/// The advisory vocabulary reports NON_NFC_TEXT and FORBIDDEN_CODE_POINT instead, and
/// the text stays exactly as it was written.
/// </remarks>
internal static class AprDocumentSanitizer
{
    internal static void Sanitize(AprDocument document)
    {
        foreach (var section in document.Sections) SanitizeSection(section);
    }

    private static void SanitizeSection(Section section)
    {
        foreach (var prompt in section.Prompts) SanitizePrompt(prompt);
        foreach (var nested in section.Sections) SanitizeSection(nested);
    }

    private static void SanitizePrompt(Prompt prompt)
    {
        // A response is what a person typed, and is preserved byte for byte. The setter
        // is kept because it is where that guarantee lives.
        prompt.SetNormalizedResponse(prompt.Response);
    }
}

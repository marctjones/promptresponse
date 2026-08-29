using PromptResponse.Core.Models;
using System.Text;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Inspects a document for "almost-always-abusive" characters that
/// <see cref="Text.StringSanitizer"/> deliberately preserves on save (because
/// they have rare-but-real legitimate uses — Persian ZWNJ, emoji ZWJ, line-break
/// hyphenation, RTL marks). Emits an advisory per affected prompt so the user
/// can verify the character was intentional.
/// </summary>
/// <remarks>
/// Vision invariant: never blocking — these characters are valid Unicode and
/// the user may have entered them on purpose. The advisor only surfaces them
/// so the user isn't tricked by hidden content.
///
/// Advisory categories:
/// <list type="bullet">
///   <item><b>HIDDEN_ZWSP</b>: zero-width space U+200B</item>
///   <item><b>HIDDEN_ZWJ</b> / <b>HIDDEN_ZWNJ</b>: legitimate in Persian / Hindi /
///     emoji but worth surfacing when the surrounding text is clearly Latin</item>
///   <item><b>HIDDEN_SOFT_HYPHEN</b>: soft hyphen U+00AD inside a word</item>
///   <item><b>HIDDEN_BIDI_MARK</b>: LRM/RLM in Latin-only text (likely paste artifact)</item>
///   <item><b>HIDDEN_VARIATION_SELECTOR</b>: VS1..VS256 — emoji presentation
///     selectors and variation selectors; usually intentional but worth flagging
///     when present without an emoji base</item>
/// </list>
/// </remarks>
public sealed class HiddenCharacterAdvisor : IValidator<AprDocument>
{
    /// <inheritdoc />
    public ValidationResult Validate(AprDocument target)
    {
        var result = new ValidationResult();
        if (target == null) return result;

        ScanSubmissionUrls(target, result);

        foreach (var section in target.Sections)
        {
            ScanSection(section, result);
        }
        return result;
    }

    /// <summary>
    /// The submission URL is the one field where a hidden character is never
    /// innocent: it is authored, machine-consumed, and bound into the publisher
    /// signature so the target cannot be redirected silently. A zero-width space
    /// inside a hostname renders to a reviewer as the host they expect while being a
    /// different string, so it is surfaced here and blocks signing.
    /// </summary>
    private static void ScanSubmissionUrls(AprDocument document, ValidationResult result)
    {
        var urls = document.Metadata?.SubmissionUrls;
        if (urls is null) return;
        for (var i = 0; i < urls.Count; i++)
        {
            if (!Text.StringSanitizer.ContainsHiddenCharacters(urls[i])) continue;
            result.AddWarning(new ValidationWarning(
                "The submission URL contains hidden characters (zero-width, bidi, or similar). "
                + "It may display as a different address than it actually is. Retype it rather than editing it.",
                $"metadata.submissionUrls[{i}]", "SUBMISSION_URL_HIDDEN_CHARS"));
        }
    }

    private static void ScanSection(Section section, ValidationResult result)
    {
        foreach (var prompt in section.Prompts)
        {
            ScanPrompt(prompt, result);
        }
        foreach (var nested in section.Sections)
        {
            ScanSection(nested, result);
        }
    }

    private static void ScanPrompt(Prompt prompt, ValidationResult result)
    {
        if (string.IsNullOrEmpty(prompt.Response)) return;

        var findings = ScanString(prompt.Response);
        foreach (var finding in findings)
        {
            result.AddWarning(new ValidationWarning(
                $"Response contains a hidden character ({finding.Description}) at offset {finding.Offset}. " +
                $"This is valid Unicode but may have been pasted accidentally; verify it was intentional.",
                prompt.Id,
                finding.Code));
        }
    }

    /// <summary>Scans a single string for hidden characters and returns one
    /// finding per occurrence. Public for unit testability and for views that
    /// want to render per-character markers.</summary>
    public static IReadOnlyList<HiddenCharFinding> ScanString(string value)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<HiddenCharFinding>();

        var findings = new List<HiddenCharFinding>();
        var idx = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var v = rune.Value;
            string? code = null;
            string? description = null;
            switch (v)
            {
                case 0x200B:
                    code = "HIDDEN_ZWSP";
                    description = "zero-width space U+200B";
                    break;
                case 0x200C:
                    code = "HIDDEN_ZWNJ";
                    description = "zero-width non-joiner U+200C";
                    break;
                case 0x200D:
                    code = "HIDDEN_ZWJ";
                    description = "zero-width joiner U+200D";
                    break;
                case 0x200E:
                    code = "HIDDEN_BIDI_MARK";
                    description = "left-to-right mark U+200E";
                    break;
                case 0x200F:
                    code = "HIDDEN_BIDI_MARK";
                    description = "right-to-left mark U+200F";
                    break;
                case 0x00AD:
                    code = "HIDDEN_SOFT_HYPHEN";
                    description = "soft hyphen U+00AD";
                    break;
                case 0x2060:
                    code = "HIDDEN_WORD_JOINER";
                    description = "word joiner U+2060";
                    break;
                case >= 0x202A and <= 0x202E:
                    code = "BIDI_OVERRIDE";
                    description = $"bidirectional override U+{v:X4}";
                    break;
                case >= 0x2066 and <= 0x2069:
                    code = "BIDI_ISOLATE";
                    description = $"bidirectional isolate U+{v:X4}";
                    break;
                case 0xFEFF:
                    code = "TEXT_BOM";
                    description = "byte-order mark U+FEFF inside text";
                    break;
                case 0xFFFE or 0xFFFF:
                    code = "NONCHARACTER";
                    description = $"Unicode noncharacter U+{v:X4}";
                    break;
                case >= 0x00 and <= 0x08:
                case 0x0B or 0x0C:
                case >= 0x0E and <= 0x1F:
                case >= 0x7F and <= 0x9F:
                    code = "CONTROL_CHARACTER";
                    description = $"control character U+{v:X4}";
                    break;
                case 0x2061:
                case 0x2062:
                case 0x2063:
                case 0x2064:
                    code = "HIDDEN_INVISIBLE_OPERATOR";
                    description = $"invisible math operator U+{v:X4}";
                    break;
                case >= 0xFE00 and <= 0xFE0F:
                    code = "HIDDEN_VARIATION_SELECTOR";
                    description = $"variation selector U+{v:X4}";
                    break;
                case >= 0xE0100 and <= 0xE01EF:
                    code = "HIDDEN_VARIATION_SELECTOR";
                    description = $"variation selector U+{v:X4}";
                    break;
            }

            if (code != null && description != null)
            {
                findings.Add(new HiddenCharFinding(idx, v, code, description));
            }
            idx += rune.Utf16SequenceLength;
        }
        return findings;
    }
}

/// <summary>One detected hidden character occurrence.</summary>
/// <param name="Offset">Index within the source string where the character occurs.</param>
/// <param name="Codepoint">Unicode codepoint value.</param>
/// <param name="Code">Stable advisory code (e.g. "HIDDEN_ZWSP").</param>
/// <param name="Description">Human-readable description for advisory display.</param>
public sealed record HiddenCharFinding(int Offset, int Codepoint, string Code, string Description);

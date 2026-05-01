using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Detects mixed-script content in URL and email response fields — the
/// homoglyph attack vector browsers guard against in their IDN address-bar
/// display. A domain like <c>аpple.com</c> (Cyrillic 'а') renders identically
/// to <c>apple.com</c> but resolves to a different host. This advisor flags
/// such cases so the user can verify the value was intentional.
/// </summary>
/// <remarks>
/// Vision invariant: never blocking, never rewriting. The advisory only points
/// out the suspicious script-mixing so the user can decide.
///
/// Scope:
/// <list type="bullet">
///   <item><b>URL hint</b>: parse the host portion; advise if any label mixes
///     scripts (e.g., Cyrillic + Latin in one label).</item>
///   <item><b>Email hint</b>: parse the domain portion; advise if any label
///     mixes scripts.</item>
///   <item>Other hint types are out of scope — homoglyph detection on prose
///     responses produces too many false positives.</item>
/// </list>
///
/// Script detection uses Unicode block ranges for the most commonly confused
/// scripts (Latin, Cyrillic, Greek, Armenian, Han, Hangul, Arabic, Hebrew,
/// Thai, Devanagari). Digits, ASCII punctuation, and combining marks are
/// script-neutral.
/// </remarks>
public sealed class MixedScriptAdvisor : IValidator<AprDocument>
{
    /// <inheritdoc />
    public ValidationResult Validate(AprDocument target)
    {
        var result = new ValidationResult();
        if (target == null) return result;

        foreach (var section in target.Sections)
        {
            ScanSection(section, result);
        }
        return result;
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
        var hint = prompt.Hints?.ExpectedDataType?.ToLowerInvariant();
        if (hint != "url" && hint != "email") return;

        var host = hint == "url" ? ExtractUrlHost(prompt.Response) : ExtractEmailDomain(prompt.Response);
        if (string.IsNullOrEmpty(host)) return;

        foreach (var label in host.Split('.'))
        {
            var scripts = DetectScripts(label);
            if (scripts.Count > 1)
            {
                result.AddWarning(new ValidationWarning(
                    $"The {(hint == "url" ? "URL host" : "email domain")} label '{label}' " +
                    $"mixes scripts ({string.Join(", ", scripts)}). " +
                    $"Look-alike characters from different scripts can spoof the value — verify it's intentional.",
                    prompt.Id,
                    "MIXED_SCRIPT"));
            }
        }
    }

    /// <summary>Returns the host portion of a URL string, or null if it can't be parsed.</summary>
    public static string? ExtractUrlHost(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        // Best-effort parse — Uri requires a scheme; tolerate "example.com" by prepending one.
        var input = url.Contains("://") ? url : "http://" + url;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }
        return null;
    }

    /// <summary>Returns the domain portion of an email address, or null if it
    /// doesn't look like an email.</summary>
    public static string? ExtractEmailDomain(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var atIdx = email.IndexOf('@');
        if (atIdx < 0 || atIdx == email.Length - 1) return null;
        return email[(atIdx + 1)..];
    }

    /// <summary>Detects which scripts are present in <paramref name="text"/>,
    /// excluding script-neutral characters (digits, ASCII punctuation,
    /// combining marks). Returns an empty set for empty/script-neutral input.
    /// </summary>
    public static IReadOnlySet<string> DetectScripts(string text)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text)) return found;

        foreach (var rune in text.EnumerateRunes())
        {
            var script = ClassifyScript(rune.Value);
            if (script != null) found.Add(script);
        }
        return found;
    }

    private static string? ClassifyScript(int v)
    {
        // Script-neutral: ASCII digits, ASCII punctuation, hyphen-minus, etc.
        if (v < 0x0080)
        {
            // Treat ASCII letters as Latin; everything else (digits, punctuation) is neutral.
            return char.IsLetter((char)v) ? "Latin" : null;
        }

        return v switch
        {
            // Latin (extensions)
            >= 0x00C0 and <= 0x024F => "Latin",
            >= 0x1E00 and <= 0x1EFF => "Latin",
            >= 0x2C60 and <= 0x2C7F => "Latin",
            >= 0xA720 and <= 0xA7FF => "Latin",
            // Greek
            >= 0x0370 and <= 0x03FF => "Greek",
            >= 0x1F00 and <= 0x1FFF => "Greek",
            // Cyrillic
            >= 0x0400 and <= 0x04FF => "Cyrillic",
            >= 0x0500 and <= 0x052F => "Cyrillic",
            >= 0x2DE0 and <= 0x2DFF => "Cyrillic",
            >= 0xA640 and <= 0xA69F => "Cyrillic",
            // Armenian
            >= 0x0530 and <= 0x058F => "Armenian",
            // Hebrew
            >= 0x0590 and <= 0x05FF => "Hebrew",
            // Arabic
            >= 0x0600 and <= 0x06FF => "Arabic",
            >= 0x0750 and <= 0x077F => "Arabic",
            // Devanagari
            >= 0x0900 and <= 0x097F => "Devanagari",
            // Thai
            >= 0x0E00 and <= 0x0E7F => "Thai",
            // Han (CJK Unified Ideographs)
            >= 0x4E00 and <= 0x9FFF => "Han",
            >= 0x3400 and <= 0x4DBF => "Han",
            >= 0x20000 and <= 0x2A6DF => "Han",
            // Hangul
            >= 0x1100 and <= 0x11FF => "Hangul",
            >= 0xAC00 and <= 0xD7AF => "Hangul",
            // Hiragana
            >= 0x3040 and <= 0x309F => "Hiragana",
            // Katakana
            >= 0x30A0 and <= 0x30FF => "Katakana",
            // Combining marks — script-neutral (they attach to the preceding base char)
            >= 0x0300 and <= 0x036F => null,
            >= 0x1AB0 and <= 0x1AFF => null,
            >= 0x1DC0 and <= 0x1DFF => null,
            >= 0x20D0 and <= 0x20FF => null,
            >= 0xFE20 and <= 0xFE2F => null,
            // Default: neutral (digits, punctuation, symbols, emoji)
            _ => null,
        };
    }
}

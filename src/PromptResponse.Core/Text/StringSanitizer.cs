using System.Globalization;
using System.Text;

namespace PromptResponse.Core.Text;

/// <summary>
/// Normalizes and strips characters that have no legitimate use in form responses.
/// Applied at the serialization boundary so anything written to disk is clean,
/// while anything the user is currently typing in-memory remains untouched until
/// they save.
/// </summary>
/// <remarks>
/// Vision invariant: any visible text is a valid response. This sanitizer only
/// removes characters that are <i>definitionally invisible or definitionally
/// deceptive</i>:
///
/// <list type="bullet">
///   <item>Unicode normalization to NFC — so a precomposed "é" and "e + combining
///     acute" are stored identically and compare equal in downstream consumers.</item>
///   <item>BOM (<c>U+FEFF</c>) appearing mid-string — only meaningful at the start
///     of a UTF-encoded byte stream; inside a string it's hidden noise.</item>
///   <item>Bidirectional override characters <c>U+202D</c> (LRO) and <c>U+202E</c>
///     (RLO) — used to spoof file extensions and labels (<c>"evil_‮gpj.exe"</c>
///     renders as <c>"evil_exe.jpg"</c>). Bidi <i>marks</i> (LRM <c>U+200E</c>,
///     RLM <c>U+200F</c>) are preserved because they have legitimate uses in
///     mixed-script text.</item>
///   <item>Lone surrogates and U+FFFE/U+FFFF non-characters.</item>
///   <item>C0 control characters <c>U+0000..U+001F</c> EXCEPT tab/LF/CR, which
///     are legitimate in multi-line responses.</item>
///   <item>DEL <c>U+007F</c>.</item>
/// </list>
///
/// Characters that are <b>preserved</b> (these have legitimate uses):
/// <list type="bullet">
///   <item>Zero-width joiner <c>U+200D</c> — required for emoji sequences
///     (e.g. 👨‍👩‍👧 family emoji glues components with ZWJ).</item>
///   <item>Zero-width non-joiner <c>U+200C</c> — required in Persian, Hindi, etc.
///     to prevent unwanted ligatures.</item>
///   <item>Bidi marks LRM/RLM — required for correct rendering of mixed-script
///     text (Arabic + Latin numerals, etc.).</item>
///   <item>Soft hyphen <c>U+00AD</c> — visible only when line breaks at it; useful
///     in long URL/word strings.</item>
///   <item>Combining accents and other combining marks.</item>
/// </list>
///
/// Detecting and advising on the <i>almost-always-abusive</i> set
/// (zero-width space U+200B, soft hyphen mid-word, etc.) is the
/// <c>HiddenCharacterAdvisor</c>'s job — that emits warnings, this strips.
/// </remarks>
public static class StringSanitizer
{
    /// <summary>
    /// Returns <paramref name="value"/> with NFC normalization applied and the
    /// "always-abusive" character set removed. Returns the input unchanged if it's
    /// null or empty.
    /// </summary>
    public static string? NormalizeAndStrip(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Fast path: if nothing's abusive AND already NFC, return unchanged.
        var hasAbusive = ContainsAbusive(value);
        var alreadyNfc = !hasAbusive && value.IsNormalized(NormalizationForm.FormC);
        if (alreadyNfc) return value;

        // Strip first — non-character codepoints make Normalize() throw, so we must
        // remove them before normalization.
        string stripped;
        if (hasAbusive)
        {
            var sb = new StringBuilder(value.Length);
            foreach (var rune in value.EnumerateRunes())
            {
                if (!IsAbusive(rune)) sb.Append(rune.ToString());
            }
            stripped = sb.ToString();
        }
        else
        {
            stripped = value;
        }

        return stripped.IsNormalized(NormalizationForm.FormC)
            ? stripped
            : stripped.Normalize(NormalizationForm.FormC);
    }

    /// <summary>True when the character has no legitimate use in a form response
    /// and is silently stripped on save.</summary>
    public static bool IsAbusive(System.Text.Rune rune)
    {
        var v = rune.Value;
        return v switch
        {
            // C0 controls except tab/LF/CR
            >= 0x0000 and <= 0x0008 => true,
            0x000B or 0x000C => true,
            >= 0x000E and <= 0x001F => true,
            // DEL + C1 controls (rarely useful in form responses; almost always paste artifacts)
            0x007F => true,
            >= 0x0080 and <= 0x009F => true,
            // Bidi overrides — used in spoofing
            0x202D or 0x202E => true,
            // Bidi isolate overrides — same risk class
            0x2066 or 0x2067 or 0x2068 or 0x2069 => true,
            // BOM mid-string
            0xFEFF => true,
            // Object replacement, interlinear annotation anchor/separator/terminator
            0xFFFC => true,
            0xFFF9 or 0xFFFA or 0xFFFB => true,
            // Non-characters (U+FFFE, U+FFFF) and the U+FDD0..U+FDEF block
            >= 0xFDD0 and <= 0xFDEF => true,
            0xFFFE or 0xFFFF => true,
            // Plane non-characters: U+1FFFE, U+1FFFF, ..., U+10FFFE, U+10FFFF
            _ when (v & 0xFFFF) >= 0xFFFE && v >= 0x10000 && v <= 0x10FFFF => true,
            _ => false,
        };
    }

    private static bool ContainsAbusive(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (IsAbusive(rune)) return true;
        }
        return false;
    }
}

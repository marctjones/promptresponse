using System.Text.RegularExpressions;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>
/// The advisory conditions the format names, reported under the codes it names them by.
/// </summary>
/// <remarks>
/// The rules on section 7.2's rows (APR-VAL-018 to APR-VAL-035) say an implementation
/// reporting one of these conditions reports it under the code the row gives it. Whether to warn at all stays this library's choice;
/// the spelling does not, because a warning exists to be understood by whoever reads it
/// next, and two implementations reporting one condition under two names have produced
/// advice that only travels as prose.
///
/// This library used its own vocabulary — <c>TYPE_MISMATCH</c> for what the format calls
/// <c>RESPONSE_CONTRADICTS_TYPE</c>, <c>ROW_COUNT_OUT_OF_HINT_RANGE</c> for
/// <c>TABLE_OVER_CAPACITY</c> — and did not report most of the list at all.
///
/// Every one of these is advisory: none makes a document invalid, none prevents saving,
/// and none prevents entering any text (APR-VAL-002, APR-VAL-006).
/// </remarks>
internal static class AdvisoryVocabulary
{
    /// <summary>Data types the registry carries. An unrecognised one degrades to text.</summary>
    /// <remarks>
    /// Mirrors <c>schemas/apr-types-1.0.json</c>. A copy, because Core reads no file at
    /// run time — and therefore a copy that can drift, which is why
    /// <c>RegisteredTypes_MatchTheTypeRegistry</c> compares the two and fails when they
    /// disagree. The first version of this list omitted <c>color</c> and
    /// <c>password</c>, so a document declaring either was told its type was not
    /// registered.
    /// </remarks>
    internal static readonly HashSet<string> Registered = new(StringComparer.Ordinal)
    {
        "boolean", "color", "currency", "date", "datetime", "email", "multichoice",
        "multiline", "number", "password", "phone", "range", "select", "text", "time",
        "url",
    };

    /// <summary>Submission schemes this document defines. Anything else is advisory.</summary>
    private static readonly HashSet<string> Schemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https", "mailto",
    };

    internal static void Inspect(AprDocument document, ValidationResult result)
    {
        InspectShape(document, result);
        InspectText(document, result);
        InspectSubmission(document, result);
        InspectExtensions(document.Metadata?.Extensions, "metadata", result);
        var roles = new HashSet<string>(
            document.Roles?.Select(role => role.Id).Where(id => !string.IsNullOrEmpty(id))
                ?? [], StringComparer.Ordinal);
        foreach (var (section, path) in Walk(document))
        {
            InspectSection(section, path, roles, result);
        }
    }

    private static IEnumerable<(Section Section, string Path)> Walk(AprDocument document)
    {
        var pending = new Stack<(Section, string)>();
        for (var index = (document.Sections?.Count ?? 0) - 1; index >= 0; index--)
        {
            pending.Push((document.Sections![index], $"sections[{index}]"));
        }
        while (pending.Count > 0)
        {
            var (section, path) = pending.Pop();
            yield return (section, path);
            for (var index = (section.Sections?.Count ?? 0) - 1; index >= 0; index--)
            {
                pending.Push((section.Sections![index], $"{path}.sections[{index}]"));
            }
        }
    }

    private static void InspectSection(
        Section section, string path, HashSet<string> roles, ValidationResult result)
    {
        // Table members on a section that is not a table are preserved and ignored, and
        // saying so is how an author learns the section is not the table they meant.
        if (!string.Equals(section.Kind, "table", StringComparison.Ordinal)
            && (section.MaxRows is not null || section.CanAddRows is not null))
        {
            result.AddWarning(new ValidationWarning(
                "maxRows or canAddRows on a section that is not a table; a table is a "
                + "table only by carrying kind: \"table\".",
                path, "TABLE_MEMBERS_ON_A_PLAIN_SECTION"));
        }
        Role(section.Role, $"{path}.role", roles, result);
        InspectExtensions(section.Extensions, path, result);

        for (var index = 0; index < (section.Prompts?.Count ?? 0); index++)
        {
            InspectPrompt(section.Prompts![index], $"{path}.prompts[{index}]", roles, result);
        }
    }

    private static void InspectPrompt(
        Prompt prompt, string path, HashSet<string> roles, ValidationResult result)
    {
        Role(prompt.Role, $"{path}.role", roles, result);
        InspectExtensions(prompt.Extensions, path, result);

        var hints = prompt.Hints;
        if (hints is null) return;

        if (hints.ExpectedDataType is { Length: > 0 } declared && !Registered.Contains(declared))
        {
            result.AddWarning(new ValidationWarning(
                $"expectedDataType '{declared}' is not in the type registry; an "
                + "unrecognised type degrades to text and never rejects a response.",
                $"{path}.hints.expectedDataType", "UNREGISTERED_DATA_TYPE"));
        }

        // A hint that cannot be applied at all is worth saying, because the author
        // believes they have asked for something and nothing is happening.
        if (hints.ValidationPattern is { Length: > 0 } pattern && !Compiles(pattern))
        {
            result.AddWarning(new ValidationWarning(
                "validationPattern is not a valid regular expression, so nothing can "
                + "apply it.", $"{path}.hints.validationPattern", "HINT_UNUSABLE"));
        }

        var response = prompt.Response;
        if (string.IsNullOrEmpty(response)) return;

        if (hints.SuggestedValues is { Count: > 0 } suggested
            && !suggested.Contains(response, StringComparer.Ordinal))
        {
            result.AddWarning(new ValidationWarning(
                "the response is not one of the suggested values, which is allowed: a "
                + "shortlist is an offer, and \"other\" is often the right answer.",
                $"{path}.response", "RESPONSE_OUTSIDE_SUGGESTED_VALUES"));
        }

        if (hints.MinNumber is not null || hints.MaxNumber is not null)
        {
            if (double.TryParse(response, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var value)
                && ((hints.MinNumber is { } low && value < low)
                    || (hints.MaxNumber is { } high && value > high)))
            {
                result.AddWarning(new ValidationWarning(
                    "the response falls outside the suggested bounds, which describe the "
                    + "control offered rather than a limit on the answer.",
                    $"{path}.response", "RESPONSE_OUTSIDE_BOUNDS"));
            }
        }
    }

    private static void Role(
        string? role, string path, HashSet<string> roles, ValidationResult result)
    {
        if (role is not { Length: > 0 } named || roles.Contains(named)) return;
        result.AddWarning(new ValidationWarning(
            $"role '{named}' is not declared in roles; an undeclared role "
            + "degrades to no special treatment.", path, "UNDECLARED_ROLE"));
    }

    private static void InspectExtensions(
        Dictionary<string, System.Text.Json.JsonElement>? extensions, string path,
        ValidationResult result)
    {
        foreach (var name in extensions?.Keys ?? Enumerable.Empty<string>())
        {
            if (name.Contains('.', StringComparison.Ordinal)) continue;
            result.AddWarning(new ValidationWarning(
                $"unknown member '{name}' carries no reverse-DNS prefix; unprefixed names "
                + "are reserved to the specification.", $"{path}.{name}", "UNPREFIXED_MEMBER"));
        }
    }

    /// <summary>Members whose value has a shape the format states, not just a type.</summary>
    /// <remarks>
    /// These are errors rather than advisories. A templateId that is not a URI is not
    /// unique by construction, a `regarding` entry that is not a digest resolves to
    /// nothing, and a table capped at zero rows cannot hold the instance a table is
    /// required to have. Each says something the format does not allow, which is what
    /// section 7.1 calls WRONG_TYPE.
    /// </remarks>
    private static void InspectShape(AprDocument document, ValidationResult result)
    {
        var metadata = document.Metadata;
        if (metadata?.TemplateId is { Length: > 0 } template && !IsUri(template))
        {
            result.AddError(new ValidationError(
                $"templateId '{template}' is not a URI. A template identifier must be "
                + "unique across every author who will ever publish a form, which is why "
                + "it rests on a namespace someone already owns — a tag URI, or a mailto.",
                "metadata.templateId", "WRONG_TYPE"));
        }
        for (var index = 0; index < (metadata?.Regarding?.Count ?? 0); index++)
        {
            var entry = metadata!.Regarding![index];
            if (IsDigest(entry)) continue;
            result.AddError(new ValidationError(
                $"regarding entry {index} is not a digest. Every entry names a record by "
                + "its semantic digest, and one that is not a digest names nothing.",
                $"metadata.regarding[{index}]", "WRONG_TYPE"));
        }
        foreach (var (section, path) in Walk(document))
        {
            if (section.MaxRows is { } cap && cap < 1)
            {
                result.AddError(new ValidationError(
                    $"maxRows is {cap}. A table always holds at least one instance, so a "
                    + "cap below one describes a table that cannot exist.",
                    $"{path}.maxRows", "WRONG_TYPE"));
            }
        }
    }

    /// <summary>Holds human-facing text to the floor the format states.</summary>
    /// <remarks>
    /// Human-facing text must be in Normalization Form C and must not carry a code point
    /// that is unassigned, a surrogate, private-use, a control other than tab and
    /// newline, or one UTS #39 classifies as default-ignorable, deprecated or
    /// not-a-character. A validator reports a violation at authoring time.
    ///
    /// A response is not human-facing text in this sense. It is what a person typed, and
    /// suspicious characters in one are surfaced and rendered visibly while the document
    /// stays valid — which is why this walks titles and labels and never a response.
    /// </remarks>
    private static void InspectText(AprDocument document, ValidationResult result)
    {
        HoldToTheFloor(document.Metadata?.Title, "metadata.title", result);
        HoldToTheFloor(document.Metadata?.Description, "metadata.description", result);
        HoldToTheFloor(document.Metadata?.Author, "metadata.author", result);
        HoldToTheFloor(document.Metadata?.Publisher, "metadata.publisher", result);
        LookForConfusableScriptMix(document.Metadata?.Title, "metadata.title", result);
        LookForConfusableScriptMix(document.Metadata?.Description, "metadata.description", result);
        LookForConfusableScriptMix(document.Metadata?.Author, "metadata.author", result);
        LookForConfusableScriptMix(document.Metadata?.Publisher, "metadata.publisher", result);
        foreach (var (section, path) in Walk(document))
        {
            HoldToTheFloor(section.Title, $"{path}.title", result);
            HoldToTheFloor(section.Description, $"{path}.description", result);
            LookForConfusableScriptMix(section.Title, $"{path}.title", result);
            LookForConfusableScriptMix(section.Description, $"{path}.description", result);
            for (var index = 0; index < (section.Prompts?.Count ?? 0); index++)
            {
                HoldToTheFloor(section.Prompts![index].Label, $"{path}.prompts[{index}].label", result);
                LookForConfusableScriptMix(section.Prompts![index].Label, $"{path}.prompts[{index}].label", result);
            }
        }
    }

    /// <summary>
    /// APR-TEXT-012: "SHOULD apply the confusable and mixed-script detection of
    /// UTS #39... report what it finds." Full UTS #39 restriction-level analysis
    /// needs a declared document language to avoid flagging ordinary multi-script
    /// text (Japanese Han+Hiragana+Katakana, Korean Hangul+Han, Latin loanwords
    /// in Indic/Arabic/Hebrew text) -- APR has no metadata.language member yet,
    /// so that full analysis isn't attempted here.
    /// </summary>
    /// <remarks>
    /// What doesn't need a declared language: Latin, Cyrillic and Greek have
    /// extensive letter-shape homoglyphs between them (Cyrillic а/Latin a, Greek
    /// Α/Latin A) and essentially no legitimate reason to co-occur within one
    /// title or label -- unlike CJK/Hangul/Indic scripts, which routinely mix
    /// with Latin for brand names, loanwords and numerals. Flagging only these
    /// three scripts mixing with each other is a narrow, script-agnostic slice
    /// of UTS #39 that produces zero known false positives on real multi-script
    /// text.
    ///
    /// Not in specification 7.2's warnings table -- CONFUSABLE_SCRIPT_MIX is
    /// this implementation's own spelling of an APR-VAL-002 "MAY surface any
    /// warning, including conditions this table does not name" extension, not a
    /// code every implementation must use.
    ///
    /// .NET's BCL has no Unicode Script property (unlike ICU), so script is
    /// approximated with compact code-point ranges rather than character names.
    /// A code point outside all three ranges is simply not counted -- a miss
    /// there is a false negative, the safe direction for an advisory check.
    /// </remarks>
    private static void LookForConfusableScriptMix(string? value, string path, ValidationResult result)
    {
        if (value is not { Length: > 0 }) return;
        var scripts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var rune in value.EnumerateRunes())
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(rune.Value) is not
                (System.Globalization.UnicodeCategory.UppercaseLetter
                or System.Globalization.UnicodeCategory.LowercaseLetter
                or System.Globalization.UnicodeCategory.TitlecaseLetter
                or System.Globalization.UnicodeCategory.ModifierLetter
                or System.Globalization.UnicodeCategory.OtherLetter))
            {
                continue;   // Digits, punctuation and spaces are script-neutral.
            }
            var script = ConfusableScriptOf(rune.Value);
            if (script is not null) scripts.Add(script);
        }
        if (scripts.Count <= 1) return;
        result.AddWarning(new ValidationWarning(
            $"mixes {string.Join(", ", scripts)} letters in one field; Latin, Cyrillic "
            + "and Greek share look-alike letters, and a mix within one title or label "
            + "is rarely intentional.", path, "CONFUSABLE_SCRIPT_MIX"));
    }

    private static string? ConfusableScriptOf(int codePoint) => codePoint switch
    {
        (>= 0x0041 and <= 0x005A) or (>= 0x0061 and <= 0x007A)
            or (>= 0x00C0 and <= 0x024F) or (>= 0x1E00 and <= 0x1EFF) => "Latin",
        (>= 0x0400 and <= 0x052F) or (>= 0x1C80 and <= 0x1C8F)
            or (>= 0x2DE0 and <= 0x2DFF) or (>= 0xA640 and <= 0xA69F) => "Cyrillic",
        (>= 0x0370 and <= 0x03FF) or (>= 0x1F00 and <= 0x1FFF) => "Greek",
        _ => null,
    };

    // Specification 8.2.3 places NON_NFC_TEXT and FORBIDDEN_CODE_POINT in the
    // warnings table (7.2), not the errors table (7.1, stated exhaustive by
    // APR-VAL-007): a validator MUST report them, but a warning MUST NOT affect
    // validity or block saving (APR-VAL-006, APR-VAL-002). AddError here would
    // reject a document the format requires to stay valid.
    private static void HoldToTheFloor(string? value, string path, ValidationResult result)
    {
        if (value is not { Length: > 0 }) return;
        if (!value.IsNormalized(System.Text.NormalizationForm.FormC))
        {
            result.AddWarning(new ValidationWarning(
                "human-facing text must be in Normalization Form C; two spellings of one "
                + "word are two different strings to everything that compares them.",
                path, "NON_NFC_TEXT"));
        }
        foreach (var rune in value.EnumerateRunes())
        {
            if (!BelowTheFloor(rune)) continue;
            // Not uniformly "renders as nothing": this category also holds ZWJ and
            // ZWNJ, load-bearing for correct glyph shaping in Persian, Hindi and
            // other scripts. Say what the rule is, not a rendering claim that's
            // false for part of the set it covers.
            result.AddWarning(new ValidationWarning(
                $"human-facing text carries U+{rune.Value:X4}, which the human-facing "
                + "text floor excludes.", path, "FORBIDDEN_CODE_POINT"));
            return;   // One report names the member; listing every offender adds noise.
        }
    }

    /// <summary>Is this code point one the human-facing text floor excludes?</summary>
    /// <remarks>
    /// Unassigned, a surrogate, private-use, a control other than tab and newline, or
    /// classified by UTS #39 as default-ignorable, deprecated or not-a-character. The
    /// default-ignorable set is the one that matters in practice: a zero-width space
    /// renders as nothing, so it can make one label look exactly like another while
    /// comparing unequal to it.
    ///
    /// Deliberately not <c>StringSanitizer.IsAbusive</c>, which is a narrower set aimed
    /// at paste artefacts in a response and does not carry the zero-width characters.
    /// </remarks>
    private static bool BelowTheFloor(System.Text.Rune rune)
    {
        var value = rune.Value;
        if (value is 0x0009 or 0x000A) return false;
        var category = System.Text.Rune.GetUnicodeCategory(rune);
        if (category is System.Globalization.UnicodeCategory.Control
            or System.Globalization.UnicodeCategory.Format
            or System.Globalization.UnicodeCategory.Surrogate
            or System.Globalization.UnicodeCategory.PrivateUse
            or System.Globalization.UnicodeCategory.OtherNotAssigned)
        {
            // Format (Cf) is the category the individually listed code points below
            // belong to — the zero-width characters, the bidi controls, the annotation
            // anchors. Naming the category rather than only its members means a code
            // point assigned to it in a later Unicode version is excluded on the day it
            // is assigned, rather than the day somebody notices.
            return true;
        }
        return value switch
        {
            0x00AD => true,                              // soft hyphen
            0x061C => true,                              // Arabic letter mark
            >= 0x180B and <= 0x180F => true,             // Mongolian selectors and separator
            >= 0x200B and <= 0x200F => true,             // zero-width and directional marks
            >= 0x202A and <= 0x202E => true,             // embedding and override
            >= 0x2060 and <= 0x206F => true,             // word joiner, invisible operators
            0xFEFF => true,                              // byte-order mark mid-string
            >= 0xFFF0 and <= 0xFFF8 => true,             // unassigned specials
            0xFFFE or 0xFFFF => true,                    // not a character
            >= 0x1D173 and <= 0x1D17A => true,           // musical format controls
            >= 0xE0000 and <= 0xE0FFF => true,           // tags and variation selectors
            _ => (value & 0xFFFE) == 0xFFFE,             // every plane's non-characters
        };
    }

    /// <summary>A URI has a scheme. Nothing here fetches one; that is forbidden.</summary>
    private static bool IsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out _);

    private static bool IsDigest(string value) =>
        value.StartsWith("sha256:", StringComparison.Ordinal)
        && value.Length == "sha256:".Length + 64
        && value.AsSpan("sha256:".Length).ToString()
            .All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static void InspectSubmission(AprDocument document, ValidationResult result)
    {
        var urls = document.Metadata?.SubmissionUrls;
        for (var index = 0; index < (urls?.Count ?? 0); index++)
        {
            var url = urls![index];
            var scheme = url.Contains(':', StringComparison.Ordinal)
                ? url[..url.IndexOf(':', StringComparison.Ordinal)]
                : string.Empty;
            if (Schemes.Contains(scheme)) continue;
            result.AddWarning(new ValidationWarning(
                $"submission entry {index} names the scheme '{scheme}', which this "
                + "document does not define; a reader offers the entries it understands.",
                $"metadata.submissionUrls[{index}]", "SUBMISSION_URL_UNSUPPORTED"));
        }
    }

    private static bool Compiles(string pattern)
    {
        try
        {
            _ = Regex.Match(string.Empty, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50));
            return true;
        }
        catch (ArgumentException)
        {
            return false;   // Not a regular expression at all, which is the condition.
        }
        catch (RegexMatchTimeoutException)
        {
            return true;    // It compiles; it is merely slow, which is a different thing.
        }
    }
}

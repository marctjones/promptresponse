using System.Text.RegularExpressions;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>
/// The advisory conditions the format names, reported under the codes it names them by.
/// </summary>
/// <remarks>
/// APR-VAL-009 says an implementation reporting one of these conditions reports it under
/// the code section 7.2 gives it. Whether to warn at all stays this library's choice;
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
    private static readonly HashSet<string> Registered = new(StringComparer.Ordinal)
    {
        "text", "multiline", "number", "currency", "range", "date", "time", "datetime",
        "boolean", "select", "multichoice", "email", "phone", "url",
    };

    /// <summary>Submission schemes this document defines. Anything else is advisory.</summary>
    private static readonly HashSet<string> Schemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https", "mailto",
    };

    /// <summary>Members whose whole purpose is to be read or heard by a person.</summary>
    private static readonly string[] HumanText = ["title", "description", "author", "publisher", "label"];

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
            $"role '{named}' is not declared in metadata.roles; an undeclared role "
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
        foreach (var (section, path) in Walk(document))
        {
            HoldToTheFloor(section.Title, $"{path}.title", result);
            HoldToTheFloor(section.Description, $"{path}.description", result);
            for (var index = 0; index < (section.Prompts?.Count ?? 0); index++)
            {
                HoldToTheFloor(section.Prompts![index].Label, $"{path}.prompts[{index}].label", result);
            }
        }
    }

    private static void HoldToTheFloor(string? value, string path, ValidationResult result)
    {
        if (value is not { Length: > 0 }) return;
        if (!value.IsNormalized(System.Text.NormalizationForm.FormC))
        {
            result.AddError(new ValidationError(
                "human-facing text must be in Normalization Form C; two spellings of one "
                + "word are two different strings to everything that compares them.",
                path, "NON_NFC_TEXT"));
        }
        foreach (var rune in value.EnumerateRunes())
        {
            if (!BelowTheFloor(rune)) continue;
            result.AddError(new ValidationError(
                $"human-facing text carries U+{rune.Value:X4}, which the format excludes: "
                + "a character that renders as nothing can make one label look like "
                + "another.", path, "FORBIDDEN_CODE_POINT"));
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
            or System.Globalization.UnicodeCategory.Surrogate
            or System.Globalization.UnicodeCategory.PrivateUse
            or System.Globalization.UnicodeCategory.OtherNotAssigned)
        {
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

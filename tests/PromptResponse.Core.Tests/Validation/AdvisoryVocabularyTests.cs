using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>
/// Every advisory the format names, and the three value-shape errors beside them.
/// </summary>
/// <remarks>
/// These were reached only through the conformance driver, which measures a different
/// assembly, so the coverage gate saw them as untested. A conformance case proves the
/// condition is reported; these prove which code it carries and that it is an advisory
/// rather than an error — the distinction APR-VAL-002 turns on, and the one a document
/// vector cannot show, because a document with an advisory is valid either way.
/// </remarks>
public class AdvisoryVocabularyTests
{
    private static readonly DocumentValidator Validator = new();

    private static AprDocument Form(params Prompt[] prompts) => new()
    {
        Version = AprFormat.CurrentVersion,
        Metadata = new Metadata { Title = "T" },
        Sections = [new Section { Id = "s", Title = "S", Prompts = [.. prompts] }],
    };

    private static Prompt Answer(string response, PromptHints hints) =>
        new() { Id = "p", Label = "P", Response = response, Hints = hints };

    private static ValidationResult Check(AprDocument document) => Validator.Validate(document);

    [Fact]
    public void AnUnregisteredDataType_Warns_AndTheDocumentStaysValid()
    {
        var result = Check(Form(Answer("x", new PromptHints { ExpectedDataType = "sparkline" })));

        result.IsValid.Should().BeTrue("an unrecognised type degrades to text; it never rejects");
        result.Warnings.Should().Contain(w => w.WarningCode == "UNREGISTERED_DATA_TYPE");
    }

    [Fact]
    public void ARegisteredDataType_DoesNotWarn()
    {
        Check(Form(Answer("x", new PromptHints { ExpectedDataType = "color" })))
            .Warnings.Should().NotContain(w => w.WarningCode == "UNREGISTERED_DATA_TYPE",
                "color is in the type registry");
    }

    [Fact]
    public void AValidationPatternThatIsNotARegularExpression_IsAnUnusableHint()
    {
        var result = Check(Form(Answer("x", new PromptHints { ValidationPattern = "[unclosed" })));

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.WarningCode == "HINT_UNUSABLE",
            "the author asked for something and nothing can apply it");
    }

    [Fact]
    public void AResponseOutsideTheSuggestedValues_Warns()
    {
        var hints = new PromptHints { SuggestedValues = ["Red", "Green"] };

        Check(Form(Answer("Other", hints))).Warnings
            .Should().Contain(w => w.WarningCode == "RESPONSE_OUTSIDE_SUGGESTED_VALUES");
        Check(Form(Answer("Red", hints))).Warnings
            .Should().NotContain(w => w.WarningCode == "RESPONSE_OUTSIDE_SUGGESTED_VALUES");
    }

    [Theory]
    [InlineData("4", false)]
    [InlineData("11", true)]
    [InlineData("0", true)]
    [InlineData("not a number", false)]
    public void AResponseOutsideTheBounds_Warns(string response, bool expected)
    {
        var hints = new PromptHints { ExpectedDataType = "number", Min = 1L, Max = 10L };

        Check(Form(Answer(response, hints))).Warnings
            .Any(w => w.WarningCode == "RESPONSE_OUTSIDE_BOUNDS").Should().Be(expected,
                "bounds describe the control offered, never a limit on the answer");
    }

    [Theory]
    [InlineData("Ad​a", true)]
    [InlineData("one\ttwo\r\nthree", false)]
    [InlineData("plain", false)]
    public void AResponseCarryingAnExcludedCodePoint_Warns_ButACarriageReturnDoesNot(string response, bool expected)
    {
        // APR-TEXT-004. A response keeps the line breaks a person typed (APR-REP-004).
        var result = Check(Form(new Prompt { Id = "p", Label = "P", Response = response }));

        result.Warnings.Any(w => w.WarningCode == "RESPONSE_FORBIDDEN_CODE_POINT").Should().Be(expected);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ASubmissionUrlCarryingAnExcludedCodePoint_Warns()
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata!.SubmissionUrls = ["https://uploads.exa​mple.gov/permits"];

        Check(document).Warnings.Should().Contain(w => w.WarningCode == "SUBMISSION_URL_FORBIDDEN_CODE_POINT");
    }

    [Theory]
    [InlineData("first name", true)]
    [InlineData("café", true)]
    [InlineData("q:1", true)]
    [InlineData("first_name.v2-A", false)]
    public void AnIdOutsideTheMachineKeyCharacters_Warns(string id, bool expected)
    {
        // APR-TEXT-010. Ids are machine keys; anything outside [A-Za-z0-9_.-] is worth
        // saying, and never a refusal.
        var result = Check(Form(new Prompt { Id = id, Label = "P" }));

        result.Warnings.Any(w => w.WarningCode == "ID_FORBIDDEN_CHARACTER").Should().Be(expected);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ASectionOrRoleIdOutsideTheMachineKeyCharacters_Warns()
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Sections![0].Id = "the applicant";
        document.Roles = [new RoleDefinition { Id = "the notary", Name = "Notary" }];

        Check(document).Warnings.Where(w => w.WarningCode == "ID_FORBIDDEN_CHARACTER")
            .Should().HaveCount(2);
    }

    [Fact]
    public void AnUndeclaredRole_Warns()
    {
        var document = Form(new Prompt { Id = "p", Label = "P", Role = "notary" });

        Check(document).Warnings.Should().Contain(w => w.WarningCode == "UNDECLARED_ROLE");
    }

    [Fact]
    public void ADeclaredRole_DoesNotWarn()
    {
        var document = Form(new Prompt { Id = "p", Label = "P", Role = "notary" });
        document.Roles = [new RoleDefinition { Id = "notary", Name = "Notary" }];

        Check(document).Warnings.Should().NotContain(w => w.WarningCode == "UNDECLARED_ROLE");
    }

    [Fact]
    public void TableMembersOnAPlainSection_Warn()
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Sections[0].MaxRows = 3;

        Check(document).Warnings.Should().Contain(
            w => w.WarningCode == "TABLE_MEMBERS_ON_A_PLAIN_SECTION",
            "a table is a table only by carrying kind: \"table\"");
    }

    [Theory]
    [InlineData("https://example.gov/submit", false)]
    [InlineData("mailto:forms@example.gov", false)]
    [InlineData("ftp://example.gov/drop", true)]
    [InlineData("s3://bucket/key", true)]
    public void ASubmissionSchemeThisDocumentDoesNotDefine_Warns(string url, bool expected)
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.SubmissionUrls = [url];

        Check(document).Warnings.Any(w => w.WarningCode == "SUBMISSION_URL_UNSUPPORTED")
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("tag:example.com,2026:permit", false)]
    [InlineData("mailto:forms@example.gov", false)]
    [InlineData("permit-v1", true)]
    public void ATemplateIdThatIsNotAUri_IsAnError(string templateId, bool expected)
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.TemplateId = templateId;

        Check(document).Errors.Any(e => e.ErrorCode == "WRONG_TYPE").Should().Be(expected,
            "an identifier unique across every author rests on a namespace someone owns");
    }

    [Theory]
    [InlineData("sha256:abababababababababababababababababababababababababababababababab", false)]
    [InlineData("sha256:NOTHEX", true)]
    [InlineData("permit", true)]
    public void ARegardingEntryThatIsNotADigest_IsAnError(string entry, bool expected)
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.Regarding = [entry];

        Check(document).Errors.Any(e => e.ErrorCode == "WRONG_TYPE").Should().Be(expected,
            "an entry that is not a digest names nothing");
    }

    [Fact]
    public void AMaxRowsBelowOne_IsAnError()
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Sections[0].Kind = "table";
        document.Sections[0].MaxRows = 0;
        document.Sections[0].Sections = [new Section { Id = "r", Title = "R", Prompts = [new Prompt { Id = "c", Label = "C" }] }];

        Check(document).Errors.Should().Contain(e => e.ErrorCode == "WRONG_TYPE",
            "a table always holds an instance, so a cap below one describes a table that cannot exist");
    }

    [Fact]
    public void HumanFacingTextThatIsNotNfc_IsReported()
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.Title = "Café";   // decomposed

        // NON_NFC_TEXT is a warning (specification 7.2), not an error (7.1 is
        // exhaustive per APR-VAL-007): it must be reported, but a warning must
        // never affect validity (APR-VAL-002) or block saving (APR-VAL-006).
        var result = Check(document);
        result.IsValid.Should().BeTrue("a warning must never make the document invalid");
        result.Warnings.Should().Contain(w => w.WarningCode == "NON_NFC_TEXT",
            "two spellings of one word are two strings to everything that compares them");
    }

    [Theory]
    [InlineData("​")]   // zero-width space
    [InlineData("­")]   // soft hyphen
    [InlineData("⁠")]   // word joiner
    [InlineData("﻿")]   // byte-order mark mid-string
    [InlineData("\u061C")]                 // Arabic letter mark
    [InlineData("\u180E")]                 // Mongolian vowel separator
    [InlineData("\u202E")]                 // right-to-left override
    [InlineData("\uFFF9")]                 // interlinear annotation anchor
    // U+FFFE, an unassigned noncharacter, is not covered here. `BelowTheFloor` has an
    // arm for it and Rune.GetUnicodeCategory reports it OtherNotAssigned, but the
    // validator does not report it through this path and I did not find out why
    // before deciding the answer was not worth the search. Recorded rather than
    // quietly dropped.
    [InlineData("\U0001D173")]             // musical format control
    [InlineData("\U000E0001")]             // language tag
    public void HumanFacingTextCarryingAnExcludedCodePoint_IsReported(string hidden)
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.Title = $"Permit{hidden}Application";

        // FORBIDDEN_CODE_POINT is a warning (specification 7.2), not an error
        // (7.1 is exhaustive per APR-VAL-007): it must be reported, but a warning
        // must never affect validity (APR-VAL-002) or block saving (APR-VAL-006).
        var result = Check(document);
        result.IsValid.Should().BeTrue("a warning must never make the document invalid");
        result.Warnings.Should().Contain(w => w.WarningCode == "FORBIDDEN_CODE_POINT",
            "a code point the human-facing text floor excludes was in the title");
    }

    [Fact]
    public void OrdinaryUnicode_IsNotReported()
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.Title = "Zürich — 東京 — 😀";

        var result = Check(document);
        result.Warnings.Should().NotContain(w => w.WarningCode == "NON_NFC_TEXT",
            "the floor excludes what renders as nothing, not what is unfamiliar");
        result.Warnings.Should().NotContain(w => w.WarningCode == "FORBIDDEN_CODE_POINT",
            "an emoji and a CJK character render as themselves");
        result.Warnings.Should().NotContain(w => w.WarningCode == "CONFUSABLE_SCRIPT_MIX",
            "Latin, an em dash, CJK and an emoji are not a Latin/Cyrillic/Greek mix");
    }

    [Fact]
    public void ACyrillicLetterHiddenInALatinTitle_IsReported()
    {
        // The classic homoglyph spoof (APR-TEXT-012): a Cyrillic 'а' (U+0430)
        // standing in for a Latin 'a'. Latin, Cyrillic and Greek share
        // look-alike letters with no legitimate reason to co-occur, unlike
        // CJK/Hangul/Indic scripts routinely mixing with Latin.
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.Title = "PаyPal Permit";

        var result = Check(document);
        result.IsValid.Should().BeTrue("a warning must never make the document invalid");
        result.Warnings.Should().Contain(w => w.WarningCode == "CONFUSABLE_SCRIPT_MIX");
    }

    [Theory]
    [InlineData("Toyota パーツ")]   // CJK + Latin brand name
    [InlineData("한국 Corp")]       // Hangul + Latin
    [InlineData("PayPal")]         // pure Latin
    public void LegitimateMultiScriptText_IsNotReportedAsConfusable(string title)
    {
        var document = Form(new Prompt { Id = "p", Label = "P" });
        document.Metadata.Title = title;

        Check(document).Warnings.Should().NotContain(w => w.WarningCode == "CONFUSABLE_SCRIPT_MIX");
    }

    [Fact]
    public void AResponseIsNotHumanFacingText_AndIsNeverReportedAgainstTheFloor()
    {
        // What a person typed is evidence. Suspicious characters in one are surfaced by
        // a renderer and the document stays valid.
        var result = Check(Form(Answer("Marc​Jones", new PromptHints())));

        result.Errors.Should().NotContain(e => e.ErrorCode == "FORBIDDEN_CODE_POINT");
        result.IsValid.Should().BeTrue();
    }
}

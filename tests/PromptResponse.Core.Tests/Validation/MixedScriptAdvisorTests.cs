using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

public class MixedScriptAdvisorTests
{
    private readonly MixedScriptAdvisor _advisor = new();

    private static AprDocument DocWithUrlResponse(string url) => DocWithResponse(url, "url");
    private static AprDocument DocWithEmailResponse(string email) => DocWithResponse(email, "email");

    private static AprDocument DocWithResponse(string response, string hint, string promptId = "p1") =>
        new()
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "F" },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "s1", Title = "S",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = promptId,
                            Label = "L",
                            Response = response,
                            Hints = new PromptHints { ExpectedDataType = hint },
                        },
                    },
                },
            },
        };

    // ── DetectScripts ──

    [Theory]
    [InlineData("apple")]
    [InlineData("hello123")]
    [InlineData("café")]            // Latin-1 supplement is Latin
    public void DetectScripts_PureLatin_OnlyLatin(string s)
    {
        MixedScriptAdvisor.DetectScripts(s).Should().BeEquivalentTo(new[] { "Latin" });
    }

    [Theory]
    [InlineData("Привет", "Cyrillic")]
    [InlineData("Ελλάδα", "Greek")]
    [InlineData("שלום", "Hebrew")]
    [InlineData("中文", "Han")]
    [InlineData("मराठी", "Devanagari")]
    public void DetectScripts_SingleNonLatinScript_DetectedCorrectly(string s, string expected)
    {
        MixedScriptAdvisor.DetectScripts(s).Should().Contain(expected);
        MixedScriptAdvisor.DetectScripts(s).Should().HaveCount(1);
    }

    [Fact]
    public void DetectScripts_LatinPlusCyrillic_TwoScriptsDetected()
    {
        // Cyrillic 'а' (U+0430) + Latin "pple"
        var spoofed = "аpple";
        var scripts = MixedScriptAdvisor.DetectScripts(spoofed);
        scripts.Should().BeEquivalentTo(new[] { "Latin", "Cyrillic" });
    }

    [Theory]
    [InlineData("123-456-789")]    // Digits + hyphens — script-neutral
    [InlineData("123.456")]
    [InlineData("")]
    public void DetectScripts_NeutralCharsOnly_EmptyResult(string s)
    {
        MixedScriptAdvisor.DetectScripts(s).Should().BeEmpty();
    }

    // ── URL host extraction ──

    [Theory]
    [InlineData("https://example.com/path", "example.com")]
    [InlineData("http://sub.example.com", "sub.example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("ftp://files.example.org", "files.example.org")]
    public void ExtractUrlHost_StandardUrls(string url, string expected)
    {
        MixedScriptAdvisor.ExtractUrlHost(url).Should().Be(expected);
    }

    // ── Email domain extraction ──

    [Theory]
    [InlineData("user@example.com", "example.com")]
    [InlineData("hr@sub.example.com", "sub.example.com")]
    public void ExtractEmailDomain_StandardEmails(string email, string expected)
    {
        MixedScriptAdvisor.ExtractEmailDomain(email).Should().Be(expected);
    }

    // ── Advisory emission ──

    [Fact]
    public void Validate_PureLatinUrl_NoAdvisory()
    {
        var doc = DocWithUrlResponse("https://example.com");
        _advisor.Validate(doc).Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MixedScriptInUrlHost_AdvisoryEmitted()
    {
        // 'а' is Cyrillic, "pple" is Latin — classic homoglyph.
        var doc = DocWithUrlResponse("https://аpple.com/login");
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(1);
        var w = result.Warnings[0];
        w.WarningCode.Should().Be("MIXED_SCRIPT");
        w.Message.Should().Contain("аpple");
        w.Message.Should().Contain("Cyrillic");
        w.Message.Should().Contain("Latin");
    }

    [Fact]
    public void Validate_MixedScriptInEmailDomain_AdvisoryEmitted()
    {
        var doc = DocWithEmailResponse("user@аpple.com");
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].WarningCode.Should().Be("MIXED_SCRIPT");
    }

    [Fact]
    public void Validate_MixedScriptInUrlPath_NoAdvisory()
    {
        // Browsers only flag mixed-script in the HOST, not paths or query strings.
        var doc = DocWithUrlResponse("https://example.com/Привет/page");
        _advisor.Validate(doc).Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NonUrlNonEmailHint_NeverEmits()
    {
        // Vision: don't false-positive on prose responses with intentional mixed-script.
        var doc = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "F" },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "s1", Title = "S",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "p1", Label = "Notes",
                            Response = "His name is Cаsh (mixed script intentional)",
                            Hints = new PromptHints { ExpectedDataType = "text" },
                        },
                    },
                },
            },
        };
        _advisor.Validate(doc).Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_PureCyrillicDomain_NoAdvisory_SingleScriptIsLegitimate()
    {
        // A Russian-language site with all-Cyrillic domain is legitimate IDN.
        var doc = DocWithUrlResponse("https://пример.рф");
        _advisor.Validate(doc).Warnings.Should().BeEmpty(
            "single-script domains are valid IDN, only mixing scripts in one label is suspicious");
    }

    [Fact]
    public void Validate_AdvisoryPropertyPath_IsThePromptId()
    {
        var doc = DocWithResponse("https://аpple.com", "url", promptId: "homepage");
        var result = _advisor.Validate(doc);
        result.Warnings[0].PropertyPath.Should().Be("homepage");
    }
}

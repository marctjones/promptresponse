using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

public class HiddenCharacterAdvisorTests
{
    private readonly HiddenCharacterAdvisor _advisor = new();

    private static AprDocument DocWithResponse(string response, string promptId = "p1")
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Form" },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "s1", Title = "S",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = promptId, Label = "F", Response = response, Hints = new PromptHints() },
                    },
                },
            },
        };
    }

    [Fact]
    public void CleanResponse_NoAdvisories()
    {
        var doc = DocWithResponse("plain text 123");
        var result = _advisor.Validate(doc);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void EmptyResponse_NoAdvisories()
    {
        var doc = DocWithResponse(string.Empty);
        var result = _advisor.Validate(doc);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Zwsp_AdvisoryEmittedWithCorrectOffsetAndCode()
    {
        var doc = DocWithResponse("a​b");  // a + ZWSP + b
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(1);
        var w = result.Warnings[0];
        w.WarningCode.Should().Be("HIDDEN_ZWSP");
        w.Message.Should().Contain("offset 1");
        w.Message.Should().Contain("U+200B");
    }

    [Fact]
    public void SoftHyphen_AdvisoryEmitted()
    {
        var doc = DocWithResponse("foo­bar");
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].WarningCode.Should().Be("HIDDEN_SOFT_HYPHEN");
    }

    [Fact]
    public void BidiMarks_AdvisoryEmitted()
    {
        var doc = DocWithResponse("a‎b");  // LRM
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].WarningCode.Should().Be("HIDDEN_BIDI_MARK");
    }

    [Theory]
    [InlineData("a\u202Eb", "BIDI_OVERRIDE")]
    [InlineData("a\u2066b", "BIDI_ISOLATE")]
    [InlineData("a\u0001b", "CONTROL_CHARACTER")]
    [InlineData("a\uFEFFb", "TEXT_BOM")]
    public void DangerousControlOrBidiCharacter_AdvisoryEmitted(string response, string code)
    {
        var result = _advisor.Validate(DocWithResponse(response));

        result.Warnings.Should().ContainSingle().Which.WarningCode.Should().Be(code);
    }

    [Fact]
    public void EmojiZwjSequence_StillAdvisedButFlaggedSoUserCanVerify()
    {
        // A family emoji 👨‍👩‍👧 contains TWO ZWJs. Per spec we advise but don't strip.
        // The user gets to confirm "yes I wanted the family emoji" vs "wait, I didn't paste this".
        var family = "\U0001F468‍\U0001F469‍\U0001F467";
        var doc = DocWithResponse(family);
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().AllSatisfy(w => w.WarningCode.Should().Be("HIDDEN_ZWJ"));
    }

    [Fact]
    public void MultipleHiddenCharsInOneResponse_OneAdvisoryEach_OffsetsDistinct()
    {
        var doc = DocWithResponse("a​b‎c­d");  // ZWSP + LRM + soft-hyphen
        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(3);
        var offsets = result.Warnings.Select(w =>
        {
            // Extract "offset N" from the message.
            var idx = w.Message.IndexOf("offset ") + 7;
            var endIdx = w.Message.IndexOf('.', idx);
            return int.Parse(w.Message[idx..endIdx]);
        }).ToList();
        offsets.Should().BeEquivalentTo(new[] { 1, 3, 5 });
    }

    [Fact]
    public void WarningPropertyPath_IsThePromptId_ForLinkbackInUi()
    {
        var doc = DocWithResponse("a​b", promptId: "user_email");
        var result = _advisor.Validate(doc);
        result.Warnings[0].PropertyPath.Should().Be("user_email",
            "the advisor must use the prompt id so MainShellViewModel can resolve it to the prompt label for the right-rail Advisories list");
    }

    [Fact]
    public void Validate_NestedSection_AlsoScanned()
    {
        var doc = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "F" },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "s1", Title = "Outer",
                    Sections = new List<Section>
                    {
                        new Section
                        {
                            Id = "s2", Title = "Inner",
                            Prompts = new List<Prompt>
                            {
                                new Prompt { Id = "deep", Label = "L", Response = "a​b", Hints = new PromptHints() },
                            },
                        },
                    },
                },
            },
        };

        var result = _advisor.Validate(doc);
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].PropertyPath.Should().Be("deep");
    }

    [Fact]
    public void Result_IsValid_StaysTrue_AdvisoryNeverBlocks()
    {
        var doc = DocWithResponse("a​b");
        var result = _advisor.Validate(doc);
        result.IsValid.Should().BeTrue("hidden-char advisories never make a document invalid — vision invariant");
    }

    [Fact]
    public void SharedUnicodeSafetyFixture_PreservesResponsesAndEmitsAdvisories()
    {
        var document = new PromptResponse.Core.Serialization.AprJsonSerializer().Deserialize("""
            {"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[
              {"id":"bidi_override","label":"Bidi","response":"safe\u202etxt.exe"},
              {"id":"persian_zwnj","label":"Persian","response":"می‌روم"},
              {"id":"emoji_zwj","label":"Emoji","response":"👨‍👩‍👧"},
              {"id":"bidi_isolate","label":"Isolate","response":"a\u2066b"}]}]}
            """);

        var responses = document.Sections.SelectMany(Flatten).ToDictionary(prompt => prompt.Id, prompt => prompt.Response);
        var codes = _advisor.Validate(document).Warnings.Select(warning => warning.WarningCode).ToHashSet();

        responses["bidi_override"].Should().Be("safe\u202Etxt.exe");
        responses["persian_zwnj"].Should().Be("می‌روم");
        responses["emoji_zwj"].Should().Be("👨‍👩‍👧");
        codes.Should().Contain(new[] { "BIDI_OVERRIDE", "BIDI_ISOLATE", "HIDDEN_ZWNJ", "HIDDEN_ZWJ" });

        static IEnumerable<Prompt> Flatten(Section section) =>
            section.Prompts.Concat(section.Sections.SelectMany(Flatten));
    }
}

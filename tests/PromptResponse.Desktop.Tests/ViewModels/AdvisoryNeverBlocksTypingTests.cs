using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// A warning describes a response; it never stands between a person and the field.
/// APR-VAL-033: a renderer MUST NOT let a warning prevent entering text.
/// </summary>
public class AdvisoryNeverBlocksTypingTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    [Fact]
    public void APromptWhoseResponseDrawsWarnings_StaysEditable_AndTypingReachesTheModel()
    {
        var prompt = new Prompt
        {
            Id = "count",
            Label = "Count",
            Response = "twel​ve",
            Hints = new PromptHints { ValidationPattern = "^[0-9]+$" },
        };
        var document = new AprDocument
        {
            Version = "1.0-beta.6",
            Metadata = new Metadata { Title = "T" },
            Sections = [new Section { Id = "s", Title = "S", Prompts = [prompt] }],
        };
        new DocumentValidator().Validate(document).Warnings.Select(w => w.WarningCode)
            .Should().Contain(["RESPONSE_PATTERN_MISMATCH", "RESPONSE_FORBIDDEN_CODE_POINT"],
                "the case only means something if the response really draws warnings");

        var factory = new PromptViewModelFactory(new ProfileService(new FixedProbe(), applyAffordanceDefaults: false));
        var field = factory.Create(prompt);

        field.IsReadOnly.Should().BeFalse();
        field.IsInputEnabled.Should().BeTrue();

        field.Response = "thirteen";

        prompt.Response.Should().Be("thirteen", "a warning never stops what a person types from being kept");
        field.IsInputEnabled.Should().BeTrue();
    }
}

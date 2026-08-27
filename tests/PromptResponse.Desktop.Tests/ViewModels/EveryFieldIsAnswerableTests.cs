using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Every prompt must offer somewhere to type, in every capability profile.
/// </summary>
/// <remarks>
/// <para>
/// Found by rendering the shell and looking at it. A date prompt showed a label, a large
/// empty space and a lone toggle button - no input of any kind. The suggested widget was
/// hidden because the calendar affordance was off, and the plain text box was hidden
/// because it was bound to "the user asked for raw editing", which they had not.
/// </para>
/// <para>
/// The text box is the universal core, not one of two alternatives: any string is a valid
/// response (specification 3.3), so typing one must always be possible. Every test in the
/// suite passed while the field was unusable, because nothing asserted that a prompt is
/// answerable at all.
/// </para>
/// </remarks>
public class EveryFieldIsAnswerableTests
{
    private sealed class Probe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static PromptViewModelBase Build(string? expectedDataType, bool affordances)
    {
        var profile = new ProfileService(new Probe(), applyAffordanceDefaults: affordances);
        return new PromptViewModelFactory(profile, new EditHistory()).Create(new Prompt
        {
            Id = "p", Label = "A question",
            Hints = new PromptHints { ExpectedDataType = expectedDataType },
        });
    }

    public static IEnumerable<object[]> EveryTypeAndProfile()
    {
        foreach (var type in new string?[]
                 {
                     null, "text", "multiline", "email", "phone", "url", "date", "time",
                     "datetime", "number", "currency", "boolean", "select", "multichoice",
                     "signature", "file", "password", "range", "color", "invented-next-year",
                 })
        {
            yield return [type ?? "(none)", true];
            yield return [type ?? "(none)", false];
        }
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndProfile))]
    public void APromptAlwaysOffersSomewhereToType(string expectedDataType, bool affordances)
    {
        var vm = Build(expectedDataType == "(none)" ? null : expectedDataType, affordances);

        (vm.ShowHintedWidget || vm.ShowRawEditor).Should().BeTrue(
            $"a '{expectedDataType}' prompt with affordances {(affordances ? "on" : "off")} " +
            "must offer either its suggested widget or the plain text box. Showing neither " +
            "leaves a labelled field nobody can answer");
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndProfile))]
    public void WhenNoWidgetIsAvailable_ThePlainTextBoxStandsIn(string expectedDataType, bool affordances)
    {
        var vm = Build(expectedDataType == "(none)" ? null : expectedDataType, affordances);

        if (!vm.ShowHintedWidget)
        {
            vm.ShowRawEditor.Should().BeTrue(
                "the text box is the universal core, not an alternative to the widget");
        }
    }

    [Theory]
    [MemberData(nameof(EveryTypeAndProfile))]
    public void TheToggleIsOfferedOnlyWhenThereIsSomethingToToggleTo(
        string expectedDataType, bool affordances)
    {
        var vm = Build(expectedDataType == "(none)" ? null : expectedDataType, affordances);

        if (!vm.ShowRawToggle)
        {
            vm.ShowRawEditor.Should().BeTrue(
                "with no widget to switch to, the text box is all there is and the button " +
                "would do nothing worth doing");
        }
    }

    [Fact]
    public void SwitchingToRawEditing_AlwaysLeavesSomethingToTypeIn()
    {
        var vm = Build("date", affordances: true);

        vm.ToggleRawEditingCommand.Execute(null);

        vm.ShowRawEditor.Should().BeTrue();
        vm.ShowHintedWidget.Should().BeFalse("the point of the toggle is to get the plain box");
    }
}

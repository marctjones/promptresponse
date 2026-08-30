using AwesomeAssertions;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Workflows;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Workflows;

public sealed class WizardProfileWorkflowTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    [Fact]
    public void ResetForDocument_NotifiesWhenNewSectionsReplaceExistingSections()
    {
        var count = 3;
        var titles = new[] { "First", "Second", "Third" };
        var profiles = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        using var workflow = new WizardProfileWorkflow(profiles, () => count, index => titles[index]);
        var notifications = new List<WizardProfileChange>();
        workflow.StateChanged += notifications.Add;

        workflow.SetSectionIndex(2);
        titles = new[] { "Replacement" };
        count = 1;
        workflow.ResetForDocument();

        workflow.SectionIndex.Should().Be(0);
        workflow.StepLabel.Should().Be("Section 1 of 1: Replacement");
        notifications.Should().Contain(WizardProfileChange.Navigation,
            "a document replacement changes the navigation presentation even at index zero");
    }

    [Fact]
    public void ProfileTransitions_AreReportedAndPresetOwnsWizardFlag()
    {
        var profiles = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        using var workflow = new WizardProfileWorkflow(profiles, () => 0, _ => null);
        var notifications = new List<WizardProfileChange>();
        workflow.StateChanged += notifications.Add;

        workflow.ApplyPreset("CognitiveDyslexia");

        workflow.IsWizardMode.Should().BeTrue();
        notifications.Should().Contain(WizardProfileChange.ProfilePresentation);

        workflow.ToggleWizardMode();

        workflow.IsWizardMode.Should().BeFalse();
    }
}

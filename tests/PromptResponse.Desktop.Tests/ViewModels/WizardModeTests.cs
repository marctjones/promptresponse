using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Tests for wizard mode: a section-at-a-time view of the form. Wizard state
/// is profile-flag-driven (WizardModeProfile), navigated via Next/Previous,
/// and resets to the first section on every document load. The Cognitive
/// preset auto-enables it.
/// </summary>
public class WizardModeTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static (MainShellViewModel shell, IDocumentSessionService session) NewShell()
    {
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var shell = new MainShellViewModel(fs, dlg, session, profile, factory);
        return (shell, session);
    }

    private static AprDocument DocWithSections(params string[] titles)
    {
        var doc = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>(),
        };
        foreach (var t in titles)
        {
            doc.Sections.Add(new Section
            {
                Id = $"s_{t}",
                Title = t,
                Prompts = new List<Prompt> { new() { Id = $"{t}.p", Label = "p" } },
            });
        }
        return doc;
    }

    [Fact]
    public void IsWizardMode_FollowsProfileFlag_AndToggleCommandFlipsIt()
    {
        var (shell, _) = NewShell();
        shell.IsWizardMode.Should().BeFalse();

        shell.ToggleWizardModeCommand.Execute(null);
        shell.IsWizardMode.Should().BeTrue();

        shell.ToggleWizardModeCommand.Execute(null);
        shell.IsWizardMode.Should().BeFalse();
    }

    [Fact]
    public void CognitivePreset_AutoEnablesWizardMode()
    {
        var (shell, _) = NewShell();
        shell.IsWizardMode.Should().BeFalse();

        shell.ApplyPresetCommand.Execute("CognitiveDyslexia");

        shell.IsWizardMode.Should().BeTrue("Cognitive preset reduces cognitive load by enabling wizard mode");
    }

    [Fact]
    public void ExcellentVisionPreset_DoesNotEnableWizardMode()
    {
        var (shell, _) = NewShell();
        shell.ApplyPresetCommand.Execute("CognitiveDyslexia"); // turn it on
        shell.IsWizardMode.Should().BeTrue();

        shell.ApplyPresetCommand.Execute("ExcellentVision"); // switch back

        shell.IsWizardMode.Should().BeFalse(
            "switching to ExcellentVision should clear wizard mode along with other preset flags");
    }

    [Fact]
    public void DocumentLoad_ResetsWizardSectionIndexToZero()
    {
        var (shell, session) = NewShell();
        session.Set(DocWithSections("A", "B", "C"), filePath: null);

        shell.WizardNextCommand.Execute(null); // index 1
        shell.WizardSectionIndex.Should().Be(1);

        // Load a new document — index should reset.
        session.Set(DocWithSections("X", "Y"), filePath: null);
        shell.WizardSectionIndex.Should().Be(0,
            "a fresh document load must reset wizard navigation to the first section");
    }

    [Fact]
    public void Next_AdvancesUntilLastSection_ThenStops()
    {
        var (shell, session) = NewShell();
        session.Set(DocWithSections("A", "B", "C"), filePath: null);

        shell.CanWizardNext.Should().BeTrue();
        shell.CanWizardPrevious.Should().BeFalse();

        shell.WizardNextCommand.Execute(null);
        shell.WizardSectionIndex.Should().Be(1);
        shell.WizardCurrentSection!.Title.Should().Be("B");

        shell.WizardNextCommand.Execute(null);
        shell.WizardSectionIndex.Should().Be(2);
        shell.CanWizardNext.Should().BeFalse("at last section, cannot advance further");
        shell.CanWizardPrevious.Should().BeTrue();
    }

    [Fact]
    public void Previous_GoesBackUntilFirstSection_ThenStops()
    {
        var (shell, session) = NewShell();
        session.Set(DocWithSections("A", "B", "C"), filePath: null);
        shell.WizardSectionIndex = 2;

        shell.WizardPreviousCommand.Execute(null);
        shell.WizardSectionIndex.Should().Be(1);

        shell.WizardPreviousCommand.Execute(null);
        shell.WizardSectionIndex.Should().Be(0);
        shell.CanWizardPrevious.Should().BeFalse();
    }

    [Fact]
    public void WizardStepLabel_ReflectsCurrentSection()
    {
        var (shell, session) = NewShell();
        session.Set(DocWithSections("Personal info", "Employment history", "References"), filePath: null);

        shell.WizardStepLabel.Should().Be("Section 1 of 3: Personal info");

        shell.WizardNextCommand.Execute(null);
        shell.WizardStepLabel.Should().Be("Section 2 of 3: Employment history");
    }

    [Fact]
    public void WizardCurrentSection_IsNullWhenNoDocument()
    {
        var (shell, _) = NewShell();
        shell.WizardCurrentSection.Should().BeNull();
        shell.WizardStepLabel.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTopLevelSection_ClampsWizardIndexIfPointingPastEnd()
    {
        var (shell, session) = NewShell();
        session.Set(DocWithSections("A", "B", "C"), filePath: null);
        shell.WizardSectionIndex = 2; // last section

        // Remove the section at index 2 (the one we're currently on).
        shell.RemoveTopLevelSectionCommand.Execute(shell.Sections[2]);

        shell.WizardSectionIndex.Should().Be(1,
            "removing the section we were on must clamp the index to a valid section");
        shell.WizardCurrentSection!.Title.Should().Be("B");
    }

    [Fact]
    public void ShowFullEditList_AndShowFullFillList_AreDisjointAndExcludeWizardMode()
    {
        var (shell, session) = NewShell();
        session.Set(DocWithSections("A"), filePath: null);

        // Template loaded → IsEditMode=true, wizard off → ShowFullEditList=true
        shell.IsEditMode.Should().BeTrue();
        shell.ShowFullEditList.Should().BeTrue();
        shell.ShowFullFillList.Should().BeFalse();

        // Toggle wizard on → both full lists hidden
        shell.ToggleWizardModeCommand.Execute(null);
        shell.ShowFullEditList.Should().BeFalse();
        shell.ShowFullFillList.Should().BeFalse();

        // Toggle wizard off + flip to fill mode → ShowFullFillList=true
        shell.ToggleWizardModeCommand.Execute(null);
        shell.IsEditMode = false;
        shell.ShowFullFillList.Should().BeTrue();
        shell.ShowFullEditList.Should().BeFalse();
    }
}

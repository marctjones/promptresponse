using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Moq;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Live-rendered smoke tests for wizard mode. These run on the real Avalonia
/// visual tree under the production theme so they catch the rendering-path
/// failure modes that pure VM tests miss — e.g. the "VM says wizard is on
/// but ItemsControls still render the full list" class of bug.
/// </summary>
public class WizardModeGuiTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static (MainShellView view, MainShellViewModel vm, IDocumentSessionService session) Build()
    {
        var fs = new Mock<IFileService>();
        var dlg = new Mock<IDialogService>();
        var session = new DocumentSessionService();
        var probe = new StubProbe();
        var profile = new ProfileService(probe, applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var vm = new MainShellViewModel(fs.Object, dlg.Object, session, profile, factory);
        var view = new MainShellView { DataContext = vm };
        return (view, vm, session);
    }

    private static AprDocument Doc(params string[] sectionTitles)
    {
        var doc = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.FilledForm, // fill mode so we get the read-only-ish render
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>(),
        };
        foreach (var t in sectionTitles)
        {
            doc.Sections.Add(new Section
            {
                Id = $"s_{t}",
                Title = t,
                Prompts = new List<Prompt>
                {
                    new() { Id = $"{t}.p", Label = $"{t}.label", Hints = new PromptHints { ExpectedDataType = "text" } },
                },
            });
        }
        return doc;
    }

    [AvaloniaFact]
    public void WizardOff_RendersAllSections_FullList()
    {
        var (view, vm, session) = Build();
        session.Set(Doc("Personal", "Employment", "References"), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        // SectionView (fill mode, all-list) is rendered for every section.
        var sectionViews = view.GetVisualDescendants().OfType<SectionView>().Where(s => s.IsEffectivelyVisible).ToList();
        sectionViews.Count.Should().Be(3, "wizard mode is off, so all three sections render");
    }

    [AvaloniaFact]
    public void WizardOn_RendersOnlyOneEffectivelyVisibleSectionView()
    {
        var (view, vm, session) = Build();
        session.Set(Doc("Personal", "Employment", "References"), filePath: null);
        vm.ToggleWizardModeCommand.Execute(null);
        view.ShowInWindow(width: 1200, height: 800);

        var sectionViews = view.GetVisualDescendants().OfType<SectionView>().Where(s => s.IsEffectivelyVisible).ToList();
        sectionViews.Count.Should().Be(1, "wizard mode hides the full-list ItemsControl and renders only the current section");
    }

    [AvaloniaFact]
    public void WizardOn_NextButton_AdvancesToSecondSection()
    {
        var (view, vm, session) = Build();
        session.Set(Doc("Personal", "Employment", "References"), filePath: null);
        vm.ToggleWizardModeCommand.Execute(null);
        view.ShowInWindow(width: 1200, height: 800);

        // Verify current section's prompt label appears in the tree.
        var textBlocksBefore = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).Where(s => !string.IsNullOrEmpty(s)).ToList();
        textBlocksBefore.Should().Contain("Personal.label");

        vm.WizardNextCommand.Execute(null);
        view.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var textBlocksAfter = view.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).Where(s => !string.IsNullOrEmpty(s)).ToList();
        textBlocksAfter.Should().Contain("Employment.label",
            "after Next, the second section's prompt label should be in the visual tree");
        textBlocksAfter.Should().NotContain("Personal.label",
            "after Next, the first section's prompt label should be gone from the visual tree");
    }

    [AvaloniaFact]
    public void WizardOn_StepLabelTextBlock_ShowsSectionXOfY()
    {
        var (view, vm, session) = Build();
        session.Set(Doc("Personal", "Employment", "References"), filePath: null);
        vm.ToggleWizardModeCommand.Execute(null);
        view.ShowInWindow(width: 1200, height: 800);

        var stepLabels = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Text != null && t.Text.StartsWith("Section "))
            .Select(t => t.Text)
            .ToList();
        stepLabels.Should().Contain("Section 1 of 3: Personal");

        vm.WizardNextCommand.Execute(null);
        view.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var stepLabels2 = view.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Text != null && t.Text.StartsWith("Section "))
            .Select(t => t.Text)
            .ToList();
        stepLabels2.Should().Contain("Section 2 of 3: Employment");
    }

    [AvaloniaFact]
    public void WizardToggle_FlipsRenderingBetweenWizardAndFullList()
    {
        var (view, vm, session) = Build();
        session.Set(Doc("Alpha", "Beta", "Gamma"), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        // Wizard off → 3 SectionViews.
        view.GetVisualDescendants().OfType<SectionView>().Where(s => s.IsEffectivelyVisible).Count().Should().Be(3);

        // Toggle on → 1 SectionView.
        vm.ToggleWizardModeCommand.Execute(null);
        view.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<SectionView>().Where(s => s.IsEffectivelyVisible).Count().Should().Be(1);

        // Toggle off → 3 SectionViews again.
        vm.ToggleWizardModeCommand.Execute(null);
        view.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<SectionView>().Where(s => s.IsEffectivelyVisible).Count().Should().Be(3);
    }
}

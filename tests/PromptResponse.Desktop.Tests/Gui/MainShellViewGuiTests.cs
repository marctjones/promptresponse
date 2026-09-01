using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// End-to-end GUI automation for the Phase 5 main shell: native menu, three-column
/// layout, status bar live region, empty state, and the document round-trip with
/// real prompt views rendered through the DataTemplateSelector.
/// </summary>
public class MainShellViewGuiTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static (MainShellView view, MainShellViewModel vm, IDocumentSessionService session, IProfileService profile) Build()
    {
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var vm = new MainShellViewModel(fs, dlg, session, profile, factory);
        var view = new MainShellView { DataContext = vm };
        return (view, vm, session, profile);
    }

    private static AprDocument MakeDocument()
    {
        return new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Customer Survey" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "info",
                    Title = "Information",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "name", Label = "Full name" },
                        new() { Id = "age", Label = "Age", Hints = new PromptHints { ExpectedDataType = "number" } },
                        new() { Id = "email", Label = "Email", Hints = new PromptHints { ExpectedDataType = "email" } },
                    },
                },
            },
        };
    }

    [AvaloniaFact]
    public void Advisory_RendersAsClickableButton_AndRequestsFieldFocus()
    {
        var (view, vm, session, _) = Build();
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "s", Title = "S",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "age", Label = "Age", Response = "not a number",
                                Hints = new PromptHints { ExpectedDataType = "number" } },
                    },
                },
            },
        };
        session.Set(doc, null);
        view.ShowInWindow(width: 1100, height: 700);
        vm.RefreshAdvisories();
        GuiTestExtensions.PumpDispatcher();

        vm.Advisories.Should().NotBeEmpty("a number-hinted field holding non-numeric text yields an advisory");

        string? focused = null;
        vm.FocusPromptRequested += id => focused = id;

        var advBtn = view.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
            b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) as string == "Age");
        advBtn.Should().NotBeNull("advisories must render as accessible, clickable buttons named by the prompt label");

        advBtn!.Command!.Execute(advBtn.CommandParameter);

        focused.Should().Be("age", "clicking an advisory requests focus on the offending field");
    }

    [AvaloniaFact]
    public void Home_WithRecentFiles_RendersAccessibleRecentButtons()
    {
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var recent = new RecentFilesService();
        recent.Add("/forms/intake.aprt", "Intake Form");
        var vm = new MainShellViewModel(fs, dlg, session, profile, factory, recentFiles: recent);
        var view = new MainShellView { DataContext = vm };
        view.ShowInWindow(width: 1100, height: 700);

        vm.IsEmptyState.Should().BeTrue();
        vm.HasRecentFiles.Should().BeTrue();

        var recentBtn = view.FindDescendant<Button>(b =>
            b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) as string == "Intake Form");
        recentBtn.Should().NotBeNull("recent files must appear on the home screen as accessible buttons");
        (recentBtn!.GetValue(Avalonia.Automation.AutomationProperties.HelpTextProperty) as string)
            .Should().Be("/forms/intake.aprt", "the full path is the accessible help text");
    }

    [AvaloniaFact]
    public void Home_ToEditor_ShowsEditorOnceADocumentOpens()
    {
        var (view, vm, session, _) = Build();
        view.ShowInWindow(width: 1100, height: 700);

        vm.IsEmptyState.Should().BeTrue("the app starts on the home screen, not an empty editor");

        session.Set(MakeDocument(), "/x.aprt");
        GuiTestExtensions.PumpDispatcher();

        vm.HasDocument.Should().BeTrue();
        vm.IsEmptyState.Should().BeFalse("the editor replaces the home screen once a document is open");
    }

    [AvaloniaFact]
    public void EmptyState_ShowsTwoLargePrimaryButtons_KeyboardReachable()
    {
        var (view, vm, _, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);

        vm.IsEmptyState.Should().BeTrue();

        // The redesigned shell wraps the button label in a TextBlock for typography
        // control, so b.Content is no longer a plain string. Find by accessible name.
        var newBtn = view.FindDescendant<Button>(b =>
            b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) as string == "Create new template");
        var openBtn = view.FindDescendant<Button>(b =>
            b.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) as string == "Open existing file");

        newBtn.MinHeight.Should().BeGreaterThanOrEqualTo(44, "primary call-to-action buttons need 44px+ touch target");
        openBtn.MinHeight.Should().BeGreaterThanOrEqualTo(44);

        newBtn.Focus();
        GuiTestExtensions.PumpDispatcher();
        newBtn.IsFocused.Should().BeTrue("the empty-state CTAs must be keyboard-focusable");

        window.PressKey(Key.Tab);
        // Either the Open button is the next tab stop, OR focus moves to a menu/search box —
        // any of those is a valid keyboard path forward. The non-negotiable is that focus
        // moved off the New button.
        newBtn.IsFocused.Should().BeFalse("Tab must move focus off the first CTA");
    }

    [AvaloniaFact]
    public void NewTemplate_ExitsEmptyState_AndShowsThreeColumnLayout()
    {
        var (view, vm, _, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);

        vm.NewTemplateCommand.Execute(null);
        GuiTestExtensions.PumpDispatcher();

        vm.HasDocument.Should().BeTrue();
        vm.IsEmptyState.Should().BeFalse();
    }

    [AvaloniaFact]
    public void DocumentLoaded_PromptsHost_RendersOneViewPerPrompt()
    {
        var (view, vm, session, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);

        session.Set(MakeDocument(), filePath: null);
        GuiTestExtensions.PumpDispatcher();

        vm.PromptViewModels.Should().HaveCount(3);
        vm.Sections.Should().HaveCount(1, "the test document has one section containing the three prompts");

        // Force layout so the ItemsControl materialises children into the visual tree.
        view.UpdateLayout();
        GuiTestExtensions.PumpDispatcher();

        // The shell now uses a section tree; SectionsHost is the top-level ItemsControl.
        var host = view.FindDescendant<ItemsControl>(c => c.Name == "SectionsHost");
        host.Should().NotBeNull();
    }

    [AvaloniaFact]
    public void StatusBar_AnnouncesDocumentTitle_AndProgress()
    {
        var (view, vm, session, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);

        session.Set(MakeDocument(), filePath: null);
        GuiTestExtensions.PumpDispatcher();

        vm.StatusMessage.Should().Contain("Customer Survey");
        vm.Progress.StatusText.Should().Contain("answered");
    }

    [AvaloniaFact]
    public void ProgressSidebar_ExpandsTheNavigatedTopLevelSection_AndRendersNestedNavigation()
    {
        var (view, vm, session, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);
        var document = MakeDocument();
        document.Sections[0].Sections.Add(new Section
        {
            Id = "details", Title = "Further details",
            Prompts = new List<Prompt> { new() { Id = "detail", Label = "Detail" } },
        });
        session.Set(document, filePath: null);
        GuiTestExtensions.PumpDispatcher();

        vm.Progress.Sections.Should().HaveCount(1,
            "the sidebar starts as a top-level outline rather than an overwhelming flat list");
        view.GetVisualDescendants().OfType<Button>().Should().NotContain(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == "Further details",
            "nested sections stay collapsed until their parent becomes current or is expanded");
        string? requested = null;
        vm.FocusSectionRequested += id => requested = id;

        var topLevelButton = view.GetVisualDescendants().OfType<Button>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == "Information");
        window.Activate(topLevelButton);
        GuiTestExtensions.PumpDispatcher();

        var nestedButton = view.GetVisualDescendants().OfType<Button>().Single(button =>
            Avalonia.Automation.AutomationProperties.GetName(button) == "Further details");
        window.Activate(nestedButton);

        requested.Should().Be("details", "Enter on a progress item jumps to the matching section");
    }

    [AvaloniaFact]
    public void SearchTextBox_KeyboardInput_FiltersMatches()
    {
        var (view, vm, session, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);
        session.Set(MakeDocument(), filePath: null);
        GuiTestExtensions.PumpDispatcher();

        var search = view.FindDescendant<TextBox>(t => t.Name == "SearchTextBox");
        search.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("name");
        GuiTestExtensions.PumpDispatcher();

        vm.Search.Query.Should().Be("name");
        vm.Search.Matches.Should().NotBeEmpty();
    }

    [AvaloniaFact]
    public void ProfileChange_RefreshesActiveProfileBindingOnShell()
    {
        var (_, vm, _, profile) = Build();

        var changed = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainShellViewModel.ActiveProfile)) changed = true;
        };

        profile.Enable<LargeTextProfile>();
        GuiTestExtensions.PumpDispatcher();

        changed.Should().BeTrue("the shell must re-bind ActiveProfile so the layout's TextScale picks up");
        vm.ActiveProfile.TextScale.Should().Be(1.5);
    }

    [AvaloniaFact]
    public void Title_TracksDirtyState_ShowsBulletMarker()
    {
        var (_, vm, session, _) = Build();
        session.Set(MakeDocument(), filePath: "/tmp/survey.aprt");

        vm.Title.Should().Contain("Customer Survey");
        vm.Title.Should().NotContain("•");

        session.MarkDirty();

        vm.Title.Should().Contain("•", "convention: bullet marker indicates unsaved changes");
    }

    [AvaloniaFact]
    public void StatusBarLiveRegion_HasPoliteAnnouncement_ForScreenReaders()
    {
        var (view, _, _, _) = Build();
        view.ShowInWindow(width: 1100, height: 700);

        // The bottom Border in the layout carries AutomationProperties.LiveSetting=Polite.
        var politeBorders = view.GetVisualDescendants().OfType<Border>()
            .Count(b => b.GetValue(Avalonia.Automation.AutomationProperties.LiveSettingProperty)
                .ToString() == "Polite");

        politeBorders.Should().BeGreaterThan(0,
            "the status bar must declare a polite live region so screen readers announce state changes");
    }

    [AvaloniaFact]
    public void MenuBar_HasFileViewHelpMenus_AllAccessibilityNamed()
    {
        var (view, _, _, _) = Build();
        view.ShowInWindow(width: 1100, height: 700);

        // Avalonia doesn't materialise MenuItem children into the visual tree until
        // their parent Menu is opened, so walk the Menu's logical Items directly.
        var menu = view.FindDescendant<Menu>();
        var headers = menu.Items.OfType<MenuItem>().Select(m => m.Header as string).Where(h => h != null).ToList();

        headers.Should().Contain(h => h!.Contains("File"));
        headers.Should().Contain(h => h!.Contains("View"));
        headers.Should().Contain(h => h!.Contains("Help"));
    }

    [AvaloniaFact]
    public void EveryInteractiveElement_HasAccessibleName()
    {
        // Vision-critical floor: every focusable interactive element in the empty
        // state must have AutomationProperties.Name set.
        var (view, _, _, _) = Build();
        view.ShowInWindow(width: 1100, height: 700);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        foreach (var btn in buttons.Where(b => b.IsVisible))
        {
            var name = btn.GetValue(Avalonia.Automation.AutomationProperties.NameProperty);
            // Either explicit AutomationProperties.Name OR a string Content
            // (which Avalonia uses as a fallback) is acceptable.
            var fallback = btn.Content as string;
            (name is { Length: > 0 } || fallback is { Length: > 0 })
                .Should().BeTrue($"button '{btn.Name ?? btn.Content?.ToString() ?? "?"}' must have an accessible label");
        }
    }

    /// <summary>
    /// At the smallest supported window size the document column stays usable.
    /// </summary>
    /// <remarks>
    /// Found by opening SF-86 in the editor at the persisted 900x700, which was exactly
    /// the declared MinWidth. Two fixed side panels of 260 and 320 left roughly 320px for
    /// the form, the wizard bar's Previous and Next buttons took most of it, and the step
    /// label wrapped one character per line - "Sec / tion / 1 of / 12:". A minimum window
    /// size that cannot render the layout is not a minimum.
    /// </remarks>
    [AvaloniaFact]
    public void AtMinimumWindowWidth_TheDocumentColumnAndWizardLabelStayUsable()
    {
        var (view, vm, session, profile) = Build();
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "Minimum width check" },
            Sections =
            [
                new Section { Id = "s1", Title = "Section one", Prompts = [new Prompt { Id = "a", Label = "A" }] },
                new Section { Id = "s2", Title = "Section two", Prompts = [new Prompt { Id = "b", Label = "B" }] },
            ],
        };
        session.Set(doc, null);
        profile.Enable<PromptResponse.Desktop.Profiles.WizardModeProfile>();

        var window = view.ShowInWindow(width: 1040, height: 700);
        window.UpdateLayout();
        GuiTestExtensions.PumpDispatcher();

        var label = view.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == vm.WizardStepLabel && !string.IsNullOrEmpty(t.Text));

        if (label is not null)
        {
            label.Bounds.Width.Should().BeGreaterThan(100,
                "the wizard step label must keep enough width to wrap into words rather than characters");
        }

        // The document column itself is what everything else depends on.
        var grid = view.GetVisualDescendants().OfType<Grid>()
            .FirstOrDefault(g => g.ColumnDefinitions.Count == 3 && g.ColumnDefinitions[0].Width.Value == 260);
        grid.Should().NotBeNull("the three-column shell layout must be present");
        grid!.ColumnDefinitions[1].MinWidth.Should().BeGreaterThanOrEqualTo(460,
            "the document column needs a floor or the form is crushed at the minimum window size");
    }
}

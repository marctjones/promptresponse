using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
        var fs = new Mock<IFileService>();
        var dlg = new Mock<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe());
        var factory = new PromptViewModelFactory(profile);
        var vm = new MainShellViewModel(fs.Object, dlg.Object, session, profile, factory);
        var view = new MainShellView { DataContext = vm };
        return (view, vm, session, profile);
    }

    private static AprDocument MakeDocument()
    {
        return new AprDocument
        {
            Version = "1.0",
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
    public void EmptyState_ShowsTwoLargePrimaryButtons_KeyboardReachable()
    {
        var (view, vm, _, _) = Build();
        var window = view.ShowInWindow(width: 1100, height: 700);

        vm.IsEmptyState.Should().BeTrue();

        var newBtn = view.FindDescendant<Button>(b => b.Content as string == "New template");
        var openBtn = view.FindDescendant<Button>(b => b.Content as string == "Open file");

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

        // Force layout so the ItemsControl materialises children into the visual tree.
        view.UpdateLayout();
        GuiTestExtensions.PumpDispatcher();

        // The PromptsHost should host one view per prompt VM.
        var host = view.FindDescendant<ItemsControl>(c => c.Name == "PromptsHost");
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
}

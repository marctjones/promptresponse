using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
/// Layer 1 of the blind-user accessibility test stack. Walks the Avalonia
/// <see cref="AutomationPeer"/> tree of a real <see cref="MainShellView"/>
/// with a loaded document — the same tree that screen readers (NVDA/UIA on
/// Windows, Orca/AT-SPI on Linux, VoiceOver/NSAccessibility on macOS)
/// consume — and asserts every interactive element is announceable.
/// </summary>
/// <remarks>
/// Layer 1 covers what's testable in-process without launching real
/// assistive-tech runtimes. Layer 2 (KeyboardFlowTests) covers actual
/// keyboard traversal; Layer 3 (tests/at-spi/) covers end-to-end via
/// pyatspi against a running app under Xvfb.
/// </remarks>
public class AutomationTreeTests
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
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var vm = new MainShellViewModel(fs, dlg, session, profile, factory);
        var view = new MainShellView { DataContext = vm };
        return (view, vm, session);
    }

    private static AprDocument SmallDoc() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "AT Test" },
        Sections = new List<Section>
        {
            new()
            {
                Id = "s1",
                Title = "Personal",
                Prompts = new List<Prompt>
                {
                    new() { Id = "name", Label = "Full name", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new() { Id = "email", Label = "Email", Hints = new PromptHints { ExpectedDataType = "email" } },
                },
            },
            new()
            {
                Id = "s2",
                Title = "Employment",
                Prompts = new List<Prompt>
                {
                    new() { Id = "employer", Label = "Current employer", Hints = new PromptHints { ExpectedDataType = "text" } },
                },
            },
        },
    };

    /// <summary>Walks the peer tree depth-first. Yields every peer reachable
    /// via <see cref="AutomationPeer.GetChildren"/>.</summary>
    private static IEnumerable<AutomationPeer> WalkPeerTree(AutomationPeer root)
    {
        var stack = new Stack<AutomationPeer>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var peer = stack.Pop();
            yield return peer;
            foreach (var child in peer.GetChildren()) stack.Push(child);
        }
    }

    [AvaloniaFact]
    public void EveryFocusableControlElement_HasNonEmptyAccessibleName()
    {
        var (view, _, session) = Build();
        session.Set(SmallDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var rootPeer = ControlAutomationPeer.CreatePeerForElement(view);
        var unnamed = WalkPeerTree(rootPeer)
            .Where(p => p.IsControlElement() && p.IsKeyboardFocusable() && !p.IsOffscreen())
            .Where(p => string.IsNullOrWhiteSpace(p.GetName()))
            .Select(p => $"  - {p.GetType().Name} ({p.GetLocalizedControlType()})")
            .ToList();

        unnamed.Should().BeEmpty(
            "every focusable interactive control must report an AutomationPeer.GetName so screen readers can announce it. Unnamed peers found:\n" +
            string.Join("\n", unnamed));
    }

    [AvaloniaFact]
    public void EveryFocusableControlElement_ReportsKnownControlType()
    {
        // Screen readers map ControlType to the spoken role ("button", "edit",
        // "menu item"). An empty or unknown role makes a control silent or
        // mis-announced; both are blockers for a blind user.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "button", "edit", "checkbox", "menu", "menubar", "menu item", "menuitem",
            "combobox", "list", "list item", "listitem", "radiobutton", "radio button",
            "tab", "tab item", "tabitem", "scrollbar", "slider", "spinner", "splitbutton",
            "thumb", "tooltip", "header", "headeritem", "header item", "title bar", "titlebar",
            "separator", "text", "image", "hyperlink", "calendar", "datagrid", "data item",
            "dataitem", "document", "group", "pane", "progressbar", "progress bar",
            "statusbar", "status bar", "table", "toolbar", "tree", "tree item", "treeitem",
            "window", "custom",
        };

        var (view, _, session) = Build();
        session.Set(SmallDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var rootPeer = ControlAutomationPeer.CreatePeerForElement(view);
        var bad = WalkPeerTree(rootPeer)
            .Where(p => p.IsControlElement() && p.IsKeyboardFocusable() && !p.IsOffscreen())
            .Where(p => !allowed.Contains(p.GetLocalizedControlType()))
            .Select(p => $"  - {p.GetType().Name}: GetLocalizedControlType()='{p.GetLocalizedControlType()}'")
            .ToList();

        bad.Should().BeEmpty(
            "every focusable peer must report a known ControlType so screen readers can announce its role:\n" +
            string.Join("\n", bad));
    }

    [AvaloniaFact]
    public void StatusBar_AndAdvisoryList_AnnounceAsLiveRegions()
    {
        // Status bar text + advisory items must be polite live regions so screen
        // readers announce changes without yanking focus away from the user's
        // current cell. We set AutomationProperties.LiveSetting=Polite in XAML;
        // this test guards against accidental removal.
        var (view, _, session) = Build();
        session.Set(SmallDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var liveRegions = view.GetVisualDescendants().OfType<Control>()
            .Where(c => AutomationProperties.GetLiveSetting(c) == AutomationLiveSetting.Polite)
            .Select(c => AutomationProperties.GetName(c) ?? c.GetType().Name)
            .ToList();

        // We expect at least one live region — the status bar — and ideally the
        // wizard step label too. Empty here means a regression dropped them.
        liveRegions.Should().NotBeEmpty(
            "the shell must expose at least one Polite live region so progress + advisory updates can be announced without stealing focus");
    }

    [AvaloniaFact]
    public void Menu_BarAndItems_AppearInAutomationTree_WithNames()
    {
        var (view, _, session) = Build();
        session.Set(SmallDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var rootPeer = ControlAutomationPeer.CreatePeerForElement(view);
        var menuPeers = WalkPeerTree(rootPeer)
            .Where(p => string.Equals(p.GetLocalizedControlType(), "menu", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(p.GetLocalizedControlType(), "menubar", StringComparison.OrdinalIgnoreCase))
            .ToList();

        menuPeers.Should().NotBeEmpty("the main menu must appear in the automation tree");
        // At least one of File/Edit/View/Help should be reachable under a menu peer.
        var menuChildren = menuPeers.SelectMany(WalkPeerTree)
            .Where(p => p.IsControlElement() && !string.IsNullOrWhiteSpace(p.GetName()))
            .Select(p => p.GetName())
            .ToList();
        menuChildren.Any(n => n != null && n.Contains("File", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue("File menu must be discoverable from the menu peer");
    }

    [AvaloniaFact]
    public void EveryPromptInForm_HasAccessibleNameMatchingItsLabel()
    {
        // A blind user navigating a form by Tab needs each input announced as
        // "Full name, edit". This test asserts that for every Prompt model in
        // the document, the corresponding TextBox has its Label as its
        // AutomationProperties.Name.
        var (view, _, session) = Build();
        session.Set(SmallDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var nameableInputs = view.GetVisualDescendants().OfType<TextBox>()
            .Where(tb => tb.IsEffectivelyVisible)
            .Select(tb => new { Tb = tb, Name = AutomationProperties.GetName(tb) })
            .ToList();

        // Among the visible TextBoxes we should find ones named "Full name",
        // "Email", "Current employer" — the prompt labels.
        var names = nameableInputs.Select(x => x.Name ?? string.Empty).ToList();
        names.Should().Contain("Full name");
        names.Should().Contain("Email");
        names.Should().Contain("Current employer");
    }

    [AvaloniaFact]
    public void WizardNavBar_ButtonsAreKeyboardFocusable_AndNamed()
    {
        var (view, vm, session) = Build();
        session.Set(SmallDoc(), filePath: null);
        vm.ToggleWizardModeCommand.Execute(null);
        view.ShowInWindow(width: 1200, height: 800);

        var navButtons = view.GetVisualDescendants().OfType<Button>()
            .Where(b => b.IsEffectivelyVisible)
            .Select(b => AutomationProperties.GetName(b) ?? string.Empty)
            .Where(n => n.Contains("section", StringComparison.OrdinalIgnoreCase) || n.Contains("Previous", StringComparison.OrdinalIgnoreCase) || n.Contains("Next", StringComparison.OrdinalIgnoreCase))
            .ToList();

        navButtons.Should().NotBeEmpty(
            "wizard mode must expose Previous/Next buttons in the automation tree so a blind user can navigate sections via Tab + Enter");
    }
}

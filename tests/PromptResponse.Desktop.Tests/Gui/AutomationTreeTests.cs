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
    private static (MainShellView view, MainShellViewModel vm, IDocumentSessionService session) Build()
    {
        var shell = GuiShellHarness.Create();
        return (shell.View, shell.ViewModel, shell.Session);
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
    public void EveryPromptInForm_HasAccessibleNameMatchingItsLabel_AprRender001()
    {
        // A blind person navigating a form by Tab needs each input announced as "Full
        // name, edit". APR-RENDER-001 asks for the label as the accessible name, and this
        // asserts it per prompt rather than by looking for a few labels somewhere among
        // the names: the join is the AutomationId #369 put on every prompt view, so a
        // renderer that named the right number of fields wrongly is caught.
        var (view, _, session) = Build();
        var document = SmallDoc();
        session.Set(document, filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var labels = document.Sections
            .SelectMany(section => section.Prompts)
            .ToDictionary(prompt => prompt.Id, prompt => prompt.Label, StringComparer.Ordinal);

        var named = view.GetVisualDescendants().OfType<Control>()
            .Where(control => labels.ContainsKey(AutomationProperties.GetAutomationId(control) ?? ""))
            .ToDictionary(
                control => AutomationProperties.GetAutomationId(control)!,
                control => control.GetVisualDescendants().OfType<TextBox>()
                    .Select(AutomationProperties.GetName).FirstOrDefault(),
                StringComparer.Ordinal);

        named.Keys.Should().BeEquivalentTo(labels.Keys, "every prompt is rendered");
        foreach (var (id, label) in labels)
        {
            named[id].Should().Be(label,
                "the field rendering {0} must be announced by its label", id);
        }
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

    /// <summary>A document that carries the two hints these assertions are about.</summary>
    private static AprDocument HintedDoc() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Hints", TemplateId = "tag:example.com,2026:hints" },
        Sections =
        [
            new Section
            {
                Id = "s1",
                Title = "Applicant",
                Prompts =
                [
                    new Prompt { Id = "full_name", Label = "Full name", Hints = new PromptHints
                        { HelpText = "As it appears on your passport", Placeholder = "Ada Lovelace" } },
                    new Prompt { Id = "email", Label = "Email", Hints = new PromptHints
                        { ExpectedDataType = "email", HelpText = "We will not share this",
                          Placeholder = "you@example.gov" } },
                ],
            },
        ],
    };

    [AvaloniaFact]
    public void HelpText_IsProgrammaticallyAssociatedWithItsControl_AprRender003()
    {
        // APR-RENDER-003: helpText MUST be programmatically associated with its prompt,
        // not merely adjacent to it. Every prompt view also renders the text in a
        // SelectableTextBlock beside the field, which is what "adjacent" looks like and
        // is not what the rule asks for — so this reads it off the control's peer, which
        // is what a screen reader is told.
        var (view, _, session) = Build();
        session.Set(HintedDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var described = view.GetVisualDescendants().OfType<TextBox>()
            .Where(box => box.IsEffectivelyVisible
                && AutomationProperties.GetName(box) is "Full name" or "Email")
            .Select(box => (Name: AutomationProperties.GetName(box),
                            Help: AutomationProperties.GetHelpText(box)))
            .ToList();

        described.Should().HaveCount(2, "both prompts render an input");
        described.Should().OnlyContain(input => !string.IsNullOrWhiteSpace(input.Help),
            "help text reaches the control, not only the paragraph next to it");
        described.Should().Contain(input => input.Help == "As it appears on your passport");
        described.Should().Contain(input => input.Help == "We will not share this");
    }

    [AvaloniaFact]
    public void APlaceholderIsNeverTheAccessibleName_AprRender002()
    {
        // APR-RENDER-002: a placeholder MUST NOT be the only label. A placeholder
        // disappears the moment somebody types, so a field named by one is unnamed for
        // the rest of the session — and much assistive technology never sees it at all.
        var (view, _, session) = Build();
        session.Set(HintedDoc(), filePath: null);
        view.ShowInWindow(width: 1200, height: 800);

        var inputs = view.GetVisualDescendants().OfType<TextBox>()
            .Where(box => box.IsEffectivelyVisible && box.PlaceholderText is not null)
            .Select(box => (Name: AutomationProperties.GetName(box), Placeholder: box.PlaceholderText))
            .ToList();

        inputs.Should().NotBeEmpty("the fixture gives both prompts a placeholder");
        inputs.Should().OnlyContain(input => !string.IsNullOrWhiteSpace(input.Name),
            "a field with a placeholder still has to be named");
        inputs.Should().NotContain(input => input.Name == input.Placeholder,
            "the label names the field; the placeholder is a hint that vanishes");
    }
}

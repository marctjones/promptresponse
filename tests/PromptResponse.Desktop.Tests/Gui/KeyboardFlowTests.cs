using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
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
/// Layer 2 of the blind-user accessibility test stack. Drives the application
/// keyboard-only — the input pipeline a screen-reader user actually uses —
/// and asserts focus traversal, command activation, and wizard navigation
/// all reach a meaningful next state without requiring a mouse.
/// </summary>
/// <remarks>
/// Avalonia.Headless's input pipeline has known gaps documented in our
/// memory note: <c>MouseDown/MouseUp</c> synthesis doesn't fire <c>Click</c>,
/// and Space-on-ToggleButton doesn't toggle. <see cref="GuiTestExtensions.Activate"/>
/// works around those for the elements that need it. The tests below stay on
/// keyboard-pipeline activation paths that the harness handles correctly:
/// focus-via-Tab, Enter-to-activate-Button, and direct command execution
/// where the harness can't synthesize the keystroke.
/// </remarks>
public class KeyboardFlowTests
{
    private static (MainShellView view, MainShellViewModel vm, IDocumentSessionService session, Window window) Build()
    {
        var shell = GuiShellHarness.Create();
        var window = shell.View.ShowInWindow(width: 1200, height: 800);
        return (shell.View, shell.ViewModel, shell.Session, window);
    }

    private static AprDocument MultiSectionDoc() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Keyboard test" },
        Sections = new List<Section>
        {
            new() { Id = "a", Title = "Alpha", Prompts = new List<Prompt>
            {
                new() { Id = "a1", Label = "Alpha one", Hints = new PromptHints { ExpectedDataType = "text" } },
                new() { Id = "a2", Label = "Alpha two", Hints = new PromptHints { ExpectedDataType = "text" } },
            }},
            new() { Id = "b", Title = "Beta", Prompts = new List<Prompt>
            {
                new() { Id = "b1", Label = "Beta one", Hints = new PromptHints { ExpectedDataType = "text" } },
            }},
            new() { Id = "g", Title = "Gamma", Prompts = new List<Prompt>
            {
                new() { Id = "g1", Label = "Gamma one", Hints = new PromptHints { ExpectedDataType = "text" } },
            }},
        },
    };

    /// <summary>Returns the chain of controls that gain focus when Tab is
    /// pressed repeatedly from the current focused element. Stops after
    /// <paramref name="maxSteps"/> or when focus revisits a previously-seen
    /// control. Pure keyboard pipeline: no mouse.</summary>
    private static List<Control> WalkTabOrder(Window window, int maxSteps = 50)
    {
        var visited = new List<Control>();
        var seen = new HashSet<object>();
        for (var i = 0; i < maxSteps; i++)
        {
            window.PressKey(Key.Tab);
            var focused = window.FocusManager?.GetFocusedElement() as Control;
            if (focused == null) break;
            if (!seen.Add(focused)) break; // revisited — stop to avoid infinite loop
            visited.Add(focused);
        }
        return visited;
    }

    [AvaloniaFact]
    public void Tab_FromFreshWindow_LandsOnFocusableControl()
    {
        var (_, _, session, window) = Build();
        session.Set(MultiSectionDoc(), filePath: null);
        Dispatcher.UIThread.RunJobs();

        window.PressKey(Key.Tab);
        var focused = window.FocusManager?.GetFocusedElement();

        focused.Should().NotBeNull(
            "after a single Tab from a fresh window, focus must land on some focusable control — otherwise the keyboard pipeline is broken for screen-reader users");
    }

    [AvaloniaFact]
    public void TabOrder_TraversesMultipleFocusableControls()
    {
        var (_, _, session, window) = Build();
        session.Set(MultiSectionDoc(), filePath: null);
        Dispatcher.UIThread.RunJobs();

        var chain = WalkTabOrder(window, maxSteps: 30);

        chain.Should().HaveCountGreaterThan(2,
            "with three sections + multiple prompts + menu items + the right rail, Tab traversal must reach at least a few distinct controls");
    }

    [AvaloniaFact]
    public void TabOrder_IncludesPromptInputs()
    {
        // A blind user's #1 use case is "tab to a field, type, tab to next".
        // The TextBox bound to a Prompt's Response must be reachable via Tab.
        var (view, _, session, window) = Build();
        session.Set(MultiSectionDoc(), filePath: null);
        Dispatcher.UIThread.RunJobs();

        var chain = WalkTabOrder(window, maxSteps: 40);

        chain.OfType<TextBox>().Should().NotBeEmpty(
            "tab traversal must reach at least one prompt TextBox so users can type into the form via the keyboard");
    }

    [AvaloniaFact]
    public void EnterOnFocusedButton_ExecutesItsCommand()
    {
        // Wizard Next button — focus + Enter should advance the wizard.
        var (_, vm, session, window) = Build();
        session.Set(MultiSectionDoc(), filePath: null);
        vm.ToggleWizardModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var nextButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.IsEffectivelyVisible
                && (string.Equals((b.Content as string), "Next ▶")
                    || (Avalonia.Automation.AutomationProperties.GetName(b)?.StartsWith("Next") == true)));
        nextButton.Should().NotBeNull("wizard mode must expose a Next button");

        var indexBefore = vm.WizardSectionIndex;
        window.Activate(nextButton!);

        vm.WizardSectionIndex.Should().Be(indexBefore + 1,
            "Enter on the focused Next button must advance the wizard via the keyboard pipeline");
    }

    [AvaloniaFact]
    public void EveryFocusableControlIsEnabled_NoTrapBeforeAReachableInput()
    {
        // Regression guard: any disabled-but-focusable control or any focusable
        // control with no automation name would be a trap for screen-reader
        // users — they'd hear nothing or get stuck. Tab-walk and verify the
        // controls we land on are usable.
        var (_, _, session, window) = Build();
        session.Set(MultiSectionDoc(), filePath: null);
        Dispatcher.UIThread.RunJobs();

        var chain = WalkTabOrder(window, maxSteps: 40);

        var problematic = chain
            .Where(c => !c.IsEffectivelyEnabled || string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(c)))
            .Where(c =>
                // TextBoxes get their name through the prompt label binding
                // which may not be set on the textbox directly — exempt them
                // since AutomationTreeTests covers that gap.
                c is not TextBox)
            .Select(c => $"  - {c.GetType().Name} enabled={c.IsEffectivelyEnabled}")
            .ToList();

        problematic.Should().BeEmpty(
            "controls reached via Tab must be enabled and have an automation name — otherwise a screen-reader user would hit a silent / dead-end stop:\n" +
            string.Join("\n", problematic));
    }

    [AvaloniaFact]
    public void WizardNavigation_FullKeyboardPath_AdvancesAcrossSections()
    {
        // End-to-end: enable wizard, focus Next button, Enter to advance, repeat.
        // This is the path a blind user follows to fill a long form.
        var (_, vm, session, window) = Build();
        session.Set(MultiSectionDoc(), filePath: null);
        vm.ToggleWizardModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var nextButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetName(b) == "Next section");
        nextButton.Should().NotBeNull();

        // Section 1 → 2
        window.Activate(nextButton!);
        vm.WizardCurrentSection!.Title.Should().Be("Beta");

        // Section 2 → 3
        window.Activate(nextButton!);
        vm.WizardCurrentSection!.Title.Should().Be("Gamma");

        // At last section, Next should be disabled — clicking it does nothing.
        var indexAtLast = vm.WizardSectionIndex;
        vm.CanWizardNext.Should().BeFalse();
    }
}

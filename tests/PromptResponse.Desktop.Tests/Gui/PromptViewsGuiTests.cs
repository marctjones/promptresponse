using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// GUI automation for the polymorphic prompt views. Validates that real keyboard
/// input lands in the bound TextBox, that AutomationProperties are exposed for
/// screen-reader users, and that the DataTemplateSelector picks the right view per
/// VM type.
/// </summary>
public class PromptViewsGuiTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static Prompt P(string id, string label, string? type = null, string response = "") =>
        new()
        {
            Id = id,
            Label = label,
            Response = response,
            Hints = new PromptHints { ExpectedDataType = type },
        };

    [AvaloniaFact]
    public void TextPromptView_KeyboardInput_UpdatesResponse()
    {
        var vm = new TextPromptViewModel(P("p", "Name"), NewService());
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("John Doe");

        vm.Response.Should().Be("John Doe", "real keyboard input must land in the bound VM");
    }

    [AvaloniaFact]
    public void TextPromptView_HasAccessibleNameAndHelpText()
    {
        var prompt = P("p", "Full Name");
        prompt.Hints.HelpText = "Last name first";
        var vm = new TextPromptViewModel(prompt, NewService());
        var view = new TextPromptView { DataContext = vm };
        view.ShowInWindow(width: 400, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");

        textBox.GetValue(Avalonia.Automation.AutomationProperties.NameProperty).Should().Be("Full Name");
        textBox.GetValue(Avalonia.Automation.AutomationProperties.HelpTextProperty).Should().Be("Last name first");
    }

    [AvaloniaFact]
    public void NumberPromptView_NonNumericInput_AcceptsItUnchanged()
    {
        // Vision invariant rendered on the GUI: typing "five" into a number-hinted
        // prompt is accepted and stored as-is.
        var vm = new NumberPromptViewModel(P("p", "Age", "number"), NewService());
        var view = new NumberPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 400, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("five");

        vm.Response.Should().Be("five");
    }

    [AvaloniaFact]
    public void NumberPromptView_ProfileChange_UpdatesDisplayValue()
    {
        var service = NewService();
        var vm = new NumberPromptViewModel(P("p", "Salary", "number", "42000"), service);
        var view = new NumberPromptView { DataContext = vm };
        view.ShowInWindow(width: 400, height: 200);

        vm.DisplayValue.Should().Be("42000");

        service.Enable<NumberThousandsSeparatorsProfile>();
        GuiTestExtensions.PumpDispatcher();

        vm.DisplayValue.Should().NotBe("42000");
        vm.DisplayValue.Should().Contain(",");
    }

    [AvaloniaFact]
    public void MultilinePromptView_AcceptsMultilineKeyboardInput()
    {
        var vm = new MultilinePromptViewModel(P("p", "Notes", "multiline"), NewService());
        var view = new MultilinePromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 300);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("first line");
        window.PressKey(Key.Enter);
        window.TypeText("second line");

        vm.Response.Should().Contain("first line");
        vm.Response.Should().Contain("second line");
    }

    [AvaloniaFact]
    public void BooleanPromptView_RadioYes_SetsIsTrueTrue()
    {
        // The radios are gated by BooleanRadiosProfile — enable it so they're visible.
        var service = NewService();
        service.Enable<BooleanRadiosProfile>();
        var vm = new BooleanPromptViewModel(P("p", "Resident", "boolean"), service);
        var view = new BooleanPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var yes = view.FindDescendant<RadioButton>(r => r.Name == "YesRadio");
        window.Activate(yes);

        vm.IsTrue.Should().BeTrue();
        vm.Response.Should().Be("yes");
    }

    [AvaloniaFact]
    public void BooleanPromptView_FreeTextEntry_AcceptsAnyValue()
    {
        var vm = new BooleanPromptViewModel(P("p", "Resident", "boolean"), NewService());
        var view = new BooleanPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var freeText = view.FindDescendant<TextBox>(t => t.Name == "FreeTextEntry");
        freeText.Focus();
        GuiTestExtensions.PumpDispatcher();
        window.TypeText("maybe");

        vm.Response.Should().Be("maybe");
        vm.IsTrue.Should().BeNull("'maybe' isn't a yes/no, but it's a valid response");
    }

    [AvaloniaFact]
    public void SelectPromptView_FreeTextEntry_IsEnabled_AndReachable()
    {
        var prompt = P("p", "Tier");
        prompt.Hints.SuggestedValues = new List<string> { "Gold", "Silver" };
        var vm = new SelectPromptViewModel(prompt, NewService());
        var view = new SelectPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var freeText = view.FindDescendant<TextBox>(t => t.Name == "FreeTextEntry");
        freeText.Focus();
        GuiTestExtensions.PumpDispatcher();
        window.TypeText("Platinum");

        vm.Response.Should().Be("Platinum",
            "users can type a response not in the suggestion list");
    }

    [AvaloniaFact]
    public void DataTemplateSelector_PicksRightView_ForEachVmType()
    {
        var selector = new PromptDataTemplateSelector();
        var service = NewService();

        ((IDataTemplate)selector).Build(new TextPromptViewModel(P("p", "x"), service))
            .Should().BeOfType<TextPromptView>();
        ((IDataTemplate)selector).Build(new NumberPromptViewModel(P("p", "x", "number"), service))
            .Should().BeOfType<NumberPromptView>();
        ((IDataTemplate)selector).Build(new DatePromptViewModel(P("p", "x", "date"), service))
            .Should().BeOfType<DatePromptView>();
        ((IDataTemplate)selector).Build(new MultilinePromptViewModel(P("p", "x", "multiline"), service))
            .Should().BeOfType<MultilinePromptView>();
        ((IDataTemplate)selector).Build(new BooleanPromptViewModel(P("p", "x", "boolean"), service))
            .Should().BeOfType<BooleanPromptView>();
        ((IDataTemplate)selector).Build(new SelectPromptViewModel(P("p", "x"), service))
            .Should().BeOfType<SelectPromptView>();
        ((IDataTemplate)selector).Build(new EmailPromptViewModel(P("p", "x", "email"), service))
            .Should().BeOfType<EmailPromptView>("Email now has its own dedicated view");
    }

    [AvaloniaFact]
    public void DataTemplateSelector_Match_AcceptsAnyPromptVm_RejectsEverythingElse()
    {
        var selector = new PromptDataTemplateSelector();
        IDataTemplate dt = selector;

        dt.Match(new TextPromptViewModel(P("p", "x"), NewService())).Should().BeTrue();
        dt.Match(new NumberPromptViewModel(P("p", "x", "number"), NewService())).Should().BeTrue();
        dt.Match(new object()).Should().BeFalse();
        dt.Match(null).Should().BeFalse();
    }

    [AvaloniaFact]
    public void TextPromptView_TabFromTextBox_LeavesView_KeyboardOnlyFlow()
    {
        // Two separate text views in a vertical stack — Tab between them is the
        // most common keyboard navigation pattern in form filling.
        var vm1 = new TextPromptViewModel(P("p1", "First"), NewService());
        var vm2 = new TextPromptViewModel(P("p2", "Second"), NewService());
        var v1 = new TextPromptView { DataContext = vm1 };
        var v2 = new TextPromptView { DataContext = vm2 };
        var stack = new StackPanel { Children = { v1, v2 } };
        var window = stack.ShowInWindow(width: 600, height: 400);

        var first = v1.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        var second = v2.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        first.Focus();
        GuiTestExtensions.PumpDispatcher();
        first.IsFocused.Should().BeTrue();

        window.PressKey(Key.Tab);

        second.IsFocused.Should().BeTrue("Tab should move focus to the next prompt's TextBox");
    }
}

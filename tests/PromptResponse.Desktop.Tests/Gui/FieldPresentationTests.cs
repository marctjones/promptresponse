using Xunit;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// How a field is presented follows from its expected type, never from the file: a
/// document states no layout, so each rule here is a renderer decision that has to be
/// pinned down. These cover the width class a short answer gets, the colour preview,
/// and that the raw-text escape hatch stays on every specialised widget.
/// </summary>
public class FieldPresentationTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService Profiles() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static Prompt P(string type, string response = "", IList<string>? suggested = null) => new()
    {
        Id = "p",
        Label = "Field",
        Response = response,
        Hints = suggested is null
            ? new PromptHints { ExpectedDataType = type }
            : new PromptHints { ExpectedDataType = type, SuggestedValues = suggested.ToList() },
    };

    private static TextPromptView ShowText(string type, string response = "")
    {
        var view = new TextPromptView { DataContext = new TextPromptViewModel(P(type, response), Profiles()) };
        view.ShowInWindow(width: 600, height: 200);
        return view;
    }

    [AvaloniaTheory]
    [InlineData("time")]
    [InlineData("range")]
    [InlineData("color")]
    public void AShortTypedAnswer_GetsTheShortWidthClass(string type)
        => ShowText(type).Classes.Should().Contain("field-short");

    [AvaloniaTheory]
    [InlineData("text")]
    [InlineData("password")]
    [InlineData(null)]
    public void FreeText_KeepsTheFullMeasure(string? type)
        => ShowText(type ?? "").Classes.Should().NotContain("field-short");

    [AvaloniaFact]
    public void AColourField_PreviewsAValidHexAndLeavesTheTextAlone()
    {
        var view = ShowText("color", "#2A7DE1");

        var swatch = view.FindDescendant<Border>(b => b.Name == "ColorSwatch");
        swatch.IsVisible.Should().BeTrue();
        ((SolidColorBrush)swatch.Background!).Color.Should().Be(Color.Parse("#2A7DE1"));
        view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox").Text.Should().Be("#2A7DE1");
    }

    [AvaloniaFact]
    public void AColourFieldHoldingAnythingElse_ShowsNoPreview_ButKeepsTheAnswer()
    {
        var view = ShowText("color", "see attached");

        var swatch = view.FindDescendant<Border>(b => b.Name == "ColorSwatch");
        swatch.Background.Should().Be(Brushes.Transparent, "free text is a valid answer and is not coerced into a colour");
        view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox").Text.Should().Be("see attached");
    }

    [AvaloniaFact]
    public void ThePreview_FollowsWhatIsTyped()
    {
        var view = ShowText("color", "#FF0000");
        var box = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        var swatch = view.FindDescendant<Border>(b => b.Name == "ColorSwatch");

        box.Text = "#00FF00";
        GuiTestExtensions.PumpDispatcher();

        ((SolidColorBrush)swatch.Background!).Color.Should().Be(Color.Parse("#00FF00"));
    }

    [AvaloniaFact]
    public void ANonColourField_HasNoSwatchShowing()
        => ShowText("time", "14:30:00").FindDescendant<Border>(b => b.Name == "ColorSwatch").IsVisible.Should().BeFalse();

    [AvaloniaFact]
    public void TheSwatch_IsDecoration_NotAControl()
    {
        var swatch = ShowText("color", "#2A7DE1").FindDescendant<Border>(b => b.Name == "ColorSwatch");

        swatch.Focusable.Should().BeFalse("the text box is the answer, so the preview must not take a Tab stop");
        swatch.IsHitTestVisible.Should().BeFalse();
    }

    [AvaloniaTheory]
    [InlineData("boolean")]
    [InlineData("date")]
    [InlineData("select")]
    [InlineData("multichoice")]
    public void EverySpecialisedWidget_KeepsANamedRawTextToggle(string type)
    {
        Control view = type switch
        {
            "boolean" => new BooleanPromptView { DataContext = new BooleanPromptViewModel(P(type), Profiles()) },
            "date" => new DatePromptView { DataContext = new DatePromptViewModel(P(type), Profiles()) },
            "select" => new SelectPromptView { DataContext = new SelectPromptViewModel(P(type, suggested: ["A", "B"]), Profiles()) },
            _ => new MultichoicePromptView { DataContext = new MultichoicePromptViewModel(P(type, suggested: ["A", "B"]), Profiles()) },
        };
        view.ShowInWindow(width: 600, height: 300);

        var toggle = view.FindDescendant<Button>(b => b.Classes.Contains("raw-toggle"));
        toggle.Should().NotBeNull("a specialised widget never removes the raw-text path");
        Avalonia.Automation.AutomationProperties.GetName(toggle).Should().NotBeNullOrWhiteSpace();
        toggle.Command.Should().NotBeNull();
    }
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Long help text must be readable in full, and copyable.
/// </summary>
/// <remarks>
/// Found by opening SF-86 in the running application. Its certification statement is 289
/// characters of help text, rendered in a TextBlock with no TextWrapping: one clipped
/// line that could not be selected. That form expects the person filling it to copy that
/// exact text into a field, so an unselectable, half-visible statement is not a cosmetic
/// problem - it is the task being impossible.
/// </remarks>
public class ReadableTextTests
{
    private sealed class Probe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private const string Certification =
        "I certify that the statements I have made on this form, and any attachments to it, " +
        "are true, complete, and correct to the best of my knowledge and belief and are made " +
        "in good faith. I understand that a knowing and willful false statement can be " +
        "punished by fine or imprisonment or both.";

    private static TextPromptViewModel WithHelp(string help) =>
        new(new Prompt
            {
                Id = "p", Label = "Certification statement",
                Hints = new PromptHints { HelpText = help },
            },
            new ProfileService(new Probe(), applyAffordanceDefaults: false),
            new EditHistory());

    [AvaloniaFact]
    public void LongHelpText_IsSelectable()
    {
        var view = new TextPromptView { DataContext = WithHelp(Certification) };
        view.ShowInWindow(width: 700, height: 300);

        view.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Should().Contain(t => t.Text == Certification,
                "SF-86 asks the person filling it to copy this statement into a field; " +
                "text they cannot select is text they cannot copy");
    }

    [AvaloniaFact]
    public void LongHelpText_WrapsInsteadOfBeingClipped()
    {
        var view = new TextPromptView { DataContext = WithHelp(Certification) };
        var window = view.ShowInWindow(width: 700, height: 300);
        window.UpdateLayout();

        var help = view.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Single(t => t.Text == Certification);

        help.TextWrapping.Should().NotBe(Avalonia.Media.TextWrapping.NoWrap,
            "289 characters on one line is 289 characters nobody can read");
        help.Bounds.Height.Should().BeGreaterThan(20,
            $"wrapped text occupies more than a single line; measured {help.Bounds.Height}px");
        help.Bounds.Width.Should().BeLessThanOrEqualTo(700,
            "and it must fit the window rather than running off the edge");
    }

    [AvaloniaFact]
    public void ShortHelpText_IsUnaffected()
    {
        var view = new TextPromptView { DataContext = WithHelp("Your full legal name.") };
        view.ShowInWindow(width: 700, height: 300);

        view.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Should().Contain(t => t.Text == "Your full legal name.");
    }
}

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Verifies the PromptDataTemplateSelector returns a dedicated view for every
/// supported data-type hint in the .apr format. A regression here would mean a
/// prompt type silently falls back to a generic text input — exactly the gap the
/// user identified.
/// </summary>
public class PromptDataTemplateCoverageTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new StubProbe(), applyAffordanceDefaults: false);

    private static Prompt P(string type) => new()
    {
        Id = "p1",
        Label = "x",
        Hints = new PromptHints { ExpectedDataType = type },
    };

    [AvaloniaTheory]
    [InlineData("text", typeof(TextPromptView))]
    [InlineData("multiline", typeof(MultilinePromptView))]
    [InlineData("number", typeof(NumberPromptView))]
    [InlineData("currency", typeof(CurrencyPromptView))]
    [InlineData("date", typeof(DatePromptView))]
    [InlineData("datetime", typeof(DatePromptView))]
    [InlineData("email", typeof(EmailPromptView))]
    [InlineData("url", typeof(UrlPromptView))]
    [InlineData("phone", typeof(PhonePromptView))]
    [InlineData("boolean", typeof(BooleanPromptView))]
    [InlineData("signature", typeof(SignaturePromptView))]
    [InlineData("file", typeof(FilePromptView))]
    public void EveryDataTypeHint_RendersDedicatedView(string hint, Type expectedView)
    {
        var factory = new PromptViewModelFactory(NewService());
        var vm = factory.Create(P(hint));

        var selector = (IDataTemplate)new PromptDataTemplateSelector();
        var view = selector.Build(vm);

        view.Should().NotBeNull();
        view.Should().BeOfType(expectedView,
            $"prompts hinting '{hint}' must render via {expectedView.Name}, not fall back to a generic input");
    }

    [AvaloniaFact]
    public void SelectPromptViewModel_RendersSelectView()
    {
        var prompt = new Prompt
        {
            Id = "p1",
            Label = "x",
            Hints = new PromptHints
            {
                SuggestedValues = new List<string> { "A", "B" },
            },
        };
        var vm = new PromptViewModelFactory(NewService()).Create(prompt);

        ((IDataTemplate)new PromptDataTemplateSelector()).Build(vm)
            .Should().BeOfType<SelectPromptView>(
                "a prompt with SuggestedValues should render the SelectPromptView");
    }

    [AvaloniaFact]
    public void MultichoicePromptViewModel_RendersMultichoiceView()
    {
        var prompt = new Prompt
        {
            Id = "p1",
            Label = "x",
            Hints = new PromptHints { ExpectedDataType = "multichoice" },
        };
        var vm = new PromptViewModelFactory(NewService()).Create(prompt);

        ((IDataTemplate)new PromptDataTemplateSelector()).Build(vm)
            .Should().BeOfType<MultichoicePromptView>();
    }
}

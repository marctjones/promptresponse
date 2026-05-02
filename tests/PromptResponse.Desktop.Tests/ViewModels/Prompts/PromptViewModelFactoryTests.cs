using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Prompts;

public class PromptViewModelFactoryTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static PromptViewModelFactory CreateFactory() => new(new ProfileService(new FixedProbe(), applyAffordanceDefaults: false));

    private static Prompt PromptWithHint(string? hint, IList<string>? suggestions = null)
    {
        var hints = new PromptHints();
        if (hint is not null) hints.ExpectedDataType = hint;
        if (suggestions is not null) hints.SuggestedValues = suggestions.ToList();
        return new Prompt { Id = "p1", Label = "x", Hints = hints };
    }

    [Theory]
    [InlineData("number", typeof(NumberPromptViewModel))]
    [InlineData("currency", typeof(CurrencyPromptViewModel))]
    [InlineData("date", typeof(DatePromptViewModel))]
    [InlineData("datetime", typeof(DatePromptViewModel))]
    [InlineData("email", typeof(EmailPromptViewModel))]
    [InlineData("url", typeof(UrlPromptViewModel))]
    [InlineData("phone", typeof(PhonePromptViewModel))]
    [InlineData("boolean", typeof(BooleanPromptViewModel))]
    [InlineData("multiline", typeof(MultilinePromptViewModel))]
    [InlineData("signature", typeof(SignaturePromptViewModel))]
    [InlineData("file", typeof(FilePromptViewModel))]
    [InlineData("table", typeof(TablePromptViewModel))]
    [InlineData("select", typeof(SelectPromptViewModel))]
    [InlineData("multichoice", typeof(MultichoicePromptViewModel))]
    public void Create_ReturnsCorrectVmTypeForKnownHint(string hint, Type expected)
    {
        var factory = CreateFactory();
        var prompt = PromptWithHint(hint);

        var vm = factory.Create(prompt);

        vm.Should().BeOfType(expected);
    }

    [Theory]
    [InlineData("text")]
    [InlineData(null)]
    [InlineData("unknown_made_up_type")]
    public void Create_FallsBackToText_ForUnknownOrMissingHints(string? hint)
    {
        var factory = CreateFactory();
        var prompt = PromptWithHint(hint);

        var vm = factory.Create(prompt);

        vm.Should().BeOfType<TextPromptViewModel>();
    }

    [Fact]
    public void Create_HasSuggestions_NoExplicitHint_ReturnsSelectVm()
    {
        var factory = CreateFactory();
        var prompt = PromptWithHint(null, new[] { "Option A", "Option B" });

        var vm = factory.Create(prompt);

        vm.Should().BeOfType<SelectPromptViewModel>();
    }

    [Fact]
    public void Create_TimeHint_FallsBackToTextForNow()
    {
        // No dedicated TimePromptViewModel yet; "time" maps to TextPromptViewModel
        // since profile-aware time formatting is a future enhancement.
        var factory = CreateFactory();
        var prompt = PromptWithHint("time");

        var vm = factory.Create(prompt);

        vm.Should().BeOfType<TextPromptViewModel>();
    }

    [Fact]
    public void Create_RejectsNullPrompt()
    {
        var factory = CreateFactory();

        Action act = () => factory.Create(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_RejectsNullService()
    {
        Action act = () => new PromptViewModelFactory(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EveryCreatedVm_DerivesFromBase_AndHasMatchingPromptId()
    {
        var factory = CreateFactory();
        var prompt = PromptWithHint("text");
        prompt.Id = "the_prompt_id";

        var vm = factory.Create(prompt);

        vm.Should().BeAssignableTo<PromptViewModelBase>();
        vm.Id.Should().Be("the_prompt_id");
    }
}

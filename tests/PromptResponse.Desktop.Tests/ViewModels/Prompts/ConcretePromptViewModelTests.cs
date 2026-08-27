using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Prompts;

public class ConcretePromptViewModelTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static Prompt P(string id, string label, Action<Prompt>? setup = null)
    {
        var p = new Prompt { Id = id, Label = label, Hints = new PromptHints() };
        setup?.Invoke(p);
        return p;
    }

    // ---- BooleanPromptViewModel ----

    [Theory]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("YES", true)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("on", true)]
    [InlineData("no", false)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("off", false)]
    public void Boolean_RecognisedYesNoStrings_MapToTriState(string raw, bool expected)
    {
        var vm = new BooleanPromptViewModel(P("p", "Q", p => p.Response = raw), NewService());

        vm.IsTrue.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("maybe")]
    [InlineData("depends")]
    [InlineData("see notes")]
    public void Boolean_FreeTextResponse_MapsToNullTriState_ButResponseIsPreserved(string raw)
    {
        var vm = new BooleanPromptViewModel(P("p", "Q", p => p.Response = raw), NewService());

        vm.IsTrue.Should().BeNull("non-yes/no responses are valid but un-classified");
        vm.Response.Should().Be(raw, "free-text response is preserved exactly");
    }

    [Fact]
    public void Boolean_SetIsTrueTrue_StoresCanonicalTrue()
    {
        var vm = new BooleanPromptViewModel(P("p", "Q"), NewService());

        vm.IsTrue = true;

        vm.Response.Should().Be("true", "canonical boolean write form (specification section 4.9)");
    }

    [Fact]
    public void Boolean_SetIsTrueFalse_StoresCanonicalFalse()
    {
        var vm = new BooleanPromptViewModel(P("p", "Q"), NewService());

        vm.IsTrue = false;

        vm.Response.Should().Be("false", "canonical boolean write form (specification section 4.9)");
    }

    [Fact]
    public void Boolean_SetIsTrueNull_ClearsResponse()
    {
        var vm = new BooleanPromptViewModel(P("p", "Q", p => p.Response = "yes"), NewService());

        vm.IsTrue = null;

        vm.Response.Should().BeEmpty();
    }

    // ---- SelectPromptViewModel ----

    [Fact]
    public void Select_ExposesSuggestionsFromHints()
    {
        var vm = new SelectPromptViewModel(
            P("p", "Q", p => p.Hints.SuggestedValues = new List<string> { "A", "B", "C" }),
            NewService());

        vm.SuggestedValues.Should().Equal("A", "B", "C");
    }

    [Fact]
    public void Select_HasEmptySuggestions_WhenHintAbsent()
    {
        var vm = new SelectPromptViewModel(P("p", "Q"), NewService());

        vm.SuggestedValues.Should().BeEmpty();
    }

    [Fact]
    public void Select_AcceptsResponseNotInSuggestionList()
    {
        // Vision: any text is valid, even if not in the suggestion list.
        var vm = new SelectPromptViewModel(
            P("p", "Q", p => p.Hints.SuggestedValues = new List<string> { "A", "B" }),
            NewService());

        vm.Response = "completely custom response";

        vm.Response.Should().Be("completely custom response");
    }

    // ---- MultichoicePromptViewModel ----

    [Fact]
    public void Multichoice_StartsEmpty_NoSelectionsActive()
    {
        var vm = new MultichoicePromptViewModel(
            P("p", "Q", p => p.Hints.SuggestedValues = new List<string> { "Apple", "Banana" }),
            NewService());

        vm.IsSelected("Apple").Should().BeFalse();
    }

    [Fact]
    public void Multichoice_SelectAndDeselect_RoundTripThroughResponse()
    {
        var vm = new MultichoicePromptViewModel(
            P("p", "Q", p => p.Hints.SuggestedValues = new List<string> { "Apple", "Banana", "Cherry" }),
            NewService());

        vm.Select("Apple");
        vm.Select("Cherry");
        vm.IsSelected("Apple").Should().BeTrue();
        vm.IsSelected("Cherry").Should().BeTrue();
        vm.IsSelected("Banana").Should().BeFalse();

        vm.Deselect("Apple");
        vm.IsSelected("Apple").Should().BeFalse();
    }

    [Fact]
    public void Multichoice_SerializesAsNewlineSeparatedString()
    {
        var vm = new MultichoicePromptViewModel(
            P("p", "Q", p => p.Hints.SuggestedValues = new List<string> { "X", "Y", "Z" }),
            NewService());

        vm.Select("X");
        vm.Select("Z");

        vm.Response.Should().Be("X\nZ", "canonical multichoice write form is newline-separated: a suggested value may itself contain a comma");
    }

    [Fact]
    public void Multichoice_SelectingTwiceIsIdempotent()
    {
        var vm = new MultichoicePromptViewModel(
            P("p", "Q", p => p.Hints.SuggestedValues = new List<string> { "A" }),
            NewService());

        vm.Select("A");
        vm.Select("A");

        vm.Response.Should().Be("A");
    }

    // ---- FilePromptViewModel ----

    [Fact]
    public void File_FileName_ReturnsBasenameFromPath()
    {
        var vm = new FilePromptViewModel(
            P("p", "Attach", p => p.Response = "/home/marc/docs/contract.pdf"),
            NewService());

        vm.FileName.Should().Be("contract.pdf");
    }

    [Fact]
    public void File_FileName_EmptyForEmptyResponse()
    {
        var vm = new FilePromptViewModel(P("p", "Attach"), NewService());

        vm.FileName.Should().BeEmpty();
    }

    [Fact]
    public void File_AcceptsFreeTextResponse()
    {
        // Vision: even a "file" prompt accepts any text — "see attached PDF in email".
        var vm = new FilePromptViewModel(P("p", "Attach"), NewService());

        vm.Response = "see attached PDF in email";

        vm.Response.Should().Be("see attached PDF in email");
    }

    // (Tables are no longer prompt-typed — they're modeled as Section.TableLayout
    // with row sub-sections and cell prompts. See TableSectionViewModelTests.)

    // ---- Currency / Date / Number profile-aware display ----

    [Fact]
    public void Currency_CurrencyDisplayFlag_RendersWithSymbol()
    {
        var service = NewService();
        service.Enable<CurrencyDisplayProfile>();
        var vm = new CurrencyPromptViewModel(P("p", "$", p => { p.Response = "1500"; p.Hints.ExpectedDataType = "currency"; }), service);

        vm.DisplayValue.Should().NotBe("1500");
    }

    [Fact]
    public void Date_IsoDatePrettifyFlag_RendersHumanReadable()
    {
        var service = NewService();
        service.Enable<IsoDatePrettifyProfile>();
        var vm = new DatePromptViewModel(P("p", "DOB", p => { p.Response = "2025-04-29"; p.Hints.ExpectedDataType = "date"; }), service);

        vm.DisplayValue.Should().NotBe("2025-04-29");
        vm.DisplayValue.Should().Contain("April");
    }
}

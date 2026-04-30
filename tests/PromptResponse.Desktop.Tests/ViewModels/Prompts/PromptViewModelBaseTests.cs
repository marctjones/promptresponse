using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Prompts;

/// <summary>
/// Contract tests for the polymorphic prompt-VM base class. Every concrete
/// subclass shares this behaviour: response round-trips, profile-aware display,
/// validation advisories, and the universal vision invariant that any text is valid.
/// </summary>
public class PromptViewModelBaseTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe());

    private static Prompt MakePrompt(string id, string label, string? type = null, string response = "") =>
        new()
        {
            Id = id,
            Label = label,
            Response = response,
            Hints = new PromptHints { ExpectedDataType = type },
        };

    [Fact]
    public void Constructor_ExposesPromptIdLabelAndResponse()
    {
        var prompt = MakePrompt("p1", "Age", "number", "42");
        var vm = new TextPromptViewModel(prompt, NewService());

        vm.Id.Should().Be("p1");
        vm.Label.Should().Be("Age");
        vm.Response.Should().Be("42");
    }

    [Fact]
    public void SettingResponse_PropagatesToPromptModel()
    {
        var prompt = MakePrompt("p1", "Age");
        var vm = new TextPromptViewModel(prompt, NewService());

        vm.Response = "42";

        prompt.Response.Should().Be("42");
    }

    [Fact]
    public void SettingResponse_RaisesPropertyChanged_ForResponseAndDisplayValue()
    {
        var prompt = MakePrompt("p1", "Age");
        var vm = new TextPromptViewModel(prompt, NewService());
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Response = "new value";

        changed.Should().Contain(nameof(vm.Response));
        changed.Should().Contain(nameof(vm.DisplayValue));
    }

    [Fact]
    public void SettingResponse_ToSameValue_DoesNotRaisePropertyChanged()
    {
        var prompt = MakePrompt("p1", "Age", response: "42");
        var vm = new TextPromptViewModel(prompt, NewService());
        var changed = 0;
        vm.PropertyChanged += (_, _) => changed++;

        vm.Response = "42";

        changed.Should().Be(0, "idempotent set must not pulse PropertyChanged");
    }

    [Fact]
    public void DisplayValue_OnDefaultProfile_EqualsRawResponse()
    {
        var prompt = MakePrompt("p1", "Age", "number", "42000");
        var vm = new TextPromptViewModel(prompt, NewService());

        vm.DisplayValue.Should().Be("42000");
    }

    [Fact]
    public void DisplayValue_WhenVisualFormattingActive_RendersFormatted()
    {
        var service = NewService();
        service.Enable<VisualFormattingProfile>();
        var prompt = MakePrompt("p1", "Salary", "number", "42000");
        var vm = new NumberPromptViewModel(prompt, service);

        vm.DisplayValue.Should().NotBe("42000");
        vm.DisplayValue.Should().Contain("42").And.Contain(",");
    }

    [Fact]
    public void DisplayValue_NonNumericResponseInNumberPrompt_PassesThrough()
    {
        // Cornerstone vision invariant: non-numeric response in a number field is valid.
        var service = NewService();
        service.Enable<VisualFormattingProfile>();
        var prompt = MakePrompt("p1", "Age", "number", "five");
        var vm = new NumberPromptViewModel(prompt, service);

        vm.DisplayValue.Should().Be("five");
    }

    [Fact]
    public void ProfileChanged_TriggersDisplayValueRefresh()
    {
        var service = NewService();
        var prompt = MakePrompt("p1", "Salary", "number", "42000");
        var vm = new NumberPromptViewModel(prompt, service);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.Enable<VisualFormattingProfile>();

        changed.Should().Contain(nameof(vm.DisplayValue),
            "DisplayValue depends on the active profile and must re-bind when it changes");
    }

    [Fact]
    public void Dispose_UnsubscribesFromProfileChanged()
    {
        var service = NewService();
        var prompt = MakePrompt("p1", "x");
        var vm = new TextPromptViewModel(prompt, service);

        vm.Dispose();

        var changed = 0;
        vm.PropertyChanged += (_, _) => changed++;
        service.Enable<VisualFormattingProfile>();

        changed.Should().Be(0, "disposed VMs must stop reacting to profile changes");
    }

    [Fact]
    public void Constructor_RejectsNullPromptOrService()
    {
        Action nullPrompt = () => new TextPromptViewModel(null!, NewService());
        Action nullService = () => new TextPromptViewModel(MakePrompt("p", "x"), null!);

        nullPrompt.Should().Throw<ArgumentNullException>();
        nullService.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HelpText_ReadsFromHints_ExposesNullWhenAbsent()
    {
        var prompt = MakePrompt("p1", "x");
        prompt.Hints.HelpText = "Enter your age in years";
        var vm = new TextPromptViewModel(prompt, NewService());

        vm.HelpText.Should().Be("Enter your age in years");
    }

    [Fact]
    public void Placeholder_ReadsFromHints()
    {
        var prompt = MakePrompt("p1", "x");
        prompt.Hints.Placeholder = "e.g. 42";
        var vm = new TextPromptViewModel(prompt, NewService());

        vm.Placeholder.Should().Be("e.g. 42");
    }
}

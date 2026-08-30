using AwesomeAssertions;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditorMutationTests
{
    [Fact]
    public void SectionTitle_TwoWayBound_PersistsToModel()
    {
        var (vm, model, _, _) = NewBlankSection();

        vm.Title = "Personal Information";

        vm.Title.Should().Be("Personal Information");
        model.Title.Should().Be("Personal Information",
            "the editor's two-way title binding must reach the model so a save persists it");
    }

    [Fact]
    public void SectionDescription_TwoWayBound_PersistsToModel()
    {
        var (vm, model, _, _) = NewBlankSection();

        vm.Description = "Standard contact details";

        model.Description.Should().Be("Standard contact details");
        vm.HasDescription.Should().BeTrue();
    }

    [Fact]
    public void PromptLabel_TwoWayBound_PersistsToModel()
    {
        var (vm, model, _, _) = NewBlankSection();
        var promptVm = vm.AddPrompt();

        promptVm.Label = "Full legal name";

        promptVm.Label.Should().Be("Full legal name");
        model.Prompts[0].Label.Should().Be("Full legal name");
    }

    [Fact]
    public void PromptExpectedDataType_TwoWayBound_PersistsToModel_AndTriggersDisplayValueRefresh()
    {
        var (vm, model, _, _) = NewBlankSection();
        var promptVm = vm.AddPrompt();
        var displayChanges = 0;
        promptVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PromptViewModelBase.DisplayValue)) displayChanges++;
        };

        promptVm.ExpectedDataType = "currency";

        promptVm.ExpectedDataType.Should().Be("currency");
        model.Prompts[0].Hints.ExpectedDataType.Should().Be("currency");
        displayChanges.Should().BeGreaterThan(0, "type changes affect rendered display, so DisplayValue must be re-evaluated");
    }

    [Fact]
    public void PromptHints_PlaceholderHelpTextPattern_PersistToModel()
    {
        var (vm, model, _, _) = NewBlankSection();
        var p = vm.AddPrompt();

        p.Placeholder = "you@example.com";
        p.HelpText = "Enter your work email";
        p.ValidationPattern = @"^[^@]+@[^@]+\.[^@]+$";

        var saved = model.Prompts[0].Hints;
        saved.Placeholder.Should().Be("you@example.com");
        saved.HelpText.Should().Be("Enter your work email");
        saved.ValidationPattern.Should().Be(@"^[^@]+@[^@]+\.[^@]+$");
    }
}

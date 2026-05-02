using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// SectionViewModel mirrors the recursive Section model in the rendering tree:
/// Title, Description, NestedSections (recursive), and PromptViewModels (typed).
/// The form view binds to a flat list-of-sections and the SectionView template
/// renders the hierarchy.
/// </summary>
public class SectionViewModelTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static PromptViewModelFactory NewFactory() =>
        new(new ProfileService(new StubProbe(), applyAffordanceDefaults: false));

    [Fact]
    public void Constructor_ExposesTitleAndDescription_FromSectionModel()
    {
        var section = new Section
        {
            Id = "personal",
            Title = "Personal Information",
            Description = "Basic identifying details.",
        };
        var vm = new SectionViewModel(section, NewFactory(), depth: 0);

        vm.Id.Should().Be("personal");
        vm.Title.Should().Be("Personal Information");
        vm.Description.Should().Be("Basic identifying details.");
        vm.Depth.Should().Be(0);
    }

    [Fact]
    public void Constructor_BuildsPromptVmsViaFactory()
    {
        var section = new Section
        {
            Id = "s1",
            Title = "S",
            Prompts = new List<Prompt>
            {
                new() { Id = "p1", Label = "Text" },
                new() { Id = "p2", Label = "Number", Hints = new PromptHints { ExpectedDataType = "number" } },
                new() { Id = "p3", Label = "Yes/No", Hints = new PromptHints { ExpectedDataType = "boolean" } },
            },
        };
        var vm = new SectionViewModel(section, NewFactory(), depth: 0);

        vm.PromptViewModels.Should().HaveCount(3);
        vm.PromptViewModels[0].Should().BeOfType<TextPromptViewModel>();
        vm.PromptViewModels[1].Should().BeOfType<NumberPromptViewModel>();
        vm.PromptViewModels[2].Should().BeOfType<BooleanPromptViewModel>();
    }

    [Fact]
    public void Constructor_BuildsNestedSections_RecursivelyWithIncrementedDepth()
    {
        var inner = new Section { Id = "inner", Title = "Inner" };
        var outer = new Section
        {
            Id = "outer",
            Title = "Outer",
            Sections = new List<Section> { inner },
        };
        var vm = new SectionViewModel(outer, NewFactory(), depth: 0);

        vm.NestedSections.Should().ContainSingle();
        vm.NestedSections[0].Title.Should().Be("Inner");
        vm.NestedSections[0].Depth.Should().Be(1, "nested sections increment depth for indentation");
    }

    [Fact]
    public void HasDescription_ReturnsFalseForEmptyOrWhitespace()
    {
        new SectionViewModel(new Section { Id = "a", Title = "T", Description = null }, NewFactory(), 0)
            .HasDescription.Should().BeFalse();
        new SectionViewModel(new Section { Id = "a", Title = "T", Description = "   " }, NewFactory(), 0)
            .HasDescription.Should().BeFalse();
        new SectionViewModel(new Section { Id = "a", Title = "T", Description = "Real description" }, NewFactory(), 0)
            .HasDescription.Should().BeTrue();
    }

    [Fact]
    public void Constructor_DeepNesting_PreservesDepthChain()
    {
        var l3 = new Section { Id = "l3", Title = "L3" };
        var l2 = new Section { Id = "l2", Title = "L2", Sections = new List<Section> { l3 } };
        var l1 = new Section { Id = "l1", Title = "L1", Sections = new List<Section> { l2 } };

        var vm = new SectionViewModel(l1, NewFactory(), depth: 0);

        vm.Depth.Should().Be(0);
        vm.NestedSections[0].Depth.Should().Be(1);
        vm.NestedSections[0].NestedSections[0].Depth.Should().Be(2);
    }
}

using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// FormProgressViewModel tracks how many prompts have responses out of the total.
/// Pure derived state — no UI dependencies, fully unit-testable.
/// </summary>
public class FormProgressViewModelTests
{
    private static AprDocument BuildDocument(params (string id, string label, string response)[] prompts)
    {
        var section = new Section
        {
            Id = "section_1",
            Title = "Section",
            Prompts = prompts.Select(p => new Prompt { Id = p.id, Label = p.label, Response = p.response }).ToList(),
        };
        return new AprDocument { Metadata = new Metadata { Title = "T" }, Sections = new List<Section> { section } };
    }

    [Fact]
    public void SetDocument_PopulatesPerSectionProgress()
    {
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections = new()
            {
                new Section
                {
                    Id = "a", Title = "Personal",
                    Prompts = new() { new() { Id = "n", Response = "x" }, new() { Id = "e", Response = "" } },
                },
                new Section
                {
                    Id = "b", Title = "Work",
                    Prompts = new() { new() { Id = "w1", Response = "x" } },
                    Sections = new() { new Section { Id = "b1", Title = "Sub", Prompts = new() { new() { Id = "w2", Response = "y" } } } },
                },
            },
        };

        var vm = new FormProgressViewModel();
        vm.SetDocument(doc);

        vm.Sections.Should().HaveCount(2, "the sidebar starts with the document's top-level section outline");

        var personal = vm.Sections[0];
        personal.Title.Should().Be("Personal");
        personal.Answered.Should().Be(1);
        personal.Total.Should().Be(2);
        personal.PercentComplete.Should().Be(50);
        personal.IsComplete.Should().BeFalse();
        personal.StatusText.Should().Be("1/2");

        var work = vm.Sections[1];
        work.Total.Should().Be(2);          // direct + nested
        work.Answered.Should().Be(2);
        work.IsComplete.Should().BeTrue();
        work.StatusText.Should().Be("✓ 2/2");

        var nested = work.Children.Single();
        nested.Title.Should().Be("Sub");
        nested.SectionId.Should().Be("b1");
        nested.Depth.Should().Be(1);
        nested.TopLevelIndex.Should().Be(1);
    }

    [Fact]
    public void SetDocument_Null_ClearsSections()
    {
        var vm = new FormProgressViewModel();
        vm.SetDocument(BuildDocument(("a", "A", "x")));
        vm.Sections.Should().NotBeEmpty();

        vm.SetDocument(null);

        vm.Sections.Should().BeEmpty();
    }

    [Fact]
    public void EmptyDocument_HasZeroPromptsAndZeroPercent()
    {
        var vm = new FormProgressViewModel();
        vm.SetDocument(new AprDocument { Metadata = new Metadata { Title = "T" }, Sections = new() });

        vm.TotalPrompts.Should().Be(0);
        vm.AnsweredPrompts.Should().Be(0);
        vm.PercentComplete.Should().Be(0);
    }

    [Fact]
    public void NullDocument_IsSafe_AndShowsZero()
    {
        var vm = new FormProgressViewModel();

        vm.SetDocument(null);

        vm.TotalPrompts.Should().Be(0);
        vm.AnsweredPrompts.Should().Be(0);
        vm.PercentComplete.Should().Be(0);
    }

    [Fact]
    public void DocumentWithAllAnswered_Reports100Percent()
    {
        var doc = BuildDocument(("a", "A", "x"), ("b", "B", "y"), ("c", "C", "z"));
        var vm = new FormProgressViewModel();

        vm.SetDocument(doc);

        vm.TotalPrompts.Should().Be(3);
        vm.AnsweredPrompts.Should().Be(3);
        vm.PercentComplete.Should().Be(100);
    }

    [Fact]
    public void DocumentWithMixedAnswers_ComputesCorrectPercentage()
    {
        var doc = BuildDocument(("a", "A", "x"), ("b", "B", ""), ("c", "C", "z"), ("d", "D", "  "));
        var vm = new FormProgressViewModel();

        vm.SetDocument(doc);

        vm.TotalPrompts.Should().Be(4);
        vm.AnsweredPrompts.Should().Be(2, "whitespace-only is treated as unanswered");
        vm.PercentComplete.Should().Be(50);
    }

    [Fact]
    public void NestedSections_TraversedRecursively()
    {
        var inner = new Section { Id = "inner", Title = "Inner",
            Prompts = new List<Prompt> { new() { Id = "p1", Label = "P1", Response = "x" } } };
        var outer = new Section { Id = "outer", Title = "Outer",
            Prompts = new List<Prompt> { new() { Id = "p2", Label = "P2", Response = "" } },
            Sections = new List<Section> { inner } };
        var doc = new AprDocument { Metadata = new Metadata { Title = "T" }, Sections = new List<Section> { outer } };

        var vm = new FormProgressViewModel();
        vm.SetDocument(doc);

        vm.TotalPrompts.Should().Be(2);
        vm.AnsweredPrompts.Should().Be(1);
        vm.PercentComplete.Should().Be(50);
    }

    [Fact]
    public void PropertyChanged_RaisedForAllDerivedProperties_OnSetDocument()
    {
        var vm = new FormProgressViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SetDocument(BuildDocument(("a", "A", "x")));

        changed.Should().Contain(nameof(vm.TotalPrompts));
        changed.Should().Contain(nameof(vm.AnsweredPrompts));
        changed.Should().Contain(nameof(vm.PercentComplete));
        changed.Should().Contain(nameof(vm.StatusText));
    }

    [Fact]
    public void Refresh_RecomputesAfterMutation_WithoutFullReset()
    {
        var doc = BuildDocument(("a", "A", ""), ("b", "B", ""));
        var vm = new FormProgressViewModel();
        vm.SetDocument(doc);

        vm.AnsweredPrompts.Should().Be(0);

        // Simulate the user filling in a prompt
        doc.Sections[0].Prompts[0].Response = "answer";
        vm.Refresh();

        vm.AnsweredPrompts.Should().Be(1);
        vm.PercentComplete.Should().Be(50);
    }

    [Fact]
    public void StatusText_IsHumanReadable_AndScreenReaderFriendly()
    {
        var vm = new FormProgressViewModel();
        vm.SetDocument(BuildDocument(("a", "A", "x"), ("b", "B", "")));

        vm.StatusText.Should().Contain("1");
        vm.StatusText.Should().Contain("2");
        vm.StatusText.Should().Contain("answered", because: "screen-reader-friendly phrasing");
    }
}

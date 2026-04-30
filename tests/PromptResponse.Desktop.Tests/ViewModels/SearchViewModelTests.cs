using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// SearchViewModel implements find/jump within a form: matches prompts by label,
/// id, or response text. Extracted from FormFillingViewModel.
/// </summary>
public class SearchViewModelTests
{
    private static AprDocument BuildDocument()
    {
        return new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "personal",
                    Title = "Personal Information",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "name", Label = "Full name", Response = "Alex" },
                        new() { Id = "age", Label = "Age", Response = "42" },
                        new() { Id = "email", Label = "Email address", Response = "alex@example.com" },
                    },
                },
                new()
                {
                    Id = "preferences",
                    Title = "Preferences",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "color", Label = "Favourite color", Response = "blue" },
                    },
                },
            },
        };
    }

    [Fact]
    public void NewVm_HasNoMatches_AndEmptyQuery()
    {
        var vm = new SearchViewModel();

        vm.Query.Should().BeEmpty();
        vm.Matches.Should().BeEmpty();
        vm.MatchCount.Should().Be(0);
    }

    [Fact]
    public void Search_ByLabelSubstring_FindsMatchingPrompts()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());

        vm.Query = "email";

        vm.Matches.Should().ContainSingle();
        vm.Matches[0].Id.Should().Be("email");
    }

    [Fact]
    public void Search_ByResponseSubstring_FindsMatchingPrompts()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());

        vm.Query = "alex";

        vm.Matches.Should().HaveCount(2, "matches both Label-or-Id and Response containing 'alex'");
        vm.Matches.Should().Contain(m => m.Id == "name");
        vm.Matches.Should().Contain(m => m.Id == "email");
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());

        vm.Query = "FULL NAME";

        vm.Matches.Should().ContainSingle();
        vm.Matches[0].Id.Should().Be("name");
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsNoMatches_NotEverything()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());

        vm.Query = "";

        vm.Matches.Should().BeEmpty("empty query should not flood the user with all prompts");
    }

    [Fact]
    public void Search_NullDocument_IsSafe()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(null);

        vm.Query = "anything";

        vm.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Search_AcrossNestedSections_TraversesEverything()
    {
        var inner = new Section
        {
            Id = "inner",
            Title = "Inner",
            Prompts = new List<Prompt>
            {
                new() { Id = "deep", Label = "Deeply nested", Response = "found me" },
            },
        };
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new() { Id = "outer", Title = "Outer", Sections = new List<Section> { inner } },
            },
        };

        var vm = new SearchViewModel();
        vm.SetDocument(doc);
        vm.Query = "deeply";

        vm.Matches.Should().ContainSingle();
        vm.Matches[0].Id.Should().Be("deep");
    }

    [Fact]
    public void Clear_ResetsQueryAndMatches()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());
        vm.Query = "email";

        vm.Clear();

        vm.Query.Should().BeEmpty();
        vm.Matches.Should().BeEmpty();
    }

    [Fact]
    public void NextMatch_AndPreviousMatch_NavigateMatchesCyclically()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());
        vm.Query = "alex"; // matches name + email

        vm.CurrentMatchIndex.Should().Be(0);
        vm.CurrentMatch?.Id.Should().Be("name");

        vm.NextMatch();
        vm.CurrentMatch?.Id.Should().Be("email");

        vm.NextMatch();
        vm.CurrentMatch?.Id.Should().Be("name", "navigation cycles around");

        vm.PreviousMatch();
        vm.CurrentMatch?.Id.Should().Be("email");
    }

    [Fact]
    public void PropertyChanged_RaisedForMatchCountAndCurrentMatch_OnQueryChange()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Query = "email";

        changed.Should().Contain(nameof(vm.MatchCount));
        changed.Should().Contain(nameof(vm.CurrentMatch));
    }

    [Fact]
    public void NavigationOnEmptyMatches_IsSafe()
    {
        var vm = new SearchViewModel();
        vm.SetDocument(BuildDocument());

        vm.NextMatch();
        vm.PreviousMatch();

        vm.CurrentMatch.Should().BeNull();
    }
}

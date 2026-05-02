using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the Section model with recursive section support.
/// </summary>
public class SectionTests
{
    [Fact]
    public void Section_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var section = new Section();

        // Assert
        section.Id.Should().BeEmpty();
        section.Title.Should().BeEmpty();
        section.Description.Should().BeNull();
        section.Sections.Should().NotBeNull().And.BeEmpty();
        section.Prompts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SetId_ShouldStoreValue()
    {
        // Arrange
        var section = new Section();
        const string expectedId = "section_001";

        // Act
        section.Id = expectedId;

        // Assert
        section.Id.Should().Be(expectedId);
    }

    [Fact]
    public void SetTitle_ShouldStoreValue()
    {
        // Arrange
        var section = new Section();
        const string expectedTitle = "Personal Information";

        // Act
        section.Title = expectedTitle;

        // Assert
        section.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void SetDescription_ShouldStoreValue()
    {
        // Arrange
        var section = new Section();
        const string expectedDescription = "Your personal details";

        // Act
        section.Description = expectedDescription;

        // Assert
        section.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void AddChildSection_ShouldAddToCollection()
    {
        // Arrange
        var section = new Section();
        var childSection = new Section { Id = "child_001", Title = "Contact" };

        // Act
        section.Sections.Add(childSection);

        // Assert
        section.Sections.Should().ContainSingle()
            .Which.Should().BeSameAs(childSection);
    }

    [Fact]
    public void AddPrompt_ShouldAddToCollection()
    {
        // Arrange
        var section = new Section();
        var prompt = new Prompt { Id = "prompt_001", Label = "Age" };

        // Act
        section.Prompts.Add(prompt);

        // Assert
        section.Prompts.Should().ContainSingle()
            .Which.Should().BeSameAs(prompt);
    }

    [Fact]
    public void AddMultipleChildSections_ShouldMaintainOrder()
    {
        // Arrange
        var section = new Section();
        var child1 = new Section { Id = "child_001", Title = "First" };
        var child2 = new Section { Id = "child_002", Title = "Second" };
        var child3 = new Section { Id = "child_003", Title = "Third" };

        // Act
        section.Sections.Add(child1);
        section.Sections.Add(child2);
        section.Sections.Add(child3);

        // Assert
        section.Sections.Should().HaveCount(3)
            .And.ContainInOrder(child1, child2, child3);
    }

    [Fact]
    public void Section_CanHaveBothChildSectionsAndPrompts()
    {
        // Arrange
        var section = new Section();
        var childSection = new Section { Id = "child_001" };
        var prompt = new Prompt { Id = "prompt_001" };

        // Act
        section.Sections.Add(childSection);
        section.Prompts.Add(prompt);

        // Assert
        section.Sections.Should().ContainSingle();
        section.Prompts.Should().ContainSingle();
    }

    [Fact]
    public void Section_WithInitialValues_ShouldSetProperties()
    {
        // Arrange
        var childSection = new Section { Id = "child_001" };
        var prompt = new Prompt { Id = "prompt_001" };

        // Act
        var section = new Section
        {
            Id = "section_001",
            Title = "Employment History",
            Description = "Your work experience",
            Sections = new List<Section> { childSection },
            Prompts = new List<Prompt> { prompt }
        };

        // Assert
        section.Id.Should().Be("section_001");
        section.Title.Should().Be("Employment History");
        section.Description.Should().Be("Your work experience");
        section.Sections.Should().ContainSingle();
        section.Prompts.Should().ContainSingle();
    }

    [Fact]
    public void Section_WithNullDescription_ShouldAcceptNull()
    {
        // Arrange & Act
        var section = new Section
        {
            Id = "section_001",
            Title = "Test Section",
            Description = null
        };

        // Assert
        section.Description.Should().BeNull();
    }

    [Fact]
    public void Section_WithOnlyPrompts_ShouldBeValid()
    {
        // Arrange & Act
        var section = new Section
        {
            Id = "section_001",
            Title = "Simple Section",
            Prompts = new List<Prompt>
            {
                new() { Id = "prompt_001" },
                new() { Id = "prompt_002" }
            }
        };

        // Assert
        section.Sections.Should().BeEmpty();
        section.Prompts.Should().HaveCount(2);
    }

    [Fact]
    public void Section_WithOnlyChildSections_ShouldBeValid()
    {
        // Arrange & Act
        var section = new Section
        {
            Id = "section_001",
            Title = "Complex Section",
            Sections = new List<Section>
            {
                new() { Id = "child_001" },
                new() { Id = "child_002" }
            }
        };

        // Assert
        section.Sections.Should().HaveCount(2);
        section.Prompts.Should().BeEmpty();
    }

    [Fact]
    public void Section_WithDeepNesting_ShouldSupportMultipleLevels()
    {
        // Arrange & Act - Create 3 levels of nesting
        var level3 = new Section
        {
            Id = "level3",
            Title = "Level 3",
            Prompts = new List<Prompt> { new() { Id = "prompt_001", Label = "Deep Prompt" } }
        };

        var level2 = new Section
        {
            Id = "level2",
            Title = "Level 2",
            Sections = new List<Section> { level3 }
        };

        var level1 = new Section
        {
            Id = "level1",
            Title = "Level 1",
            Sections = new List<Section> { level2 }
        };

        // Assert
        level1.Sections.Should().ContainSingle();
        level1.Sections[0].Sections.Should().ContainSingle();
        level1.Sections[0].Sections[0].Prompts.Should().ContainSingle();
        level1.Sections[0].Sections[0].Prompts[0].Label.Should().Be("Deep Prompt");
    }

    [Fact]
    public void Section_WithMultipleBranchesAtDifferentDepths_ShouldWork()
    {
        // Arrange & Act
        var root = new Section
        {
            Id = "root",
            Title = "Root Section",
            Sections = new List<Section>
            {
                new()
                {
                    Id = "branch1",
                    Title = "Branch 1",
                    Prompts = new List<Prompt> { new() { Id = "p1", Label = "Q1" } }
                },
                new()
                {
                    Id = "branch2",
                    Title = "Branch 2",
                    Sections = new List<Section>
                    {
                        new()
                        {
                            Id = "branch2_1",
                            Title = "Branch 2.1",
                            Prompts = new List<Prompt> { new() { Id = "p2", Label = "Q2" } }
                        }
                    }
                }
            },
            Prompts = new List<Prompt> { new() { Id = "p3", Label = "Root Prompt" } }
        };

        // Assert
        root.Sections.Should().HaveCount(2);
        root.Sections[0].Prompts.Should().ContainSingle();
        root.Sections[1].Sections.Should().ContainSingle();
        root.Sections[1].Sections[0].Prompts.Should().ContainSingle();
        root.Prompts.Should().ContainSingle();
    }

    [Fact]
    public void Section_WithFiveLevelsDeep_ShouldWorkWithoutLimit()
    {
        // Arrange & Act - Test 5 levels to verify no arbitrary depth limit
        var level5 = new Section { Id = "l5", Title = "Level 5", Prompts = new List<Prompt> { new() { Id = "p1" } } };
        var level4 = new Section { Id = "l4", Title = "Level 4", Sections = new List<Section> { level5 } };
        var level3 = new Section { Id = "l3", Title = "Level 3", Sections = new List<Section> { level4 } };
        var level2 = new Section { Id = "l2", Title = "Level 2", Sections = new List<Section> { level3 } };
        var level1 = new Section { Id = "l1", Title = "Level 1", Sections = new List<Section> { level2 } };

        // Assert - Navigate all 5 levels
        level1.Sections[0].Sections[0].Sections[0].Sections[0].Id.Should().Be("l5");
        level1.Sections[0].Sections[0].Sections[0].Sections[0].Prompts.Should().ContainSingle();
    }
}

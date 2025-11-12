using FluentAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the Section model.
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
        section.Subsections.Should().NotBeNull().And.BeEmpty();
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
    public void AddSubsection_ShouldAddToCollection()
    {
        // Arrange
        var section = new Section();
        var subsection = new Subsection { Id = "subsection_001", Title = "Contact" };

        // Act
        section.Subsections.Add(subsection);

        // Assert
        section.Subsections.Should().ContainSingle()
            .Which.Should().BeSameAs(subsection);
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
    public void AddMultipleSubsections_ShouldMaintainOrder()
    {
        // Arrange
        var section = new Section();
        var sub1 = new Subsection { Id = "sub_001", Title = "First" };
        var sub2 = new Subsection { Id = "sub_002", Title = "Second" };
        var sub3 = new Subsection { Id = "sub_003", Title = "Third" };

        // Act
        section.Subsections.Add(sub1);
        section.Subsections.Add(sub2);
        section.Subsections.Add(sub3);

        // Assert
        section.Subsections.Should().HaveCount(3)
            .And.ContainInOrder(sub1, sub2, sub3);
    }

    [Fact]
    public void Section_CanHaveBothSubsectionsAndPrompts()
    {
        // Arrange
        var section = new Section();
        var subsection = new Subsection { Id = "sub_001" };
        var prompt = new Prompt { Id = "prompt_001" };

        // Act
        section.Subsections.Add(subsection);
        section.Prompts.Add(prompt);

        // Assert
        section.Subsections.Should().ContainSingle();
        section.Prompts.Should().ContainSingle();
    }

    [Fact]
    public void Section_WithInitialValues_ShouldSetProperties()
    {
        // Arrange
        var subsection = new Subsection { Id = "sub_001" };
        var prompt = new Prompt { Id = "prompt_001" };

        // Act
        var section = new Section
        {
            Id = "section_001",
            Title = "Employment History",
            Description = "Your work experience",
            Subsections = new List<Subsection> { subsection },
            Prompts = new List<Prompt> { prompt }
        };

        // Assert
        section.Id.Should().Be("section_001");
        section.Title.Should().Be("Employment History");
        section.Description.Should().Be("Your work experience");
        section.Subsections.Should().ContainSingle();
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
        section.Subsections.Should().BeEmpty();
        section.Prompts.Should().HaveCount(2);
    }

    [Fact]
    public void Section_WithOnlySubsections_ShouldBeValid()
    {
        // Arrange & Act
        var section = new Section
        {
            Id = "section_001",
            Title = "Complex Section",
            Subsections = new List<Subsection>
            {
                new() { Id = "sub_001" },
                new() { Id = "sub_002" }
            }
        };

        // Assert
        section.Subsections.Should().HaveCount(2);
        section.Prompts.Should().BeEmpty();
    }
}

using FluentAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the Subsection model.
/// </summary>
public class SubsectionTests
{
    [Fact]
    public void Subsection_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var subsection = new Subsection();

        // Assert
        subsection.Id.Should().BeEmpty();
        subsection.Title.Should().BeEmpty();
        subsection.Description.Should().BeNull();
        subsection.Prompts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SetId_ShouldStoreValue()
    {
        // Arrange
        var subsection = new Subsection();
        const string expectedId = "subsection_001_001";

        // Act
        subsection.Id = expectedId;

        // Assert
        subsection.Id.Should().Be(expectedId);
    }

    [Fact]
    public void SetTitle_ShouldStoreValue()
    {
        // Arrange
        var subsection = new Subsection();
        const string expectedTitle = "Name and Contact";

        // Act
        subsection.Title = expectedTitle;

        // Assert
        subsection.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void SetDescription_ShouldStoreValue()
    {
        // Arrange
        var subsection = new Subsection();
        const string expectedDescription = "Basic contact information";

        // Act
        subsection.Description = expectedDescription;

        // Assert
        subsection.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void AddPrompt_ShouldAddToCollection()
    {
        // Arrange
        var subsection = new Subsection();
        var prompt = new Prompt { Id = "prompt_001", Label = "Name" };

        // Act
        subsection.Prompts.Add(prompt);

        // Assert
        subsection.Prompts.Should().ContainSingle()
            .Which.Should().BeSameAs(prompt);
    }

    [Fact]
    public void AddMultiplePrompts_ShouldMaintainOrder()
    {
        // Arrange
        var subsection = new Subsection();
        var prompt1 = new Prompt { Id = "prompt_001", Label = "First Name" };
        var prompt2 = new Prompt { Id = "prompt_002", Label = "Last Name" };
        var prompt3 = new Prompt { Id = "prompt_003", Label = "Email" };

        // Act
        subsection.Prompts.Add(prompt1);
        subsection.Prompts.Add(prompt2);
        subsection.Prompts.Add(prompt3);

        // Assert
        subsection.Prompts.Should().HaveCount(3)
            .And.ContainInOrder(prompt1, prompt2, prompt3);
    }

    [Fact]
    public void Subsection_WithInitialValues_ShouldSetProperties()
    {
        // Arrange
        var prompt1 = new Prompt { Id = "prompt_001" };
        var prompt2 = new Prompt { Id = "prompt_002" };

        // Act
        var subsection = new Subsection
        {
            Id = "subsection_001_001",
            Title = "Personal Details",
            Description = "Your personal information",
            Prompts = new List<Prompt> { prompt1, prompt2 }
        };

        // Assert
        subsection.Id.Should().Be("subsection_001_001");
        subsection.Title.Should().Be("Personal Details");
        subsection.Description.Should().Be("Your personal information");
        subsection.Prompts.Should().HaveCount(2);
    }

    [Fact]
    public void Subsection_WithNullDescription_ShouldAcceptNull()
    {
        // Arrange & Act
        var subsection = new Subsection
        {
            Id = "subsection_001",
            Title = "Test",
            Description = null
        };

        // Assert
        subsection.Description.Should().BeNull();
    }
}

using FluentAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the PromptHints model.
/// </summary>
public class PromptHintsTests
{
    [Fact]
    public void PromptHints_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var hints = new PromptHints();

        // Assert
        hints.Placeholder.Should().BeNull();
        hints.ExpectedDataType.Should().BeNull();
        hints.SuggestedValues.Should().NotBeNull().And.BeEmpty();
        hints.HelpText.Should().BeNull();
        hints.ValidationPattern.Should().BeNull();
    }

    [Fact]
    public void SetPlaceholder_ShouldStoreValue()
    {
        // Arrange
        var hints = new PromptHints();
        const string expectedPlaceholder = "Enter your name";

        // Act
        hints.Placeholder = expectedPlaceholder;

        // Assert
        hints.Placeholder.Should().Be(expectedPlaceholder);
    }

    [Fact]
    public void SetExpectedDataType_ShouldStoreValue()
    {
        // Arrange
        var hints = new PromptHints();
        const string expectedType = "email";

        // Act
        hints.ExpectedDataType = expectedType;

        // Assert
        hints.ExpectedDataType.Should().Be(expectedType);
    }

    [Fact]
    public void SetSuggestedValues_ShouldStoreValues()
    {
        // Arrange
        var hints = new PromptHints();
        var expectedValues = new List<string> { "Option 1", "Option 2", "Option 3" };

        // Act
        hints.SuggestedValues = expectedValues;

        // Assert
        hints.SuggestedValues.Should().BeEquivalentTo(expectedValues);
    }

    [Fact]
    public void SetHelpText_ShouldStoreValue()
    {
        // Arrange
        var hints = new PromptHints();
        const string expectedHelp = "Please enter a valid email address";

        // Act
        hints.HelpText = expectedHelp;

        // Assert
        hints.HelpText.Should().Be(expectedHelp);
    }

    [Fact]
    public void SetValidationPattern_ShouldStoreValue()
    {
        // Arrange
        var hints = new PromptHints();
        const string expectedPattern = @"^\d{4}-\d{2}-\d{2}$";

        // Act
        hints.ValidationPattern = expectedPattern;

        // Assert
        hints.ValidationPattern.Should().Be(expectedPattern);
    }

    [Fact]
    public void PromptHints_WithInitialValues_ShouldSetProperties()
    {
        // Arrange & Act
        var hints = new PromptHints
        {
            Placeholder = "YYYY-MM-DD",
            ExpectedDataType = "date",
            SuggestedValues = new List<string> { "2025-01-01", "2025-12-31" },
            HelpText = "Enter the date",
            ValidationPattern = @"^\d{4}-\d{2}-\d{2}$"
        };

        // Assert
        hints.Placeholder.Should().Be("YYYY-MM-DD");
        hints.ExpectedDataType.Should().Be("date");
        hints.SuggestedValues.Should().HaveCount(2);
        hints.HelpText.Should().Be("Enter the date");
        hints.ValidationPattern.Should().Be(@"^\d{4}-\d{2}-\d{2}$");
    }
}

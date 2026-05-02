using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the Prompt model.
/// </summary>
public class PromptTests
{
    [Fact]
    public void Prompt_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var prompt = new Prompt();

        // Assert
        prompt.Id.Should().BeEmpty();
        prompt.Label.Should().BeEmpty();
        prompt.Response.Should().BeEmpty();
        prompt.Hints.Should().NotBeNull();
        prompt.ResponseMetadata.Should().NotBeNull();
    }

    [Fact]
    public void SetId_ShouldStoreValue()
    {
        // Arrange
        var prompt = new Prompt();
        const string expectedId = "prompt_001";

        // Act
        prompt.Id = expectedId;

        // Assert
        prompt.Id.Should().Be(expectedId);
    }

    [Fact]
    public void SetLabel_ShouldStoreValue()
    {
        // Arrange
        var prompt = new Prompt();
        const string expectedLabel = "Full Legal Name";

        // Act
        prompt.Label = expectedLabel;

        // Assert
        prompt.Label.Should().Be(expectedLabel);
    }

    [Fact]
    public void SetResponse_ShouldStoreValue()
    {
        // Arrange
        var prompt = new Prompt();
        const string expectedResponse = "John Doe";

        // Act
        prompt.Response = expectedResponse;

        // Assert
        prompt.Response.Should().Be(expectedResponse);
    }

    [Fact]
    public void SetResponse_ShouldUpdateLastModified()
    {
        // Arrange
        var prompt = new Prompt();
        var beforeTime = DateTime.UtcNow;

        // Wait a tiny bit to ensure time difference
        Thread.Sleep(10);

        // Act
        prompt.Response = "Test response";

        // Assert
        prompt.ResponseMetadata.LastModified.Should().BeAfter(beforeTime);
    }

    [Fact]
    public void SetResponse_WithNull_ShouldStoreEmptyString()
    {
        // Arrange
        var prompt = new Prompt();

        // Act
        prompt.Response = null!;

        // Assert
        prompt.Response.Should().BeEmpty();
    }

    [Fact]
    public void SetResponse_MultipleTimes_ShouldUpdateLastModifiedEachTime()
    {
        // Arrange
        var prompt = new Prompt();
        prompt.Response = "First";
        var firstModified = prompt.ResponseMetadata.LastModified;

        Thread.Sleep(10);

        // Act
        prompt.Response = "Second";

        // Assert
        prompt.ResponseMetadata.LastModified.Should().NotBeNull();
        prompt.ResponseMetadata.LastModified!.Value.Should().BeAfter(firstModified!.Value);
    }

    [Fact]
    public void SetResponse_WithEmptyString_ShouldUpdateLastModified()
    {
        // Arrange
        var prompt = new Prompt();
        var beforeTime = DateTime.UtcNow;

        Thread.Sleep(10);

        // Act
        prompt.Response = "";

        // Assert
        prompt.ResponseMetadata.LastModified.Should().BeAfter(beforeTime);
    }

    [Fact]
    public void SetResponse_WithWhitespace_ShouldPreserveWhitespace()
    {
        // Arrange
        var prompt = new Prompt();
        const string whitespaceResponse = "  spaces  ";

        // Act
        prompt.Response = whitespaceResponse;

        // Assert
        prompt.Response.Should().Be(whitespaceResponse);
    }

    [Fact]
    public void SetResponse_WithMultilineText_ShouldPreserveNewlines()
    {
        // Arrange
        var prompt = new Prompt();
        const string multilineResponse = "Line 1\nLine 2\nLine 3";

        // Act
        prompt.Response = multilineResponse;

        // Assert
        prompt.Response.Should().Be(multilineResponse);
    }

    [Fact]
    public void Prompt_WithInitialValues_ShouldSetProperties()
    {
        // Arrange
        const string id = "prompt_001";
        const string label = "Email Address";
        const string response = "test@example.com";

        // Act
        var prompt = new Prompt
        {
            Id = id,
            Label = label,
            Response = response
        };

        // Assert
        prompt.Id.Should().Be(id);
        prompt.Label.Should().Be(label);
        prompt.Response.Should().Be(response);
    }
}

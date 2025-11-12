using FluentAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the ResponseMetadata model.
/// </summary>
public class ResponseMetadataTests
{
    [Fact]
    public void ResponseMetadata_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var metadata = new ResponseMetadata();

        // Assert
        metadata.InferredDataType.Should().BeNull();
        metadata.LastModified.Should().BeNull();
    }

    [Fact]
    public void SetInferredDataType_ShouldStoreValue()
    {
        // Arrange
        var metadata = new ResponseMetadata();
        const string expectedType = "email";

        // Act
        metadata.InferredDataType = expectedType;

        // Assert
        metadata.InferredDataType.Should().Be(expectedType);
    }

    [Fact]
    public void SetLastModified_ShouldStoreValue()
    {
        // Arrange
        var metadata = new ResponseMetadata();
        var expectedTime = DateTime.UtcNow;

        // Act
        metadata.LastModified = expectedTime;

        // Assert
        metadata.LastModified.Should().Be(expectedTime);
    }

    [Fact]
    public void ResponseMetadata_WithInitialValues_ShouldSetProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var metadata = new ResponseMetadata
        {
            InferredDataType = "date",
            LastModified = timestamp
        };

        // Assert
        metadata.InferredDataType.Should().Be("date");
        metadata.LastModified.Should().Be(timestamp);
    }
}

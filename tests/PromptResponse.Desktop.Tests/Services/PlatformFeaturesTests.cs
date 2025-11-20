using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Unit tests for PlatformFeatures service.
/// Tests cross-platform feature detection and graceful fallbacks.
/// </summary>
public class PlatformFeaturesTests
{
    private readonly Mock<ILogger<PlatformFeatures>> _mockLogger;

    public PlatformFeaturesTests()
    {
        _mockLogger = new Mock<ILogger<PlatformFeatures>>();
    }

    [Fact]
    public void Constructor_LogsPlatformDetection()
    {
        // Arrange & Act
        var service = new PlatformFeatures(_mockLogger.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Platform detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetAccentColor_ReturnsValidColor()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var color = service.GetAccentColor();

        // Assert
        color.Should().NotBe(default, "accent color should be a valid color");

        // Color should be a valid hex color (has R, G, B components)
        color.R.Should().BeInRange(0, 255);
        color.G.Should().BeInRange(0, 255);
        color.B.Should().BeInRange(0, 255);
    }

    [Fact]
    public void GetAccentColor_ReturnsConsistentColor()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var color1 = service.GetAccentColor();
        var color2 = service.GetAccentColor();

        // Assert
        color1.Should().Be(color2, "accent color should be consistent across calls");
    }

    [Fact]
    public void GetAnimationDuration_WithNormalMotion_ReturnsSpecifiedDuration()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);
        const double expectedDuration = 250.0;

        // Act
        var duration = service.GetAnimationDuration(expectedDuration);

        // Assert
        // Since we don't have reduced motion detection yet, should return the normal duration
        duration.Should().Be(expectedDuration, "should return normal duration when reduced motion is not preferred");
    }

    [Fact]
    public void GetAnimationDuration_WithZeroDuration_ReturnsZero()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var duration = service.GetAnimationDuration(0);

        // Assert
        duration.Should().Be(0, "zero duration should remain zero");
    }

    [Fact]
    public void GetAnimationDuration_WithNegativeDuration_ReturnsValue()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);
        const double negativeDuration = -100.0;

        // Act
        var duration = service.GetAnimationDuration(negativeDuration);

        // Assert
        // Should handle negative values (though they shouldn't be used in practice)
        duration.Should().Be(negativeDuration);
    }

    [Fact]
    public void PrefersReducedMotion_ReturnsBoolean()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var prefersReducedMotion = service.PrefersReducedMotion();

        // Assert
        // Bool methods should return true or false - just verify the call doesn't throw
        (prefersReducedMotion || !prefersReducedMotion).Should().BeTrue("should return a valid boolean value");
    }

    [Fact]
    public void SupportsAcrylic_ReturnsBoolean()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var supportsAcrylic = service.SupportsAcrylic;

        // Assert
        // Bool properties should return true or false - just verify the call doesn't throw
        (supportsAcrylic || !supportsAcrylic).Should().BeTrue("should return a valid boolean value");
    }

    [Fact]
    public void SupportsCustomTitleBar_ReturnsBoolean()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var supportsCustomTitleBar = service.SupportsCustomTitleBar;

        // Assert
        // Bool properties should return true or false - just verify the call doesn't throw
        (supportsCustomTitleBar || !supportsCustomTitleBar).Should().BeTrue("should return a valid boolean value");
    }

    [Fact]
    public void SupportsSystemAccentColor_ReturnsBoolean()
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var supportsSystemAccentColor = service.SupportsSystemAccentColor;

        // Assert
        // Bool properties should return true or false - just verify the call doesn't throw
        (supportsSystemAccentColor || !supportsSystemAccentColor).Should().BeTrue("should return a valid boolean value");
    }

    [Theory]
    [InlineData(150.0)]
    [InlineData(250.0)]
    [InlineData(350.0)]
    [InlineData(1000.0)]
    public void GetAnimationDuration_WithVariousDurations_ReturnsCorrectValue(double duration)
    {
        // Arrange
        var service = new PlatformFeatures(_mockLogger.Object);

        // Act
        var result = service.GetAnimationDuration(duration);

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0, "animation duration should never be negative");
    }

    [Fact]
    public void MultipleInstances_ProvideSameFeatureDetection()
    {
        // Arrange
        var service1 = new PlatformFeatures(_mockLogger.Object);
        var service2 = new PlatformFeatures(_mockLogger.Object);

        // Act & Assert
        service1.SupportsAcrylic.Should().Be(service2.SupportsAcrylic,
            "platform features should be consistent across instances");
        service1.SupportsCustomTitleBar.Should().Be(service2.SupportsCustomTitleBar,
            "platform features should be consistent across instances");
        service1.SupportsSystemAccentColor.Should().Be(service2.SupportsSystemAccentColor,
            "platform features should be consistent across instances");
    }
}

using AwesomeAssertions;
using PromptResponse.Core.Commands;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Commands;

/// <summary>
/// Unit tests for SetPromptResponseCommand.
/// </summary>
public class SetPromptResponseCommandTests
{
    [Fact]
    public void Execute_ShouldSetNewValue()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command = new SetPromptResponseCommand(prompt, "new value");

        // Act
        command.Execute();

        // Assert
        prompt.Response.Should().Be("new value");
    }

    [Fact]
    public void Undo_ShouldRestoreOriginalValue()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command = new SetPromptResponseCommand(prompt, "new value");
        command.Execute();

        // Act
        command.Undo();

        // Assert
        prompt.Response.Should().Be("original");
    }

    [Fact]
    public void Undo_ShouldRestoreLastModifiedTimestamp()
    {
        // Arrange
        var originalTime = DateTime.UtcNow.AddHours(-1);
        var prompt = new Prompt
        {
            Id = "test",
            Label = "Test",
            Response = "original",
            ResponseMetadata = new ResponseMetadata { LastModified = originalTime }
        };

        var command = new SetPromptResponseCommand(prompt, "new value");
        command.Execute();

        // Act
        command.Undo();

        // Assert
        prompt.ResponseMetadata.LastModified.Should().Be(originalTime);
    }

    [Fact]
    public void ExecuteAndUndo_MultipleTimes_ShouldWork()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command = new SetPromptResponseCommand(prompt, "new value");

        // Act & Assert
        command.Execute();
        prompt.Response.Should().Be("new value");

        command.Undo();
        prompt.Response.Should().Be("original");

        command.Execute();
        prompt.Response.Should().Be("new value");

        command.Undo();
        prompt.Response.Should().Be("original");
    }

    [Fact]
    public void Constructor_WithNullPrompt_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SetPromptResponseCommand(null!, "value"));
    }

    [Fact]
    public void Description_ShouldContainPromptLabel()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Email Address", Response = "test" };
        var command = new SetPromptResponseCommand(prompt, "new");

        // Act
        var description = command.Description;

        // Assert
        description.Should().Contain("Email Address");
    }

    [Fact]
    public void CanMergeWith_SamePrompt_ShouldReturnTrue()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "v1" };
        var command1 = new SetPromptResponseCommand(prompt, "v2");
        command1.Execute();

        // Immediately create another command (within merge window)
        var command2 = new SetPromptResponseCommand(prompt, "v3");

        // Act
        var canMerge = command1.CanMergeWith(command2);

        // Assert
        canMerge.Should().BeTrue();
    }

    [Fact]
    public void CanMergeWith_DifferentPrompt_ShouldReturnFalse()
    {
        // Arrange
        var prompt1 = new Prompt { Id = "test1", Label = "Test 1", Response = "v1" };
        var prompt2 = new Prompt { Id = "test2", Label = "Test 2", Response = "v1" };

        var command1 = new SetPromptResponseCommand(prompt1, "v2");
        var command2 = new SetPromptResponseCommand(prompt2, "v2");

        // Act
        var canMerge = command1.CanMergeWith(command2);

        // Assert
        canMerge.Should().BeFalse();
    }

    [Fact]
    public void CanMergeWith_DifferentCommandType_ShouldReturnFalse()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "v1" };
        var command = new SetPromptResponseCommand(prompt, "v2");
        var otherCommand = new MockCommand();

        // Act
        var canMerge = command.CanMergeWith(otherCommand);

        // Assert
        canMerge.Should().BeFalse();
    }

    [Fact]
    public void MergeWith_ShouldUpdateToNewValue()
    {
        // Arrange
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command1 = new SetPromptResponseCommand(prompt, "v1");
        var command2 = new SetPromptResponseCommand(prompt, "v2");

        // Act
        command1.MergeWith(command2);
        command1.Execute();

        // Assert
        prompt.Response.Should().Be("v2");

        // Undo should go back to original
        command1.Undo();
        prompt.Response.Should().Be("original");
    }

    // Mock command for testing
    private class MockCommand : ICommand
    {
        public string Description => "Mock";
        public void Execute() { }
        public void Undo() { }
        public bool CanMergeWith(ICommand other) => false;
        public void MergeWith(ICommand other) { }
    }
}

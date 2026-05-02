using AwesomeAssertions;
using PromptResponse.Core.Commands;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Commands;

/// <summary>
/// Unit tests for UndoRedoManager.
/// </summary>
public class UndoRedoManagerTests
{
    [Fact]
    public void NewManager_ShouldHaveNoUndoRedo()
    {
        // Arrange & Act
        var manager = new UndoRedoManager();

        // Assert
        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
        manager.UndoCount.Should().Be(0);
        manager.RedoCount.Should().Be(0);
    }

    [Fact]
    public void ExecuteCommand_ShouldEnableUndo()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command = new SetPromptResponseCommand(prompt, "new value");

        // Act
        manager.ExecuteCommand(command);

        // Assert
        manager.CanUndo.Should().BeTrue();
        manager.CanRedo.Should().BeFalse();
        manager.UndoCount.Should().Be(1);
        prompt.Response.Should().Be("new value");
    }

    [Fact]
    public void Undo_ShouldRevertCommand()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command = new SetPromptResponseCommand(prompt, "new value");
        manager.ExecuteCommand(command);

        // Act
        manager.Undo();

        // Assert
        prompt.Response.Should().Be("original");
        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_ShouldReapplyCommand()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var command = new SetPromptResponseCommand(prompt, "new value");
        manager.ExecuteCommand(command);
        manager.Undo();

        // Act
        manager.Redo();

        // Assert
        prompt.Response.Should().Be("new value");
        manager.CanUndo.Should().BeTrue();
        manager.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void MultipleCommands_ShouldUndoInReverseOrder()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "v0" };

        // Execute commands with delays to prevent merge optimization
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v1"));
        Thread.Sleep(60); // Exceed 50ms merge window
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v2"));
        Thread.Sleep(60);
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v3"));

        // Act & Assert
        prompt.Response.Should().Be("v3");

        manager.Undo();
        prompt.Response.Should().Be("v2");

        manager.Undo();
        prompt.Response.Should().Be("v1");

        manager.Undo();
        prompt.Response.Should().Be("v0");

        manager.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void ExecutingNewCommand_ShouldClearRedoStack()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "v0" };

        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v1"));
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v2"));
        manager.Undo(); // Now can redo

        // Act
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v3"));

        // Assert
        manager.CanRedo.Should().BeFalse();
        manager.RedoCount.Should().Be(0);
    }

    [Fact]
    public void MaxUndoLevels_ShouldLimitStackSize()
    {
        // Arrange
        var manager = new UndoRedoManager(maxUndoLevels: 3);
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "v0" };

        // Act - Add more than max
        for (int i = 1; i <= 5; i++)
        {
            manager.ExecuteCommand(new SetPromptResponseCommand(prompt, $"v{i}"));
        }

        // Assert
        manager.UndoCount.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Clear_ShouldResetBothStacks()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "v0" };

        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v1"));
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "v2"));
        manager.Undo();

        // Act
        manager.Clear();

        // Assert
        manager.CanUndo.Should().BeFalse();
        manager.CanRedo.Should().BeFalse();
        manager.UndoCount.Should().Be(0);
        manager.RedoCount.Should().Be(0);
    }

    [Fact]
    public void GetUndoDescription_ShouldReturnCommandDescription()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test Prompt", Response = "original" };
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "new"));

        // Act
        var description = manager.GetUndoDescription();

        // Assert
        description.Should().Contain("Test Prompt");
    }

    [Fact]
    public void GetRedoDescription_ShouldReturnCommandDescription()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test Prompt", Response = "original" };
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "new"));
        manager.Undo();

        // Act
        var description = manager.GetRedoDescription();

        // Assert
        description.Should().Contain("Test Prompt");
    }

    [Fact]
    public void StateChanged_ShouldFireOnCommandExecution()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        var eventFired = false;
        manager.StateChanged += (s, e) => eventFired = true;

        // Act
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "new"));

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void StateChanged_ShouldFireOnUndo()
    {
        // Arrange
        var manager = new UndoRedoManager();
        var prompt = new Prompt { Id = "test", Label = "Test", Response = "original" };
        manager.ExecuteCommand(new SetPromptResponseCommand(prompt, "new"));

        var eventFired = false;
        manager.StateChanged += (s, e) => eventFired = true;

        // Act
        manager.Undo();

        // Assert
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void Undo_WithEmptyStack_ShouldThrow()
    {
        // Arrange
        var manager = new UndoRedoManager();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => manager.Undo());
    }

    [Fact]
    public void Redo_WithEmptyStack_ShouldThrow()
    {
        // Arrange
        var manager = new UndoRedoManager();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => manager.Redo());
    }

    [Fact]
    public void ExecuteCommand_WithNull_ShouldThrow()
    {
        // Arrange
        var manager = new UndoRedoManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.ExecuteCommand(null!));
    }
}

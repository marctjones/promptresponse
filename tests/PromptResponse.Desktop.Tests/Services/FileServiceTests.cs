using FluentAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Unit tests for FileService.
/// </summary>
/// <remarks>
/// These tests are skipped for now as FileService depends on Avalonia UI dialogs.
/// TODO: Implement tests with mocked Avalonia StorageProvider or refactor FileService
/// to separate file I/O from dialog logic.
/// </remarks>
public class FileServiceTests
{
    [Fact(Skip = "FileService depends on Avalonia dialogs - TODO: refactor or mock")]
    public async Task OpenFileAsync_WithValidFile_ShouldReturnDocument()
    {
        // TODO: Mock Avalonia StorageProvider
        // TODO: Create test APR file
        // TODO: Verify document is loaded correctly
        await Task.CompletedTask;
        Assert.True(true, "Placeholder test - implement with proper mocks");
    }

    [Fact(Skip = "FileService depends on Avalonia dialogs - TODO: refactor or mock")]
    public async Task SaveFileAsAsync_WithValidDocument_ShouldSaveFile()
    {
        // TODO: Mock Avalonia StorageProvider
        // TODO: Create test document
        // TODO: Verify file is saved with correct extension
        await Task.CompletedTask;
        Assert.True(true, "Placeholder test - implement with proper mocks");
    }

    [Fact(Skip = "FileService depends on Avalonia dialogs - TODO: refactor or mock")]
    public void ExtensionOverride_AprtFile_ShouldTreatAsTemplate()
    {
        // TODO: Test that .aprt files always get DocumentType.Template
        // TODO: Verify it overrides the DocumentType from file content
        Assert.True(true, "Placeholder test - implement with proper mocks");
    }

    [Fact(Skip = "FileService depends on Avalonia dialogs - TODO: refactor or mock")]
    public void ExtensionOverride_AprfFile_ShouldTreatAsFilledForm()
    {
        // TODO: Test that .aprf files always get DocumentType.FilledForm
        // TODO: Verify it overrides the DocumentType from file content
        Assert.True(true, "Placeholder test - implement with proper mocks");
    }

    [Fact(Skip = "FileService depends on Avalonia dialogs - TODO: refactor or mock")]
    public void ClearCurrentFilePath_ShouldResetPath()
    {
        // TODO: Test that ClearCurrentFilePath() sets CurrentFilePath to null
        Assert.True(true, "Placeholder test - implement with proper mocks");
    }
}

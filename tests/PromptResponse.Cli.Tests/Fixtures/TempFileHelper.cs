using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Tests.Fixtures;

/// <summary>
/// Helper for creating and managing temporary test files.
/// </summary>
public class TempFileHelper : IDisposable
{
    private readonly IAprSerializer _serializer;
    private readonly string _tempDirectory;
    private readonly List<string> _filesToCleanup = new();

    public TempFileHelper(IAprSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"apr-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    /// Creates a test file from an APR document.
    /// </summary>
    public string CreateTempFile(AprDocument document, string? fileName = null)
    {
        fileName ??= $"test-{Guid.NewGuid():N}.apr";
        var filePath = Path.Combine(_tempDirectory, fileName);

        var json = _serializer.Serialize(document);
        File.WriteAllText(filePath, json);

        _filesToCleanup.Add(filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a temporary template file.
    /// </summary>
    public string CreateTemplateFile(string? fileName = null)
    {
        return CreateTempFile(TestDocumentFactory.CreateComplexTemplate(), fileName ?? "template.aprt");
    }

    /// <summary>
    /// Creates a temporary filled form file.
    /// </summary>
    public string CreateFilledFormFile(string? fileName = null)
    {
        return CreateTempFile(TestDocumentFactory.CreateFilledForm(), fileName ?? "filled.aprf");
    }

    /// <summary>
    /// Creates a temporary file with custom content.
    /// </summary>
    public string CreateFileWithContent(string content, string? fileName = null)
    {
        fileName ??= $"test-{Guid.NewGuid():N}.json";
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, content);
        _filesToCleanup.Add(filePath);
        return filePath;
    }

    /// <summary>
    /// Gets the full path to a file in the temp directory.
    /// </summary>
    public string GetPath(string fileName) => Path.Combine(_tempDirectory, fileName);

    public void Dispose()
    {
        foreach (var file in _filesToCleanup)
        {
            try { File.Delete(file); }
            catch { /* Ignore cleanup errors */ }
        }

        try { Directory.Delete(_tempDirectory, true); }
        catch { /* Ignore cleanup errors */ }
    }
}

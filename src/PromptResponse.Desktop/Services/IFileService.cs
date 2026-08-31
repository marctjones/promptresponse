using PromptResponse.Core.Models;
using PromptResponse.Core.Beta6;
using System.Security.Cryptography.X509Certificates;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Service for file operations.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Shows an open file dialog and loads an APR document.
    /// </summary>
    /// <returns>The loaded document, or null if cancelled.</returns>
    Task<AprDocument?> OpenFileAsync();

    /// <summary>
    /// Loads an APR document directly from a known file path. No dialog is shown.
    /// Used for command-line "--open path" startup and demo flows.
    /// </summary>
    /// <returns>The loaded document, or null if the path doesn't exist or fails to parse.</returns>
    Task<AprDocument?> LoadFileAsync(string filePath);

    /// <summary>
    /// Shows a save file dialog and saves an APR document.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <returns>True if saved successfully.</returns>
    Task<bool> SaveFileAsAsync(AprDocument document);

    /// <summary>
    /// Saves an APR document to a specific path.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="filePath">The file path.</param>
    Task SaveFileAsync(AprDocument document, string filePath);

    /// <summary>
    /// Shows a save-file dialog for exporting to a PDF and returns the chosen
    /// path, or null if cancelled. Does not write anything — the caller renders
    /// to the returned path.
    /// </summary>
    /// <param name="suggestedFileName">The default file name (e.g. "My Form.pdf").</param>
    Task<string?> PickPdfExportPathAsync(string suggestedFileName);

    /// <summary>
    /// Shows a save-file dialog for exporting to an arbitrary format and returns
    /// the chosen path, or null if cancelled. Does not write anything — the caller
    /// renders to the returned path.
    /// </summary>
    /// <param name="suggestedFileName">The default file name (e.g. "My Form.html").</param>
    /// <param name="title">The dialog title (e.g. "Export web form").</param>
    /// <param name="typeLabel">The file-type label shown in the picker (e.g. "HTML Page").</param>
    /// <param name="extension">The default extension without a dot (e.g. "html").</param>
    Task<string?> PickExportPathAsync(string suggestedFileName, string title, string typeLabel, string extension);

    /// <summary>
    /// Shows an open-file dialog filtered to PDFs and returns the chosen path, or
    /// null if cancelled. Does not read the file — the caller imports it.
    /// </summary>
    Task<string?> PickPdfImportPathAsync();

    /// <summary>
    /// Shows an open-file dialog filtered to PKCS#12 certificates (.pfx/.p12) and
    /// returns the chosen path, or null if cancelled.
    /// </summary>
    Task<string?> PickCertificateAsync();

    /// <summary>
    /// Gets the last opened or saved file path.
    /// </summary>
    string? CurrentFilePath { get; }

    /// <summary>
    /// Clears the current file path (used when opening a template for filling).
    /// </summary>
    void ClearCurrentFilePath();

    /// <summary>
    /// Sets the current file path (used when opening a file from command line).
    /// </summary>
    /// <param name="filePath">The file path to set.</param>
    void SetCurrentFilePath(string filePath);

    /// <summary>Records which form occurrence was selected from a beta.6 stream.</summary>
    void TrackBeta6FormSelection(string filePath, int formIndex) { }

    /// <summary>Appends a beta.6 independent CMS attestation to the open stream.</summary>
    Task<bool> AppendBeta6AttestationAsync(AprDocument document, X509Certificate2 certificate, IReadOnlyList<string>? fields = null) => Task.FromResult(false);

    /// <summary>Resolves independent beta.6 attestations in the open stream.</summary>
    IReadOnlyList<AprAttestationResolution> GetBeta6Attestations() => [];
}

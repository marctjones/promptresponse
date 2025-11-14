using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PromptResponse.Core.Services.Certificates;

/// <summary>
/// Implementation of 1Password integration using the 1Password CLI (`op`).
/// Requires the CLI to be installed from: https://1password.com/downloads/command-line/
/// </summary>
public class OnePasswordService : IOnePasswordService
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ILogger<OnePasswordService> _logger;
    private const string OpCommand = "op";
    private const string CertificateTag = "promptresponse-cert";

    public OnePasswordService(
        ICertificateGenerator certificateGenerator,
        ILogger<OnePasswordService> logger)
    {
        _certificateGenerator = certificateGenerator ?? throw new ArgumentNullException(nameof(certificateGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            _logger.LogDebug("Checking if 1Password CLI is available");

            // Check if op command exists
            var versionCheck = await RunOpCommandAsync("--version");
            if (!versionCheck.success)
            {
                _logger.LogWarning("1Password CLI not found. Install from: https://1password.com/downloads/command-line/");
                return false;
            }

            _logger.LogInformation("1Password CLI version: {Version}", versionCheck.output.Trim());

            // Check if user is signed in
            var whoamiCheck = await RunOpCommandAsync("whoami");
            if (!whoamiCheck.success)
            {
                _logger.LogWarning("Not signed in to 1Password. Run 'op signin' first.");
                return false;
            }

            _logger.LogInformation("Signed in to 1Password as: {User}", whoamiCheck.output.Trim());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking 1Password availability");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> StoreCertificateAsync(
        X509Certificate2 cert,
        string password,
        string title,
        string? vault = null)
    {
        if (cert == null) throw new ArgumentNullException(nameof(cert));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required", nameof(password));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required", nameof(title));

        try
        {
            _logger.LogInformation("Storing certificate in 1Password: {Title}", title);

            // Export certificate as PFX
            var pfxData = _certificateGenerator.ExportPfx(cert, password);

            // Write to temporary file
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
            await File.WriteAllBytesAsync(tempFile, pfxData);

            try
            {
                // Create document in 1Password
                var args = new StringBuilder();
                args.Append($"document create \"{tempFile}\"");
                args.Append($" --title \"{title}\"");
                args.Append($" --tags \"{CertificateTag}\"");

                if (!string.IsNullOrWhiteSpace(vault))
                {
                    args.Append($" --vault \"{vault}\"");
                }

                var result = await RunOpCommandAsync(args.ToString());

                if (result.success)
                {
                    _logger.LogInformation("Certificate stored successfully in 1Password");

                    // Also store the password as a note
                    await StorePasswordNoteAsync(title, password, vault);

                    return true;
                }
                else
                {
                    _logger.LogError("Failed to store certificate in 1Password: {Error}", result.error);
                    return false;
                }
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing certificate in 1Password");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<X509Certificate2?> RetrieveCertificateAsync(
        string title,
        string password,
        string? vault = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required", nameof(title));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required", nameof(password));

        try
        {
            _logger.LogInformation("Retrieving certificate from 1Password: {Title}", title);

            // Get document from 1Password
            var args = new StringBuilder();
            args.Append($"document get \"{title}\"");

            if (!string.IsNullOrWhiteSpace(vault))
            {
                args.Append($" --vault \"{vault}\"");
            }

            var result = await RunOpCommandAsync(args.ToString(), returnBinaryOutput: true);

            if (!result.success || result.binaryOutput == null)
            {
                _logger.LogError("Failed to retrieve certificate from 1Password: {Error}", result.error);
                return null;
            }

            // Import certificate from PFX data
            var cert = new X509Certificate2(
                result.binaryOutput,
                password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            _logger.LogInformation("Certificate retrieved successfully");
            return cert;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificate from 1Password");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> ListCertificatesAsync(string? vault = null)
    {
        try
        {
            _logger.LogDebug("Listing certificates from 1Password");

            // List documents with certificate tag
            var args = new StringBuilder();
            args.Append($"item list --tags \"{CertificateTag}\" --format json");

            if (!string.IsNullOrWhiteSpace(vault))
            {
                args.Append($" --vault \"{vault}\"");
            }

            var result = await RunOpCommandAsync(args.ToString());

            if (!result.success)
            {
                _logger.LogError("Failed to list certificates: {Error}", result.error);
                return Enumerable.Empty<string>();
            }

            // Parse JSON output (simplified - in production use System.Text.Json)
            var titles = new List<string>();
            var items = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(result.output);

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item.TryGetProperty("title", out var titleProp))
                    {
                        titles.Add(titleProp.GetString() ?? "");
                    }
                }
            }

            _logger.LogInformation("Found {Count} certificates in 1Password", titles.Count);
            return titles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing certificates from 1Password");
            return Enumerable.Empty<string>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteCertificateAsync(string title, string? vault = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required", nameof(title));

        try
        {
            _logger.LogInformation("Deleting certificate from 1Password: {Title}", title);

            var args = new StringBuilder();
            args.Append($"item delete \"{title}\"");

            if (!string.IsNullOrWhiteSpace(vault))
            {
                args.Append($" --vault \"{vault}\"");
            }

            var result = await RunOpCommandAsync(args.ToString());

            if (result.success)
            {
                _logger.LogInformation("Certificate deleted successfully");

                // Also delete the password note
                await DeletePasswordNoteAsync(title, vault);

                return true;
            }
            else
            {
                _logger.LogError("Failed to delete certificate: {Error}", result.error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting certificate from 1Password");
            return false;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Run a 1Password CLI command
    /// </summary>
    private async Task<(bool success, string output, string error, byte[]? binaryOutput)> RunOpCommandAsync(
        string arguments,
        bool returnBinaryOutput = false)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OpCommand,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug("Running: {Command} {Args}", OpCommand, arguments);

            process.Start();

            byte[]? binaryData = null;
            string output;

            if (returnBinaryOutput)
            {
                using var memoryStream = new MemoryStream();
                await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
                binaryData = memoryStream.ToArray();
                output = Convert.ToBase64String(binaryData); // For logging
            }
            else
            {
                output = await process.StandardOutput.ReadToEndAsync();
            }

            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;

            if (!success)
            {
                _logger.LogDebug("Command failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            }

            return (success, output, error, binaryData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running op command: {Args}", arguments);
            return (false, "", ex.Message, null);
        }
    }

    /// <summary>
    /// Store the certificate password as a secure note in 1Password
    /// </summary>
    private async Task<bool> StorePasswordNoteAsync(string certTitle, string password, string? vault)
    {
        try
        {
            var noteTitle = $"{certTitle} (Password)";

            var args = new StringBuilder();
            args.Append($"item create --category \"Secure Note\"");
            args.Append($" --title \"{noteTitle}\"");
            args.Append($" --tags \"{CertificateTag}\"");
            args.Append($" notesPlain=\"Certificate password: {password}\"");

            if (!string.IsNullOrWhiteSpace(vault))
            {
                args.Append($" --vault \"{vault}\"");
            }

            var result = await RunOpCommandAsync(args.ToString());
            return result.success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store password note (non-critical)");
            return false;
        }
    }

    /// <summary>
    /// Delete the associated password note
    /// </summary>
    private async Task<bool> DeletePasswordNoteAsync(string certTitle, string? vault)
    {
        try
        {
            var noteTitle = $"{certTitle} (Password)";

            var args = new StringBuilder();
            args.Append($"item delete \"{noteTitle}\"");

            if (!string.IsNullOrWhiteSpace(vault))
            {
                args.Append($" --vault \"{vault}\"");
            }

            var result = await RunOpCommandAsync(args.ToString());
            return result.success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete password note (non-critical)");
            return false;
        }
    }

    #endregion
}

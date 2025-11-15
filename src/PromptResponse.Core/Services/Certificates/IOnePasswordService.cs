using System.Security.Cryptography.X509Certificates;

namespace PromptResponse.Core.Services.Certificates;

/// <summary>
/// Service for storing certificates securely in 1Password.
/// Requires 1Password CLI (`op`) to be installed and configured.
/// </summary>
public interface IOnePasswordService
{
    /// <summary>
    /// Check if 1Password CLI is available and authenticated.
    /// </summary>
    /// <returns>True if CLI is available and user is signed in</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Store a certificate in 1Password as a secure document.
    /// </summary>
    /// <param name="cert">Certificate to store</param>
    /// <param name="password">Password for PFX encryption</param>
    /// <param name="title">Title for the 1Password item</param>
    /// <param name="vault">Vault name (optional, uses default vault if null)</param>
    /// <returns>True if stored successfully</returns>
    Task<bool> StoreCertificateAsync(
        X509Certificate2 cert,
        string password,
        string title,
        string? vault = null);

    /// <summary>
    /// Retrieve a certificate from 1Password.
    /// </summary>
    /// <param name="title">Title of the 1Password item</param>
    /// <param name="password">Password to decrypt the PFX</param>
    /// <param name="vault">Vault name (optional)</param>
    /// <returns>Certificate if found, null otherwise</returns>
    Task<X509Certificate2?> RetrieveCertificateAsync(
        string title,
        string password,
        string? vault = null);

    /// <summary>
    /// List certificates stored in 1Password.
    /// </summary>
    /// <param name="vault">Vault name (optional)</param>
    /// <returns>List of certificate titles</returns>
    Task<IEnumerable<string>> ListCertificatesAsync(string? vault = null);

    /// <summary>
    /// Delete a certificate from 1Password.
    /// </summary>
    /// <param name="title">Title of the certificate to delete</param>
    /// <param name="vault">Vault name (optional)</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteCertificateAsync(string title, string? vault = null);
}

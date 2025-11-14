using System.Security.Cryptography.X509Certificates;

namespace PromptResponse.Core.Services.Certificates;

/// <summary>
/// Service for managing certificates in OS-specific certificate stores.
/// Supports Windows Certificate Store, macOS Keychain, and Linux NSS database.
/// </summary>
public interface ICertificateStore
{
    /// <summary>
    /// Install a certificate to the OS certificate store.
    /// </summary>
    /// <param name="cert">Certificate to install (should include private key for signing certificates)</param>
    /// <param name="location">Store location (CurrentUser or LocalMachine)</param>
    /// <param name="storeName">Store name (My for personal certificates, Root for CA certificates)</param>
    /// <returns>True if installation succeeded, false otherwise</returns>
    Task<bool> InstallCertificateAsync(
        X509Certificate2 cert,
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My);

    /// <summary>
    /// Get all certificates from the specified store.
    /// </summary>
    /// <param name="location">Store location</param>
    /// <param name="storeName">Store name</param>
    /// <returns>Collection of certificates</returns>
    IEnumerable<X509Certificate2> GetCertificates(
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My);

    /// <summary>
    /// Find certificates matching specified criteria.
    /// </summary>
    /// <param name="findType">Type of search (by subject name, thumbprint, etc.)</param>
    /// <param name="findValue">Value to search for</param>
    /// <param name="validOnly">Only return valid certificates</param>
    /// <returns>Matching certificates</returns>
    IEnumerable<X509Certificate2> FindCertificates(
        X509FindType findType,
        object findValue,
        bool validOnly = true);

    /// <summary>
    /// Remove a certificate from the OS store.
    /// </summary>
    /// <param name="thumbprint">Thumbprint of certificate to remove</param>
    /// <param name="location">Store location</param>
    /// <param name="storeName">Store name</param>
    /// <returns>True if removal succeeded, false if certificate not found</returns>
    Task<bool> RemoveCertificateAsync(
        string thumbprint,
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My);

    /// <summary>
    /// Check if a certificate is already installed in the store.
    /// </summary>
    /// <param name="thumbprint">Certificate thumbprint</param>
    /// <param name="location">Store location</param>
    /// <param name="storeName">Store name</param>
    /// <returns>True if certificate exists in store</returns>
    bool IsCertificateInstalled(
        string thumbprint,
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My);
}

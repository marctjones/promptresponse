using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace PromptResponse.Core.Services.Certificates;

/// <summary>
/// Cross-platform implementation of certificate store management.
/// Uses .NET's X509Store for Windows, which also works on Linux and macOS with some limitations.
/// </summary>
public class CertificateStoreService : ICertificateStore
{
    private readonly ILogger<CertificateStoreService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateStoreService"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic messages</param>
    public CertificateStoreService(ILogger<CertificateStoreService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> InstallCertificateAsync(
        X509Certificate2 cert,
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My)
    {
        if (cert == null)
        {
            throw new ArgumentNullException(nameof(cert));
        }

        try
        {
            _logger.LogInformation(
                "Installing certificate {Subject} to {StoreName}/{Location}",
                cert.Subject,
                storeName,
                location);

            await Task.Run(() =>
            {
                using var store = new X509Store(storeName, location);
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                store.Close();
            });

            _logger.LogInformation(
                "Successfully installed certificate {Thumbprint}",
                cert.Thumbprint);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to install certificate {Subject}: {Error}",
                cert.Subject,
                ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public IEnumerable<X509Certificate2> GetCertificates(
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My)
    {
        try
        {
            using var store = new X509Store(storeName, location);
            store.Open(OpenFlags.ReadOnly);

            var certificates = store.Certificates.Cast<X509Certificate2>().ToList();

            _logger.LogDebug(
                "Found {Count} certificates in {StoreName}/{Location}",
                certificates.Count,
                storeName,
                location);

            return certificates;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get certificates from {StoreName}/{Location}: {Error}",
                storeName,
                location,
                ex.Message);
            return Enumerable.Empty<X509Certificate2>();
        }
    }

    /// <inheritdoc />
    public IEnumerable<X509Certificate2> FindCertificates(
        X509FindType findType,
        object findValue,
        bool validOnly = true)
    {
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            var found = store.Certificates.Find(findType, findValue, validOnly);

            _logger.LogDebug(
                "Found {Count} certificates matching {FindType}={FindValue}",
                found.Count,
                findType,
                findValue);

            return found.Cast<X509Certificate2>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to find certificates: {Error}",
                ex.Message);
            return Enumerable.Empty<X509Certificate2>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveCertificateAsync(
        string thumbprint,
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            throw new ArgumentException("Thumbprint is required", nameof(thumbprint));
        }

        try
        {
            _logger.LogInformation(
                "Removing certificate {Thumbprint} from {StoreName}/{Location}",
                thumbprint,
                storeName,
                location);

            var removed = await Task.Run(() =>
            {
                using var store = new X509Store(storeName, location);
                store.Open(OpenFlags.ReadWrite);

                var certs = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    thumbprint,
                    validOnly: false);

                if (certs.Count == 0)
                {
                    _logger.LogWarning(
                        "Certificate {Thumbprint} not found in store",
                        thumbprint);
                    return false;
                }

                foreach (var cert in certs.Cast<X509Certificate2>())
                {
                    store.Remove(cert);
                }

                store.Close();
                return true;
            });

            if (removed)
            {
                _logger.LogInformation(
                    "Successfully removed certificate {Thumbprint}",
                    thumbprint);
            }

            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to remove certificate {Thumbprint}: {Error}",
                thumbprint,
                ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsCertificateInstalled(
        string thumbprint,
        StoreLocation location = StoreLocation.CurrentUser,
        StoreName storeName = StoreName.My)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            throw new ArgumentException("Thumbprint is required", nameof(thumbprint));
        }

        try
        {
            using var store = new X509Store(storeName, location);
            store.Open(OpenFlags.ReadOnly);

            var certs = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: false);

            return certs.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check if certificate {Thumbprint} is installed: {Error}",
                thumbprint,
                ex.Message);
            return false;
        }
    }
}

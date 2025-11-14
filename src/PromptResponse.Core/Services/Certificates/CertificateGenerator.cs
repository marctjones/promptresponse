using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Services.Certificates;

/// <summary>
/// Implementation of certificate generation using .NET's built-in X.509 APIs.
/// Generates certificates compatible with commercial CAs, Microsoft Certificate Services,
/// Adobe Sign, and DocuSign by following standard X.509 formats and OID conventions.
/// </summary>
public class CertificateGenerator : ICertificateGenerator
{
    // Standard OIDs for Extended Key Usage (EKU)
    private const string OidEmailProtection = "1.3.6.1.5.5.7.3.4";        // S/MIME
    private const string OidDocumentSigning = "1.3.6.1.4.1.311.10.3.12";  // Adobe/MS document signing
    private const string OidCodeSigning = "1.3.6.1.5.5.7.3.3";            // Code signing

    /// <inheritdoc />
    public X509Certificate2 GenerateSelfSignedCertificate(CertificateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CommonName))
        {
            throw new ArgumentException("CommonName is required", nameof(request));
        }

        // Generate RSA key pair
        using var rsa = RSA.Create(request.KeySize);

        // Build the subject distinguished name
        var subjectName = BuildSubjectName(request);

        // Create certificate request
        var certRequest = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add Subject Alternative Name (SAN) with email if provided
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddEmailAddress(request.Email);
            certRequest.CertificateExtensions.Add(sanBuilder.Build());
        }

        // Add Key Usage extension
        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // Add Enhanced Key Usage (EKU) based on intended usage
        var ekuOids = GetExtendedKeyUsageOids(request.Usage);
        certRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(ekuOids, critical: false));

        // Add Basic Constraints (not a CA)
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // Generate self-signed certificate
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5); // 5 min clock skew tolerance
        var notAfter = DateTimeOffset.UtcNow.AddDays(request.ValidityDays);

        var certificate = certRequest.CreateSelfSigned(notBefore, notAfter);

        // Return with exportable private key
        return new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    /// <inheritdoc />
    public X509Certificate2 GenerateTestCA(string caName)
    {
        if (string.IsNullOrWhiteSpace(caName))
        {
            throw new ArgumentException("CA name is required", nameof(caName));
        }

        using var rsa = RSA.Create(4096); // CAs should use 4096-bit keys

        var subjectName = new X500DistinguishedName($"CN={caName}, O=Test CA, OU=Testing");

        var certRequest = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add Basic Constraints (IS a CA)
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 0,
                critical: true));

        // Add Key Usage for CA
        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                critical: true));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddYears(10); // CAs valid longer

        var certificate = certRequest.CreateSelfSigned(notBefore, notAfter);

        return new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    /// <inheritdoc />
    public X509Certificate2 GenerateTestCertificate(CertificateRequest request, X509Certificate2 caCert)
    {
        if (string.IsNullOrWhiteSpace(request.CommonName))
        {
            throw new ArgumentException("CommonName is required", nameof(request));
        }

        if (caCert == null || !caCert.HasPrivateKey)
        {
            throw new ArgumentException("CA certificate must have a private key", nameof(caCert));
        }

        using var rsa = RSA.Create(request.KeySize);

        var subjectName = BuildSubjectName(request);

        var certRequest = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add SAN with email
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddEmailAddress(request.Email);
            certRequest.CertificateExtensions.Add(sanBuilder.Build());
        }

        // Add Key Usage
        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // Add Enhanced Key Usage
        var ekuOids = GetExtendedKeyUsageOids(request.Usage);
        certRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(ekuOids, critical: false));

        // Add Basic Constraints (not a CA)
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = DateTimeOffset.UtcNow.AddDays(request.ValidityDays);

        // Generate serial number
        var serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);

        // Sign with CA certificate
        using var caRsa = caCert.GetRSAPrivateKey();
        if (caRsa == null)
        {
            throw new InvalidOperationException("CA certificate does not have an RSA private key");
        }

        var certificate = certRequest.Create(
            caCert.SubjectName,
            X509SignatureGenerator.CreateForRSA(caRsa, RSASignaturePadding.Pkcs1),
            notBefore,
            notAfter,
            serialNumber);

        // Attach the private key
        var certificateWithKey = certificate.CopyWithPrivateKey(rsa);

        return new X509Certificate2(
            certificateWithKey.Export(X509ContentType.Pfx),
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }

    /// <inheritdoc />
    public byte[] ExportPfx(X509Certificate2 cert, string password)
    {
        if (cert == null)
        {
            throw new ArgumentNullException(nameof(cert));
        }

        if (!cert.HasPrivateKey)
        {
            throw new ArgumentException("Certificate must have a private key to export as PFX", nameof(cert));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required for PFX export", nameof(password));
        }

        return cert.Export(X509ContentType.Pfx, password);
    }

    /// <summary>
    /// Build X.500 distinguished name from certificate request
    /// </summary>
    private static X500DistinguishedName BuildSubjectName(CertificateRequest request)
    {
        var parts = new List<string>
        {
            $"CN={EscapeDnValue(request.CommonName)}"
        };

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            parts.Add($"E={EscapeDnValue(request.Email)}");
        }

        if (!string.IsNullOrWhiteSpace(request.OrganizationalUnit))
        {
            parts.Add($"OU={EscapeDnValue(request.OrganizationalUnit)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Organization))
        {
            parts.Add($"O={EscapeDnValue(request.Organization)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            parts.Add($"C={EscapeDnValue(request.Country)}");
        }

        return new X500DistinguishedName(string.Join(", ", parts));
    }

    /// <summary>
    /// Escape special characters in DN values
    /// </summary>
    private static string EscapeDnValue(string value)
    {
        // Escape special characters in X.500 distinguished names
        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("+", "\\+")
            .Replace("\"", "\\\"")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace(";", "\\;");
    }

    /// <summary>
    /// Get Enhanced Key Usage OIDs based on certificate usage flags
    /// </summary>
    private static OidCollection GetExtendedKeyUsageOids(CertificateUsage usage)
    {
        var oids = new OidCollection();

        if (usage.HasFlag(CertificateUsage.EmailSigning))
        {
            oids.Add(new Oid(OidEmailProtection, "Email Protection"));
        }

        if (usage.HasFlag(CertificateUsage.DocumentSigning))
        {
            oids.Add(new Oid(OidDocumentSigning, "Document Signing"));
        }

        if (usage.HasFlag(CertificateUsage.CodeSigning))
        {
            oids.Add(new Oid(OidCodeSigning, "Code Signing"));
        }

        if (oids.Count == 0)
        {
            // Default to email signing if no usage specified
            oids.Add(new Oid(OidEmailProtection, "Email Protection"));
        }

        return oids;
    }
}

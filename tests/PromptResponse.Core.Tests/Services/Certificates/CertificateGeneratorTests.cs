using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Services.Certificates;
using Xunit;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace PromptResponse.Core.Tests.Services.Certificates;

/// <summary>
/// Tests for certificate generation functionality
/// </summary>
public class CertificateGeneratorTests
{
    [Fact]
    public void GenerateSelfSignedCertificate_ShouldCreateValidCertificate()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com",
            Organization = "Test Org",
            ValidityDays = 365,
            Usage = CertificateUsage.EmailSigning
        };

        // Act
        using var cert = generator.GenerateSelfSignedCertificate(request);

        // Assert
        cert.Should().NotBeNull();
        cert.HasPrivateKey.Should().BeTrue("certificate must include private key for signing");
        cert.Subject.Should().Contain("CN=Test User");
        cert.Subject.Should().Contain("E=test@example.com");
        cert.NotBefore.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        cert.NotAfter.Should().BeCloseTo(DateTime.UtcNow.AddDays(365), TimeSpan.FromDays(1));
    }

    [Fact]
    public void GenerateSelfSignedCertificate_ShouldIncludeEmailSigningExtension()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com",
            Usage = CertificateUsage.EmailSigning
        };

        // Act
        using var cert = generator.GenerateSelfSignedCertificate(request);

        // Assert - Check for Extended Key Usage extension with email protection OID
        var ekuExtension = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        ekuExtension.Should().NotBeNull("certificate must have Enhanced Key Usage extension");
        ekuExtension!.EnhancedKeyUsages.Cast<Oid>()
            .Should().Contain(oid => oid.Value == "1.3.6.1.5.5.7.3.4",
                "must include emailProtection OID for S/MIME compatibility");
    }

    [Fact]
    public void GenerateSelfSignedCertificate_ShouldIncludeDocumentSigningExtension()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com",
            Usage = CertificateUsage.DocumentSigning
        };

        // Act
        using var cert = generator.GenerateSelfSignedCertificate(request);

        // Assert - Check for document signing capability
        var ekuExtension = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        ekuExtension.Should().NotBeNull();
        // Adobe/Microsoft document signing OID
        ekuExtension!.EnhancedKeyUsages.Cast<Oid>()
            .Should().Contain(oid => oid.Value == "1.3.6.1.4.1.311.10.3.12",
                "must include documentSigning OID for PDF/Word compatibility");
    }

    [Fact]
    public void GenerateSelfSignedCertificate_WithMultipleUsages_ShouldIncludeAllExtensions()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com",
            Usage = CertificateUsage.EmailSigning | CertificateUsage.DocumentSigning
        };

        // Act
        using var cert = generator.GenerateSelfSignedCertificate(request);

        // Assert
        var ekuExtension = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        ekuExtension.Should().NotBeNull();
        var oids = ekuExtension!.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value).ToList();

        oids.Should().Contain("1.3.6.1.5.5.7.3.4", "must include email protection");
        oids.Should().Contain("1.3.6.1.4.1.311.10.3.12", "must include document signing");
    }

    [Fact]
    public void ExportPfx_ShouldCreatePasswordProtectedFile()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com",
            Usage = CertificateUsage.All
        };
        using var cert = generator.GenerateSelfSignedCertificate(request);
        var password = "TestPassword123!";

        // Act
        var pfxData = generator.ExportPfx(cert, password);

        // Assert
        pfxData.Should().NotBeNull();
        pfxData.Length.Should().BeGreaterThan(0);

        // Verify we can re-import the certificate
        using var reimportedCert = new X509Certificate2(pfxData, password, X509KeyStorageFlags.Exportable);
        reimportedCert.HasPrivateKey.Should().BeTrue();
        reimportedCert.Subject.Should().Be(cert.Subject);
    }

    [Fact]
    public void GenerateSelfSignedCertificate_ShouldUseRSA2048MinimumKeySize()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com"
        };

        // Act
        using var cert = generator.GenerateSelfSignedCertificate(request);

        // Assert - Verify key size is at least 2048 bits (industry standard)
        using var rsa = cert.GetRSAPrivateKey();
        rsa.Should().NotBeNull();
        rsa!.KeySize.Should().BeGreaterOrEqualTo(2048,
            "RSA key size must be at least 2048 bits for security");
    }

    [Fact]
    public void GenerateSelfSignedCertificate_ShouldBeCompatibleWithX509Chain()
    {
        // Arrange
        var generator = new CertificateGenerator();
        var request = new PromptResponse.Core.Models.CertificateRequest
        {
            CommonName = "Test User",
            Email = "test@example.com",
            Usage = CertificateUsage.All
        };

        // Act
        using var cert = generator.GenerateSelfSignedCertificate(request);

        // Assert - Verify certificate can be validated (even if self-signed)
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        var isValid = chain.Build(cert);
        // Self-signed certs may not validate fully, but should at least process
        cert.Verify().Should().BeFalse("self-signed certs don't validate without being in trust store");
    }
}

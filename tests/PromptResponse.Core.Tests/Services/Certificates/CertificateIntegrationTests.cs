using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Xunit;

namespace PromptResponse.Core.Tests.Services.Certificates;

/// <summary>
/// Integration tests demonstrating automated certificate generation for testing
/// </summary>
public class CertificateIntegrationTests
{
    [Fact]
    public void TestCertificateProvider_ShouldProvideValidEmailCertificate()
    {
        // Act
        var cert = TestCertificateProvider.GetEmailSigningCertificate();

        // Assert
        cert.Should().NotBeNull();
        cert.HasPrivateKey.Should().BeTrue();
        cert.Subject.Should().Contain("Test Email User");

        var ekuExtension = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        ekuExtension.Should().NotBeNull();
        ekuExtension!.EnhancedKeyUsages.Cast<Oid>()
            .Should().Contain(oid => oid.Value == "1.3.6.1.5.5.7.3.4");
    }

    [Fact]
    public void TestCertificateProvider_ShouldProvideValidDocumentCertificate()
    {
        // Act
        var cert = TestCertificateProvider.GetDocumentSigningCertificate();

        // Assert
        cert.Should().NotBeNull();
        cert.HasPrivateKey.Should().BeTrue();

        var ekuExtension = cert.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        ekuExtension.Should().NotBeNull();
        ekuExtension!.EnhancedKeyUsages.Cast<Oid>()
            .Should().Contain(oid => oid.Value == "1.3.6.1.4.1.311.10.3.12");
    }

    [Fact]
    public void TestCertificateProvider_ShouldCacheCertificates()
    {
        // Act - Get same certificate twice
        var cert1 = TestCertificateProvider.GetEmailSigningCertificate();
        var cert2 = TestCertificateProvider.GetEmailSigningCertificate();

        // Assert - Should be same instance (cached)
        cert1.Should().BeSameAs(cert2, "certificates should be cached for performance");
    }

    [Fact]
    public void TestCertificateProvider_ShouldGenerateFreshCertificates()
    {
        // Act
        var cert1 = TestCertificateProvider.GenerateFreshCertificate("User 1", "user1@test.com");
        var cert2 = TestCertificateProvider.GenerateFreshCertificate("User 2", "user2@test.com");

        // Assert - Should be different instances
        cert1.Should().NotBeSameAs(cert2);
        cert1.Subject.Should().Contain("User 1");
        cert2.Subject.Should().Contain("User 2");
    }

    [Fact]
    public void TestCertificateProvider_ShouldProvideTestCA()
    {
        // Act
        var ca = TestCertificateProvider.GetTestCA();

        // Assert
        ca.Should().NotBeNull();
        ca.HasPrivateKey.Should().BeTrue();

        var basicConstraints = ca.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .FirstOrDefault();

        basicConstraints.Should().NotBeNull();
        basicConstraints!.CertificateAuthority.Should().BeTrue("must be a CA certificate");
    }

    [Fact]
    public void TestCertificateProvider_ShouldProvideCASignedCertificate()
    {
        // Act
        var cert = TestCertificateProvider.GetCASignedCertificate();
        var ca = TestCertificateProvider.GetTestCA();

        // Assert
        cert.Should().NotBeNull();
        cert.HasPrivateKey.Should().BeTrue();
        cert.Issuer.Should().Be(ca.Subject, "certificate should be signed by the test CA");
    }

    [Fact]
    public void CertificateChain_ShouldValidateWithTestCA()
    {
        // Arrange
        var cert = TestCertificateProvider.GetCASignedCertificate();
        var ca = TestCertificateProvider.GetTestCA();

        // Act - Build certificate chain
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ExtraStore.Add(ca);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        var buildResult = chain.Build(cert);

        // Assert
        chain.ChainElements.Count.Should().BeGreaterOrEqualTo(2,
            "chain should include both certificate and CA");

        // The last element should be the CA
        var rootCert = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        rootCert.Subject.Should().Be(ca.Subject);
    }

    [Fact]
    public void ExportedCertificate_ShouldBeReimportable()
    {
        // Arrange
        var originalCert = TestCertificateProvider.GetAllPurposeCertificate();
        var password = "TestPassword123!";

        // Act - Export and re-import
        var pfxData = TestCertificateProvider.ExportCertificateToPfx(originalCert, password);
        using var reimportedCert = new X509Certificate2(
            pfxData,
            password,
            X509KeyStorageFlags.Exportable);

        // Assert
        reimportedCert.HasPrivateKey.Should().BeTrue();
        reimportedCert.Subject.Should().Be(originalCert.Subject);
        reimportedCert.Thumbprint.Should().Be(originalCert.Thumbprint);
    }

    [Fact]
    public void ExportedCertificate_ShouldBeCompatibleWithAllPlatforms()
    {
        // Arrange
        var cert = TestCertificateProvider.GetAllPurposeCertificate();
        var password = "Test123!";

        // Act - Export as PFX (PKCS#12)
        var pfxData = TestCertificateProvider.ExportCertificateToPfx(cert, password);

        // Assert - Verify it's valid PKCS#12 format
        pfxData.Should().NotBeNull();
        pfxData.Length.Should().BeGreaterThan(0);

        // PKCS#12 files start with 0x30 (ASN.1 SEQUENCE)
        pfxData[0].Should().Be(0x30, "PFX files should start with ASN.1 SEQUENCE tag");

        // Should be importable (this validates PKCS#12 format)
        Action importAction = () =>
        {
            using var imported = new X509Certificate2(pfxData, password);
            imported.HasPrivateKey.Should().BeTrue();
        };

        importAction.Should().NotThrow("exported PFX should be valid PKCS#12 format");
    }

    [Fact]
    public void GeneratedCertificate_ShouldHaveValidDateRange()
    {
        // Arrange & Act
        var cert = TestCertificateProvider.GenerateFreshCertificate(
            "Test User",
            "test@example.com",
            validityDays: 365);

        // Assert
        var now = DateTime.UtcNow;
        cert.NotBefore.Should().BeBefore(now.AddMinutes(1),
            "certificate should be valid now");
        cert.NotAfter.Should().BeAfter(now.AddDays(364),
            "certificate should be valid for at least 364 days");
        cert.NotAfter.Should().BeBefore(now.AddDays(366),
            "certificate should not be valid for more than 366 days");
    }
}

using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Services;
using PromptResponse.Core.Services.Certificates;
using Xunit;

namespace PromptResponse.Core.Tests.Services;

/// <summary>
/// Unit tests for SignatureService.
/// </summary>
public class SignatureServiceTests
{
    private readonly SignatureService _signatureService;
    private readonly CertificateGenerator _certificateGenerator;

    public SignatureServiceTests()
    {
        _signatureService = new SignatureService();
        _certificateGenerator = new CertificateGenerator();
    }

    [Fact]
    public void SignTemplate_ValidTemplate_CreatesSignature()
    {
        // Arrange
        var template = CreateSampleTemplate();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Test Publisher",
            Email = "publisher@test.com",
            Organization = "Test Org",
            Usage = CertificateUsage.DocumentSigning
        });

        // Act
        var signature = _signatureService.SignTemplate(template, certificate, "Official template");

        // Assert
        signature.Should().NotBeNull();
        signature.SignerName.Should().Be("Test Publisher");
        signature.SignerEmail.Should().Be("publisher@test.com");
        signature.SignerOrganization.Should().Be("Test Org");
        signature.SignatureType.Should().Be("template");
        signature.SignatureData.Should().NotBeNullOrEmpty();
        signature.SignatureReason.Should().Be("Official template");
        signature.HashAlgorithm.Should().Be("SHA256");
        signature.CertificateThumbprint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SignFilledForm_ValidFilledForm_CreatesSignature()
    {
        // Arrange
        var filledForm = CreateSampleFilledForm();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "John Doe",
            Email = "john@example.com",
            Usage = CertificateUsage.DocumentSigning
        });

        // Act
        var signature = _signatureService.SignFilledForm(filledForm, certificate, "Attestation of accuracy");

        // Assert
        signature.Should().NotBeNull();
        signature.SignerName.Should().Be("John Doe");
        signature.SignerEmail.Should().Be("john@example.com");
        signature.SignatureType.Should().Be("filledForm");
        signature.SignatureData.Should().NotBeNullOrEmpty();
        signature.SignatureReason.Should().Be("Attestation of accuracy");
    }

    [Fact]
    public void SignTemplate_FilledFormProvided_ThrowsArgumentException()
    {
        // Arrange
        var filledForm = CreateSampleFilledForm();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Test",
            Usage = CertificateUsage.DocumentSigning
        });

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _signatureService.SignTemplate(filledForm, certificate));
    }

    [Fact]
    public void SignFilledForm_TemplateProvided_ThrowsArgumentException()
    {
        // Arrange
        var template = CreateSampleTemplate();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Test",
            Usage = CertificateUsage.DocumentSigning
        });

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _signatureService.SignFilledForm(template, certificate));
    }

    [Fact]
    public void IsSigned_UnsignedDocument_ReturnsFalse()
    {
        // Arrange
        var document = CreateSampleTemplate();

        // Act
        var result = _signatureService.IsSigned(document);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSigned_SignedTemplate_ReturnsTrue()
    {
        // Arrange
        var document = CreateSampleTemplate();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Test",
            Usage = CertificateUsage.DocumentSigning
        });
        var signature = _signatureService.SignTemplate(document, certificate);
        document.Metadata.TemplateSignatures = new List<DigitalSignature> { signature };

        // Act
        var result = _signatureService.IsSigned(document);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSigned_SignedFilledForm_ReturnsTrue()
    {
        // Arrange
        var document = CreateSampleFilledForm();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Test",
            Usage = CertificateUsage.DocumentSigning
        });
        var signature = _signatureService.SignFilledForm(document, certificate);
        document.Metadata.FormSignatures = new List<DigitalSignature> { signature };

        // Act
        var result = _signatureService.IsSigned(document);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ValidSignature_ReturnsSuccess()
    {
        // Arrange
        var document = CreateSampleTemplate();
        var certificate = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Test",
            Usage = CertificateUsage.DocumentSigning
        });
        var signature = _signatureService.SignTemplate(document, certificate);

        // Act
        var result = _signatureService.VerifySignature(document, signature);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SignTemplate_MultipleSignatures_AllIncluded()
    {
        // Arrange
        var document = CreateSampleTemplate();

        var cert1 = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Signer 1",
            Usage = CertificateUsage.DocumentSigning
        });

        var cert2 = _certificateGenerator.GenerateSelfSignedCertificate(new CertificateRequest
        {
            CommonName = "Signer 2",
            Usage = CertificateUsage.DocumentSigning
        });

        // Act
        var signature1 = _signatureService.SignTemplate(document, cert1);
        var signature2 = _signatureService.SignTemplate(document, cert2);

        document.Metadata.TemplateSignatures = new List<DigitalSignature> { signature1, signature2 };

        // Assert
        document.Metadata.TemplateSignatures.Should().HaveCount(2);
        document.Metadata.TemplateSignatures[0].SignerName.Should().Be("Signer 1");
        document.Metadata.TemplateSignatures[1].SignerName.Should().Be("Signer 2");
    }

    private AprDocument CreateSampleTemplate()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Template",
                Description = "A test template",
                TemplateId = "test-template-001",
                TemplateVersion = "1.0",
                Author = "Test Author",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "prompt_1",
                            Label = "Question 1",
                            Response = ""
                        }
                    }
                }
            }
        };
    }

    private AprDocument CreateSampleFilledForm()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Filled Form",
                TemplateId = "test-template-001",
                TemplateVersion = "1.0",
                FilledBy = "John Doe",
                FilledDate = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "prompt_1",
                            Label = "Question 1",
                            Response = "Answer 1"
                        }
                    }
                }
            }
        };
    }
}

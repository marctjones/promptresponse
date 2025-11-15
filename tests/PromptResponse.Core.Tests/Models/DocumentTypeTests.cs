using FluentAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the DocumentType enum.
/// </summary>
public class DocumentTypeTests
{
    [Fact]
    public void DocumentType_ShouldHaveTemplateValue()
    {
        // Arrange & Act
        var docType = DocumentType.Template;

        // Assert
        docType.Should().Be(DocumentType.Template);
        ((int)docType).Should().Be(0);
    }

    [Fact]
    public void DocumentType_ShouldHaveFilledFormValue()
    {
        // Arrange & Act
        var docType = DocumentType.FilledForm;

        // Assert
        docType.Should().Be(DocumentType.FilledForm);
        ((int)docType).Should().Be(1);
    }

    [Fact]
    public void DocumentType_ShouldBeDistinct()
    {
        // Arrange & Act & Assert
        DocumentType.Template.Should().NotBe(DocumentType.FilledForm);
    }

    [Fact]
    public void DocumentType_ToString_ShouldReturnName()
    {
        // Arrange & Act & Assert
        DocumentType.Template.ToString().Should().Be("Template");
        DocumentType.FilledForm.ToString().Should().Be("FilledForm");
    }
}

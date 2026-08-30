using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Valid nested document-shape scenarios.</summary>
public class DocumentValidatorComplexDocumentTests : DocumentValidatorTestBase
{
    [Fact]
    public void Validate_ComplexValidDocument_ShouldReturnValid()
    {
        var document = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Complex Form", Description = "A test form" },
            Sections =
            [
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Sections =
                    [
                        new()
                        {
                            Id = "child_001_001",
                            Title = "Child Section 1",
                            Prompts = [new Prompt { Id = "prompt_001", Label = "Q1" }]
                        }
                    ],
                    Prompts = [new Prompt { Id = "prompt_002", Label = "Q2" }]
                },
                new()
                {
                    Id = "section_002",
                    Title = "Section 2",
                    Prompts = [new Prompt { Id = "prompt_003", Label = "Q3" }]
                }
            ]
        };

        var result = Validator.Validate(document);

        result.IsValid.Should().BeTrue();
    }
}

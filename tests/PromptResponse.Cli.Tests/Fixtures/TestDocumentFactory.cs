using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Tests.Fixtures;

/// <summary>
/// Factory for creating test APR documents with consistent structure.
/// </summary>
public static class TestDocumentFactory
{
    /// <summary>
    /// Creates a minimal valid template.
    /// </summary>
    public static AprDocument CreateMinimalTemplate()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Template",
                Author = "Test Author",
                Created = DateTime.UtcNow,
                TemplateId = "test-template-001",
                TemplateVersion = "1.0"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_001",
                            Label = "Question 1",
                            Response = "",
                            Hints = new PromptHints
                            {
                                ExpectedDataType = "text",
                                Placeholder = "Enter text here"
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a template with multiple sections and nested sections.
    /// </summary>
    public static AprDocument CreateComplexTemplate()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Complex Template",
                Description = "A template with nested sections",
                Author = "Test Author",
                Created = DateTime.UtcNow,
                TemplateId = "complex-template-001",
                TemplateVersion = "1.0"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Description = "First main section",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_001",
                            Label = "Name",
                            Response = "",
                            Hints = new PromptHints { ExpectedDataType = "text" }
                        }
                    },
                    Sections = new List<Section>
                    {
                        new()
                        {
                            Id = "subsection_001",
                            Title = "Subsection 1.1",
                            Prompts = new List<Prompt>
                            {
                                new()
                                {
                                    Id = "prompt_002",
                                    Label = "Email",
                                    Response = "",
                                    Hints = new PromptHints { ExpectedDataType = "email" }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "section_002",
                    Title = "Section 2",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_003",
                            Label = "Date",
                            Response = "",
                            Hints = new PromptHints { ExpectedDataType = "date" }
                        },
                        new()
                        {
                            Id = "prompt_004",
                            Label = "Notes",
                            Response = "",
                            Hints = new PromptHints
                            {
                                ExpectedDataType = "text",
                                SuggestedValues = new List<string> { "Option A", "Option B", "Option C" }
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Creates a filled form from a template.
    /// </summary>
    public static AprDocument CreateFilledForm()
    {
        var filled = CreateComplexTemplate();
        filled.DocumentType = DocumentType.FilledForm;
        filled.Metadata.FilledBy = "John Doe";
        filled.Metadata.FilledDate = DateTime.UtcNow;
        filled.Metadata.Modified = DateTime.UtcNow;

        // Fill in responses
        filled.Sections[0].Prompts[0].Response = "John Doe";
        filled.Sections[0].Prompts[0].ResponseMetadata.LastModified = DateTime.UtcNow;

        filled.Sections[0].Sections[0].Prompts[0].Response = "john@example.com";
        filled.Sections[0].Sections[0].Prompts[0].ResponseMetadata.LastModified = DateTime.UtcNow;

        filled.Sections[1].Prompts[0].Response = "2025-04-30";
        filled.Sections[1].Prompts[0].ResponseMetadata.LastModified = DateTime.UtcNow;

        filled.Sections[1].Prompts[1].Response = "Option A";
        filled.Sections[1].Prompts[1].ResponseMetadata.LastModified = DateTime.UtcNow;

        return filled;
    }

    /// <summary>
    /// Creates a partially filled form.
    /// </summary>
    public static AprDocument CreatePartiallyFilledForm()
    {
        var filled = CreateComplexTemplate();
        filled.DocumentType = DocumentType.FilledForm;
        filled.Metadata.FilledBy = "Jane Smith";
        filled.Metadata.FilledDate = DateTime.UtcNow;

        // Only fill first prompt
        filled.Sections[0].Prompts[0].Response = "Jane Smith";
        filled.Sections[0].Prompts[0].ResponseMetadata.LastModified = DateTime.UtcNow;

        return filled;
    }
}

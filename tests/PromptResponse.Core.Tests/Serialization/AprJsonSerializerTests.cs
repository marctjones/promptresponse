using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Tests.Serialization;

/// <summary>Shared fixture for APR JSON serialization behavior suites.</summary>
public abstract class AprJsonSerializerTestBase
{
    protected AprJsonSerializer Serializer { get; } = new();

    protected static AprDocument CreateComplexDocument() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Complex Form", Description = "A complex test form", Created = DateTime.UtcNow, Author = "Test Author", TemplateId = "test-001", TemplateVersion = "1.0" },
        Sections =
        [
            new Section
            {
                Id = "section_001", Title = "Section with Child Sections", Description = "Test section",
                Sections = [new Section { Id = "child_001_001", Title = "Child Section 1", Prompts = [new Prompt { Id = "prompt_001", Label = "Question 1", Response = "", Hints = new PromptHints { ExpectedDataType = "text" } }] }],
                Prompts = [new Prompt { Id = "prompt_002", Label = "Section-level question" }],
            },
            new Section { Id = "section_002", Title = "Simple Section", Prompts = [new Prompt { Id = "prompt_003", Label = "Q3" }] },
        ],
    };
}

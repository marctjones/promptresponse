using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

/// <summary>
/// Unit tests for the AprJsonSerializer.
/// </summary>
public class AprJsonSerializerTests
{
    private readonly AprJsonSerializer _serializer;

    public AprJsonSerializerTests()
    {
        _serializer = new AprJsonSerializer();
    }

    [Fact]
    public void Serialize_WithSimpleDocument_ShouldProduceValidJson()
    {
        // Arrange
        var document = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Test Form" },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "Test Section" }
            }
        };

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"version\": \"1.0\"");
        json.Should().Contain("\"documentType\": \"template\"");
        json.Should().Contain("\"title\": \"Test Form\"");
    }

    [Fact]
    public void Serialize_WithComplexDocument_ShouldIncludeAllStructure()
    {
        // Arrange
        var document = CreateComplexDocument();

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().Contain("\"version\": \"1.0\"");
        json.Should().Contain("\"documentType\": \"template\"");
        json.Should().Contain("\"sections\"");
        json.Should().Contain("\"subsections\"");
        json.Should().Contain("\"prompts\"");
    }

    [Fact]
    public void Deserialize_WithValidJson_ShouldCreateDocument()
    {
        // Arrange
        var json = """
        {
            "version": "1.0",
            "documentType": "template",
            "metadata": {
                "title": "Test Form"
            },
            "sections": []
        }
        """;

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be("1.0");
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Test Form");
    }

    [Fact]
    public void Deserialize_WithFilledFormType_ShouldSetCorrectType()
    {
        // Arrange
        var json = """
        {
            "version": "1.0",
            "documentType": "filledForm",
            "metadata": { "title": "Filled" },
            "sections": []
        }
        """;

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.DocumentType.Should().Be(DocumentType.FilledForm);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        var act = () => _serializer.Deserialize(invalidJson);
        act.Should().Throw<SerializationException>();
    }

    [Fact]
    public void RoundTrip_SimpleDocument_ShouldPreserveData()
    {
        // Arrange
        var original = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Form",
                Description = "A test"
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
                            Response = "Answer 1"
                        }
                    }
                }
            }
        };

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize(json);

        // Assert
        deserialized.Version.Should().Be(original.Version);
        deserialized.DocumentType.Should().Be(original.DocumentType);
        deserialized.Metadata.Title.Should().Be(original.Metadata.Title);
        deserialized.Metadata.Description.Should().Be(original.Metadata.Description);
        deserialized.Sections.Should().HaveCount(1);
        deserialized.Sections[0].Id.Should().Be("section_001");
        deserialized.Sections[0].Prompts.Should().HaveCount(1);
        deserialized.Sections[0].Prompts[0].Response.Should().Be("Answer 1");
    }

    [Fact]
    public void RoundTrip_ComplexDocument_ShouldPreserveAllData()
    {
        // Arrange
        var original = CreateComplexDocument();

        // Act
        var json = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize(json);

        // Assert
        deserialized.Sections.Should().HaveCount(original.Sections.Count);
        deserialized.Sections[0].Subsections.Should().HaveCount(
            original.Sections[0].Subsections.Count);
        deserialized.Sections[0].Subsections[0].Prompts.Should().HaveCount(
            original.Sections[0].Subsections[0].Prompts.Count);
    }

    [Fact]
    public void Serialize_WithPromptHints_ShouldIncludeAllHints()
    {
        // Arrange
        var document = new AprDocument
        {
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Test",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_001",
                            Label = "Email",
                            Hints = new PromptHints
                            {
                                Placeholder = "you@example.com",
                                ExpectedDataType = "email",
                                SuggestedValues = new List<string> { "test@test.com" },
                                HelpText = "Enter email",
                                ValidationPattern = @"^.+@.+\..+$"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().Contain("\"placeholder\": \"you@example.com\"");
        json.Should().Contain("\"expectedDataType\": \"email\"");
        json.Should().Contain("\"suggestedValues\"");
        json.Should().Contain("\"helpText\": \"Enter email\"");
        json.Should().Contain("\"validationPattern\"");
    }

    [Fact]
    public void Deserialize_WithNullFields_ShouldHandleGracefully()
    {
        // Arrange
        var json = """
        {
            "version": "1.0",
            "documentType": "template",
            "metadata": {
                "title": "Test",
                "description": null,
                "author": null
            },
            "sections": [
                {
                    "id": "section_001",
                    "title": "Test",
                    "description": null,
                    "prompts": []
                }
            ]
        }
        """;

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Metadata.Description.Should().BeNull();
        document.Metadata.Author.Should().BeNull();
        document.Sections[0].Description.Should().BeNull();
    }

    [Fact]
    public void Serialize_WithDateTime_ShouldUseIso8601Format()
    {
        // Arrange
        var timestamp = new DateTime(2025, 11, 12, 14, 30, 0, DateTimeKind.Utc);
        var document = new AprDocument
        {
            Metadata = new Metadata
            {
                Title = "Test",
                Created = timestamp,
                Modified = timestamp
            }
        };

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().Contain("2025-11-12T14:30:00");
    }

    [Fact]
    public void Deserialize_WithIso8601DateTime_ShouldParseCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": "1.0",
            "documentType": "template",
            "metadata": {
                "title": "Test",
                "created": "2025-11-12T14:30:00Z"
            },
            "sections": []
        }
        """;

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Metadata.Created.Should().NotBeNull();
        document.Metadata.Created!.Value.Year.Should().Be(2025);
        document.Metadata.Created!.Value.Month.Should().Be(11);
        document.Metadata.Created!.Value.Day.Should().Be(12);
    }

    [Fact]
    public void Serialize_WithEmptyCollections_ShouldIncludeEmptyArrays()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>()
        };

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().Contain("\"sections\": []");
    }

    [Fact]
    public void Serialize_ShouldProducePrettyPrintedJson()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>()
        };

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().Contain("\n"); // Should have newlines (pretty-printed)
        json.Should().Contain("  "); // Should have indentation
    }

    [Fact]
    public void Serialize_WithCaseConversion_ShouldUseCamelCase()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" }
        };

        // Act
        var json = _serializer.Serialize(document);

        // Assert
        json.Should().Contain("\"documentType\""); // camelCase
        json.Should().NotContain("\"DocumentType\""); // not PascalCase
    }

    private static AprDocument CreateComplexDocument()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Complex Form",
                Description = "A complex test form",
                Created = DateTime.UtcNow,
                Author = "Test Author",
                TemplateId = "test-001",
                TemplateVersion = "1.0"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section with Subsections",
                    Description = "Test section",
                    Subsections = new List<Subsection>
                    {
                        new()
                        {
                            Id = "subsection_001_001",
                            Title = "Subsection 1",
                            Prompts = new List<Prompt>
                            {
                                new()
                                {
                                    Id = "prompt_001",
                                    Label = "Question 1",
                                    Response = "",
                                    Hints = new PromptHints
                                    {
                                        ExpectedDataType = "text"
                                    }
                                }
                            }
                        }
                    },
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_002",
                            Label = "Section-level question"
                        }
                    }
                },
                new()
                {
                    Id = "section_002",
                    Title = "Simple Section",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_003", Label = "Q3" }
                    }
                }
            }
        };
    }
}

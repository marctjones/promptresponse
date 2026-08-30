using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Shared fixture and document builders for ExportCommand behavioral tests.
/// </summary>
public partial class ExportCommandTests
{
    private readonly AprJsonSerializer _serializer;
    private readonly ExportCommand _command;

    public ExportCommandTests()
    {
        _serializer = new AprJsonSerializer();
        _command = new ExportCommand(_serializer);
    }

    private async Task<string> WriteDocumentAsync(AprDocument document)
    {
        var inputFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(inputFile, _serializer.Serialize(document));
        return inputFile;
    }


    private AprDocument CreateTestDocument()
    {
        return new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Form",
                TemplateId = "test-v1"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt1",
                            Label = "Test Question",
                            Response = "Test Answer",
                            Hints = new PromptHints
                            {
                                ExpectedDataType = "text"
                            }
                        }
                    }
                }
            }
        };
    }

    private AprDocument CreateDocumentWithMultiplePrompts()
    {
        return new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Multi-Prompt Form",
                TemplateId = "multi-v1"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt1",
                            Label = "Question 1",
                            Response = "Answer 1"
                        },
                        new()
                        {
                            Id = "prompt2",
                            Label = "Question 2",
                            Response = "Answer 2"
                        }
                    }
                }
            }
        };
    }
}

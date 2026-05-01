using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PromptResponse.Cli.Api;
using PromptResponse.Cli.Tests.Fixtures;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Cli.Tests.Api;

public class FormFillingApiTests : IDisposable
{
    private readonly FormFillingApi _api;
    private readonly IAprSerializer _serializer;
    private readonly TempFileHelper _tempHelper;

    public FormFillingApiTests()
    {
        _serializer = new AprJsonSerializer();
        var validator = new DocumentValidator();
        var logger = new Mock<ILogger<FormFillingApi>>();
        _api = new FormFillingApi(_serializer, validator, logger.Object);
        _tempHelper = new TempFileHelper(_serializer);
    }

    [Fact]
    public async Task LoadTemplateAsync_ValidTemplate_Succeeds()
    {
        var path = _tempHelper.CreateTemplateFile();
        var doc = await _api.LoadTemplateAsync(path);
        doc.DocumentType.Should().Be(DocumentType.Template);
    }

    [Fact]
    public async Task LoadTemplateAsync_FileNotFound_Throws() =>
        await _api.Invoking(a => a.LoadTemplateAsync("/nonexistent/file.aprt"))
            .Should().ThrowAsync<FileNotFoundException>();

    [Fact]
    public async Task LoadTemplateAsync_FilledForm_Throws()
    {
        var path = _tempHelper.CreateFilledFormFile();
        await _api.Invoking(a => a.LoadTemplateAsync(path))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void FillForm_WithResponses_Succeeds()
    {
        var template = TestDocumentFactory.CreateComplexTemplate();
        var responses = new Dictionary<string, string>
        {
            ["prompt_001"] = "John Doe",
            ["prompt_003"] = "2025-04-30"
        };

        var filled = _api.FillForm(template, responses, "Test User");

        filled.DocumentType.Should().Be(DocumentType.FilledForm);
        filled.Metadata.FilledBy.Should().Be("Test User");
        filled.Sections[0].Prompts[0].Response.Should().Be("John Doe");
    }

    [Fact]
    public void FillForm_NullTemplate_Throws() =>
        _api.Invoking(a => a.FillForm(null!, new Dictionary<string, string>(), null))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void FillForm_FilledFormAsTemplate_Throws()
    {
        var filled = TestDocumentFactory.CreateFilledForm();
        _api.Invoking(a => a.FillForm(filled, new Dictionary<string, string>(), null))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FillFormFromJson_ValidJson_Succeeds()
    {
        var template = TestDocumentFactory.CreateMinimalTemplate();
        var json = "{\"prompt_001\": \"Test Value\"}";

        var filled = _api.FillFormFromJson(template, json, "Test User");

        filled.Sections[0].Prompts[0].Response.Should().Be("Test Value");
    }

    [Fact]
    public void FillFormFromJson_InvalidJson_Throws()
    {
        var template = TestDocumentFactory.CreateMinimalTemplate();
        _api.Invoking(a => a.FillFormFromJson(template, "{ invalid", null))
            .Should().Throw<Exception>();
    }

    [Fact]
    public async Task SaveFilledFormAsync_ValidForm_CreatesFile()
    {
        var filled = TestDocumentFactory.CreateFilledForm();
        var outputPath = _tempHelper.GetPath($"output-{Guid.NewGuid():N}.aprf");

        await _api.SaveFilledFormAsync(filled, outputPath);

        File.Exists(outputPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveFilledFormAsync_TemplateInput_Throws()
    {
        var template = TestDocumentFactory.CreateMinimalTemplate();
        var path = _tempHelper.GetPath("output.aprf");
        await _api.Invoking(a => a.SaveFilledFormAsync(template, path))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void ValidateFilledForm_ValidForm_Succeeds()
    {
        var form = TestDocumentFactory.CreateFilledForm();
        var result = _api.ValidateFilledForm(form);
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetPromptIds_Template_ReturnsIds()
    {
        var template = TestDocumentFactory.CreateComplexTemplate();
        var ids = _api.GetPromptIds(template);
        ids.Should().NotBeEmpty();
        ids.Should().Contain("prompt_001");
    }

    [Fact]
    public void GetCompletionPercentage_FilledForm_ReturnsPercentage()
    {
        var filled = TestDocumentFactory.CreateFilledForm();
        var percentage = _api.GetCompletionPercentage(filled);
        percentage.Should().BeGreaterThan(0);
        percentage.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void GetCompletionPercentage_EmptyTemplate_ReturnsZero()
    {
        var template = TestDocumentFactory.CreateMinimalTemplate();
        var percentage = _api.GetCompletionPercentage(template);
        percentage.Should().Be(0);
    }

    public void Dispose() => _tempHelper?.Dispose();
}

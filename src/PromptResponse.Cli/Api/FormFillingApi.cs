using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Microsoft.Extensions.Logging;
using PromptResponse.Cli.Api.Filling;

namespace PromptResponse.Cli.Api;

/// <summary>
/// Programmatic API for filling out APR forms.
/// </summary>
/// <remarks>
/// This API can be used by other applications to fill forms programmatically
/// without user interaction.
/// </remarks>
public class FormFillingApi
{
    private readonly IAprSerializer _serializer;
    private readonly DocumentValidator _validator;
    private readonly ILogger<FormFillingApi> _logger;
    private readonly FilledFormFactory _filledFormFactory;
    private readonly FormResponseApplicator _responseApplicator = new();
    private readonly FilledFormWriter _filledFormWriter;

    public FormFillingApi(
        IAprSerializer serializer,
        DocumentValidator validator,
        ILogger<FormFillingApi> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _filledFormFactory = new FilledFormFactory(_serializer);
        _filledFormWriter = new FilledFormWriter(_serializer);
    }

    /// <summary>
    /// Loads a template from file.
    /// </summary>
    public async Task<AprDocument> LoadTemplateAsync(string templatePath)
    {
        _logger.LogInformation("Loading template from {Path}", templatePath);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        using var stream = File.OpenRead(templatePath);
        var document = await _serializer.DeserializeAsync(stream);

        if (document.DocumentType != DocumentType.Template)
        {
            throw new InvalidOperationException("Document is not a template");
        }

        return document;
    }

    /// <summary>
    /// Fills a form with responses from a dictionary.
    /// </summary>
    /// <param name="template">The template to fill.</param>
    /// <param name="responses">Dictionary mapping prompt IDs to response values.</param>
    /// <param name="filledBy">Name of person filling form (optional).</param>
    /// <returns>The filled form document.</returns>
    public AprDocument FillForm(
        AprDocument template,
        Dictionary<string, string> responses,
        string? filledBy = null)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (template.DocumentType != DocumentType.Template)
        {
            throw new InvalidOperationException("Document must be a template");
        }

        _logger.LogInformation("Filling form with {Count} responses", responses.Count);

        var filledForm = _filledFormFactory.Create(template, filledBy);
        var result = _responseApplicator.Apply(filledForm, responses);

        _logger.LogInformation(
            "Applied {Applied}/{Total} responses",
            result.AppliedCount,
            responses.Count);

        foreach (var promptId in result.MissingPromptIds)
        {
            _logger.LogWarning("Prompt ID not found: {PromptId}", promptId);
        }

        if (result.MissingPromptIds.Count > 0)
        {
            _logger.LogWarning("Missing prompts: {Missing}", string.Join(", ", result.MissingPromptIds));
        }

        return filledForm;
    }

    /// <summary>
    /// Fills a form using JSON response data.
    /// </summary>
    /// <param name="template">The template to fill.</param>
    /// <param name="jsonResponses">JSON object with responses.</param>
    /// <param name="filledBy">Name of person filling form (optional).</param>
    public AprDocument FillFormFromJson(
        AprDocument template,
        string jsonResponses,
        string? filledBy = null)
    {
        return FillForm(template, FormResponseJsonParser.Parse(jsonResponses), filledBy);
    }

    /// <summary>
    /// Saves a filled form to file.
    /// </summary>
    public async Task SaveFilledFormAsync(AprDocument filledForm, string outputPath)
    {
        if (filledForm.DocumentType != DocumentType.FilledForm)
        {
            throw new InvalidOperationException("Document must be a filled form");
        }

        _logger.LogInformation("Saving filled form to {Path}", outputPath);

        await _filledFormWriter.WriteAsync(filledForm, outputPath);

        _logger.LogInformation("Filled form saved successfully");
    }

    /// <summary>
    /// Validates a filled form.
    /// </summary>
    public ValidationResult ValidateFilledForm(AprDocument filledForm)
    {
        return _validator.Validate(filledForm);
    }

    /// <summary>
    /// Gets all prompt IDs from a template.
    /// </summary>
    public List<string> GetPromptIds(AprDocument template)
    {
        return FormPromptMetrics.GetPromptIds(template);
    }

    /// <summary>
    /// Gets completion status (percentage of prompts filled).
    /// </summary>
    public double GetCompletionPercentage(AprDocument document)
    {
        return FormPromptMetrics.GetCompletionPercentage(document);
    }
}

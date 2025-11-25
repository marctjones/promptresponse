using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Microsoft.Extensions.Logging;

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

    public FormFillingApi(
        IAprSerializer serializer,
        DocumentValidator validator,
        ILogger<FormFillingApi> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        // Clone template to filled form
        var filledForm = CloneAsFilledForm(template, filledBy);

        // Apply responses
        var appliedCount = 0;
        var missingPrompts = new List<string>();

        foreach (var (promptId, response) in responses)
        {
            if (TrySetResponse(filledForm, promptId, response))
            {
                appliedCount++;
            }
            else
            {
                missingPrompts.Add(promptId);
                _logger.LogWarning("Prompt ID not found: {PromptId}", promptId);
            }
        }

        _logger.LogInformation(
            "Applied {Applied}/{Total} responses",
            appliedCount,
            responses.Count);

        if (missingPrompts.Count > 0)
        {
            _logger.LogWarning(
                "Missing prompts: {Missing}",
                string.Join(", ", missingPrompts));
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
        var responses = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponses);
        if (responses == null)
        {
            throw new InvalidOperationException("Invalid JSON format");
        }

        return FillForm(template, responses, filledBy);
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

        // Ensure .aprf extension
        if (!outputPath.EndsWith(".aprf", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Path.ChangeExtension(outputPath, ".aprf");
        }

        var json = _serializer.Serialize(filledForm);
        await File.WriteAllTextAsync(outputPath, json);

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
        var promptIds = new List<string>();

        foreach (var section in template.Sections)
        {
            CollectPromptIdsFromSection(section, promptIds);
        }

        return promptIds;
    }

    private void CollectPromptIdsFromSection(Section section, List<string> promptIds)
    {
        foreach (var prompt in section.Prompts)
        {
            promptIds.Add(prompt.Id);
        }

        foreach (var childSection in section.Sections)
        {
            CollectPromptIdsFromSection(childSection, promptIds);
        }
    }

    /// <summary>
    /// Gets completion status (percentage of prompts filled).
    /// </summary>
    public double GetCompletionPercentage(AprDocument document)
    {
        var totalPrompts = 0;
        var filledPrompts = 0;

        foreach (var section in document.Sections)
        {
            CountCompletionInSection(section, ref totalPrompts, ref filledPrompts);
        }

        return totalPrompts > 0 ? (double)filledPrompts / totalPrompts * 100 : 0;
    }

    private void CountCompletionInSection(Section section, ref int totalPrompts, ref int filledPrompts)
    {
        foreach (var prompt in section.Prompts)
        {
            totalPrompts++;
            if (!string.IsNullOrWhiteSpace(prompt.Response))
            {
                filledPrompts++;
            }
        }

        foreach (var childSection in section.Sections)
        {
            CountCompletionInSection(childSection, ref totalPrompts, ref filledPrompts);
        }
    }

    private AprDocument CloneAsFilledForm(AprDocument template, string? filledBy)
    {
        // Serialize and deserialize to clone
        var json = _serializer.Serialize(template);
        var cloned = _serializer.Deserialize(json);

        // Convert to filled form
        cloned.DocumentType = DocumentType.FilledForm;
        cloned.Metadata.FilledBy = filledBy ?? Environment.UserName;
        cloned.Metadata.FilledDate = DateTime.UtcNow;
        cloned.Metadata.Modified = DateTime.UtcNow;

        return cloned;
    }

    private bool TrySetResponse(AprDocument document, string promptId, string response)
    {
        foreach (var section in document.Sections)
        {
            if (TrySetResponseInSection(section, promptId, response))
            {
                return true;
            }
        }

        return false;
    }

    private bool TrySetResponseInSection(Section section, string promptId, string response)
    {
        foreach (var prompt in section.Prompts)
        {
            if (prompt.Id == promptId)
            {
                prompt.Response = response;
                prompt.ResponseMetadata.LastModified = DateTime.UtcNow;
                return true;
            }
        }

        foreach (var childSection in section.Sections)
        {
            if (TrySetResponseInSection(childSection, promptId, response))
            {
                return true;
            }
        }

        return false;
    }
}

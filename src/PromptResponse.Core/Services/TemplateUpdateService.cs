using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Services;

/// <summary>
/// Service for checking and applying template updates from upstream sources.
/// </summary>
public interface ITemplateUpdateService
{
    /// <summary>
    /// Checks if an update is available for the template.
    /// </summary>
    /// <param name="document">The filled form to check for updates.</param>
    /// <returns>Update check result with availability and version info.</returns>
    Task<TemplateUpdateCheckResult> CheckForUpdateAsync(AprDocument document);

    /// <summary>
    /// Fetches the latest template from the source URL.
    /// </summary>
    /// <param name="sourceUrl">The URL to fetch from.</param>
    /// <returns>The fetched template document.</returns>
    Task<AprDocument> FetchTemplateAsync(string sourceUrl);

    /// <summary>
    /// Applies an update by migrating responses from the current document to the new template.
    /// </summary>
    /// <param name="currentDocument">The current filled form with responses.</param>
    /// <param name="newTemplate">The new template to migrate to.</param>
    /// <returns>Migration result with the updated document and change summary.</returns>
    TemplateMigrationResult ApplyUpdate(AprDocument currentDocument, AprDocument newTemplate);
}

/// <summary>
/// Result of checking for template updates.
/// </summary>
public class TemplateUpdateCheckResult
{
    /// <summary>
    /// Whether the check was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// The current template version.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// The new template version available.
    /// </summary>
    public string? NewVersion { get; init; }

    /// <summary>
    /// Error message if the check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The fetched template if update is available.
    /// </summary>
    public AprDocument? NewTemplate { get; init; }
}

/// <summary>
/// Result of migrating responses to a new template.
/// </summary>
public class TemplateMigrationResult
{
    /// <summary>
    /// The migrated document with responses transferred to new template structure.
    /// </summary>
    public required AprDocument MigratedDocument { get; init; }

    /// <summary>
    /// Number of prompts that had their responses migrated successfully.
    /// </summary>
    public int MigratedPromptCount { get; init; }

    /// <summary>
    /// Prompts that existed in the old form but not in the new template (orphaned responses).
    /// </summary>
    public List<OrphanedPrompt> OrphanedPrompts { get; init; } = new();

    /// <summary>
    /// New prompts added in the template that weren't in the old form.
    /// </summary>
    public List<NewPrompt> NewPrompts { get; init; } = new();

    /// <summary>
    /// Summary message describing the migration.
    /// </summary>
    public string Summary => GenerateSummary();

    private string GenerateSummary()
    {
        var parts = new List<string>();

        if (MigratedPromptCount > 0)
            parts.Add($"{MigratedPromptCount} response(s) migrated");

        if (NewPrompts.Count > 0)
            parts.Add($"{NewPrompts.Count} new field(s) added");

        if (OrphanedPrompts.Count > 0)
            parts.Add($"{OrphanedPrompts.Count} field(s) removed (responses preserved in notes)");

        return parts.Count > 0 ? string.Join(", ", parts) : "No changes";
    }
}

/// <summary>
/// Represents a prompt that was in the old form but removed in the new template.
/// </summary>
public class OrphanedPrompt
{
    /// <summary>
    /// The prompt ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The prompt label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The response that was orphaned.
    /// </summary>
    public string? Response { get; init; }

    /// <summary>
    /// The section path where this prompt was located.
    /// </summary>
    public string? SectionPath { get; init; }
}

/// <summary>
/// Represents a new prompt added in the template update.
/// </summary>
public class NewPrompt
{
    /// <summary>
    /// The prompt ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The prompt label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// The section path where this prompt is located.
    /// </summary>
    public string? SectionPath { get; init; }
}

/// <summary>
/// Default implementation of the template update service.
/// </summary>
public class TemplateUpdateService : ITemplateUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly IAprSerializer _serializer;
    private readonly ILogger<TemplateUpdateService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateUpdateService"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for fetching templates.</param>
    /// <param name="serializer">The serializer for parsing APR documents.</param>
    /// <param name="logger">The logger instance.</param>
    public TemplateUpdateService(
        HttpClient httpClient,
        IAprSerializer serializer,
        ILogger<TemplateUpdateService> logger)
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TemplateUpdateCheckResult> CheckForUpdateAsync(AprDocument document)
    {
        var sourceUrl = document.Metadata.TemplateSourceUrl;

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return new TemplateUpdateCheckResult
            {
                Success = false,
                ErrorMessage = "No template source URL configured"
            };
        }

        try
        {
            _logger.LogInformation("Checking for template updates from {SourceUrl}", sourceUrl);

            var newTemplate = await FetchTemplateAsync(sourceUrl);

            var currentVersion = document.Metadata.TemplateVersion ?? "unknown";
            var newVersion = newTemplate.Metadata.TemplateVersion ?? "unknown";

            // Compare versions - update available if versions differ
            var updateAvailable = !string.Equals(currentVersion, newVersion, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Template update check complete. Current: {CurrentVersion}, Available: {NewVersion}, UpdateAvailable: {UpdateAvailable}",
                currentVersion, newVersion, updateAvailable);

            return new TemplateUpdateCheckResult
            {
                Success = true,
                UpdateAvailable = updateAvailable,
                CurrentVersion = currentVersion,
                NewVersion = newVersion,
                NewTemplate = updateAvailable ? newTemplate : null
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch template from {SourceUrl}", sourceUrl);
            return new TemplateUpdateCheckResult
            {
                Success = false,
                ErrorMessage = $"Failed to fetch template: {ex.Message}"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse template from {SourceUrl}", sourceUrl);
            return new TemplateUpdateCheckResult
            {
                Success = false,
                ErrorMessage = $"Invalid template format: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking for updates from {SourceUrl}", sourceUrl);
            return new TemplateUpdateCheckResult
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<AprDocument> FetchTemplateAsync(string sourceUrl)
    {
        _logger.LogDebug("Fetching template from {SourceUrl}", sourceUrl);

        var response = await _httpClient.GetAsync(sourceUrl);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var template = _serializer.Deserialize(content);

        if (template == null)
        {
            throw new InvalidOperationException("Failed to deserialize template");
        }

        return template;
    }

    /// <inheritdoc />
    public TemplateMigrationResult ApplyUpdate(AprDocument currentDocument, AprDocument newTemplate)
    {
        _logger.LogInformation("Applying template update from version {OldVersion} to {NewVersion}",
            currentDocument.Metadata.TemplateVersion,
            newTemplate.Metadata.TemplateVersion);

        // Build a map of all prompts in the current document with their responses
        var currentPrompts = new Dictionary<string, (Prompt Prompt, string SectionPath)>();
        CollectPrompts(currentDocument.Sections, currentPrompts, "");

        // Build a map of all prompts in the new template
        var newPrompts = new Dictionary<string, (Prompt Prompt, string SectionPath)>();
        CollectPrompts(newTemplate.Sections, newPrompts, "");

        var orphanedPrompts = new List<OrphanedPrompt>();
        var addedPrompts = new List<NewPrompt>();
        var migratedCount = 0;

        // Find orphaned prompts (in current but not in new)
        foreach (var (id, (prompt, sectionPath)) in currentPrompts)
        {
            if (!newPrompts.ContainsKey(id) && !string.IsNullOrEmpty(prompt.Response))
            {
                orphanedPrompts.Add(new OrphanedPrompt
                {
                    Id = id,
                    Label = prompt.Label,
                    Response = prompt.Response,
                    SectionPath = sectionPath
                });
            }
        }

        // Find new prompts (in new but not in current)
        foreach (var (id, (prompt, sectionPath)) in newPrompts)
        {
            if (!currentPrompts.ContainsKey(id))
            {
                addedPrompts.Add(new NewPrompt
                {
                    Id = id,
                    Label = prompt.Label,
                    SectionPath = sectionPath
                });
            }
        }

        // Create the migrated document (deep clone of new template)
        var migratedDocument = CloneDocument(newTemplate);
        migratedDocument.DocumentType = DocumentType.FilledForm;

        // Preserve filled form metadata
        migratedDocument.Metadata.FilledBy = currentDocument.Metadata.FilledBy;
        migratedDocument.Metadata.FilledDate = currentDocument.Metadata.FilledDate;
        migratedDocument.Metadata.Modified = DateTime.UtcNow;

        // Migrate responses
        MigrateResponses(migratedDocument.Sections, currentPrompts, ref migratedCount);

        // Add orphaned responses as a note in the metadata or a special section
        if (orphanedPrompts.Count > 0)
        {
            AddOrphanedResponsesNote(migratedDocument, orphanedPrompts);
        }

        _logger.LogInformation(
            "Template migration complete. Migrated: {Migrated}, New: {New}, Orphaned: {Orphaned}",
            migratedCount, addedPrompts.Count, orphanedPrompts.Count);

        return new TemplateMigrationResult
        {
            MigratedDocument = migratedDocument,
            MigratedPromptCount = migratedCount,
            OrphanedPrompts = orphanedPrompts,
            NewPrompts = addedPrompts
        };
    }

    private void CollectPrompts(
        List<Section> sections,
        Dictionary<string, (Prompt, string)> prompts,
        string parentPath)
    {
        foreach (var section in sections)
        {
            var sectionPath = string.IsNullOrEmpty(parentPath)
                ? section.Title
                : $"{parentPath} > {section.Title}";

            foreach (var prompt in section.Prompts)
            {
                if (!string.IsNullOrEmpty(prompt.Id))
                {
                    prompts[prompt.Id] = (prompt, sectionPath);
                }
            }

            if (section.Sections.Count > 0)
            {
                CollectPrompts(section.Sections, prompts, sectionPath);
            }
        }
    }

    private void MigrateResponses(
        List<Section> sections,
        Dictionary<string, (Prompt Prompt, string SectionPath)> sourcePrompts,
        ref int migratedCount)
    {
        foreach (var section in sections)
        {
            foreach (var prompt in section.Prompts)
            {
                if (!string.IsNullOrEmpty(prompt.Id) &&
                    sourcePrompts.TryGetValue(prompt.Id, out var source) &&
                    !string.IsNullOrEmpty(source.Prompt.Response))
                {
                    prompt.Response = source.Prompt.Response;

                    // Also migrate response metadata if available
                    if (source.Prompt.ResponseMetadata != null)
                    {
                        prompt.ResponseMetadata = new ResponseMetadata
                        {
                            LastModified = source.Prompt.ResponseMetadata.LastModified,
                            InferredDataType = source.Prompt.ResponseMetadata.InferredDataType
                        };
                    }

                    migratedCount++;
                }
            }

            if (section.Sections.Count > 0)
            {
                MigrateResponses(section.Sections, sourcePrompts, ref migratedCount);
            }
        }
    }

    private AprDocument CloneDocument(AprDocument source)
    {
        // Use serialization for deep clone
        var json = _serializer.Serialize(source);
        return _serializer.Deserialize(json)
            ?? throw new InvalidOperationException("Failed to clone document");
    }

    private void AddOrphanedResponsesNote(AprDocument document, List<OrphanedPrompt> orphanedPrompts)
    {
        // Store orphaned responses in document description as a note
        var note = "\n\n--- Responses from previous template version (fields removed in update) ---\n";

        foreach (var orphan in orphanedPrompts)
        {
            note += $"- {orphan.Label} ({orphan.Id}): {orphan.Response}\n";
        }

        document.Metadata.Description = (document.Metadata.Description ?? "") + note;
    }
}

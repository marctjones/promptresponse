# Form Management System - Technical Specification

**Version:** 1.0
**Date:** 2025-11-18
**Status:** Design Phase

## Executive Summary

This document specifies the architecture and implementation plan for transforming PromptResponse into a comprehensive form management system. The system will enable organizations (e.g., town halls, municipal offices) to manage collections of APR forms with features including:

- **Storage abstraction** for local folders and AWS S3
- **Metadata database** for tracking, tagging, and workflow management
- **Form discovery and grouping** by template type
- **Private tagging system** that doesn't modify original files
- **Status tracking** for form processing workflows
- **Reporting capabilities** for submission analytics

## 1. System Architecture

### 1.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Desktop Application                       │
│  ┌───────────────────────────────────────────────────────┐ │
│  │           PromptResponse.Desktop                      │ │
│  │  - MainWindow (existing)                              │ │
│  │  - FormManagementView (NEW)                           │ │
│  │  - StorageConfigView (NEW)                            │ │
│  └─────────────────┬─────────────────────────────────────┘ │
└────────────────────┼───────────────────────────────────────┘
                     │
        ┌────────────┴──────────────┐
        │                           │
        ▼                           ▼
┌──────────────────┐      ┌──────────────────┐
│ Form Management  │      │  Storage Layer   │
│     Service      │◄─────┤   (NEW)          │
│   (Business      │      │ - IStorage       │
│    Logic)        │      │   Provider       │
└────────┬─────────┘      │ - Local          │
         │                │ - S3             │
         │                └──────────────────┘
         │
         ▼
┌──────────────────┐
│  Metadata DB     │
│  (SQLite)        │
│  - Forms         │
│  - Tags          │
│  - Templates     │
│  - Status        │
└──────────────────┘
```

### 1.2 New Projects

#### PromptResponse.Storage
- **Purpose:** Abstraction layer for form storage (local/cloud)
- **Dependencies:**
  - PromptResponse.Core
  - AWSSDK.S3 (3.7.x)
- **Namespace:** `PromptResponse.Storage`

#### PromptResponse.Data
- **Purpose:** Entity Framework Core data layer for metadata
- **Dependencies:**
  - PromptResponse.Core
  - Microsoft.EntityFrameworkCore.Sqlite (8.0.x)
  - Microsoft.EntityFrameworkCore.Design (8.0.x)
- **Namespace:** `PromptResponse.Data`

#### PromptResponse.Management
- **Purpose:** Business logic for form management
- **Dependencies:**
  - PromptResponse.Core
  - PromptResponse.Storage
  - PromptResponse.Data
- **Namespace:** `PromptResponse.Management`

## 2. Data Model

### 2.1 Database Schema (SQLite)

```sql
-- Form metadata (doesn't modify APR files)
CREATE TABLE FormMetadata (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FormId TEXT NOT NULL UNIQUE,           -- Storage provider ID
    FileName TEXT NOT NULL,
    TemplateId TEXT,                       -- From APR metadata
    StorageLocation TEXT NOT NULL,         -- "local" | "s3"
    StorageKey TEXT NOT NULL,              -- Path or S3 key

    -- Dates from file system
    FileCreatedDate TEXT NOT NULL,         -- ISO 8601
    FileModifiedDate TEXT NOT NULL,        -- ISO 8601

    -- Dates from metadata
    ImportedDate TEXT NOT NULL,            -- When added to system
    LastScannedDate TEXT NOT NULL,         -- Last time file was checked

    -- Workflow tracking
    Status TEXT NOT NULL DEFAULT 'New',    -- ProcessingStatus enum
    AssignedTo TEXT,                       -- Username
    ProcessedDate TEXT,                    -- When marked processed

    -- Computed/cached fields
    Title TEXT,                            -- From APR metadata
    FilledBy TEXT,                         -- From APR metadata
    FilledDate TEXT,                       -- From APR metadata

    -- Flags
    IsTemplate INTEGER NOT NULL DEFAULT 0, -- Boolean
    IsDeleted INTEGER NOT NULL DEFAULT 0,  -- Soft delete

    FOREIGN KEY (TemplateId) REFERENCES FormTemplate(TemplateId)
);

CREATE INDEX idx_form_template ON FormMetadata(TemplateId);
CREATE INDEX idx_form_status ON FormMetadata(Status);
CREATE INDEX idx_form_storage ON FormMetadata(StorageLocation);
CREATE INDEX idx_form_created ON FormMetadata(FileCreatedDate);
CREATE INDEX idx_form_modified ON FormMetadata(FileModifiedDate);

-- Tags (private to user, never written to APR file)
CREATE TABLE FormTag (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FormMetadataId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Color TEXT NOT NULL,                  -- Hex color
    CreatedDate TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,              -- Username

    FOREIGN KEY (FormMetadataId) REFERENCES FormMetadata(Id) ON DELETE CASCADE
);

CREATE INDEX idx_tag_form ON FormTag(FormMetadataId);
CREATE INDEX idx_tag_name ON FormTag(Name);

-- Template catalog
CREATE TABLE FormTemplate (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TemplateId TEXT NOT NULL UNIQUE,      -- From APR metadata
    Title TEXT NOT NULL,
    Description TEXT,
    Version TEXT,

    -- Statistics
    SubmissionCount INTEGER NOT NULL DEFAULT 0,
    FirstSeenDate TEXT NOT NULL,
    LastUsedDate TEXT,

    -- Reference to template file (if available)
    TemplateFormId TEXT,                   -- FormId of template

    FOREIGN KEY (TemplateFormId) REFERENCES FormMetadata(FormId)
);

CREATE INDEX idx_template_id ON FormTemplate(TemplateId);

-- Notes (free-form text annotations)
CREATE TABLE FormNote (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FormMetadataId INTEGER NOT NULL,
    NoteText TEXT NOT NULL,
    CreatedDate TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedDate TEXT NOT NULL,

    FOREIGN KEY (FormMetadataId) REFERENCES FormMetadata(Id) ON DELETE CASCADE
);

CREATE INDEX idx_note_form ON FormNote(FormMetadataId);

-- Storage connections
CREATE TABLE StorageConnection (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,                   -- "Local" | "S3"
    IsActive INTEGER NOT NULL DEFAULT 1,

    -- Local settings
    LocalPath TEXT,

    -- S3 settings
    S3BucketName TEXT,
    S3Region TEXT,
    S3Prefix TEXT,
    S3AccessKeyId TEXT,                   -- Encrypted
    S3SecretAccessKey TEXT,               -- Encrypted

    CreatedDate TEXT NOT NULL,
    LastUsedDate TEXT
);
```

### 2.2 Entity Models

```csharp
namespace PromptResponse.Data.Entities;

public class FormMetadata
{
    public int Id { get; set; }
    public string FormId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? TemplateId { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;

    public DateTime FileCreatedDate { get; set; }
    public DateTime FileModifiedDate { get; set; }
    public DateTime ImportedDate { get; set; }
    public DateTime LastScannedDate { get; set; }

    public ProcessingStatus Status { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? ProcessedDate { get; set; }

    public string? Title { get; set; }
    public string? FilledBy { get; set; }
    public DateTime? FilledDate { get; set; }

    public bool IsTemplate { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties
    public List<FormTag> Tags { get; set; } = new();
    public List<FormNote> Notes { get; set; } = new();
    public FormTemplate? Template { get; set; }
}

public class FormTag
{
    public int Id { get; set; }
    public int FormMetadataId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#808080";
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    public FormMetadata Form { get; set; } = null!;
}

public class FormTemplate
{
    public int Id { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }

    public int SubmissionCount { get; set; }
    public DateTime FirstSeenDate { get; set; }
    public DateTime? LastUsedDate { get; set; }

    public string? TemplateFormId { get; set; }

    public List<FormMetadata> Submissions { get; set; } = new();
}

public class FormNote
{
    public int Id { get; set; }
    public int FormMetadataId { get; set; }
    public string NoteText { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedDate { get; set; }

    public FormMetadata Form { get; set; } = null!;
}

public class StorageConnection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public StorageType Type { get; set; }
    public bool IsActive { get; set; }

    public string? LocalPath { get; set; }

    public string? S3BucketName { get; set; }
    public string? S3Region { get; set; }
    public string? S3Prefix { get; set; }
    public string? S3AccessKeyId { get; set; }
    public string? S3SecretAccessKey { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? LastUsedDate { get; set; }
}

public enum ProcessingStatus
{
    New,
    InReview,
    Processed,
    RequiresRevision,
    Archived
}

public enum StorageType
{
    Local,
    S3
}
```

## 3. Storage Abstraction Layer

### 3.1 IStorageProvider Interface

```csharp
namespace PromptResponse.Storage;

/// <summary>
/// Abstraction for form storage (local file system or cloud storage).
/// </summary>
public interface IStorageProvider
{
    string ProviderType { get; }

    /// <summary>
    /// Lists all APR files in the storage.
    /// </summary>
    Task<IEnumerable<StoredFormInfo>> ListFormsAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about a form without loading the entire document.
    /// </summary>
    Task<StoredFormInfo> GetFormInfoAsync(
        string formId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a complete APR document.
    /// </summary>
    Task<AprDocument> LoadFormAsync(
        string formId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an APR document (updates existing or creates new).
    /// </summary>
    Task<string> SaveFormAsync(
        AprDocument document,
        string? formId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets raw bytes of form file (for backup/export).
    /// </summary>
    Task<byte[]> GetFormBytesAsync(
        string formId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a form exists.
    /// </summary>
    Task<bool> FormExistsAsync(
        string formId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a form (if supported).
    /// </summary>
    Task DeleteFormAsync(
        string formId,
        CancellationToken cancellationToken = default);
}

public record StoredFormInfo(
    string FormId,
    string FileName,
    string StorageKey,
    DateTime CreatedDate,
    DateTime ModifiedDate,
    long SizeBytes,
    string? Etag = null
);
```

### 3.2 LocalFileStorageProvider

```csharp
namespace PromptResponse.Storage.Local;

public class LocalFileStorageProvider : IStorageProvider
{
    private readonly string _basePath;
    private readonly IAprSerializer _serializer;

    public string ProviderType => "Local";

    public LocalFileStorageProvider(string basePath, IAprSerializer serializer)
    {
        _basePath = Path.GetFullPath(basePath);
        _serializer = serializer;

        if (!Directory.Exists(_basePath))
            throw new DirectoryNotFoundException($"Base path not found: {_basePath}");
    }

    public async Task<IEnumerable<StoredFormInfo>> ListFormsAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var searchPath = string.IsNullOrEmpty(prefix)
            ? _basePath
            : Path.Combine(_basePath, prefix);

        if (!Directory.Exists(searchPath))
            return Enumerable.Empty<StoredFormInfo>();

        var patterns = new[] { "*.apr", "*.aprt", "*.aprf" };
        var forms = new List<StoredFormInfo>();

        foreach (var pattern in patterns)
        {
            var files = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                var fileInfo = new FileInfo(filePath);
                var relativePath = Path.GetRelativePath(_basePath, filePath);

                forms.Add(new StoredFormInfo(
                    FormId: relativePath,
                    FileName: fileInfo.Name,
                    StorageKey: relativePath,
                    CreatedDate: fileInfo.CreationTimeUtc,
                    ModifiedDate: fileInfo.LastWriteTimeUtc,
                    SizeBytes: fileInfo.Length
                ));
            }
        }

        return forms;
    }

    public async Task<AprDocument> LoadFormAsync(
        string formId,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_basePath, formId);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Form not found: {formId}");

        await using var stream = File.OpenRead(fullPath);
        var document = await _serializer.DeserializeAsync(stream);

        if (document == null)
            throw new InvalidOperationException($"Failed to deserialize form: {formId}");

        // Apply extension-based document type override
        var extension = Path.GetExtension(formId).ToLowerInvariant();
        if (extension == ".aprt")
            document.DocumentType = DocumentType.Template;
        else if (extension == ".aprf")
            document.DocumentType = DocumentType.FilledForm;

        return document;
    }

    public async Task<string> SaveFormAsync(
        AprDocument document,
        string? formId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(formId))
        {
            // Generate new file name
            var extension = document.DocumentType == DocumentType.Template ? ".aprt" : ".aprf";
            var fileName = $"{SanitizeFileName(document.Metadata.Title)}_{Guid.NewGuid():N}{extension}";
            formId = fileName;
        }

        var fullPath = Path.Combine(_basePath, formId);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = _serializer.Serialize(document);
        await File.WriteAllTextAsync(fullPath, json, cancellationToken);

        return formId;
    }

    // Additional methods...
}
```

### 3.3 S3StorageProvider

```csharp
namespace PromptResponse.Storage.S3;

using Amazon.S3;
using Amazon.S3.Model;

public class S3StorageProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _prefix;
    private readonly IAprSerializer _serializer;

    public string ProviderType => "S3";

    public S3StorageProvider(
        string bucketName,
        string region,
        string accessKey,
        string secretKey,
        IAprSerializer serializer,
        string? prefix = null)
    {
        _bucketName = bucketName;
        _prefix = prefix ?? string.Empty;
        _serializer = serializer;

        var config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region) };
        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<IEnumerable<StoredFormInfo>> ListFormsAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var searchPrefix = string.IsNullOrEmpty(prefix)
            ? _prefix
            : $"{_prefix}/{prefix}".TrimStart('/');

        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = searchPrefix
        };

        var forms = new List<StoredFormInfo>();
        ListObjectsV2Response response;

        do
        {
            response = await _s3Client.ListObjectsV2Async(request, cancellationToken);

            foreach (var obj in response.S3Objects)
            {
                var extension = Path.GetExtension(obj.Key).ToLowerInvariant();
                if (extension is ".apr" or ".aprt" or ".aprf")
                {
                    forms.Add(new StoredFormInfo(
                        FormId: obj.Key,
                        FileName: Path.GetFileName(obj.Key),
                        StorageKey: obj.Key,
                        CreatedDate: obj.LastModified,
                        ModifiedDate: obj.LastModified,
                        SizeBytes: obj.Size,
                        Etag: obj.ETag
                    ));
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated);

        return forms;
    }

    public async Task<AprDocument> LoadFormAsync(
        string formId,
        CancellationToken cancellationToken = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = formId
        };

        using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        await using var stream = response.ResponseStream;

        var document = await _serializer.DeserializeAsync(stream);

        if (document == null)
            throw new InvalidOperationException($"Failed to deserialize form: {formId}");

        return document;
    }

    // Additional methods...
}
```

## 4. Form Management Service

### 4.1 IFormManagementService Interface

```csharp
namespace PromptResponse.Management;

public interface IFormManagementService
{
    // Discovery and Import
    Task<ImportResult> ScanAndImportFormsAsync(
        IStorageProvider storage,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task RefreshFormMetadataAsync(
        string formId,
        CancellationToken cancellationToken = default);

    // Querying
    Task<PagedResult<FormSummary>> GetFormsAsync(
        FormQuery query,
        CancellationToken cancellationToken = default);

    Task<FormDetail?> GetFormDetailAsync(
        string formId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TemplateGroup>> GetFormsByTemplateAsync(
        CancellationToken cancellationToken = default);

    Task<IEnumerable<FormSummary>> GetRecentFormsAsync(
        int count = 10,
        CancellationToken cancellationToken = default);

    // Tagging
    Task<FormTag> AddTagAsync(
        string formId,
        string tagName,
        string color,
        CancellationToken cancellationToken = default);

    Task RemoveTagAsync(
        int tagId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<string>> GetAvailableTagNamesAsync(
        CancellationToken cancellationToken = default);

    // Notes
    Task<FormNote> AddNoteAsync(
        string formId,
        string noteText,
        CancellationToken cancellationToken = default);

    Task UpdateNoteAsync(
        int noteId,
        string noteText,
        CancellationToken cancellationToken = default);

    Task DeleteNoteAsync(
        int noteId,
        CancellationToken cancellationToken = default);

    // Workflow
    Task UpdateStatusAsync(
        string formId,
        ProcessingStatus status,
        CancellationToken cancellationToken = default);

    Task AssignFormAsync(
        string formId,
        string userName,
        CancellationToken cancellationToken = default);

    Task BulkUpdateStatusAsync(
        IEnumerable<string> formIds,
        ProcessingStatus status,
        CancellationToken cancellationToken = default);

    // Reporting
    Task<FormStatistics> GetStatisticsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<FormSummary>> GetProcessedFormsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    // Export
    Task<string> ExportToCsvAsync(
        FormQuery query,
        string outputPath,
        CancellationToken cancellationToken = default);
}

public record FormQuery(
    string? TemplateId = null,
    ProcessingStatus? Status = null,
    DateTime? CreatedAfter = null,
    DateTime? CreatedBefore = null,
    DateTime? ModifiedAfter = null,
    DateTime? ModifiedBefore = null,
    string? TagName = null,
    string? SearchText = null,
    string? AssignedTo = null,
    FormQuerySortBy SortBy = FormQuerySortBy.ModifiedDateDesc,
    int Skip = 0,
    int Take = 50
);

public enum FormQuerySortBy
{
    CreatedDateAsc,
    CreatedDateDesc,
    ModifiedDateAsc,
    ModifiedDateDesc,
    TitleAsc,
    TitleDesc,
    StatusAsc,
    StatusDesc
}

public record FormSummary(
    string FormId,
    string FileName,
    string? TemplateId,
    string? TemplateTitle,
    string? Title,
    DateTime CreatedDate,
    DateTime ModifiedDate,
    ProcessingStatus Status,
    string? AssignedTo,
    List<FormTag> Tags,
    bool IsTemplate
);

public record FormDetail(
    FormSummary Summary,
    string? Description,
    string? FilledBy,
    DateTime? FilledDate,
    string StorageLocation,
    long SizeBytes,
    List<FormNote> Notes
);

public record TemplateGroup(
    string TemplateId,
    string TemplateTitle,
    string? Description,
    int TotalCount,
    int NewCount,
    int InReviewCount,
    int ProcessedCount,
    DateTime? LastSubmissionDate,
    List<FormSummary> RecentSubmissions
);

public record FormStatistics(
    int TotalForms,
    int TotalTemplates,
    Dictionary<ProcessingStatus, int> FormsByStatus,
    Dictionary<string, int> FormsByTemplate,
    Dictionary<DateTime, int> FormsByDate,
    int FormsThisWeek,
    int FormsThisMonth,
    Dictionary<string, int> MostUsedTags
);

public record ImportResult(
    int FormsScanned,
    int FormsImported,
    int FormsUpdated,
    int FormsSkipped,
    int TemplatesDiscovered,
    TimeSpan Duration,
    List<string> Errors
);

public record ImportProgress(
    int FormsProcessed,
    int TotalForms,
    string? CurrentFile
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);
```

### 4.2 Implementation Key Methods

```csharp
public class FormManagementService : IFormManagementService
{
    private readonly FormMetadataContext _dbContext;
    private readonly IAprSerializer _serializer;
    private readonly ILogger<FormManagementService> _logger;

    public async Task<ImportResult> ScanAndImportFormsAsync(
        IStorageProvider storage,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult(0, 0, 0, 0, 0, TimeSpan.Zero, new List<string>());

        try
        {
            // Get all forms from storage
            var storedForms = await storage.ListFormsAsync(cancellationToken: cancellationToken);
            var formsList = storedForms.ToList();

            result = result with { FormsScanned = formsList.Count };

            for (int i = 0; i < formsList.Count; i++)
            {
                var storedForm = formsList[i];
                progress?.Report(new ImportProgress(i + 1, formsList.Count, storedForm.FileName));

                try
                {
                    // Check if form already exists
                    var existing = await _dbContext.FormMetadata
                        .FirstOrDefaultAsync(f => f.FormId == storedForm.FormId, cancellationToken);

                    if (existing != null)
                    {
                        // Update if modified
                        if (existing.FileModifiedDate < storedForm.ModifiedDate)
                        {
                            await UpdateFormMetadataAsync(existing, storage, storedForm, cancellationToken);
                            result = result with { FormsUpdated = result.FormsUpdated + 1 };
                        }
                        else
                        {
                            result = result with { FormsSkipped = result.FormsSkipped + 1 };
                        }
                    }
                    else
                    {
                        // Import new form
                        await ImportFormAsync(storage, storedForm, cancellationToken);
                        result = result with { FormsImported = result.FormsImported + 1 };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing form: {FormId}", storedForm.FormId);
                    result.Errors.Add($"{storedForm.FileName}: {ex.Message}");
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Count templates discovered
            var templateCount = await _dbContext.FormTemplate.CountAsync(cancellationToken);
            result = result with { TemplatesDiscovered = templateCount };
        }
        finally
        {
            stopwatch.Stop();
            result = result with { Duration = stopwatch.Elapsed };
        }

        return result;
    }

    private async Task ImportFormAsync(
        IStorageProvider storage,
        StoredFormInfo storedForm,
        CancellationToken cancellationToken)
    {
        // Load the APR document to get metadata
        var document = await storage.LoadFormAsync(storedForm.FormId, cancellationToken);

        // Create form metadata entry
        var formMetadata = new FormMetadata
        {
            FormId = storedForm.FormId,
            FileName = storedForm.FileName,
            TemplateId = document.Metadata.TemplateId,
            StorageLocation = storage.ProviderType,
            StorageKey = storedForm.StorageKey,
            FileCreatedDate = storedForm.CreatedDate,
            FileModifiedDate = storedForm.ModifiedDate,
            ImportedDate = DateTime.UtcNow,
            LastScannedDate = DateTime.UtcNow,
            Status = ProcessingStatus.New,
            Title = document.Metadata.Title,
            FilledBy = document.Metadata.FilledBy,
            FilledDate = document.Metadata.FilledDate,
            IsTemplate = document.DocumentType == DocumentType.Template
        };

        _dbContext.FormMetadata.Add(formMetadata);

        // Register or update template
        if (!string.IsNullOrEmpty(document.Metadata.TemplateId))
        {
            await RegisterTemplateAsync(document, cancellationToken);
        }
    }

    private async Task RegisterTemplateAsync(
        AprDocument document,
        CancellationToken cancellationToken)
    {
        var templateId = document.Metadata.TemplateId!;

        var template = await _dbContext.FormTemplate
            .FirstOrDefaultAsync(t => t.TemplateId == templateId, cancellationToken);

        if (template == null)
        {
            template = new FormTemplate
            {
                TemplateId = templateId,
                Title = document.Metadata.Title,
                Description = document.Metadata.Description,
                Version = document.Metadata.TemplateVersion,
                FirstSeenDate = DateTime.UtcNow
            };
            _dbContext.FormTemplate.Add(template);
        }

        template.SubmissionCount++;
        template.LastUsedDate = DateTime.UtcNow;
    }

    public async Task<PagedResult<FormSummary>> GetFormsAsync(
        FormQuery query,
        CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.FormMetadata
            .Include(f => f.Tags)
            .Include(f => f.Template)
            .Where(f => !f.IsDeleted)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(query.TemplateId))
            queryable = queryable.Where(f => f.TemplateId == query.TemplateId);

        if (query.Status.HasValue)
            queryable = queryable.Where(f => f.Status == query.Status.Value);

        if (query.CreatedAfter.HasValue)
            queryable = queryable.Where(f => f.FileCreatedDate >= query.CreatedAfter.Value);

        if (query.CreatedBefore.HasValue)
            queryable = queryable.Where(f => f.FileCreatedDate <= query.CreatedBefore.Value);

        if (query.ModifiedAfter.HasValue)
            queryable = queryable.Where(f => f.FileModifiedDate >= query.ModifiedAfter.Value);

        if (query.ModifiedBefore.HasValue)
            queryable = queryable.Where(f => f.FileModifiedDate <= query.ModifiedBefore.Value);

        if (!string.IsNullOrEmpty(query.TagName))
            queryable = queryable.Where(f => f.Tags.Any(t => t.Name == query.TagName));

        if (!string.IsNullOrEmpty(query.AssignedTo))
            queryable = queryable.Where(f => f.AssignedTo == query.AssignedTo);

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var search = query.SearchText.ToLower();
            queryable = queryable.Where(f =>
                f.FileName.ToLower().Contains(search) ||
                (f.Title != null && f.Title.ToLower().Contains(search)) ||
                (f.FilledBy != null && f.FilledBy.ToLower().Contains(search)));
        }

        // Apply sorting
        queryable = query.SortBy switch
        {
            FormQuerySortBy.CreatedDateAsc => queryable.OrderBy(f => f.FileCreatedDate),
            FormQuerySortBy.CreatedDateDesc => queryable.OrderByDescending(f => f.FileCreatedDate),
            FormQuerySortBy.ModifiedDateAsc => queryable.OrderBy(f => f.FileModifiedDate),
            FormQuerySortBy.ModifiedDateDesc => queryable.OrderByDescending(f => f.FileModifiedDate),
            FormQuerySortBy.TitleAsc => queryable.OrderBy(f => f.Title),
            FormQuerySortBy.TitleDesc => queryable.OrderByDescending(f => f.Title),
            FormQuerySortBy.StatusAsc => queryable.OrderBy(f => f.Status),
            FormQuerySortBy.StatusDesc => queryable.OrderByDescending(f => f.Status),
            _ => queryable.OrderByDescending(f => f.FileModifiedDate)
        };

        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(f => new FormSummary(
                f.FormId,
                f.FileName,
                f.TemplateId,
                f.Template != null ? f.Template.Title : null,
                f.Title,
                f.FileCreatedDate,
                f.FileModifiedDate,
                f.Status,
                f.AssignedTo,
                f.Tags.ToList(),
                f.IsTemplate
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<FormSummary>(
            items,
            totalCount,
            query.Skip / query.Take + 1,
            query.Take
        );
    }

    // Additional methods...
}
```

## 5. User Interface Design

### 5.1 New Views

#### FormManagementView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PromptResponse.Desktop.Views.FormManagementView">

    <Grid ColumnDefinitions="250,*">
        <!-- Left Sidebar: Filters and Templates -->
        <ScrollViewer Grid.Column="0" Padding="10">
            <StackPanel Spacing="10">
                <TextBlock Text="Form Management"
                           FontSize="18"
                           FontWeight="Bold"/>

                <!-- Storage Selector -->
                <StackPanel>
                    <TextBlock Text="Storage" FontWeight="SemiBold"/>
                    <ComboBox Items="{Binding StorageConnections}"
                              SelectedItem="{Binding SelectedStorage}"/>
                    <Button Content="Configure..." Command="{Binding ConfigureStorageCommand}"/>
                </StackPanel>

                <!-- Template Filter -->
                <StackPanel>
                    <TextBlock Text="Templates" FontWeight="SemiBold"/>
                    <ListBox Items="{Binding Templates}"
                             SelectedItem="{Binding SelectedTemplate}">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <StackPanel>
                                    <TextBlock Text="{Binding Title}" FontWeight="Medium"/>
                                    <TextBlock Text="{Binding SubmissionCount, StringFormat='{}{0} submissions'}"
                                               FontSize="11" Foreground="#666"/>
                                </StackPanel>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </StackPanel>

                <!-- Status Filter -->
                <StackPanel>
                    <TextBlock Text="Status" FontWeight="SemiBold"/>
                    <CheckBox Content="New" IsChecked="{Binding ShowNew}"/>
                    <CheckBox Content="In Review" IsChecked="{Binding ShowInReview}"/>
                    <CheckBox Content="Processed" IsChecked="{Binding ShowProcessed}"/>
                    <CheckBox Content="Requires Revision" IsChecked="{Binding ShowRequiresRevision}"/>
                    <CheckBox Content="Archived" IsChecked="{Binding ShowArchived}"/>
                </StackPanel>

                <!-- Tag Filter -->
                <StackPanel>
                    <TextBlock Text="Tags" FontWeight="SemiBold"/>
                    <ComboBox Items="{Binding AvailableTags}"
                              SelectedItem="{Binding SelectedTag}"
                              PlaceholderText="Filter by tag..."/>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>

        <!-- Right Content: Form List and Details -->
        <Grid Grid.Column="1" RowDefinitions="Auto,*,Auto">
            <!-- Toolbar -->
            <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="10" Margin="10">
                <TextBox Text="{Binding SearchText}"
                         Watermark="Search forms..."
                         Width="300"/>
                <Button Content="Refresh" Command="{Binding RefreshCommand}"/>
                <Button Content="Generate Report" Command="{Binding GenerateReportCommand}"/>
                <Button Content="Export CSV" Command="{Binding ExportCsvCommand}"/>
            </StackPanel>

            <!-- Forms List -->
            <DataGrid Grid.Row="1"
                      Items="{Binding Forms}"
                      SelectedItem="{Binding SelectedForm}"
                      IsReadOnly="True"
                      AutoGenerateColumns="False">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="File Name" Binding="{Binding FileName}"/>
                    <DataGridTextColumn Header="Template" Binding="{Binding TemplateTitle}"/>
                    <DataGridTextColumn Header="Created" Binding="{Binding CreatedDate, StringFormat='{}{0:yyyy-MM-dd}'}"/>
                    <DataGridTextColumn Header="Modified" Binding="{Binding ModifiedDate, StringFormat='{}{0:yyyy-MM-dd}'}"/>
                    <DataGridTextColumn Header="Status" Binding="{Binding Status}"/>
                    <DataGridTemplateColumn Header="Tags">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <ItemsControl Items="{Binding Tags}">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <StackPanel Orientation="Horizontal" Spacing="4"/>
                                        </ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Border Background="{Binding Color}"
                                                    CornerRadius="3"
                                                    Padding="4,2">
                                                <TextBlock Text="{Binding Name}"
                                                           FontSize="10"
                                                           Foreground="White"/>
                                            </Border>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>

            <!-- Action Panel -->
            <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="10" Margin="10">
                <Button Content="Open Form" Command="{Binding OpenFormCommand}"/>
                <Button Content="Add Tag..." Command="{Binding AddTagCommand}"/>
                <ComboBox Items="{Binding StatusOptions}"
                          SelectedItem="{Binding NewStatus}"
                          PlaceholderText="Change status..."/>
                <Button Content="Apply Status" Command="{Binding ApplyStatusCommand}"/>
                <Button Content="Assign To..." Command="{Binding AssignCommand}"/>
            </StackPanel>
        </Grid>
    </Grid>
</UserControl>
```

### 5.2 ViewModels

```csharp
public class FormManagementViewModel : ViewModelBase
{
    private readonly IFormManagementService _formService;
    private readonly IStorageProvider _storage;

    public ObservableCollection<StorageConnectionViewModel> StorageConnections { get; }
    public ObservableCollection<TemplateGroupViewModel> Templates { get; }
    public ObservableCollection<FormSummaryViewModel> Forms { get; }
    public ObservableCollection<string> AvailableTags { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = LoadFormsAsync(); // Debounce in production
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenFormCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand ApplyStatusCommand { get; }
    public ICommand GenerateReportCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand ConfigureStorageCommand { get; }

    private async Task LoadFormsAsync()
    {
        var query = BuildQuery();
        var result = await _formService.GetFormsAsync(query);

        Forms.Clear();
        foreach (var form in result.Items)
        {
            Forms.Add(new FormSummaryViewModel(form));
        }
    }

    private FormQuery BuildQuery()
    {
        return new FormQuery(
            TemplateId: SelectedTemplate?.TemplateId,
            Status: GetSelectedStatus(),
            SearchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            TagName: SelectedTag,
            SortBy: CurrentSortBy,
            Skip: (CurrentPage - 1) * PageSize,
            Take: PageSize
        );
    }
}
```

## 6. Implementation Phases

### Phase 1: Storage Layer (Weeks 1-2)
- [x] Define `IStorageProvider` interface
- [ ] Implement `LocalFileStorageProvider`
- [ ] Unit tests for local provider
- [ ] Implement `S3StorageProvider`
- [ ] Integration tests with MinIO/LocalStack
- [ ] Error handling and logging

### Phase 2: Data Layer (Weeks 2-3)
- [ ] Define Entity Framework models
- [ ] Create `FormMetadataContext`
- [ ] EF Core migrations
- [ ] Seed data for testing
- [ ] Repository pattern (optional)
- [ ] Unit tests for data access

### Phase 3: Business Logic (Weeks 3-5)
- [ ] Implement `FormManagementService`
- [ ] Form scanning and import
- [ ] Tag management
- [ ] Status workflow
- [ ] Search and filtering
- [ ] Statistics and reporting
- [ ] Unit and integration tests

### Phase 4: UI Layer (Weeks 5-7)
- [ ] Create `FormManagementView.axaml`
- [ ] Implement `FormManagementViewModel`
- [ ] Storage configuration dialog
- [ ] Tag management UI
- [ ] Status change UI
- [ ] Report generation UI
- [ ] Accessibility testing

### Phase 5: Integration (Week 7-8)
- [ ] Integrate with existing `MainWindow`
- [ ] Add navigation between form editing and management
- [ ] Settings persistence
- [ ] Performance optimization
- [ ] End-to-end testing

### Phase 6: Polish and Documentation (Week 9-10)
- [ ] Performance tuning
- [ ] Accessibility audit
- [ ] User documentation
- [ ] Administrator guide
- [ ] Migration guide for existing users

## 7. Security Considerations

### 7.1 Credential Storage
- **S3 Credentials**: Store encrypted in database using Data Protection API (DPAPI) on Windows, keychain on macOS, secret-service on Linux
- **Never log credentials**
- **Use IAM roles** when running on EC2

### 7.2 Tag Privacy
- Tags are stored ONLY in local SQLite database
- Tags NEVER written to APR files
- Tags are per-user (if multi-user support added)

### 7.3 File Permissions
- Validate file paths to prevent directory traversal
- Check permissions before reading/writing
- Handle permission errors gracefully

## 8. Performance Targets

- **Form list load**: < 500ms for 1000 forms
- **Form detail load**: < 100ms
- **Search**: < 200ms with indexed fields
- **Bulk import**: > 50 forms/second (local), > 10 forms/second (S3)
- **Tag operations**: < 50ms

## 9. Testing Strategy

### 9.1 Unit Tests
- All service methods
- Data access layer
- ViewModels
- Converters

### 9.2 Integration Tests
- Storage providers with real file systems
- Database operations
- Full import workflow
- Search and filtering

### 9.3 Accessibility Tests
- Screen reader navigation
- Keyboard-only workflow
- High contrast support
- Focus management

### 9.4 Performance Tests
- Large dataset (10,000 forms)
- Concurrent operations
- Memory usage
- Database query efficiency

## 10. Dependencies

### New NuGet Packages

```xml
<!-- PromptResponse.Storage -->
<PackageReference Include="AWSSDK.S3" Version="3.7.*" />

<!-- PromptResponse.Data -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*" />

<!-- PromptResponse.Desktop (additional) -->
<PackageReference Include="Avalonia.Controls.DataGrid" Version="11.0.*" />
```

## 11. Migration Path for Existing Users

Existing PromptResponse users will see:
1. **No breaking changes** to existing form editing workflow
2. **Optional**: New "Form Management" menu item
3. **On first use**: Wizard to set up storage connection
4. **Automatic**: Scans configured folder and builds database

## Appendix A: Town Hall Scenario Walkthrough

```
Day 1: Setup
1. Admin opens PromptResponse
2. Clicks "Tools > Form Management"
3. Wizard opens: "Set up form storage"
4. Selects "Local Folder"
5. Browses to "C:\TownHall\Forms"
6. Clicks "Scan Forms"
7. App discovers:
   - 15 filled forms (.aprf)
   - 3 templates (.aprt)
   - Groups by template automatically

Day 2: Processing Forms
1. Staff opens Form Management view
2. Sees "Employment Application (12 submissions)"
3. Filters: Created "Last 7 days"
4. Opens form "john-doe-employment.aprf"
5. Reviews submission
6. Returns to management view
7. Right-clicks form → "Add Tag" → "verified" (green)
8. Changes status: "New" → "Processed"

Week 1: Reporting
1. Manager clicks "Generate Report"
2. Selects date range: "Last Week"
3. Report shows:
   - 15 submissions received
   - 8 processed
   - 5 in review
   - 2 new
   - Most used: "Employment Application"
4. Exports to PDF for council meeting

Month 1: Analysis
1. Reviews trends over time
2. Identifies peak submission days
3. Tags problematic submissions "needs-follow-up"
4. Assigns forms to specific staff members
```

## Appendix B: Example Queries

```csharp
// All forms from last week
var lastWeek = await formService.GetFormsAsync(new FormQuery(
    CreatedAfter: DateTime.Now.AddDays(-7)
));

// Processed employment applications
var processed = await formService.GetFormsAsync(new FormQuery(
    TemplateId: "employment-application-v1",
    Status: ProcessingStatus.Processed
));

// Forms needing attention (tagged "urgent")
var urgent = await formService.GetFormsAsync(new FormQuery(
    TagName: "urgent",
    Status: ProcessingStatus.InReview
));

// My assigned forms
var myForms = await formService.GetFormsAsync(new FormQuery(
    AssignedTo: Environment.UserName,
    Status: ProcessingStatus.InReview
));
```

## Appendix C: CSV Export Format

```csv
FormId,FileName,Template,Title,CreatedDate,ModifiedDate,Status,AssignedTo,Tags,FilledBy,FilledDate
"forms/john-doe.aprf","john-doe.aprf","Employment Application","Employment Application for John Doe","2025-01-15","2025-01-15","Processed","alice","verified,processed","jdoe","2025-01-15T10:30:00Z"
```

---

## Document History

| Version | Date       | Author | Changes              |
|---------|------------|--------|----------------------|
| 1.0     | 2025-11-18 | Claude | Initial specification |

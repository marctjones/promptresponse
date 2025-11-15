# PromptResponse Architecture

This document describes the system architecture, design decisions, and technical structure of PromptResponse.

## System Overview

```
┌─────────────────────────────────────────────────────────┐
│                   PromptResponse                        │
│                                                         │
│  ┌───────────────┐              ┌──────────────────┐   │
│  │   Desktop UI  │              │   Mobile UI      │   │
│  │  (AvaloniaUI) │              │  (Future)        │   │
│  └───────┬───────┘              └────────┬─────────┘   │
│          │                               │             │
│          └───────────┬───────────────────┘             │
│                      │                                 │
│          ┌───────────▼───────────┐                     │
│          │  PromptResponse.Core  │                     │
│          │                       │                     │
│          │  ├── Models           │                     │
│          │  ├── Serialization    │                     │
│          │  ├── Validation       │                     │
│          │  └── Logging          │                     │
│          └───────────┬───────────┘                     │
│                      │                                 │
│          ┌───────────▼───────────┐                     │
│          │   File System (.apr)  │                     │
│          └───────────────────────┘                     │
└─────────────────────────────────────────────────────────┘
```

## Architecture Principles

### 1. Separation of Concerns

- **Core Library**: Platform-agnostic business logic
- **UI Layer**: Platform-specific presentation
- **Clear boundaries**: No UI code in Core, no business logic in UI

### 2. MVVM Pattern (UI Layer)

```
View (XAML) ←→ ViewModel ←→ Model (Core)
```

- **Views**: XAML-based UI components
- **ViewModels**: UI state and commands
- **Models**: Domain objects from Core library

### 3. Dependency Inversion

- Core library has no dependencies on UI
- UI depends on Core through interfaces
- Services injected via dependency injection

## Project Structure

### Core Library (`PromptResponse.Core`)

**Purpose**: Platform-agnostic form processing logic

```
PromptResponse.Core/
├── Models/
│   ├── AprDocument.cs          # Root document
│   ├── DocumentType.cs         # Enum: Template/FilledForm
│   ├── Metadata.cs             # Document metadata
│   ├── Section.cs              # Section grouping
│   ├── Subsection.cs           # Nested grouping
│   ├── Prompt.cs               # Individual prompt
│   ├── PromptHints.cs          # Type hints, suggestions
│   └── ResponseMetadata.cs     # Response tracking
├── Serialization/
│   ├── IAprSerializer.cs       # Serialization interface
│   ├── AprJsonSerializer.cs    # JSON implementation
│   └── SerializationException.cs
├── Validation/
│   ├── IValidator.cs
│   ├── DocumentValidator.cs    # Validate structure
│   └── DataTypeValidator.cs    # Validate type hints
└── Logging/
    └── CoreLogger.cs           # Logging utilities
```

**Dependencies**:
- `System.Text.Json` (built-in)
- `Microsoft.Extensions.Logging.Abstractions`

### Desktop Application (`PromptResponse.Desktop`)

**Purpose**: Cross-platform desktop UI (Linux, Windows, macOS)

```
PromptResponse.Desktop/
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── TemplateEditorViewModel.cs
│   ├── FormFillingViewModel.cs
│   ├── SectionViewModel.cs
│   ├── PromptViewModel.cs
│   └── ViewModelBase.cs
├── Views/
│   ├── MainWindow.axaml
│   ├── TemplateEditorView.axaml
│   ├── FormFillingView.axaml
│   ├── SectionView.axaml
│   └── PromptView.axaml
├── Services/
│   ├── IFileService.cs
│   ├── FileService.cs
│   ├── IModeDetectionService.cs
│   ├── ModeDetectionService.cs
│   └── IDialogService.cs
├── Converters/
│   └── DataTypeToWidgetConverter.cs
├── App.axaml.cs
└── Program.cs
```

**Dependencies**:
- `Avalonia` (MIT License)
- `Avalonia.Desktop` (MIT License)
- `PromptResponse.Core` (local)

### Test Projects

```
PromptResponse.Core.Tests/
├── Models/
│   ├── AprDocumentTests.cs
│   ├── SectionTests.cs
│   ├── PromptTests.cs
│   └── ...
├── Serialization/
│   ├── AprJsonSerializerTests.cs
│   └── DeserializationTests.cs
├── Validation/
│   └── DocumentValidatorTests.cs
└── Integration/
    └── EndToEndTests.cs

PromptResponse.Desktop.Tests/
├── ViewModels/
│   ├── TemplateEditorViewModelTests.cs
│   └── FormFillingViewModelTests.cs
└── Services/
    └── FileServiceTests.cs
```

## Core Components

### 1. Data Models

#### AprDocument

```csharp
/// <summary>
/// Root document representing an APR file.
/// </summary>
public class AprDocument
{
    public string Version { get; set; } = "1.0";
    public DocumentType DocumentType { get; set; }
    public Metadata Metadata { get; set; } = new();
    public List<Section> Sections { get; set; } = new();
}
```

#### Section

```csharp
/// <summary>
/// Top-level grouping of prompts in a document.
/// </summary>
public class Section
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Subsection> Subsections { get; set; } = new();
    public List<Prompt> Prompts { get; set; } = new();
}
```

#### Prompt

```csharp
/// <summary>
/// Individual question/field in the form.
/// </summary>
public class Prompt
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public PromptHints Hints { get; set; } = new();
    public ResponseMetadata ResponseMetadata { get; set; } = new();
}
```

### 2. Serialization

**Design**: Strategy pattern for multiple formats

```csharp
public interface IAprSerializer
{
    /// <summary>
    /// Serializes an APR document to a string.
    /// </summary>
    string Serialize(AprDocument document);

    /// <summary>
    /// Deserializes a string to an APR document.
    /// </summary>
    AprDocument Deserialize(string content);

    /// <summary>
    /// Asynchronously deserializes a stream to an APR document.
    /// </summary>
    Task<AprDocument> DeserializeAsync(Stream stream);
}
```

**Implementation**: JSON using `System.Text.Json`

### 3. Validation

**Two-tier validation**:

1. **Structural**: Required fields, valid structure
2. **Semantic**: Type hints, suggested values format

```csharp
public interface IValidator<T>
{
    ValidationResult Validate(T item);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
}
```

### 4. File Service

**Responsibilities**:
- Load/save APR files
- Detect document type
- Handle file I/O errors

```csharp
public interface IFileService
{
    Task<AprDocument> LoadAsync(string filePath);
    Task SaveAsync(string filePath, AprDocument document);
    DocumentType DetectDocumentType(string filePath);
}
```

## UI Architecture

### MVVM Implementation

#### ViewModels

**Base ViewModel**:
```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

**Example ViewModel**:
```csharp
public class PromptViewModel : ViewModelBase
{
    private string _response = string.Empty;

    public Prompt Model { get; }

    public string Response
    {
        get => _response;
        set
        {
            if (_response != value)
            {
                _response = value;
                Model.Response = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand ClearCommand { get; }
}
```

#### Views

**XAML Binding**:
```xml
<TextBox Text="{Binding Response, Mode=TwoWay}"
         Watermark="{Binding Hints.Placeholder}"
         AutoCompleteSource="{Binding Hints.SuggestedValues}" />
```

### Navigation Flow

```
App Start
    ↓
MainWindow
    ├─→ New Template → TemplateEditorView
    ├─→ Open File → ModeDetectionService
    │       ├─→ Template
    │       │   ├─→ Edit Template → TemplateEditorView
    │       │   └─→ Fill Form → FormFillingView
    │       └─→ FilledForm → FormFillingView
    └─→ Recent Files → (above flows)
```

## Mode Detection Logic

```csharp
public class ModeDetectionService : IModeDetectionService
{
    public async Task<DocumentMode> DetermineMode(string filePath)
    {
        var document = await _fileService.LoadAsync(filePath);

        if (document.DocumentType == DocumentType.FilledForm)
        {
            // Go directly to filling mode
            return DocumentMode.Filling;
        }

        // Template - ask user
        var choice = await _dialogService.ShowChoice(
            "Open Template",
            "Would you like to edit the template or fill it out?",
            new[] { "Fill Out Form", "Edit Template" }
        );

        return choice == 0 ? DocumentMode.Filling : DocumentMode.Editing;
    }
}
```

## Data Flow

### Loading a Document

```
User clicks Open
    ↓
FileService.LoadAsync(path)
    ↓
AprJsonSerializer.DeserializeAsync(stream)
    ↓
DocumentValidator.Validate(document)
    ↓
ModeDetectionService.DetermineMode(path)
    ↓
Navigate to appropriate View
    ↓
ViewModel wraps Models
    ↓
View binds to ViewModel
```

### Saving a Document

```
User clicks Save
    ↓
ViewModel updates Models
    ↓
DocumentValidator.Validate(document)
    ↓
AprJsonSerializer.Serialize(document)
    ↓
FileService.SaveAsync(path, document)
    ↓
Update UI with success/error
```

### Editing a Response

```
User types in TextBox
    ↓
Two-way binding updates ViewModel.Response
    ↓
ViewModel updates Prompt.Response
    ↓
ResponseMetadata.LastModified = DateTime.UtcNow
    ↓
PropertyChanged event raised
    ↓
UI updated (if needed)
```

## Dependency Injection

```csharp
// Program.cs
var services = new ServiceCollection();

// Core services
services.AddSingleton<IAprSerializer, AprJsonSerializer>();
services.AddSingleton<IValidator<AprDocument>, DocumentValidator>();

// Desktop services
services.AddSingleton<IFileService, FileService>();
services.AddSingleton<IModeDetectionService, ModeDetectionService>();
services.AddSingleton<IDialogService, DialogService>();

// ViewModels
services.AddTransient<MainWindowViewModel>();
services.AddTransient<TemplateEditorViewModel>();
services.AddTransient<FormFillingViewModel>();

// Logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
```

## Error Handling

### Layered Approach

1. **Core Layer**: Throw specific exceptions
2. **Service Layer**: Catch, log, and re-throw or wrap
3. **ViewModel Layer**: Catch, show user-friendly message
4. **View Layer**: Display error UI

### Exception Types

```csharp
// Core
public class SerializationException : Exception { }
public class ValidationException : Exception { }

// Services
public class FileAccessException : Exception { }
public class UnsupportedVersionException : Exception { }
```

### Logging

```csharp
try
{
    _logger.LogInformation("Loading document from {FilePath}", filePath);
    var document = await _serializer.DeserializeAsync(stream);
    _logger.LogDebug("Document loaded: {DocumentType}", document.DocumentType);
    return document;
}
catch (JsonException ex)
{
    _logger.LogError(ex, "Failed to deserialize APR document");
    throw new SerializationException("Invalid APR file format", ex);
}
```

## Performance Considerations

### Large Forms

- **Lazy loading**: Load sections on-demand
- **Virtualization**: Use virtual scrolling for 1000+ prompts
- **Async I/O**: All file operations async
- **Streaming**: Stream large JSON files

### Memory Management

- **Dispose patterns**: Implement IDisposable for file handles
- **Weak references**: For cached data
- **Object pooling**: For frequently created objects

## Security

### Input Validation

- Validate all file inputs
- Sanitize responses for display
- Prevent path traversal in file operations

### No Code Execution

- APR files are pure data
- No scripting or executable code
- Safe to open untrusted files

## Testing Strategy

### Unit Tests

- Test each class in isolation
- Mock dependencies
- Cover edge cases and error conditions

### Integration Tests

- Test component interactions
- Test serialization round-trips
- Test file I/O operations

### UI Tests

- Test ViewModel logic
- Test data binding
- Manual testing for UI/UX

## Form Submission Architecture

### Hybrid Submission Model

PromptResponse supports multiple form submission workflows:

#### 1. Manual Workflow (Default)
```
User fills form → Save locally → Manual send (email, file transfer)
```
- No infrastructure required
- Maximum privacy
- Simple workflow
- Works offline

#### 2. Direct S3 Submission (Future)
```
Template with S3 config → User fills form → Direct POST to S3 → No server needed
```

**Architecture:**
```
┌──────────────────┐
│  Template Author │
│  (Has AWS creds) │
└────────┬─────────┘
         │
         │ 1. Generate pre-signed POST policy
         │    (AWSAccessKeyId, policy, signature)
         │
         ▼
┌─────────────────────────────────┐
│  APR Template with              │
│  submissionConfig in metadata   │
└────────┬────────────────────────┘
         │
         │ 2. Distribute template
         │
         ▼
┌─────────────────┐
│  User fills out │
│  form           │
└────────┬────────┘
         │
         │ 3. Click "Submit"
         │
         ▼
┌─────────────────────────────────┐
│  PromptResponse Desktop/CLI     │
│  reads submissionConfig         │
└────────┬────────────────────────┘
         │
         │ 4. HTTPS POST directly to S3
         │    with policy + signature
         │
         ▼
┌─────────────────┐
│  S3 validates   │
│  signature &    │
│  accepts file   │
└─────────────────┘
```

**Benefits:**
- No server-side code required
- No serverless functions needed
- Direct client-to-S3 upload
- S3 handles validation via policy
- Can combine with APR digital signatures
- Template signature proves authenticity
- S3 policy controls upload authorization

**Security Model:**
- Pre-signed POST policy includes expiration (typically 7 days max)
- Policy restricts file size, content type, key prefix
- Access key ID visible in template (not secret key)
- Signature computed from secret key but doesn't expose it
- Anyone with template can upload (until expiration)
- S3 bucket can have additional policies (encryption, lifecycle)

**Implementation Considerations:**
- Check policy expiration before submitting
- Handle CORS requirements (S3 bucket configuration)
- Provide fallback to manual save if submission fails
- Show policy expiration warning in UI
- Optional: Support policy refresh from URL

### 3. Webhook Submission (Future)
```
Template with webhook URL → User fills form → POST to webhook
```
- Simple HTTP POST to custom endpoint
- Template includes webhook URL
- Implementation handles auth, validation, storage

## Future Considerations

### Mobile Support

- Share Core library
- Platform-specific UI (Avalonia Mobile or MAUI)
- Mobile-optimized input widgets

### Plugin System

- Custom validators
- Custom data types
- Export formats
- Custom submission handlers

### Collaboration

- Conflict resolution for concurrent edits
- Version control integration
- Cloud sync

## Design Decisions

### Why JSON?

- Human-readable
- Wide tool support
- Easy to parse
- Interoperable

### Why Plain Text Responses?

- Maximum flexibility
- No data loss from type coercion
- Portable across systems
- Accessibility

### Why No Layout Info?

- Separation of content and presentation
- Dynamic UI adaptation
- Accessibility
- Portability

### Why Two Document Types?

- Clear intent (template vs filled)
- Different workflows
- Prevent accidental template modification

## References

- [FILE_FORMAT.md](FILE_FORMAT.md) - APR format specification
- [DEVELOPMENT.md](DEVELOPMENT.md) - Development guidelines
- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [.NET Documentation](https://learn.microsoft.com/en-us/dotnet/)

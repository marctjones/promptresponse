# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

PromptResponse is a cross-platform form creation and filling application using a flexible JSON-based format (.apr files). It replaces traditional document-based forms (Word, PDF) with a semantic, portable, text-based format that breaks free from the page metaphor.

**Technology Stack**: .NET 8.0, C# 12, AvaloniaUI 11, xUnit

## Build and Test Commands

### Building

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/PromptResponse.Core
dotnet build src/PromptResponse.Desktop
dotnet build src/PromptResponse.Cli
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/PromptResponse.Core.Tests

# Run specific test by name filter
dotnet test --filter "FullyQualifiedName~PromptTests.Prompt_ShouldInitializeWithEmptyResponse"

# Run with verbose output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Running Applications

**Using the Launcher Scripts (Recommended):**

```bash
# Linux/macOS
./run.sh              # Launch GUI (default)
./run.sh --demo       # Run all CLI demos interactively
./run.sh --validate   # Validate all example APR files
./run.sh --info       # Show info about example files
./run.sh --new        # Create a new template interactively
./run.sh --test       # Run all tests
./run.sh --help       # Show CLI help
./run.sh --usage      # Show launcher options

# Windows PowerShell
.\run.ps1             # Launch GUI (default)
.\run.ps1 demo        # Run all CLI demos interactively
.\run.ps1 validate    # Validate all example APR files
.\run.ps1 info        # Show info about example files
.\run.ps1 new         # Create a new template interactively
.\run.ps1 test        # Run all tests
.\run.ps1 help        # Show CLI help
.\run.ps1 usage       # Show launcher options
```

**Direct Commands:**

```bash
# Run CLI tool
dotnet run --project src/PromptResponse.Cli -- help
dotnet run --project src/PromptResponse.Cli -- validate examples/employment-application.apr
dotnet run --project src/PromptResponse.Cli -- info examples/simple-contact-form.apr
dotnet run --project src/PromptResponse.Cli -- new my-form.apr

# Run Desktop application
dotnet run --project src/PromptResponse.Desktop
```

## Architecture

### Three-Layer Architecture

1. **Core Library** (`PromptResponse.Core`): Platform-agnostic business logic
   - `Models/`: Domain models (AprDocument, Section, Subsection, Prompt, etc.)
   - `Serialization/`: JSON serialization using System.Text.Json
   - `Validation/`: Structural and semantic validation

2. **Desktop UI** (`PromptResponse.Desktop`): Cross-platform AvaloniaUI application
   - `ViewModels/`: MVVM view models
   - `Views/`: XAML views
   - `Services/`: File I/O and mode detection services

3. **CLI Tool** (`PromptResponse.Cli`): Command-line interface for APR file management
   - Commands: validate, info, new, help, version

### Key Design Principles

- **Separation of Concerns**: Core has no UI dependencies; UI depends on Core
- **MVVM Pattern**: Views (XAML) ↔ ViewModels ↔ Models (Core)
- **Dependency Injection**: Services injected via DI container
- **Two Document Types**:
  - `template`: Blank form definition with empty responses
  - `filledForm`: Completed form with user data
- **Type Hints, Not Restrictions**: Expected data types are suggestions only; all responses are stored as strings

### APR File Format

- **Format**: JSON-based, UTF-8 encoded
- **Structure**: 3-level hierarchy: Document → Sections → Subsections → Prompts
- **Data Model**:
  - All responses are plain text strings
  - Type hints (`expectedDataType`) guide UI but are never enforced
  - IDs must be unique within scope
  - Metadata tracks creation, modification, authorship

**Important**: Responses are ALWAYS strings, never typed values. A numeric response is stored as `"42"`, not `42`.

### File Extensions

APR files use three file extensions to distinguish templates from filled forms:

- **`.aprt`** - APR Template (blank form intended to be filled out)
- **`.aprf`** - APR Filled Form (completed form with user data)
- **`.apr`** - Generic APR file (backward compatibility, auto-detects from content)

**Extension Override Behavior:**
- File extension **takes precedence** over the `documentType` field in the JSON
- Renaming a filled form to `.aprt` treats it as a template
- Renaming a template to `.aprf` treats it as a filled form
- `.apr` extension uses the `documentType` field from file content

**Workflow:**
1. Templates are created/saved as `.aprt` files
2. When opening `.aprt` for filling, app converts to FilledForm and forces "Save As"
3. Filled forms are saved as `.aprf` files
4. Opening `.aprf` allows direct editing and saving to same file

**CLI Commands:**
```bash
# Create new template (defaults to .aprt)
dotnet run --project src/PromptResponse.Cli -- new my-template

# All commands work with any extension
dotnet run --project src/PromptResponse.Cli -- validate form.aprt
dotnet run --project src/PromptResponse.Cli -- info form.aprf
```

## Development Workflow

### Test-Driven Development (TDD)

**This project follows strict TDD practices with accessibility as a core requirement:**

1. Write failing test FIRST (including accessibility test if UI change)
2. Run test (verify it fails)
3. Implement minimum code to pass
4. Run test (verify it passes)
5. **Run accessibility tests** (verify no regressions)
6. Refactor if needed
7. Commit

**Requirements:**
- Unit tests for each component
- Integration tests for component interactions
- **Accessibility tests for all UI changes**
- >80% code coverage target
- **All tests must pass before committing** (including accessibility tests)
- **Never commit broken code or inaccessible UI**

**Accessibility Testing Requirements:**
- All new APR files must pass `AprAccessibilityValidationTests`
- All UI changes must include `AutomationProperties.Name` and `AutomationProperties.HelpText`
- Run `dotnet test tests/PromptResponse.AccessibilityTests` before committing
- Prefer automated/static tests over manual screen reader testing
- See `tests/PromptResponse.AccessibilityTests/README.md` for guidelines

### Code Quality Standards

**Documentation:**
- XML documentation comments (`///`) for all public classes and methods
- Document WHY, not just WHAT
- Inline comments for complex logic

**C# Conventions:**
- Follow Microsoft C# Coding Conventions
- Use nullable reference types
- PascalCase for public members
- camelCase with underscore prefix for private fields (`_fieldName`)
- Use modern C# features (pattern matching, records, etc.)

**Accessibility (CRITICAL - Non-Negotiable):**
- **ALL interactive UI elements MUST have `AutomationProperties.Name`**
- **Form fields MUST have `AutomationProperties.HelpText` when help is available**
- **APR documents MUST have unique, descriptive labels for all prompts**
- **Section and subsection titles are REQUIRED (not optional)**
- **Test accessibility with automated tests BEFORE committing**
- **Never use placeholder text as the only label**
- **Never create duplicate field labels**
- Accessibility is NOT a "nice to have" - it's a REQUIREMENT
- Target: WCAG 2.1 Level AA compliance minimum

**Logging:**
- Use structured logging with `Microsoft.Extensions.Logging`
- Log levels: Debug → Information → Warning → Error → Critical
- Example: `_logger.LogInformation("Loading document from {FilePath}", filePath)`

### Git Workflow

**Main branch**: `main`

**Commit message format:**
```
<type>: <description>

[optional body]
```

**Types:** `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `chore`

**Examples:**
- `feat: add Section model with subsection support`
- `fix: correct JSON deserialization for null values`
- `test: add integration tests for document serialization`

## Core Components

### Models Hierarchy

```
AprDocument
  ├── Version (string, always "1.0")
  ├── DocumentType (enum: Template | FilledForm)
  ├── Metadata (creation, modification, authorship)
  └── Sections[] (list of Section)
      ├── Subsections[] (optional nested grouping)
      │   └── Prompts[]
      └── Prompts[] (can exist at both section and subsection level)
```

### Serialization

- Interface: `IAprSerializer`
- Implementation: `AprJsonSerializer` using `System.Text.Json`
- Methods: `Serialize()`, `Deserialize()`, `DeserializeAsync(Stream)`
- All file I/O should be async

### Validation

- Interface: `IValidator<T>`
- Two-tier validation:
  1. **Structural**: Required fields, valid hierarchy, unique IDs
  2. **Semantic**: Type hints format, suggested values format
- Returns `ValidationResult` with errors list

### Services

**IFileService**: Load/save APR files, handle I/O errors
**IModeDetectionService**: Determine whether to edit template or fill form based on document type
**IDialogService**: Show dialogs and prompts to user

## Common Patterns

### Working with Prompts

```csharp
// Prompts store responses as strings
var prompt = new Prompt
{
    Id = "prompt_001",
    Label = "Age",
    Response = "42",  // Always string, even for numbers
    Hints = new PromptHints
    {
        ExpectedDataType = "number"  // Hint only, not enforced
    }
};
```

### Loading/Saving Documents

```csharp
// Loading
var document = await _serializer.DeserializeAsync(stream);
var validationResult = _validator.Validate(document);

// Saving
var json = _serializer.Serialize(document);
await _fileService.SaveAsync(filePath, document);
```

### ViewModel Pattern

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
                Model.ResponseMetadata.LastModified = DateTime.UtcNow;
                OnPropertyChanged();
            }
        }
    }
}
```

## Dependencies

**Core Library:**
- `System.Text.Json` (built-in)
- `Microsoft.Extensions.Logging.Abstractions`

**Desktop Application:**
- `Avalonia` 11.0+ (MIT License)
- `PromptResponse.Core` (local)

**CLI Tool:**
- `PromptResponse.Core` (local)

**Test Projects:**
- `xUnit` 2.6+
- `FluentAssertions` 6.12+
- `coverlet.collector` 6.0+ (for coverage)

**Principle**: Minimize external dependencies; prefer pure .NET libraries; use only permissive licenses (MIT, Apache 2.0, BSD)

## Important Constraints

1. **No Layout Information**: APR format contains no styling or layout data (separation of content and presentation)
2. **No Code Execution**: APR files are pure data, no scripting or executable code (safe to open untrusted files)
3. **String-Only Responses**: All responses stored as strings, regardless of expected type
4. **3-Level Max Depth**: Section → Subsection → Prompt (subsections cannot nest)
5. **Unique IDs**: All IDs must be unique within their scope
6. **Stable IDs**: IDs should remain stable across versions (don't change when reordering)

## Performance Considerations

- Use async/await for all file I/O operations
- Stream large JSON files when possible
- Target: Forms with 1000+ prompts should load in <1 second
- Consider lazy loading sections for very large forms
- Use virtual scrolling for long lists in UI

## Accessibility

PromptResponse is designed with **accessibility as a core requirement**, targeting WCAG 2.1 Level AA compliance.

**Key Features:**
- Full keyboard navigation with logical tab order
- Screen reader support via AutomationProperties
- Theme switching (Light/Dark/System Default)
- High contrast support through system integration
- Clear focus indicators and visual hierarchy
- Accessible form structure with semantic names

**Testing:**
- Linux: Orca screen reader, Accerciser
- Windows: Narrator, NVDA, Accessibility Insights
- macOS: VoiceOver, Accessibility Inspector

See `ACCESSIBILITY.md` for comprehensive testing guide and accessibility features.

## Documentation References

- `ACCESSIBILITY.md`: Comprehensive accessibility guide and testing tools
- `DEBUGGING.md`: Debug logging guide for tracking execution flow
- `LAUNCHER.md`: Running the application with run.sh/run.ps1
- `docs/ARCHITECTURE.md`: Detailed system architecture and design decisions
- `docs/FILE_FORMAT.md`: Complete APR format specification
- `docs/DEVELOPMENT.md`: Full development guidelines and TDD workflow
- `docs/USAGE.md`: User-facing usage instructions
- `src/PromptResponse.Cli/README.md`: CLI tool documentation

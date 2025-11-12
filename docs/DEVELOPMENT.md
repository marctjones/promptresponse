# Development Guide

This document outlines the development practices and guidelines for contributing to PromptResponse.

## Core Development Principles

### 1. Test-Driven Development (TDD)

**Always write tests BEFORE implementation:**

```bash
# Workflow for every feature
1. Write failing test
2. Run test (verify it fails)
3. Implement minimum code to pass
4. Run test (verify it passes)
5. Refactor if needed
6. Run test again
7. Commit
```

**Testing Requirements:**
- Unit tests for each component
- Integration tests for component interactions
- Run tests after every change
- Maintain >80% code coverage
- All tests must pass before committing

### 2. Incremental Development

- Build from foundation up
- Each component must be tested before moving to next
- Commit frequently (every working feature)
- **Never commit broken code**
- Each commit should be deployable

### 3. Code Quality Standards

**Documentation:**
- XML documentation comments for all public classes and methods
- Inline comments for complex logic
- Document WHY, not just WHAT

**C# Coding Standards:**
- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types
- Use modern C# features (pattern matching, records, etc.)
- PascalCase for public members
- camelCase for private fields with underscore prefix (_fieldName)

**Example:**
```csharp
/// <summary>
/// Represents a prompt (question) in an APR document.
/// </summary>
/// <remarks>
/// Prompts always store responses as strings, but may include type hints
/// to guide the UI in providing appropriate input widgets.
/// </remarks>
public class Prompt
{
    private string _response = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for this prompt.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-visible label for this prompt.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response value.
    /// Always stored as a string regardless of expected data type.
    /// </summary>
    public string Response
    {
        get => _response;
        set
        {
            _response = value ?? string.Empty;
            ResponseMetadata.LastModified = DateTime.UtcNow;
        }
    }
}
```

### 4. Logging & Debugging

**Structured Logging:**
```csharp
using Microsoft.Extensions.Logging;

// Use built-in .NET logging
_logger.LogDebug("Loading APR document from {FilePath}", filePath);
_logger.LogInformation("Document loaded successfully: {DocumentType}", doc.DocumentType);
_logger.LogWarning("Missing templateId in filled form document");
_logger.LogError(ex, "Failed to deserialize APR document");
_logger.LogCritical("Unrecoverable error in serialization engine");
```

**Log Levels:**
- **Debug**: Detailed debugging information
- **Information**: General informational messages
- **Warning**: Warning messages for recoverable issues
- **Error**: Error messages for failures
- **Critical**: Critical failures requiring immediate attention

### 5. Dependency Management

**Principles:**
- Prefer pure .NET libraries (no native dependencies)
- Minimize external dependencies
- Document WHY each dependency is needed
- Use only permissive licenses (MIT, Apache 2.0, BSD)

**Current Dependencies:**
```xml
<!-- Core Library -->
<ItemGroup>
  <PackageReference Include="System.Text.Json" Version="8.0.0" />
  <!-- Built-in, no external dep -->
</ItemGroup>

<!-- Desktop Application -->
<ItemGroup>
  <PackageReference Include="Avalonia" Version="11.0.0" />
  <!-- Cross-platform UI: MIT License -->
  <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  <!-- Logging: MIT License -->
</ItemGroup>

<!-- Test Projects -->
<ItemGroup>
  <PackageReference Include="xUnit" Version="2.6.0" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
  <PackageReference Include="coverlet.collector" Version="6.0.0" />
</ItemGroup>
```

## Development Setup

### Prerequisites

```bash
# Install .NET 8.0 SDK
# Linux:
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Verify installation
dotnet --version  # Should show 8.0.x
```

### Initial Setup

```bash
# Clone repository
git clone https://github.com/yourusername/promptresponse.git
cd promptresponse

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Project Structure

```
promptresponse/
├── src/
│   ├── PromptResponse.Core/          # Platform-agnostic core library
│   │   ├── Models/                   # Domain models
│   │   ├── Serialization/            # JSON serialization
│   │   ├── Validation/               # Optional validation
│   │   └── Logging/                  # Logging utilities
│   ├── PromptResponse.Desktop/       # Avalonia desktop app
│   │   ├── ViewModels/               # MVVM view models
│   │   ├── Views/                    # XAML views
│   │   ├── Services/                 # File I/O, etc.
│   │   └── Program.cs
│   └── PromptResponse.Mobile/        # Future: Mobile app
├── tests/
│   ├── PromptResponse.Core.Tests/
│   │   ├── Models/                   # Model unit tests
│   │   ├── Serialization/            # Serialization tests
│   │   └── Integration/              # Integration tests
│   └── PromptResponse.Desktop.Tests/
├── docs/                             # Documentation
├── examples/                         # Example .apr files
└── tools/                            # Build scripts, etc.
```

## TDD Workflow Example

### Example: Adding Prompt Model

**Step 1: Write Test First**
```csharp
// tests/PromptResponse.Core.Tests/Models/PromptTests.cs
public class PromptTests
{
    [Fact]
    public void Prompt_ShouldInitializeWithEmptyResponse()
    {
        // Arrange & Act
        var prompt = new Prompt();

        // Assert
        prompt.Response.Should().BeEmpty();
    }

    [Fact]
    public void SetResponse_ShouldUpdateLastModified()
    {
        // Arrange
        var prompt = new Prompt();
        var beforeTime = DateTime.UtcNow;

        // Act
        prompt.Response = "Test response";

        // Assert
        prompt.Response.Should().Be("Test response");
        prompt.ResponseMetadata.LastModified.Should().BeAfter(beforeTime);
    }
}
```

**Step 2: Run Test (Should Fail)**
```bash
dotnet test
# Expected: Compilation error or test failure
```

**Step 3: Implement Minimum Code**
```csharp
// src/PromptResponse.Core/Models/Prompt.cs
public class Prompt
{
    private string _response = string.Empty;

    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Response
    {
        get => _response;
        set
        {
            _response = value ?? string.Empty;
            ResponseMetadata.LastModified = DateTime.UtcNow;
        }
    }
    public ResponseMetadata ResponseMetadata { get; set; } = new();
}
```

**Step 4: Run Test (Should Pass)**
```bash
dotnet test
# Expected: All tests pass
```

**Step 5: Commit**
```bash
git add .
git commit -m "feat: add Prompt model with response tracking"
```

## Git Workflow

### Commit Message Convention

```
<type>: <description>

[optional body]

[optional footer]
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `chore`: Maintenance tasks

**Examples:**
```
feat: add Section model with subsection support
fix: correct JSON deserialization for null values
docs: update FILE_FORMAT.md with subsection examples
test: add integration tests for document serialization
```

### Branch Strategy

```bash
# Main branch: claude/csharp-cross-platform-app-011CV4c6LNDr1EMwJCyTY83C
# All development happens on this branch

# Commit frequently
git add .
git commit -m "feat: implement feature"

# Push when ready
git push -u origin claude/csharp-cross-platform-app-011CV4c6LNDr1EMwJCyTY83C
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/PromptResponse.Core.Tests

# Run with verbose output
dotnet test --verbosity detailed

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test
dotnet test --filter "FullyQualifiedName~PromptTests.Prompt_ShouldInitializeWithEmptyResponse"
```

## Code Coverage

Maintain >80% code coverage:

```bash
# Generate coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# View coverage summary
cat coverage.opencover.xml | grep "sequenceCoverage"
```

## Debugging

### Enable Debug Logging

```csharp
// In Program.cs or App.axaml.cs
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

### Debug in VS Code

```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/PromptResponse.Desktop/bin/Debug/net8.0/PromptResponse.Desktop.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "console": "internalConsole",
      "stopAtEntry": false
    }
  ]
}
```

## Performance Guidelines

- Avoid premature optimization
- Profile before optimizing
- Target: Forms with 1000+ prompts should load <1s
- Use async/await for file I/O
- Stream large JSON files when possible

## Security Considerations

- Validate all file inputs
- Sanitize user inputs for display
- No execution of code from .apr files
- No network access (local files only for MVP)

## Questions?

Open an issue or discussion on GitHub.

# Development Guide

This document outlines the development practices and guidelines for contributing to PromptResponse.

## Core Development Principles

### 1. Accessibility-First Development (CRITICAL)

**Accessibility is NOT optional - it's a core requirement equal to functionality and testing.**

PromptResponse targets **WCAG 2.1 Level AA compliance** minimum. Every feature, every UI element, every APR file must be accessible to users with disabilities.

**Why Accessibility is Critical:**
- 15% of the world's population has some form of disability
- Legal requirement in many jurisdictions (ADA, Section 508, EAA)
- Screen reader users, keyboard-only users, low vision users ALL must be able to use PromptResponse
- Inaccessible UI is **broken UI** - treat accessibility bugs as critical bugs

**Accessibility-First Workflow:**

```bash
# Every feature must include accessibility from the start
1. Design feature with accessibility in mind
2. Write accessibility test FIRST (along with functional test)
3. Implement feature with accessibility properties
4. Run functional tests
5. Run accessibility tests
6. Verify with automated tools
7. Commit only when ALL tests pass (including accessibility)
```

**Accessibility is a BLOCKER:**
- ❌ Cannot merge code with failing accessibility tests
- ❌ Cannot ship features missing AutomationProperties
- ❌ Cannot create APR files with missing/duplicate labels
- ✅ Must run `dotnet test tests/PromptResponse.AccessibilityTests` before every commit

**See:** `ACCESSIBILITY.md` and `tests/PromptResponse.AccessibilityTests/README.md` for complete guidelines.

### 2. Test-Driven Development (TDD)

**Always write tests BEFORE implementation (including accessibility tests):**

```bash
# Workflow for every feature
1. Write failing test (functional + accessibility if UI)
2. Run test (verify it fails)
3. Implement minimum code to pass
4. Run test (verify it passes)
5. Run accessibility tests (verify no regressions)
6. Refactor if needed
7. Run all tests again
8. Commit
```

**Testing Requirements:**
- Unit tests for each component
- Integration tests for component interactions
- **Accessibility tests for all UI changes and APR files**
- Run tests after every change
- Maintain >80% code coverage
- **All tests must pass before committing (including accessibility)**

## Accessibility Testing

**Automated and static accessibility testing is REQUIRED for all changes.**

### Automated Accessibility Tests

PromptResponse uses **automated, headless accessibility testing** that runs in CI/CD without requiring manual screen reader testing:

```bash
# Run accessibility tests (MUST pass before commit)
dotnet test tests/PromptResponse.AccessibilityTests

# Expected output:
# ✅ 8 passed, ⏭️ 2 skipped (integration tests)
```

### Types of Accessibility Tests

**1. Static APR File Validation (Primary - Always Use)**

These tests analyze APR files without running the application:

- ✅ **Fast**: < 100ms per test
- ✅ **No dependencies**: No screen reader or running app needed
- ✅ **CI/CD friendly**: Runs anywhere
- ✅ **Deterministic**: Same results every time

**What they test:**
- All prompts have unique, descriptive labels
- All sections have titles
- Help text is meaningful
- No duplicate labels (confusing for screen reader users)
- Labels aren't technical IDs (user-friendly)

**Example:**
```csharp
[Fact]
public async Task AprDocument_Prompts_ShouldHave_UniqueLabels()
{
    var document = LoadTestDocument();
    var labels = GetAllPrompts(document).Select(p => p.Label);

    var duplicates = labels.GroupBy(l => l)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key);

    duplicates.Should().BeEmpty(
        "duplicate labels confuse screen reader users");
}
```

**2. Runtime Accessibility Tree Inspection (Future)**

These tests launch the app and inspect what assistive technologies see:

- ⚠️ **Slower**: 2-5 seconds (app launch)
- ⚠️ **Platform-specific**: Requires AT-SPI2 (Linux) or UI Automation (Windows)
- ✅ **Comprehensive**: Validates actual accessibility tree
- ✅ **Real-world**: Tests what screen readers actually see

**Status:** Framework implemented, full integration pending

### Mandatory Accessibility Checks

**For UI Changes (XAML):**

Every interactive element MUST have:

```xml
<!-- Text input field -->
<TextBox Text="{Binding Response}"
         AutomationProperties.Name="{Binding Label}"
         AutomationProperties.HelpText="{Binding HelpText}"
         TabIndex="0"/>

<!-- Button -->
<Button Command="{Binding SaveCommand}"
        AutomationProperties.Name="Save Form"
        AutomationProperties.HelpText="Saves the current form to disk"/>

<!-- Section expander -->
<Expander Header="{Binding Title}"
          AutomationProperties.Name="{Binding Title}"
          AutomationProperties.HelpText="{Binding Description}"/>
```

**For APR Files:**

Every APR file MUST have:
- Unique prompt labels (no duplicates)
- Section titles (not empty)
- Descriptive labels (not IDs like "prompt_001")
- Help text when needed
- Concise titles (< 100 characters for comfort)

**Test BEFORE committing:**
```bash
dotnet test tests/PromptResponse.AccessibilityTests
```

### Accessibility Testing Workflow

**When creating a new APR file:**

```bash
# 1. Create the APR file
vim examples/my-form.aprt

# 2. Run accessibility validation
dotnet test tests/PromptResponse.AccessibilityTests \
  --filter "FullyQualifiedName~AprFile_Structure_ShouldSupportAccessibility"

# 3. Fix any issues (duplicate labels, missing titles, etc.)

# 4. Commit only when tests pass
git add examples/my-form.aprt
git commit -m "feat: add my-form template with accessibility"
```

**When adding UI elements:**

```bash
# 1. Add XAML with AutomationProperties
vim src/PromptResponse.Desktop/Views/MyView.axaml

# 2. Run all tests
dotnet test

# 3. Manually test keyboard navigation (Tab through all elements)

# 4. If you have Orca/NVDA, test with screen reader (optional but recommended)

# 5. Commit when everything works
git commit -m "feat: add accessible MyView component"
```

### Testing Tools & Approaches

**Priority 1: Automated Static Tests (REQUIRED)**
- Fast, deterministic, CI/CD friendly
- No manual interaction needed
- Primary validation method

**Priority 2: Automated Runtime Tests (IN DEVELOPMENT)**
- Validates actual accessibility tree
- Platform-specific but automated
- Secondary validation when available

**Priority 3: Manual Screen Reader Testing (OPTIONAL)**
- Real-world validation
- Slow, requires expertise
- Use for final verification only

**We prioritize automation over manual testing** because:
- ✅ Faster feedback
- ✅ Consistent results
- ✅ Catches regressions automatically
- ✅ No special hardware/software setup
- ✅ Works in CI/CD pipelines

### Common Accessibility Issues (MUST AVOID)

**❌ Missing AutomationProperties.Name:**
```xml
<!-- BAD: Screen readers can't identify this -->
<TextBox Text="{Binding Response}"/>

<!-- GOOD: Screen reader announces "Email Address" -->
<TextBox Text="{Binding Response}"
         AutomationProperties.Name="Email Address"/>
```

**❌ Duplicate labels in APR:**
```json
// BAD: Two prompts called "Phone"
{"id": "home_phone", "label": "Phone"},
{"id": "work_phone", "label": "Phone"}

// GOOD: Unique labels
{"id": "home_phone", "label": "Home Phone"},
{"id": "work_phone", "label": "Work Phone"}
```

**❌ Using placeholder as only label:**
```xml
<!-- BAD: Placeholder disappears when typing -->
<TextBox Watermark="Enter your name"/>

<!-- GOOD: Proper label always visible to screen readers -->
<TextBlock Text="Full Name"/>
<TextBox Text="{Binding Name}"
         Watermark="e.g., John Smith"
         AutomationProperties.Name="Full Name"/>
```

**❌ No help text for complex fields:**
```xml
<!-- BAD: No guidance -->
<TextBox AutomationProperties.Name="Date of Birth"/>

<!-- GOOD: Help text explains format -->
<TextBox AutomationProperties.Name="Date of Birth"
         AutomationProperties.HelpText="Enter date in MM/DD/YYYY format"/>
```

### Accessibility Test Coverage Goals

**Current:** 8 automated tests validating APR structure
**Target:** 20+ tests covering all accessibility requirements
**Future:** Full runtime accessibility tree validation

**Expanding tests:**
- Add tests for new validation rules
- Test keyboard navigation patterns
- Validate focus management
- Test with multiple APR file types
- Validate color contrast (if applicable)

### Resources

- **Primary:** `tests/PromptResponse.AccessibilityTests/README.md`
- **Guide:** `ACCESSIBILITY.md`
- **Standards:** [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- **Avalonia:** [Accessibility Documentation](https://docs.avaloniaui.net/docs/concepts/accessibility)

### 3. Incremental Development

- Build from foundation up
- Each component must be tested before moving to next
- Commit frequently (every working feature)
- **Never commit broken code**
- Each commit should be deployable

### 4. Code Quality Standards

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

### 5. Logging & Debugging

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

### 6. Dependency Management

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
# Run all tests (functional + accessibility)
dotnet test

# Run only accessibility tests (MUST pass before commit)
dotnet test tests/PromptResponse.AccessibilityTests

# Run functional tests
dotnet test tests/PromptResponse.Core.Tests

# Run with verbose output
dotnet test --verbosity detailed

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific accessibility test
dotnet test --filter "FullyQualifiedName~AprDocument_Prompts_ShouldHave_UniqueLabels"

# Run specific test
dotnet test --filter "FullyQualifiedName~PromptTests.Prompt_ShouldInitializeWithEmptyResponse"

# Watch mode for TDD (reruns tests on file changes)
dotnet watch test --project tests/PromptResponse.AccessibilityTests
```

**Pre-Commit Checklist:**
```bash
# 1. Run ALL tests
dotnet test

# 2. Verify accessibility tests pass
dotnet test tests/PromptResponse.AccessibilityTests

# 3. Check for warnings
dotnet build --warnaserror

# 4. Commit only if everything passes
git add .
git commit -m "feat: description"
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

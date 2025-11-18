# Claude Code Context - PromptResponse Project

**Last Updated**: 2025-11-15

This file provides critical context for Claude Code sessions to ensure consistent, principled development.

---

## Environment

### Claude Code Web (Current Environment)
- **Headless Only**: No interactive UI testing possible
- **No dotnet available**: Cannot run `dotnet build` or `dotnet test` directly
- **Git operations**: Fully supported
- **File operations**: Full read/write access
- **Testing strategy**: Can create tests, but cannot execute them in this environment

**What this means:**
- ✅ Create and edit code
- ✅ Write unit and integration tests
- ✅ Design UI in XAML
- ✅ Commit and push changes
- ❌ Run the application
- ❌ Execute dotnet commands
- ❌ Verify builds compile
- ❌ Run test suites

**User must verify locally:**
```bash
dotnet build
dotnet test
dotnet run --project src/PromptResponse.Desktop
```

---

## Core Purpose - The "Why We Exist"

### Primary Goal
**Break free from the page metaphor** for forms and data collection.

### What We Are
A **cross-platform form creation and filling application** using a **flexible, semantic, text-based format** (.apr files).

### What We Are NOT
- ❌ A word processor
- ❌ A PDF form editor
- ❌ A layout/design tool
- ❌ A database system
- ❌ A workflow engine

### The Fundamental Idea
Traditional forms (Word docs, PDFs) are:
- Tied to page layout and printing
- Not accessible
- Hard to parse programmatically
- Mixed content and presentation

**PromptResponse separates these concerns:**
- **Content** = .apr file (pure data, semantic structure)
- **Presentation** = UI rendering (flexible, adaptive)
- **No layout information** in the file format
- **No code execution** - pure data only

---

## Non-Negotiable Principles

### 1. Accessibility (WCAG 2.1 Level AA) - CRITICAL
**This is NOT optional. This is a REQUIREMENT.**

- All interactive UI elements MUST have `AutomationProperties.Name`
- Form fields MUST have `AutomationProperties.HelpText` when help is available
- APR documents MUST have unique, descriptive labels for all prompts
- Section and subsection titles are REQUIRED (not optional)
- Test accessibility BEFORE committing
- Never use placeholder text as the only label
- Never create duplicate field labels
- Multi-modal feedback (never visual-only)
- Maintain 4.5:1 contrast ratio (normal text), 3:1 (large text)

**If a feature breaks accessibility, we DON'T ship it.**

### 2. Test-Driven Development (TDD) - CRITICAL
**This is NOT optional. This is a REQUIREMENT.**

The workflow is:
1. Write failing test FIRST
2. Run test (verify it fails)
3. Implement minimum code to pass
4. Run test (verify it passes)
5. Run accessibility tests (verify no regressions)
6. Refactor if needed
7. Commit

**Requirements:**
- Unit tests for each component
- Integration tests for component interactions
- Accessibility tests for all UI changes
- >80% code coverage target
- All tests must pass before committing
- Never commit broken code
- Never commit inaccessible UI

**In Claude Code Web:**
- CREATE tests (even though we can't run them)
- USER verifies tests pass locally before merging

### 3. Cross-Platform Compatibility - CRITICAL
**Works identically on Windows, Linux, macOS**

- Use pure AvaloniaUI (cross-platform)
- No Windows-specific APIs in main code
- Platform detection via dependency injection
- Graceful fallbacks for platform features
- Test on multiple platforms

**Platform-specific features need:**
- Interface abstraction (e.g., `IPlatformFeatures`)
- Fallback implementations
- Never crash on unsupported platforms

### 4. Format Integrity - CRITICAL
**The .apr format has strict constraints:**

- **No layout information** (separation of content/presentation)
- **No code execution** (safe to open untrusted files)
- **String-only responses** (regardless of expected type)
- **3-level max depth**: Section → Subsection → Prompt
- **Unique IDs** within scope
- **Stable IDs** (don't change when reordering)
- **UTF-8 JSON** format only

**If a feature requires breaking these constraints, say NO.**

### 5. Separation of Concerns - CRITICAL
**Architecture layers must remain separate:**

```
Core (Models, Validation, Serialization)
  ↓ depends on
Desktop UI (AvaloniaUI)
  ↓ depends on
CLI Tool

NEVER: Core depends on UI
NEVER: Mix presentation in Core
```

---

## Development Workflow

### Standard Process
1. **Plan** the feature/fix
2. **Write tests** (TDD - tests first!)
3. **Implement** code to pass tests
4. **Verify** (build, test, accessibility)
5. **Commit** with clear message
6. **Push** to feature branch

### In Claude Code Web Environment
1. **Plan** the feature/fix
2. **Write tests** (create test files)
3. **Implement** code
4. **Document** what needs verification
5. **Commit** changes
6. **Push** to branch
7. **Tell user** to verify locally:
   ```bash
   dotnet build
   dotnet test
   dotnet run --project src/PromptResponse.Desktop
   ```
8. **Only then** consider merging

### Commit Frequency
- Commit **after tests pass** (or after creating tests if can't run)
- Commit **frequently** (small, atomic changes)
- Each commit should be **buildable**
- Use **conventional commits** format:
  - `feat:` New feature
  - `fix:` Bug fix
  - `test:` Add/update tests
  - `docs:` Documentation
  - `refactor:` Code restructuring
  - `perf:` Performance improvement
  - `chore:` Maintenance

---

## Logging Philosophy

### Why We Log Everything
- Debugging cross-platform issues
- Understanding user workflows
- Performance analysis
- Support and troubleshooting

### Logging Standards
- Use structured logging (`Microsoft.Extensions.Logging`)
- Include context (file paths, operation names)
- Log levels:
  - **Debug**: Detailed diagnostic info
  - **Information**: General flow (file loaded, operation succeeded)
  - **Warning**: Recoverable issues
  - **Error**: Operation failures
  - **Critical**: Application-level failures

### Example
```csharp
_logger.LogInformation("Loading document from {FilePath}", filePath);
_logger.LogError(ex, "Failed to deserialize APR file: {FilePath}", filePath);
```

---

## Design Principles

### User Experience
- **Modern, clean aesthetics** (but accessible!)
- **Intuitive workflows** (minimal learning curve)
- **Cross-platform consistency** (same UX everywhere)
- **Responsive feedback** (show what's happening)
- **Respect user preferences** (theme, motion, etc.)

### Technical Excellence
- **Simple over complex** (avoid over-engineering)
- **Explicit over implicit** (clear intent)
- **Testable** (designed for testing)
- **Maintainable** (others can understand it)
- **Documented** (XML comments on public APIs)

---

## What We Can Compromise On

### Negotiable
- ✅ UI visual details (colors, spacing, animations)
- ✅ Performance optimizations (if they don't break functionality)
- ✅ Additional features (as long as they don't violate core principles)
- ✅ Implementation approach (multiple valid solutions)
- ✅ Platform-specific enhancements (with fallbacks)

### Non-Negotiable
- ❌ **Accessibility** - Never compromise
- ❌ **Cross-platform compatibility** - Must work everywhere
- ❌ **Format integrity** - No layout/code in .apr files
- ❌ **Testing** - No untested code
- ❌ **Separation of concerns** - Keep architecture clean
- ❌ **Data safety** - Never corrupt user data
- ❌ **Security** - No code execution from files

---

## When to Say "We Can't Do That"

### Red Flags
If a feature request involves:
- Adding layout information to .apr format → **Say NO**
- Making Core depend on UI → **Say NO**
- Breaking accessibility → **Say NO**
- Skipping tests → **Say NO**
- Platform-specific-only features (no fallback) → **Say NO**
- Compromising data safety → **Say NO**

### How to Say No
"We can't do [X] because it would compromise [core principle]. Instead, we could [alternative that preserves principles]."

**Example:**
> User: "Can we add a 'fontSize' field to prompts so the UI can render them bigger?"
>
> Response: "We can't add 'fontSize' to the .apr format because that's layout/presentation information, and our core principle is separating content from presentation. Instead, we could:
> 1. Add a semantic 'importance' hint (low/normal/high) in the .apr file
> 2. Let each UI platform decide how to render 'high importance' (could be larger font, bold, etc.)
>
> This preserves format flexibility while giving UI rendering guidance."

---

## Common Patterns

### Dependency Injection
```csharp
// Register in App.axaml.cs
services.AddSingleton<IService, ServiceImpl>();

// Inject in ViewModel
public MyViewModel(IService service) { }
```

### Platform Detection
```csharp
public MyViewModel(IPlatformFeatures platform)
{
    if (platform.SupportsAcrylic)
    {
        // Use acrylic
    }
    else
    {
        // Fallback to solid color
    }
}
```

### MVVM Pattern
```csharp
// Model (Core)
public class Prompt { }

// ViewModel (Desktop)
public class PromptViewModel : ViewModelBase
{
    public Prompt Model { get; }

    public string Response
    {
        get => Model.Response;
        set
        {
            Model.Response = value;
            OnPropertyChanged();
        }
    }
}

// View (XAML)
<TextBox Text="{Binding Response}" />
```

---

## File Structure

```
PromptResponse/
├── src/
│   ├── PromptResponse.Core/          # Platform-agnostic business logic
│   │   ├── Models/                   # Domain models (APR format)
│   │   ├── Serialization/            # JSON serialization
│   │   └── Validation/               # Structural/semantic validation
│   ├── PromptResponse.Desktop/       # Cross-platform AvaloniaUI app
│   │   ├── Views/                    # XAML views
│   │   ├── ViewModels/               # MVVM view models
│   │   ├── Services/                 # Desktop services
│   │   └── Styles/                   # Design system (NEW)
│   └── PromptResponse.Cli/           # Command-line tool
├── tests/
│   ├── PromptResponse.Core.Tests/
│   ├── PromptResponse.AccessibilityTests/  # WCAG compliance tests
│   └── ...
├── docs/                             # Documentation
├── examples/                         # Example .apr files
├── CLAUDE.md                         # Project instructions for Claude
└── .claude/context.md                # This file
```

---

## Key Dependencies

### Core
- `System.Text.Json` - JSON serialization
- `Microsoft.Extensions.Logging.Abstractions` - Logging

### Desktop
- `Avalonia` 11.0+ (MIT License)
- Cross-platform UI framework

### Testing
- `xUnit` - Test framework
- `FluentAssertions` - Assertion library
- `coverlet.collector` - Code coverage

**Principle**: Minimize dependencies, prefer pure .NET, use only permissive licenses.

---

## Quick Reference Commands

### User Should Run Locally
```bash
# Build
dotnet build

# Test
dotnet test
dotnet test tests/PromptResponse.AccessibilityTests

# Run
dotnet run --project src/PromptResponse.Desktop
dotnet run --project src/PromptResponse.Cli -- help

# Coverage
dotnet test /p:CollectCoverage=true
```

### In Claude Code Web
```bash
# What WE can do
git status
git add -A
git commit -m "message"
git push -u origin branch-name

# File operations
# (All Read, Write, Edit, Glob, Grep tools work)
```

---

## Success Criteria

A feature is DONE when:
- ✅ Tests written and passing (unit + integration)
- ✅ Accessibility tests pass (WCAG 2.1 AA)
- ✅ Builds without errors
- ✅ Works on Windows, Linux, macOS
- ✅ Documented (XML comments, README updates)
- ✅ Committed with clear message
- ✅ Pushed to feature branch
- ✅ Code reviewed (if applicable)

**In Claude Code Web**: User verifies build/tests locally

---

## Remember

1. **Accessibility is NOT optional**
2. **Write tests BEFORE code**
3. **We're in a headless environment** - can't run dotnet
4. **Cross-platform or bust**
5. **No layout in .apr format**
6. **Commit frequently, after tests pass**
7. **Log everything important**
8. **When in doubt, preserve core principles**

---

## Contact/References

- **CLAUDE.md**: Project-specific instructions
- **docs/DEVELOPMENT.md**: Development guidelines
- **docs/ARCHITECTURE.md**: System architecture
- **docs/FILE_FORMAT.md**: .apr format specification
- **docs/ACCESSIBILITY.md**: Accessibility testing guide

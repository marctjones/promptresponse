# Accessibility Testing Framework

## Overview

This test project provides automated accessibility testing for PromptResponse using both static APR file validation and dynamic runtime accessibility tree inspection (when available).

## Test Categories

### 1. Static APR Validation Tests (Fast, Always Available)

These tests validate that APR files contain proper accessibility metadata **without** needing the application to run:

- **Title validation**: Forms must have clear, concise titles
- **Label validation**: All prompts must have descriptive, unique labels
- **Help text validation**: Help text must be meaningful when provided
- **Section structure**: Sections and subsections must have titles
- **No duplicate labels**: Prevents confusing screen reader users

**Running:**
```bash
dotnet test tests/PromptResponse.AccessibilityTests

# Output:
# ✅ 8 passed, 2 skipped (integration tests)
```

**Benefits:**
- Runs in CI/CD without special setup
- Fast feedback during development
- Catches common accessibility issues early

### 2. Dynamic Runtime Inspection Tests (Integration, Platform-Specific)

These tests launch the application and inspect the actual accessibility tree that assistive technologies see:

- **Accessibility tree validation**: Verifies UI properly exposes accessibility properties
- **AutomationProperties validation**: Confirms XAML properties map correctly
- **Screen reader compatibility**: Tests what screen readers actually see
- **Cross-platform**: macOS has an opt-in System Events AX capture; Linux and
  Windows runtime backends remain incomplete.

**Status:**
- ✅ Framework implemented
- ⚠️ Integration tests currently skipped (requires running app)
- 🚧 Full AT-SPI2 integration pending (D-Bus complexity)
- 🚧 Windows UI Automation pending

## Running Tests

### All Tests (Static Only, Fast)

```bash
# From project root
dotnet test tests/PromptResponse.AccessibilityTests

# Expected: 8 passed, 2 skipped
```

### Specific Test Classes

```bash
# APR file validation tests
dotnet test tests/PromptResponse.AccessibilityTests --filter FullyQualifiedName~AprAccessibilityValidationTests

# Integration tests (currently skipped)
dotnet test tests/PromptResponse.AccessibilityTests --filter FullyQualifiedName~RunningApplication
```

### Watch Mode (TDD)

```bash
dotnet watch test --project tests/PromptResponse.AccessibilityTests
```

## Platform Support

| Platform | Inspector | Status | Tests Available |
|----------|-----------|--------|-----------------|
| **Linux** | AT-SPI2 | 🟡 Partial | Static tests work, runtime inspection pending |
| **Windows** | UI Automation | 🔴 Planned | Static tests work, runtime planned |
| **macOS** | System Events / AX | 🟡 Opt-in | `scripts/verify-macos-accessibility.sh` captures a live tree from a packaged app; requires Accessibility permission |

### Linux (AT-SPI2)

**Requirements:**
```bash
sudo apt-get install at-spi2-core
```

**Environment:**
```bash
export AVALONIA_ENABLE_ACCESSIBILITY=1
```

**Checking availability:**
```bash
ps aux | grep at-spi
echo $AT_SPI_BUS
```

### Windows (UI Automation) - Future

**Planned implementation:**
- Use `FlaUI` NuGet package for cross-.NET Core support
- Or `System.Windows.Automation` for .NET Framework compat
- Map UIAutomation properties to `AccessibleElement`

**Contribution welcome!**

### macOS (System Events / AX)

Package the app, grant the invoking Terminal or CI runner Accessibility
permission, then run:

```bash
scripts/package-macos-app.sh --output dist/PromptResponse.app
scripts/verify-macos-accessibility.sh dist/PromptResponse.app
```

The capture writes a timestamped JSON accessibility tree and fails when core
menu controls are unnamed. Complete `docs/MACOS_ACCESSIBILITY_SMOKE.md` as the
human VoiceOver evidence for that same build.

## Architecture

### Abstraction Layer

```
IAccessibilityInspector (interface)
├── LinuxAccessibilityInspector (AT-SPI2)
├── WindowsAccessibilityInspector (UI Automation)
└── MacAccessibilityInspector (NSAccessibility)
```

### Core Types

**`AccessibleElement`**
- Represents a UI element as seen by assistive technologies
- Properties: Name, Role, Description, Value, States
- Tree structure with Parent/Children
- Helper methods for searching descendants

**`AccessibilityValidationResult`**
- Contains validation issues
- Severity levels: Critical, High, Medium, Low, Info
- Recommendations for fixes

**`IAccessibilityInspector`**
- Platform-agnostic interface
- Methods:
  - `FindElementByNameAsync()` - Find specific element
  - `FindElementsByRoleAsync()` - Find by role (button, text field, etc.)
  - `GetAccessibilityTreeAsync()` - Get entire tree
  - `ValidateElementAsync()` - Validate accessibility properties

## Test Examples

### Static APR Validation

```csharp
[Fact]
public async Task AprDocument_ShouldHave_AccessibleTitle()
{
    // Arrange
    var json = await File.ReadAllTextAsync("examples/form.aprt");
    var document = _serializer.Deserialize(json);

    // Assert
    document.Metadata.Title.Should().NotBeNullOrWhiteSpace(
        "because the form title is announced by screen readers");
}
```

### Dynamic Runtime Inspection (Integration)

```csharp
[Fact(Skip = "Requires running application")]
public async Task RunningApplication_AllFormFields_ShouldBeAccessible()
{
    var inspector = AccessibilityInspectorFactory.CreateInspector();

    // Launch app with test file
    var document = LoadTestDocument();

    // Verify each prompt is accessible
    foreach (var prompt in GetAllPrompts(document))
    {
        var element = await inspector.FindElementByNameAsync(
            prompt.Label, "text field");

        element.Should().NotBeNull(
            $"prompt '{prompt.Label}' should be accessible");

        var validation = await inspector.ValidateElementAsync(element);
        validation.IsValid.Should().BeTrue();
    }
}
```

## Continuous Integration

### GitHub Actions Example

```yaml
name: Accessibility Tests

on: [push, pull_request]

jobs:
  accessibility:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Run accessibility tests
        run: dotnet test tests/PromptResponse.AccessibilityTests
```

Static tests run in CI without special setup!

## Writing New Tests

### 1. Static APR Validation Test

```csharp
[Theory]
[InlineData("examples/form1.aprt")]
[InlineData("examples/form2.aprt")]
public async Task AprFile_CustomValidation(string aprFile)
{
    // Load APR
    var json = await File.ReadAllTextAsync(aprFile);
    var document = _serializer.Deserialize(json);

    // Validate accessibility properties
    // ...your custom validation...

    // Assert with helpful messages
    condition.Should().BeTrue(
        "because screen reader users need...");
}
```

### 2. Runtime Accessibility Test

```csharp
[Fact(Skip = "Requires running app")]
public async Task CustomAccessibilityCheck()
{
    var inspector = AccessibilityInspectorFactory.CreateInspector();

    if (!await inspector.IsAvailableAsync())
    {
        // Skip if inspector not available
        return;
    }

    // TODO: Launch app
    // TODO: Perform actions
    // TODO: Validate accessibility tree
}
```

## Manual Testing with Orca (Linux)

For interactive testing with actual screen reader:

```bash
# See test-accessibility.sh in project root
./test-accessibility.sh --file examples/form.aprt --no-speech
```

This launches the app with Orca and captures all announcements to a log file for review.

## Common Validation Rules

### ✅ Do

- **All interactive elements must have accessible names**
  - Set `AutomationProperties.Name` in XAML
  - Use the field label as the accessible name

- **Help text should be accessible descriptions**
  - Set `AutomationProperties.HelpText`
  - Maps to accessible description

- **Use semantic roles**
  - TextBox for text fields
  - Button for buttons
  - Proper controls auto-assign roles

- **Maintain focus order**
  - TabIndex for logical order
  - Test keyboard navigation

### ❌ Don't

- **Don't use placeholder text as only label**
  - Placeholders disappear when typing
  - Use proper labels with AutomationProperties.Name

- **Don't duplicate labels**
  - Screen reader users navigate by field name
  - Make labels unique and descriptive

- **Don't rely on color alone**
  - Use text or icons for information
  - Ensure sufficient contrast

- **Don't create keyboard traps**
  - All interactive elements must be reachable
  - Tab order must be logical

## Debugging Failed Tests

### Test Failure: "Element has no accessible name"

**Cause:** AutomationProperties.Name not set

**Fix:**
```xml
<TextBox Text="{Binding Response}"
         AutomationProperties.Name="{Binding Label}"
         AutomationProperties.HelpText="{Binding HelpText}"/>
```

### Test Failure: "Duplicate labels found"

**Cause:** Multiple prompts with identical labels

**Fix:** Make labels unique:
- "First Name" / "Last Name" instead of "Name" / "Name"
- "Home Phone" / "Work Phone" instead of "Phone" / "Phone"

### Test Failure: "Label looks like technical ID"

**Cause:** Label is set to ID value (e.g., "prompt_001")

**Fix:** Use user-friendly labels:
```csharp
prompt.Label = "Email Address";  // Good
prompt.Label = "email_addr_001";  // Bad - looks like ID
```

## Performance

**Static tests:** < 100ms per test
**Integration tests:** 2-5 seconds (app launch + inspection)

**CI Impact:** Minimal - static tests add ~1 second to build

## Future Enhancements

### Planned

- [ ] Full AT-SPI2 D-Bus integration for Linux
- [ ] Windows UI Automation integration (FlaUI)
- [ ] macOS NSAccessibility integration
- [ ] Visual regression testing for focus indicators
- [ ] Automated keyboard navigation testing
- [ ] Color contrast validation
- [ ] Font size scaling tests
- [ ] ARIA role validation (if applicable)

### Contributions Welcome

See `ACCESSIBILITY.md` in project root for contribution guidelines.

**Priority areas:**
1. Windows UI Automation implementation
2. AT-SPI2 D-Bus query implementation
3. Automated app launching for integration tests
4. Additional validation rules

## Resources

- [ACCESSIBILITY.md](../../ACCESSIBILITY.md) - Comprehensive accessibility guide
- [IAccessibilityInspector.cs](./IAccessibilityInspector.cs) - Core interface
- [AprAccessibilityValidationTests.cs](./AprAccessibilityValidationTests.cs) - Test examples
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [Avalonia Accessibility](https://docs.avaloniaui.net/docs/concepts/accessibility)

## Support

**Questions? Issues?**
- Check test output for specific failure messages
- Review `ACCESSIBILITY.md` for accessibility requirements
- Run `./test-accessibility.sh` for interactive testing
- File issues with specific test failures and platform info

**Contributing:**
- Add tests for new accessibility requirements
- Implement platform-specific inspectors
- Improve validation rules
- Add integration test automation

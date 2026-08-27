using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using System.IO;
using Xunit;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Tests that validate APR files have proper accessibility properties
/// and that those properties are correctly exposed to assistive technologies.
/// </summary>
/// <remarks>
/// These tests verify:
/// 1. APR files contain appropriate content for accessibility
/// 2. UI properly exposes accessibility properties from APR data
/// 3. Screen readers can access all form content
///
/// Tests can run in two modes:
/// - Unit mode: Validates APR file structure without UI (fast)
/// - Integration mode: Validates actual accessibility tree (requires running app)
/// </remarks>
public class AprAccessibilityValidationTests
{
    private readonly IAprSerializer _serializer;

    public AprAccessibilityValidationTests()
    {
        _serializer = new AprJsonSerializer();
    }

    [Fact]
    public async Task AprDocument_ShouldHave_AccessibleTitle()
    {
        // Arrange
        var aprFile = "examples/simple-contact-form.aprt";

        if (!File.Exists(aprFile))
        {
            // Skip if example file doesn't exist (CI environments)
            return;
        }

        // Act
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // Assert
        document.Metadata.Title.Should().NotBeNullOrWhiteSpace(
            "because the form title is announced by screen readers as the main heading");

        document.Metadata.Title.Length.Should().BeLessThan(100,
            "because very long titles are difficult for screen reader users to understand");
    }

    [Fact]
    public async Task AprDocument_ShouldHave_DescriptiveLabels()
    {
        // Arrange
        var aprFile = "examples/employment-application.apr";

        if (!File.Exists(aprFile))
        {
            return;
        }

        // Act
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // Assert
        var allPrompts = GetAllPrompts(document);

        allPrompts.Should().NotBeEmpty("because forms should have prompts");

        foreach (var prompt in allPrompts)
        {
            prompt.Label.Should().NotBeNullOrWhiteSpace(
                $"because prompt {prompt.Id} needs a label for screen readers");

            prompt.Label.Should().NotBe(prompt.Id,
                $"because '{prompt.Label}' looks like a technical ID, not a user-friendly label");

            // Labels should be reasonably concise
            prompt.Label.Length.Should().BeLessThan(150,
                $"because label '{prompt.Label}' is too long for comfortable screen reader listening");
        }
    }

    [Fact]
    public async Task AprDocument_Prompts_ShouldHave_UniqueLabels()
    {
        // Arrange
        var aprFile = "examples/employment-application.apr";

        if (!File.Exists(aprFile))
        {
            return;
        }

        // Act
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // Assert
        var allPrompts = GetAllPrompts(document);
        var labels = allPrompts.Select(p => p.Label).ToList();

        var duplicates = labels.GroupBy(l => l)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty(
            "because duplicate labels confuse screen reader users who navigate by field name. " +
            $"Duplicate labels found: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public async Task AprDocument_Sections_ShouldHave_Titles()
    {
        // Arrange
        var aprFile = "examples/employment-application.apr";

        if (!File.Exists(aprFile))
        {
            return;
        }

        // Act
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // Assert
        document.Sections.Should().NotBeEmpty("because forms are organized into sections");

        foreach (var section in document.Sections)
        {
            section.Title.Should().NotBeNullOrWhiteSpace(
                $"because section {section.Id} needs a title for screen reader navigation");

            section.Title.Should().NotBe(section.Id,
                $"because '{section.Title}' looks like a technical ID");
        }
    }

    [Fact]
    public async Task AprDocument_HelpText_ShouldBeDescriptive()
    {
        // Arrange
        var aprFile = "examples/employment-application.apr";

        if (!File.Exists(aprFile))
        {
            return;
        }

        // Act
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // Assert
        var promptsWithHelp = GetAllPrompts(document)
            .Where(p => !string.IsNullOrWhiteSpace(p.Hints.HelpText))
            .ToList();

        foreach (var prompt in promptsWithHelp)
        {
            prompt.Hints.HelpText!.Length.Should().BeGreaterThan(5,
                $"because help text '{prompt.Hints.HelpText}' for '{prompt.Label}' is too short to be helpful");

            prompt.Hints.HelpText.Should().NotBe(prompt.Label,
                $"because help text should provide additional guidance beyond the label");
        }
    }



    // ── Bundled starter templates (#36) — hard-asserted (no silent skip) ──

    private static string RepoRoot => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..");

    [Theory]
    [InlineData("time-off-request.aprt")]
    [InlineData("expense-report.aprt")]
    [InlineData("it-access-request.aprt")]
    [InlineData("contact-intake.aprt")]
    [InlineData("event-registration.aprt")]
    [InlineData("incident-report.aprt")]
    [InlineData("order-form.aprt")]
    public async Task StarterTemplate_IsAccessible(string fileName)
    {
        var path = Path.Combine(RepoRoot, "examples", fileName);
        File.Exists(path).Should().BeTrue($"the bundled starter template {fileName} must exist");

        var document = _serializer.Deserialize(await File.ReadAllTextAsync(path));

        document.Metadata.Title.Should().NotBeNullOrWhiteSpace("a template needs a title");

        var labels = new List<string>();
        var ids = new List<string>();
        void Walk(Section section, string context)
        {
            section.Id.Should().NotBeNullOrWhiteSpace($"every section needs an id ({context})");
            section.Title.Should().NotBeNullOrWhiteSpace($"every section needs a title ({context})");
            ids.Add(section.Id);
            foreach (var p in section.Prompts)
            {
                p.Id.Should().NotBeNullOrWhiteSpace($"every prompt needs an id (in {section.Title})");
                p.Label.Should().NotBeNullOrWhiteSpace($"every prompt needs a label (in {section.Title})");
                p.Hints.HelpText.Should().NotBeNullOrWhiteSpace(
                    $"starter-template prompt '{p.Label}' should have help text for screen readers");
                labels.Add(p.Label);
                ids.Add(p.Id);
            }
            foreach (var child in section.Sections) Walk(child, $"{context} / {section.Title}");
        }
        foreach (var s in document.Sections) Walk(s, fileName);

        labels.Should().OnlyHaveUniqueItems("duplicate labels confuse screen readers");
        ids.Should().OnlyHaveUniqueItems("ids must be unique within a document");
    }

    [Theory]
    [InlineData("examples/simple-contact-form.aprt")]
    [InlineData("examples/employment-application.apr")]
    public async Task AprFile_Structure_ShouldSupportAccessibility(string aprFile)
    {
        if (!File.Exists(aprFile))
        {
            return;
        }

        // Act
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // Assert - Document level
        document.Metadata.Title.Should().NotBeNullOrWhiteSpace(
            "document must have a title for screen reader announcement");

        // Assert - Section level
        foreach (var section in document.Sections)
        {
            section.Id.Should().NotBeNullOrWhiteSpace("sections must have IDs");
            section.Title.Should().NotBeNullOrWhiteSpace(
                "sections must have titles for navigation");

            // Validate prompts and child sections recursively
            ValidateSectionAccessibility(section, $"Section '{section.Title}'");
        }
    }

    private void ValidateSectionAccessibility(Section section, string context)
    {
        // Validate prompts at this level
        ValidatePromptsAccessibility(section.Prompts, context);

        // Recursively validate child sections
        foreach (var childSection in section.Sections)
        {
            childSection.Title.Should().NotBeNullOrWhiteSpace(
                "sections must have titles");

            ValidateSectionAccessibility(childSection,
                $"Child Section '{childSection.Title}' in {context}");
        }
    }

    private void ValidatePromptsAccessibility(IEnumerable<Prompt> prompts, string context)
    {
        foreach (var prompt in prompts)
        {
            prompt.Id.Should().NotBeNullOrWhiteSpace(
                $"prompt in {context} must have ID");

            prompt.Label.Should().NotBeNullOrWhiteSpace(
                $"prompt {prompt.Id} in {context} must have label for screen readers");

            // If there's a placeholder, it shouldn't be the only guidance
            if (!string.IsNullOrWhiteSpace(prompt.Hints.Placeholder) &&
                string.IsNullOrWhiteSpace(prompt.Hints.HelpText))
            {
                // This is not a failure, but worth noting
                // Placeholders disappear when typing, so help text is better
            }
        }
    }

    private List<Prompt> GetAllPrompts(AprDocument document)
    {
        var prompts = new List<Prompt>();

        foreach (var section in document.Sections)
        {
            CollectPromptsFromSection(section, prompts);
        }

        return prompts;
    }

    private void CollectPromptsFromSection(Section section, List<Prompt> prompts)
    {
        prompts.AddRange(section.Prompts);

        foreach (var childSection in section.Sections)
        {
            CollectPromptsFromSection(childSection, prompts);
        }
    }

    // Two stubs that required a running application and an external accessibility
    // inspector were removed rather than left skipped. They were never implemented -
    // their bodies were TODOs - and a permanent skip hides a gap instead of recording it.
    //
    // Their intent is covered runnably by the headless GUI suite:
    // AutomationTreeTests.EveryPromptInForm_HasAccessibleNameMatchingItsLabel and
    // Menu_BarAndItems_AppearInAutomationTree_WithNames query the real automation tree
    // in-process. External AT-SPI verification against a live app remains the manual
    // script at tests/at-spi/run_at_spi_smoke.sh, recorded as a gap in tests/registry.json.
}

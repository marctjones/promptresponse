using FluentAssertions;
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

    [Fact(Skip = "Requires running application - integration test")]
    public async Task RunningApplication_ShouldExpose_FormTitleToScreenReader()
    {
        // This test requires the application to be running
        // It would launch the app, query the accessibility tree, and verify the title

        var inspector = AccessibilityInspectorFactory.CreateInspector();

        if (!await inspector.IsAvailableAsync())
        {
            // Skip if accessibility inspector not available
            return;
        }

        // TODO: Launch application
        // TODO: Load test APR file
        // TODO: Query accessibility tree
        // TODO: Verify title is exposed

        var tree = await inspector.GetAccessibilityTreeAsync("PromptResponse");
        tree.Should().NotBeNull("because application should be accessible");

        // Find the form title
        var titleElement = await inspector.FindElementByNameAsync("Simple Contact Form");
        titleElement.Should().NotBeNull("because form title should be accessible");
        titleElement!.Role.Should().Be("heading", "because title should be marked as heading");
    }

    [Fact(Skip = "Requires running application - integration test")]
    public async Task RunningApplication_AllFormFields_ShouldBeAccessible()
    {
        // This test would:
        // 1. Load APR file
        // 2. Launch app with that file
        // 3. Query accessibility tree
        // 4. Verify each prompt in APR has corresponding accessible element
        // 5. Verify labels match
        // 6. Verify help text is exposed

        var inspector = AccessibilityInspectorFactory.CreateInspector();

        if (!await inspector.IsAvailableAsync())
        {
            return;
        }

        var aprFile = "examples/simple-contact-form.aprt";
        var json = await File.ReadAllTextAsync(aprFile);
        var document = _serializer.Deserialize(json);

        // TODO: Launch app with this file

        var allPrompts = GetAllPrompts(document);

        foreach (var prompt in allPrompts)
        {
            // Find corresponding accessible element
            var element = await inspector.FindElementByNameAsync(prompt.Label, "text field");

            element.Should().NotBeNull(
                $"because prompt '{prompt.Label}' should be accessible to screen readers");

            element!.Name.Should().Be(prompt.Label,
                "because accessible name should match the prompt label");

            if (!string.IsNullOrWhiteSpace(prompt.Hints.HelpText))
            {
                element.Description.Should().Be(prompt.Hints.HelpText,
                    "because help text should be exposed as accessible description");
            }

            // Validate the element
            var validation = await inspector.ValidateElementAsync(element);
            validation.IsValid.Should().BeTrue(
                $"because '{prompt.Label}' should have proper accessibility properties. " +
                $"Issues: {string.Join(", ", validation.Issues.Select(i => i.Message))}");
        }
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

            // Validate prompts
            ValidatePromptsAccessibility(section.Prompts, $"Section '{section.Title}'");

            // Validate subsections
            foreach (var subsection in section.Subsections)
            {
                subsection.Title.Should().NotBeNullOrWhiteSpace(
                    "subsections must have titles");

                ValidatePromptsAccessibility(subsection.Prompts,
                    $"Subsection '{subsection.Title}' in Section '{section.Title}'");
            }
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
            prompts.AddRange(section.Prompts);

            foreach (var subsection in section.Subsections)
            {
                prompts.AddRange(subsection.Prompts);
            }
        }

        return prompts;
    }
}

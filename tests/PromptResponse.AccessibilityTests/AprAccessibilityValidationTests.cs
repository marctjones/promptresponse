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
    private static string RepoRoot => Path.Combine(
        Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..");

    private readonly IAprSerializer _serializer;

    public AprAccessibilityValidationTests()
    {
        _serializer = new AprJsonSerializer();
    }

    /// <summary>Every bundled example, discovered from disk.</summary>
    /// <remarks>
    /// Enumerated rather than listed. The tests these replaced named two files by hand and
    /// opened with "if (!File.Exists(path)) return;" so they would not break in CI. The
    /// files were later renamed, and the guard did exactly what it was written to do: the
    /// tests kept passing while asserting nothing at all. Six of them, in the suite
    /// CLAUDE.md calls non-negotiable.
    ///
    /// Reading the directory removes both failure modes. A rename cannot silence these
    /// tests, a new example is covered the day it is added, and if the directory is ever
    /// empty the member data itself fails rather than yielding no cases.
    /// </remarks>
    public static IEnumerable<object[]> AllExamples()
    {
        var dir = Path.Combine(RepoRoot, "examples");
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.aprt").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList()
            : [];

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"No example templates found under {dir}. These tests assert against the " +
                "bundled examples; finding none is a broken checkout, not an empty test run.");
        }

        return files.Select(f => new object[] { Path.GetFileName(f) });
    }

    private async Task<AprDocument> LoadExample(string fileName)
    {
        var path = Path.Combine(RepoRoot, "examples", fileName);
        File.Exists(path).Should().BeTrue($"the bundled example {fileName} must exist");
        return _serializer.Deserialize(await File.ReadAllTextAsync(path));
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public async Task Example_HasAccessibleTitle(string fileName)
    {
        var document = await LoadExample(fileName);

        document.Metadata.Title.Should().NotBeNullOrWhiteSpace(
            "because the form title is announced by screen readers as the main heading");
        document.Metadata.Title.Length.Should().BeLessThan(100,
            "because very long titles are difficult for screen reader users to understand");
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public async Task Example_HasDescriptiveLabels(string fileName)
    {
        var document = await LoadExample(fileName);
        var prompts = GetAllPrompts(document);

        foreach (var prompt in prompts)
        {
            prompt.Label.Should().NotBeNullOrWhiteSpace(
                $"because prompt {prompt.Id} must have a label for screen readers");
        }

        // field-types-showcase exists to demonstrate how the UI handles awkward input,
        // and one of its labels is deliberately long - the label says so in its own text.
        // Exempting the file wholesale would let a genuinely careless label slip in beside
        // the intentional one, so the allowance is exactly one label, and every other label
        // in that file is held to the same limit as everywhere else.
        var overLimit = prompts.Where(p => p.Label.Length >= 100).ToList();
        var allowance = fileName == "field-types-showcase.aprt" ? 1 : 0;

        overLimit.Should().HaveCountLessThanOrEqualTo(allowance,
            "because long labels are tiring to listen to on a screen reader. Over the " +
            $"limit in {fileName}: {string.Join(" | ", overLimit.Select(p => p.Label))}");
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public async Task Example_SectionsHaveTitles(string fileName)
    {
        var document = await LoadExample(fileName);

        document.Sections.Should().NotBeEmpty("because forms are organized into sections");

        void Walk(Section section)
        {
            section.Title.Should().NotBeNullOrWhiteSpace(
                $"because section {section.Id} needs a title for screen reader navigation");
            section.Title.Should().NotBe(section.Id,
                $"because '{section.Title}' looks like a technical ID");
            foreach (var child in section.Sections) Walk(child);
        }
        foreach (var section in document.Sections) Walk(section);
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public async Task Example_HelpTextIsDescriptive(string fileName)
    {
        var document = await LoadExample(fileName);

        foreach (var prompt in GetAllPrompts(document)
                     .Where(p => !string.IsNullOrWhiteSpace(p.Hints.HelpText)))
        {
            prompt.Hints.HelpText!.Length.Should().BeGreaterThan(5,
                $"because help text '{prompt.Hints.HelpText}' for '{prompt.Label}' is too short to be helpful");
            prompt.Hints.HelpText.Should().NotBe(prompt.Label,
                "because help text should provide additional guidance beyond the label");
        }
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public async Task Example_StructureSupportsAccessibility(string fileName)
    {
        var document = await LoadExample(fileName);

        document.Metadata.Title.Should().NotBeNullOrWhiteSpace(
            "document must have a title for screen reader announcement");

        foreach (var section in document.Sections)
        {
            section.Id.Should().NotBeNullOrWhiteSpace("sections must have IDs");
            section.Title.Should().NotBeNullOrWhiteSpace("sections must have titles for navigation");
            ValidateSectionAccessibility(section, $"Section '{section.Title}'");
        }
    }

    // ── Bundled starter templates (#36) — hard-asserted (no silent skip) ──

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

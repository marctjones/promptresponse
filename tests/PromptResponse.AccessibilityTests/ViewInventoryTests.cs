using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace PromptResponse.AccessibilityTests;

/// <summary>Every view the application can show is on disk and is asserted against.</summary>
/// <remarks>
/// Enumerating the Views directory stops a rename from silencing a test, but it does
/// not stop a deletion: a view that is gone is simply not enumerated, and the suite
/// gets quietly smaller while staying green. That is the same defect the missing-file
/// guards had, wearing a different hat.
///
/// So the inventory is derived from the code that constructs the views. Every prompt
/// view the template selector can return must exist, and every view file must be
/// reachable by the accessibility theories. Deleting one fails here.
/// </remarks>
public class ViewInventoryTests
{
    [Fact]
    public void EveryPromptViewTheSelectorCanConstruct_ExistsOnDisk()
    {
        var selector = KeyboardNavigationTestFiles.ReadDesktopFile(
            Path.Combine("Views", "Prompts", "PromptDataTemplateSelector.cs"));

        var constructed = Regex.Matches(selector, @"new (\w+PromptView)\(\)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        constructed.Should().NotBeEmpty(
            "the template selector is the inventory these tests are derived from");

        foreach (var view in constructed)
        {
            var path = KeyboardNavigationTestFiles.DesktopPath(
                Path.Combine("Views", "Prompts", view + ".axaml"));
            File.Exists(path).Should().BeTrue(
                $"the template selector constructs {view}, so {view}.axaml must exist "
                + "and be covered by the accessibility theories");
        }
    }

    [Fact]
    public void TheViewInventory_IsNotSilentlyShrinking()
    {
        var views = KeyboardNavigationTestFiles.AllViews().Select(row => (string)row[0]).ToList();

        // A floor, not an exact count: adding a view is ordinary and must not fail.
        // Losing one is not, and this is what makes a deletion visible in a suite
        // whose cases are discovered rather than listed.
        views.Should().HaveCountGreaterThanOrEqualTo(22,
            "views are discovered from disk, so a deleted view removes its own test "
            + "cases; if a view was deliberately retired, lower this floor in the same "
            + "commit and say which one and why");
    }
}

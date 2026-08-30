using AwesomeAssertions;
using PromptResponse.Cli.Commands.Diff;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands.Diff;

public class DiffOutputFormatterTests
{
    [Fact]
    public void Format_NoDifferences_UsesTheExistingSuccessMessage()
    {
        var lines = DiffOutputFormatter.Format("first.apr", "second.apr", Array.Empty<Difference>()).ToArray();

        lines.Should().ContainInOrder(
            "Document Comparison",
            "File 1: first.apr",
            "File 2: second.apr",
            "✓ Documents are identical (responses match)");
        lines[^1].Should().Be("═══════════════════════════════════════");
    }

    [Fact]
    public void Format_DifferenceWithNullValues_ShowsEmptyMarkersAndSummary()
    {
        var lines = DiffOutputFormatter.Format(
            "first.apr",
            "second.apr",
            new[] { new Difference("Structure", "Section[1]", null, "Added") }).ToArray();

        lines.Should().ContainInOrder(
            "Found 1 difference(s):",
            "[Structure] Section[1]",
            "  File 1: (empty)",
            "  File 2: Added",
            "Summary: 1 difference(s) found");
    }
}

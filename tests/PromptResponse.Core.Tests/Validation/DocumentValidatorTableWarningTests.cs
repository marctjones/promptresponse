using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Locks the advisory table diagnostics before their traversal is refactored.</summary>
public sealed class DocumentValidatorTableWarningTests : DocumentValidatorTestBase
{
    [Fact]
    public void EmptyTable_IsAnError_NotAnAdvisory()
    {
        var table = CreateSection(prompts: []); table.Kind = "table";
        var result = Validator.Validate(CreateDocument("T", table));
        // beta.6 made a table's first instance required rather than merely described,
        // so this stopped being the TABLE_NO_ROWS advisory and became an error: a table
        // with no instances cannot describe its own fields.
        result.Errors.Should().ContainSingle(error =>
            error.ErrorCode == "EMPTY_TABLE" && error.PropertyPath == "sections[0]");
    }

    [Fact]
    public void RaggedTable_ReportsAlignmentWarning()
    {
        var table = CreateTable(Row("a", "A"), Row("b", "B", "C"));
        var result = Validator.Validate(CreateDocument("T", table));
        result.Warnings.Should().ContainSingle(warning => warning.WarningCode == "TABLE_RAGGED" && warning.PropertyPath == "sections[0].sections");
    }

    [Fact]
    public void DifferentlyLabelledTableColumns_ReportLabelWarning()
    {
        var table = CreateTable(Row("a", "A"), Row("b", "B"));
        var result = Validator.Validate(CreateDocument("T", table));
        result.Warnings.Should().ContainSingle(warning => warning.WarningCode == "TABLE_LABEL_MISMATCH" && warning.Message.Contains("field 0 'B'"));
    }

    [Fact]
    public void TableAboveAdvisoryMaximum_ReportsCapacityWarningAfterShapeWarnings()
    {
        var table = CreateTable(Row("a", "A"), Row("b", "B", "C")); table.MaxRows = 1;
        var warnings = Validator.Validate(CreateDocument("T", table)).Warnings;
        warnings.Select(warning => warning.WarningCode).Should().ContainInOrder("TABLE_RAGGED", "TABLE_OVER_CAPACITY");
    }

    private static Section CreateTable(params Section[] rows) { var table = CreateSection(childSections: [.. rows]); table.Kind = "table"; return table; }
    private static Section Row(string id, params string[] labels) => CreateSection(id, id, labels.Select((label, index) => new Prompt { Id = $"{id}-{index}", Label = label }).ToList());
}

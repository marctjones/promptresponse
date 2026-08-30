using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Locks the advisory table diagnostics before their traversal is refactored.</summary>
public sealed class DocumentValidatorTableWarningTests : DocumentValidatorTestBase
{
    [Fact]
    public void EmptyTable_ReportsNoRowsWarning()
    {
        var table = CreateSection(prompts: []); table.Kind = "table";
        var result = Validator.Validate(CreateDocument("T", table));
        result.Warnings.Should().ContainSingle(warning => warning.WarningCode == "TABLE_NO_ROWS" && warning.PropertyPath == "sections[0]");
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
        var table = CreateTable(Row("a", "A"), Row("b", "B", "C")); table.MaxRows = "1";
        var warnings = Validator.Validate(CreateDocument("T", table)).Warnings;
        warnings.Select(warning => warning.WarningCode).Should().ContainInOrder("TABLE_RAGGED", "TABLE_OVER_CAPACITY");
    }

    private static Section CreateTable(params Section[] rows) { var table = CreateSection(childSections: [.. rows]); table.Kind = "table"; return table; }
    private static Section Row(string id, params string[] labels) => CreateSection(id, id, labels.Select((label, index) => new Prompt { Id = $"{id}-{index}", Label = label }).ToList());
}

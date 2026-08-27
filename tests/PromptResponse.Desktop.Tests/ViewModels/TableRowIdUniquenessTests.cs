using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Rows added in the editor must not collide with rows in another table.
/// </summary>
/// <remarks>
/// Section ids share one namespace across the whole document, but row ids were generated
/// by counting within their own table - so every dynamic table produced "row1", "row2",
/// and a form with two of them became invalid the moment each had a row. Ordinary use of
/// the editor produced a document that failed the application's own validator.
///
/// It had already happened: examples/field-types-showcase.aprt shipped with two dynamic
/// tables whose starter rows were both called "row1". No test caught it because no test
/// built two tables in one document, and the example files were not being validated.
/// </remarks>
public class TableRowIdUniquenessTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static SectionViewModel TableOver(Section section, EditHistory history) =>
        new(section,
            new PromptViewModelFactory(new ProfileService(new FixedProbe(), applyAffordanceDefaults: false), history),
            depth: 0,
            onPromptAdded: _ => { },
            onPromptRemoved: _ => { },
            history: history);

    [Fact]
    public void TwoDynamicTablesInOneDocument_ProduceUniqueRowIds()
    {
        var history = new EditHistory();
        var first = new Section { Id = "tbl_orders", Title = "Orders", Prompts = [] };
        var second = new Section { Id = "tbl_addresses", Title = "Addresses", Prompts = [] };

        var document = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Two Tables" },
            Sections = [first, second],
        };

        var firstTable = TableOver(first, history);
        var secondTable = TableOver(second, history);
        firstTable.ConvertToDynamicTable();
        secondTable.ConvertToDynamicTable();

        // Add rows to both, the way a person filling in two tables would.
        for (var i = 0; i < 3; i++)
        {
            firstTable.AddRow();
            secondTable.AddRow();
        }

        var rowIds = first.Sections.Select(r => r.Id).Concat(second.Sections.Select(r => r.Id)).ToList();

        rowIds.Should().OnlyHaveUniqueItems(
            "section ids share one namespace across the document, so a row in one table " +
            "must not take the same id as a row in another");

        var result = new DocumentValidator().Validate(document);
        result.IsValid.Should().BeTrue(
            "adding rows to two tables is ordinary editing and must not produce a document " +
            "the application itself rejects. Errors: " +
            string.Join(" | ", result.Errors.Select(e => $"{e.ErrorCode} at {e.PropertyPath}")));
    }

    [Fact]
    public void CellIdsFollowTheirRow_SoTheOwningTableIsLegible()
    {
        var history = new EditHistory();
        var table = new Section { Id = "tbl_orders", Title = "Orders", Prompts = [] };
        var vm = TableOver(table, history);
        vm.ConvertToDynamicTable();
        vm.AddRow();

        var row = table.Sections[^1];
        row.Id.Should().StartWith("tbl_orders.",
            "a row id names the table it belongs to");
        row.Prompts.Should().OnlyContain(cell => cell.Id.StartsWith(row.Id + "."),
            "cells are named after their row, so the whole path is legible from a cell id");
    }
}

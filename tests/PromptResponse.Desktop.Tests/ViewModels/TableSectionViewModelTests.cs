using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Tests for the table-section view-model: a Section flagged with TableLayout
/// renders as a table; its child Sections are rows; each row's Prompts are cells
/// addressed by id "{rowId}.{columnId}". Vision invariants: every cell value
/// stays a string, type hints are advisory, and any visible text is a valid
/// cell response. Static tables have a fixed row set; dynamic tables expose
/// add/remove that mutate the underlying Section tree.
/// </summary>
public class TableSectionViewModelTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
    private static PromptViewModelFactory NewFactory() => new(NewService());

    // ── Helpers — build the post-migration shape (Section + rows + cell prompts) ──

    private static Section FixedTableSection(Dictionary<string, Dictionary<string, string>>? cellValues = null)
    {
        var def = new TableDefinition
        {
            Columns = new List<TableColumn>
            {
                new() { Id = "revenue", Label = "Revenue", Type = "currency", Placeholder = "0.00" },
                new() { Id = "expenses", Label = "Expenses", Type = "currency", Placeholder = "0.00" },
            },
            FixedRows = new List<FixedRow>
            {
                new() { Id = "q1", Label = "Q1" },
                new() { Id = "q2", Label = "Q2" },
            },
        };
        var section = new Section { Id = "tbl", Title = "Quarterly results", TableLayout = def };
        foreach (var row in def.FixedRows!)
        {
            var rowSection = new Section { Id = row.Id, Title = row.Label };
            foreach (var col in def.Columns)
            {
                var initial = cellValues?.GetValueOrDefault(row.Id)?.GetValueOrDefault(col.Id) ?? string.Empty;
                rowSection.Prompts.Add(new Prompt
                {
                    Id = $"{row.Id}.{col.Id}",
                    Label = col.Label,
                    Response = initial,
                    Hints = new PromptHints { ExpectedDataType = col.Type, Placeholder = col.Placeholder },
                });
            }
            section.Sections.Add(rowSection);
        }
        return section;
    }

    private static Section DynamicTableSection(int minRows = 0, int maxRows = 100, string rowLabel = "Item")
    {
        var def = new TableDefinition
        {
            Columns = new List<TableColumn>
            {
                new() { Id = "desc", Label = "Description", Type = "text" },
                new() { Id = "qty", Label = "Qty", Type = "number" },
            },
            DynamicRows = new DynamicRowConfig { MinRows = minRows, MaxRows = maxRows, RowLabel = rowLabel },
        };
        return new Section { Id = "tbl", Title = "Line items", TableLayout = def };
    }

    // ── Construction + flags ──

    [Fact]
    public void TableLayoutFlag_DistinguishesTableFromRegularSection()
    {
        var regular = new SectionViewModel(new Section { Id = "s", Title = "X" }, NewFactory(), depth: 0);
        var table = new SectionViewModel(FixedTableSection(), NewFactory(), depth: 0);

        regular.IsTableSection.Should().BeFalse();
        regular.IsRegularSection.Should().BeTrue();
        table.IsTableSection.Should().BeTrue();
        table.IsRegularSection.Should().BeFalse();
    }

    [Fact]
    public void Fixed_NestedSections_AreRowsInDeclaredOrder()
    {
        var vm = new SectionViewModel(FixedTableSection(), NewFactory(), depth: 0);

        vm.IsFixedTable.Should().BeTrue();
        vm.IsDynamicTable.Should().BeFalse();
        vm.NestedSections.Should().HaveCount(2);
        vm.NestedSections[0].Id.Should().Be("q1");
        vm.NestedSections[0].Title.Should().Be("Q1");
        vm.NestedSections[1].Id.Should().Be("q2");
    }

    [Fact]
    public void Fixed_RowCells_AreInColumnOrder_WithCellPromptIds()
    {
        var vm = new SectionViewModel(FixedTableSection(), NewFactory(), depth: 0);
        var firstRow = vm.NestedSections[0];

        firstRow.Cells.Should().HaveCount(2);
        firstRow.Cells[0].ColumnId.Should().Be("revenue");
        firstRow.Cells[1].ColumnId.Should().Be("expenses");
        firstRow.PromptViewModels[0].Id.Should().Be("q1.revenue");
        firstRow.PromptViewModels[1].Id.Should().Be("q1.expenses");
    }

    [Fact]
    public void Fixed_HydratedCellValues_ExposedThroughCellViewModelValue()
    {
        var initial = new Dictionary<string, Dictionary<string, string>>
        {
            ["q1"] = new() { ["revenue"] = "100000", ["expenses"] = "80000" },
        };
        var vm = new SectionViewModel(FixedTableSection(initial), NewFactory(), depth: 0);

        var q1Revenue = vm.NestedSections[0].Cells.First(c => c.ColumnId == "revenue");
        q1Revenue.Value.Should().Be("100000");
    }

    [Fact]
    public void Fixed_TypingIntoCellViewModel_PropagatesToUnderlyingPromptResponse()
    {
        var sectionModel = FixedTableSection();
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0);
        var q1Revenue = vm.NestedSections[0].Cells.First(c => c.ColumnId == "revenue");

        q1Revenue.Value = "100000";

        // The underlying Prompt's Response — what gets persisted to the .aprf —
        // is the source of truth and it's a plain string.
        var cellPrompt = sectionModel.Sections[0].Prompts.First(p => p.Id == "q1.revenue");
        cellPrompt.Response.Should().Be("100000");
    }

    [Fact]
    public void Fixed_FreeTextInTypedColumn_StoredAsString_VisionInvariant()
    {
        var sectionModel = FixedTableSection();
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0);
        var q1Revenue = vm.NestedSections[0].Cells.First(c => c.ColumnId == "revenue");

        q1Revenue.Value = "approximately a hundred grand";

        // Currency-typed column accepts arbitrary text — type hints are advisory only.
        sectionModel.Sections[0].Prompts.First(p => p.Id == "q1.revenue")
            .Response.Should().Be("approximately a hundred grand");
    }

    // ── Dynamic rows ──

    [Fact]
    public void Dynamic_EmptyTable_StartsWithZeroRows()
    {
        var vm = new SectionViewModel(DynamicTableSection(), NewFactory(), depth: 0);

        vm.IsDynamicTable.Should().BeTrue();
        vm.NestedSections.Should().BeEmpty();
        vm.CanAddRow.Should().BeTrue();
        vm.CanRemoveRow.Should().BeFalse();
    }

    [Fact]
    public void Dynamic_AddRow_AppendsRowSubSection_WithCellPromptsInColumnOrder()
    {
        var sectionModel = DynamicTableSection(rowLabel: "Item");
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0);

        vm.AddRow();

        vm.NestedSections.Should().HaveCount(1);
        var row = vm.NestedSections[0];
        row.Title.Should().Be("Item 1");
        row.Cells.Select(c => c.ColumnId).Should().Equal("desc", "qty");

        // The model gained a real Section + cell Prompts, addressable by id —
        // database imports iterate the prompt tree directly.
        sectionModel.Sections.Should().HaveCount(1);
        var rowSection = sectionModel.Sections[0];
        rowSection.Prompts.Should().HaveCount(2);
        rowSection.Prompts[0].Id.Should().Be($"{row.Id}.desc");
        rowSection.Prompts[1].Id.Should().Be($"{row.Id}.qty");
    }

    [Fact]
    public void Dynamic_AtMaxRows_AddCommandCannotExecute()
    {
        var vm = new SectionViewModel(DynamicTableSection(maxRows: 2), NewFactory(), depth: 0);

        vm.AddRow();
        vm.AddRow();

        vm.CanAddRow.Should().BeFalse();
        vm.AddRowCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Dynamic_RemoveRow_DropsRow_AndRenumbersRemaining()
    {
        var sectionModel = DynamicTableSection();
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0);
        vm.AddRow(); vm.AddRow(); vm.AddRow();
        vm.NestedSections[0].Cells.First(c => c.ColumnId == "desc").Value = "first";
        vm.NestedSections[1].Cells.First(c => c.ColumnId == "desc").Value = "second";
        vm.NestedSections[2].Cells.First(c => c.ColumnId == "desc").Value = "third";

        vm.RemoveRow(vm.NestedSections[0]);

        vm.NestedSections.Should().HaveCount(2);
        vm.NestedSections[0].Cells.First(c => c.ColumnId == "desc").Value.Should().Be("second");
        vm.NestedSections[0].Title.Should().Be("Item 1", "remaining rows must renumber after removal");
        vm.NestedSections[1].Title.Should().Be("Item 2");
        sectionModel.Sections.Should().HaveCount(2);
    }

    [Fact]
    public void Dynamic_AtMinRows_RemoveCommandCannotExecute()
    {
        var sectionModel = DynamicTableSection(minRows: 1);
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0);
        vm.AddRow();

        vm.CanRemoveRow.Should().BeFalse();
        vm.RemoveRowCommand.CanExecute(vm.NestedSections[0]).Should().BeFalse();
    }

    [Fact]
    public void Dynamic_AddRow_NotifiesHostOfNewPromptVms()
    {
        // The shell relies on this callback to (a) track new cell prompt VMs in
        // its progress/advisory walks and (b) subscribe to their Response events.
        var added = new List<PromptViewModelBase>();
        var sectionModel = DynamicTableSection();
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0,
            onPromptAdded: added.Add, onPromptRemoved: null);

        vm.AddRow();

        added.Should().HaveCount(2, "one cell-prompt VM per column");
        added.Select(p => p.Id).Should().BeEquivalentTo(
            new[] { vm.NestedSections[0].Cells[0].ColumnId, vm.NestedSections[0].Cells[1].ColumnId }
                .Select(col => $"{vm.NestedSections[0].Id}.{col}"));
    }

    [Fact]
    public void Dynamic_RemoveRow_NotifiesHostOfRemovedPromptVms()
    {
        var removed = new List<PromptViewModelBase>();
        var vm = new SectionViewModel(DynamicTableSection(), NewFactory(), depth: 0,
            onPromptAdded: _ => { }, onPromptRemoved: removed.Add);
        vm.AddRow();

        vm.RemoveRow(vm.NestedSections[0]);

        removed.Should().HaveCount(2);
    }

    // ── Vision invariants ──

    [Fact]
    public void EveryCellPromptResponse_IsString_RegardlessOfColumnType()
    {
        var sectionModel = FixedTableSection();
        var vm = new SectionViewModel(sectionModel, NewFactory(), depth: 0);

        // currency-typed column gets pure non-numeric text — must round-trip as string.
        vm.NestedSections[0].Cells.First(c => c.ColumnId == "revenue").Value = "see notes";
        vm.NestedSections[1].Cells.First(c => c.ColumnId == "expenses").Value = "TBD";

        sectionModel.Sections[0].Prompts.First(p => p.Id == "q1.revenue").Response.Should().Be("see notes");
        sectionModel.Sections[1].Prompts.First(p => p.Id == "q2.expenses").Response.Should().Be("TBD");
    }

    [Fact]
    public void RegularSection_DoesNotExposeTableMembers()
    {
        var vm = new SectionViewModel(new Section { Id = "s", Title = "X" }, NewFactory(), depth: 0);

        vm.IsTableSection.Should().BeFalse();
        vm.Cells.Should().BeEmpty();
        vm.Columns.Should().BeEmpty();
        vm.CanAddRow.Should().BeFalse();
    }
}

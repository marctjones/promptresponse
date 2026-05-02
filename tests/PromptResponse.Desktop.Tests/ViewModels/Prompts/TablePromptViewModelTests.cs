using System.Text.Json.Nodes;
using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Prompts;

/// <summary>
/// Tests for the table prompt's per-cell editor view-model. The serialized response
/// round-trips correctly for both fixed-row and dynamic-row tables, every cell
/// stays a string (vision invariant), and add/remove for dynamic rows respects
/// MinRows / MaxRows advisory bounds.
/// </summary>
public class TablePromptViewModelTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static Prompt FixedTablePrompt(string response = "")
    {
        return new Prompt
        {
            Id = "p", Label = "Quarterly results",
            Response = response,
            Hints = new PromptHints
            {
                ExpectedDataType = "table",
                TableDefinition = new TableDefinition
                {
                    Columns = new List<TableColumn>
                    {
                        new() { Id = "revenue", Label = "Revenue", Type = "currency" },
                        new() { Id = "expenses", Label = "Expenses", Type = "currency" },
                    },
                    FixedRows = new List<FixedRow>
                    {
                        new() { Id = "q1", Label = "Q1" },
                        new() { Id = "q2", Label = "Q2" },
                    },
                },
            },
        };
    }

    private static Prompt DynamicTablePrompt(string response = "", int minRows = 0, int maxRows = 100)
    {
        return new Prompt
        {
            Id = "p", Label = "Line items",
            Response = response,
            Hints = new PromptHints
            {
                ExpectedDataType = "table",
                TableDefinition = new TableDefinition
                {
                    Columns = new List<TableColumn>
                    {
                        new() { Id = "desc", Label = "Description" },
                        new() { Id = "qty", Label = "Qty", Type = "number" },
                        new() { Id = "price", Label = "Price", Type = "currency" },
                    },
                    DynamicRows = new DynamicRowConfig { MinRows = minRows, MaxRows = maxRows, RowLabel = "Item" },
                },
            },
        };
    }

    // ── Construction + hydration ──

    [Fact]
    public void Fixed_Construction_MaterializesOneRowPerFixedRow_AllCellsEmpty()
    {
        var vm = new TablePromptViewModel(FixedTablePrompt(), NewService());
        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Id.Should().Be("q1");
        vm.Rows[0].Label.Should().Be("Q1");
        vm.Rows[0].Cells.Should().HaveCount(2);
        vm.Rows[0].Cells[0].Value.Should().BeEmpty();
    }

    [Fact]
    public void Fixed_HydrateFromExistingResponseJson_PopulatesCellValues()
    {
        var json = """{"q1":{"revenue":"100000","expenses":"80000"},"q2":{"revenue":"120000","expenses":"90000"}}""";
        var vm = new TablePromptViewModel(FixedTablePrompt(json), NewService());

        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Cells.First(c => c.ColumnId == "revenue").Value.Should().Be("100000");
        vm.Rows[0].Cells.First(c => c.ColumnId == "expenses").Value.Should().Be("80000");
        vm.Rows[1].Cells.First(c => c.ColumnId == "revenue").Value.Should().Be("120000");
    }

    [Fact]
    public void Fixed_MalformedJsonResponse_FallsBackToEmptyCells_ResponsePreserved()
    {
        // Vision invariant — free-text in a table prompt's response stays valid;
        // we just can't hydrate cells from it. The user keeps their text intact.
        var prompt = FixedTablePrompt("this is not JSON");
        var vm = new TablePromptViewModel(prompt, NewService());
        vm.Rows.Should().HaveCount(2);  // structurally we still show fixed rows
        vm.Rows[0].Cells.Should().AllSatisfy(c => c.Value.Should().BeEmpty());
    }

    [Fact]
    public void Fixed_TypingIntoCell_SerializesToResponseJson()
    {
        var vm = new TablePromptViewModel(FixedTablePrompt(), NewService());

        vm.Rows[0].Cells.First(c => c.ColumnId == "revenue").Value = "100000";

        var parsed = JsonNode.Parse(vm.Response) as JsonObject;
        parsed.Should().NotBeNull();
        parsed!["q1"]!["revenue"]!.GetValue<string>().Should().Be("100000");
    }

    [Fact]
    public void Fixed_FreeTextInTypedColumn_StaysFreeText()
    {
        // Vision: typing "approximately a hundred grand" into a currency-typed column
        // is valid and stored as-is — the column type is advisory only.
        var vm = new TablePromptViewModel(FixedTablePrompt(), NewService());

        vm.Rows[0].Cells.First(c => c.ColumnId == "revenue").Value = "approximately a hundred grand";

        var parsed = JsonNode.Parse(vm.Response) as JsonObject;
        parsed!["q1"]!["revenue"]!.GetValue<string>().Should().Be("approximately a hundred grand");
    }

    // ── Dynamic rows ──

    [Fact]
    public void Dynamic_EmptyResponse_StartsWithZeroRows()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(), NewService());
        vm.Rows.Should().BeEmpty();
        vm.IsDynamicTable.Should().BeTrue();
    }

    [Fact]
    public void Dynamic_MinRowsHonoredOnHydrate_PadsUpToMinimum()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(minRows: 2), NewService());
        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Label.Should().Be("Item 1");
        vm.Rows[1].Label.Should().Be("Item 2");
    }

    [Fact]
    public void Dynamic_AddRow_AppendsAndSerializes()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(), NewService());

        vm.AddRow();
        vm.AddRow();

        vm.Rows.Should().HaveCount(2);
        vm.Rows[1].Label.Should().Be("Item 2");
        var arr = JsonNode.Parse(vm.Response) as JsonArray;
        arr.Should().NotBeNull();
        arr!.Count.Should().Be(2);
    }

    [Fact]
    public void Dynamic_AtMaxRows_AddCommandCannotExecute()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(maxRows: 2), NewService());
        vm.AddRow();
        vm.AddRow();
        vm.CanAddRow.Should().BeFalse();
        vm.AddRowCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Dynamic_RemoveRow_RemovesAndRelabelsRemaining()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(), NewService());
        vm.AddRow(); vm.AddRow(); vm.AddRow();
        vm.Rows[0].Cells.First(c => c.ColumnId == "desc").Value = "first";
        vm.Rows[1].Cells.First(c => c.ColumnId == "desc").Value = "second";
        vm.Rows[2].Cells.First(c => c.ColumnId == "desc").Value = "third";

        vm.RemoveRow(vm.Rows[0]);

        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Cells.First(c => c.ColumnId == "desc").Value.Should().Be("second");
        vm.Rows[0].Label.Should().Be("Item 1", "remaining rows must renumber after removal");
        vm.Rows[1].Label.Should().Be("Item 2");
    }

    [Fact]
    public void Dynamic_AtMinRows_RemoveCommandCannotExecute()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(minRows: 1), NewService());
        // Hydration padded to the min.
        vm.Rows.Should().HaveCount(1);
        vm.CanRemoveRow.Should().BeFalse();
        vm.RemoveRowCommand.CanExecute(vm.Rows[0]).Should().BeFalse();
    }

    [Fact]
    public void Dynamic_HydrateFromArrayResponse_PopulatesCells_AndPreservesRowIds()
    {
        var json = """[{"__rowId":"abc","desc":"Widget","qty":"3","price":"9.99"},{"__rowId":"def","desc":"Gadget","qty":"1","price":"19.99"}]""";
        var vm = new TablePromptViewModel(DynamicTablePrompt(json), NewService());

        vm.Rows.Should().HaveCount(2);
        vm.Rows[0].Id.Should().Be("abc");
        vm.Rows[0].Cells.First(c => c.ColumnId == "desc").Value.Should().Be("Widget");
        vm.Rows[1].Cells.First(c => c.ColumnId == "qty").Value.Should().Be("1");
    }

    [Fact]
    public void Dynamic_RowIdsArePersisted_AcrossSerializeRoundTrip()
    {
        var vm = new TablePromptViewModel(DynamicTablePrompt(), NewService());
        vm.AddRow();
        var firstRowId = vm.Rows[0].Id;

        vm.Rows[0].Cells.First().Value = "anything";  // triggers serialize

        var arr = JsonNode.Parse(vm.Response) as JsonArray;
        arr.Should().NotBeNull();
        arr!.Count.Should().Be(1);
        ((JsonObject)arr[0]!)["__rowId"]!.GetValue<string>().Should().Be(firstRowId);
    }

    // ── Vision invariants ──

    [Fact]
    public void EveryCellValueIsAlwaysString_RegardlessOfColumnType()
    {
        var prompt = FixedTablePrompt();
        // currency-typed columns get pure non-numeric text — must round-trip as string.
        var vm = new TablePromptViewModel(prompt, NewService());
        vm.Rows[0].Cells.First(c => c.ColumnId == "revenue").Value = "see notes";
        vm.Rows[1].Cells.First(c => c.ColumnId == "expenses").Value = "TBD";

        var parsed = JsonNode.Parse(vm.Response) as JsonObject;
        parsed!["q1"]!["revenue"]!.GetValue<string>().Should().Be("see notes");
        parsed!["q2"]!["expenses"]!.GetValue<string>().Should().Be("TBD");
    }

    [Fact]
    public void NoDefinition_IsHandled_RowsEmpty_ResponseUntouched()
    {
        var prompt = new Prompt { Id = "p", Label = "L", Response = "free text", Hints = new PromptHints() };
        var vm = new TablePromptViewModel(prompt, NewService());
        vm.HasDefinition.Should().BeFalse();
        vm.Rows.Should().BeEmpty();
        vm.Response.Should().Be("free text");
    }
}

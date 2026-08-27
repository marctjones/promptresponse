using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Live-rendered smoke tests for table-section rendering in <see cref="SectionView"/>.
///
/// These tests run on the real Avalonia visual tree under the production theme
/// (via <see cref="HeadlessAppBuilder"/>). They guard against the "VM tests
/// pass while the live app shows nothing" failure mode by asserting on the
/// materialized visual tree: column header TextBlocks, row labels, and cell
/// TextBox editors must all be present, with the cell ↔ Prompt.Response wire
/// actually intact.
/// </summary>
public class TableSectionViewGuiTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static (SectionView view, SectionViewModel vm, Section model) BuildFixedTable()
    {
        // A table is a section marked kind=table: rows are child sections, cells are
        // their prompts, and a column header is simply the corresponding prompt's label.
        var columns = new[] { (Id: "revenue", Label: "Revenue"), (Id: "expenses", Label: "Expenses") };
        var section = new Section { Id = "tbl", Title = "Quarterly", Kind = "table" };
        foreach (var (rowId, rowTitle) in new[] { ("q1", "Q1"), ("q2", "Q2") })
        {
            var rowSection = new Section { Id = rowId, Title = rowTitle };
            foreach (var col in columns)
            {
                rowSection.Prompts.Add(new Prompt
                {
                    Id = $"{rowId}.{col.Id}",
                    Label = col.Label,
                    Hints = new PromptHints { ExpectedDataType = "currency" },
                });
            }
            section.Sections.Add(rowSection);
        }
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var vm = new SectionViewModel(section, factory, depth: 0);
        var view = new SectionView { DataContext = vm };
        return (view, vm, section);
    }

    [AvaloniaFact]
    public void FixedTable_RendersColumnHeadersInVisualTree()
    {
        var (view, _, _) = BuildFixedTable();
        view.ShowInWindow(width: 900, height: 600);

        // Both column labels must appear as TextBlocks somewhere in the tree —
        // proves the Columns ItemsControl actually materialized its template.
        var textBlocks = view.GetVisualDescendants().OfType<TextBlock>().ToList();
        textBlocks.Select(t => t.Text).Should().Contain("Revenue");
        textBlocks.Select(t => t.Text).Should().Contain("Expenses");
    }

    [AvaloniaFact]
    public void FixedTable_RendersOneCellEditorPerRowPerColumn()
    {
        var (view, vm, _) = BuildFixedTable();
        view.ShowInWindow(width: 900, height: 600);

        // 2 rows × 2 columns = 4 cell editors. The TextBoxes whose DataContext
        // is a TableCellViewModel are the cells (other TextBoxes might be in
        // unrelated parts of the page chrome).
        var cellTextBoxes = view.GetVisualDescendants().OfType<TextBox>()
            .Where(tb => tb.DataContext is TableCellViewModel)
            .ToList();
        cellTextBoxes.Should().HaveCount(4);

        // Spot-check: each row's cells reference distinct TableCellViewModels
        // matching the model.
        var allCellVms = vm.NestedSections.SelectMany(r => r.Cells).ToHashSet();
        cellTextBoxes.Select(tb => tb.DataContext).Should().BeEquivalentTo(allCellVms);
    }

    [AvaloniaFact]
    public void FixedTable_TypingIntoCellEditor_UpdatesUnderlyingPromptResponse()
    {
        var (view, _, model) = BuildFixedTable();
        view.ShowInWindow(width: 900, height: 600);

        // Find the cell editor for q1.revenue and set its Text. The two-way
        // binding through TableCellViewModel.Value must propagate to the
        // underlying Prompt.Response in the model.
        var revenue = view.GetVisualDescendants().OfType<TextBox>()
            .First(tb => tb.DataContext is TableCellViewModel c && c.ColumnId == "revenue"
                         && ((TableCellViewModel)tb.DataContext).Value == string.Empty);
        revenue.Text = "100000";

        model.Sections[0].Prompts.First(p => p.Id == "q1.revenue")
            .Response.Should().Be("100000",
                "the cell editor's two-way binding must reach the model — otherwise " +
                "what the user sees diverges from what gets saved");
    }

    [AvaloniaFact]
    public void RegularSection_DoesNotRenderTableGrid()
    {
        // Sanity: a non-table section (no TableLayout) must NOT show the table
        // header row. This is the "table layout doesn't leak into regular
        // sections" check.
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var section = new Section { Id = "s", Title = "Plain section",
            Prompts = new List<Prompt> {
                new() { Id = "p1", Label = "Hello", Hints = new PromptHints() }
            },
        };
        var vm = new SectionViewModel(section, factory, depth: 0);
        var view = new SectionView { DataContext = vm };
        view.ShowInWindow(width: 900, height: 600);

        view.GetVisualDescendants().OfType<TextBox>()
            .Any(tb => tb.DataContext is TableCellViewModel)
            .Should().BeFalse("regular sections must not render any TableCellViewModel-bound editors");
    }

    [AvaloniaFact]
    public void DynamicTable_AddRowButton_AppendsRowEditorsLive()
    {
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        var section = new Section
        {
            Id = "tbl",
            Title = "Items",
            Kind = "table",
            CanAddRows = "true",
            MaxRows = "5",
        };
        var vm = new SectionViewModel(section, factory, depth: 0);
        var view = new SectionView { DataContext = vm };
        view.ShowInWindow(width: 900, height: 600);

        // Initially: zero cell editors (empty dynamic table).
        view.GetVisualDescendants().OfType<TextBox>()
            .Count(tb => tb.DataContext is TableCellViewModel).Should().Be(0);

        vm.AddRowCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        view.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        view.GetVisualDescendants().OfType<TextBox>()
            .Count(tb => tb.DataContext is TableCellViewModel).Should().Be(1,
                "after AddRow, one cell editor for the new row's single column must materialize");
    }
}

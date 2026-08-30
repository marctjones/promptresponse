using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditorMutationTests
{
    [Fact]
    public void ConvertToFixedTable_PromotesSection_WithDefaultColumnAndRow()
    {
        var (vm, model, _, _) = NewBlankSection();

        vm.ConvertToFixedTable();

        vm.IsTableSection.Should().BeTrue();
        vm.IsFixedTable.Should().BeTrue();
        vm.Columns.Should().HaveCount(1);
        vm.NestedSections.Should().HaveCount(1, "fixed tables start with one starter row");
        vm.NestedSections[0].PromptViewModels.Should().HaveCount(1, "starter row has one cell per starter column");
        model.Kind.Should().Be("table");
    }

    [Fact]
    public void ConvertToFixedTable_DropsPreexistingPromptsOnTheSection_NotifiesHost()
    {
        var (vm, model, _, removed) = NewBlankSection();
        var preexisting = vm.AddPrompt();

        vm.ConvertToFixedTable();

        vm.PromptViewModels.Should().BeEmpty();
        model.Prompts.Should().BeEmpty();
        removed.Should().Contain(preexisting,
            "the host must hear about every removed prompt so subscriptions are released");
    }

    [Fact]
    public void ConvertToDynamicTable_StartsWithOneInstance()
    {
        var (vm, _, _, _) = NewBlankSection();

        vm.ConvertToDynamicTable();

        vm.IsTableSection.Should().BeTrue();
        vm.IsDynamicTable.Should().BeTrue();
        vm.NestedSections.Should().HaveCount(1);
        vm.Columns.Should().NotBeEmpty();
    }

    [Fact]
    public void RemoveTableLayout_RevertsToRegularSection_AndDetachesAllCellPrompts()
    {
        var (vm, model, _, removed) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddFixedRow();

        vm.RemoveTableLayout();

        vm.IsTableSection.Should().BeFalse();
        model.Kind.Should().BeNull();
        vm.NestedSections.Should().NotBeEmpty("rows survive as ordinary child sections");
        removed.Should().BeEmpty("no prompt is detached — nothing was owned by a table layout");
    }

    [Fact]
    public void AddColumn_AddsCellPromptToEveryExistingRow()
    {
        var (vm, _, _, _) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddFixedRow();

        vm.AddColumn();

        vm.Columns.Should().HaveCount(2);
        foreach (var rowVm in vm.NestedSections)
        {
            rowVm.PromptViewModels.Should().HaveCount(2,
                "every row gets a fresh cell prompt for the new column so the grid stays rectangular");
        }
    }

    [Fact]
    public void RemoveColumn_DropsCellPromptsFromEveryRow_AndNotifiesHost()
    {
        var (vm, _, _, removed) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();
        vm.AddFixedRow();
        var colToRemove = vm.Columns[1];
        var removedBefore = removed.Count;

        vm.RemoveColumn(colToRemove);

        vm.Columns.Should().HaveCount(1);
        foreach (var rowVm in vm.NestedSections) rowVm.PromptViewModels.Should().HaveCount(1);
        (removed.Count - removedBefore).Should().Be(2, "two cell prompts were under the removed column");
    }

    [Fact]
    public void AddColumn_AppearsInLiveBoundColumnsCollection()
    {
        var (vm, _, _, _) = NewBlankSection();
        vm.ConvertToFixedTable();
        var initialCount = vm.Columns.Count;
        var observableTriggered = false;
        if (vm.Columns is System.Collections.Specialized.INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += (_, _) => observableTriggered = true;
        }

        vm.AddColumn();

        vm.Columns.Count.Should().Be(initialCount + 1, "the bound Columns collection must reflect the add");
        observableTriggered.Should().BeTrue(
            "Columns must raise CollectionChanged so ItemsControl re-renders with the new column inline-editable");
    }

    [Fact]
    public void RemoveColumn_DropsFromLiveBoundColumnsCollection()
    {
        var (vm, _, _, _) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();
        var col2 = vm.Columns[1];
        var initialCount = vm.Columns.Count;

        vm.RemoveColumn(col2);

        vm.Columns.Count.Should().Be(initialCount - 1);
        vm.Columns.Should().NotContain(col2);
    }

    [Fact]
    public void RemoveColumn_RefusesToDropTheLastColumn()
    {
        var (vm, _, _, _) = NewBlankSection();
        vm.ConvertToFixedTable();

        vm.RemoveColumn(vm.Columns[0]);

        vm.Columns.Should().HaveCount(1, "a table must keep at least one column");
    }

    [Fact]
    public void AddFixedRow_AppendsRowSubSectionWithCellPromptPerColumn()
    {
        var (vm, _, added, _) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();
        var addedBefore = added.Count;

        vm.AddFixedRow();

        vm.NestedSections.Last().PromptViewModels.Should().HaveCount(2);
        (added.Count - addedBefore).Should().Be(2, "two new cell prompt VMs reach the host");
    }

    [Fact]
    public void AddFixedRow_CopiesColumnShapeWithoutSharingMutableSuggestedValues()
    {
        var (vm, model, _, _) = NewBlankSection();
        vm.ConvertToFixedTable();
        var source = model.Sections[0].Prompts[0];
        source.Label = "Service";
        source.Hints.Placeholder = "Describe the service";
        source.Hints.SuggestedValues.Add("Consulting");

        vm.AddFixedRow();

        var copied = model.Sections[1].Prompts[0];
        copied.Id.Should().Be("s1.row2.col1");
        copied.Label.Should().Be("Service");
        copied.Hints.Placeholder.Should().Be("Describe the service");
        copied.Hints.SuggestedValues.Should().ContainSingle().Which.Should().Be("Consulting");
        copied.Hints.SuggestedValues.Should().NotBeSameAs(source.Hints.SuggestedValues,
            "each row owns its mutable choices after inheriting the table column shape");
    }
}

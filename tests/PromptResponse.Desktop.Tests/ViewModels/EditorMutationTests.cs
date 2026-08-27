using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Tests for the structural editing surface on <see cref="SectionViewModel"/>
/// and <see cref="PromptViewModelBase"/>: every mutation propagates to the
/// underlying model so a save persists exactly what the user authored. Add /
/// remove / convert-to-table / column ops keep the model + VM trees consistent
/// and notify the host of prompt VM lifecycle so the shell can keep its
/// progress + advisory subscriptions in sync.
/// </summary>
public class EditorMutationTests
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

    private static (SectionViewModel vm, Section model, List<PromptViewModelBase> added, List<PromptViewModelBase> removed) NewBlankSection()
    {
        var added = new List<PromptViewModelBase>();
        var removed = new List<PromptViewModelBase>();
        var section = new Section { Id = "s1", Title = "S", Prompts = new List<Prompt>() };
        var vm = new SectionViewModel(section, NewFactory(), depth: 0,
            onPromptAdded: added.Add, onPromptRemoved: removed.Add);
        return (vm, section, added, removed);
    }

    // ── Title / Description editing ──

    [Fact]
    public void SectionTitle_TwoWayBound_PersistsToModel()
    {
        var (vm, model, _, _) = NewBlankSection();

        vm.Title = "Personal Information";

        vm.Title.Should().Be("Personal Information");
        model.Title.Should().Be("Personal Information",
            "the editor's two-way title binding must reach the model so a save persists it");
    }

    [Fact]
    public void SectionDescription_TwoWayBound_PersistsToModel()
    {
        var (vm, model, _, _) = NewBlankSection();

        vm.Description = "Standard contact details";

        model.Description.Should().Be("Standard contact details");
        vm.HasDescription.Should().BeTrue();
    }

    // ── Prompt label / type / hints editing ──

    [Fact]
    public void PromptLabel_TwoWayBound_PersistsToModel()
    {
        var (vm, model, _, _) = NewBlankSection();
        var promptVm = vm.AddPrompt();

        promptVm.Label = "Full legal name";

        promptVm.Label.Should().Be("Full legal name");
        model.Prompts[0].Label.Should().Be("Full legal name");
    }

    [Fact]
    public void PromptExpectedDataType_TwoWayBound_PersistsToModel_AndTriggersDisplayValueRefresh()
    {
        var (vm, model, _, _) = NewBlankSection();
        var promptVm = vm.AddPrompt();
        var displayChanges = 0;
        promptVm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PromptViewModelBase.DisplayValue)) displayChanges++; };

        promptVm.ExpectedDataType = "currency";

        promptVm.ExpectedDataType.Should().Be("currency");
        model.Prompts[0].Hints.ExpectedDataType.Should().Be("currency");
        displayChanges.Should().BeGreaterThan(0, "type changes affect rendered display, so DisplayValue must be re-evaluated");
    }

    [Fact]
    public void PromptHints_PlaceholderHelpTextPattern_PersistToModel()
    {
        var (vm, model, _, _) = NewBlankSection();
        var p = vm.AddPrompt();

        p.Placeholder = "you@example.com";
        p.HelpText = "Enter your work email";
        p.ValidationPattern = @"^[^@]+@[^@]+\.[^@]+$";

        var saved = model.Prompts[0].Hints;
        saved.Placeholder.Should().Be("you@example.com");
        saved.HelpText.Should().Be("Enter your work email");
        saved.ValidationPattern.Should().Be(@"^[^@]+@[^@]+\.[^@]+$");
    }

    // ── Add / remove prompt ──

    [Fact]
    public void AddPrompt_AppendsDefaultPromptAndNotifiesHost()
    {
        var (vm, model, added, _) = NewBlankSection();

        var newVm = vm.AddPrompt();

        vm.PromptViewModels.Should().Contain(newVm);
        model.Prompts.Should().HaveCount(1);
        model.Prompts[0].Hints.ExpectedDataType.Should().Be("text",
            "default prompts are text-typed so the user can immediately rename + retype");
        added.Should().Contain(newVm,
            "the host needs to subscribe to the new VM's Response changes for progress + advisories");
    }

    [Fact]
    public void RemovePrompt_DropsPromptFromModelAndVMTree_AndNotifiesHost()
    {
        var (vm, model, _, removed) = NewBlankSection();
        var p1 = vm.AddPrompt();
        var p2 = vm.AddPrompt();

        vm.RemovePrompt(p1);

        vm.PromptViewModels.Should().NotContain(p1);
        vm.PromptViewModels.Should().Contain(p2);
        model.Prompts.Should().HaveCount(1);
        removed.Should().Contain(p1);
    }

    // ── Add / remove nested section ──

    [Fact]
    public void AddNestedSection_AppendsAndPropagatesCallbacks()
    {
        var (vm, model, added, _) = NewBlankSection();

        var child = vm.AddNestedSection();
        child.AddPrompt();

        vm.NestedSections.Should().Contain(child);
        model.Sections.Should().HaveCount(1);
        added.Should().HaveCount(1, "the prompt added inside the nested child must reach the host's subscription");
    }

    [Fact]
    public void RemoveNestedSection_DetachesAllPromptsRecursively()
    {
        var (vm, _, added, removed) = NewBlankSection();
        var child = vm.AddNestedSection();
        var grandchild = child.AddNestedSection();
        grandchild.AddPrompt();
        grandchild.AddPrompt();

        vm.RemoveNestedSection(child);

        vm.NestedSections.Should().BeEmpty();
        removed.Should().HaveCount(2,
            "removing a section subtree must detach every prompt under it (including grandchildren)");
    }

    // ── Table authoring ──

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

        // Prompts that were directly on the section are gone — table sections
        // hold structure (rows + cells), not free-floating prompts.
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
        // A table always carries at least one instance — it is what names the fields.
        vm.NestedSections.Should().HaveCount(1);
        vm.Columns.Should().NotBeEmpty();
    }

    [Fact]
    public void RemoveTableLayout_RevertsToRegularSection_AndDetachesAllCellPrompts()
    {
        var (vm, model, _, removed) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddFixedRow();
        // Two rows × one column = two cell prompt VMs.

        vm.RemoveTableLayout();

        vm.IsTableSection.Should().BeFalse();
        model.Kind.Should().BeNull();

        // Dropping table-ness now costs nothing: rows were only ever ordinary child
        // sections and cells only ever ordinary prompts, so they simply stop being
        // presented as a grid. There is no layout object whose removal destroys data.
        vm.NestedSections.Should().NotBeEmpty("rows survive as ordinary child sections");
        removed.Should().BeEmpty("no prompt is detached — nothing was owned by a table layout");
    }

    [Fact]
    public void AddColumn_AddsCellPromptToEveryExistingRow()
    {
        var (vm, _, _, _) = NewBlankSection();
        vm.ConvertToFixedTable();
        vm.AddFixedRow(); // two rows now

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
        vm.AddColumn(); // 2 columns
        vm.AddFixedRow(); // 2 rows × 2 columns = 4 cells

        var colToRemove = vm.Columns[1];
        var removedBefore = removed.Count;

        vm.RemoveColumn(colToRemove);

        vm.Columns.Should().HaveCount(1);
        foreach (var rowVm in vm.NestedSections)
        {
            rowVm.PromptViewModels.Should().HaveCount(1);
        }
        (removed.Count - removedBefore).Should().Be(2, "two cell prompts were under the removed column");
    }

    [Fact]
    public void AddColumn_AppearsInLiveBoundColumnsCollection()
    {
        // Regression: the column-list editor's ItemsControl is bound to the
        // SectionViewModel.Columns collection. The previous implementation
        // returned the underlying List<TableColumn> directly, so adding a
        // column kept the same list reference and the ItemsControl never
        // re-fetched — the user couldn't see the new column to edit it
        // until they switched away and came back.
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
        vm.AddColumn(); // 2 columns
        var col2 = vm.Columns[1];
        var initialCount = vm.Columns.Count;

        vm.RemoveColumn(col2);

        vm.Columns.Count.Should().Be(initialCount - 1);
        vm.Columns.Should().NotContain(col2);
    }

    [Fact]
    public void RemoveColumn_RefusesToDropTheLastColumn()
    {
        // A table with zero columns is meaningless; the editor should refuse
        // (the user can RemoveTableLayout if they want to abandon the table).
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
        vm.AddColumn(); // 2 columns
        var addedBefore = added.Count;

        vm.AddFixedRow();

        vm.NestedSections.Last().PromptViewModels.Should().HaveCount(2);
        (added.Count - addedBefore).Should().Be(2, "two new cell prompt VMs reach the host");
    }

    // ── Top-level section management (via shell-style direct list mutation) ──

    [Fact]
    public void AddPromptOnNestedSection_WiresCallbackThroughToHost()
    {
        // Regression guard: when callbacks propagate through nested SectionViewModels,
        // a prompt added deep in the tree still reaches the host's subscription.
        var (vm, _, added, _) = NewBlankSection();
        var child = vm.AddNestedSection();
        var grandchild = child.AddNestedSection();

        var deep = grandchild.AddPrompt();

        added.Should().Contain(deep,
            "constructor-injected callbacks must propagate to grandchild SectionViewModels");
    }
}

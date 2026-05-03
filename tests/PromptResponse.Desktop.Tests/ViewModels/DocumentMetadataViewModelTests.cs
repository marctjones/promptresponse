using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Editing;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Document metadata wrapper exposes every Metadata field as a two-way property
/// that writes through to the model and (when an EditHistory is supplied) records
/// each edit as a mergeable property-edit command. Without an EditHistory, edits
/// still propagate but bypass undo — used by tests and by short-lived dialogs.
/// </summary>
public class DocumentMetadataViewModelTests
{
    private static Metadata NewMetadata() => new()
    {
        Title = "Original",
        Description = "original description",
        Author = "alex",
        TemplateId = "tpl-1",
        TemplateVersion = "0.1.0",
    };

    [Fact]
    public void Constructor_NullMetadata_Throws()
    {
        Action act = () => new DocumentMetadataViewModel(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TitleSetter_PropagatesToModel_AndRaisesPropertyChanged_AndChanged()
    {
        var meta = NewMetadata();
        var vm = new DocumentMetadataViewModel(meta);
        var props = new List<string?>();
        var changedFired = 0;
        vm.PropertyChanged += (_, e) => props.Add(e.PropertyName);
        vm.Changed += (_, _) => changedFired++;

        vm.Title = "Renamed";

        meta.Title.Should().Be("Renamed", "setter must reach the underlying Metadata model");
        props.Should().Contain(nameof(vm.Title));
        changedFired.Should().Be(1, "Changed is what the shell uses to mark the document dirty");
    }

    [Fact]
    public void NullTitle_StoredAsEmptyString_NotNull()
    {
        // Title is non-nullable on the model; the VM coalesces null → "".
        var meta = NewMetadata();
        var vm = new DocumentMetadataViewModel(meta);

        vm.Title = null!;

        meta.Title.Should().Be(string.Empty);
    }

    [Fact]
    public void DescriptionAuthorTemplateIdVersion_AllPropagateToModel()
    {
        var meta = NewMetadata();
        var vm = new DocumentMetadataViewModel(meta);

        vm.Description = "new desc";
        vm.Author = "blair";
        vm.TemplateId = "tpl-2";
        vm.TemplateVersion = "1.0.0";

        meta.Description.Should().Be("new desc");
        meta.Author.Should().Be("blair");
        meta.TemplateId.Should().Be("tpl-2");
        meta.TemplateVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void NoOpAssignment_DoesNotRaisePropertyChanged_OrRecordEdit()
    {
        var meta = NewMetadata();
        var history = new EditHistory();
        var vm = new DocumentMetadataViewModel(meta, history);
        var props = new List<string?>();
        vm.PropertyChanged += (_, e) => props.Add(e.PropertyName);

        // Re-assign the same value the model already has.
        vm.Title = "Original";

        props.Should().BeEmpty("setting a property to its current value must short-circuit");
        history.CanUndo.Should().BeFalse("a no-op assignment must not pollute undo history");
    }

    // ── Undo / redo ──

    [Fact]
    public void TitleEdit_WithHistory_IsUndoableAndRedoable()
    {
        var meta = NewMetadata();
        var history = new EditHistory();
        var vm = new DocumentMetadataViewModel(meta, history);

        vm.Title = "Renamed";

        history.CanUndo.Should().BeTrue();
        history.Undo();
        vm.Title.Should().Be("Original");
        meta.Title.Should().Be("Original", "undo must reach the model, not just the VM cache");

        history.Redo();
        vm.Title.Should().Be("Renamed");
        meta.Title.Should().Be("Renamed");
    }

    [Fact]
    public void ConsecutiveTitleKeystrokes_MergeIntoSingleUndoStep()
    {
        var meta = NewMetadata();
        var history = new EditHistory();
        var vm = new DocumentMetadataViewModel(meta, history);

        vm.Title = "P";
        vm.Title = "Pe";
        vm.Title = "Per";

        history.Undo();
        vm.Title.Should().Be("Original",
            "metadata field keystrokes within the merge window must collapse to one undo step");
        history.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void EditsToDifferentMetadataFields_DoNotMerge()
    {
        var meta = NewMetadata();
        var history = new EditHistory();
        var vm = new DocumentMetadataViewModel(meta, history);

        vm.Title = "New title";
        vm.Author = "blair";

        // Two separate undo steps.
        history.Undo();
        meta.Author.Should().Be("alex", "Author edit reverted");
        meta.Title.Should().Be("New title", "Title edit still applied");

        history.Undo();
        meta.Title.Should().Be("Original");
    }

    [Fact]
    public void IsApplying_BypassesHistoryRecord_ButStillUpdatesModelAndNotifies()
    {
        // When a command is being replayed by Undo/Redo, IsApplying is true on the
        // history and setters must propagate the value without recording a NEW
        // command (that would corrupt the stack).
        var meta = NewMetadata();
        var history = new EditHistory();
        var vm = new DocumentMetadataViewModel(meta, history);

        var props = new List<string?>();
        vm.PropertyChanged += (_, e) => props.Add(e.PropertyName);

        history.Execute(new PropertyEditCommand<string>(
            target: vm, propertyName: nameof(vm.Title),
            apply: v => vm.Title = v,
            oldValue: meta.Title, newValue: "ViaCommand"));

        meta.Title.Should().Be("ViaCommand");
        history.CanUndo.Should().BeTrue();
        // After Undo + Redo, IsApplying short-circuits — no new commands recorded.
        history.Undo();
        history.Redo();
        meta.Title.Should().Be("ViaCommand");
        // Stack still has exactly one redo'able command at top.
        history.CanRedo.Should().BeFalse();
        history.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void NoHistory_StillUpdatesModel_ButNoUndoRecorded()
    {
        var meta = NewMetadata();
        var vm = new DocumentMetadataViewModel(meta);

        vm.Title = "New";
        vm.Author = "B";

        meta.Title.Should().Be("New");
        meta.Author.Should().Be("B");
        // Sanity: no history was passed, so nothing to undo through.
    }
}

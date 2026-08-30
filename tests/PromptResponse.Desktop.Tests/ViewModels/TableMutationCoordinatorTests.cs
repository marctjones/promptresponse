using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public sealed class TableMutationCoordinatorTests
{
    [Fact]
    public void ConvertToTable_Undo_RestoresDirectPromptAndBalancesHostLifecycle()
    {
        var original = new Prompt { Id = "name", Label = "Name" };
        var section = new Section { Id = "contact", Title = "Contact", Prompts = [original] };
        var added = new List<PromptViewModelBase>();
        var removed = new List<PromptViewModelBase>();
        var history = new EditHistory();
        var viewModel = new SectionViewModel(
            section,
            NewFactory(),
            depth: 0,
            onPromptAdded: added.Add,
            onPromptRemoved: removed.Add,
            history);

        viewModel.ConvertToFixedTable();
        history.Undo();

        section.Kind.Should().BeNull();
        section.Prompts.Should().ContainSingle().Which.Id.Should().Be(original.Id,
            "undo restores the snapshot's direct prompt data, rather than leaving table cells attached");
        removed.Should().HaveCount(3,
            "the history command restores its captured table snapshot before undo removes its cell again");
        added.Should().HaveCount(3,
            "both snapshot restoration and undo announce reconstructed prompt VMs to the host");
        removed.Last().Id.Should().Be("contact.row1.name");
        added.Last().Model.Id.Should().Be(original.Id);
    }

    private static PromptViewModelFactory NewFactory() => new(new ProfileService(
        new FixedAccessibilityProbe(),
        applyAffordanceDefaults: false));

    private sealed class FixedAccessibilityProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }
}

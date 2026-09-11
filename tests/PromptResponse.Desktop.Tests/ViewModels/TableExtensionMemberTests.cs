using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// A table edit must not be a way to lose members the editor does not recognise.
/// </summary>
/// <remarks>
/// Specification 5.8: an unrecognised member present on read is still present,
/// unchanged, on write (APR-MODEL-021). Table editing snapshots the section tree
/// for undo/redo, so a snapshot that dropped extension members made undo destroy
/// them - silently, on a document belonging to whoever used a newer producer or
/// carried their own `com.example.*` data.
/// </remarks>
public class TableExtensionMemberTests
{
    [Fact]
    public void Undo_KeepsExtensionMembersOnTableAndCell()
    {
        var (table, viewModel, history) = Table();

        viewModel.AddColumn();
        history.Undo();

        table.Extensions.Should().ContainKey("tableLayout",
            "an unprefixed member is still preserved as data: reading one is a warning, not a loss");
        table.Sections[0].Prompts[0].Extensions.Should().ContainKey("com.example.priority")
            .WhoseValue.GetRawText().Should().Be("2");
    }

    [Fact]
    public void Redo_KeepsExtensionMembersOnTableAndCell()
    {
        var (table, viewModel, history) = Table();

        viewModel.AddColumn();
        history.Undo();
        history.Redo();

        table.Extensions.Should().ContainKey("tableLayout");
        table.Sections[0].Prompts[0].Extensions.Should().ContainKey("com.example.priority");
    }

    [Fact]
    public void Undo_KeepsExtensionMembersOnACellsHints()
    {
        var (table, viewModel, history) = Table();

        viewModel.AddColumn();
        history.Undo();

        table.Sections[0].Prompts[0].Hints.Extensions.Should().ContainKey("com.example.widget");
    }

    [Fact]
    public void Undo_KeepsTheCellsRoleAndBoundsHints()
    {
        // Role and the bounds hints were as absent from the old hand-written snapshot
        // as the extension members were, and were lost the same way.
        var (table, viewModel, history) = Table();

        viewModel.AddColumn();
        history.Undo();

        var cell = table.Sections[0].Prompts[0];
        cell.Role.Should().Be("nurse");
        cell.Hints.Min.Should().Be(1);
        cell.Hints.Max.Should().Be(10);
        cell.Hints.Step.Should().Be(0.5);
        cell.Hints.ValidationPattern.Should().Be("^[0-9]+$");
    }

    [Fact]
    public void Undo_OfATableConversionKeepsExtensionMembersOnTheDirectPrompt()
    {
        // Converting to a table moves direct prompts into the first row; undo puts
        // them back, and what comes back has to be what was there.
        var prompt = new Prompt
        {
            Id = "name",
            Label = "Name",
            Extensions = Members(("com.example.priority", "2")),
        };
        var section = new Section { Id = "contact", Title = "Contact", Prompts = [prompt] };
        var history = new EditHistory();
        var viewModel = new SectionViewModel(section, NewFactory(), depth: 0, _ => { }, _ => { }, history);

        viewModel.ConvertToFixedTable();
        history.Undo();

        section.Prompts.Should().ContainSingle()
            .Which.Extensions.Should().ContainKey("com.example.priority");
    }

    private static (Section Table, SectionViewModel ViewModel, EditHistory History) Table()
    {
        var cell = new Prompt
        {
            Id = "tbl.row1.priority",
            Label = "Priority",
            Role = "nurse",
            Extensions = Members(("com.example.priority", "2")),
            Hints = new PromptHints
            {
                ExpectedDataType = "number",
                ValidationPattern = "^[0-9]+$",
                Min = 1,
                Max = 10,
                Step = 0.5,
                Extensions = Members(("com.example.widget", "\"dial\"")),
            },
        };
        var table = new Section
        {
            Id = "tbl",
            Title = "Tasks",
            Kind = "table",
            CanAddRows = true,
            // Unprefixed, as a document written by a producer that ignored APR-MODEL-031
            // would carry it. A reader preserves it anyway; only writing one is refused.
            Extensions = Members(("tableLayout", "\"grid\"")),
            Sections = [new Section { Id = "tbl.row1", Title = "Row 1", Prompts = [cell] }],
        };
        var history = new EditHistory();
        var viewModel = new SectionViewModel(table, NewFactory(), depth: 0, _ => { }, _ => { }, history);
        return (table, viewModel, history);
    }

    private static Dictionary<string, JsonElement> Members(params (string Name, string Json)[] members)
        => members.ToDictionary(
            member => member.Name,
            member => JsonDocument.Parse(member.Json).RootElement.Clone(),
            StringComparer.Ordinal);

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

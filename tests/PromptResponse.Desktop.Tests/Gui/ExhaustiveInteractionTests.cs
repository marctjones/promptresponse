using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;
using Xunit;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Activates every interactive element in the shell and records what it touched.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written tests cover the flows someone thought to write down. This covers the
/// rest: every button is clicked, every text box typed into, every combo and check and
/// radio operated. It asserts the two things that must hold no matter which control was
/// hit - the application does not throw, and the document stays structurally valid.
/// </para>
/// <para>
/// It writes tests/gui-exercised.json, so coverage is measured by what was actually
/// activated at runtime rather than by which control names happen to appear in test
/// source. That distinction matters: a name can be mentioned in a comment.
/// </para>
/// </remarks>
public class ExhaustiveInteractionTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    // Controls that end the session, replace the document out from under the walk, or
    // open a native picker the harness cannot serve. Excluding the document-replacing
    // ones matters: clicking "New" early detaches every control discovered after it, so
    // the driver would silently stop exercising most of the interface.
    private static readonly string[] Excluded =
    [
        "Exit", "Quit", "Close window", "New document", "New template", "Open", "Save as", "Browse",
    ];

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null && !File.Exists(Path.Combine(d.FullName, "PromptResponse.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string? NameOf(Visual v) =>
        v.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) as string
        ?? (v as ContentControl)?.Content?.ToString();

    private static AprDocument Doc() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Exhaustive", TemplateId = "t" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Fields",
                Prompts =
                [
                    new Prompt { Id = "text", Label = "Text", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "num", Label = "Number", Hints = new PromptHints { ExpectedDataType = "number" } },
                    new Prompt { Id = "bool", Label = "Boolean", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                    new Prompt { Id = "date", Label = "Date", Hints = new PromptHints { ExpectedDataType = "date" } },
                    new Prompt { Id = "sel", Label = "Select", Hints = new PromptHints { ExpectedDataType = "select", SuggestedValues = ["A", "B"] } },
                    new Prompt { Id = "multi", Label = "Multi", Hints = new PromptHints { ExpectedDataType = "multichoice", SuggestedValues = ["X", "Y"] } },
                ],
            },
            new Section
            {
                Id = "tbl", Title = "Table", Kind = "table", CanAddRows = "true",
                Sections = [new Section { Id = "r1", Title = "Row 1", Prompts =
                    [new Prompt { Id = "r1.a", Label = "A", Response = "1" }] }],
            },
        ],
    };

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryInteractiveElement_CanBeActivated_WithoutBreakingTheDocument(bool editMode)
    {
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: true);
        var shell = new MainShellViewModel(Substitute.For<IFileService>(), Substitute.For<IDialogService>(),
            session, profile, new PromptViewModelFactory(profile));

        var doc = Doc();
        if (editMode) doc.DocumentType = DocumentType.Template;
        session.Set(doc, null);
        if (editMode != shell.IsEditMode) shell.ToggleEditModeCommand.Execute(null);

        var view = new MainShellView { DataContext = shell };
        var window = view.ShowInWindow(width: 1400, height: 900);
        for (var i = 0; i < 4; i++) { window.UpdateLayout(); GuiTestExtensions.PumpDispatcher(); }

        var exercised = new List<object>();
        var failures = new List<string>();
        var surface = editMode ? "shell.edit" : "shell.fill";

        // Activating a control can rebuild the tree, so re-snapshot each round and keep
        // going until a round reaches nothing new. A single snapshot would miss every
        // control that only materialises after an earlier click.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var round = 0; round < 6; round++)
        {
        var targets = view.GetVisualDescendants().OfType<Control>()
            .Where(c => c is Button or CheckBox or RadioButton or ComboBox or TextBox or ToggleButton or Expander)
            .Where(c => c is not RepeatButton)
            .ToList();
        var reachedThisRound = 0;

        foreach (var control in targets)
        {
            var label = NameOf(control);
            if (label is null || Excluded.Any(x => label.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;
            if (!control.IsEnabled) continue;
            var key = control.GetType().Name + "|" + label;
            if (!seen.Add(key)) continue;
            reachedThisRound++;

            try
            {
                switch (control)
                {
                    case TextBox tb when !tb.IsReadOnly:
                        tb.Text = "N/A — typed by the exhaustive driver";
                        break;
                    case ComboBox cb when cb.ItemCount > 0:
                        cb.SelectedIndex = 0;
                        break;
                    case CheckBox or RadioButton or ToggleButton:
                        ((ToggleButton)control).IsChecked = true;
                        break;
                    case Expander ex:
                        ex.IsExpanded = !ex.IsExpanded;
                        break;
                    case Button b:
                        if (b.Command?.CanExecute(b.CommandParameter) == true)
                        {
                            b.Command.Execute(b.CommandParameter);
                        }
                        break;
                }
                GuiTestExtensions.PumpDispatcher();
                exercised.Add(new { surface, kind = control.GetType().Name, label });
            }
            catch (Exception ex)
            {
                failures.Add($"{surface} {control.GetType().Name} '{label}': {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
            }
        }

        window.UpdateLayout();
        GuiTestExtensions.PumpDispatcher();
        if (reachedThisRound == 0) break;
        }

        // Merge into the exercised register so coverage reflects runtime activation.
        var path = Path.Combine(RepoRoot(), "tests", "gui-exercised.json");
        var existing = File.Exists(path)
            ? JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(path))!
                .Where(e => e.GetProperty("surface").GetString() != surface)
                .Select(e => (object)new
                {
                    surface = e.GetProperty("surface").GetString()!,
                    kind = e.GetProperty("kind").GetString()!,
                    label = e.GetProperty("label").GetString()!,
                }).ToList()
            : [];
        existing.AddRange(exercised);
        File.WriteAllText(path, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));

        failures.Should().BeEmpty("no control may throw when activated");
        exercised.Should().NotBeEmpty("the driver must actually reach controls");

        // Whatever was clicked, the document must still be a valid document.
        if (session.CurrentDocument is { } current)
        {
            var result = new DocumentValidator().Validate(current);
            result.IsValid.Should().BeTrue(
                "clicking around an application must never produce an invalid document, but it produced: "
                + string.Join("; ", result.Errors.Take(3).Select(e => $"{e.ErrorCode} at {e.PropertyPath}"))
                + " | structure: " + string.Join(", ", current.Sections.Select(sec =>
                    $"{sec.Id}[kind={sec.Kind ?? "-"} prompts={sec.Prompts.Count} subs={sec.Sections.Count}]")));
        }
    }
}

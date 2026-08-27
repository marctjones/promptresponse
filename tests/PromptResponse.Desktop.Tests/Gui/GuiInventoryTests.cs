using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;
using Xunit;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Walks the rendered visual tree and writes an inventory of every interactive element
/// to tests/gui-inventory.json.
/// </summary>
/// <remarks>
/// Enumerating the interface by hand goes stale the moment someone adds a button, and
/// driving a live application with synthetic clicks is unreliable and cannot see what it
/// missed. Walking the tree headlessly is exhaustive, repeatable, and produces a register
/// that can be diffed: a new control that nothing tests shows up as an untested entry
/// rather than as silence.
/// </remarks>
public class GuiInventoryTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private sealed record Element(
        string Surface, string Kind, string? Name, string? Text, bool Enabled, bool CanFocus);

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null && !File.Exists(Path.Combine(d.FullName, "PromptResponse.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static AprDocument Doc() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Inventory", TemplateId = "t", Description = "d" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Every hint type",
                Prompts =
                [
                    new Prompt { Id = "text", Label = "Text", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "multiline", Label = "Multiline", Hints = new PromptHints { ExpectedDataType = "multiline" } },
                    new Prompt { Id = "num", Label = "Number", Response = "not a number", Hints = new PromptHints { ExpectedDataType = "number" } },
                    new Prompt { Id = "cur", Label = "Currency", Hints = new PromptHints { ExpectedDataType = "currency" } },
                    new Prompt { Id = "bool", Label = "Boolean", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                    new Prompt { Id = "date", Label = "Date", Hints = new PromptHints { ExpectedDataType = "date" } },
                    new Prompt { Id = "sel", Label = "Select", Hints = new PromptHints { ExpectedDataType = "select", SuggestedValues = ["A", "B"] } },
                    new Prompt { Id = "multi", Label = "Multichoice", Hints = new PromptHints { ExpectedDataType = "multichoice", SuggestedValues = ["X", "Y"] } },
                    new Prompt { Id = "email", Label = "Email", Hints = new PromptHints { ExpectedDataType = "email" } },
                    new Prompt { Id = "phone", Label = "Phone", Hints = new PromptHints { ExpectedDataType = "phone" } },
                    new Prompt { Id = "url", Label = "Url", Hints = new PromptHints { ExpectedDataType = "url" } },
                    new Prompt { Id = "file", Label = "File", Hints = new PromptHints { ExpectedDataType = "file" } },
                    new Prompt { Id = "sig", Label = "Signature", Hints = new PromptHints { ExpectedDataType = "signature" } },
                ],
            },
            new Section
            {
                Id = "tbl", Title = "Table", Kind = "table", CanAddRows = "true",
                Sections =
                [
                    new Section { Id = "r1", Title = "Row 1", Prompts =
                    [
                        new Prompt { Id = "r1.a", Label = "A", Response = "1" },
                        new Prompt { Id = "r1.b", Label = "B", Response = "2" },
                    ] },
                ],
            },
        ],
    };

    private static IEnumerable<Element> Walk(Visual root, string surface)
    {
        foreach (var v in root.GetVisualDescendants())
        {
            var kind = v.GetType().Name;
            var interactive = v is Button or ToggleButton or CheckBox or RadioButton or ComboBox
                or TextBox or MenuItem or Menu or CalendarDatePicker or ListBox or Slider or Expander;
            if (!interactive) continue;

            var name = v.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) as string;
            var text = v switch
            {
                ContentControl cc => cc.Content?.ToString(),
                TextBox tb => tb.PlaceholderText ?? tb.Name,
                _ => null,
            };
            var input = v as InputElement;
            yield return new Element(surface, kind, name, Truncate(text), input?.IsEnabled ?? true, input?.Focusable ?? false);
        }
    }

    private static string? Truncate(string? s) =>
        s is null ? null : s.Length > 60 ? s[..60] : s;

    [AvaloniaFact]
    public void WriteInteractiveElementInventory()
    {
        var all = new List<Element>();

        foreach (var (surface, editMode) in new[] { ("shell.fill", false), ("shell.edit", true) })
        {
            var session = new DocumentSessionService();
            var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: true);
            var shell = new MainShellViewModel(Substitute.For<IFileService>(), Substitute.For<IDialogService>(),
                session, profile, new PromptViewModelFactory(profile));
            var doc = Doc();
            if (editMode) doc.DocumentType = DocumentType.Template;
            session.Set(doc, null);
            if (editMode && !shell.IsEditMode) shell.ToggleEditModeCommand.Execute(null);
            if (!editMode && shell.IsEditMode) shell.ToggleEditModeCommand.Execute(null);

            var view = new MainShellView { DataContext = shell };
            var window = view.ShowInWindow(width: 1400, height: 900);
            for (var i = 0; i < 4; i++) { window.UpdateLayout(); GuiTestExtensions.PumpDispatcher(); }
            all.AddRange(Walk(view, surface));
        }

        foreach (var (surface, control) in new (string, Control)[]
        {
            ("dialog.displayPreferences", new DisplayPreferencesView { DataContext = new DisplayPreferencesViewModel(new ProfileService(new StubProbe(), applyAffordanceDefaults: false)) }),
            ("dialog.keyboardShortcuts", new KeyboardShortcutsDialog()),
            ("dialog.about", new AboutDialog()),
        })
        {
            try
            {
                if (control is Window w) { w.Show(); GuiTestExtensions.PumpDispatcher(); w.UpdateLayout(); all.AddRange(Walk(w, surface)); }
                else { var win = control.ShowInWindow(width: 900, height: 700); win.UpdateLayout(); GuiTestExtensions.PumpDispatcher(); all.AddRange(Walk(control, surface)); }
            }
            catch (Exception ex)
            {
                all.Add(new Element(surface, "COULD_NOT_RENDER", ex.GetType().Name, ex.Message.Split('\n')[0], false, false));
            }
        }

        var bySurface = all.GroupBy(e => e.Surface)
            .ToDictionary(g => g.Key, g => (object)new
            {
                total = g.Count(),
                unnamed = g.Count(e => string.IsNullOrWhiteSpace(e.Name) && string.IsNullOrWhiteSpace(e.Text)),
                byKind = g.GroupBy(e => e.Kind).OrderByDescending(k => k.Count())
                          .ToDictionary(k => k.Key, k => k.Count()),
                elements = g.OrderBy(e => e.Kind, StringComparer.Ordinal)
                            .ThenBy(e => e.Name ?? e.Text ?? "", StringComparer.Ordinal).ToList(),
            });

        var payload = new
        {
            comment = "Interactive elements discovered by walking the rendered visual tree headlessly. "
                    + "Regenerated by GuiInventoryTests; do not hand-edit.",
            generatedBy = "tests/PromptResponse.Desktop.Tests/Gui/GuiInventoryTests.cs",
            totals = new { elements = all.Count, surfaces = bySurface.Count },
            surfaces = bySurface,
        };

        var outPath = Path.Combine(RepoRoot(), "tests", "gui-inventory.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { WriteIndented = true }));

        all.Should().NotBeEmpty("the inventory must actually discover controls");
    }
}

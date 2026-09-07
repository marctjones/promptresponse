// The Avalonia desktop client's renderer-conformance driver.
//
// It boots the shipped App headlessly, loads each case into the real MainShellView, and
// reports the interaction snapshot docs/RENDERER_CONFORMANCE.md defines — accessible
// names and roles read off the automation tree a screen reader consumes, the focus order
// from real Tab traversal, and the network requests the runtime actually made.
//
// Nothing here consults the document except to turn a control's AutomationId into a JSON
// pointer, which is a join and not a judgement. A driver that walked the model to describe
// the interface would report the document back and pass chapter 13 without rendering.
//
//     python3 scripts/run-renderer-conformance.py \
//         --driver "dotnet run --project tools/PromptResponse.RendererDriver.Avalonia --no-build -v q"
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop;

namespace PromptResponse.RendererDriver.Avalonia;

internal static class Program
{
    private const double Width = 1400, Height = 1000;

    private static int Main()
    {
        using var network = new NetworkWatch();
        var suite = JsonNode.Parse(Console.In.ReadToEnd())!.AsObject();

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var results = new JsonArray();
        foreach (var node in suite["cases"]!.AsArray())
        {
            var testCase = node!.AsObject();
            if (testCase["surface"]?.GetValue<string>() != "renderer") continue;
            results.Add(Answer(testCase, network));
        }

        Console.Out.Write(new JsonObject
        {
            ["implementation"] = new JsonObject
            {
                ["name"] = "PromptResponse.Desktop (Avalonia)",
                ["version"] = PromptResponse.Core.AprFormat.CurrentVersion,
                // Renders; does not export. The export rule belongs to the surface that does.
                ["surfaces"] = new JsonArray("renderer"),
            },
            ["results"] = results,
        }.ToJsonString());
        return 0;
    }

    private static JsonObject Answer(JsonObject testCase, NetworkWatch network)
    {
        var source = testCase["document"]!.GetValue<string>();
        var document = new AprBeta6Reader().ReadForm(source, AprRepresentation.Jsonc);
        var pointers = Pointers.Of(JsonNode.Parse(source)!.AsObject());

        // Rendered as a form somebody is filling in, which is the presentation chapter 13
        // legislates: labels as accessible names, help text associated, a computed field
        // still editable, a hint mismatch not blocking a save. The shell picks its views
        // from `documentType`, and a template opens in the editor instead — a real second
        // surface, and not the one these rules are about.
        var shown = new AprBeta6Reader().ReadForm(source, AprRepresentation.Jsonc);
        shown.DocumentType = DocumentType.FilledForm;

        var shell = Shell.Create();
        var mark = network.Mark();
        shell.Session.Set(shown, filePath: null);
        var window = new Window { Width = Width, Height = Height, Content = shell.View };
        window.Show();
        Settle(window);

        var snapshot = new JsonObject
        {
            ["id"] = testCase["id"]!.GetValue<string>(),
            ["nodes"] = Nodes(window, pointers),
            ["requests"] = new JsonArray([.. network.Since(mark).Select(r => (JsonNode)r!)]),
            // Saving is answered by validation, on the document as it was read: an
            // advisory is a warning and never stops a write, and APR-RENDER-006 is the
            // rule that a hint mismatch must not.
            ["saveResult"] = SaveResult(document),
        };
        window.Close();
        return snapshot;
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(Width, Height));
        window.Arrange(new Rect(0, 0, Width, Height));
        Dispatcher.UIThread.RunJobs();
    }

    private static JsonObject SaveResult(AprDocument document)
    {
        var report = new DocumentValidator().Validate(document);
        var blocking = report.Errors.FirstOrDefault();
        return new JsonObject
        {
            ["written"] = blocking is null,
            ["blockedBy"] = blocking is null ? null : $"{blocking.ErrorCode} at {blocking.PropertyPath}",
        };
    }

    /// <summary>One node per rendered section and prompt, in the order Tab visits them.</summary>
    private static JsonArray Nodes(Window window, IReadOnlyDictionary<string, string> pointers)
    {
        var order = FocusOrder.Of(window);
        var nodes = new JsonArray();
        var headers = Headers(window, nodes);

        foreach (var container in window.GetVisualDescendants().OfType<Control>())
        {
            var id = AutomationProperties.GetAutomationId(container);
            if (string.IsNullOrEmpty(id) || !pointers.TryGetValue(id, out var pointer)) continue;

            // What the pointer says it is, not what happens to sit inside it. A section
            // whose role came from the first field within it would report itself as a
            // textbox called "Full name", which looks like a renderer defect and is not.
            var isPrompt = pointer.Contains("/prompts/", StringComparison.Ordinal);
            var input = isPrompt ? FocusOrder.PrimaryInput(container) : null;
            var named = input ?? container;
            var node = new JsonObject
            {
                ["id"] = id,
                ["role"] = isPrompt ? Role(input) : "group",
                ["name"] = AutomationProperties.GetName(named)
                           ?? AutomationProperties.GetName(container) ?? string.Empty,
                ["documentPointer"] = pointer,
            };

            var help = AutomationProperties.GetHelpText(named)
                       ?? AutomationProperties.GetHelpText(container);
            if (!string.IsNullOrEmpty(help)) node["helpText"] = help;

            if (Column(container, headers) is { } column) node["columnHeader"] = column;

            if (input is not null)
            {
                node["editable"] = input is TextBox box ? !box.IsReadOnly && box.IsEnabled
                                                        : input.IsEnabled;
                node["value"] = input is TextBox text ? text.Text ?? string.Empty : string.Empty;
                if (order.TryGetValue(input, out var position)) node["keyboardOrder"] = position;
            }
            else if (order.TryGetValue(container, out var position))
            {
                node["keyboardOrder"] = position;
            }
            nodes.Add(node);
        }
        return nodes;
    }

    /// <summary>The column headers a table declares, as nodes its cells can name.</summary>
    /// <remarks>
    /// `AutomationProperties.IsColumnHeader` is what Avalonia carries on every backend;
    /// the UIA grid pattern interfaces exist only in Avalonia.Win32.Automation, which is
    /// why docs/RENDERER_CONFORMANCE.md expresses this as a property. The header a cell
    /// belongs to is read positionally, the same way a browser resolves a `td` in a column
    /// whose `th` carries no explicit reference.
    /// </remarks>
    private static IReadOnlyList<Control> Headers(Window window, JsonArray nodes)
    {
        var headers = window.GetVisualDescendants().OfType<Control>()
            .Where(AutomationProperties.GetIsColumnHeader)
            .ToList();
        for (var index = 0; index < headers.Count; index++)
        {
            nodes.Add(new JsonObject
            {
                ["id"] = $"column:{index}",
                ["role"] = "columnheader",
                ["name"] = AutomationProperties.GetName(headers[index]) ?? string.Empty,
                ["isColumnHeader"] = true,
            });
        }
        return headers;
    }

    /// <summary>Which column header names this cell, if it is a cell at all.</summary>
    private static string? Column(Control cell, IReadOnlyList<Control> headers)
    {
        if (headers.Count == 0) return null;
        var row = cell.GetVisualAncestors().OfType<Control>().FirstOrDefault(
            ancestor => !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(ancestor)));
        if (row is null) return null;
        var siblings = row.GetVisualDescendants().OfType<Control>()
            .Where(control => !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(control)))
            .ToList();
        var position = siblings.IndexOf(cell);
        return position >= 0 && position < headers.Count ? $"column:{position}" : null;
    }

    private static string Role(Control? input) => input switch
    {
        TextBox => "textbox",
        CheckBox or ToggleButton => "checkbox",
        ComboBox => "combobox",
        Button => "button",
        null => "group",
        _ => "text",
    };
}

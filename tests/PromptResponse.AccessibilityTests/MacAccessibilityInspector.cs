using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Inspects the live macOS accessibility tree exposed through System Events.
/// This is intentionally opt-in: macOS requires the test runner to be granted
/// Accessibility permission in Privacy &amp; Security before another application's
/// UI tree may be read.
/// </summary>
public sealed class MacAccessibilityInspector : IAccessibilityInspector
{
    public string Platform => "macOS (NSAccessibility/System Events)";

    public Task<bool> IsAvailableAsync() => Task.FromResult(
        OperatingSystem.IsMacOS() && AXIsProcessTrusted());

    public async Task<AccessibleElement?> FindElementByNameAsync(string name, string? role = null)
    {
        var tree = await GetAccessibilityTreeAsync("PromptResponse");
        if (tree is null) return null;
        return tree.FindDescendants(element =>
            string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (role is null || string.Equals(element.Role, role, StringComparison.OrdinalIgnoreCase))).FirstOrDefault();
    }

    public async Task<IReadOnlyList<AccessibleElement>> FindElementsByRoleAsync(string role)
    {
        var tree = await GetAccessibilityTreeAsync("PromptResponse");
        return tree is null
            ? Array.Empty<AccessibleElement>()
            : tree.FindDescendants(element => string.Equals(element.Role, role, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<AccessibleElement?> GetAccessibilityTreeAsync(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        if (!await IsAvailableAsync()) return null;

        var start = new ProcessStartInfo("/usr/bin/osascript")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-l");
        start.ArgumentList.Add("JavaScript");
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(TreeScript);
        // applicationName is argv, not JavaScript source.
        start.ArgumentList.Add(applicationName);

        try
        {
            using var process = Process.Start(start);
            if (process is null) return null;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) return null;
            using var json = JsonDocument.Parse(output);
            return Convert(json.RootElement);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or JsonException)
        {
            return null;
        }
    }

    public Task<AccessibilityValidationResult> ValidateElementAsync(AccessibleElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        var result = new AccessibilityValidationResult();
        if (string.IsNullOrWhiteSpace(element.Name))
            result.AddIssue(AccessibilityIssueSeverity.High, $"{element.Role} has no accessible name", "Provide an accessibility label.");
        if (string.IsNullOrWhiteSpace(element.Role))
            result.AddIssue(AccessibilityIssueSeverity.High, $"{element.Name} has no accessibility role", "Use a native semantic control.");
        return Task.FromResult(result);
    }

    private static AccessibleElement Convert(JsonElement json)
    {
        var element = new AccessibleElement
        {
            Name = json.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            Role = json.TryGetProperty("role", out var role) ? role.GetString() ?? string.Empty : string.Empty,
            Description = json.TryGetProperty("description", out var description) ? description.GetString() : null,
        };
        if (json.TryGetProperty("focused", out var focused) && focused.ValueKind == JsonValueKind.True)
            element.States.Add("focused");
        if (json.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                var converted = Convert(child);
                converted.Parent = element;
                element.Children.Add(converted);
            }
        }
        return element;
    }

    // Limits protect a diagnostic test from spending unbounded time in a large UI tree.
    // Every queried property is wrapped because macOS does not expose every AX attribute
    // on every control type.
    private const string TreeScript = """
        function safe(read, fallback) { try { return read(); } catch (_) { return fallback; } }
        function snapshot(element, depth, budget) {
          if (budget.count++ >= 500 || depth > 12) return {name:"", role:"truncated", children:[]};
          var children = safe(function () { return element.uiElements(); }, []);
          return {
            name: String(safe(function () { return element.name(); }, "")),
            role: String(safe(function () { return element.role(); }, "")),
            description: String(safe(function () { return element.description(); }, "")),
            focused: Boolean(safe(function () { return element.focused(); }, false)),
            children: children.map(function (child) { return snapshot(child, depth + 1, budget); })
          };
        }
        function run(argv) {
          var name = argv[0];
          var systemEvents = Application("System Events");
          var processes = systemEvents.processes();
          for (var i = 0; i < processes.length; i++) {
            if (String(safe(function () { return processes[i].name(); }, "")) === name) {
              return JSON.stringify(snapshot(processes[i], 0, {count: 0}));
            }
          }
          return "";
        }
        """;

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();
}

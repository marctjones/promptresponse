using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Linux accessibility inspector using AT-SPI2 (Assistive Technology Service Provider Interface).
/// </summary>
/// <remarks>
/// This implementation queries the AT-SPI2 D-Bus interface to inspect what assistive
/// technologies like Orca see. It provides the same information that screen readers use.
///
/// Requires:
/// - AT-SPI2 installed (at-spi2-core package)
/// - Accessibility bus running
/// - AVALONIA_ENABLE_ACCESSIBILITY=1 environment variable
/// </remarks>
public class LinuxAccessibilityInspector : IAccessibilityInspector
{
    public string Platform => "Linux (AT-SPI2)";

    public async Task<bool> IsAvailableAsync()
    {
        await Task.CompletedTask;

        // Check if running on Linux
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // Check if AT-SPI2 is available
        // Simple check: look for accessibility bus environment variable or process
        try
        {
            var atspiBus = Environment.GetEnvironmentVariable("AT_SPI_BUS");
            if (!string.IsNullOrEmpty(atspiBus))
            {
                return true;
            }

            // Check if at-spi2-registryd is running
            var processes = System.Diagnostics.Process.GetProcesses();
            return processes.Any(p => p.ProcessName.Contains("at-spi"));
        }
        catch
        {
            return false;
        }
    }

    public async Task<AccessibleElement?> FindElementByNameAsync(string name, string? role = null)
    {
        var tree = await GetAccessibilityTreeAsync("PromptResponse");
        if (tree == null) return null;

        return tree.FindDescendants(e =>
        {
            if (!e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return false;

            if (role != null && !e.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).FirstOrDefault();
    }

    public async Task<IReadOnlyList<AccessibleElement>> FindElementsByRoleAsync(string role)
    {
        var tree = await GetAccessibilityTreeAsync("PromptResponse");
        if (tree == null) return Array.Empty<AccessibleElement>();

        return tree.FindDescendants(e =>
            e.Role.Equals(role, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    public async Task<AccessibleElement?> GetAccessibilityTreeAsync(string applicationName)
    {
        try
        {
            // Approach 1: Use accerciser-helper if available (GNOME accessibility inspector CLI)
            // Approach 2: Use atspi2-tool if available
            // Approach 3: Direct D-Bus queries via busctl

            // For now, we use busctl to query the AT-SPI2 bus
            // This is a command-line approach that's more reliable than Tmds.DBus for complex queries

            var accessible = await QueryAtSpi2ViaBusctl(applicationName);
            return accessible;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error querying AT-SPI2: {ex.Message}");
            return null;
        }
    }

    private async Task<AccessibleElement?> QueryAtSpi2ViaBusctl(string applicationName)
    {
        try
        {
            // Check if busctl is available
            var busctlCheck = await RunCommandAsync("which", "busctl");
            if (string.IsNullOrWhiteSpace(busctlCheck))
            {
                Console.WriteLine("busctl not found. Install systemd for AT-SPI2 querying.");
                return null;
            }

            // Query the AT-SPI2 registry for available applications
            // org.a11y.atspi.Registry on session bus
            var registryOutput = await RunCommandAsync("busctl",
                "--user list | grep -i atspi || true");

            if (string.IsNullOrWhiteSpace(registryOutput))
            {
                Console.WriteLine("No AT-SPI2 registry found on session bus.");
                return null;
            }

            // For a full implementation, we would:
            // 1. Query org.a11y.atspi.Registry for applications
            // 2. Find the application matching applicationName
            // 3. Query its object tree
            // 4. Recursively build AccessibleElement tree

            // This is complex D-Bus work. For now, we provide a framework
            // that can be extended when we have a running app to test against.

            Console.WriteLine($"AT-SPI2 registry found. Full tree querying not yet implemented.");
            Console.WriteLine($"To complete: query D-Bus path /org/a11y/atspi/accessible/root");

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in QueryAtSpi2ViaBusctl: {ex.Message}");
            return null;
        }
    }

    private async Task<string> RunCommandAsync(string command, string arguments)
    {
        try
        {
            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return string.Empty;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<AccessibilityValidationResult> ValidateElementAsync(AccessibleElement element)
    {
        await Task.CompletedTask;
        var result = new AccessibilityValidationResult();

        // Validate name is set
        if (string.IsNullOrWhiteSpace(element.Name))
        {
            result.AddIssue(
                AccessibilityIssueSeverity.High,
                $"{element.Role} has no accessible name",
                "Set AutomationProperties.Name to provide a label for screen readers"
            );
        }

        // Validate interactive elements have roles
        var interactiveRoles = new[] { "button", "text field", "checkbox", "radio button", "combobox" };
        if (interactiveRoles.Contains(element.Role.ToLowerInvariant()))
        {
            if (string.IsNullOrWhiteSpace(element.Name))
            {
                result.AddIssue(
                    AccessibilityIssueSeverity.Critical,
                    $"Interactive element ({element.Role}) missing accessible name",
                    "Interactive elements MUST have accessible names for keyboard/screen reader users"
                );
            }
        }

        // Validate text fields have descriptions or help text
        if (element.Role.Equals("text field", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(element.Description))
            {
                result.AddIssue(
                    AccessibilityIssueSeverity.Low,
                    $"Text field '{element.Name}' has no description",
                    "Consider adding AutomationProperties.HelpText to provide guidance"
                );
            }
        }

        // Validate focusable elements
        if (interactiveRoles.Contains(element.Role.ToLowerInvariant()))
        {
            if (!element.States.Contains("focusable"))
            {
                result.AddIssue(
                    AccessibilityIssueSeverity.High,
                    $"Interactive element '{element.Name}' is not focusable",
                    "Ensure element can receive keyboard focus"
                );
            }
        }

        return result;
    }
}

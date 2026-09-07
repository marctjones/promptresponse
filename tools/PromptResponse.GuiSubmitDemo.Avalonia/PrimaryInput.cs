using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace PromptResponse.GuiSubmitDemo.Avalonia;

/// <summary>Finds the control inside a prompt's rendered view that a person types into.</summary>
/// <remarks>
/// Copied from <c>tools/PromptResponse.RendererDriver.Avalonia/FocusOrder.cs</c> rather than
/// shared across projects — each of these tools is a small, single-purpose driver, and a
/// shared library between two of them would be a third thing to keep in sync for no reader
/// this tool has. The descent stops at any control carrying its own AutomationId, because
/// that one is a different member of the document.
/// </remarks>
internal static class PrimaryInput
{
    internal static Control? Of(Control container)
    {
        foreach (var child in container.GetVisualChildren().OfType<Control>())
        {
            if (!string.IsNullOrEmpty(AutomationProperties.GetAutomationId(child))) continue;
            if (IsInput(child)) return child;
            if (Of(child) is { } found) return found;
        }
        return null;
    }

    private static bool IsInput(Control control) =>
        control is TextBox or CheckBox or ComboBox or ToggleButton
        && control.IsEffectivelyVisible && control.Focusable;
}

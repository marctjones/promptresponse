using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace PromptResponse.RendererDriver.Avalonia;

/// <summary>Where the keyboard actually goes.</summary>
/// <remarks>
/// Pressed rather than computed. `APR-RENDER-005` is about what a person can reach with a
/// keyboard, and a driver that reported the visual-tree order of everything that looks
/// focusable would be describing the markup rather than the behaviour — which is the one
/// thing a snapshot taken from a live application can do better than one taken from a
/// model.
/// </remarks>
internal static class FocusOrder
{
    private const int Limit = 400;

    internal static IReadOnlyDictionary<Control, int> Of(Window window)
    {
        var order = new Dictionary<Control, int>();
        for (var step = 0; step < Limit; step++)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            if (window.FocusManager?.GetFocusedElement() is not Control focused) break;
            if (!order.TryAdd(focused, order.Count)) break;   // the ring closed
        }
        return order;
    }

    /// <summary>The control inside a prompt's view that a person types into.</summary>
    /// <remarks>
    /// The descent stops at any control carrying an AutomationId of its own, because that
    /// one is a different member of the document. Without that a section would take its
    /// name and its role from the first field inside it, and report itself as a textbox
    /// called "Full name" — which looks like a renderer defect and is a driver defect.
    /// </remarks>
    internal static Control? PrimaryInput(Control container)
    {
        foreach (var child in container.GetVisualChildren().OfType<Control>())
        {
            if (!string.IsNullOrEmpty(AutomationProperties.GetAutomationId(child))) continue;
            if (IsInput(child)) return child;
            if (PrimaryInput(child) is { } found) return found;
        }
        return null;
    }

    private static bool IsInput(Control control) =>
        control is TextBox or CheckBox or ComboBox or ToggleButton
        && control.IsEffectivelyVisible && control.Focusable;
}

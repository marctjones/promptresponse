using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Helpers for headless Avalonia GUI tests: window setup, keyboard/mouse event injection,
/// and visual-tree queries.
/// </summary>
public static class GuiTestExtensions
{
    /// <summary>
    /// Wraps a control in a host <see cref="Window"/>, shows it headlessly, and runs layout
    /// so it has a valid visual tree before assertions.
    /// </summary>
    public static Window ShowInWindow(this Control content, double width = 800, double height = 600)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = content,
            SizeToContent = SizeToContent.Manual,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        // Force measure + arrange so children have valid Bounds before tests inspect them.
        window.Measure(new Avalonia.Size(width, height));
        window.Arrange(new Avalonia.Rect(0, 0, width, height));
        window.UpdateLayout();
        // Force a render frame so headless input dispatch finds the visual tree.
        Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// Pumps any queued Avalonia dispatcher work to completion. Call this after raising input
    /// events to let bindings, command execution, and rendering settle before asserting.
    /// </summary>
    public static void PumpDispatcher()
    {
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Sends a keyboard key press (down + up) to the focused element of the given window.
    /// </summary>
    public static void PressKey(this Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(KeyToPhysicalKey(key), modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Types text into the focused control by raising one key event per character.
    /// </summary>
    public static void TypeText(this Window window, string text)
    {
        foreach (var ch in text)
        {
            window.KeyTextInput(ch.ToString());
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Performs a left-button click at the centre of the supplied control's bounds.
    /// </summary>
    public static void ClickCenter(this Window window, Control control)
    {
        var bounds = control.Bounds;
        var transform = control.TransformToVisual(window);
        if (transform is null)
        {
            throw new InvalidOperationException($"Control '{control.GetType().Name}' is not in the visual tree of the supplied window.");
        }
        var topLeft = bounds.TopLeft.Transform(transform.Value);
        var center = topLeft + new Vector(bounds.Width / 2.0, bounds.Height / 2.0);

        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Walks the visual tree to find the first descendant of <typeparamref name="T"/> matching
    /// an optional predicate. Throws if not found.
    /// </summary>
    public static T FindDescendant<T>(this Visual root, Func<T, bool>? predicate = null) where T : Visual
    {
        foreach (var visual in root.GetVisualDescendants())
        {
            if (visual is T typed && (predicate is null || predicate(typed)))
            {
                return typed;
            }
        }
        throw new InvalidOperationException($"No descendant of type {typeof(T).Name} matched.");
    }

    /// <summary>
    /// Same as <see cref="FindDescendant{T}"/> but returns null instead of throwing.
    /// </summary>
    public static T? TryFindDescendant<T>(this Visual root, Func<T, bool>? predicate = null) where T : Visual
    {
        foreach (var visual in root.GetVisualDescendants())
        {
            if (visual is T typed && (predicate is null || predicate(typed)))
            {
                return typed;
            }
        }
        return null;
    }

    private static PhysicalKey KeyToPhysicalKey(Key key) => key switch
    {
        Key.Tab => PhysicalKey.Tab,
        Key.Enter => PhysicalKey.Enter,
        Key.Space => PhysicalKey.Space,
        Key.Escape => PhysicalKey.Escape,
        Key.Left => PhysicalKey.ArrowLeft,
        Key.Right => PhysicalKey.ArrowRight,
        Key.Up => PhysicalKey.ArrowUp,
        Key.Down => PhysicalKey.ArrowDown,
        Key.Home => PhysicalKey.Home,
        Key.End => PhysicalKey.End,
        Key.A => PhysicalKey.A,
        Key.B => PhysicalKey.B,
        Key.C => PhysicalKey.C,
        Key.D => PhysicalKey.D,
        Key.E => PhysicalKey.E,
        Key.F => PhysicalKey.F,
        Key.G => PhysicalKey.G,
        Key.H => PhysicalKey.H,
        Key.I => PhysicalKey.I,
        Key.J => PhysicalKey.J,
        Key.K => PhysicalKey.K,
        Key.L => PhysicalKey.L,
        Key.M => PhysicalKey.M,
        Key.N => PhysicalKey.N,
        Key.O => PhysicalKey.O,
        Key.P => PhysicalKey.P,
        Key.Q => PhysicalKey.Q,
        Key.R => PhysicalKey.R,
        Key.S => PhysicalKey.S,
        Key.T => PhysicalKey.T,
        Key.U => PhysicalKey.U,
        Key.V => PhysicalKey.V,
        Key.W => PhysicalKey.W,
        Key.X => PhysicalKey.X,
        Key.Y => PhysicalKey.Y,
        Key.Z => PhysicalKey.Z,
        _ => PhysicalKey.None
    };
}

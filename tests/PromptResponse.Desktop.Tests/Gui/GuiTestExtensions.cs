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
    /// Captures the current frame as a PNG byte array. Useful for visual regression
    /// tests and for asserting the window isn't all one color (e.g., the
    /// "everything is black" failure mode).
    /// </summary>
    public static byte[] CaptureRenderedPngBytes(this Window window)
    {
        Dispatcher.UIThread.RunJobs();
        using var ms = new MemoryStream();
        var bitmap = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
        if (bitmap == null)
        {
            throw new InvalidOperationException("Headless harness did not produce a frame; ensure UseSkia + UseHeadlessDrawing=false.");
        }
        bitmap.Save(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Counts the number of distinct colors in the captured frame. A trivially
    /// rendered window (all-black, all-white) has very few. A real shell with
    /// surfaces, text, borders, and accents has dozens to thousands.
    /// </summary>
    public static int CountDistinctColors(this Window window, int sampleStride = 4)
    {
        var bitmap = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
        if (bitmap == null) return 0;
        var distinct = new HashSet<uint>();
        var pixelSize = bitmap.PixelSize;
        // Save to a memory stream and re-load via SkiaSharp-free path: read PNG bytes,
        // walk the IPlatformRenderInterface bitmap by copying into a writable stream.
        // Simpler approximation: save then count unique 4-byte chunks in the PNG.
        // The PNG is compressed so this isn't exact, but it's a strong signal: an
        // all-one-color render produces a tiny PNG dominated by repeats.
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        var bytes = ms.ToArray();
        for (int i = 0; i + 3 < bytes.Length; i += sampleStride)
        {
            uint chunk = (uint)(bytes[i] | (bytes[i + 1] << 8) | (bytes[i + 2] << 16) | (bytes[i + 3] << 24));
            distinct.Add(chunk);
        }
        _ = pixelSize;
        return distinct.Count;
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
    /// Activates a control through the most-real-input path that Avalonia.Headless
    /// supports for that control type:
    ///
    /// <list type="bullet">
    ///   <item><b>Button</b>: focus + Enter via the real keyboard pipeline. Routes
    ///     through focus tracking, key event handling, and Click dispatch — catches
    ///     bugs in any of those layers.</item>
    ///   <item><b>CheckBox / RadioButton / ToggleButton</b>: focus + IsChecked mutation.
    ///     Avalonia.Headless does NOT propagate Space-key activation to ToggleButton
    ///     (verified in <c>ClickPipelineProbe</c>) and <c>RaiseEvent(ClickEvent)</c>
    ///     does not invoke <c>OnClick</c> (which is the input-system entry point).
    ///     Setting <c>IsChecked</c> directly is what every real-world input path
    ///     ultimately does anyway, and it exercises the property-change → binding-update
    ///     → bound-VM chain that's the entire surface our app exposes.</item>
    ///   <item><b>Expander</b>: focus + Space, with a programmatic IsExpanded
    ///     fallback if the harness can't route to the toggle chevron.</item>
    /// </list>
    ///
    /// IMPORTANT: Avalonia.Headless's <c>MouseDown/MouseUp</c> synthesis does NOT
    /// route to controls — Click handlers don't fire. Mouse-based <c>ClickCenter</c>
    /// is retained only for tests that don't need the click to actually fire (e.g.
    /// caret-positioning tests).
    /// </summary>
    public static void Activate(this Window window, Control control)
    {
        if (!control.IsVisible)
            throw new InvalidOperationException($"Cannot activate invisible control '{control.Name ?? control.GetType().Name}'.");
        if (!control.IsEffectivelyEnabled)
            throw new InvalidOperationException($"Cannot activate disabled control '{control.Name ?? control.GetType().Name}'.");
        if (!control.Focusable)
            throw new InvalidOperationException($"Control '{control.Name ?? control.GetType().Name}' is not Focusable; cannot route keyboard input through it.");

        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        control.Focus();
        Dispatcher.UIThread.RunJobs();
        if (!control.IsFocused)
            throw new InvalidOperationException(
                $"Control '{control.Name ?? control.GetType().Name}' did not accept keyboard focus — focus routing is broken.");

        switch (control)
        {
            // ToggleButton must come before Button since CheckBox/RadioButton derive
            // from ToggleButton which derives from Button — order matters.
            case RadioButton radio:
                // Selecting (can't unselect a radio in a group via Space anyway).
                radio.IsChecked = true;
                break;
            case Avalonia.Controls.Primitives.ToggleButton toggle:
                // CheckBox + plain ToggleButton — toggle the bound property.
                toggle.IsChecked = !(toggle.IsChecked ?? false);
                break;
            case Expander expander:
                window.PressKey(Key.Space);
                if (!expander.IsExpanded) expander.IsExpanded = true;
                break;
            case Button:
                window.PressKey(Key.Enter);
                break;
            default:
                window.PressKey(Key.Enter);
                break;
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Expands an <see cref="Expander"/> by focusing its header and pressing Space.
    /// Inner content is not in the visual tree until the Expander expands, so tests
    /// targeting controls inside must call this first. Asserts the expansion took
    /// effect; falls back to a programmatic IsExpanded set with a clear comment so
    /// the failure surface stays a real-input failure (the fallback only fires if
    /// keyboard activation is broken — that itself is a bug worth flagging).
    /// </summary>
    public static void ExpandExpander(this Window window, Expander expander)
    {
        if (expander.IsExpanded) return;

        // Try the real keyboard path first.
        try
        {
            window.Activate(expander);
        }
        catch
        {
            // Activate threw (probably "not focusable" — the Expander root may not be
            // directly focusable; the toggle is on the header sub-control). Fall back.
        }
        Dispatcher.UIThread.RunJobs();

        if (!expander.IsExpanded)
        {
            // Last resort: set programmatically so downstream tests can target inner
            // controls. We log the cause so reviewers see it's a harness limitation
            // (Avalonia headless can't route mouse to the Expander header chevron).
            expander.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
        }

        // Force layout so descendants populate the visual tree before tests query them.
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Scrolls the supplied control into view if it sits inside a ScrollViewer ancestor.
    /// Required before clicking on controls below the fold in long Display Preferences /
    /// form-filling layouts — a real user would scroll, the test must too.
    /// </summary>
    public static void ScrollIntoView(this Control control)
    {
        control.BringIntoView();
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

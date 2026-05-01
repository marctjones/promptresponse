using Avalonia.Controls;
using Avalonia.Input;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Wires an <see cref="IInputFormatter"/> to a <see cref="TextBox"/>: every time
/// the user types, the formatter is invoked, and (if the result differs) the
/// TextBox's text + caret are updated atomically. The behavior is gated on the
/// formatter's advertised <see cref="IInputFormatter.GateProfile"/> being active
/// in the supplied <see cref="IProfileService"/> — when that flag is off, typing
/// passes through untouched (universal-core behavior).
/// </summary>
/// <remarks>
/// Two distinct trigger points are supported:
///   <list type="bullet">
///     <item><b>Live</b> — formatter runs on every TextChanged. Right for digit-only
///       masks like phone/SSN/EIN/ZIP where reshape lands on natural boundaries
///       (after 3rd digit insert "(", after 6th insert "-").</item>
///     <item><b>OnCommit</b> — formatter runs only on LostFocus. Right for
///       currency/percentage where mid-typing reshape ("12." → "$12.00") fights
///       the user's intent.</item>
///   </list>
/// </remarks>
public static class InputMaskBehavior
{
    public enum Trigger
    {
        Live,
        OnCommit,
    }

    /// <summary>Attaches the formatter to the TextBox. Returns a disposable that
    /// removes the handlers when disposed (used in unit/GUI tests; live views
    /// hold the TextBox for their lifetime so detach is a no-op).</summary>
    public static IDisposable Attach(
        TextBox textBox,
        IInputFormatter formatter,
        IProfileService profileService,
        Trigger trigger = Trigger.Live)
    {
        if (textBox == null) throw new ArgumentNullException(nameof(textBox));
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
        if (profileService == null) throw new ArgumentNullException(nameof(profileService));

        var inFlight = false;

        void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (trigger != Trigger.Live) return;
            ApplyFormat();
        }

        void OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ApplyFormat();
        }

        void ApplyFormat()
        {
            if (inFlight) return;
            // Each formatter advertises its own gate flag (e.g. PhoneInputMaskProfile);
            // the mask only fires when that specific capability is in the active profile.
            if (!profileService.IsActive(formatter.GateProfile)) return;
            var raw = textBox.Text ?? string.Empty;
            var caret = textBox.CaretIndex;
            var result = formatter.Format(raw, caret);
            if (result.Text == raw && result.CaretIndex == caret) return;

            // Recursion guard: setting Text fires TextChanged again.
            inFlight = true;
            try
            {
                textBox.Text = result.Text;
                textBox.CaretIndex = Math.Clamp(result.CaretIndex, 0, result.Text.Length);
            }
            finally
            {
                inFlight = false;
            }
        }

        textBox.TextChanged += OnTextChanged;
        textBox.LostFocus += OnLostFocus;

        return new Disposer(() =>
        {
            textBox.TextChanged -= OnTextChanged;
            textBox.LostFocus -= OnLostFocus;
        });
    }

    private sealed class Disposer : IDisposable
    {
        private Action? _action;
        public Disposer(Action action) => _action = action;
        public void Dispose() { _action?.Invoke(); _action = null; }
    }
}

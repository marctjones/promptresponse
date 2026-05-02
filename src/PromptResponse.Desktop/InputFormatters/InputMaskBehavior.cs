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
///
/// Set the env var <c>PROMPTRESPONSE_TRACE_INPUT_MASK=1</c> to enable diagnostic
/// logging of every attach/text-change/gate-check/reshape decision. Used to chase
/// live-app reports that the auto-formatter doesn't fire when the user expects.
/// </remarks>
public static class InputMaskBehavior
{
    private static readonly bool TraceEnabled =
        Environment.GetEnvironmentVariable("PROMPTRESPONSE_TRACE_INPUT_MASK") is { Length: > 0 } v
        && v != "0" && !v.Equals("false", StringComparison.OrdinalIgnoreCase);

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

        Trace($"Attach: tb='{textBox.Name ?? "(unnamed)"}' formatter={formatter.GetType().Name} " +
              $"gate={formatter.GateProfile.Name} trigger={trigger}");

        var inFlight = false;

        void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            Trace($"OnTextChanged: tb='{textBox.Name}' text='{Truncate(textBox.Text)}' trigger={trigger} inFlight={inFlight}");
            if (trigger != Trigger.Live)
            {
                Trace("  → skip: trigger is OnCommit, not Live");
                return;
            }
            ApplyFormat();
        }

        void OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Trace($"OnLostFocus: tb='{textBox.Name}' text='{Truncate(textBox.Text)}'");
            ApplyFormat();
        }

        void ApplyFormat()
        {
            if (inFlight)
            {
                Trace("  → skip: re-entrant call (inFlight=true)");
                return;
            }
            // Each formatter advertises its own gate flag (e.g. PhoneInputMaskProfile);
            // the mask only fires when that specific capability is in the active profile.
            var gateActive = profileService.IsActive(formatter.GateProfile);
            Trace($"  gate {formatter.GateProfile.Name} active={gateActive}");
            if (!gateActive)
            {
                Trace("  → skip: gate flag not active — universal-core passthrough");
                return;
            }
            var raw = textBox.Text ?? string.Empty;
            var caret = textBox.CaretIndex;
            var result = formatter.Format(raw, caret);
            Trace($"  format: raw='{Truncate(raw)}' caret={caret} → text='{Truncate(result.Text)}' caret={result.CaretIndex}");
            if (result.Text == raw && result.CaretIndex == caret)
            {
                Trace("  → skip: formatter is identity for this input");
                return;
            }

            // Recursion guard: setting Text fires TextChanged again.
            inFlight = true;
            try
            {
                textBox.Text = result.Text;
                textBox.CaretIndex = Math.Clamp(result.CaretIndex, 0, result.Text.Length);
                Trace($"  → applied: tb.Text='{Truncate(textBox.Text)}' caret={textBox.CaretIndex}");
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

    private static void Trace(string message)
    {
        if (!TraceEnabled) return;
        Console.WriteLine($"[InputMask] {message}");
    }

    private static string Truncate(string? s)
    {
        if (s == null) return "(null)";
        return s.Length <= 40 ? s : s[..37] + "...";
    }

    private sealed class Disposer : IDisposable
    {
        private Action? _action;
        public Disposer(Action action) => _action = action;
        public void Dispose() { _action?.Invoke(); _action = null; }
    }
}

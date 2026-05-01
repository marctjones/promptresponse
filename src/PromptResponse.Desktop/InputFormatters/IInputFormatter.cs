namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Result of an input-mask formatting pass: the rewritten text plus the caret
/// position the TextBox should restore after assigning the new text.
/// </summary>
public readonly record struct FormatResult(string Text, int CaretIndex);

/// <summary>
/// Reshapes a raw user-typed string into a canonical visible form for a specific
/// type hint (phone, SSN, currency, …). Implementations MUST be non-destructive:
/// when the input doesn't unambiguously look like the formatter's type (e.g.
/// the user typed "see attached"), the input must be returned unchanged.
/// </summary>
/// <remarks>
/// Vision invariant: any visible text must remain a valid response. Formatters
/// reshape only when the user is clearly entering data of the expected type;
/// otherwise they pass through. The caller is responsible for capability gating
/// (only invoke when VisualFormatting is active).
/// </remarks>
public interface IInputFormatter
{
    /// <summary>
    /// The capability-profile flag that gates this formatter. <see cref="InputMaskBehavior"/>
    /// only invokes <see cref="Format"/> when this profile is active in the user's
    /// composition, preserving the universal-core invariant that affordances are off
    /// by default.
    /// </summary>
    Type GateProfile { get; }

    /// <summary>
    /// Reshape <paramref name="raw"/> into canonical form. <paramref name="caretIndex"/>
    /// is the caret position before formatting; the returned <see cref="FormatResult.CaretIndex"/>
    /// is the position after formatting (digit-anchored when reshaping occurred,
    /// preserved otherwise).
    /// </summary>
    FormatResult Format(string raw, int caretIndex);
}

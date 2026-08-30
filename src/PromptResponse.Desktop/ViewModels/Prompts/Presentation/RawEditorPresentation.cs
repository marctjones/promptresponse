namespace PromptResponse.Desktop.ViewModels.Prompts.Presentation;

/// <summary>
/// Pure presentation policy for the universal raw-response editor.
/// The prompt view model owns the per-session toggle state; this class keeps the
/// resulting widget, accessibility and sizing decisions together and testable.
/// </summary>
internal static class RawEditorPresentation
{
    internal static bool ShowHintedWidget(bool rawEditing, bool hintedWidgetAvailable) =>
        !rawEditing && hintedWidgetAvailable;

    internal static bool ShowRawEditor(bool rawEditing, bool hintedWidgetAvailable) =>
        rawEditing || !hintedWidgetAvailable;

    internal static bool ShowRawToggle(bool hintedWidgetAvailable) => hintedWidgetAvailable;

    internal static string ToggleGlyph(bool rawEditing) =>
        rawEditing ? "\u2611\uFE0E" : "\u270E\uFE0E";

    internal static double ToggleGlyphSize(double textScale) => 20.0 * textScale;

    internal static double ToggleButtonSize(double toggleGlyphSize) =>
        Math.Max(36.0, toggleGlyphSize * 1.7);

    internal static string ToggleName(bool rawEditing, string label) =>
        rawEditing
            ? $"Use the suggested input for {label}"
            : $"Type any text for {label}";
}

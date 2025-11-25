using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PromptResponse.Desktop.Services;

namespace PromptResponse.Desktop.Behaviors;

/// <summary>
/// Attached behavior that automatically formats TextBox input based on data type.
/// </summary>
public static class InputFormattingBehavior
{
    /// <summary>
    /// The data type to format (e.g., "phone", "ssn", "currency").
    /// </summary>
    public static readonly AttachedProperty<string?> DataTypeProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("DataType", typeof(InputFormattingBehavior));

    /// <summary>
    /// Whether formatting is enabled.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("IsEnabled", typeof(InputFormattingBehavior), false);

    private static bool _isFormatting;

    static InputFormattingBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<TextBox>(OnIsEnabledChanged);
    }

    public static string? GetDataType(TextBox element) => element.GetValue(DataTypeProperty);
    public static void SetDataType(TextBox element, string? value) => element.SetValue(DataTypeProperty, value);

    public static bool GetIsEnabled(TextBox element) => element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(TextBox element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            textBox.LostFocus += OnTextBoxLostFocus;
            textBox.TextChanged += OnTextChanged;
        }
        else
        {
            textBox.LostFocus -= OnTextBoxLostFocus;
            textBox.TextChanged -= OnTextChanged;
        }
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isFormatting || sender is not TextBox textBox)
            return;

        var dataType = GetDataType(textBox);
        if (string.IsNullOrEmpty(dataType))
            return;

        // Only format certain types on every keystroke
        var formatOnType = dataType.ToLowerInvariant() switch
        {
            "phone" => true,
            "ssn" => true,
            "ein" => true,
            "creditcard" => true,
            "zipcode" => true,
            _ => false
        };

        if (!formatOnType)
            return;

        var currentText = textBox.Text ?? "";
        var formatted = InputFormatter.Format(currentText, dataType);

        if (formatted != currentText)
        {
            _isFormatting = true;
            try
            {
                // Remember cursor position relative to digit count
                var cursorPos = textBox.CaretIndex;
                var digitsBeforeCursor = CountDigits(currentText, cursorPos);

                textBox.Text = formatted;

                // Restore cursor position
                var newCursorPos = FindPositionAfterDigits(formatted, digitsBeforeCursor);
                textBox.CaretIndex = Math.Min(newCursorPos, formatted.Length);
            }
            finally
            {
                _isFormatting = false;
            }
        }
    }

    private static void OnTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isFormatting || sender is not TextBox textBox)
            return;

        var dataType = GetDataType(textBox);
        if (string.IsNullOrEmpty(dataType))
            return;

        // Format on blur for types that don't format on every keystroke (like currency)
        var formatOnBlur = dataType.ToLowerInvariant() switch
        {
            "currency" => true,
            _ => false
        };

        if (!formatOnBlur)
            return;

        var currentText = textBox.Text ?? "";
        var formatted = InputFormatter.Format(currentText, dataType);

        if (formatted != currentText)
        {
            _isFormatting = true;
            try
            {
                textBox.Text = formatted;
            }
            finally
            {
                _isFormatting = false;
            }
        }
    }

    private static int CountDigits(string text, int upToPosition)
    {
        int count = 0;
        for (int i = 0; i < upToPosition && i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
                count++;
        }
        return count;
    }

    private static int FindPositionAfterDigits(string text, int digitCount)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                count++;
                if (count >= digitCount)
                    return i + 1;
            }
        }
        return text.Length;
    }
}

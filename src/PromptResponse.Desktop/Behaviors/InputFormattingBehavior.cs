using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PromptResponse.Desktop.Services;
using System;
using System.Text.RegularExpressions;

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
        var dataType = GetDataType(textBox);
        Console.WriteLine($"[InputFormattingBehavior] IsEnabledChanged: {e.NewValue}, DataType: {dataType}");

        if (e.NewValue is true)
        {
            textBox.LostFocus += OnTextBoxLostFocus;
            textBox.TextChanged += OnTextChanged;
            // Use AddHandler with handledEventsToo:true to intercept input before TextBox handles it
            textBox.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel, handledEventsToo: true);
            textBox.KeyDown += OnKeyDown;
        }
        else
        {
            textBox.LostFocus -= OnTextBoxLostFocus;
            textBox.TextChanged -= OnTextChanged;
            textBox.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
            textBox.KeyDown -= OnKeyDown;
        }
    }

    /// <summary>
    /// Filters text input based on data type to prevent invalid characters.
    /// </summary>
    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox textBox || string.IsNullOrEmpty(e.Text))
            return;

        var dataType = GetDataType(textBox)?.ToLowerInvariant();
        Console.WriteLine($"[InputFormattingBehavior] TextInput: '{e.Text}', DataType: {dataType}");

        if (string.IsNullOrEmpty(dataType))
            return;

        // For numeric types, only allow digits and decimal point
        if (dataType == "number" || dataType == "currency")
        {
            var currentText = textBox.Text ?? "";
            foreach (var c in e.Text)
            {
                // Allow digits
                if (char.IsDigit(c))
                    continue;
                // Allow one decimal point
                if (c == '.' && !currentText.Contains('.'))
                    continue;
                // Allow minus sign at start
                if (c == '-' && textBox.CaretIndex == 0 && !currentText.Contains('-'))
                    continue;
                // Block other characters
                Console.WriteLine($"[InputFormattingBehavior] Blocking character: '{c}'");
                e.Handled = true;
                return;
            }
        }
        // For phone, ssn, ein, creditcard, zipcode - only digits
        else if (dataType is "phone" or "ssn" or "ein" or "creditcard" or "zipcode")
        {
            foreach (var c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    Console.WriteLine($"[InputFormattingBehavior] Blocking non-digit: '{c}'");
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Allows navigation and editing keys for all types.
    /// </summary>
    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Always allow navigation, delete, backspace, etc.
        // These keys don't trigger TextInput, so we don't need to handle them
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

        // Format on blur for types that don't format on every keystroke (like currency, number)
        var formatOnBlur = dataType.ToLowerInvariant() switch
        {
            "currency" => true,
            "number" => true,
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

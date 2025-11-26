using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PromptResponse.Desktop.Converters;

/// <summary>
/// Converts a string to DateTimeOffset for CalendarDatePicker binding.
/// </summary>
public class DateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            if (DateTime.TryParse(str, out var dt))
                return new DateTimeOffset(dt);
            // Try parsing just the date part for datetime strings
            if (str.Contains('T'))
            {
                var datePart = str.Split('T')[0];
                if (DateTime.TryParse(datePart, out var dateOnly))
                    return new DateTimeOffset(dateOnly);
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset dto)
            return dto.ToString("yyyy-MM-dd");
        return "";
    }
}

/// <summary>
/// Converts a string to TimeSpan for TimePicker binding.
/// </summary>
public class TimeSpanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            if (TimeSpan.TryParse(str, out var time))
                return time;
            // Try parsing as time from datetime string
            if (DateTime.TryParse(str, out var dt))
                return dt.TimeOfDay;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
            return time.ToString(@"hh\:mm\:ss");
        return "";
    }
}

/// <summary>
/// Extracts time portion from a datetime string.
/// </summary>
public class DateTimeToTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            if (DateTime.TryParse(str, out var dt))
                return dt.TimeOfDay;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is typically used alongside DateTimeConverter
        // ConvertBack is handled by combining date and time in code-behind
        return value?.ToString() ?? "";
    }
}

/// <summary>
/// Converts color string to SolidColorBrush for color preview.
/// </summary>
public class ColorBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string colorStr && !string.IsNullOrEmpty(colorStr))
        {
            try
            {
                // Try parsing as hex color
                if (colorStr.StartsWith("#"))
                {
                    if (Color.TryParse(colorStr, out var color))
                        return new SolidColorBrush(color);
                }
                // Try common color names
                var namedColor = GetNamedColor(colorStr.ToLowerInvariant());
                if (namedColor.HasValue)
                    return new SolidColorBrush(namedColor.Value);
            }
            catch
            {
                // Ignore parsing errors
            }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() ?? "";
    }

    private static Color? GetNamedColor(string name)
    {
        return name switch
        {
            "red" => Colors.Red,
            "green" => Colors.Green,
            "blue" => Colors.Blue,
            "yellow" => Colors.Yellow,
            "orange" => Colors.Orange,
            "purple" => Colors.Purple,
            "pink" => Colors.Pink,
            "black" => Colors.Black,
            "white" => Colors.White,
            "gray" or "grey" => Colors.Gray,
            "brown" => Colors.Brown,
            "cyan" => Colors.Cyan,
            "magenta" => Colors.Magenta,
            "lime" => Colors.Lime,
            "navy" => Colors.Navy,
            "teal" => Colors.Teal,
            "olive" => Colors.Olive,
            "maroon" => Colors.Maroon,
            "silver" => Colors.Silver,
            "aqua" => Colors.Aqua,
            "fuchsia" => Colors.Fuchsia,
            _ => null
        };
    }
}

/// <summary>
/// Converts validation boolean to appropriate icon path.
/// </summary>
public class ValidationIconConverter : IValueConverter
{
    // Checkmark icon for valid
    private const string ValidIcon = "M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z";
    // Warning/X icon for invalid
    private const string InvalidIcon = "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z";
    // Circle for empty/neutral
    private const string NeutralIcon = "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid ? ValidIcon : InvalidIcon;
        }
        return NeutralIcon;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts validation boolean to appropriate color.
/// </summary>
public class ValidationColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid
                ? new SolidColorBrush(Color.Parse("#4CAF50")) // Green
                : new SolidColorBrush(Color.Parse("#F44336")); // Red
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts validation boolean to tooltip text.
/// </summary>
public class ValidationTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid ? "Valid format" : "Invalid format";
        }
        return "Enter a value";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Checks if a collection contains a specific value.
/// </summary>
public class ContainsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Collections.Generic.List<string> list && parameter is string item)
        {
            return list.Contains(item);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

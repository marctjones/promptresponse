using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PromptResponse.Desktop.Converters;

/// <summary>
/// Converts a string response to a boolean by comparing it to a parameter value.
/// Used for radio buttons to check if this option is selected.
/// </summary>
public class EqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string response && parameter is string option)
        {
            return response == option;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string option)
        {
            return option;
        }
        return null;
    }
}

/// <summary>
/// Converts a string response ("true"/"false") to a boolean.
/// Used for boolean checkbox fields.
/// </summary>
public class BooleanResponseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string response)
        {
            return response.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked)
        {
            return isChecked ? "true" : "false";
        }
        return "false";
    }
}

/// <summary>
/// Checks if a comma-separated response contains a specific value.
/// Used for multichoice checkbox fields.
/// </summary>
public class ContainsValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string response && parameter is string option)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            var values = response.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return values.Contains(option);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ConvertBack is handled by the Click event handler
        throw new NotImplementedException();
    }
}

/// <summary>
/// Multi-value converter that compares two string values for equality.
/// Used for radio buttons where we need to compare Response to the option value.
/// Values[0] = Response (from PromptViewModel)
/// Values[1] = Option value (from the current item)
/// </summary>
public class EqualsMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is string response && values[1] is string option)
        {
            return response == option;
        }
        return false;
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Multi-value converter that checks if a comma-separated list contains a value.
/// Used for multichoice checkboxes.
/// Values[0] = Response (from PromptViewModel)
/// Values[1] = Option value (from the current item)
/// </summary>
public class ContainsMultiConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is string response && values[1] is string option)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return false;
            }

            var items = response.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return items.Contains(option);
        }
        return false;
    }

    public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

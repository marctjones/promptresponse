namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Maps an APR <c>expectedDataType</c> hint string to the <see cref="IInputFormatter"/>
/// (if any) that reshapes input for that hint. Unknown hints get no formatter —
/// the user types whatever they want and nothing reshapes it.
/// </summary>
public static class InputFormatterRegistry
{
    /// <summary>Returns the input formatter for the given type-hint string, or null
    /// if the hint has no associated formatter (free-text fields, signatures, etc.).</summary>
    public static IInputFormatter? ForHint(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint)) return null;
        return hint.ToLowerInvariant() switch
        {
            "phone" => new PhoneInputFormatter(),
            "ssn" => new SsnInputFormatter(),
            "ein" => new EinInputFormatter(),
            "zipcode" or "zip" or "postalcode" => new ZipCodeInputFormatter(),
            "currency" => new CurrencyInputFormatter(),
            "percentage" or "percent" => new PercentageInputFormatter(),
            _ => null,
        };
    }
}

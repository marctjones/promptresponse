namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Maps an APR <c>expectedDataType</c> hint string to the <see cref="IInputFormatter"/>
/// (if any) that reshapes input for that hint. Unknown hints get no formatter —
/// the user types whatever they want and nothing reshapes it.
/// </summary>
public static class InputFormatterRegistry
{
    private static readonly IReadOnlyDictionary<string, IInputFormatter> Formatters =
        new Dictionary<string, IInputFormatter>(StringComparer.OrdinalIgnoreCase)
        {
            ["phone"] = new PhoneInputFormatter(),
            ["ssn"] = new SsnInputFormatter(),
            ["ein"] = new EinInputFormatter(),
            ["zipcode"] = new ZipCodeInputFormatter(),
            ["zip"] = new ZipCodeInputFormatter(),
            ["postalcode"] = new ZipCodeInputFormatter(),
            ["currency"] = new CurrencyInputFormatter(),
            ["percentage"] = new PercentageInputFormatter(),
            ["percent"] = new PercentageInputFormatter(),
        };

    /// <summary>Returns the input formatter for the given type-hint string, or null
    /// if the hint has no associated formatter (free-text fields, signatures, etc.).</summary>
    public static IInputFormatter? ForHint(string? hint)
    {
        return string.IsNullOrWhiteSpace(hint)
            ? null
            : Formatters.GetValueOrDefault(hint);
    }
}

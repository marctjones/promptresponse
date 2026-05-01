using System.Globalization;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Display-rendering flag: when active, numeric responses with the "number" hint
/// render with the active culture's thousands separators ("42,000"). Non-numeric
/// responses ("five") pass through unchanged — vision invariant.
/// </summary>
public sealed class NumberThousandsSeparatorsProfile : RenderingProfileBase
{
    private readonly CultureInfo _culture;

    public NumberThousandsSeparatorsProfile() : this(CultureInfo.CurrentCulture) { }

    public NumberThousandsSeparatorsProfile(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public override string Name => "NumberThousandsSeparators";

    public override string? FormatDisplay(string? rawValue, string? typeHint)
    {
        if (string.IsNullOrEmpty(rawValue)) return rawValue;
        if (!string.Equals(typeHint, "number", StringComparison.OrdinalIgnoreCase)) return rawValue;
        if (!double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var value))
        {
            return rawValue;
        }
        return value % 1 == 0 ? value.ToString("N0", _culture) : value.ToString("G", _culture);
    }
}

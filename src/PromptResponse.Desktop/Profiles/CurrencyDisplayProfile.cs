using System.Globalization;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Display-rendering flag: when active, "currency"-hinted responses that parse as
/// decimals render with the active culture's currency symbol ($1,234.56). Free
/// text passes through.
/// </summary>
public sealed class CurrencyDisplayProfile : RenderingProfileBase
{
    private readonly CultureInfo _culture;

    public CurrencyDisplayProfile() : this(CultureInfo.CurrentCulture) { }

    public CurrencyDisplayProfile(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public override string Name => "CurrencyDisplay";

    public override string? FormatDisplay(string? rawValue, string? typeHint)
    {
        if (string.IsNullOrEmpty(rawValue)) return rawValue;
        if (!string.Equals(typeHint, "currency", StringComparison.OrdinalIgnoreCase)) return rawValue;
        if (!decimal.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var value))
        {
            return rawValue;
        }
        return value.ToString("C2", _culture);
    }
}

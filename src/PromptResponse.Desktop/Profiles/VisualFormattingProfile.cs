using System.Globalization;
using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Renders raw response strings as human-readable display for users whose capability
/// profile benefits from visual formatting (commas in numbers, currency symbols,
/// human-readable dates). The stored response is unchanged — the profile only
/// transforms display.
/// </summary>
/// <remarks>
/// CRITICAL invariant: when the raw response doesn't parse as the suggested type
/// (e.g., "five" in a number-hinted prompt), <see cref="FormatDisplay"/> returns the
/// raw text unchanged. Any visible text is a valid response in PromptResponse.
/// </remarks>
public sealed class VisualFormattingProfile : IRenderingProfile
{
    private readonly CultureInfo _culture;

    public VisualFormattingProfile() : this(CultureInfo.CurrentCulture) { }

    public VisualFormattingProfile(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public string Name => "VisualFormatting";

    public string? FormatDisplay(string? rawValue, string? typeHint)
    {
        if (string.IsNullOrEmpty(rawValue) || string.IsNullOrEmpty(typeHint))
        {
            return rawValue;
        }

        return typeHint.ToLowerInvariant() switch
        {
            "number" => TryFormatNumber(rawValue),
            "currency" => TryFormatCurrency(rawValue),
            "date" => TryFormatDate(rawValue),
            _ => rawValue,
        };
    }

    private string TryFormatNumber(string raw)
    {
        if (double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            // Preserve fractional precision for non-integer inputs; integer inputs render without ".0".
            return value % 1 == 0
                ? value.ToString("N0", _culture)
                : value.ToString("G", _culture);
        }
        return raw;
    }

    private string TryFormatCurrency(string raw)
    {
        if (decimal.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value))
        {
            return value.ToString("C2", _culture);
        }
        return raw;
    }

    private string TryFormatDate(string raw)
    {
        if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("MMMM d, yyyy", _culture);
        }
        return raw;
    }

    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => true;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}

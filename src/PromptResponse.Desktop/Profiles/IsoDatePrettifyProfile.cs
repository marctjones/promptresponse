using System.Globalization;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Display-rendering flag: when active, "date"-hinted ISO-8601 responses
/// (yyyy-MM-dd) render as long-form dates ("April 29, 2026"). Free-text dates
/// ("end of Q2", "see attached") pass through.
/// </summary>
public sealed class IsoDatePrettifyProfile : RenderingProfileBase
{
    private readonly CultureInfo _culture;

    public IsoDatePrettifyProfile() : this(CultureInfo.CurrentCulture) { }

    public IsoDatePrettifyProfile(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public override string Name => "IsoDatePrettify";

    public override string? FormatDisplay(string? rawValue, string? typeHint)
    {
        if (string.IsNullOrEmpty(rawValue)) return rawValue;
        if (!string.Equals(typeHint, "date", StringComparison.OrdinalIgnoreCase)) return rawValue;
        if (!DateTime.TryParseExact(rawValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return rawValue;
        }
        return date.ToString("MMMM d, yyyy", _culture);
    }
}

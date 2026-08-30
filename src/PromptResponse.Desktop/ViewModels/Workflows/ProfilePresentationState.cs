using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Derives the shell's visual presentation tokens from the active accessibility
/// profile. It has no document/session dependency and is intentionally a
/// read-only binding state object.
/// </summary>
internal sealed class ProfilePresentationState
{
    private readonly IProfileService _profiles;

    public ProfilePresentationState(IProfileService profiles) => _profiles = profiles;

    public IRenderingProfile ActiveProfile => _profiles.ActiveProfile;

    private ColorPalette Palette => ColorTokens.For(ActiveProfile.ColorScheme);
    private IBrush BrushFor(ColorRole role) => new SolidColorBrush(Palette[role]);

    public IBrush SurfaceBrush => BrushFor(ColorRole.Surface);
    public IBrush SubtleSurfaceBrush => BrushFor(ColorRole.SubtleSurface);
    public IBrush ElevatedSurfaceBrush => BrushFor(ColorRole.ElevatedSurface);
    public IBrush OnSurfaceBrush => BrushFor(ColorRole.OnSurface);
    public IBrush MutedTextBrush => BrushFor(ColorRole.MutedText);
    public IBrush PrimaryBrush => BrushFor(ColorRole.Primary);
    public IBrush OnPrimaryBrush => BrushFor(ColorRole.OnPrimary);
    public IBrush BorderBrush => BrushFor(ColorRole.Border);
    public IBrush DividerBrush => BrushFor(ColorRole.Divider);
    public IBrush FocusBrush => BrushFor(ColorRole.Focus);

    public CornerRadius ControlCornerRadius { get; } = new(3);
    public CornerRadius SurfaceCornerRadius { get; } = new(4);

    public ThemeVariant ThemeVariant => ActiveProfile.ColorScheme switch
    {
        ColorScheme.Dark => ThemeVariant.Dark,
        ColorScheme.HighContrast => ThemeVariant.Dark,
        _ => ThemeVariant.Light,
    };

    public double CaptionFontSize => 12 * ActiveProfile.TextScale;
    public double BodyFontSize => 14 * ActiveProfile.TextScale;
    public double SubtitleFontSize => 18 * ActiveProfile.TextScale;
    public double TitleFontSize => 22 * ActiveProfile.TextScale;
    public double DisplayFontSize => 32 * ActiveProfile.TextScale;
}

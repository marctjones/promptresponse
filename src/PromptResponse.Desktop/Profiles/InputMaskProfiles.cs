namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Marker for the input-mask capability flags. Each input-mask profile is a
/// distinct type because it serves as the <em>gate identity</em> wired through
/// the system: an <see cref="InputFormatters.IInputFormatter"/> binds to one via
/// <see cref="InputFormatters.IInputFormatter.GateProfile"/>, the Display
/// Preferences panel toggles each independently, and persistence stores them by
/// type name. They are grouped in this one file (rather than a file each) to
/// keep the Profiles module tidy without changing those load-bearing identities.
/// </summary>
public interface IInputMaskProfile : IRenderingProfile
{
}

/// <summary>
/// Input-mask flag: when active, "phone"-hinted text fields reshape digit-only
/// input as the user types ("(555) 123-4567"). Free text ("see HR") passes
/// through unchanged.
/// </summary>
public sealed class PhoneInputMaskProfile : RenderingProfileBase, IInputMaskProfile
{
    public override string Name => "PhoneInputMask";
}

/// <summary>
/// Input-mask flag: when active, "ssn"-hinted text fields reshape digit-only
/// input as the user types ("###-##-####"). Free text passes through.
/// </summary>
public sealed class SsnInputMaskProfile : RenderingProfileBase, IInputMaskProfile
{
    public override string Name => "SsnInputMask";
}

/// <summary>
/// Input-mask flag: when active, "ein"-hinted text fields reshape digit-only
/// input as the user types ("##-#######"). Free text passes through.
/// </summary>
public sealed class EinInputMaskProfile : RenderingProfileBase, IInputMaskProfile
{
    public override string Name => "EinInputMask";
}

/// <summary>
/// Input-mask flag: when active, "zip"/"zipcode"-hinted text fields reshape
/// digit-only input as the user types ("#####" or "#####-####"). Free text
/// passes through.
/// </summary>
public sealed class ZipInputMaskProfile : RenderingProfileBase, IInputMaskProfile
{
    public override string Name => "ZipInputMask";
}

/// <summary>
/// Input-mask flag: when active, "currency"-hinted text fields reshape numeric
/// input as the user types (grouping + decimals). Free text passes through.
/// </summary>
public sealed class CurrencyInputMaskProfile : RenderingProfileBase, IInputMaskProfile
{
    public override string Name => "CurrencyInputMask";
}

/// <summary>
/// Input-mask flag: when active, "percentage"-hinted text fields reshape numeric
/// input as the user types. Free text passes through.
/// </summary>
public sealed class PercentageInputMaskProfile : RenderingProfileBase, IInputMaskProfile
{
    public override string Name => "PercentageInputMask";
}

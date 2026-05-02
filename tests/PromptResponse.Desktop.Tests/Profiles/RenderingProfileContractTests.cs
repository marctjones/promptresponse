using AwesomeAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.Desktop.Tests.Profiles;

/// <summary>
/// Contract tests every <see cref="IRenderingProfile"/> implementation must satisfy:
/// non-mutating display formatting, sane defaults, and the universal-core invariant
/// that no profile can corrupt a stored response by formatting it.
/// </summary>
public class RenderingProfileContractTests
{
    public static IEnumerable<object[]> AllProfiles()
    {
        // Color schemes
        yield return new object[] { new DefaultProfile() };
        yield return new object[] { new LightProfile() };
        yield return new object[] { new DarkProfile() };
        yield return new object[] { new HighContrastProfile() };
        // Global enhancements
        yield return new object[] { new LargeTextProfile() };
        yield return new object[] { new ReducedMotionProfile() };
        yield return new object[] { new ScreenReaderTunedProfile() };
        yield return new object[] { new LargeHitTargetsProfile() };
        // Display rendering flags
        yield return new object[] { new NumberThousandsSeparatorsProfile() };
        yield return new object[] { new CurrencyDisplayProfile() };
        yield return new object[] { new IsoDatePrettifyProfile() };
        yield return new object[] { new DisplaysAsPreviewProfile() };
        // Interactive widget flags
        yield return new object[] { new CalendarPickerProfile() };
        yield return new object[] { new BooleanRadiosProfile() };
        // Input mask flags
        yield return new object[] { new PhoneInputMaskProfile() };
        yield return new object[] { new SsnInputMaskProfile() };
        yield return new object[] { new EinInputMaskProfile() };
        yield return new object[] { new ZipInputMaskProfile() };
        yield return new object[] { new CurrencyInputMaskProfile() };
        yield return new object[] { new PercentageInputMaskProfile() };
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void FormatDisplay_OnEmptyOrNull_ReturnsInputUnchanged(IRenderingProfile profile)
    {
        profile.FormatDisplay(string.Empty, "number").Should().Be(string.Empty);
        profile.FormatDisplay(null, "number").Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void FormatDisplay_OnNonNumericNumberHint_PassesRawTextThrough(IRenderingProfile profile)
    {
        // Vision invariant: any visible text is a valid response. A "number"-hinted prompt
        // with response "five" must not be mangled by formatting — it must round-trip.
        var inputs = new[] { "five", "n/a", "approximately 5", "I prefer not to say" };
        foreach (var input in inputs)
        {
            profile.FormatDisplay(input, "number")
                .Should().Be(input, "non-numeric responses must pass through unchanged on every profile");
        }
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void MinimumTouchTarget_IsAtLeast32px(IRenderingProfile profile)
    {
        // 32px is our absolute floor (WCAG 2.1 AA target-size); the LargeHitTargets profile
        // raises this further. No profile should be below the floor.
        profile.MinimumTouchTarget.Width.Should().BeGreaterThanOrEqualTo(32);
        profile.MinimumTouchTarget.Height.Should().BeGreaterThanOrEqualTo(32);
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void TextScale_IsAtLeast100Percent(IRenderingProfile profile)
    {
        profile.TextScale.Should().BeGreaterThanOrEqualTo(1.0);
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void TargetContrast_IsAALevelOrHigher(IRenderingProfile profile)
    {
        profile.TargetContrast.Should().BeOneOf(ContrastLevel.AA, ContrastLevel.AAA);
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void Name_IsNonEmpty(IRenderingProfile profile)
    {
        profile.Name.Should().NotBeNullOrWhiteSpace("profiles must self-identify for the preferences UI and telemetry");
    }
}

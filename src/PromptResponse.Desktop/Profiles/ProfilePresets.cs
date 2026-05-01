namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Named capability-profile presets — one-click compositions of individual feature
/// flags for the five reference users (Excellent vision, Blind/screen-reader,
/// LowVision/HC, Cognitive, Motor). The "Customize" panel in Display Preferences
/// exposes every flag individually for users whose needs don't match a preset.
/// </summary>
/// <remarks>
/// See <c>docs/CAPABILITY_PROFILES.md</c> for the design rationale and the table of
/// which flags each preset composes.
///
/// Presets are *imperative* in the sense that <see cref="Apply"/> takes the service's
/// active set as the starting point and clears affordance flags before composing the
/// preset's flags. Color scheme is set via <see cref="IProfileService.SetColorScheme"/>;
/// global behaviors (LargeText, ReducedMotion, ScreenReaderTuned, LargeHitTargets)
/// are toggled to match the preset's intent.
/// </remarks>
public static class ProfilePresets
{
    public enum Preset
    {
        ExcellentVision,
        BlindScreenReader,
        LowVisionHighContrast,
        CognitiveDyslexia,
        MotorMobility,
    }

    public static IReadOnlyList<Type> AllAffordanceFlags { get; } = new Type[]
    {
        typeof(NumberThousandsSeparatorsProfile),
        typeof(CurrencyDisplayProfile),
        typeof(IsoDatePrettifyProfile),
        typeof(DisplaysAsPreviewProfile),
        typeof(CalendarPickerProfile),
        typeof(BooleanRadiosProfile),
        typeof(PhoneInputMaskProfile),
        typeof(SsnInputMaskProfile),
        typeof(EinInputMaskProfile),
        typeof(ZipInputMaskProfile),
        typeof(CurrencyInputMaskProfile),
        typeof(PercentageInputMaskProfile),
    };

    /// <summary>Applies the given preset to the supplied <paramref name="service"/>.
    /// Clears all affordance flags first, then enables the subset the preset wants.
    /// Color-scheme and global behavior flags are set explicitly.</summary>
    public static void Apply(Preset preset, IProfileService service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));

        // Clear all affordance flags so the preset starts from a clean slate.
        foreach (var flagType in AllAffordanceFlags)
        {
            DisableByType(service, flagType);
        }
        // Clear the always-on globals too — preset re-applies the ones it wants.
        DisableByType(service, typeof(LargeTextProfile));
        DisableByType(service, typeof(ReducedMotionProfile));
        DisableByType(service, typeof(ScreenReaderTunedProfile));
        DisableByType(service, typeof(LargeHitTargetsProfile));

        switch (preset)
        {
            case Preset.ExcellentVision:
                service.SetColorScheme(ColorScheme.Light);
                EnableAll(service, AllAffordanceFlags);
                break;

            case Preset.BlindScreenReader:
                service.SetColorScheme(ColorScheme.Light);
                service.Enable<ScreenReaderTunedProfile>();
                service.Enable<ReducedMotionProfile>();
                // Display affordances stay on — no input interruption.
                service.Enable<NumberThousandsSeparatorsProfile>();
                service.Enable<CurrencyDisplayProfile>();
                service.Enable<IsoDatePrettifyProfile>();
                service.Enable<DisplaysAsPreviewProfile>();
                // Boolean radios are arrow-key friendly for screen readers.
                service.Enable<BooleanRadiosProfile>();
                // Commit-time masks fire once on LostFocus — low disruption.
                service.Enable<CurrencyInputMaskProfile>();
                service.Enable<PercentageInputMaskProfile>();
                // Live masks (Phone/SSN/EIN/Zip) and Calendar picker are intentionally OFF.
                break;

            case Preset.LowVisionHighContrast:
                service.SetColorScheme(ColorScheme.HighContrast);
                service.Enable<LargeTextProfile>();
                service.Enable<LargeHitTargetsProfile>();
                EnableAll(service, AllAffordanceFlags);
                break;

            case Preset.CognitiveDyslexia:
                // Today this preset is sparse — most cognitive flags are still ⏳ later.
                // It enables the v0.1 flags now; future cognitive flags fold in here.
                service.SetColorScheme(ColorScheme.Light);
                service.Enable<LargeTextProfile>();
                EnableAll(service, AllAffordanceFlags);
                break;

            case Preset.MotorMobility:
                // Today this preset is sparse — most motor flags are still ⏳ later.
                service.SetColorScheme(ColorScheme.Light);
                service.Enable<LargeHitTargetsProfile>();
                service.Enable<ReducedMotionProfile>();
                EnableAll(service, AllAffordanceFlags);
                break;
        }
    }

    private static void EnableAll(IProfileService service, IReadOnlyList<Type> flags)
    {
        foreach (var flagType in flags) EnableByType(service, flagType);
    }

    // The IProfileService API uses generics (Enable<T>(), Disable<T>()) which we can't
    // call dispatched-by-Type at compile time. The reflection trampoline keeps presets
    // declarative — the imperative invocations are localized here.
    private static void EnableByType(IProfileService service, Type flagType)
    {
        var method = typeof(IProfileService).GetMethod(nameof(IProfileService.Enable))!
            .MakeGenericMethod(flagType);
        method.Invoke(service, null);
    }

    private static void DisableByType(IProfileService service, Type flagType)
    {
        var method = typeof(IProfileService).GetMethod(nameof(IProfileService.Disable))!
            .MakeGenericMethod(flagType);
        method.Invoke(service, null);
    }
}

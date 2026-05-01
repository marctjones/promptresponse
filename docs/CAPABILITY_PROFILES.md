# Capability Profiles — Architecture Sketch

**Status:** Design sketch · 2026-05-01 · Pre-implementation
**Owner:** Marc Jones
**Scope:** PromptResponse.Desktop accessibility & UX affordance system

---

## Core Idea

Every PromptResponse user has a **capability profile** — a set of feature flags that turn individual UX affordances on or off. There is no "normal user" baseline; sighted office workers, blind screen-reader users, low-vision keyboard users, dyslexic readers, and tremor-affected mouse users are all *equally first-class* and are served by composing the right flags for their capabilities.

Programmatically, profiles are **bags of independent feature flags**. Two users with overlapping needs can flip individual flags without inheriting an entire bundle. In the GUI, we expose a small number of **named presets** that flip a curated set of flags at once, plus a "customize" panel that exposes every flag.

### Three invariants the flag system MUST preserve

1. **Universal core stays functional with all flags off.** Every form is fillable with zero affordances active.
2. **Free text is always a valid response.** No mask, picker, or reshaping affordance may block the user from typing arbitrary visible text into any field.
3. **Stored response is always raw.** Affordances reshape the *display* only when explicitly committing to the field; the persisted document stores exactly what the user committed.

---

## Feature Flags

Flags are grouped by axis. Each flag is a single boolean and composes orthogonally with the others.

### Color & contrast

| Flag | Behavior |
|---|---|
| `LightTheme` | Light color scheme. (Mutually exclusive with Dark/HC.) |
| `DarkTheme` | Dark color scheme. |
| `HighContrastTheme` | Pure black/white + saturated yellow accents; WCAG AAA. |

### Typography & layout

| Flag | Behavior |
|---|---|
| `LargeText` | All text scaled 1.5×. |
| `DyslexiaFont` | Switches base font to Atkinson Hyperlegible (OpenDyslexic optional). |
| `IncreasedSpacing` | Line-height 1.6, letter-spacing 0.05em — line-tracking aid. |

### Visual formatting affordances (currently bundled in `VisualFormattingProfile`)

| Flag | Behavior |
|---|---|
| `NumberThousandsSeparators` | Renders `42000` as `42,000` in display preview. |
| `CurrencyDisplay` | Renders `1234.56` as `$1,234.56` on commit. |
| `PercentageSuffix` | Appends `%` on commit. |
| `IsoDatePrettify` | Renders `2026-04-29` as `April 29, 2026` in display preview. |
| `CalendarPicker` | Shows `CalendarDatePicker` widget beside Date fields. |
| `BooleanRadios` | Shows Yes/No radios beside Boolean text input. |
| `DisplaysAsPreview` | Renders the "Displays as: …" live region under formatted fields. |

### Input masks (currently bundled, all live except where noted)

| Flag | Behavior |
|---|---|
| `PhoneInputMask` | Live reshape of digit-only input to `(555) 123-4567`. |
| `SsnInputMask` | Live reshape to `###-##-####`. |
| `EinInputMask` | Live reshape to `##-#######`. |
| `ZipInputMask` | Live reshape to `#####-####` (5-digit untouched). |
| `CurrencyInputMask` | Reshape to `$1,234.56` on commit. |
| `PercentageInputMask` | Reshape to `12.5%` on commit. |

### Motion & motor

| Flag | Behavior |
|---|---|
| `ReducedMotion` | Disables decorative animations and transitions. |
| `LargeHitTargets` | Min click/tap target ≥ 44×44 px. |
| `HitMarginPadding` | Extra 8–12 px invisible padding around clickable elements — tremor near-misses still register. |

### Cognitive & comprehension

| Flag | Behavior |
|---|---|
| `SectionFocusMode` | Collapses all sections except the one with focus. |
| `AlwaysVisibleHelp` | Renders `helpText` inline below every field instead of on hover/expand. |
| `InlineExamples` | Shows `hints.examples[0]` as ghost text below placeholder, with "more" link. |
| `PlainLanguageLabels` | Uses `hints.plainLanguage` (if present) instead of formal `label`. |
| `PersistentProgress` | Pins "3 of 12 sections complete" + section list to the sidebar. |

### Screen-reader & voice control

| Flag | Behavior |
|---|---|
| `ScreenReaderTuned` | Verbose live regions; section-context announcements; suppresses noisy reshape events. |
| `PersistentToasts` | Errors/info don't auto-dismiss — voice/switch users get time to acknowledge. |
| `KeyboardShortcutOverlay` | Alt-press highlights every visible keyboard shortcut. |
| `VoiceControlNames` | Adds Dragon-friendly short alternates ("save", "next", "phone") to `AutomationProperties.Name`. |

### Universal core (NOT flags — always on)

These behaviors are unconditional. They were considered for flag-status and rejected because every user benefits.

- Autosave on every edit (filled forms).
- Confirm-on-leave with unsaved changes.
- No hover-only UI: every reveal has a click/keyboard alternative.
- `AutomationProperties.Name` + `HelpText` on every interactive element.
- Section titles required (validation rejects unnamed sections).

---

## Named Presets (GUI surface)

Five presets ship as one-click options in Display Preferences. "Customize" exposes every flag from the table above.

### 1 · Excellent vision (default for sighted, no other capabilities)

Most affordances on; nothing accessibility-specific.

| Flag bundle | State |
|---|---|
| `LightTheme` (or OS-detected) | ✓ |
| All `*InputMask` flags | ✓ |
| `CalendarPicker`, `BooleanRadios`, `DisplaysAsPreview` | ✓ |
| `NumberThousandsSeparators`, `CurrencyDisplay`, `PercentageSuffix`, `IsoDatePrettify` | ✓ |

### 2 · Blind / screen reader

Pure-text input; affordances that interrupt screen reader speech are off.

| Flag bundle | State |
|---|---|
| `ScreenReaderTuned`, `PersistentToasts`, `VoiceControlNames` | ✓ |
| `AlwaysVisibleHelp` | ✓ |
| `ReducedMotion` | ✓ |
| All `*InputMask` flags (live) | ✗ — caret jumps and re-announcements disrupt speech |
| `CalendarPicker`, `BooleanRadios` | ✗ for picker; ✓ for radios (arrow-key friendly) |
| `CurrencyInputMask`, `PercentageInputMask` (commit-time) | ✓ — single announcement on LostFocus, low disruption |
| `DisplaysAsPreview` | ✓ — confirms intent via polite live region |

### 3 · Low vision / high contrast

Sighted user with reduced acuity — affordances *help* by visually confirming input. Bigger everything.

| Flag bundle | State |
|---|---|
| `HighContrastTheme` | ✓ |
| `LargeText`, `LargeHitTargets` | ✓ |
| All `*InputMask` flags | ✓ |
| `CalendarPicker`, `BooleanRadios`, `DisplaysAsPreview` | ✓ |

### 4 · Cognitive / dyslexia / executive-function (NEW)

Forms-specific cognitive support. Plain language, one thing at a time, visible help.

| Flag bundle | State |
|---|---|
| `DyslexiaFont`, `IncreasedSpacing`, `LargeText` | ✓ |
| `SectionFocusMode`, `PersistentProgress` | ✓ |
| `AlwaysVisibleHelp`, `InlineExamples`, `PlainLanguageLabels` | ✓ |
| `UndoBar`, `ConfirmDestructive` | ✓ |
| All visual masks + display affordances | ✓ |
| `ReducedMotion` | optional |

### 5 · Motor / mobility (NEW)

Voice-control, switch, tremor, one-handed users.

| Flag bundle | State |
|---|---|
| `LargeHitTargets`, `HitMarginPadding` | ✓ |
| `UndoBar`, `ConfirmDestructive`, `PersistentToasts` | ✓ |
| `KeyboardShortcutOverlay`, `VoiceControlNames` | ✓ |
| `ReducedMotion` | ✓ |
| All visual masks (visual confirmation aids) | ✓ |

---

## Note on the `Undo` system

`UndoBar` and `ConfirmDestructive` appear in the Cognitive and Motor presets only. They reference an undo stack that doesn't exist yet — adding it is its own concrete piece of work, separate from the flag split. The flags can ship disabled-by-default until the stack lands.

---

## Migration Path (current code → this sketch)

The existing `VisualFormattingProfile` is a single coarse switch. To move to the per-feature flag model:

1. **Split `VisualFormattingProfile`** into ~13 individual flag classes (one per row in the "Visual formatting affordances" + "Input masks" tables above). Each is its own `IRenderingProfile` subclass for now; the flag is "is this profile in the active composition?". This keeps the `IProfileService.IsActive(typeof(...))` API intact.
2. **Update view wiring** to check the specific flag instead of the bundle:
   - `PhonePromptView` → `PhoneInputMask`, not `VisualFormattingProfile`
   - `DatePromptView` → `CalendarPicker`
   - `NumberPromptView` "Displays as:" preview → `NumberThousandsSeparators` + `DisplaysAsPreview`
3. **Add the new flags one at a time** as concrete features (`SectionFocusMode`, `DyslexiaFont`, etc.). Each gets its own commit, its own GUI tests, and its own checkbox in Display Preferences.
4. **Refactor `DisplayPreferencesViewModel`** to expose every flag, plus a "Preset" picker that calls a `ProfilePresets.Apply(presetName, IProfileService)` helper.
5. **Universal-core promotions** (autosave, confirm-on-leave, no-hover-only-UI) move out of `IRenderingProfile` if they were ever there, or stay in shell-level code where they already are.

Each step is its own commit. No bulk landings.

---

## Open questions

- **Should flag-state persist per-document or per-user?** Today the app persists per-user via `SettingsService`. A document containing `presetHint: "cognitive"` could nudge a new user toward a starting preset without overriding the user's saved choice.
- **Should presets be selectable per-form?** A government benefits form might ship with a "cognitive" hint baked in; the user can still override.
- **Do we expose preset switching in the menu bar, or only in Display Preferences?** Quick toggle from the menu would help users who switch contexts (work device vs personal).

These are deferred to after the flag split lands.

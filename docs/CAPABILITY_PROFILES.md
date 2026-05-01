# Capability Profiles — Architecture

**Status:** Ships in v0.1 · 2026-05-01
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

The **Ships** column is the source of truth for what's implemented today vs. designed for later:

- ✅ **now** — implemented in v0.1 (this batch)
- ⏳ **later** — designed but not yet implemented

### Color & contrast

| Flag | Ships | Behavior |
|---|---|---|
| `LightTheme` | ✅ now | Light color scheme. (Mutually exclusive with Dark/HC.) |
| `DarkTheme` | ✅ now | Dark color scheme. |
| `HighContrastTheme` | ✅ now | Pure black/white + saturated yellow accents; WCAG AAA. |

### Typography & layout

| Flag | Ships | Behavior |
|---|---|---|
| `LargeText` | ✅ now | All text scaled 1.5×. |
| `DyslexiaFont` | ⏳ later | Switches base font to Atkinson Hyperlegible (OpenDyslexic optional). |
| `IncreasedSpacing` | ⏳ later | Line-height 1.6, letter-spacing 0.05em — line-tracking aid. |

### Visual display affordances (split out from `VisualFormattingProfile`)

These reshape **display** of stored values; the underlying response stays raw.

| Flag | Ships | Behavior |
|---|---|---|
| `NumberThousandsSeparators` | ✅ now | Renders `42000` as `42,000` in the "Displays as:" preview. |
| `CurrencyDisplay` | ✅ now | Renders `1234.56` as `$1,234.56` in display preview. |
| `IsoDatePrettify` | ✅ now | Renders `2026-04-29` as `April 29, 2026` in display preview. |
| `DisplaysAsPreview` | ✅ now | Renders the "Displays as: …" live region under formatted fields. |

### Interactive widget affordances (auxiliary input alongside the universal text field)

These add a parallel widget; the text field always remains the source of truth.

| Flag | Ships | Behavior |
|---|---|---|
| `CalendarPicker` | ✅ now | Shows `CalendarDatePicker` widget beside Date fields. |
| `BooleanRadios` | ✅ now | Shows Yes/No radios beside Boolean text input. |

### Input masks (reshape user-typed text in place)

| Flag | Ships | Behavior |
|---|---|---|
| `PhoneInputMask` | ✅ now | Live reshape of digit-only input to `(555) 123-4567`. |
| `SsnInputMask` | ✅ now | Live reshape to `###-##-####`. |
| `EinInputMask` | ✅ now | Live reshape to `##-#######`. |
| `ZipInputMask` | ✅ now | Live reshape to `#####-####` (5-digit untouched). |
| `CurrencyInputMask` | ✅ now | Reshape to `$1,234.56` on commit (LostFocus). |
| `PercentageInputMask` | ✅ now | Reshape to `12.5%` on commit (LostFocus). |

### Motion & motor

| Flag | Ships | Behavior |
|---|---|---|
| `ReducedMotion` | ✅ now | Disables decorative animations and transitions. |
| `LargeHitTargets` | ✅ now | Min click/tap target ≥ 44×44 px. (Currently in `MotorAssistProfile`; renamed and used as its own flag.) |
| `HitMarginPadding` | ⏳ later | Extra 8–12 px invisible padding around clickable elements — tremor near-misses still register. |

### Cognitive & comprehension

| Flag | Ships | Behavior |
|---|---|---|
| `SectionFocusMode` | ⏳ later | Collapses all sections except the one with focus. |
| `AlwaysVisibleHelp` | ⏳ later | Renders `helpText` inline below every field instead of on hover/expand. (Today: help text is *always* below fields — this flag controls future "expand-on-hover" alternatives.) |
| `InlineExamples` | ⏳ later | Shows `hints.examples[0]` as ghost text below placeholder, with "more" link. |
| `PlainLanguageLabels` | ⏳ later | Uses `hints.plainLanguage` (if present) instead of formal `label`. |
| `PersistentProgress` | ⏳ later | Pins "3 of 12 sections complete" + section list to the sidebar. |

### Screen-reader & voice control

| Flag | Ships | Behavior |
|---|---|---|
| `ScreenReaderTuned` | ✅ now | Verbose live regions; section-context announcements; suppresses noisy reshape events. |
| `PersistentToasts` | ⏳ later | Errors/info don't auto-dismiss — voice/switch users get time to acknowledge. |
| `KeyboardShortcutOverlay` | ⏳ later | Alt-press highlights every visible keyboard shortcut. |
| `VoiceControlNames` | ⏳ later | Adds Dragon-friendly short alternates ("save", "next", "phone") to `AutomationProperties.Name`. |

### Universal core (NOT flags — always on)

These behaviors are unconditional. They were considered for flag-status and rejected because every user benefits.

- Autosave on every edit (filled forms).
- Confirm-on-leave with unsaved changes.
- No hover-only UI: every reveal has a click/keyboard alternative.
- `AutomationProperties.Name` + `HelpText` on every interactive element.
- Section titles required (validation rejects unnamed sections).
- Help text rendered below every field (always-visible today).

---

## Named Presets (GUI surface)

Five presets ship as one-click options in Display Preferences. "Customize" exposes every flag in the table above.

Each preset table below shows its **target composition**. Presets compose only the flags in the ✅ now column today; later flags will be added when they ship without changing the preset's name or intent.

### 1 · Excellent vision (default for sighted, no other capabilities)

Most affordances on; nothing accessibility-specific.

| Flag | State |
|---|---|
| `LightTheme` (or OS-detected) | ✓ |
| All `*InputMask` flags | ✓ |
| `CalendarPicker`, `BooleanRadios`, `DisplaysAsPreview` | ✓ |
| `NumberThousandsSeparators`, `CurrencyDisplay`, `IsoDatePrettify` | ✓ |

### 2 · Blind / screen reader

Pure-text input; affordances that interrupt screen reader speech are off.

| Flag | State |
|---|---|
| `ScreenReaderTuned` | ✓ |
| `ReducedMotion` | ✓ |
| Live `*InputMask` flags (Phone/SSN/EIN/Zip) | ✗ — caret jumps and re-announcements disrupt speech |
| `CalendarPicker` | ✗ — typed ISO date faster |
| `BooleanRadios` | ✓ — arrow-key friendly |
| `CurrencyInputMask`, `PercentageInputMask` (commit-time) | ✓ — single announcement on LostFocus, low disruption |
| `DisplaysAsPreview` | ✓ — confirms intent via polite live region |
| `NumberThousandsSeparators`, `CurrencyDisplay`, `IsoDatePrettify` | ✓ — display-only, no input interruption |
| ⏳ later: `PersistentToasts`, `VoiceControlNames` | (target) |

### 3 · Low vision / high contrast

Sighted user with reduced acuity — affordances *help* by visually confirming input. Bigger everything.

| Flag | State |
|---|---|
| `HighContrastTheme` | ✓ |
| `LargeText`, `LargeHitTargets` | ✓ |
| All `*InputMask` flags | ✓ |
| `CalendarPicker`, `BooleanRadios`, `DisplaysAsPreview` | ✓ |
| `NumberThousandsSeparators`, `CurrencyDisplay`, `IsoDatePrettify` | ✓ |

### 4 · Cognitive / dyslexia / executive-function

Forms-specific cognitive support. Plain language, one thing at a time, visible help.

> **Today this preset is sparse** because most cognitive flags are ⏳ later. It enables the ✅ now flags listed below; when the ⏳ later flags ship, they'll be added automatically.

| Flag | State |
|---|---|
| `LargeText` | ✓ |
| All visual masks + display affordances | ✓ — visual confirmation aids comprehension |
| `CalendarPicker`, `BooleanRadios`, `DisplaysAsPreview` | ✓ |
| ⏳ later: `DyslexiaFont`, `IncreasedSpacing`, `SectionFocusMode`, `InlineExamples`, `PlainLanguageLabels`, `PersistentProgress` | (target) |

### 5 · Motor / mobility

Voice-control, switch, tremor, one-handed users.

> **Today this preset is sparse** because most motor flags are ⏳ later. It enables the ✅ now flags listed below; when the ⏳ later flags ship, they'll be added automatically.

| Flag | State |
|---|---|
| `LargeHitTargets` | ✓ |
| `ReducedMotion` | ✓ |
| All visual masks (visual confirmation aids) | ✓ |
| `CalendarPicker`, `BooleanRadios`, `DisplaysAsPreview` | ✓ |
| ⏳ later: `HitMarginPadding`, `PersistentToasts`, `KeyboardShortcutOverlay`, `VoiceControlNames` | (target) |

---

## Implementation in v0.1

### What ships now

1. **`VisualFormattingProfile` is split** into 12 individual flag profile classes (one per row in the "Visual display affordances" + "Interactive widget affordances" + "Input masks" sections).
2. **`MotorAssistProfile` is renamed** to `LargeHitTargetsProfile` for clarity (was a single-affordance bundle anyway).
3. **View wiring updates** so each affordance checks the flag for itself:
   - `DatePromptView` → `IsActive(CalendarPickerProfile)` controls picker visibility
   - `BooleanPromptView` → `IsActive(BooleanRadiosProfile)` controls radio visibility
   - `NumberPromptView` / `CurrencyPromptView` → `IsActive(DisplaysAsPreviewProfile)` controls preview visibility
   - `PhonePromptView` → `IsActive(PhoneInputMaskProfile)` gates the live mask
   - `TextPromptView` (fallback for ssn/ein/zip/percentage) → `InputMaskBehavior` looks up the right per-formatter flag
4. **`ProfilePresets`** static helper exposes `ApplyExcellent`, `ApplyBlind`, `ApplyLowVision`, `ApplyCognitive`, `ApplyMotor` — each composes the flag set listed above.
5. **`DisplayPreferencesView`** gets a preset picker plus a "Customize" expandable panel that exposes every flag as its own checkbox.
6. **Tests cover** every flag profile, the registry, the preset compositions, and the view wiring (GUI tests verify a preset switch produces the expected on-screen affordances).

### What's deliberately deferred

- All ⏳ later flags. Each lands as its own commit when implemented.
- `UndoBar` / `ConfirmDestructive` — depend on an undo stack that doesn't exist yet.
- Per-document preset hints (a form file recommending a preset to new users).
- Quick preset toggle from the menu bar (only Display Preferences for now).

---

## Open questions (deferred)

- **Should flag-state persist per-document or per-user?** Today the app persists per-user via `SettingsService`. A document containing `presetHint: "cognitive"` could nudge a new user toward a starting preset without overriding the user's saved choice.
- **Should presets be selectable per-form?** A government benefits form might ship with a "cognitive" hint baked in; the user can still override.
- **Do we expose preset switching in the menu bar, or only in Display Preferences?** Quick toggle from the menu would help users who switch contexts (work device vs personal).

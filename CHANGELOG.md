# Changelog

All notable changes to PromptResponse are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
once a release is tagged. The project is currently pre-1.0 / active
development; entries are grouped by date rather than versioned releases.

## [Unreleased]

### Dependency patch sweep — 2026-05-02

Follow-up to the bundle upgrade — picks up patch / minor releases that
landed in the days after the initial commit.

- `Microsoft.Extensions.*` 10.0.0 → **10.0.7** (DI, Logging.Console,
  Logging.Abstractions; .NET 10 servicing patches).
- `xunit.runner.visualstudio` 3.0.0 → **3.1.5** (minor; same v3 line).
- `Tmds.DBus` 0.92.0 → **0.93.0**.
- `coverlet.collector` / `coverlet.msbuild` 6.0.4 → **10.0.0** (project
  re-versioned to align with .NET 10; no breaking changes for our
  `<Threshold>` / `<ThresholdType>` / `<ExcludeByFile>` config).

`dotnet list package --outdated` now reports nothing. Tests: 1206
still passing on the freshened stack. Layer 3 AT-SPI smoke test still
passes against the live bus.

### Linux accessibility & dependency refresh — 2026-05-02

- **Linux screen-reader support**: Avalonia 12's AT-SPI2 backend ships,
  so Orca / NVDA-equivalent assistive tech on Linux can now see
  PromptResponse. App registers on the session AT-SPI bus as
  `PromptResponse` (via `Application.Name`); every focusable
  interactive node carries an automation name. Verified end-to-end via
  the Layer 3 smoke test (`./tests/at-spi/run_at_spi_smoke.sh`).
- **Three-layer accessibility test stack** for blind-user scenarios:
  - Layer 1 — `AutomationTreeTests`: walks the in-process Avalonia
    `AutomationPeer` tree of a loaded document and asserts every
    focusable control has a non-empty Name + a known ControlType.
  - Layer 2 — `KeyboardFlowTests`: drives the input pipeline a
    screen-reader user actually uses (Tab traversal, Enter on focused
    Next button, no silent dead-ends).
  - Layer 3 — `tests/at-spi/`: Python + PyGObject Atspi script + bash
    launcher that walks the live AT-SPI tree (the same one Orca
    consumes) and asserts the same Name + role invariants Layer 1
    enforces in-process.
- **Bundle dependency upgrade**:
  - `.NET 8` → `.NET 10` (LTS, supported through Nov 2028)
  - `Avalonia 11.1.3` / `11.2.1` → `12.0.2` (single source of truth,
    fixing prior version skew)
  - `Microsoft.Extensions.*` `8.0.0` → `10.0.0`
  - `CommunityToolkit.Mvvm` `8.3.2` → `8.4.2`
  - `xunit` `2.6/2.9` → `xunit.v3` `3.2.2` (Avalonia 12 needs it)
  - `Microsoft.NET.Test.Sdk` `17.8` → `18.5.1`
  - `Tmds.DBus` `0.15.0` → `0.92.0` (security fix; closes NU1903
    suppression we'd been carrying)
  - **Replaced** `FluentAssertions` 6.12 / 8.8 → **AwesomeAssertions**
    9.4.0. FluentAssertions v8 changed to a paid commercial license in
    Jan 2025. AwesomeAssertions is the actively-maintained Apache-2.0
    community fork. 82 test files migrated.
  - **Replaced** `Moq` 4.20.70 → **NSubstitute** 5.3.0. NSubstitute is
    MIT-licensed, has cleaner syntax, and no SponsorLink-class trust
    incidents. 16 test files migrated across 38 `Mock<T>` usages, 19
    `.Setup`, 8 `.Verify` call sites.
- Drag-drop API ported from `IDataObject` to `IDataTransfer` /
  `DataFormat<object>` / `DataFormat.CreateInProcessFormat` per
  Avalonia 12 breaking changes.
- `LangVersion` raised to `preview` so CommunityToolkit.Mvvm 8.4
  source generators can use the latest C# features.
- Drop the `DOTNET_ROLL_FORWARD=Major` workaround that wrapped every
  `dotnet test` — projects now natively target the installed .NET 10 SDK.
- Tests: 1206 passing across Core (405) + Desktop (605) + CLI (96) +
  AccessibilityTests (100). New: 6 `AutomationTreeTests`, 6
  `KeyboardFlowTests`, plus the Layer 3 framework.

### Wizard mode — 2026-05-02

- New **section-at-a-time view** with Previous / Next navigation, step
  label ("Section 3 of 12: Employment History"), and a duplicate nav at
  the bottom of long sections. Reduces cognitive load on forms with
  many sections (SF-86 has 111 prompts across 12 sections).
- **`WizardModeProfile`** capability flag — composes with presets, persists
  across launches via the existing profile-state save path. The
  Cognitive / Dyslexia preset auto-enables it.
- `View → Toggle Wizard Mode` (Ctrl+W) menu item; matching toggle in
  Display Preferences.
- `--wizard` CLI flag: launch directly into wizard mode (e.g.
  `dotnet run --project src/PromptResponse.Desktop -- --open path.aprt --wizard`).
- 10 new `WizardModeTests` + 5 new `WizardModeGuiTests`.

### View → Capability Profile menu + Display Preferences redesign — 2026-05-02

- New `View → Capability Profile` submenu with one-click presets
  (Excellent vision / Blind-SR / LowVision-HC / Cognitive-Dyslexia /
  Motor-Mobility) — saves a trip through the prefs panel.
- Display Preferences gained an "Active enhancements" panel at the top
  with a count badge listing every active flag at a glance, plus a
  style that bolds + accent-tints the label of every checked CheckBox.

### APRT structural editor — 2026-05-02

- **Inline editor** for templates: section title / description, prompt
  label, type ComboBox, hints expander (placeholder, help text, validation
  pattern). Add / remove prompts, nested sections, top-level sections.
- **Document metadata editor** (collapsible expander): title, description,
  author, template id, template version. Live-updates the page header.
- **Table authoring**: convert section to fixed / dynamic table, column
  list editor (label / type, id auto-generated), fixed-row list editor,
  add column / row, remove table layout. Dynamic-row config: row label
  prefix, min / max bounds.
- **Drag-and-drop reorder** for prompts, nested sections, top-level
  sections, table columns, and fixed rows. ⋮⋮ drag handles per item.
  Reorders go through the EditHistory so they're undoable. Avalonia
  12's `IDataTransfer` API.
- **Undo / redo** (`Ctrl+Z` / `Ctrl+Y`): every authoring mutation
  routes through an `EditHistory`. Property edits (Title, Label, etc.)
  collapse consecutive same-target keystrokes within 500 ms into a
  single undo step. Branching on undo drops the redo stack (standard
  editor convention). Cleared on every document load.
- New tests: 18 `EditorMutationTests`, 12 `UndoRedoTests`, 8
  `ReorderTests`, plus the `TableSectionViewGuiTests` from the table
  refactor.

### Tables to nested Section + Prompt model — 2026-05-02

- Tables are now modeled as a `Section` with `Section.TableLayout` set
  rather than a `Prompt` with `expectedDataType="table"`. Row
  sub-sections live in `Section.Sections`; cell prompts live in each
  row's `Prompts` with `id = "{rowId}.{columnId}"`.
- **Database imports are now trivial**: every cell value is a top-level
  `Prompt.Response` keyed by id. External code iterating the prompt
  tree finds `q1.revenue = "100000"` directly without parsing nested
  JSON.
- `DataTypeValidator` no longer special-cases `table` — cells are
  validated as their own typed prompts (currency, date, etc.).
- `examples/field-types-showcase.aprt` updated to the new shape.
- 21 new tests: `TableSectionViewModelTests` (15), `TableMigrationTests`
  (no longer needed once the migration was dropped), and the
  `TableSectionViewGuiTests` (5 live-render).

### Dead-code cleanup — 2026-05-02

- Removed `IPlatformFeatures` + `PlatformFeatures` (entirely dead, ~75
  lines).
- Removed entire `Converters/` directory (13 unused IValueConverter
  classes, 384 lines).
- Trimmed `IDialogService` (3 dead Show*Async methods, ~120 lines).
- Trimmed `ISettingsService` (4 dead methods + 3 dead `AppSettings`
  fields).
- Removed dead DI registrations and stale package refs
  (`Avalonia.ReactiveUI`, `Avalonia.Controls.DataGrid`).
- Tightened `AvaloniaResource` glob to image-only — README.md /
  generate-icons.sh / icon-preview.html no longer embed into the
  binary.

## Earlier highlights (pre-changelog)

These predate this changelog file; see `git log` for full history.

- 2026-04-29 Per-cell table editor + nested-section visual polish.
- 2026-04-29 Default-on auto-formatters for sighted users + persisted
  capability profile.
- 2026-04-28 CLI coverage ratchet from 28% to ~83% (idlergear #24).
- 2026-04-28 Comprehensive interactive GUI test coverage — menu bar +
  every prompt type.
- 2026-04-27 `MixedScriptAdvisor` for url + email — browser-style
  homoglyph defense (Cyrillic 'а' in аpple.com).
- 2026-04-27 Per-hint-type strict sanitization for url + email.
- 2026-04-27 `HiddenCharacterAdvisor` + right-rail Advisories list.
- 2026-04-27 NFC normalize + strip always-abusive characters at the
  serialization boundary.
- 2026-04-26 Live progress + advisory updates + advisory linkback.
- 2026-04-26 Real `App` loaded in headless harness (FluentTheme was
  missing — caused tests-pass-while-live-fails class of bug).
- 2026-04-25 Capability profiles split into 12 individual flag
  profiles + 5 named presets.

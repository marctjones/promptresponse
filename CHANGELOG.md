# Changelog

All notable changes to PromptResponse are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Document → APR skill** (`.claude/skills/document-to-apr/`) — a portable AI
  skill that turns an existing form into an APR template: point any capable agent
  (Claude Code/workspace, Gemini CLI, Codex) at a PDF, Word (`.docx`),
  OpenDocument (`.odt`/`.odp`), or even an **image/scan** of a paper form, and it
  emits a valid `.aprt`. Chosen over a mechanical parser because most real forms
  aren't machine-readable (flat PDFs, scans, photos) — an agent reads them the way
  a person does. Includes a self-contained format spec, worked examples, and a
  `apr validate` verification step.

- **Fillable table cells** — fixed-row table cells are now interactive in both
  fillable outputs: in the **HTML** web form each cell becomes a checkbox
  (boolean column), dropdown (suggested values), or typed input keyed by its
  `{rowId}.{columnId}` id (round-tripping through the download shim); in the
  **PDF** form each cell becomes a live AcroForm field of the matching kind, via
  pdfe 2.7.0's new `FillableTable`. Each cell's accessible name combines its row
  and column headers. (Dynamic tables, which have no rows, render as headers.)

- **HTML export** — `apr export <file> --format=html` renders a self-contained,
  accessible HTML page (semantic headings, labeled fields, real tables with
  row/column headers, `lang` attribute, minimal inline CSS). All dynamic text is
  HTML-encoded (an XSS boundary, since responses are arbitrary input). New
  dependency-free `HtmlDocumentRenderer` on the shared `IDocumentRenderer` seam —
  the foundation for a future browser fill/view path (Milestone 2 wedge, Path B).
- **Fillable HTML (browser fill path)** — `apr export <file> --format=html --fillable`
  produces a self-contained, **interactive** web form: prompts become live inputs
  (boolean → checkbox, suggested values → dropdown, multiline → textarea, otherwise
  a typed input), pre-filled from any existing responses, with a **Download filled
  form** button that writes a valid `.aprf` JSON file — no server, no backend, no
  toolchain. Open in any browser, fill, download. Accessible (associated labels,
  `aria-describedby` help); the embedded document is unicode-escaped and all values
  HTML-encoded (XSS boundary). New `FillableHtmlDocumentRenderer`.
- **Desktop HTML export menu** — File → Export now offers **Export as HTML** and
  **Export as fillable web form** alongside the PDF options (both with screen-reader
  names and help text), so the browser fill path is reachable without the CLI.

## [0.4.1] - 2026-06-06

Completes **Milestone 2** end-to-end: live reactive expressions in the desktop
fill view (computed fields auto-update, conditional show/hide, read-only) on top
of the v0.4.0 engine, plus an **Order Form** starter template demonstrating a
computed line total and conditional fields.

### Added

- **Order Form demo template** — a bundled, accessibility-validated starter
  showing a computed `line_total` (`exprValue`) that updates live, a gift message
  that appears only for gifts (`exprHidden`), and a rush reason that becomes
  expected when rush delivery is selected (`exprExpected`). Selectable from the
  home screen; exercised end-to-end by a test.
- **Live reactive expressions in the fill view** (Milestone 2, Phase 3b) — as the
  user types, the desktop form now recomputes computed fields (`exprValue`,
  read-only, fixpoint), shows/hides prompts (`exprHidden`), and toggles read-only
  (`exprReadOnly`). `PromptViewModelBase` gains `IsVisible`/`IsReadOnly`; the shell
  re-evaluates on every response change (re-entrancy-guarded) and on load; the
  prompt container binds visibility/enablement. Cross-field validation already
  surfaced in the advisory panel. Calculation engine + conditional logic are now
  end-to-end.

## [0.4.0] - 2026-06-06

Headline: **Milestone 2 foundations + polish.** A safe, pure-data **expression
engine** (CEL subset, no code execution) and `expr*` form hints power
**computed fields** (with fixpoint recompute for chained totals),
**conditional visibility / required**, and **cross-field validation** —
evaluated by `FormExpressions` in Core, with validation already live in the
advisory panel (live computed/show-hide UI lands next). Plus: **macOS release
builds** (Apple Silicon + Intel), **per-section progress**, **advisory
click-to-field**, and a **profiles consolidation**. ~1300 tests across the stack.

### Added

- **Cross-field validation in the advisory panel** — `exprValidation` results now
  appear as advisories (clickable to the field), alongside the data-type /
  hidden-character / mixed-script advisors. Advisory-only, never blocking. First
  reactive surfacing of the Milestone 2 expression hints; live computed-value /
  show-hide UI is the next phase.
- **Expression hints + form evaluation** — `PromptHints` gains `exprHidden`,
  `exprValue`, `exprExpected`, `exprValidation`, `exprReadOnly` (CEL-subset
  strings; round-trip through JSON). `FormExpressions` (Core) evaluates them
  against live responses: conditional visibility, computed read-only values with
  **fixpoint recompute** for chained totals (circular-safe), conditional-required,
  and cross-field validation — all advisory, never blocking. Reactive desktop UI
  wiring is the next phase. (Milestone 2)
- **Expression engine** (`PromptResponse.Core.Expressions`) — a safe, pure-data
  evaluator for the spec's CEL subset (Appendix B/C): prompt ids are variables
  holding response strings, with `_this`/`_today` built-ins, `ctx.*` context, the
  `int`/`double`/`timestamp`/`size`/`matches`/`string` functions, and standard
  operators. No code execution; bounded nesting + length and regex timeouts;
  missing variables degrade to null. Foundation for Milestone 2 (computed fields,
  conditional visibility, cross-field validation). 48 tests.

- **macOS release builds** — the release workflow now produces self-contained
  tarballs for Apple Silicon (`osx-arm64`) and Intel (`osx-x64`) on a macOS
  runner, alongside the Linux tarball and Windows installer/zip. Unsigned (first
  launch needs a Gatekeeper approval); a notarized `.app` is a follow-up. (#41)
- **Per-section completion progress** — the right-rail Progress panel now lists
  each top-level section's answered/total (with a ✓ when complete) under the
  overall progress bar; each row is accessibility-named. `FormProgressViewModel`
  exposes a `Sections` collection of `SectionProgress`. (#38)
- **Advisory click-to-field** — advisories in the right-rail panel are now
  accessible buttons; activating one requests focus on the offending field (the
  view scrolls it into view and focuses its input). (#39)

### Changed

- **Profiles: consolidated the input-mask markers** — the 6 identical input-mask
  marker classes now live in one `InputMaskProfiles.cs` behind a shared
  `IInputMaskProfile` interface, with the types unchanged (so formatter gating,
  Display Preferences toggles, and settings persistence are unaffected). The
  deeper "single profile + strategy table" merge was intentionally not done —
  those types are load-bearing gate identities. (#40)

## [0.3.0] - 2026-06-06

Headline: the **shippable MVP**. PDF export — **flat and fillable AcroForm** —
from both the CLI (`apr export --format=pdf [--fillable]`) and the desktop
File → Export menu, on a new `IDocumentRenderer` seam over the pure-managed
[pdfe](https://github.com/marctjones/pdfe) engine (fillable fields are
screen-reader-named; PDFs carry document metadata). A real **home screen** with
recent files + a **starter-template gallery** (6 bundled, accessibility-validated
office forms) and first-run onboarding. **Self-contained, single-file packaging**
with installers and `.apr`/`.aprt`/`.aprf` file associations. CI moved to
.NET 10; dependency patch sweep; the unused Java BouncyCastle dependency removed
(cleared 6 Dependabot alerts). ~1300 tests passing across Core / CLI / Desktop /
Accessibility / Rendering.Pdf.

### Added

- **Starter templates** — 6 bundled, accessibility-validated office forms
  (time-off request, expense report, IT access request, contact intake, event
  registration, incident report) shipped in a `Templates/` folder next to the
  binary and surfaced as a **"Start from a template"** gallery on the home
  screen; selecting one opens a fresh unsaved copy. New `ITemplateCatalogService`.
  Each template is hard-asserted accessible in CI (title, section/prompt labels,
  help text, unique ids/labels). (#36)
- **First-run onboarding** — a getting-started hint on the home screen (shown
  until the user has opened or saved something) pointing new users to the
  starter templates and F1 shortcuts. (#35)
- **PDF exports carry document metadata** — generated PDFs (flat and fillable)
  now stamp the APR title / author / description onto the PDF Info dictionary
  (Title / Author / Subject) via pdfe v2.5.0's metadata authoring, for
  provenance and search. (Unicode text rendering through the high-level builder
  is still pending upstream — tracked in pdfe#398.)
- **Packaged, self-contained distribution** — `scripts/publish.sh` produces
  single-file, self-contained `promptresponse` (GUI) and `apr` (CLI) binaries
  that run with **no .NET runtime installed** (verified under a stripped
  `env -i`). A `release.yml` workflow attaches a Linux tarball, a Windows
  portable zip, and a Windows Inno Setup installer to the GitHub release on a
  `v*` tag. File associations for `.apr` / `.aprt` / `.aprf`: a per-user Linux
  installer (`packaging/linux/install-desktop.sh` — MIME types + desktop entry
  + default handler) and Windows registry associations via the installer.
  Docs: `docs/PACKAGING.md`. macOS packaging deferred (#41). (#33)
- **Desktop: home screen with recent files** — the empty state is now a real
  entry point. It lists recently opened/saved documents (most-recent-first,
  de-duplicated, capped, persisted in `settings.json`) as accessible buttons
  that reopen the form in one click, alongside the existing New/Open actions.
  New `IRecentFilesService`; recent files are recorded on open/save and survive
  restarts. (#34)
- **Desktop: File → Export menu** — export the currently open form, **with its
  current values**, to a PDF without leaving the app: *Export as PDF…* (flat)
  and *Export as fillable PDF form…* (interactive AcroForm). Both use a save
  dialog and the shared `IDocumentRenderer` seam; menu items are
  accessibility-named with help text. Previously PDF export was CLI-only.
- **Fillable PDF form export** — `apr export <file> --format=pdf --fillable
  --output=<file>` renders a template (or filled form) to an interactive
  **AcroForm** PDF: each prompt becomes a live field — `boolean` → checkbox,
  prompts with suggested values → dropdown, `multiline` → multi-line text, else
  a text field — named by the prompt id, with any existing response as the
  default. A blank template thus becomes a fillable PDF usable in any viewer
  (`FillablePdfDocumentRenderer`, on pdfe v2.4.0's AcroForm authoring). Without
  `--fillable`, `--format=pdf` still produces a flat PDF. Tables render
  read-only for now (per-cell fields are a follow-up); field tooltips/tagging
  deferred upstream (pdfe#380/#275). (#31)
- **PDF export** — `apr export <file> --format=pdf --output=<file>` renders a
  filled form (or template) to a flattened PDF. New `PromptResponse.Rendering.Pdf`
  project implements the `IDocumentRenderer` seam on top of the
  [pdfe](https://github.com/marctjones/pdfe) engine (`Pdfe.Core`, MIT, pure-managed,
  consumed as a locally-packed NuGet to keep the repos decoupled). Layout
  (word-wrap, pagination, tables) is handled by pdfe v2.4.0's high-level
  `PdfDocumentBuilder` facade (pdfe#383), so the renderer is a thin map from the
  shared `RenderModel`. `--exclude-empty` omits unanswered fields. MVP scope: flattened, Latin-text
  (base-14) output; Unicode font embedding, a fillable-AcroForm variant, and
  tagged/accessible PDF are deferred (tracked upstream in pdfe #378/#380/#275).
  (#31)
- **Document rendering seam** (`PromptResponse.Core.Rendering`) — a single,
  layout-free document traversal (`DocumentRenderModelBuilder`) that flattens an
  `AprDocument` into an ordered `RenderModel` of semantic blocks (headings,
  fields, tables), plus an `IDocumentRenderer` contract and a dependency-free
  `PlainTextDocumentRenderer` reference implementation. This lets PDF / text /
  HTML / print share one tree walk instead of each re-implementing traversal
  (the export commands currently triplicate it). Foundation for PDF export.
  Tables flatten to header + row/cell blocks with cells matched by
  `{rowId}.{columnId}`. No third-party rendering dependency enters Core.
  (#32)

### Changed

- **CI builds on .NET 10.** The workflow installed only the .NET 8 SDK while the
  solution targets `net10.0`, so CI could not build. Bumped `setup-dotnet` to
  `10.0.x` across all three jobs (and corrected the ".NET 8" step names).
- **`LangVersion` `preview` → `latest`** (stable C# 14, shipped with the .NET 10
  SDK). The solution no longer opts into unreleased language features; verified
  it builds unchanged.
- **Dependency patch sweep** (latest stable within the same major lines):
  Avalonia `12.0.2 → 12.0.4`, `Microsoft.Extensions.*` `10.0.7 → 10.0.8`,
  `Microsoft.NET.Test.Sdk` `18.5.1 → 18.6.0`, `coverlet` `10.0.0 → 10.0.1`,
  `Tmds.DBus` `0.93.0 → 0.94.1`. 1238 tests pass; About-dialog acknowledgements
  drift-guard still green (majors unchanged).
- **pdfe `Pdfe.Core` `2.4.0 → 2.5.0`** — minor, additive (no breaking change,
  enforced upstream by the public-API gate). v2.5.0 completes the writer epic
  (pdfe#382). Immediate win with **no code change**: `PdfDocumentBuilder` now
  defaults each fillable field's `/TU` accessible name to its label, so
  **fillable-PDF form fields are screen-reader-named out of the box** (asserted
  by a new renderer test). Newly available for future use: Unicode text +
  embedded fonts (pdfe#378), `DrawText` word-wrap (#379), document
  metadata/`/Lang` (#381), and `/MaxLen`/comb/date-field options (#380).
  Vendored from the official v2.5.0 release `.nupkg`.

### Security

- **Java SDK: removed the unused BouncyCastle dependency** (`bcprov-jdk15on` /
  `bcpkix-jdk15on` 1.70), resolving 6 moderate Dependabot alerts
  (GHSA-4h8f-2wvx-gg5w, -wg6q-6289-32hp, -4cx2-fc23-5wg6, -v435-xc8x-wvr9,
  -8xfc-gm6g-vgpv, -hr8g-6v94-x4m9). No Java source ever imported
  `org.bouncycastle.*` — `DigitalSignature` is a plain string POJO, consistent
  with APR treating signatures as data, not computed crypto. (The `jdk15on`
  artifacts were also discontinued after 1.70.) Aligns with the project's
  decision to drop signature/certificate/encryption features. If signing is ever
  implemented, re-add the current `jdk18on` artifacts (≥ 1.84).

## [0.2.0] - 2026-05-03

Headline: capability-profile rendering system with five named presets,
APRT structural editor with undo/redo + drag-drop, wizard mode,
Linux native screen-reader support (AT-SPI2), polymorphic prompt views,
hidden-character + mixed-script advisors, full bundle upgrade to
.NET 10 + Avalonia 12 + xunit.v3, MIT/Apache-2.0-only test stack
(NSubstitute + AwesomeAssertions), and ~95%+ line coverage gates
across Core / CLI / Desktop. 75 commits since v0.1.0; the most
significant changes are detailed below.

### About dialog with open-source acknowledgements — 2026-05-02

Replaces the inline 1-paragraph "About" window with a dedicated
`AboutDialog` that surfaces every runtime third-party dependency with
name, version, and license, so the user-facing binary doesn't
under-disclose its open-source inheritance.

The acknowledgements list is hand-maintained on `AboutDialog`. Three
xUnit guards in `AboutDialogAcknowledgementsTests` fail the build if it
drifts from the actual `<PackageReference>` entries in the runtime
.csprojs (Desktop / Core / Cli):

- every runtime `PackageReference` must have an entry,
- every entry's major version must match the .csproj,
- every entry must declare a non-empty license string.

Test-only deps (xUnit, NSubstitute, AwesomeAssertions, coverlet,
Avalonia headless harness) are intentionally excluded — they don't ship
in the runtime binary.

### Documentation: Expression parser correctly marked as planned — 2026-05-02

The "Expression parser ✅" row in `docs/FEATURES.md` was misleading:
there is no expression evaluator anywhere in `src/PromptResponse.Core`.
Marked as ⏳ Planned and pointed readers to
`APR_SPECIFICATION_v0.2.md`, which sketches the forward-looking
CEL-style hint design.

### Test coverage: Desktop ViewModels gap closure — 2026-05-02

Added 29 tests targeting the lowest-coverage Desktop ViewModels.

- New `DocumentMetadataViewModelTests` (8 tests): every property setter
  (Title / Description / Author / TemplateId / TemplateVersion) propagates
  through to the underlying `Metadata` model, raises `PropertyChanged` +
  `Changed`, no-op assignments short-circuit cleanly, every field is
  individually undoable, consecutive same-field keystrokes merge into one
  undo step, edits across different fields don't merge, the
  `IsApplying` short-circuit during command replay works, and the
  no-history path still propagates writes.
- New `EditingCommandUndoRedoTests` (21 tests): every structural editing
  command's Execute → Undo → Redo cycle. Closes the previously-zero
  coverage on `AddTopLevelSection` / `RemoveTopLevelSection` /
  `RemoveColumn` / `RemoveFixedRow` / `RemoveNestedSection` /
  `RemoveTableLayout` Undo paths, and the redo branch on the
  captured-state add commands (`AddColumn` / `AddFixedRow`) where the
  command reuses the captured column / row instance instead of
  synthesizing a fresh one.

Coverage moves:

| Class | Before | After |
|---|---|---|
| `DocumentMetadataViewModel` | 25.6% | **100.0%** |
| `RemoveColumnCommand` | 0.0% | 78.6% |
| `RemoveTableLayoutCommand` | 0.0% | 75.0% |
| `AddTopLevelSectionCommand` | 0.0% | 66.7% |
| `RemoveFixedRowCommand` | 0.0% | 66.7% |
| `RemoveNestedSectionCommand` | 0.0% | 66.7% |
| `AddColumnCommand` | 70.6% | 88.2% |
| `AddFixedRowCommand` | 68.8% | 87.5% |
| Desktop module overall | 74.88% line / 76.38% method | **78.70%** / **80.44%** |

Tests now: 1235 passing across Core (405) + Desktop (634, +29) + CLI (96)
+ AccessibilityTests (100). Skipped: 4 (unchanged — known harness limits).

### Mutation testing: Stryker.NET attempted, integration broken — 2026-05-02

Tried Stryker.NET 4.14.1 against `PromptResponse.Core.Tests` to validate
that the 93% line coverage actually catches behavioral changes. The full
1049-mutant run completed in ~15 min but reported **Killed: 0 / Survived:
542 / Timeout: 197**, scored 26.66% — the score is entirely from
timeouts, no real test executions. A narrow-scope re-run on a single
file (`Models/Prompt.cs`, 5 mutants) reproduced the same shape: zero
kills, all timeouts.

Symptom: Stryker emits `It looks like the test coverage capture failed.
Disable coverage based optimisation.` during analysis. The VsTest +
xunit.v3 3.2.2 path doesn't appear to actually run our 405 tests against
mutants.

Rather than commit dead scaffolding (a tool manifest + config that
yield meaningless scores), Stryker is held off until a Stryker.NET /
xunit.v3 fix lands. Tracked as a follow-up; line coverage remains the
primary gate for now.

### Dependency policy: don't pin transitives — 2026-05-02

Recorded as a comment in `tests/Directory.Build.props`. We let our direct
deps (Microsoft.NET.Test.Sdk, xUnit, NSubstitute, Avalonia, etc.) drive
what transitive versions get resolved. We tried force-pinning the
transitive `Microsoft.Testing.Platform` 1.9.1 → 2.2.2 to silence
NU1903-style noise; that pulled in `OpenTelemetry.Api` 1.15.1 with
[GHSA-g94r-2vxg-569j](https://github.com/advisories/GHSA-g94r-2vxg-569j),
introducing a *new* vulnerability while trying to chase a non-issue.
Lesson: only promote a transitive to a direct ref for **security
remediation**, not "freshness." Track via `dotnet list package` with the
`--vulnerable` / `--include-transitive` flags in CI.

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
- 2026-04-28 CLI coverage ratchet from 28% to ~83%.
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

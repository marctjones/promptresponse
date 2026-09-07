# Accessibility test suite

## What this project actually is

Every test here is **static analysis** — reading `.axaml`, `.cs`, and `.aprt`
files as text or XML and asserting on their content. Nothing renders a window,
launches the app, or queries a live accessibility tree. There is no
`IAccessibilityInspector` abstraction, no per-platform inspector, and no
`[Fact(Skip = ...)]` integration test waiting to be un-skipped — an earlier
design along those lines was never built; this suite replaced it.

Runtime, in-process evidence (a real Avalonia window, real automation
properties read off the rendered tree) lives in
`tests/PromptResponse.Desktop.Tests/Gui/AutomationTreeTests.cs` and the other
`Gui/` tests, not here. Live, native-platform evidence (an actual screen
reader on an actual OS) lives in `tests/at-spi/` (Linux, AT-SPI2, run
manually) and `scripts/verify-macos-accessibility.sh` (macOS, opt-in,
Accessibility permission required). This project's job is narrower and
cheaper: catch a missing accessible name, an untitled section, or a
documented-but-unenforced keyboard convention, in CI, in milliseconds.

## Test files

| File | Checks |
| --- | --- |
| `XamlAccessibilityValidationTests.cs` | Every interactive `.axaml` element has `AutomationProperties.Name`; no placeholder-only labels. |
| `ColorContrastTests.cs`, `ColorContrastValidationTests.cs` | WCAG contrast ratio math over `src/PromptResponse.Desktop/Profiles/ColorTokens.cs`. |
| `AprAccessibilityValidationTests.cs` | Shipped `.aprt` files: titles present, labels unique within a section, section structure sane. |
| `KeyboardNavigationValidationTests.MainWindow.cs`, `.Conventions.cs`, `.Documentation.cs` | String-level checks against `MainShellView.axaml` and this repository's documented keyboard conventions — e.g. that a given accelerator string appears. These check that the *documentation and XAML agree*, not that the accelerator works at runtime; see the caveat below. |
| `ViewInventoryTests.cs` | Anti-shrink guard: every prompt-type view referenced by `PromptDataTemplateSelector.cs` has a corresponding `.axaml` file on disk. |

## Known limit: string-level keyboard checks

Because `KeyboardNavigationValidationTests.MainWindow.cs` asserts that a
literal string like `"Ctrl+W"` appears in the XAML, it cannot detect two menu
items binding the *same* accelerator, or an accelerator that is declared but
never actually reaches the running app. Runtime keyboard-flow evidence — Tab
order, Shift-Tab return, actual key delivery — comes from
`tests/PromptResponse.Desktop.Tests/Gui/KeyboardFlowTests.cs`, a headless
Avalonia test, not from this project.

## Running

```bash
dotnet test tests/PromptResponse.AccessibilityTests
```

This is a **mandatory CI gate** (`.github/workflows/ci.yml`, job
`accessibility-gate`) with no `needs:` — it runs independently of the rest of
the solution and blocks merge on its own failure, regardless of other test
status.

## Manual, live testing

- **Linux, live AT-SPI bus:** `tests/at-spi/run_at_spi_smoke.sh` (see that
  directory's own README) — not part of `dotnet test`, run manually before a
  release.
- **macOS, live NSAccessibility tree:**
  `scripts/package-macos-app.sh` then `scripts/verify-macos-accessibility.sh`,
  which requires granting the invoking terminal Accessibility permission.
- **Windows:** no automated UI Automation harness exists yet — tracked in the
  accessibility milestone.

Record per-release, per-platform manual evidence in
`docs/release/ACCESSIBILITY_SIGNOFF.md`, which names exactly what a machine
cannot check (whether structure is *comprehensible*, whether narration is
well-timed, whether a tab order is *sensible*) rather than duplicating what
this suite already covers.

## Related documentation

- [docs/UX_ACCESSIBILITY.md](../../docs/UX_ACCESSIBILITY.md) — design intent and evidence.
- [docs/RENDERER_CONFORMANCE.md](../../docs/RENDERER_CONFORMANCE.md) — the conformance contract chapter 13 is scored against, including what a renderer driver decides that this suite does not.
- [docs/release/ACCESSIBILITY_SIGNOFF.md](../../docs/release/ACCESSIBILITY_SIGNOFF.md) — the per-release manual checklist.

# PromptResponse

## Mission
Replace rigid PDF/Word forms with a flexible, semantic, JSON-based format (.apr) that separates content from presentation. Office workers should be able to create and fill forms without fighting layout tools, and downstream systems should get clean structured data without parsing PDFs.

## What it is (post-Phase 5 baseline)
A cross-platform .NET 10 / C# 14 / AvaloniaUI 12 application stack:
- **PromptResponse.Core** — platform-agnostic models, JSON serialization, advisory validation. >95% line coverage gated in CI.
- **PromptResponse.Desktop** — Avalonia 12 app on the new MainShell stack:
  thin MainShellViewModel composing DocumentSessionService + ProfileService +
  FormProgressViewModel + SearchViewModel + PromptViewModelFactory; all using
  CommunityToolkit.Mvvm source generators. Three-column layout with native menu,
  screen-reader live region, empty state, and an F1 keyboard shortcuts dialog.
- **PromptResponse.Cli** — validate, info, new, fill, stats, diff, export.

Document model: AprDocument → Sections → (nested Sections) → Prompts (unlimited depth). Two document types: `template` (.aprt, blank) and `filledForm` (.aprf, with responses). `.apr` is the legacy auto-detect extension.

## Architecture: capability profiles (vision-anchored)
Every user has a capability profile — a set of sensory, motor, cognitive capabilities. Built as universal core + composable enhancements:
- **Core (mandatory):** semantic structure, keyboard reachable, screen-reader coherent, raw values, no required color signals or animations. Floor everyone shares.
- **Profiles (composable, optional):** Default, Light, Dark, HighContrast, VisualFormatting, LargeText, ReducedMotion, ScreenReaderTuned, MotorAssist. Compose via CompositeProfile ("most accommodating wins"). OS preferences auto-detected at startup.
- **No "normal user" baseline.** Visual formatting for sighted users is an accommodation for that capability profile, exactly the way verbose screen-reader announcements are an accommodation for a different profile. Sighted users aren't privileged; they're served by an enhancement layer.

## Non-negotiable principles
1. **Responses are always strings.** `expectedDataType` is a hint to the UI, never enforced. Any visible text is a valid response. ValidationResult.IsValid depends only on structural Errors; type-hint mismatches surface as advisory Warnings.
2. **Accessibility is a CI gate, not a feature.** WCAG 2.1 AA on Light/Dark, AAA on HighContrast — every token pair on every theme is verified by ColorContrastTests in CI. Every interactive control needs AutomationProperties.Name. Every keyboard shortcut is exercised by a test.
3. **Local-first.** Offline must work. No cloud dependencies in core flows.
4. **TDD.** Failing test first, then minimum code, then refactor. Avalonia.Headless harness for GUI automation (keyboard + mouse + visual-tree queries).
5. **Pure data, no code execution.** APR files are safe to open from untrusted sources. No scripting, no layout info. Tables hold values only — no width/alignment/style fields.
6. **Open format.** JSON fully documented. No vendor lock-in.
7. **Stable, unique IDs.** IDs unique within scope; don't change on reorder.

## Current state (0.1 baseline)
**All 6 phases through visual design + integration are complete.** Live app uses:
- Capability-profile rendering (9 profile classes + CompositeProfile + ProfileService + OS auto-detect + Display Preferences UI)
- Polymorphic prompt views (14 typed VMs + DataTemplateSelector + 6 dedicated views with text fallback for the rest)
- New MainShell composition (DocumentSessionService, FormProgressViewModel, SearchViewModel) — all CommunityToolkit.Mvvm
- Native menu, three-column layout, screen-reader-friendly status bar live region, empty state, F1 shortcuts dialog
- WCAG-gated contrast tests + keyboard navigation tests + 11+ GUI automation tests using Avalonia.Headless
- ~700 tests passing across Core / Cli / Desktop / Accessibility, 0 failing
- Zero S3, signature, certificate, or template-update code

## Decision compass
When choosing between options: simplicity over features, semantic structure over visual fidelity, advisory hints over enforcement, local-first over cloud, accessibility over polish, open format over proprietary convenience. Don't reintroduce S3/signature/certificate features without explicit user approval. Treat profiles as additive — never let one profile's enhancement break another profile's experience.
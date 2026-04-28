# PromptResponse

## Mission
Replace rigid PDF/Word forms with a flexible, semantic, JSON-based format (.apr) that separates content from presentation. Office workers should be able to create and fill forms without fighting layout tools, and downstream systems should get clean structured data without parsing PDFs.

## What it is
A cross-platform .NET 8 / C# 12 / AvaloniaUI 11 application stack:
- **PromptResponse.Core** — platform-agnostic models, serialization (System.Text.Json), advisory validation
- **PromptResponse.Desktop** — AvaloniaUI MVVM app for template authoring and form filling
- **PromptResponse.Cli** — validate, info, new, fill, stats, diff, export

Document model: AprDocument → Sections → (nested Sections) → Prompts (unlimited depth). Two document types: `template` (.aprt, blank) and `filledForm` (.aprf, with responses). `.apr` is the legacy auto-detect extension.

## Non-negotiable principles
1. **Responses are always strings.** `expectedDataType` is a hint to the UI, never enforced. Never block user input.
2. **Accessibility is a requirement, not a feature.** WCAG 2.1 AA minimum. All UI elements need `AutomationProperties.Name` (and `HelpText` when help exists). All APR docs need unique descriptive labels and section titles. Run `dotnet test tests/PromptResponse.AccessibilityTests` before committing UI changes.
3. **Local-first.** Offline must work. No cloud dependencies in core flows.
4. **TDD.** Failing test first (incl. accessibility), then minimum code, then refactor. Target >80% coverage.
5. **Pure data, no code execution.** APR files are safe to open from untrusted sources. No scripting, no layout info.
6. **Open format.** JSON is fully documented. No vendor lock-in.
7. **Stable, unique IDs.** IDs unique within scope; don't change on reorder.

## Target users
- **Primary:** small government offices (<50k residents) — limited IT, paper-form burden, accessibility compliance
- **Secondary:** community organizations (non-profits, churches, schools)
- **Tertiary:** small businesses with privacy-sensitive intake

## Current state (Phase 1: Foundation Polish)
**Recently purged:** S3 submission, S3 template gallery / publishing, X.509 cryptographic signatures, certificate management, 1Password integration, SubmissionConfig metadata, DigitalSignature metadata. Surface area is now: load/edit templates, fill forms, save as .aprt/.aprf, validate, export, check for template updates via TemplateSourceUrl.

**Done:** Core models + serialization + validation (90%+ coverage), Desktop template/form editing with theme switching and collapsible sections, CLI (validate/info/new/fill/stats/diff/export), example templates including IRS-style government forms.

**In flight:** Windows 11 UI redesign, accessibility audit. The form input field with `expectedDataType: "signature"` (typed name or drawing) is intentionally retained — it's a UI primitive, not cryptography.

**Phase 1 remaining priorities:** Undo/redo follow-through, search & navigation, validation panel, progress tracking, GitHub Actions CI/CD, performance for 1000+ prompt forms, refactor MainWindowViewModel and FormFillingViewModel (still ~1000+ lines each — adopt CommunityToolkit.Mvvm and split god classes before Phase 2).

**Phase 2 (deferred):** calculation engine (NCalc-style), conditional visibility/validation logic, repeatable sections, PDF export.
**Phase 3:** mobile via .NET MAUI, optional cloud sync.
**Phase 4:** team workspaces, workflow/approval routing, compliance, enterprise integrations.

## Decision compass
When choosing between options, prefer: simplicity over features, semantic structure over visual fidelity, advisory hints over enforcement, local-first over cloud, accessibility over polish, open format over proprietary convenience. If a grandmother can't use it, it's wrong. Don't reintroduce S3/signature/certificate features without explicit user approval.
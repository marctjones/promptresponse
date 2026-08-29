# PromptResponse Roadmap

**Current version:** 1.0.0-beta.1
**Last updated:** 2026-08-29
**Status:** Public beta — stable core APR lifecycle with planned platform and CEL hardening

> This roadmap reflects the current post-MVP codebase. Older planning docs
> described v0.2.0 and v0.3.0 cut lines; those milestones have now shipped.

---

## 1. Mission

Replace rigid PDF/Word forms with a flexible, semantic, JSON-based format
(`.apr`) that separates content from presentation. Office workers create and
fill forms without fighting layout tools; downstream systems get clean
structured data without parsing PDFs.

See [VISION.md](VISION.md) for the full vision and non-negotiable principles
(string-only responses, accessibility as a CI gate, local-first, pure-data /
no code execution, open format, stable IDs).

---

## 2. Current state (v1.0.0-beta.1) — what actually works today

The core loop is **complete and tested end-to-end**:

- **Create** a template from scratch in the desktop editor (sections, nested
  sections, typed prompts, hints) with undo/redo and drag-to-reorder.
- **Fill** a form with type-appropriate widgets (date pickers, masked
  SSN/phone/currency/zip/EIN inputs) and advisory, never-blocking validation.
- **Save / open** `.aprt` / `.aprf` / `.apr` via a working file service with
  extension-based document-type inference.
- **Export** responses to CSV / JSON / TXT / HTML / PDF from the CLI.
- **Automate** via a real CLI (`validate`, `info`, `new`, `fill`, `stats`,
  `diff`, `export`, `import`, `keygen`, `sign`, `verify`) and a programmatic
  `FormFillingApi`.
- **Import** fillable PDFs into APRT templates with quality scoring and a
  documented importer-to-skill workflow for difficult forms.
- **Publish** self-contained release artifacts for Windows, Linux, and macOS.
- **Generate presentable records** with flat PDF, fillable AcroForm PDF, and
  PDF/A archival export.
- **Use advanced form logic** through a safe CEL-subset expression engine for
  computed fields, conditional visibility, read-only state, and validation.
- **Sign and verify** publisher templates and filled responses through CLI and
  desktop flows.

Platform / quality baseline:

- **.NET 10 + C# 14 + AvaloniaUI 12 + xunit.v3**, MIT/Apache-2.0-only stack.
- **Capability-profile rendering** (themes + accessibility modes compose via
  `CompositeProfile`; OS preferences auto-detected at startup).
- **Wizard mode** (section-at-a-time filling).
- **Accessibility is CI-gated**: WCAG 2.1 AA contrast on Light/Dark, AAA on
  HighContrast; keyboard-nav tests; Linux AT-SPI2 screen-reader support.
- **~1,400 tests** across Core / CLI / Desktop / PDF / Accessibility.
- **Multi-language read/write SDKs**: Rust and Java (real), a Python Flask
  demo server (`aprt-server.py`), and a C++ skeleton.

### Known gaps (the honest list)

- **No page-exact print preview** — the app has an in-app generated-content
  preview before PDF export, but not a page-break-accurate renderer surface.
- **Import cleanup still needs deeper editing tools** — the desktop now shows
  import-quality review flags, but section cleanup and radio-group conversion
  still need purpose-built editing shortcuts.
- **Non-.NET SDK conformance runners are not wired yet** — a shared corpus now
  exists and .NET gates it, but Rust/Java/Python/C++ runners still need CI.
- **No Word/Excel export** — useful for office interoperability.
- **No mobile apps**.
- **No notarized macOS app bundle** — release tarballs exist; signing and
  notarization remain packaging follow-ups.

---

## 3. Strategic wedge — DECIDED: Path A (local-first / sovereignty / accessibility)

> **Decision (2026-06-08): commit to Path A.** After shipping the MVP and a
> foundation for both wedges, we optimize for the **local-first / sovereignty /
> accessibility buyer** — public sector, compliance, and privacy-sensitive orgs.
> It plays to the project's existing strengths (offline, open format, CI-gated
> WCAG, no vendor lock-in) and competes with PDF/Word rather than entrenched SaaS.

The two paths that were on the table:

- **A. Local-first / sovereignty / accessibility buyer** *(chosen)* — public
  sector, compliance, privacy-sensitive orgs. Offline, open format, CI-gated
  WCAG, no vendor lock-in. Competes with PDF/Word.
- **B. Mass-market form builder** *(deferred)* — competes with Google/Microsoft
  Forms, Typeform, JotForm. Requires a web fill path + frictionless sharing.
  Its foundation (HTML renderer + self-contained fillable web form) is built and
  kept warm, but we are **not** investing in a hosted/shareable app now.

Path A means Milestone 2 leans into **richer print, a submission/signature
story, and deeper import** (see Milestone 2 below), not web fill + sharing.

---

## 4. Milestones

### Milestone 1 — Shippable MVP (shipped: v0.3.0)

**Goal:** someone who isn't the author can install it, create a form, fill it,
and produce a presentable artifact — without a dev toolchain.

The full cut-line with acceptance criteria lives in [docs/MVP.md](docs/MVP.md).
The v0.3.0 cut line shipped: PDF export, renderer seam, packaged distribution,
home screen/recent files, first-run authoring guidance, starter templates, and
documentation reconciliation.

### Milestone 2 — Competitive (shipped: v0.4.x-v0.6.0)

What makes APR competitive with advanced form systems, not just usable:

- **Calculation engine** — shipped: computed/read-only fields, safe expression
  eval (CEL-style per the v0.2 spec), **no code execution**, reactive in the UI.
- **Conditional logic** — shipped: show/hide, conditional-required, read-only,
  and cross-field validation. Advisory, never input-blocking.
- **Validation panel** — shipped: advisories with click-to-field.
- **Wedge — Path A (chosen, §3):** the local-first / accessibility direction.
  Sequenced by value × low risk:
  1. **Richer print / PDF templates** *(shipped foundation)* — running footer,
     "Page X of Y", a document title block + generated date, A4/Letter page
     size, Legal page size, handling banners, and PDF/A archival output. Done
     in the PDF renderer (no format/layout added to `.apr`).
  2. **Deeper import** — done: `apr import` (AcroForm → APR) with self-scoring
     quality, the portable `document-to-apr` skill, and the importer→skill hybrid.
     Follow-ups: radio-groups → choice, richer sectioning (see #64).
  3. **Submission / signature story** — shipped foundation: CMS/PKCS#7
     certificate-backed signatures for publishers and fillers, CLI
     sign/verify/keygen, desktop signing actions, and signature status in
     exports. Follow-ups: timestamp/LTV, trust-store UX, and policy docs.
  4. **PDF/A archival output** — shipped: PDF/A-2b export path.

  *Path B (deferred):* the browser fill path foundation (HTML renderer +
  fillable web form) exists; a hosted/shareable app is not in the near-term plan.

### Milestone 3 — Public Beta Hardening (target: v0.6.x)

- **Doc and version reconciliation** — README / roadmap / feature tracker /
  project versions must describe the shipped app, not old cut lines.
- **Release smoke validation** — install and run release artifacts on Windows,
  Linux, and macOS; verify open/fill/save/export/import/sign flows.
- **Warning cleanup** — keep the solution build quiet enough that new warnings
  matter.
- **Import review UX** — shipped foundation: desktop review dialog for
  low-quality imports with score, recommendation, flag summary, and sample
  fields. Follow-ups: one-click fixes for section cleanup and radio groups.
- **Print preview** — shipped foundation: preview generated print/PDF content
  before export. Follow-up: page-break-accurate pagination preview.

### Milestone 4 — Reach (target: v0.7.x+)

- **Mobile** (.NET MAUI, shared Core) — touch-optimized fill.
- **SDK conformance suite** — shipped foundation: shared v1 corpus plus .NET
  gate. Follow-up: run the same corpus across Rust / Java / Python / C++, or
  explicitly keep individual SDKs experimental.
- **Office export** — `.docx` / `.xlsx`.
- **Optional sync / collaboration** — only if the wedge demands it; must not
  compromise local-first defaults.

> Enterprise features (RBAC, workflow/approval chains, analytics, SSO,
> compliance certifications) are explicitly **deferred** until there is a user
> base and a chosen wedge. They are not part of the near-term plan.

---

## 5. Technical debt & refactoring (targeted)

- **Consolidate the Profiles module** — ~30 profile classes back ~5 visible
  modes; merge per-type input masks into one `InputMaskProfile` strategy
  table. ~1 day, no behavior change. *Do before adding more profiles.*
- **Keep the shared renderer seam tight** so PDF/HTML/print do not drift.
- **Reconcile stale docs** with the v0.6.0 reality (ongoing).
- **Declare the SDK commitment** — a shared corpus exists; keep Rust/Java in
  lockstep by adding runners, or formally mark some SDKs experimental.

---

## 6. Out of scope (by design)

These are deliberate non-goals; revisit only with explicit approval:

- Pixel-perfect layout / branding in the data format (separation of content
  and presentation is foundational).
- Code execution / scripting in `.apr` files (safe-to-open guarantee).
- Rich-text or embedded-media responses (string-only responses).
- Cloud-by-default anything (local-first is non-negotiable).

---

## 7. Comparison snapshot

See [docs/COMPARISON_TO_TRADITIONAL_FORMS.md](docs/COMPARISON_TO_TRADITIONAL_FORMS.md)
for the full analysis. In one line: PromptResponse trades pixel-perfect layout
and a few advanced features for **simplicity, portability, data liberation,
real accessibility, and zero lock-in** — strongest against Word/Excel/PDF
forms and bespoke CRUD apps, and differentiated from cloud form builders by
being local-first and open.

---

*This roadmap is milestone-driven, not date-driven, and should be revised when
the public-beta hardening work is complete.*

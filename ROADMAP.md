# PromptResponse Roadmap

**Current version:** 0.2.0 (released 2026-05-03)
**Last updated:** 2026-06-05
**Status:** Active development — approaching a shippable MVP

> This roadmap was rewritten on 2026-06-05 to reflect the real v0.2.0 codebase.
> The previous version (dated 2025-01-13) described aspirational quarterly
> phases that have since lapsed; its targets and metrics were never met on
> that timeline and have been replaced with milestone-based planning that
> doesn't pretend to know calendar dates.

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

## 2. Current state (v0.2.0) — what actually works today

The core loop is **complete and tested end-to-end**:

- **Create** a template from scratch in the desktop editor (sections, nested
  sections, typed prompts, hints) with undo/redo and drag-to-reorder.
- **Fill** a form with type-appropriate widgets (date pickers, masked
  SSN/phone/currency/zip/EIN inputs) and advisory, never-blocking validation.
- **Save / open** `.aprt` / `.aprf` / `.apr` via a working file service with
  extension-based document-type inference.
- **Export** responses to CSV / JSON / TXT from the CLI.
- **Automate** via a real CLI (`validate`, `info`, `new`, `fill`, `stats`,
  `diff`, `export`) and a programmatic `FormFillingApi`.

Platform / quality baseline:

- **.NET 10 + C# 14 + AvaloniaUI 12 + xunit.v3**, MIT/Apache-2.0-only stack.
- **Capability-profile rendering** (themes + accessibility modes compose via
  `CompositeProfile`; OS preferences auto-detected at startup).
- **Wizard mode** (section-at-a-time filling).
- **Accessibility is CI-gated**: WCAG 2.1 AA contrast on Light/Dark, AAA on
  HighContrast; keyboard-nav tests; Linux AT-SPI2 screen-reader support.
- **~700 tests** across Core / CLI / Desktop / Accessibility, ~2:1 test:src.
- **Multi-language read/write SDKs**: Rust and Java (real), a Python Flask
  demo server (`aprt-server.py`), and a C++ skeleton.

### Known gaps (the honest list)

- **No PDF / print path** — the single biggest functional hole.
- **No packaged installers** — "install the .NET SDK and `dotnet run`" is not
  a user-facing install; `.apr` files aren't OS-associated.
- **No home screen / recent-files** — the app opens into an empty editor.
- **No calculations or conditional logic** — needed for real tax/eligibility
  forms; design sketched in `docs/APR_SPECIFICATION_v0.2.md`, unimplemented.
- **No web/browser fill path** and **no mobile apps**.
- **No import** — every form is authored from scratch (no Word/PDF → APR).

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

### Milestone 1 — Shippable MVP (target: v0.3.0)

**Goal:** someone who isn't the author can install it, create a form, fill it,
and produce a presentable artifact — without a dev toolchain.

The full cut-line with acceptance criteria lives in [docs/MVP.md](docs/MVP.md).
Headline items:

- **PDF / print export** (P0) — generate a presentable PDF/printout from a
  filled form. Resolves the central "no layout, but users still need paper"
  tension. Introduce an `IDocumentRenderer` seam so PDF/HTML/print share one
  document-tree traversal.
- **Packaged distribution** (P0) — self-contained builds for Windows + Linux,
  `.apr`/`.aprt`/`.aprf` file association, no SDK prerequisite.
- **Home screen / recent files** (P1) — a real entry point with "new from
  template", recent documents, and quick actions.
- **First-run onboarding for authoring** (P1) — guided template creation; the
  *create* side is the unproven half and needs hand-holding.
- **Starter template library** (P1) — ship more than 3 examples; this is what
  makes "create a form" feel like "pick and tweak."
- **Documentation reconciliation** (P1) — align VISION / FEATURES / docs with
  the real v0.2.0 baseline (this roadmap is step one).

### Milestone 2 — Competitive (target: v0.4.x)

What makes APR competitive with advanced form systems, not just usable:

- **Calculation engine** — computed/read-only fields, safe expression eval
  (CEL-style per the v0.2 spec), **no code execution**. Reactive in the UI.
- **Conditional logic** — show/hide and conditional-required rules; repeatable
  sections (e.g. multiple dependents). Advisory, never input-blocking.
- **Validation panel** — dedicated warnings panel, click-to-field.
- **Wedge — Path A (chosen, §3):** the local-first / accessibility direction.
  Sequenced by value × low risk:
  1. **Richer print / PDF templates** *(in progress)* — running header/footer,
     "Page X of Y", a document title block + generated date, A4/Letter page
     size. The presentable, archival artifact gov/compliance actually submit and
     file. Done in the PDF renderer (no format/layout added to `.apr`).
  2. **Deeper import** — done: `apr import` (AcroForm → APR) with self-scoring
     quality, the portable `document-to-apr` skill, and the importer→skill hybrid.
     Follow-ups: radio-groups → choice, richer sectioning (see #64).
  3. **Submission / signature story** — reintroduce a signature/attestation path
     for submitted forms (was purged in #12 for the clean Phase-1 base). Decide
     in-format vs out-of-format scope; pdfe can author AcroForm signature fields.
  4. **PDF/A archival output** — long-term-preservation profile for records.

  *Path B (deferred):* the browser fill path foundation (HTML renderer +
  fillable web form) exists; a hosted/shareable app is not in the near-term plan.

### Milestone 3 — Reach (target: v0.5.x+)

- **Mobile** (.NET MAUI, shared Core) — touch-optimized fill.
- **SDK conformance suite** — one shared format-conformance test corpus run
  across .NET / Rust / Java (and gate C++/Python as reference/experimental).
  Turns the multi-language SDKs from a maintenance liability into a real,
  trustworthy ecosystem play.
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
- **Add the `IDocumentRenderer` seam** before PDF lands, so PDF/HTML/print
  don't each grow their own document-tree walker.
- **Reconcile stale docs** with the v0.2.0 reality (ongoing).
- **Declare the SDK commitment** — keep Rust/Java in lockstep via the
  conformance suite, or formally mark some SDKs experimental.

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

*This roadmap is milestone-driven, not date-driven, and will be revised as the
strategic wedge (§3) is decided and the MVP ships.*

# MVP Cut-Line — v0.3.0 "Shippable"

**Status:** shipped in v0.3.0 · **Created:** 2026-06-05 · **Updated:** 2026-06-10 · **Baseline:** v0.2.0

> **Progress (2026-06-06):** all blockers and core P1s shipped on `main` —
> #1 PDF export (✅, flat **+** fillable AcroForm), #2 renderer seam (✅),
> #3 packaging/installers (✅), #4 home screen + recent files (✅),
> #5 onboarding (✅), #6 starter templates (✅), #7 doc reconciliation (✅).
> Bonus: GUI File → Export menu, document metadata in PDFs, pdfe upgraded to
> v2.5.0 (fillable fields are screen-reader-named). Remaining P2s (#8 progress
> UI, #9 validation panel, #10 profiles refactor, #11 macOS build) are optional.

This is the concrete issue list for the first release a non-author can install,
use, and get a presentable artifact out of — without a dev toolchain. See
[ROADMAP.md](../ROADMAP.md) §4 Milestone 1 for context.

**MVP thesis:** the core loop (create → fill → save/export) worked in v0.2.0.
The gap to *usable* was small and concrete: **a presentable output
(PDF/print), a real install, and an entry point.** That gap is now closed.

**Definition of done for v0.3.0:** A user with no .NET SDK downloads an
installer, launches to a home screen, creates a form from a starter template,
fills it, and prints/exports a clean PDF — all keyboard- and screen-reader-
accessible, all verified by tests. This definition was met.

Priority: **P0** = blocks MVP · **P1** = needed for a credible MVP · **P2** =
nice-to-have, can slip to 0.3.x.

---

## P0 — MVP blockers

### 1. PDF / print export from a filled form
**Priority:** P0 · **Effort:** M (3–5 d) · **Depends on:** #2 (renderer seam)

The central unresolved tension: the format intentionally carries no layout, but
users still need paper/PDF for submission and archival. Today there is no print
path at all — the biggest functional hole.

- Use **QuestPDF** (MIT) — already the documented recommendation in FEATURES.md.
- Render a filled `.aprf` to a clean, accessible PDF: section headings, prompt
  label + response, nested-section indentation, page numbers, header/footer.
- Support A4 + Letter; option to include/exclude empty fields.
- Expose from **both** the desktop app (File → Export PDF / Print) and the CLI
  (`export --format pdf`).
- Layout is generated from semantic structure, not stored in the format
  (honor "no layout info in `.apr`").

**Acceptance criteria**
- [x] `dotnet run --project src/PromptResponse.Cli -- export form.aprf --format pdf` produces a valid PDF.
- [x] Desktop File menu exports the current document to PDF.
- [x] Empty-field include/exclude toggle works.
- [x] A structural test asserts section/prompt/response appear in order.
- [x] No layout fields added to the APR schema.

---

### 2. `IDocumentRenderer` seam (refactor before PDF)
**Priority:** P0 · **Effort:** S (1–2 d) · **Blocks:** #1

Introduce one document-tree traversal abstraction so PDF, HTML (the Python
server already proves the shape), and any future print path don't each grow
their own walker.

- Define `IDocumentRenderer` (or a visitor over `AprDocument → Sections →
  Prompts`) in Core or a new `PromptResponse.Rendering` project.
- PDF export (#1) is the first consumer.

**Acceptance criteria**
- [x] A single traversal API exists with unit tests over a nested-section doc.
- [x] PDF export consumes it rather than re-walking the tree.

---

### 3. Packaged, self-contained distribution
**Priority:** P0 · **Effort:** M (3–5 d)

"Install the .NET 10 SDK and `dotnet run`" is not an install. Ship runnable
binaries with no prerequisites.

- Self-contained, single-file publish for **Windows (x64)** and **Linux (x64)**
  (`dotnet publish -p:PublishSingleFile=true --self-contained`).
- OS file association for `.apr` / `.aprt` / `.aprf` so double-click opens the app.
- Launch-by-file already partially exists (the `--open <file>` path) — wire it
  to the association.
- Wire into CI (GitHub Actions) to attach artifacts to a `gh release`.
- (macOS can follow in 0.3.x — see #11.)

**Acceptance criteria**
- [x] Downloadable Windows + Linux artifacts that run on a machine with no .NET installed.
- [x] Double-clicking a `.aprt`/`.aprf` opens it in the app (where the OS supports association).
- [x] CI produces the artifacts on tag.

---

## P1 — needed for a credible MVP

### 4. Home screen / recent files entry point
**Priority:** P1 · **Effort:** M (2–4 d)

The app currently opens into an empty editor. Give it a real front door.

- Recent documents (persisted via the existing settings service).
- "New from starter template" (see #6), "New blank template", "Open…".
- Fully keyboard- and screen-reader-accessible (AutomationProperties, focus order).

**Acceptance criteria**
- [x] First launch shows a home view, not an empty editor.
- [x] Recent files persist across runs and reopen correctly.
- [x] GUI headless test covers the home → open → editor flow.
- [x] Accessibility test: every actionable element has a Name; tab order verified.

---

### 5. First-run onboarding for authoring
**Priority:** P1 · **Effort:** S–M (2–3 d)

Filling a form is easy; *authoring* one well is the unproven half. Guide it.

- A short first-run flow or inline guidance for building a first template
  (add section → add prompt → set type/hint → save as `.aprt`).
- Dismissible; never blocks the keyboard/screen-reader path.

**Acceptance criteria**
- [x] A new user can reach a saved `.aprt` without reading external docs.
- [x] Onboarding is dismissible and remembered.
- [x] No accessibility regressions (contrast + keyboard-nav tests pass).

---

### 6. Starter template library
**Priority:** P1 · **Effort:** S (1–2 d)

Three examples (showcase, IRS-990, SF-86) aren't enough to make "create a form"
feel like "pick and tweak."

- Add ~6–10 common office forms (time-off request, expense report, IT/access
  request, contact/intake, event registration, incident report, etc.).
- Surface them in the "New from template" path (#4).
- Each must pass `AprAccessibilityValidationTests` (unique labels, section
  titles, help text) — accessibility is a CI gate.

**Acceptance criteria**
- [x] ≥6 new starter templates under `examples/`.
- [x] All pass accessibility validation in CI.
- [x] Selectable from the home screen.

---

### 7. Documentation reconciliation
**Priority:** P1 · **Effort:** S (1 d)

VISION/FEATURES/docs claim different baselines (0.1 vs 0.2, 2024 dates, lapsed
roadmap). Align them to the real v0.2.0 state.

- VISION.md "Current state (0.1 baseline)" → v0.2.0.
- FEATURES.md footer date + sprint section → current; mark shipped items ✅.
- Verify cross-links after this MVP doc + the rewritten ROADMAP land.

**Acceptance criteria**
- [x] No doc claims a pre-0.2.0 baseline as current.
- [x] FEATURES.md status column matches what actually ships.

---

## P2 — nice-to-have for 0.3.x (can slip)

### 8. Progress tracking in the fill view
**Priority:** P2 · **Effort:** S

Shipped: `FormProgressViewModel` is surfaced in the right rail with total and
per-section completion.

### 9. Validation / advisory panel
**Priority:** P2 · **Effort:** S–M

Shipped: the right rail lists advisories with click-to-field behavior.

### 10. Profiles module consolidation (tech debt)
**Priority:** P2 · **Effort:** S (1 d)

Shipped: profile and input-mask behavior has been consolidated behind focused
profile services and covered by tests.

### 11. macOS self-contained build
**Priority:** P2 · **Effort:** S

Shipped: release workflow produces macOS tarballs for Apple Silicon and Intel.

---

## Explicitly NOT in MVP

Deferred to Milestone 2+ (see ROADMAP.md): calculation engine, conditional
logic / show-hide, web/browser fill path, mobile apps, Word/PDF import, digital
signatures, sync/collaboration, and all enterprise features. The strategic
wedge decision (ROADMAP §3) gates which of these comes first.

---

## Suggested sequence

1. **#2 renderer seam** → **#1 PDF export** (unblocks the headline artifact).
2. **#3 distribution** in parallel (independent track).
3. **#4 home screen** → **#6 starter templates** → **#5 onboarding** (the
   first-run experience, in that dependency order).
4. **#7 doc reconciliation** last (reflects everything above).
5. P2 items as capacity allows; **#10** is a good warm-up refactor before #1.

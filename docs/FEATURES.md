# PromptResponse Feature Tracker

<!-- AI-ASSISTANT-README -->
This document tracks all features with implementation status and priority.
Update this document when implementing features or planning work.
Cross-reference with ROADMAP.md for detailed descriptions.
<!-- END-AI-ASSISTANT-README -->

## Quick Reference

**Priority Levels:**
- **P0** - Critical / Must Have (blocks release)
- **P1** - High / Should Have (core functionality)
- **P2** - Medium / Nice to Have (enhances experience)
- **P3** - Low / Future (defer until later)

**Status:**
- ✅ Complete
- 🔄 In Progress
- ⏳ Planned
- ❌ Not Started

---

## Feature Summary

| Category | Complete | In Progress | Planned | Total |
|----------|----------|-------------|---------|-------|
| Core Library | 8 | 0 | 0 | 8 |
| Desktop UI | 16 | 1 | 1 | 18 |
| Export & Print | 5 | 0 | 2 | 7 |
| Import | 2 | 0 | 1 | 3 |
| CLI Tool | 10 | 0 | 0 | 10 |
| Mobile | 0 | 0 | 4 | 4 |
| Enterprise | 0 | 0 | 6 | 6 |

---

## Core Library Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| Roles (§4.10) | P1 | ✅ | Who each part is for; marked in the reader, never enforced |
| Bounds hints (min/max/step) | P2 | ✅ | Advisory; a response outside them stays valid |
| apr-sig-v3 | P0 | ✅ | A filler signs the question they saw, not only their answer |
| Per-field signature coverage | P1 | ✅ | Signed / broken / unsigned, live and profile-aware |
| Create a signing key | P2 | ✅ | Self-signed, creation only; management stays the platform's |
| `review` command | P1 | ✅ | Processability triage for whoever receives a submission |
| Python SDK (core profile) | P1 | ✅ | Corpus-gated; gates the core-only rules .NET cannot |
| Document model (Section/Subsection/Prompt) | P0 | ✅ | Complete with tests |
| JSON serialization | P0 | ✅ | camelCase, ISO 8601 dates |
| Advisory validation | P0 | ✅ | Structural + data type |
| Data type hints | P1 | ✅ | text, email, date, number, etc. |
| Expression parser | P1 | ✅ | Safe CEL-subset evaluator in `PromptResponse.Core.Expressions` (no code execution) |
| Undo/Redo system | P1 | ✅ | Command pattern implemented |
| Calculation engine | P1 | ✅ | Computed fields (`exprValue`) with fixpoint recompute; live read-only auto-update in the fill view |
| Conditional logic | P2 | ✅ | `exprHidden` (live show/hide), `exprExpected`, `exprReadOnly`, `exprValidation` (advisory) — end-to-end |

---

## Desktop UI Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| Template editor | P0 | ✅ | Create and edit templates |
| Form filling view | P0 | ✅ | Fill forms with responses |
| File type detection | P0 | ✅ | .aprt, .aprf, .apr support |
| Theme switching | P1 | ✅ | Light, Dark, System, Custom |
| Unsaved changes tracking | P1 | ✅ | Prompt before losing work |
| Collapsible sections | P1 | ✅ | Expand/collapse in editor |
| **Windows 11 UI redesign** | P0 | 🔄 | Modern Fluent design system |
| Dashboard/home screen | P1 | ✅ | Home screen with recent files + starter-template gallery (#34, #36) |
| Progress tracking | P1 | ✅ | Right-rail total and per-section completion |
| Search & navigation | P1 | ✅ | Find prompts and matches across nested sections |
| Validation panel | P2 | ✅ | Advisory list with click-to-field |
| Print preview | P2 | ✅ | In-app generated-content preview before PDF export |
| Recent files list | P2 | ✅ | Recent files on the home screen, persisted (#34) |
| PDF / fillable export (GUI) | P1 | ✅ | File → Export: flat PDF + fillable AcroForm (#31) |
| HTML / web-form export (GUI) | P1 | ✅ | File → Export: read-only HTML page + self-contained fillable web form |
| Starter templates | P1 | ✅ | 6 bundled accessible templates, selectable on home (#36) |
| Signatures panel/actions | P1 | ✅ | Verify signatures, re-verify, sign as publisher, sign responses |

---

## Export & Print Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| CSV/JSON/TXT export | P1 | ✅ | Via CLI export command |
| HTML export | P2 | ✅ | Accessible HTML page (`export --format=html`); foundation for a browser fill path |
| Fillable HTML (browser fill) | P1 | ✅ | `export --format=html --fillable` → self-contained interactive web form that downloads `.aprf`; no server. CLI + desktop File → Export |
| **PDF export** | **P1** | **✅** | Flat **and** fillable-AcroForm PDF via CLI (`export --format=pdf [--fillable] [--page-size=a4] [--pdfa] [--banner=…]`) and the desktop File → Export menu (pdfe engine); carries document metadata, a running footer (title · generated date · "Page X of Y"), page size (Letter/A4/Legal), classification banners, and a **PDF/A-2b archival** mode (`--pdfa`, embedded font, veraPDF-validated) (#31, #80) |
| Print preview | P2 | ✅ | Preview generated print/PDF content before writing a PDF |
| Word export (.docx) | P2 | ⏳ | Export to Word format |
| Excel export (.xlsx) | P2 | ⏳ | Export to spreadsheet |

### PDF Export Details

**Priority**: P1 (High - requested by users)

**Requirements**:
- Generate professional PDF from filled form
- Include headers, footers, page numbers
- Custom PDF templates per form type
- Preserve form structure visually
- Option to include/exclude empty fields

**Technical Approach**:
- Library options: QuestPDF (recommended), iText, PdfSharp
- Keep layout logic separate from data
- Support A4 and Letter page sizes
- Embed fonts for consistency

**Use Cases**:
- Resident prints filled form for records
- Town hall archives physical copies
- Submit via mail when electronic not accepted
- Legal/compliance requirements

---

## Import Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| Document → APR (AI skill) | P1 | ✅ | Portable skill (`.claude/skills/document-to-apr/`): an agent turns a PDF / Word / OpenDocument / **image** of a form into a valid `.aprt`. Works in Claude Code, Gemini CLI, Codex, etc. |
| AcroForm PDF importer (code) | P2 | ✅ | `apr import <file.pdf>` — deterministic extraction of fillable-PDF form fields → `.aprt` (`PdfFormImporter`, pdfe). Field quality depends on PDF tooltips (`/TU`): great when present (e.g. SF-86), cryptic when absent (e.g. IRS 990 → use the skill). Flat/scanned PDFs have no fields, so the skill is the path there. |
| Import review UX | P1 | ✅ | Desktop review dialog shows score, recommendation, flag summary, and sample cryptic/duplicate/ambiguous fields before opening a weak import |

---

## CLI Tool Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| validate | P0 | ✅ | Structural and data type validation |
| import | P2 | ✅ | Fillable PDF (AcroForm) → `.aprt` template |
| info | P1 | ✅ | Display document information |
| new | P1 | ✅ | Interactive template creation |
| fill | P1 | ✅ | Programmatic response filling |
| stats | P1 | ✅ | Detailed statistics |
| diff | P1 | ✅ | Compare two APR files |
| export | P1 | ✅ | Export to CSV/JSON/TXT/HTML/PDF |
| keygen | P1 | ✅ | Generate signing certificate material |
| sign | P1 | ✅ | Sign publisher templates or filled responses |
| verify | P1 | ✅ | Verify content signatures and trust status |

---

## Mobile Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| iOS app | P2 | ❌ | .NET MAUI or native |
| Android app | P2 | ❌ | .NET MAUI or native |
| Touch-optimized UI | P2 | ❌ | Mobile-specific layouts |
| Offline support | P2 | ❌ | Work without connection |

---

## Enterprise Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| Team workspaces | P3 | ❌ | Organization accounts |
| Role-based access | P3 | ❌ | RBAC permissions |
| Workflow management | P3 | ❌ | Approval chains |
| Analytics dashboard | P3 | ❌ | Usage metrics |
| Audit logs | P3 | ❌ | Compliance tracking |
| SSO integration | P3 | ❌ | SAML, LDAP |

---

## Current Sprint

**Focus**: Public-beta hardening — keep the shipped v0.6.0 feature set reliable,
documented, and releasable before adding larger features.

### High Priority (P0-P1)

| Feature | Status | Owner | Target |
|---------|--------|-------|--------|
| Documentation/version reconciliation | 🔄 | - | v0.6.x |
| Release smoke validation | ⏳ | - | v0.6.x |
| Import review UX | ✅ | - | v0.6.x |
| macOS signing/notarization plan | ⏳ | - | v0.6.x |

### Nice to Have (P2)

| Feature | Status | Notes |
|---------|--------|-------|
| Page-exact print preview | ⏳ | Follow-up over the existing PDF renderer |
| Word/Excel export | ⏳ | Office interoperability |
| SDK conformance corpus | ✅ | Shared v1 APR fixtures with .NET validation/round-trip gate |
| Non-.NET SDK conformance runners | ⏳ | Run the shared corpus across Rust / Java / Python / C++ |

---

## Feature Requests

Track new feature requests here before adding to main list:

| Request | Requester | Date | Priority | Notes |
|---------|-----------|------|----------|-------|
| PDF export | User | 2024-11-20 | P1 | Shipped; kept for historical context |

---

## Completed Features Log

| Feature | Completed | Notes |
|---------|-----------|-------|
| Core document model | 2024-Q4 | Full test coverage |
| JSON serialization | 2024-Q4 | camelCase, ISO dates |
| CLI tools | 2024-Q4 | 6 commands |
| Template editor | 2024-Q4 | Basic functionality |
| Form filling | 2024-Q4 | With response tracking |
| Shippable MVP | 2026-06 | PDF export, packaging, home screen, starter templates |
| Competitive form logic | 2026-06 | Expressions, calculations, conditional logic, advisory panel |
| Import/export expansion | 2026-06 | PDF import, HTML/fillable web export, PDF/A |
| Signatures | 2026-06 | Publisher and filler signing, verification, desktop status/actions |

---

## How to Update This Document

### Adding a New Feature

1. Add to appropriate category table
2. Set initial priority (P0-P3)
3. Set status to ❌ Not Started
4. Add to Current Sprint if high priority
5. Commit with message: `docs: add [feature] to feature tracker`

### Updating Feature Status

1. Change status emoji (❌ → ⏳ → 🔄 → ✅)
2. Update notes with relevant details
3. Move to Completed Features Log when done
4. Commit with message: `docs: update [feature] status to [status]`

### Changing Priority

1. Update priority in table
2. Move between sprint sections if needed
3. Document reason in commit message

---

## Related Documents

- **Detailed roadmap**: [ROADMAP.md](../ROADMAP.md)
- **Implementation plan**: [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)
- **Vision**: [VISION.md](VISION.md)
- **Design system**: [../.claude/DESIGN_SYSTEM.md](../.claude/DESIGN_SYSTEM.md)

---

*Feature tracker updated 2026-06-10 — reflects the v0.6.0 post-MVP hardening baseline. See `ROADMAP.md`, `docs/MVP.md`, and `CHANGELOG.md`.*

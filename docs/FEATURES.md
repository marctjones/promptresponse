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
| Core Library | 6 | 0 | 2 | 8 |
| Desktop UI | 6 | 1 | 6 | 13 |
| Export & Print | 1 | 0 | 3 | 4 |
| CLI Tool | 6 | 0 | 0 | 6 |
| Mobile | 0 | 0 | 4 | 4 |
| Enterprise | 0 | 0 | 6 | 6 |

---

## Core Library Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| Document model (Section/Subsection/Prompt) | P0 | ✅ | Complete with tests |
| JSON serialization | P0 | ✅ | camelCase, ISO 8601 dates |
| Advisory validation | P0 | ✅ | Structural + data type |
| Data type hints | P1 | ✅ | text, email, date, number, etc. |
| Expression parser | P1 | ⏳ | Planned — not implemented; see `docs/APR_SPECIFICATION_v0.2.md` for forward-looking design |
| Undo/Redo system | P1 | ✅ | Command pattern implemented |
| Calculation engine | P1 | ⏳ | Computed fields with formulas |
| Conditional logic | P2 | ⏳ | Show/hide fields based on values |

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
| Dashboard/home screen | P1 | ⏳ | Recent docs, quick actions |
| Progress tracking | P1 | ⏳ | Visual % complete indicator |
| Search & navigation | P1 | ⏳ | Find prompts, jump to section |
| Validation panel | P2 | ⏳ | Dedicated error/warning panel |
| Print preview | P2 | ⏳ | WYSIWYG print layout |
| Recent files list | P2 | ⏳ | Quick access to recent docs |

---

## Export & Print Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| CSV/JSON/TXT export | P1 | ✅ | Via CLI export command |
| **PDF export** | **P1** | **🔄** | Flattened PDF via `export --format=pdf` (pdfe engine). MVP: Latin-text/base-14; Unicode + tagged/accessible + fillable-AcroForm deferred (see CHANGELOG #31) |
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

## CLI Tool Features

| Feature | Priority | Status | Notes |
|---------|----------|--------|-------|
| validate | P0 | ✅ | Structural and data type validation |
| info | P1 | ✅ | Display document information |
| new | P1 | ✅ | Interactive template creation |
| stats | P1 | ✅ | Detailed statistics |
| diff | P1 | ✅ | Compare two APR files |
| export | P1 | ✅ | Export to CSV/JSON/TXT |

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

## Current Sprint (Phase 1)

**Focus**: Foundation polish — clean up the basic core implementation before any Phase 2 work.

### High Priority (P0-P1)

| Feature | Status | Owner | Target |
|---------|--------|-------|--------|
| Windows 11 UI redesign | 🔄 | - | Month 1 |
| Dashboard/home screen | ⏳ | - | Month 1 |
| Form filling experience | ⏳ | - | Month 2 |
| **PDF export** | ⏳ | - | Month 2 |

### Nice to Have (P2)

| Feature | Status | Notes |
|---------|--------|-------|
| Progress tracking | ⏳ | Visual indicator |
| Print preview | ⏳ | After PDF export |
| Word/Excel export | ⏳ | After PDF export |

---

## Feature Requests

Track new feature requests here before adding to main list:

| Request | Requester | Date | Priority | Notes |
|---------|-----------|------|----------|-------|
| PDF export | User | 2024-11-20 | P1 | Added to Export features |

---

## Completed Features Log

| Feature | Completed | Notes |
|---------|-----------|-------|
| Core document model | 2024-Q4 | Full test coverage |
| JSON serialization | 2024-Q4 | camelCase, ISO dates |
| CLI tools | 2024-Q4 | 6 commands |
| Template editor | 2024-Q4 | Basic functionality |
| Form filling | 2024-Q4 | With response tracking |

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

*Feature tracker version 1.0 - Updated 2024-11-20*

# PromptResponse Documentation Index

<!-- AI-ASSISTANT-README -->
This is the master documentation index for PromptResponse.
AI assistants should start here to understand the documentation structure.
Each document includes its purpose and when to reference it.

PRIORITY READING ORDER FOR AI ASSISTANTS:
1. /CLAUDE.md - Core instructions and constraints
2. /.claude/DESIGN_SYSTEM.md - UI specifications (for UI work)
3. /docs/VISION.md - Project goals and values
4. /docs/IMPLEMENTATION_PLAN.md - Current phase and priorities
5. This index for finding specific documentation
<!-- END-AI-ASSISTANT-README -->

---

## Quick Reference

| Need to... | Read this |
|------------|-----------|
| Understand the project | [VISION.md](VISION.md) |
| Know current priorities | [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) |
| Create UI components | [/.claude/DESIGN_SYSTEM.md](../.claude/DESIGN_SYSTEM.md) |
| Understand APR format | [specifications/FILE_FORMAT.md](specifications/FILE_FORMAT.md) |
| Import an existing form (PDF/Word/image) | [IMPORT.md](IMPORT.md) |
| Write tests | [guides/DEVELOPMENT.md](guides/DEVELOPMENT.md) |

---

## Documentation Map

### Root Level

| File | Purpose | Audience |
|------|---------|----------|
| `/CLAUDE.md` | Primary AI assistant instructions | AI Assistants |
| `/README.md` | Project overview and quick start | All |
| `/ACCESSIBILITY.md` | Accessibility requirements and testing | Developers |
| `/DEBUGGING.md` | Debug logging configuration | Developers |
| `/LAUNCHER.md` | Running with launcher scripts | Users/Devs |
| `/ROADMAP.md` | High-level project roadmap | All |
| `/docs/RELEASE_SMOKE_TEST.md` | Release artifact smoke-test checklist | Release validation |
| `/docs/SDK_CONFORMANCE.md` | Shared APR compatibility corpus rules | SDK and format work |
| `/LICENSE` | AGPL-3.0-or-later license | Legal |
| [`/THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md) | Permissive-only third-party policy and notices | Legal |

### /.claude/ Directory

AI-specific configuration and instructions.

| File | Purpose | When to Reference |
|------|---------|-------------------|
| `context.md` | Session context for Claude Code | Every session |
| `DESIGN_SYSTEM.md` | **UI/UX specifications** | Any UI work |
| `IMPLEMENTATION_NOTES.md` | Current status notes | Starting work |

### /docs/ Core Documents

| File | Purpose | When to Reference |
|------|---------|-------------------|
| `INDEX.md` | This documentation map | Finding docs |
| `VISION.md` | **Mission, values, roadmap** | Architecture decisions |
| `IMPLEMENTATION_PLAN.md` | **Phased implementation plan** | Planning work |
| `CAPABILITY_PROFILES.md` | **Feature-flag capability model + 5 named presets** | Adding/editing UX affordances |

### /docs/architecture/

System design and technical architecture.

| File | Purpose | When to Reference |
|------|---------|-------------------|
| `SYSTEM_ARCHITECTURE.md` | Three-layer architecture, patterns | New features |
| `DATA_MODEL.md` | APR format deep dive | Format work |

**Note**: Some architecture docs may need to be created or moved from existing locations.

### /docs/guides/

How-to guides for development and usage.

| File | Purpose | When to Reference |
|------|---------|-------------------|
| `DEVELOPMENT.md` | TDD workflow, coding standards | Writing code |
| `UI_GUIDELINES.md` | UI implementation patterns | Creating views |
| `WORKFLOW_GUIDE.md` | Town hall user workflows | UX decisions |

### /docs/specifications/

Technical specifications and references.

| File | Purpose | When to Reference |
|------|---------|-------------------|
| `FILE_FORMAT.md` | **Complete APR format spec** | Format questions |
| `CLI_REFERENCE.md` | All CLI commands documented | CLI work |
| `API_SPECIFICATION.md` | Core library API | Using Core |

---

## Document Status

### Complete and Current

- ✅ `/CLAUDE.md` - Comprehensive, well-maintained
- ✅ `/docs/FILE_FORMAT.md` - Complete specification
- ✅ `/docs/DEVELOPMENT.md` - TDD workflow documented
- ✅ `/.claude/context.md` - Session context ready
- ✅ `/docs/VISION.md` - **NEW** - Project vision
- ✅ `/docs/IMPLEMENTATION_PLAN.md` - **NEW** - Phased plan
- ✅ `/.claude/DESIGN_SYSTEM.md` - **NEW** - UI specifications

### Needs Creation

- ⏳ `/docs/architecture/DATA_MODEL.md`
- ⏳ `/docs/guides/UI_GUIDELINES.md`
- ⏳ `/docs/guides/WORKFLOW_GUIDE.md`
- ⏳ `/docs/specifications/CLI_REFERENCE.md`
- ⏳ `/docs/specifications/API_SPECIFICATION.md`

### Needs Reorganization

- 🔄 `ARCHITECTURE.md` → Move to `/docs/architecture/SYSTEM_ARCHITECTURE.md`
- 🔄 `ACCESSIBILITY.md` → Consider moving to `/docs/guides/`

---

## For AI Assistants

### Starting a New Task

1. **Read `/CLAUDE.md`** for project constraints
2. **Check `/docs/IMPLEMENTATION_PLAN.md`** for current phase
3. **Find relevant docs** in this index
4. **Follow `/.claude/DESIGN_SYSTEM.md`** for any UI work

### Making Architecture Decisions

Reference in this order:
1. `/docs/VISION.md` - Does it align with values?
2. `/docs/architecture/SYSTEM_ARCHITECTURE.md` - Does it fit patterns?
3. `/CLAUDE.md` - Does it violate constraints?

### Creating UI Components

1. **Must read**: `/.claude/DESIGN_SYSTEM.md`
2. **Use existing styles**: Check `/src/PromptResponse.Desktop/Styles/`
3. **Follow patterns**: See existing views for examples
4. **Note accessibility gaps**: Document for future fix

### Working with APR Format

1. **Specification**: `/docs/specifications/FILE_FORMAT.md`
2. **Examples**: `/examples/` directory
3. **Constraint**: All responses are strings, never typed values

### Writing Tests

1. **Follow TDD**: `/docs/guides/DEVELOPMENT.md`
2. **Run tests**: `dotnet test`
3. **Coverage target**: >80%
4. **Never commit broken tests**

---

## Documentation Conventions

### File Headers

All documentation should include AI markers:

```markdown
<!-- AI-ASSISTANT-README -->
Brief purpose of this document.
When AI assistants should reference it.
Key points to note.
<!-- END-AI-ASSISTANT-README -->
```

### Markdown Standards

- Use ATX headers (`#`, `##`, `###`)
- Code blocks with language specifiers
- Tables for structured data
- Task lists for actionable items

### Linking

- Use relative links within docs
- Link to specific sections when relevant
- Keep links up-to-date

---

## Contributing to Documentation

### Adding New Documents

1. Place in appropriate directory
2. Add AI-ASSISTANT-README header
3. Update this INDEX.md
4. Link from related documents

### Updating Existing Documents

1. Keep AI markers current
2. Update status in this index
3. Note changes in relevant plans

### Review Checklist

- [ ] AI markers present and accurate
- [ ] Added to INDEX.md
- [ ] Links work
- [ ] Follows markdown standards
- [ ] No sensitive information

---

## Glossary

| Term | Definition |
|------|------------|
| **APR** | PromptResponse file format (JSON-based) |
| **APRT** | APR Template - blank form |
| **APRF** | APR Filled Form - form with responses |
| **Prompt** | A single field/question in a form |
| **Section** | Top-level grouping of prompts |
| **Subsection** | Nested grouping within a section |
| **Core** | Platform-agnostic library |
| **Desktop** | Avalonia UI application |
| **CLI** | Command-line interface tool |

---

*Documentation index version 1.0 - Updated 2024-11-20*

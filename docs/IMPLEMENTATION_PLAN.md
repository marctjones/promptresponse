# PromptResponse Implementation Plan

<!-- AI-ASSISTANT-README -->
This document contains the phased implementation roadmap for PromptResponse.
AI assistants should reference this when planning work and estimating scope.
Tasks should align with the current phase priorities.
<!-- END-AI-ASSISTANT-README -->

## Current Status

**Phase**: Foundation (Year 1)
**Focus**: Capability profiles, accessibility, UI polish

### What's Built

- ✅ Core library (models, serialization, validation)
- ✅ Desktop application (basic template editing, form filling)
- ✅ CLI tool (validate, info, new, export, diff)
- ✅ Capability-profile architecture (universal core + composable per-feature flags)
- ✅ Modern Win11 / macOS-aligned shell with theme switching

### What's In Progress

- 🔄 Capability-profile flag split (one flag per affordance) + 5 named presets
- 🔄 Form submission workflow (optional webhook only)
- 🔄 Accessibility audit + screen-reader tuning

### What's Planned

- ⏳ Cognitive / motor capability flags (DyslexiaFont, SectionFocusMode, HitMarginPadding, …)
- ⏳ CLI coverage ratchet toward 95%
- ⏳ Performance optimization

> **Removed from scope:** S3 / cloud-storage submission, cryptographic template/form
> signing, certificate management, template-update service. PromptResponse stays
> local-first; submission is optional and limited to webhook endpoints owned by the
> template publisher.

---

## Immediate Actions (Week 1-2)

### Day 1-3: Codebase Cleanup

**Files to Archive** (move to `promptresponse-multilang` repository):

```
/cpp/           → Archive (C++ implementation)
/java/          → Archive (Java implementation)
/python/        → Archive (Python implementation)
/rust/          → Archive (Rust implementation)
```

**Files to Clean**:

- [ ] Move `Screenshot From 2025-11-12 23-09-30.png` to `docs/images/`
- [ ] Delete `tests/PromptResponse.AccessibilityTests/UnitTest1.cs`
- [ ] Review and clean example files with malformed names

**Update .gitignore**:

```gitignore
# Archived language implementations
/cpp/
/java/
/python/
/rust/
```

### Day 4-7: Documentation Reorganization

**Create New Structure**:

```
docs/
├── INDEX.md                    ← Documentation map
├── VISION.md                   ← Project vision
├── IMPLEMENTATION_PLAN.md      ← This document
├── CAPABILITY_PROFILES.md      ← Capability flags + 5 named presets
├── architecture/
│   ├── SYSTEM_ARCHITECTURE.md  ← Move from docs/ARCHITECTURE.md
│   └── DATA_MODEL.md           ← APR format deep dive
├── guides/
│   ├── DEVELOPMENT.md          ← Keep existing
│   ├── UI_GUIDELINES.md        ← Win11 / macOS native design guide
│   └── WORKFLOW_GUIDE.md       ← Town hall workflows
├── specifications/
│   ├── FILE_FORMAT.md          ← Keep existing
│   └── CLI_REFERENCE.md        ← Complete CLI commands
└── images/
    └── screenshots/            ← UI screenshots
```

**Create AI-Readable Headers**:

All documentation should include:

```markdown
<!-- AI-ASSISTANT-README -->
Brief description of document purpose.
When AI assistants should reference this.
<!-- END-AI-ASSISTANT-README -->
```

### Day 8-14: Foundation Testing

- [ ] Run full test suite: `dotnet test`
- [ ] Fix any broken tests
- [ ] Document known issues in `KNOWN_ISSUES.md`
- [ ] Create test coverage report
- [ ] Verify build on Windows, Linux, macOS

---

## Short-Term Goals (Month 1)

### Week 3: Main Window Redesign

**Design Specifications** (see `/.claude/DESIGN_SYSTEM.md`):

**Layout**:
- Dashboard-style home screen
- Recent documents with modification time
- Quick action buttons (large, touch-friendly)
- Active capability-profile indicator
- Notification badge for new submissions

**Components**:
- Hero section with gradient background
- Card-based layout (12px radius, elevation shadow)
- Status indicators using semantic colors
- 44px minimum button height

**Implementation Tasks**:

- [ ] Create `DashboardView.axaml` and ViewModel
- [ ] Implement recent documents service
- [ ] Surface active capability-profile in shell chrome
- [ ] Create notification badge component
- [ ] Implement quick action navigation

### Week 4: Template Editor Polish

**Design Specifications**:

**Three-Panel Layout**:
- Left: Section tree navigator (collapsible)
- Center: Live form preview (scrollable)
- Right: Property panel (contextual)

**Toolbar**:
- Grouped actions: File, Edit, Insert, View
- Icon + text for primary actions
- Keyboard shortcuts shown

**Implementation Tasks**:

- [ ] Refactor `TemplateEditorView.axaml` to three-panel
- [ ] Create `PropertyPanel` component
- [ ] Implement drag-drop section reordering
- [ ] Add inline editing in preview
- [ ] Create modern toolbar/command bar

---

## Medium-Term Goals (Month 2-3)

### Month 2: Form Filling Experience

**Week 5-6: Core Filling UI**

**Design Specifications**:

- Clean, focused interface (minimal chrome)
- Progress indicator at top
- Section navigation (previous/next)
- Auto-save indicator
- Floating action buttons

**Implementation Tasks**:

- [ ] Redesign `FormFillingView.axaml`
- [ ] Implement progress tracking service
- [ ] Add auto-save with debouncing (30 seconds)
- [ ] Create section navigator component
- [ ] Add keyboard navigation (Tab, Enter)

**Week 7-8: Optional Webhook Submission**

**Design Specifications**:

- Submit button in form filling view (only when template defines `submitUrls`)
- POST as JSON to each configured webhook
- Response feedback (success / failure with retry)
- Local copy always retained — webhook submission never replaces save

**Implementation Tasks**:

- [ ] Create `SubmissionPanel` component
- [ ] Wire up `submitUrls` array from APR metadata
- [ ] Implement async POST with progress + retry
- [ ] Confirmation dialog with reference (if server returns one)
- [ ] Submission history tracking in filled-form metadata

### Month 3: Capability-Profile Polish

**Week 9-10: Per-Feature Flag Split + Presets**

(See `docs/CAPABILITY_PROFILES.md` for the full flag list.)

**Implementation Tasks**:

- [ ] Split `VisualFormattingProfile` into 12 individual flag classes
- [ ] Wire each flag to its own checkbox in Display Preferences
- [ ] Add `ProfilePresets` helper with 5 named presets
- [ ] GUI tests verifying preset switching produces expected on-screen state

**Week 11-12: Cognitive + Motor Flag Implementation**

**Implementation Tasks**:

- [ ] `DyslexiaFont` (Atkinson Hyperlegible)
- [ ] `IncreasedSpacing` (line-height 1.6, letter-spacing 0.05em)
- [ ] `SectionFocusMode` (collapse all but focused section)
- [ ] `AlwaysVisibleHelp` rendering toggle
- [ ] `HitMarginPadding` (extra invisible padding around clickables)
- [ ] `KeyboardShortcutOverlay` (Alt-press highlights shortcuts)

---

## Long-Term Goals (Month 4-6)

### Month 4: Form Authoring Polish

**Week 13-14: Read-only Filled-Form Polish**

**Implementation Tasks**:

- [ ] Mode toggle (read-only review vs edit)
- [ ] "Last submitted" indicator from metadata
- [ ] Diff view between two filled-form versions

### Month 5: CLI Enhancement

**Week 15-16: Coverage Ratchet**

Push CLI test coverage from ~28% toward 95% across `ValidateCommand`,
`InfoCommand`, `NewCommand`, `FillCommand`, `DiffCommand`, `ExportCommand`,
and the `FormFillingApi`.

**Week 19-20: New CLI Commands**

```bash
promptresponse-cli profile list           # List active capability flags
promptresponse-cli profile preset NAME    # Apply a preset to user settings
promptresponse-cli batch fill GLOB --csv  # Batch-fill forms from CSV
```

**Implementation Tasks**:

- [ ] `ProfileCommand` with `list` and `preset` subcommands
- [ ] `BatchFillCommand` with CSV input
- [ ] Update `--help` output for all commands

### Month 6: Testing & Polish

**Week 21-22: Accessibility Audit**

**Testing Requirements**:

- [ ] Screen reader testing (NVDA, JAWS, Orca)
- [ ] Keyboard navigation audit
- [ ] Color contrast verification (4.5:1 minimum)
- [ ] Focus indicator visibility
- [ ] High contrast mode testing
- [ ] 200% text scaling testing

**Implementation Tasks**:

- [ ] Add missing `AutomationProperties.Name`
- [ ] Fix contrast issues
- [ ] Improve focus indicators
- [ ] Add skip navigation
- [ ] Test and fix keyboard traps

**Week 23-24: Performance & Release**

**Performance Targets**:

- Form load: < 1 second for 1000 fields
- Webhook submission: progress indication for > 500ms
- UI responsiveness: No blocking operations on UI thread

**Implementation Tasks**:

- [ ] Profile and optimize form loading
- [ ] Add virtual scrolling for large forms
- [ ] Implement async everywhere
- [ ] Create release builds
- [ ] Write release notes

---

## CLI Command Reference

### Current Commands

| Command | Description | Status |
|---------|-------------|--------|
| `validate FILE` | Validate APR file structure | ✅ Done |
| `info FILE` | Display form information | ✅ Done |
| `new FILE` | Create new template | ✅ Done |
| `stats FILE` | Show form statistics | ✅ Done |
| `export FILE` | Export to CSV/JSON/TXT | ✅ Done |
| `diff FILE1 FILE2` | Compare two forms | ✅ Done |
| `fill FILE` | Fill form interactively | ✅ Done |

### Planned Commands

| Command | Description | Status |
|---------|-------------|--------|
| `profile list` | List active capability flags | ⏳ Planned |
| `profile preset NAME` | Apply a named capability preset | ⏳ Planned |
| `batch fill GLOB` | Batch-fill forms from CSV | ⏳ Planned |

---

## Resource Requirements

### Development Team

| Role | Allocation | Responsibilities |
|------|------------|------------------|
| Senior Developer | Full-time | Core features, architecture |
| UI/UX Designer | Half-time | Design system, mockups |
| QA Engineer | Quarter-time | Testing, accessibility audit |

### Infrastructure

- **Source Control**: GitHub
- **CI/CD**: GitHub Actions
- **Testing Devices**: Windows 11, macOS, Linux (Ubuntu)

### Testing Tools

- **Windows**: Narrator, NVDA, Accessibility Insights
- **macOS**: VoiceOver, Accessibility Inspector
- **Linux**: Orca, Accerciser

---

## Risk Mitigation

### Technical Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Avalonia limitations | High | Fallback designs, custom controls |
| Capability-flag interaction bugs | Medium | Compose-test every preset; CompositeProfile contract tests |
| Cross-platform differences | Medium | Extensive testing, platform abstractions |

### User Adoption Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Learning curve | High | Tutorials, in-app guidance, simple defaults |
| Migration friction | Medium | Import tools for existing forms |
| Trust concerns | Medium | Open source, clear security documentation |
| Offline anxiety | Low | Clear sync status indicators |

---

## Success Criteria

### Month 1 Checkpoint

- [ ] Clean, organized codebase (no archived languages)
- [ ] New documentation structure in place
- [ ] Main window redesigned with Windows 11 style
- [ ] All existing tests passing
- [ ] Template editor three-panel layout working

### Month 3 Checkpoint

- [ ] Form filling experience smooth and intuitive
- [ ] All 12 visual flags split out + 5 named presets working
- [ ] Cognitive + motor flags implemented
- [ ] Optional webhook submission working end-to-end

### Month 6 Checkpoint

- [ ] Feature-complete for v1.0
- [ ] Full documentation
- [ ] 95% test coverage
- [ ] Accessibility audit passed
- [ ] Performance targets met
- [ ] Release builds for Windows, macOS, Linux

---

## Review Schedule

| Frequency | Activity | Participants |
|-----------|----------|--------------|
| Daily | Standup (async) | Development team |
| Weekly | Progress review | Development team |
| Bi-weekly | User testing | Team + test users |
| Monthly | Stakeholder review | Team + stakeholders |
| Quarterly | Vision alignment | Full team |

---

## Budget Estimate

| Category | Amount | Notes |
|----------|--------|-------|
| Development | $150,000 | 6 months full-time senior dev |
| Design | $30,000 | 6 months half-time designer |
| Testing | $20,000 | QA + accessibility audit |
| Infrastructure | $5,000 | CI/CD, testing devices |
| Marketing | $10,000 | Documentation, website, outreach |
| **Total** | **$215,000** | |

---

## Immediate Next Steps

1. **Review and approve** this implementation plan
2. **Archive language implementations** to separate repository
3. **Create documentation structure** per this plan
4. **Begin Week 3 tasks** (Main Window Redesign)
5. **Schedule weekly standups**

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2024-11-20 | 1.0 | Initial implementation plan |

---

*This is a living document. Update weekly with progress, blockers, and adjustments.*

# PromptResponse Implementation Plan

<!-- AI-ASSISTANT-README -->
This document contains the phased implementation notes for PromptResponse.
AI assistants should prefer ROADMAP.md and docs/FEATURES.md for current
planning, then use this file for historical implementation context.
<!-- END-AI-ASSISTANT-README -->

## Current Status

**Phase**: Public-beta hardening (v0.6.x)
**Focus**: release reliability, documentation/version reconciliation, import
review UX, packaging validation, and warning cleanup

### What's Built

- ✅ Core library (models, serialization, validation)
- ✅ Desktop application (home screen, recent files, starter templates,
  template editing, form filling, search, progress, advisory panel, signatures)
- ✅ CLI tool (validate, info, new, fill, stats, diff, export, import, keygen,
  sign, verify)
- ✅ Capability-profile architecture and accessible rendering profiles
- ✅ PDF export (flat, fillable, PDF/A archival), HTML export, and PDF import
- ✅ Safe expression engine for calculations and conditional logic
- ✅ Certificate-backed publisher/filler signatures
- ✅ Cross-platform release workflows for Windows, Linux, and macOS artifacts

### What's In Progress

- 🔄 Documentation/version reconciliation against the v0.6.0 codebase
- 🔄 Solution/build hygiene so default commands cover the full project set
- 🔄 Release hardening: smoke validation of install/open/fill/export/import/sign

### What's Planned

- ✅ Import review UX for low-quality PDFs
- ✅ In-app print preview before export
- ✅ Shared SDK conformance corpus with .NET validation gate
- ⏳ Non-.NET SDK conformance runners or explicit experimental labels
- ⏳ macOS signing/notarization plan

> **Scope guard:** PromptResponse stays local-first. Cloud-by-default workflows,
> RBAC, analytics, hosted collaboration, and SSO remain deferred until there is
> validated demand and an explicit product decision.

---

## Immediate Actions

### Release Hardening

- [x] Align project versions with current release (`0.6.0`)
- [x] Fix solution configuration gaps so default `dotnet test PromptResponse.sln`
      includes the expected project set
- [x] Replace obsolete Avalonia `TextBox.Watermark` usage with `PlaceholderText`
- [ ] Run full local test suite after hardening changes
- [ ] Run release smoke tests from published artifacts on Windows, Linux, macOS
- [ ] Document any release-blocking issues in a dedicated known-issues section

### Product Polish

- [x] Add import review UX for cryptic labels, duplicate labels, and likely
      radio groups
- [ ] Add purpose-built cleanup tools for weak sectioning and radio-group
      conversion
- [x] Add in-app print preview over the shared render model
- [ ] Add page-break-accurate preview over the existing PDF renderer
- [x] Add a shared APR conformance corpus
- [x] Gate the corpus in .NET tests
- [ ] Decide whether non-.NET SDKs are supported or experimental, then add
      runners for the SDKs that stay supported

### Historical Plan

The sections below this point are retained as historical planning material from
the earlier foundation phase. Treat ROADMAP.md and docs/FEATURES.md as the
current source of truth.

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

1. Finish documentation/version reconciliation against v0.6.0.
2. Run `dotnet test PromptResponse.sln` and confirm no projects are skipped by
   solution configuration.
3. Run release smoke tests from packaged artifacts on all supported desktop
   platforms.
4. Start import review UX.
5. Plan page-exact print preview and SDK conformance work.

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2024-11-20 | 1.0 | Initial implementation plan |
| 2026-06-10 | 1.1 | Reframed current phase around v0.6.0 public-beta hardening |

---

*This is a living document. Update weekly with progress, blockers, and adjustments.*

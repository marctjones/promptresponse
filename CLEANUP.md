# Codebase Cleanup Checklist

<!-- AI-ASSISTANT-README -->
This document lists files and directories to be archived, removed, or reorganized.
Execute this cleanup before starting UI implementation work.
Archive operations should preserve git history.
<!-- END-AI-ASSISTANT-README -->

## Overview

The PromptResponse codebase contains several items that should be cleaned up to improve organization:

1. **Language implementations** - Archive to separate repository
2. **Loose files** - Move to proper locations
3. **Placeholder files** - Remove
4. **Documentation** - Reorganize

---

## Phase 1: Archive Language Implementations

### Create Archive Repository

```bash
# Create new repository: promptresponse-multilang
# Purpose: Reference implementations in other languages
```

### Directories to Archive

| Directory | Size | Description | Action |
|-----------|------|-------------|--------|
| `/cpp/` | 37 KB | C++ implementation | Archive to multilang repo |
| `/java/` | 67 KB | Java implementation | Archive to multilang repo |
| `/python/` | 102 KB | Python implementation | Archive to multilang repo |
| `/rust/` | 41 KB | Rust implementation | Archive to multilang repo |

**Total**: 247 KB to archive

### Archive Process

```bash
# 1. Create the archive repository
# 2. Copy directories with history (git filter-branch or git subtree)
# 3. Update README in archive repo explaining these are reference implementations
# 4. Remove from main repository
# 5. Update .gitignore to prevent re-adding
```

### Post-Archive Cleanup

Add to `.gitignore`:
```gitignore
# Archived language implementations (see promptresponse-multilang)
/cpp/
/java/
/python/
/rust/
```

---

## Phase 2: Move Loose Files

### Files to Relocate

| File | Current Location | New Location | Reason |
|------|------------------|--------------|--------|
| `Screenshot From 2025-11-12 23-09-30.png` | `/` (root) | `/docs/images/` | Organize screenshots |

### Process

```bash
# Create images directory
mkdir -p docs/images

# Move screenshot
mv "Screenshot From 2025-11-12 23-09-30.png" docs/images/

# Rename to something meaningful
mv docs/images/"Screenshot From 2025-11-12 23-09-30.png" docs/images/desktop-app-v1.png
```

---

## Phase 3: Remove Placeholder Files

### Files to Delete

| File | Location | Reason |
|------|----------|--------|
| `UnitTest1.cs` | `/tests/PromptResponse.AccessibilityTests/` | Generic placeholder, no real tests |

### Process

```bash
# Remove placeholder test
rm tests/PromptResponse.AccessibilityTests/UnitTest1.cs
```

---

## Phase 4: Documentation Reorganization

### Current Structure Issues

1. Multiple S3-related docs with overlapping content
2. Documentation not organized by type
3. Missing INDEX.md for navigation

### Proposed Structure

```
docs/
├── INDEX.md                    ✅ Created
├── VISION.md                   ✅ Created
├── IMPLEMENTATION_PLAN.md      ✅ Created
├── architecture/
│   ├── SYSTEM_ARCHITECTURE.md  ← Move from ARCHITECTURE.md
│   ├── DATA_MODEL.md           ← New (APR format deep dive)
│   ├── S3_INTEGRATION.md       ← Keep
│   └── CERTIFICATE_SYSTEM.md   ← New
├── guides/
│   ├── DEVELOPMENT.md          ← Keep
│   ├── UI_GUIDELINES.md        ← New
│   └── WORKFLOW_GUIDE.md       ← New
├── specifications/
│   ├── FILE_FORMAT.md          ← Keep
│   ├── CLI_REFERENCE.md        ← New
│   └── API_SPECIFICATION.md    ← New
└── images/
    └── (screenshots)
```

### Migration Steps

```bash
# Create new directories
mkdir -p docs/architecture docs/guides docs/specifications docs/images

# Move existing files
mv docs/ARCHITECTURE.md docs/architecture/SYSTEM_ARCHITECTURE.md

# Keep in current location (well-organized already)
# - docs/FILE_FORMAT.md → docs/specifications/FILE_FORMAT.md
# - docs/DEVELOPMENT.md → docs/guides/DEVELOPMENT.md

# Consolidate S3 docs (manual review needed)
# - S3_SUBMISSION_IMPLEMENTATION.md
# - FORM_MANAGEMENT_SYSTEM.md
# - TEMPLATE_PUBLISHING_WORKFLOW.md
# - MINIO_SETUP.md
# Consider merging into docs/architecture/S3_INTEGRATION.md
```

---

## Phase 5: Example File Cleanup

### Files to Review

Check for malformed filenames in `/examples/`:

| File | Issue | Action |
|------|-------|--------|
| Various `.aprf` files | Verify naming consistency | Rename if needed |

### Naming Convention

- Templates: `kebab-case.aprt`
- Filled forms: `kebab-case.aprf`
- Generic: `kebab-case.apr` (legacy only)

---

## Verification Checklist

After cleanup, verify:

- [ ] Solution builds: `dotnet build`
- [ ] All tests pass: `dotnet test`
- [ ] No broken links in documentation
- [ ] .gitignore updated
- [ ] Archive repository created and populated
- [ ] Root directory is clean

---

## Expected Results

### Before Cleanup

```
promptresponse/
├── cpp/                    ← Remove
├── java/                   ← Remove
├── python/                 ← Remove
├── rust/                   ← Remove
├── Screenshot....png       ← Move
├── docs/
│   ├── (flat structure)
│   └── (multiple S3 docs)
└── ...
```

### After Cleanup

```
promptresponse/
├── src/
├── tests/
├── examples/
├── docs/
│   ├── INDEX.md
│   ├── VISION.md
│   ├── IMPLEMENTATION_PLAN.md
│   ├── architecture/
│   ├── guides/
│   ├── specifications/
│   └── images/
├── .claude/
│   ├── context.md
│   └── DESIGN_SYSTEM.md
├── CLAUDE.md
├── README.md
└── (other root configs)
```

**Reduction**: ~250 KB removed, cleaner structure

---

## Execution Order

1. **Archive languages first** - Biggest change, do carefully
2. **Move files** - Quick wins
3. **Remove placeholders** - Trivial
4. **Reorganize docs** - Can be done incrementally
5. **Clean examples** - Review and rename

---

## Notes

- **Preserve git history** when archiving (use git subtree split)
- **Update CI/CD** if any pipelines reference archived directories
- **Communicate changes** to any contributors
- **Create redirect notices** in archived repo pointing to main repo

---

*Cleanup checklist version 1.0 - Created 2024-11-20*

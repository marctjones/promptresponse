<p align="center">
  <img src="assets/logo.svg" alt="PromptResponse logo" width="120" height="120">
</p>

# PromptResponse

A cross-platform form creation and filling application that makes it easy for office workers to create typical office forms without the hassle of editing PDFs, wrangling Word form fields and tables, or dealing with weird PDF form rendering issues.

## Overview

PromptResponse (.apr format) breaks free from the page metaphor. Traditional forms trap your data in formats that are frustrating to work with and difficult to process. PromptResponse solves this with a flexible, portable, text-based format designed for:

**Solving Daily Frustrations:**
- **Office Workers**: Create and fill forms without fighting PDF editors, Word tables, or Excel protection schemes
- **Database Integration**: JSON format imports directly into databases - no parsing headaches
- **Programmatic Filling**: Easy API for automated form filling from scripts or other systems

**Better by Design:**
- **Flexible**: No fixed layouts or styling - forms adapt to any screen
- **Portable**: Simple JSON that's easy to parse and transform
- **Accessible**: accessibility-oriented interaction and automated regression checks
- **Semantic**: Organized by sections and prompts for meaningful structure
- **Safe**: Pure data format - no code execution, safe to open untrusted files

## Implementations

| | Profile | Tests |
|---|---|---|
| **.NET** — `src/` | beta.6 JSONC/YAML, streams, manifests, and CMS attestation verification | beta.6 focused core tests |
| **Python** — `python/` | beta.6 JSONC/YAML streams, digests, manifests, witness resolution, and detached CMS content verification | beta.6 shared-corpus tests; trust is caller policy |
| **TypeScript** — `typescript/` | browser-safe beta.6 JSONC/YAML streams, digests, manifests, witness resolution, and async detached CMS content verification | beta.6 shared-corpus tests; trust is caller policy |
| **Java** — `java/` | beta.6 JSONC/YAML streams, digests, manifests, witness resolution, and detached CMS content verification | beta.6 shared-corpus tests; trust is caller policy |

The Python and TypeScript SDKs run the same beta.6 corpus as .NET. They parse
forms and independent attestation records without treating an attestation as a
member of its form; trust policy stays with the calling application.

It also earns its keep as a second opinion. Within an hour of existing it found
a conformance bug in .NET that had been invisible since tables were redesigned:
two computed properties were reaching the wire as JSON booleans, in a format
that permits exactly one.

## Key Features

- **Template Creation**: Design reusable form templates without fighting layout tools
- **Form Filling**: Fill out forms with intelligent input assistance
- **Import Existing Forms**: Turn a fillable PDF into a template with `apr import`, or use the `document-to-apr` AI skill for flat/scanned PDFs, Word, OpenDocument, or images — see [docs/IMPORT.md](docs/IMPORT.md)
- **Database-Ready**: JSON format imports directly into databases without parsing headaches
- **Programmatic API**: Fill forms from scripts, batch processes, or other applications
- **Type Hints**: Suggest data types without enforcing them
- **Cross-Platform**: Linux, Windows, and macOS desktop clients; browser demos
- **Open Format**: JSON subset for maximum interoperability

## Quick Start Demo

Try PromptResponse in 30 seconds with just Python:

```bash
# Clone and run
git clone https://github.com/marctjones/promptresponse.git
cd promptresponse
./run-web-demo.sh
```

Open http://localhost:8080 to see the form. Fill it in and submit: the answers
are written to a `.aprf` beside the source and printed to your terminal.

Point it at any APR document — `./run-web-demo.sh path/to/form.aprt`. Tables
render as tables, roles are marked, and a document that fails validation still
renders with its problems listed, because a flawed form must open or nobody can
be shown what is wrong with it.

**Requirements:** Python 3 (Flask is auto-installed)

The demo reads the file through the Python SDK in [`python/`](python/), so it
enforces the format rather than having its own opinion about it: a response
given as a JSON number is refused, and a document that parses but does not
validate still opens with its problems printed.

## Installation (Full .NET Application)

```bash
# Prerequisites: .NET 10.0 SDK
dotnet --version  # Should be 10.0 or higher

# Clone repository
git clone https://github.com/marctjones/promptresponse.git
cd promptresponse

# Build
dotnet build

# Run tests
dotnet test
```

### Easy Launcher (Recommended)

Use the provided launcher scripts for the easiest experience:

**Linux/macOS:**
```bash
./run.sh              # Launch GUI application
./run.sh --demo       # Run interactive CLI demos
./run.sh --validate   # Validate example files
./run.sh --help       # Show CLI help
./run.sh --usage      # Show all launcher options
```

**Windows:**
```powershell
.\run.ps1             # Launch GUI application
.\run.ps1 demo        # Run interactive CLI demos
.\run.ps1 validate    # Validate example files
.\run.ps1 help        # Show CLI help
.\run.ps1 usage       # Show all launcher options
```

### Manual Commands

You can also run the applications directly:

```bash
# Run CLI tool
dotnet run --project src/PromptResponse.Cli -- help

# Run Desktop application
dotnet run --project src/PromptResponse.Desktop
```

### Creating Your First Template

1. Launch PromptResponse
2. Select "New Template"
3. Add sections and prompts
4. Save as `.apr` file

### Filling Out a Form

1. Open a template (`.apr`)
2. Choose "Fill Out Form"
3. Complete responses
4. Save filled form

## Quick Start with CLI

```bash
# Is this a valid document?
dotnet run --project src/PromptResponse.Cli -- validate examples/expense-report.aprt

# What is in it?
dotnet run --project src/PromptResponse.Cli -- info examples/contact-intake.aprt

# Start a new template
dotnet run --project src/PromptResponse.Cli -- new my-form

# Can a machine process this submission, or does it need a person?
dotnet run --project src/PromptResponse.Cli -- review submission.aprf --json
```

Primary commands are `validate`, `info`, `new`, `fill`, `stats`, `review`,
`eval`, `diff`, `export`, `import`, `attest`, and `submit`; the explicit
`beta6` namespace remains available for normalization. See
[the CLI README](src/PromptResponse.Cli/README.md).

`review` is the one built for the receiving end. The format never rejects what a
person writes, so every submission that parses is valid — which tells whoever
receives it nothing about whether their pipeline can read it. `review` answers
that instead, and exits `0` to process, `2` to route to a person, `1` if the
file could not be read at all.

## Documentation

- [APR Format Specification](docs/APR_SPECIFICATION.md) - **beta.6 format target** (breaking migration in progress)

> **APR has not been publicly released.** The active migration intentionally replaces
> beta.3 with `"version": "1.0-beta.6"`; beta.3 files are not a compatibility target.
- [JSON Schema](schemas/apr-1.0-beta.6.schema.json) - machine-readable beta.6 schema
- [beta.6 conformance corpus](tests/Conformance/beta6/README.md) - the executable format contract
- [SDK Conformance](docs/SDK_CONFORMANCE.md) - what an implementation must do to claim conformance
- [Documentation map](docs/README.md) - authoritative product, architecture, UX, format, registry, and guide documents
- [Implementation Registry](docs/IMPLEMENTATION_REGISTRY.md) - every app, SDK, and engine: status and obligations
- [CLI Tool Guide](src/PromptResponse.Cli/README.md) - Command-line tool usage
- [User Guide](docs/USER_GUIDE.md) - Desktop, CLI, export, import, and signing workflows
- [Development Guide](docs/DEVELOPMENT.md) - Contributing and development setup
- [Concept Registry](docs/CONCEPT_REGISTRY.md) - intended behavior, code owner, and regression evidence
- [Architecture](docs/ARCHITECTURE.md) - System design and architecture

## Implementing APR

APR is an open format, and the point of the specification, schema, and corpus is that
you can build a reader or writer without asking us anything.

```bash
# The conformance corpus: beta.6 vectors your implementation must agree with
ls tests/Conformance/beta6/

# The language-neutral gates - no .NET required, so they fail the way your SDK would
pip install jsonschema
python3 scripts/check-schema.py          # schema agrees with every fixture
python3 scripts/check-test-registry.py   # coverage claims match the repository
./scripts/benchmark-beta6-compliance.sh  # beta.6 SDK and desktop compliance gates
```

Declare conformance as `APR 1.0-beta.6 core+attestations`. Older APR versions are
rejected. See
[docs/SDK_CONFORMANCE.md](docs/SDK_CONFORMANCE.md) for what each corpus category
requires, and [docs/IMPLEMENTATION_REGISTRY.md](docs/IMPLEMENTATION_REGISTRY.md) for
which implementations exist and what they are held to.

## Technology Stack

- **.NET 10.0** (LTS, supported through Nov 2028) — cross-platform runtime
- **C# 14** — modern language features
- **AvaloniaUI 12** — cross-platform UI framework, first .NET UI framework
  with a native Linux accessibility (AT-SPI2) backend
- **CommunityToolkit.Mvvm 8.4** — MVVM source generators (`[ObservableProperty]`,
  `[RelayCommand]`)
- **xUnit.v3** — testing framework
- **NSubstitute** — mocking (MIT-licensed; replaces Moq)
- **AwesomeAssertions** — fluent assertion library (Apache 2.0 community
  fork of FluentAssertions, kept open-source by design)

## License

PromptResponse is licensed under AGPL-3.0-or-later. Its third-party
dependencies and bundled assets are restricted to permissively licensed
components; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the
manually maintained inventory and policy.

## Project Status

🚧 **Pre-public beta migration** — beta.3 is the implementation baseline and
`1.0-beta.6` is the active format target. See [ROADMAP.md](ROADMAP.md) for the
dependency order; do not publish or stabilize beta.3 as a public contract.

- [x] Core library (models, JSON serialization, advisory validation,
      hidden-character + mixed-script advisors)
- [x] CLI tool (validate, info, new, fill, stats, diff, export, import,
      attest) with CI coverage gates
- [x] Capability-profile rendering system: Light / Dark / HighContrast,
      LargeText / ReducedMotion / ScreenReaderTuned / LargeHitTargets /
      WizardMode globals, plus 12 composable display + input-mask flags,
      named presets (Excellent vision, Blind/SR, LowVision/HC,
      Cognitive/Dyslexia, Motor/Mobility) selectable via
      `View → Capability Profile`
- [x] Polymorphic prompt views (one focused VM + view per data-type hint)
- [x] **APRT structural editor** with inline title/label edits, type
      ComboBox, hints expander, drag-and-drop reorder, undo/redo
      (Ctrl+Z / Ctrl+Y), section + nested-section + table-section
      authoring, and a document-metadata expander
- [x] **Wizard mode**: section-at-a-time rendering with Previous/Next
      navigation; auto-on under the Cognitive preset (`View → Toggle
      Wizard Mode` / Ctrl+W)
- [x] Home screen with recent files, starter templates, search, progress,
      advisory panel, and signing status
- [x] PDF export: flat, fillable AcroForm, PDF/A archival, page sizes,
      running footer, and handling banners
- [x] **Print preview**: in-app generated-content preview before PDF export
- [x] HTML export: accessible read-only page and self-contained fillable web
      form that downloads `.aprf`
- [x] PDF import: AcroForm field extraction with import-quality scoring
- [x] Calculation and conditional logic via safe expression hints
- [x] Verifiable beta.6 CMS attestations: independent document/field assertions,
      CLI creation/verification, and desktop attestation UI
- [x] **Linux accessibility (AT-SPI2)** — native screen-reader support
      via Avalonia 12; verified against Orca's AT-SPI bus
- [x] Three-layer blind-user accessibility test stack: in-process
      AutomationPeer tree + keyboard flow tests + external AT-SPI smoke
      test (`./tests/at-spi/run_at_spi_smoke.sh`)
- [x] CI accessibility evidence: theme contrast, keyboard behavior, and
      accessible-name checks; see [UX and accessibility](docs/UX_ACCESSIBILITY.md)
      for the remaining live assistive-technology evidence
- [ ] Mobile support (.NET MAUI) — future
- [ ] Word/Excel export — future
- [x] SDK conformance corpus: shared APR fixtures with .NET validation gate
- [x] Python, TypeScript, and Java core SDK conformance runners

## Contributing

See [DEVELOPMENT.md](docs/DEVELOPMENT.md) for development guidelines and best practices.

## Contact

Report issues at: https://github.com/marctjones/promptresponse/issues

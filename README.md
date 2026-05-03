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
- **Accessible**: WCAG 2.1 Level AA compliance built-in
- **Semantic**: Organized by sections and prompts for meaningful structure
- **Safe**: Pure data format - no code execution, safe to open untrusted files

## Key Features

- **Template Creation**: Design reusable form templates without fighting layout tools
- **Form Filling**: Fill out forms with intelligent input assistance
- **Database-Ready**: JSON format imports directly into databases without parsing headaches
- **Programmatic API**: Fill forms from scripts, batch processes, or other applications
- **Type Hints**: Suggest data types without enforcing them
- **Cross-Platform**: Linux, Windows (macOS, Android, iOS planned)
- **Open Format**: JSON subset for maximum interoperability

## Quick Start Demo

Try PromptResponse in 30 seconds with just Python:

```bash
# Clone and run
git clone https://github.com/marctjones/promptresponse.git
cd promptresponse
./run-aprt-server.sh
```

Open http://localhost:8080 in your browser to see a form. Fill it out and submit - the JSON data prints to your terminal.

**Requirements:** Python 3 (Flask is auto-installed)

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
# Validate a form
dotnet run --project src/PromptResponse.Cli -- validate examples/employment-application.apr

# View form information
dotnet run --project src/PromptResponse.Cli -- info examples/simple-contact-form.apr

# Create a new template
dotnet run --project src/PromptResponse.Cli -- new my-form.apr
```

See [CLI README](src/PromptResponse.Cli/README.md) for complete CLI documentation.

## Documentation

- [APR Format Specification v0.2](docs/APR_SPECIFICATION_v0.2.md) - **Complete formal specification**
- [CLI Tool Guide](src/PromptResponse.Cli/README.md) - Command-line tool usage
- [Usage Guide](docs/USAGE.md) - Detailed usage instructions
- [Development Guide](docs/DEVELOPMENT.md) - Contributing and development setup
- [File Format Specification](docs/FILE_FORMAT.md) - .apr format documentation (legacy)
- [Architecture](docs/ARCHITECTURE.md) - System design and architecture

## Technology Stack

- **.NET 10.0** (LTS, supported through Nov 2028) — cross-platform runtime
- **C# 14** (preview) — modern language features
- **AvaloniaUI 12** — cross-platform UI framework, first .NET UI framework
  with a native Linux accessibility (AT-SPI2) backend
- **CommunityToolkit.Mvvm 8.4** — MVVM source generators (`[ObservableProperty]`,
  `[RelayCommand]`)
- **xUnit.v3** — testing framework
- **NSubstitute** — mocking (MIT-licensed; replaces Moq)
- **AwesomeAssertions** — fluent assertion library (Apache 2.0 community
  fork of FluentAssertions, kept open-source by design)

## License

GPL-3.0-or-later (see each project's `<PackageLicenseExpression>` in
the corresponding `.csproj`).

## Project Status

🚧 **Active Development** — current release: **v0.2.0** (2026-05-03).
See [CHANGELOG.md](CHANGELOG.md) for what landed since v0.1.0.

- [x] Core library (models, JSON serialization, advisory validation,
      hidden-character + mixed-script advisors)
- [x] CLI tool (validate, info, new, fill, stats, diff, export) with
      ~83% line coverage gate
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
- [x] **Linux accessibility (AT-SPI2)** — native screen-reader support
      via Avalonia 12; verified against Orca's AT-SPI bus
- [x] Three-layer blind-user accessibility test stack: in-process
      AutomationPeer tree + keyboard flow tests + external AT-SPI smoke
      test (`./tests/at-spi/run_at_spi_smoke.sh`)
- [x] WCAG-gated CI: every theme contrast pair, every keyboard
      shortcut, every interactive control's accessible name
- [ ] Mobile support (.NET MAUI) — future
- [ ] Calculation engine (computed fields) — future
- [ ] Conditional logic (show/hide based on responses) — future

## Contributing

See [DEVELOPMENT.md](docs/DEVELOPMENT.md) for development guidelines and best practices.

## Contact

Report issues at: https://github.com/marctjones/promptresponse/issues

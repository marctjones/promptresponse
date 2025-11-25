# PromptResponse

A cross-platform form creation and filling application that makes it easy for office workers to create typical office forms without the hassle of editing PDFs, wrangling Word form fields and tables, or dealing with weird PDF form rendering issues.

## Overview

PromptResponse (.apr format) breaks free from the page metaphor. Traditional forms trap your data in formats that are frustrating to work with and difficult to process. PromptResponse solves this with a flexible, portable, text-based format designed for:

**Solving Daily Frustrations:**
- **Office Workers**: Create and fill forms without fighting PDF editors, Word tables, or Excel protection schemes
- **Database Integration**: JSON format imports directly into databases - no parsing headaches
- **Online Submissions**: Submit forms to S3 or webhooks without building custom CRUD apps for every form
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
- **Direct Submission**: Submit filled forms to S3 or webhooks - no server code needed
- **Database-Ready**: JSON format imports directly into databases without parsing headaches
- **Programmatic API**: Fill forms from scripts, batch processes, or other applications
- **Type Hints**: Suggest data types without enforcing them
- **Cross-Platform**: Linux, Windows (macOS, Android, iOS planned)
- **Open Format**: JSON subset for maximum interoperability

## Quick Start

### Installation

```bash
# Prerequisites: .NET 8.0 SDK
dotnet --version  # Should be 8.0 or higher

# Clone repository
git clone https://github.com/yourusername/promptresponse.git
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

- [CLI Tool Guide](src/PromptResponse.Cli/README.md) - Command-line tool usage
- [Usage Guide](docs/USAGE.md) - Detailed usage instructions
- [Development Guide](docs/DEVELOPMENT.md) - Contributing and development setup
- [File Format Specification](docs/FILE_FORMAT.md) - .apr format documentation
- [Architecture](docs/ARCHITECTURE.md) - System design and architecture

## Technology Stack

- **.NET 8.0** - Cross-platform runtime
- **C# 12** - Modern language features
- **AvaloniaUI 11** - Cross-platform UI framework
- **xUnit** - Testing framework

## License

GPL-3.0 License - See [LICENSE](LICENSE) file for details

## Project Status

🚧 **Active Development** - MVP Phase 1

- [x] Project structure
- [x] Core library (Models, Serialization, Validation)
- [x] CLI Tool (validate, info, new commands)
- [ ] Desktop UI (Template Editor, Form Filler) - Next phase
- [ ] Linux/Windows testing
- [ ] Mobile support (future)

## Contributing

See [DEVELOPMENT.md](docs/DEVELOPMENT.md) for development guidelines and best practices.

## Contact

Report issues at: https://github.com/yourusername/promptresponse/issues

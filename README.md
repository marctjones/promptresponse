# PromptResponse

A cross-platform form creation and filling application that replaces traditional document-based forms (Word, PDF) with a flexible, portable, text-based format.

## Overview

PromptResponse (.apr format) breaks free from the page metaphor, allowing forms to be:
- **Flexible**: No fixed layouts or styling in the file format
- **Portable**: Simple JSON-based format that's easy to parse
- **Accessible**: Text-based responses with type hints, not restrictions
- **Semantic**: Organized by sections and subsections for meaningful grouping

## Key Features

- **Template Creation**: Design reusable form templates
- **Form Filling**: Fill out forms with intelligent input assistance
- **Type Hints**: Suggest data types without enforcing them
- **Suggested Values**: Autocomplete where helpful
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

# Run application
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

## Documentation

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
- [ ] Core library (Models, Serialization)
- [ ] Desktop UI (Template Editor, Form Filler)
- [ ] Linux/Windows testing
- [ ] Mobile support (future)

## Contributing

See [DEVELOPMENT.md](docs/DEVELOPMENT.md) for development guidelines and best practices.

## Contact

Report issues at: https://github.com/yourusername/promptresponse/issues

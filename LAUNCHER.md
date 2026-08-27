# Launcher Scripts Guide

This directory includes convenient launcher scripts to make running and demonstrating PromptResponse easier.

## Available Scripts

- **`run.sh`** - Bash script for Linux/macOS
- **`run.ps1`** - PowerShell script for Windows

Both scripts provide identical functionality.

## Quick Start

### Open SF-86 Form for Filling (Default)

**Linux/macOS:**
```bash
./run.sh
```

**Windows:**
```powershell
.\run.ps1
```

This builds the project (if needed) and launches the AvaloniaUI desktop application with the SF-86 security clearance template open in form filling mode. This provides an immediate, working demonstration of the application with a comprehensive real-world form.

## Available Commands

### GUI Commands

```bash
# Open SF-86 template for filling (default behavior)
./run.sh

# Launch GUI without opening any file
./run.sh --no-file
./run.sh --gui
./run.sh -g

# Open a specific file for filling out
./run.sh --open examples/contact-intake.aprt
./run.sh examples/myform.aprf  # Shorthand (same as --open)

# Open a template for editing
./run.sh --edit examples/sf-86-background-check.aprt
```

### CLI Demo Commands

```bash
# Run the complete interactive demo suite
./run.sh --demo

# Validate all example APR files
./run.sh --validate

# Show detailed information about all example files
./run.sh --info

# Create a new template interactively
./run.sh --new

# Show CLI help
./run.sh --help
./run.sh -h
```

### Development Commands

```bash
# Run all tests
./run.sh --test

# Build the project only (no run)
./run.sh --build

# Show version information
./run.sh --version
./run.sh -v

# Show launcher usage help
./run.sh --usage
```

## Demo Mode Details

The `--demo` option runs an interactive demonstration that walks you through:

1. **CLI Help** - Shows available CLI commands
2. **Validation** - Validates all example APR files
3. **Information Display** - Shows detailed info about each example
4. **Template Creation** - Optional: Create a demo template interactively

This is perfect for:
- First-time users learning the system
- Demonstrations and presentations
- Testing after making changes
- Showing the full feature set

## Example Workflows

### Filling Out and Converting to Template

```bash
# Open SF-86 form for filling (default)
./run.sh

# Fill out the form in the GUI
# Use File → "Switch to Template Editing" (Ctrl+E) to convert filled form to template
# This allows you to create reusable templates from filled-out forms
```

### Quick Validation Check

```bash
# Validate all examples before committing
./run.sh --validate
```

### Development Workflow

```bash
# Make changes to code...

# Run tests to verify
./run.sh --test

# Launch GUI to test visually
./run.sh --gui
```

### Create and Test a New Template

```bash
# Create a new template
./run.sh --new

# This creates demo-template.apr and:
# - Validates the template
# - Shows its information
# - Confirms it was created correctly
```

### Full Demo for Presentation

```bash
# Run the complete demo suite
./run.sh --demo

# Follow the interactive prompts
# Press Enter to step through each demo
```

## Script Features

### Automatic Building

All commands automatically build the project before running. The build output is suppressed for a cleaner experience. If the build fails, you'll see an error message with instructions to run `dotnet build` manually for details.

### Colored Output

The scripts use colored output for better readability:
- **Blue** - Headers and section dividers
- **Green** - Success messages and completions
- **Yellow** - Information and prompts
- **Red** - Errors and warnings

### Error Handling

- Scripts verify you're in the correct directory
- Build failures are caught and reported
- Exit codes are preserved for scripting

## Customization

You can modify the scripts to add your own demo scenarios or shortcuts. Key functions to customize:

**In `run.sh`:**
- `demo_validate()` - Custom validation logic
- `demo_info()` - Custom info display
- `demo_new_template()` - Template creation workflow
- `demo_all()` - Complete demo sequence

**In `run.ps1`:**
- `Invoke-ValidateDemo` - Custom validation logic
- `Invoke-InfoDemo` - Custom info display
- `Invoke-NewTemplateDemo` - Template creation workflow
- `Invoke-AllDemos` - Complete demo sequence

## Troubleshooting

### "Permission denied" on Linux/macOS

Make the script executable:
```bash
chmod +x run.sh
```

### PowerShell Execution Policy on Windows

If you get an execution policy error:
```powershell
# Option 1: Run with bypass (for one-time use)
powershell -ExecutionPolicy Bypass -File .\run.ps1

# Option 2: Set execution policy for current user (permanent)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### ".NET SDK not found"

Ensure .NET 10.0 SDK is installed:
```bash
dotnet --version  # Should show 10.0.x
```

Download from: https://dotnet.microsoft.com/download

### "Must be run from the PromptResponse project root directory"

Navigate to the project root (where `PromptResponse.sln` is located):
```bash
cd /path/to/promptresponse
./run.sh
```

## Integration with CI/CD

The scripts can be used in automated workflows:

```bash
# Run tests in CI
./run.sh --test
EXIT_CODE=$?
if [ $EXIT_CODE -ne 0 ]; then
    echo "Tests failed!"
    exit 1
fi

# Validate examples
./run.sh --validate
```

## Advanced Usage

### Chain Commands

You can chain commands using shell operators:

```bash
# Build, test, then launch GUI
./run.sh --test && ./run.sh --gui

# Validate and show info
./run.sh --validate && ./run.sh --info
```

### Scripting

Use the scripts in your own automation:

```bash
#!/bin/bash

# Automated workflow
echo "Running PromptResponse validation..."
./run.sh --validate

echo "Running tests..."
./run.sh --test

echo "Validation and testing complete!"
```

## Support

For issues with the launcher scripts, check:
1. You're in the project root directory
2. .NET 10.0 SDK is installed
3. The project builds successfully: `dotnet build`
4. Scripts have execute permissions (Linux/macOS)

For issues with the PromptResponse application itself, see the main [README.md](README.md) and [DEVELOPMENT.md](docs/DEVELOPMENT.md).

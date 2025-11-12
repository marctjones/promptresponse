# APR CLI Tool

Command-line tool for working with APR (Adaptive Prompt Response) files.

## Installation

### Build from Source

```bash
cd src/PromptResponse.Cli
dotnet build
dotnet run -- help
```

### Install as Global Tool (requires .NET SDK)

```bash
dotnet pack -c Release
dotnet tool install --global --add-source ./bin/Release PromptResponse.Cli
```

## Usage

```bash
apr <command> [options]
```

## Commands

### validate

Validates an APR file for structural correctness and data type warnings.

```bash
apr validate <file>
```

**Example:**
```bash
apr validate examples/employment-application.apr
```

**Output:**
- ✓ Success: Shows document stats
- ✗ Structure errors: Lists validation failures
- ⚠ Type warnings: Advisory data type mismatches (doesn't fail validation)

### info

Displays detailed information about an APR file.

```bash
apr info <file>
```

**Example:**
```bash
apr info examples/simple-contact-form.apr
```

**Output:**
- Document metadata (title, type, author, etc.)
- Structure summary (sections, subsections, prompts)
- Completion status (for filled forms)
- Section details with prompt counts

### new

Creates a new APR template file interactively.

```bash
apr new <file>
```

**Example:**
```bash
apr new my-form.apr
```

**Prompts for:**
- Template title (required)
- Description (optional)
- Author (optional)
- Template ID (optional)

Creates a minimal template with one example section and prompt.

### help

Shows usage information.

```bash
apr help
```

### version

Shows version information.

```bash
apr version
```

## Examples

### Validate a Template

```bash
$ apr validate examples/employment-application.apr

Validating: examples/employment-application.apr
✓ Validation passed
  Document type: Template
  Title: Employment Application Form
  Sections: 4
  Prompts: 18
```

### View Form Information

```bash
$ apr info examples/simple-contact-form.apr

═══════════════════════════════════════
File: simple-contact-form.apr
═══════════════════════════════════════

Document Information:
  Version: 1.0
  Type: Template
  Title: Simple Contact Form
  Description: Basic contact information collection
  Template ID: simple-contact-v1
  Template Version: 1.0
  Created: 2025-11-12 00:00:00 UTC

Structure:
  Sections: 1
  Total Prompts: 3

Sections:
  1. Contact Information
     ID: section_001
     Prompts: 3

═══════════════════════════════════════
```

### Create a New Template

```bash
$ apr new survey.apr

Creating new APR template...

Template title: Customer Satisfaction Survey
Description (optional): Quarterly customer feedback
Author (optional): Marketing Team
Template ID (optional): customer-survey-q4

✓ Template created: survey.apr

The template has been created with one example section and prompt.
Edit the file to add more sections and prompts.
```

## Exit Codes

- `0`: Success
- `1`: Error (validation failed, file not found, etc.)

## Tips

- Use `apr validate` before sharing templates to ensure they're well-formed
- Use `apr info` to quickly check the structure of large forms
- The `.apr` extension is added automatically if omitted in `apr new`
- Data type warnings are advisory only - all text input is always valid

## Related

- See [FILE_FORMAT.md](../../docs/FILE_FORMAT.md) for the APR format specification
- See [USAGE.md](../../docs/USAGE.md) for general usage guide
- See [DEVELOPMENT.md](../../docs/DEVELOPMENT.md) for development guide

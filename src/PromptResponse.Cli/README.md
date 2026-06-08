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

### fill

Fills out a form interactively or programmatically.

```bash
apr fill <template> [options]
```

**Modes:**

1. **Interactive Mode (Default)**: Walk through each prompt step-by-step
2. **JSON File Mode**: Fill from a JSON file
3. **JSON String Mode**: Fill from a JSON string
4. **Non-Interactive Mode**: Fill from command-line arguments

**Options:**

- `--json-file=<file>`: Fill from JSON file
- `--json=<json-string>`: Fill from JSON string
- `--non-interactive`: Fill from command-line args
- `--set-{promptId}=<value>`: Set response (non-interactive mode)
- `--output=<file>`: Output file (default: template.aprf)
- `--filled-by=<name>`: Name of person filling form
- `--validate`: Validate after filling

**Examples:**

```bash
# Interactive mode
apr fill examples/simple-contact-form.aprt

# Fill from JSON file
apr fill examples/simple-contact-form.aprt --json-file=examples/responses-simple-contact.json

# Fill from JSON string
apr fill template.aprt --json='{"prompt_001":"John Doe","prompt_002":"john@example.com"}'

# Non-interactive with command-line args
apr fill template.aprt --non-interactive \
  --set-prompt_001="John Doe" \
  --set-prompt_002="john@example.com" \
  --filled-by="John Doe"

# Fill and validate
apr fill template.aprt --json-file=responses.json --validate --output=filled-form.aprf
```

**JSON Format:**

```json
{
  "prompt_001": "John Doe",
  "prompt_002": "john@example.com",
  "prompt_003": "2025-11-15"
}
```

**Output:**
- Creates a filled form with `.aprf` extension
- Shows completion percentage
- Optional validation results

### import

Imports a **fillable PDF** (one with AcroForm fields) into an APR template.

```bash
apr import <file.pdf> [--output=<file.aprt>] [--title=<title>]
```

**Options:**
- `--output=<file>`: Output path (default: input name with `.aprt`)
- `--title=<title>`: Document title (default: derived from the file name)
- `--report`: Print a quality score, recommendation, and per-field review flags

**Examples:**
```bash
apr import form.pdf
apr import form.pdf --output=intake.aprt --title="Intake Form"
apr import form.pdf --report
```

Field kinds map to data-type hints (checkbox → `boolean`, dropdown →
`suggestedValues`); the field tooltip becomes the label. Every run prints a
one-line **quality verdict** (the importer scores itself — high when the PDF has
field tooltips, low when it doesn't) with a use-directly / review / use-the-skill
recommendation. Flat/scanned PDFs (no form fields) report an error — for those,
plus Word/OpenDocument/images, use the `document-to-apr` skill. See
[docs/IMPORT.md](../../docs/IMPORT.md).

### keygen / sign / verify

Digital signatures over APR content (industry-standard CMS/PKCS#7 + X.509).

```bash
# generate a self-signed signing cert (or use a CA-issued .pfx / PIV card)
apr keygen --name="Town of Bloomfield" --output=publisher.pfx --cert-out=publisher.cer

# publisher signs the template and binds the submission URL
apr sign permit.aprt --publisher --cert=publisher.pfx --url="https://gov/permit/submit"

# a filler signs the responses they completed
apr sign permit.aprf --fields=applicant_name,dob --cert=ada.pfx --id=ada

# verify, pinning the publisher's public cert as a trust anchor
apr verify permit.aprf --trust=publisher.cer
```

`verify` reports each signature as `trusted` / `self-signed` / `untrusted` /
`INVALID` and exits non-zero if signed content was altered. Full guide:
[docs/SIGNING.md](../../docs/SIGNING.md).

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

### Fill Out a Form Interactively

```bash
$ apr fill examples/simple-contact-form.aprt

Template: Simple Contact Form
Template ID: simple-contact-v1

=== Interactive Form Filling ===
(Press Enter to skip a field, Ctrl+C to cancel)

--- Contact Information ---

Name: John Doe
Email: john.doe@example.com
Message: I would like to inquire about your services.

Form filling complete!
Completion: 75.0%
Saved to: examples/simple-contact-form.aprf
```

### Fill From JSON File

```bash
$ apr fill examples/employment-application.apr \
    --json-file=examples/responses-employment-app.json \
    --output=filled-employment.aprf \
    --validate

Loading responses from: examples/responses-employment-app.json

Form filling complete!
Completion: 33.3%
Saved to: filled-employment.aprf

Validating...
✓ Validation passed
```

### Fill Non-Interactively

```bash
$ apr fill examples/simple-contact-form.aprt \
    --non-interactive \
    --set-prompt_001="Jane Smith" \
    --set-prompt_002="jane@example.com" \
    --filled-by="Jane Smith"

Filling form from command-line arguments

Form filling complete!
Completion: 50.0%
Saved to: examples/simple-contact-form.aprf
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

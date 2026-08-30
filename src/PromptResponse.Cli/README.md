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
apr validate examples/expense-report.aprt
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
apr info examples/contact-intake.aprt
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
apr fill examples/contact-intake.aprt

# Fill from JSON file
apr fill examples/contact-intake.aprt --json-file=examples/responses-simple-contact.json

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
$ apr validate examples/expense-report.aprt

Validating: examples/expense-report.aprt
✓ Validation passed
  Document type: Template
  Title: Employment Application Form
  Sections: 4
  Prompts: 18
```

### View Form Information

```bash
$ apr info examples/contact-intake.aprt

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
$ apr fill examples/contact-intake.aprt

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
Saved to: examples/contact-intake.aprf
```

### Fill From JSON File

```bash
$ apr fill examples/expense-report.aprt \
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
$ apr fill examples/contact-intake.aprt \
    --non-interactive \
    --set-prompt_001="Jane Smith" \
    --set-prompt_002="jane@example.com" \
    --filled-by="Jane Smith"

Filling form from command-line arguments

Form filling complete!
Completion: 50.0%
Saved to: examples/contact-intake.aprf
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

- See [APR_SPECIFICATION.md](../../docs/APR_SPECIFICATION.md) for the APR format specification
- See [USER_GUIDE.md](../../docs/USER_GUIDE.md) for general usage guidance
- See [DEVELOPMENT.md](../../docs/DEVELOPMENT.md) for development guide

## `review` — the receiving end

Every other command here serves whoever is authoring or filling a form. `review` serves
whoever is on the other side of a submission, holding a file somebody sent them.

They cannot use `validate` for that. The format refuses, absolutely, to reject anything a
person writes, so every submission that parses is valid — and validity therefore tells a
receiver nothing about whether their pipeline can read it. `review` answers the question
they actually have: **will a machine reading these fields get what the author intended?**

```bash
apr review submission.aprf                          # human-readable report
apr review submission.aprf --json                   # for a pipeline
apr review submission.aprf --template=form.aprt     # ...and is it even our form?
apr review submission.aprf --strict                 # flag advisories too
```

### Exit codes

The point of the command, because a routing script is usually what reads them.

| Code | Meaning |
|---|---|
| `0` | Safe to process automatically |
| `2` | Route to a person, a model, or back to the submitter |
| `1` | The file could not be read at all |

Advisories alone exit `0`: answering outside the suggested options is explicitly allowed
by the format and is often exactly right. `--strict` exits `2` for any finding.

### What it reports

Findings carry a **stable code** — route on that, never on the wording.

| Code | Severity | Meaning |
|---|---|---|
| `RULE_FAILED` | needs review | The form's own `exprValidation` rule says no. The strongest signal available: the author stating what they meant, not a guess. |
| `TYPE_MISMATCH` | needs review | The response does not parse as its declared type. |
| `PATTERN_MISMATCH` | needs review | The response does not match the author's `validationPattern`. |
| `OUTSIDE_SUGGESTED` | advisory | Not one of the offered options — allowed, and often right. |
| `OUTSIDE_BOUNDS` | advisory | Outside `min`/`max`. Bounds describe the control, not a limit. |
| `BLANK` | advisory | No answer. The format has no required responses; whether it matters is your policy. |

Fields the form itself is not asking for — hidden by `exprHidden`, or whose `exprExpected`
is false — are skipped entirely. A conditional branch that does not apply is not a gap,
and flagging it would bury the real findings.

### Is this even our form?

`--template=<file>` compares the submission against the template it claims to answer. A
submission can be valid, parse cleanly, pass every check above, and still answer different
questions.

| Code | Meaning |
|---|---|
| `PROMPT_RELABELLED` | Same id, different question. **The dangerous one:** a pipeline maps by id, so a truthful answer gets filed under a question nobody asked. |
| `PROMPT_MISSING` | The template asks it; the submission has no such field. |
| `PROMPT_ADDED` | The submitter is answering something never published. |
| `PROMPT_RETYPED` | The declared type differs from the template's. |
| `PROMPT_OPTIONS_CHANGED` | Chosen from a different shortlist than the one published. |
| `TEMPLATE_IDENTITY_MISMATCH` | `templateId`/`templateVersion` disagree. |

The comparison uses the same canonical bytes a publisher signature binds, so responses
never affect it — a faithfully filled form compares identical.

**If the template was signed, you do not need this.** A publisher signature already binds
the form definition and survives filling (spec §9), so verifying it proves the questions
are untouched without needing the original file at all. `--template` is for when there is
no signature but you do have the template. And note that `templateId` is self-asserted:
it catches the wrong form sent by accident, never one sent deliberately.

**Nothing this command reports means the document is invalid.** Every report says so
explicitly, in both output formats, so no downstream system reads "review required" as
"reject".

## Signature status, everywhere

`info`, `validate`, `stats` and `review` all report a document's signatures. You
do not have to already suspect a problem and reach for `verify` in order to be
told about one.

| State | What is said |
|---|---|
| unsigned | nothing at all |
| signed and valid | one line per signature, with the signer and how far they are trusted |
| signed and broken | which signature broke, and that the data is still readable |

**Unsigned is not a warning.** Signing is optional and most documents are never
signed; treating that as suspicious would make the common case look alarming and
teach people to dismiss the message, which disarms it for the case that matters.

**`validate` still exits 0 on a broken signature.** Specification §6.1 is
explicit that no validation error may arise from the state of a signature, and
that a validator rejecting a document because a signature is missing or invalid
is not implementing APR. The break is reported loudly and the command succeeds.

**`review` does exit 2**, because it answers a different question: not "is this
document valid" but "can a machine handle this submission unattended". Somebody
attested to the form and it no longer matches, so it needs a person — whatever
the answers themselves look like.

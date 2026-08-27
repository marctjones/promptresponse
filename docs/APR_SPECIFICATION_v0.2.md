> ## ⚠️ SUPERSEDED
>
> This draft is **superseded by [`APR_SPECIFICATION.md`](APR_SPECIFICATION.md)**
> and is retained for history only. It describes a format version (`"0.2"`) that no
> shipped file or validator has ever accepted, mandates extension-over-`documentType`
> precedence that v1.0 deliberately inverts, documents table column widths that no
> longer exist, and predates both the expression and signature profiles.
>
> Do not implement from this document. The carve-out that once kept Appendix B and C
> alive as the expression-language reference is gone: APR expressions are now CEL
> (v1.0 §8), so the language is defined by
> [cel-spec](https://github.com/cel-expr/cel-spec) and tested by its own conformance
> suite. Nothing in this file is normative.

# APR File Format Specification

**Version:** 0.2
**Status:** Draft
**Last Updated:** 2025-12-03

---

## 1. Overview

APR (Adaptive Prompt Response) is a JSON-based file format for portable, flexible forms. It separates form structure from presentation, allowing any compliant implementation to render and collect form data.

### 1.1 Design Principles

1. **Text-Only Responses** - All user data is stored as strings
2. **Hints, Not Constraints** - Type hints guide UI but never restrict input
3. **Any String is Valid** - The format itself never rejects user input
4. **Hierarchical Structure** - Sections nest to organize content logically
5. **No Presentation Data** - No fonts, colors, layouts, or styling
6. **Safe to Parse** - Pure data, no executable content
7. **Offline-First** - Files are self-contained; no network required to read or write
8. **Database-Ready** - JSON structure maps directly to database records
9. **Open and Portable** - No vendor lock-in; any conforming implementation can read/write
10. **Accessible by Design** - Structure supports screen readers and assistive technology

### 1.2 Presentation Independence

APR is a **data format**, not a presentation format. It describes *what* to collect, not *how* to display it.

**Any interface can render APR forms:**

| Interface Type | How APR Maps |
|----------------|--------------|
| **Graphical UI** | Labels become form labels, prompts become input fields |
| **Web Form** | Sections become fieldsets, hints guide input widgets |
| **Voice/IVR** | Labels are spoken prompts, responses are transcribed |
| **Text/CLI** | Labels are printed, user types responses |
| **Conversational AI** | Labels become questions, AI collects responses |
| **API/Programmatic** | Direct field access by ID, no UI at all |

**The specification suggests, never mandates:**

- `expectedDataType: "date"` → *suggests* a date picker in GUI, spoken date format in voice
- `suggestedValues` → *suggests* a dropdown or spoken menu options
- `helpText` → *suggests* additional guidance, rendered however appropriate
- `placeholder` → *suggests* example text, may be ignored entirely

An APR implementation could be:
- A desktop application with rich widgets
- A command-line tool that prints prompts and reads stdin
- A phone system that speaks questions and records answers
- A web form rendered in a browser
- A script that fills fields programmatically with no UI

All are valid. The format is agnostic to how—or whether—it gets displayed.

### 1.3 The Fundamental Rule: Any String is Valid

**This is the most important concept in the APR format.**

A response field can contain ANY printable string. The file format itself has no opinion about whether the response is "correct" or "properly formatted."

- If `expectedDataType` is `date` and the user enters `"April seventh, nineteen eighty-five"` → **Valid APR file**
- If `expectedDataType` is `email` and the user enters `"call me instead"` → **Valid APR file**
- If `expectedDataType` is `number` and the user enters `"about twelve"` → **Valid APR file**
- If a required field is left blank → **Valid APR file**

**The distinction is between file validity and workflow acceptance:**

| Concept | Responsibility | Example |
|---------|----------------|---------|
| **File Validity** | APR format | Is it valid JSON with the required structure? |
| **Workflow Acceptance** | Business process | Did the user fill it out correctly for our needs? |

A workflow (HR system, government agency, etc.) may reject a submitted form because:
- Required fields are empty
- Date formats don't match what they can parse
- The user put their dog's name instead of their own name
- The form is only half completed

But these are **workflow rejections**, not **file format errors**. The APR file itself is perfectly valid.

This is no different than paper forms: if you hand someone a form and they write "N/A" in every field, it's still a filled-out form. You might reject it, but the form exists and is readable.

**Why this matters:**

1. **Implementations are simple** - Parsers never need to validate response content
2. **Users are never blocked** - UI should never prevent saving because of "invalid" data
3. **Data is never lost** - Unusual input is preserved exactly as entered
4. **Workflows decide** - Business logic lives in the workflow, not the file format

### 1.4 File Extensions

| Extension | Meaning | Behavior |
|-----------|---------|----------|
| `.aprt` | Template | Blank form; responses are ignored when rendering |
| `.aprf` | Filled Form | Completed form; responses are displayed |
| `.apr` | Generic | Check `documentType` field to determine behavior |

**Rule:** File extension takes precedence over `documentType` field.

---

## 2. JSON Subset

APR uses a strict subset of JSON. This section defines exactly what JSON features are used.

### 2.1 Allowed JSON Value Types

| JSON Type | Used In APR | Notes |
|-----------|-------------|-------|
| `string` | Yes | All text values, all responses |
| `object` | Yes | Structured containers only |
| `array` | Yes | Lists of sections, prompts, etc. |
| `number` | No | **Not used** - all numbers are strings |
| `boolean` | No | **Not used** - use strings `"true"`/`"false"` |
| `null` | No | **Not used** - use empty string `""` |

**APR files contain only strings, objects, and arrays. No numbers, booleans, or nulls.**

### 2.2 Why Strings Only for Values

All user-facing values are strings because:
1. Responses like `"42"` and `"forty-two"` are both valid
2. No type coercion issues across implementations
3. Empty string `""` is unambiguous (vs `null` vs `0` vs `false`)
4. Preserves exactly what the user typed

### 2.3 String Escaping

Strings follow standard JSON escaping rules. Characters that must be escaped:

| Character | Escape Sequence | Notes |
|-----------|-----------------|-------|
| `"` | `\"` | Double quote |
| `\` | `\\` | Backslash |
| Newline | `\n` | Line feed (U+000A) |
| Carriage return | `\r` | (U+000D) |
| Tab | `\t` | (U+0009) |
| Unicode | `\uXXXX` | Any Unicode code point |

**Examples of valid response strings:**

```json
"response": "He said \"hello\""
"response": "Line 1\nLine 2"
"response": "Path: C:\\Users\\Name"
"response": "Price: $100"
"response": "Caf\u00e9"
```

Single quotes do NOT need escaping in JSON strings:

```json
"response": "It's working"
```

### 2.4 Object Structure Rules

Objects in APR have fixed schemas. Each object type has:
- **Required fields** that must be present
- **Optional fields** that may be omitted
- **No additional fields** beyond what's specified (implementations must ignore unknown fields)

### 2.5 Array Structure Rules

Arrays in APR contain:
- Zero or more elements of a single, defined type
- Elements are objects (never raw strings, numbers, or mixed types)

| Array | Contains |
|-------|----------|
| `sections` | Section objects |
| `prompts` | Prompt objects |
| `suggestedValues` | Strings (exception: array of strings) |
| `columns` | Column definition objects |
| `fixedRows` | Row definition objects |

### 2.6 Nesting Rules

```
Document (root object)
└── sections (array)
    └── Section (object)
        ├── sections (array) ─── recursive, unlimited depth
        │   └── Section (object)
        └── prompts (array)
            └── Prompt (object)
                └── hints (object)
                    └── tableDefinition (object) ── optional
                        ├── columns (array)
                        ├── fixedRows (array) ── OR
                        └── dynamicRows (object)
```

**Nesting constraints:**
- Sections can nest within sections (unlimited depth)
- Prompts cannot contain other prompts
- Prompts cannot contain sections
- Tables cannot nest within tables

### 2.7 Identifier Format

The `id` field for sections, prompts, and table columns/rows must follow these rules:

| Rule | Description |
|------|-------------|
| **No whitespace** | IDs must not contain spaces, tabs, or newlines |
| **ASCII only** | Use only ASCII letters, digits, and underscore |
| **Start with letter** | Must begin with a letter (a-z, A-Z) |
| **Case sensitive** | `Section_1` and `section_1` are different IDs |

**Valid ID pattern (regex):** `^[a-zA-Z][a-zA-Z0-9_]*$`

**Valid examples:**
```
section_001
prompt_email
employmentHistory
Q1_2024
```

**Invalid examples:**
```
001_section      (starts with number)
my section       (contains space)
prompt-email     (contains hyphen)
über_field       (contains non-ASCII)
```

### 2.8 Unicode and Character Encoding

APR files use UTF-8 encoding as specified by RFC 3629.

**Normative references:**

| Standard | Applies To |
|----------|------------|
| [RFC 8259](https://tools.ietf.org/html/rfc8259) | JSON syntax and string encoding |
| [RFC 3629](https://tools.ietf.org/html/rfc3629) | UTF-8 encoding |
| [Unicode Standard](https://www.unicode.org/versions/latest/) | Character definitions |

**Requirements:**

1. All strings MUST be valid UTF-8 (per RFC 3629)
2. JSON strings MUST follow RFC 8259 escaping rules
3. Implementations MUST reject ill-formed UTF-8 sequences

**Security considerations:**

For Unicode security issues (homoglyphs, bidirectional text attacks, invisible characters), implementations SHOULD follow:

- [Unicode Technical Report #36: Unicode Security Considerations](https://www.unicode.org/reports/tr36/)
- [Unicode Technical Standard #39: Unicode Security Mechanisms](https://www.unicode.org/reports/tr39/)

These documents define categories of confusable characters, recommended security profiles, and detection mechanisms. Implementations processing untrusted input SHOULD implement appropriate mitigations from these standards.

**Minimum requirements:**

1. Reject strings containing null (U+0000)
2. Reject unpaired UTF-16 surrogates (U+D800-U+DFFF) - these are invalid in UTF-8
3. Control characters U+0000-U+001F are prohibited except:
   - TAB (U+0009)
   - LF (U+000A)
   - CR (U+000D)

---

## 3. Document Structure

### 3.1 Root Object

```json
{
  "version": "0.2",
  "documentType": "template",
  "metadata": { },
  "sections": [ ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | string | Yes | Specification version (currently "0.2") |
| `documentType` | string | Yes | Either `"template"` or `"filledForm"` |
| `metadata` | object | Yes | Document metadata |
| `sections` | array | Yes | Array of Section objects |

### 3.2 Metadata Object

```json
{
  "title": "Employment Application",
  "description": "Standard employment application form",
  "templateId": "emp-app-2025",
  "version": {
    "major": "2",
    "minor": "1"
  },
  "created": "2025-01-15T00:00:00Z",
  "modified": "2025-01-15T00:00:00Z",
  "published": "2025-01-20T00:00:00Z",
  "publisher": {
    "name": "Acme Corporation HR",
    "email": "hr@acme.example.com",
    "url": "https://acme.example.com/hr"
  },
  "submitUrls": [
    {
      "url": "https://submissions.acme.example.com/hr/applications",
      "label": "Submit Application",
      "primary": "true"
    }
  ]
}
```

### 3.3 Core Metadata Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | Yes | Human-readable form title |
| `description` | string | No | Form description |
| `templateId` | string | No | Unique template identifier (stable across versions) |
| `created` | string | No | ISO 8601 timestamp when first created |
| `modified` | string | No | ISO 8601 timestamp when last modified |

### 3.4 Version Object

Templates use semantic versioning to track changes:

```json
{
  "version": {
    "major": "2",
    "minor": "1"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `major` | string | Yes | Major version (breaking changes to form structure) |
| `minor` | string | Yes | Minor version (non-breaking additions or fixes) |

**Version semantics:**

| Change Type | Version Bump | Examples |
|-------------|--------------|----------|
| Breaking | Major | Removing fields, renaming IDs, restructuring sections |
| Non-breaking | Minor | Adding new fields, fixing typos, adding help text |

**Version comparison:**
- `2.1` is newer than `2.0`
- `2.0` is newer than `1.9`
- Filled forms should record the template version they were created from

### 3.5 Publisher Object

The publisher identifies who created and published the template:

```json
{
  "publisher": {
    "name": "Acme Corporation HR",
    "email": "hr@acme.example.com",
    "url": "https://acme.example.com/hr",
    "organization": "Acme Corporation",
    "location": "New York, NY, USA"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Publisher name (person or department) |
| `email` | string | No | Contact email |
| `url` | string | No | Publisher's website or profile |
| `organization` | string | No | Organization name |
| `location` | string | No | Geographic location |

**Publisher Authentication:**

The publisher object provides attribution only. PromptResponse does not implement
cryptographic signing or signature verification of templates or filled forms;
recipients who need authenticated provenance should rely on out-of-band channels
(signed PDFs, e-sign workflows, certificate-pinned distribution, etc.).

### 3.6 Publication and Distribution Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `published` | string | No | ISO 8601 timestamp when publicly released |
| `submitUrls` | array | No | Recommended URLs for form submission (array of Submit URL objects) |

**Submit URLs:**

The `submitUrls` field specifies where completed forms should be submitted. It is an array of Submit URL objects, allowing forms to be submitted to multiple destinations:

```json
{
  "submitUrls": [
    {
      "url": "https://api.example.com/forms/submit",
      "label": "Submit to HR System",
      "primary": "true"
    },
    {
      "url": "mailto:hr-backup@example.com",
      "label": "Email to HR (backup)"
    }
  ]
}
```

**Submit URL Object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `url` | string | Yes | The submission endpoint URL |
| `label` | string | No | Human-readable name for this destination |
| `primary` | string | No | Set to `"true"` to indicate the preferred submission target |

**Supported URL schemes:**

| Scheme | Example | Behavior |
|--------|---------|----------|
| `https://` | `https://api.example.com/submit` | HTTPS POST with form data (JSON body) |
| `http://` | `http://internal.example.com/submit` | HTTP POST (insecure, warn user) |
| `mailto:` | `mailto:forms@example.com` | Open email client with form attached |

**Usage notes:**

- Applications SHOULD present all submit URLs to the user for selection
- If `primary` is set on one URL, applications MAY pre-select it as the default
- Users may choose to submit to multiple destinations, one destination, or save locally
- The `submitUrls` array is a recommendation only; users are never required to use it

### 3.7 Filled Form Additional Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `filledBy` | string | No | Name of person who filled the form |
| `filledDate` | string | No | ISO 8601 timestamp when filled |
| `templateVersion` | object | No | Version of template used (copy of `version` at fill time) |
| `submissions` | array | No | History of form submissions (array of Submission objects) |

### 3.8 Submission History

The `submissions` array tracks when and where a filled form was submitted, along with any response received from the destination:

```json
{
  "submissions": [
    {
      "submittedAt": "2025-01-22T14:30:00Z",
      "submittedTo": "https://api.example.com/forms/submit",
      "submittedToLabel": "Submit to HR System",
      "status": "success",
      "responseId": "APP-2025-00847",
      "responseMessage": "Application received. Your reference number is APP-2025-00847.",
      "responseData": {
        "confirmationNumber": "APP-2025-00847",
        "estimatedReviewDate": "2025-02-05",
        "trackingUrl": "https://status.example.com/track/APP-2025-00847"
      }
    },
    {
      "submittedAt": "2025-01-22T14:30:05Z",
      "submittedTo": "mailto:hr-backup@example.com",
      "submittedToLabel": "Email to HR (backup)",
      "status": "success",
      "responseMessage": "Email queued for delivery"
    }
  ]
}
```

**Submission Object:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `submittedAt` | string | Yes | ISO 8601 timestamp when submission occurred |
| `submittedTo` | string | Yes | The URL the form was submitted to |
| `submittedToLabel` | string | No | Human-readable name of the destination (from `submitUrls`) |
| `status` | string | Yes | Submission result: `"success"`, `"failed"`, or `"pending"` |
| `responseId` | string | No | Reference/confirmation number from the remote system |
| `responseMessage` | string | No | Human-readable response or error message |
| `responseData` | object | No | Additional structured data returned by the remote system |

**Status values:**

| Status | Meaning |
|--------|---------|
| `success` | Submission completed successfully |
| `failed` | Submission failed (see `responseMessage` for details) |
| `pending` | Submission in progress or awaiting confirmation |

**Usage notes:**

- Applications SHOULD append to `submissions` after each submission attempt
- Failed submissions SHOULD be recorded to help users troubleshoot
- The `responseId` field is intended for reference numbers, order numbers, confirmation codes, etc.
- The `responseData` object can store any structured data the remote system returns
- Applications MAY display submission history to help users track their forms

---

## 4. Sections

Sections organize prompts into logical groups. Sections can nest within sections to any depth.

```json
{
  "id": "section_personal",
  "title": "Personal Information",
  "description": "Basic contact details",
  "sections": [ ],
  "prompts": [ ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier within document |
| `title` | string | Yes | Section heading |
| `description` | string | No | Section description |
| `sections` | array | No | Nested Section objects |
| `prompts` | array | No | Array of Prompt objects |

**Rules:**
- A section must contain at least one prompt or one nested section
- Section IDs must be unique across the entire document
- Nesting depth is unlimited

---

## 5. Prompts

Prompts are individual form fields that collect user input.

```json
{
  "id": "prompt_email",
  "label": "Email Address",
  "response": "",
  "hints": { }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier within document |
| `label` | string | Yes | User-visible field label |
| `response` | string | Yes | User's response (empty string if blank) |
| `hints` | object | No | UI hints (see section 5) |

**Rules:**
- Prompt IDs must be unique across the entire document
- Response is ALWAYS a string, never null, number, boolean, or object
- For templates (`.aprt`), response should be empty string `""`
- For filled forms (`.aprf`), response contains user data

---

## 6. Hints

Hints provide guidance to the UI. All hints are optional and advisory only.

### 6.1 Naming Convention

All hint fields use a **category prefix** to indicate their purpose:

| Prefix | Purpose | Examples |
|--------|---------|----------|
| `type` | Data type (no prefix, special) | `type` |
| `input*` | Input assistance | `inputPlaceholder`, `inputHelpText` |
| `behavior*` | Field behavior | `behaviorExpected`, `behaviorReadOnly` |
| `display*` | Visual formatting | `displayPrefix`, `displayMask` |
| `layout*` | Positioning | `layoutWidth`, `layoutGroup` |
| `suggest*` | Value suggestions | `suggestValues`, `suggestValuesUrl` |

This convention makes it easy to identify what category a hint belongs to and to search for all hints of a particular type.

### 6.2 Complete Example

```json
{
  "id": "prompt_salary",
  "label": "Annual Salary",
  "response": "",
  "hints": {
    "type": "currency",
    "inputPlaceholder": "75000.00",
    "inputHelpText": "Enter your annual salary before taxes",
    "inputDefaultValue": "0.00",
    "inputValidationPattern": "^\\d+(\\.\\d{2})?$",
    "behaviorExpected": true,
    "behaviorReadOnly": false,
    "displayPrefix": "$",
    "displaySuffix": " USD",
    "layoutWidth": "50%",
    "layoutGroup": "compensation_row",
    "layoutOrder": 1
  }
}
```

### 6.3 Type Field

| Field | Type | Description |
|-------|------|-------------|
| `type` | string | Data type hint (see section 7) |

The `type` field (formerly `expectedDataType`) suggests how the UI should render the field. It has no prefix because it's the most fundamental hint.

```json
{
  "hints": {
    "type": "email"
  }
}
```

### 6.4 Input Hints (`input*`)

Input hints help users enter data correctly.

| Field | Type | Description |
|-------|------|-------------|
| `inputPlaceholder` | string | Example text shown in empty field |
| `inputHelpText` | string | Additional guidance shown to user |
| `inputDefaultValue` | string | Value to pre-populate when creating new filled form |
| `inputValidationPattern` | string | Regex pattern (advisory only) |

```json
{
  "hints": {
    "type": "email",
    "inputPlaceholder": "you@example.com",
    "inputHelpText": "Enter your primary email address",
    "inputValidationPattern": "^[^@]+@[^@]+\\.[^@]+$"
  }
}
```

### 6.5 Behavior Hints (`behavior*`)

Behavior hints control how the field behaves.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `behaviorExpected` | boolean | `false` | Field is expected to be filled (shows indicator) |
| `behaviorReadOnly` | boolean | `false` | Field is display-only (pre-populated by system) |
| `behaviorHidden` | boolean | `false` | Field exists in data but should not be displayed |

**Important:** These are all advisory hints:

- `behaviorExpected: true` does NOT make the file invalid if left empty - it's a hint to show an indicator (e.g., asterisk)
- `behaviorReadOnly: true` suggests the UI should prevent editing, but the response can still be modified programmatically
- `behaviorHidden: true` suggests the field shouldn't be shown to users, but data should be preserved

**Why `behaviorExpected` instead of `behaviorRequired`?**

The word "required" implies validation and enforcement. But APR hints are always advisory - the file is valid regardless of whether expected fields are filled. Using `expected` makes it clear this is what the form designer *expects*, not what the format *requires*.

**Example - Expected field:**
```json
{
  "id": "prompt_name",
  "label": "Full Legal Name",
  "hints": {
    "behaviorExpected": true,
    "inputHelpText": "Please enter your name as it appears on government ID"
  }
}
```

**Example - Read-only field:**
```json
{
  "id": "prompt_form_id",
  "label": "Form ID",
  "response": "APP-2025-001234",
  "hints": {
    "behaviorReadOnly": true,
    "inputHelpText": "Automatically assigned - do not modify"
  }
}
```

**Example - Hidden field:**
```json
{
  "id": "prompt_internal_tracking",
  "label": "Internal Tracking Code",
  "response": "TRK-XYZ-789",
  "hints": {
    "behaviorHidden": true
  }
}
```

### 6.6 Display Hints (`display*`)

Display hints control how values are formatted and presented.

| Field | Type | Description |
|-------|------|-------------|
| `displayPrefix` | string | Text displayed before the input (e.g., "$") |
| `displaySuffix` | string | Text displayed after the input (e.g., "%", " lbs") |
| `displayMask` | string | Input mask pattern for formatted entry |
| `displayFormat` | string | How to format the value for display |

**Prefix and Suffix:**

```json
{
  "id": "prompt_price",
  "label": "Price",
  "hints": {
    "type": "currency",
    "displayPrefix": "$",
    "inputPlaceholder": "0.00"
  }
}
```

```json
{
  "id": "prompt_weight",
  "label": "Weight",
  "hints": {
    "type": "number",
    "displaySuffix": " lbs"
  }
}
```

**Input Mask:**

The `displayMask` field suggests an input pattern. Use `#` for digits and `A` for letters:

| Mask | Example Input | Use Case |
|------|---------------|----------|
| `(###) ###-####` | (555) 123-4567 | US phone |
| `###-##-####` | 123-45-6789 | SSN |
| `##-#######` | 12-3456789 | EIN |
| `####-####-####-####` | 1234-5678-9012-3456 | Credit card |
| `AAAAA-####` | ABCDE-1234 | Product code |

```json
{
  "id": "prompt_phone",
  "label": "Phone Number",
  "hints": {
    "type": "phone",
    "displayMask": "(###) ###-####",
    "inputPlaceholder": "(555) 123-4567"
  }
}
```

Implementations MAY auto-format as the user types, or MAY ignore the mask entirely.

**Display Format:**

The `displayFormat` field suggests how to format values for display (distinct from `type` which affects input):

| Format | Description | Example |
|--------|-------------|---------|
| `uppercase` | Display in uppercase | "JOHN DOE" |
| `lowercase` | Display in lowercase | "john doe" |
| `titlecase` | Capitalize each word | "John Doe" |
| `date-long` | Long date format | "January 15, 2025" |
| `date-short` | Short date format | "1/15/25" |
| `currency` | Currency format | "$1,234.56" |
| `percent` | Percentage format | "75.5%" |

### 6.7 Suggestion Hints (`suggest*`)

Suggestion hints provide value options to the user.

| Field | Type | Description |
|-------|------|-------------|
| `suggestValues` | array | Static list of suggested values |
| `suggestValuesUrl` | string | URL to fetch suggestions from |
| `suggestAllowOther` | boolean | Whether values not in list are allowed (default: `true`) |

**Static Suggested Values:**
```json
{
  "hints": {
    "suggestValues": ["Red", "Green", "Blue"],
    "suggestAllowOther": false
  }
}
```

**Dynamic Suggested Values:**

The `suggestValuesUrl` field allows fetching suggestions from a remote endpoint:

```json
{
  "hints": {
    "suggestValuesUrl": "https://api.example.com/countries",
    "inputHelpText": "Select a country (requires internet connection)"
  }
}
```

**URL Response Format:**

The endpoint MUST return a JSON array of strings:
```json
["United States", "Canada", "Mexico", "United Kingdom", "Germany", "France"]
```

Or an array of objects with `value` and optional `label`:
```json
[
  { "value": "US", "label": "United States" },
  { "value": "CA", "label": "Canada" },
  { "value": "MX", "label": "Mexico" }
]
```

**Important considerations:**
- Breaks the "offline-first" principle - implementations SHOULD cache results
- Implementations MAY refuse to fetch from untrusted URLs
- If fetch fails, implementations SHOULD fall back to free text entry
- `suggestValues` (static) takes precedence if both are specified

### 6.8 Layout Hints (`layout*`)

Layout hints suggest how prompts should be arranged. These are advisory - implementations may render all prompts vertically.

| Field | Type | Description |
|-------|------|-------------|
| `layoutWidth` | string | Width hint for the prompt |
| `layoutGroup` | string | Group ID for horizontal grouping |
| `layoutOrder` | number | Order within group (1, 2, 3...) |

**Width Hint:**

The `layoutWidth` field suggests how much horizontal space a prompt should occupy:

| Format | Example | Meaning |
|--------|---------|---------|
| Percentage | `"50%"` | Half of available width |
| Pixels | `"200px"` | Fixed pixel width |
| Relative | `"*"` or `"2*"` | Relative to siblings |

```json
{
  "id": "prompt_city",
  "label": "City",
  "hints": { "layoutWidth": "50%" }
},
{
  "id": "prompt_state",
  "label": "State",
  "hints": { "layoutWidth": "20%" }
},
{
  "id": "prompt_zip",
  "label": "ZIP",
  "hints": { "layoutWidth": "30%" }
}
```

**Group Hint:**

For explicit horizontal grouping, use `layoutGroup` and `layoutOrder`:

```json
{
  "id": "prompt_city",
  "label": "City",
  "hints": { "layoutGroup": "address_row_2", "layoutOrder": 1, "layoutWidth": "50%" }
},
{
  "id": "prompt_state",
  "label": "State",
  "hints": { "layoutGroup": "address_row_2", "layoutOrder": 2, "layoutWidth": "25%" }
},
{
  "id": "prompt_zip",
  "label": "ZIP",
  "hints": { "layoutGroup": "address_row_2", "layoutOrder": 3, "layoutWidth": "25%" }
}
```

Prompts with the same `layoutGroup` value are rendered on the same row, ordered by `layoutOrder`.

### 6.9 Type-Specific Hint Fields

Some `type` values support additional hint fields. These use the `type*` prefix:

**For `range` type:**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `typeRangeMin` | string | "0" | Minimum value |
| `typeRangeMax` | string | "100" | Maximum value |
| `typeRangeStep` | string | "1" | Step increment |

```json
{
  "hints": {
    "type": "range",
    "typeRangeMin": "0",
    "typeRangeMax": "10",
    "typeRangeStep": "1",
    "inputHelpText": "Rate from 0 to 10"
  }
}
```

**For `table` type:**

| Field | Type | Description |
|-------|------|-------------|
| `typeTableDefinition` | object | Table structure (see section 8) |

```json
{
  "hints": {
    "type": "table",
    "typeTableDefinition": {
      "columns": [...],
      "dynamicRows": { "minRows": 1, "maxRows": 10 }
    }
  }
}
```

---

## 7. Data Types

The `type` field suggests how the UI should render the field.

### 7.1 Core Types (Recommended for all implementations)

| Type | Description | Example Response |
|------|-------------|------------------|
| `text` | Single-line text | `"John Doe"` |
| `multiline` | Multi-line text | `"Line 1\nLine 2"` |
| `number` | Numeric value | `"42"` or `"3.14"` |
| `boolean` | Yes/No choice | `"Yes"` or `"No"` |
| `date` | Date | `"2025-01-15"` |
| `time` | Time | `"14:30"` |
| `datetime` | Date and time | `"2025-01-15T14:30:00"` |
| `email` | Email address | `"user@example.com"` |
| `phone` | Phone number | `"+1-555-0100"` |
| `url` | Web URL | `"https://example.com"` |
| `currency` | Monetary amount | `"99.99"` |

### 7.2 Extended Types (Optional for implementations)

| Type | Description | Example Response |
|------|-------------|------------------|
| `password` | Masked text input | `"secret123"` |
| `color` | Color value | `"#FF5733"` |
| `range` | Slider input | `"75"` |
| `file` | File path/reference | `"/path/to/file.pdf"` |
| `signature` | Styled signature text | `"John M. Doe"` |
| `multichoice` | Multiple selections | `"Option A, Option B"` |
| `table` | Tabular data | See section 8 |

### 7.3 Formatted Text Types (Optional)

| Type | Description | Example Response |
|------|-------------|------------------|
| `ssn` | Social Security Number | `"123-45-6789"` |
| `ein` | Employer ID Number | `"12-3456789"` |
| `zipcode` | ZIP/Postal code | `"12345"` or `"12345-6789"` |
| `creditcard` | Credit card number | `"4111-1111-1111-1111"` |

### 7.4 Type Handling Rules

1. **Unknown types** → Render as `text`
2. **Missing `type`** → Default to `text`
3. **Any response is valid** → Types never restrict what the user can enter
4. **All responses are strings** → Even numbers are stored as `"42"` not `42`

### 7.5 Types Are Hints, Not Validators

The `type` field tells the UI how to render the input (date picker, number spinner, etc.) and tells the user what kind of data the form designer expects.

**It does NOT:**
- Prevent the user from entering any string they want
- Make the file invalid if the response doesn't match the type
- Require implementations to validate responses against the type

**Examples of valid responses regardless of `type`:**

| type | Valid Responses (all of these are valid) |
|------|------------------------------------------|
| `date` | `"2025-01-15"`, `"January 15th"`, `"next Tuesday"`, `"TBD"`, `""` |
| `number` | `"42"`, `"forty-two"`, `"~50"`, `"N/A"`, `""` |
| `email` | `"user@example.com"`, `"none"`, `"see attached"`, `""` |
| `phone` | `"+1-555-0100"`, `"unlisted"`, `"ask my assistant"`, `""` |
| `boolean` | `"Yes"`, `"No"`, `"Maybe"`, `"It's complicated"`, `""` |

The form designer's intent was one thing. What the user actually wrote is another. The APR format preserves exactly what the user wrote. The workflow that processes the form decides whether to accept it.

---

## 8. Tables

Tables allow structured data entry with rows and columns.

### 8.1 Table Definition

```json
{
  "type": "table",
  "typeTableDefinition": {
    "columns": [ ],
    "fixedRows": [ ],
    "dynamicRows": { }
  }
}
```

A table has EITHER `fixedRows` OR `dynamicRows`, never both.

### 8.2 Columns

```json
{
  "columns": [
    { "id": "item", "label": "Item", "type": "text" },
    { "id": "qty", "label": "Quantity", "type": "number", "width": "60px" },
    { "id": "price", "label": "Price", "type": "currency", "placeholder": "0.00" }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Column identifier |
| `label` | string | Yes | Column header text |
| `type` | string | No | Cell type: `text`, `number`, `currency`, `date`, `boolean` |
| `placeholder` | string | No | Placeholder for cells |
| `suggestedValues` | array | No | List of suggested values for cells (renders as dropdown) |
| `helpText` | string | No | Help text for the column (shown in header tooltip or similar) |
| `width` | string | No | Width hint for the column (see below) |

**Display Hints:**

Columns support optional display hints that implementations MAY use to improve rendering. These hints are advisory and implementations MUST function correctly if they ignore them.

**Width Hint:**

The `width` field suggests column width. Implementations MAY use this as a hint but are free to adjust based on available space:

| Format | Example | Meaning |
|--------|---------|---------|
| Pixels | `"100px"` | Fixed pixel width |
| Percentage | `"25%"` | Percentage of table width |
| Relative | `"*"` or `"2*"` | Relative to other columns |

```json
{
  "columns": [
    { "id": "description", "label": "Description", "width": "40%" },
    { "id": "qty", "label": "Qty", "width": "60px" },
    { "id": "notes", "label": "Notes", "width": "*" }
  ]
}
```

**Column-Level Suggested Values:**

For columns with a limited set of valid options, `suggestedValues` can provide a dropdown:

```json
{
  "id": "state",
  "label": "State",
  "type": "text",
  "suggestedValues": ["AL", "AK", "AZ", "AR", "CA", "..."]
}
```

### 8.3 Fixed Rows

Fixed tables have predefined, immutable rows:

```json
{
  "fixedRows": [
    { "id": "2024", "label": "Year 2024" },
    { "id": "2023", "label": "Year 2023" }
  ]
}
```

**Response format (object keyed by row ID):**

```json
{
  "2024": { "item": "Salary", "qty": "1", "price": "75000" },
  "2023": { "item": "Salary", "qty": "1", "price": "72000" }
}
```

### 8.4 Dynamic Rows

Dynamic tables allow users to add/remove rows:

```json
{
  "dynamicRows": {
    "minRows": 1,
    "maxRows": 50,
    "rowLabel": "Item"
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `minRows` | number | 0 | Minimum rows required |
| `maxRows` | number | 100 | Maximum rows allowed |
| `rowLabel` | string | `"Row"` | Label prefix for auto-generated row labels |

**Row Label:**

The `rowLabel` field provides a prefix for generating row labels in the UI (e.g., "Item 1", "Item 2" or "Address 1", "Address 2"):

```json
{
  "dynamicRows": {
    "minRows": 1,
    "maxRows": 20,
    "rowLabel": "Line Item"
  }
}
```

This is a display hint only. Implementations MAY ignore it or use a different labeling scheme.

**Response format (array of objects):**

```json
[
  { "item": "Widget A", "qty": "10", "price": "25.00" },
  { "item": "Widget B", "qty": "5", "price": "42.50" }
]
```

### 8.5 Table Response Storage

Table responses are stored as **JSON strings** in the `response` field:

```json
{
  "id": "prompt_items",
  "label": "Order Items",
  "response": "[{\"item\":\"Widget\",\"qty\":\"10\"}]",
  "hints": {
    "type": "table",
    "typeTableDefinition": { ... }
  }
}
```

---

## 9. Rendering Behavior by Data Type

This section specifies how compliant implementations SHOULD render each type.

### 9.1 Input Controls

| Type | Recommended Control |
|------|---------------------|
| `text` | Single-line text input |
| `multiline` | Multi-line textarea |
| `number` | Numeric input (allow decimals) |
| `currency` | Numeric input with 2 decimal places |
| `boolean` | Radio buttons or toggle |
| `date` | Date picker or text input |
| `time` | Time picker or text input |
| `datetime` | Combined date/time picker |
| `email` | Text input (may validate format) |
| `phone` | Text input (may format) |
| `url` | Text input (may validate format) |
| `password` | Masked text input |
| `color` | Color picker |
| `range` | Slider |
| `file` | File browser |
| `signature` | Text input (may style as cursive) |
| `multichoice` | Checkboxes for each `suggestValues` item |

### 9.2 Suggested Values Behavior

When `suggestValues` is present:

| Type | Behavior |
|------|----------|
| `boolean` | Use values as radio button labels (e.g., `["Yes", "No"]`) |
| `multichoice` | Render as checkboxes; response is comma-separated |
| All other types | Render as dropdown OR autocomplete; user may type custom value |

---

## 10. Validation

### 10.1 File Structure Validation (Required)

These validations determine if a file is a valid APR document:

| Check | Requirement |
|-------|-------------|
| JSON syntax | Must be valid JSON |
| `version` | Must be present, must be a recognized version |
| `documentType` | Must be `"template"` or `"filledForm"` |
| `sections` | Must be an array (may be empty) |
| `id` uniqueness | All IDs must be unique within the document |
| `response` type | Must be a string (including empty string `""`) |

If any of these fail, the file is not a valid APR document.

### 10.2 Response String Validation (Required)

Responses MUST be valid, printable strings:

| Requirement | Description |
|-------------|-------------|
| Valid UTF-8 | Must be properly encoded UTF-8 |
| Printable characters | Must contain only printable characters |
| No control characters | No ASCII 0-31 except TAB (9), LF (10), CR (13) |
| No null bytes | Must not contain null (0x00) |

Allowed whitespace in responses:
- Space (0x20)
- Tab (0x09)
- Newline (0x0A) - for multiline responses
- Carriage return (0x0D) - for Windows line endings

**Whitespace trimming:** Implementations MAY trim leading and trailing whitespace from responses, even if this results in an empty string. This is always permitted but never required.

If a response contains invalid or non-printable characters, the file is invalid.

### 10.3 Response Semantic Validation (Never Required)

Implementations MUST NOT reject a file based on response *meaning*:

- Response doesn't match `type` → **Still valid**
- Response doesn't match `inputValidationPattern` → **Still valid**
- Response is empty → **Still valid**
- Response contains "wrong" information → **Still valid**

**The semantic content of responses is NEVER validated by the file format.**

The format validates that responses are proper strings. It never validates what those strings mean.

### 10.4 Advisory Feedback (Optional)

Implementations MAY show advisory feedback to help users:

- Highlight fields where response doesn't match `type`
- Show warning icon if `inputValidationPattern` doesn't match
- Indicate which fields have `behaviorExpected: true`

But this feedback must NEVER:
- Prevent the user from saving the file
- Prevent the user from entering any text they want
- Mark the file as "invalid"

Advisory feedback is a courtesy to the user, not a file format requirement.

---

## 11. Extensibility

### 11.1 Unknown Fields

Implementations MUST:
- Ignore unknown fields when reading
- Preserve unknown fields when writing (round-trip safe)

### 11.2 Unknown Data Types

Implementations MUST:
- Treat unknown `type` values as `text`
- Never fail to load a document due to unknown type

### 11.3 Version Compatibility

**Version format:** `major.minor` (e.g., "1.1")

| Change Type | Version Bump | Examples |
|-------------|--------------|----------|
| Breaking change | Major | Removing required field, changing structure |
| New optional feature | Minor | Adding new optional field, new data type |
| Clarification | None | Fixing typos, adding examples |

**Compatibility rules:**

1. Implementations MUST reject documents with unrecognized major version
2. Implementations SHOULD accept documents with higher minor version (ignore new fields)
3. Implementations MUST preserve the original `version` field when saving

**Example:** An implementation supporting "1.1" should:
- Accept "1.0", "1.1" documents
- Accept "1.2" documents (ignoring unknown features)
- Reject "2.0" documents

---

## 12. Safety and Security

### 12.1 Safe to Parse

APR files are **pure data**. They contain no executable content:

- No scripts or macros
- No external references that auto-load
- No embedded objects that execute
- No formulas or calculations

An APR file cannot harm a system by being opened. The worst case is malformed JSON that fails to parse.

### 12.2 Safe to Display

Rendering APR forms should not introduce vulnerabilities:

- Responses should be escaped before display in HTML/XML contexts
- `label`, `helpText`, and other template content should be treated as untrusted
- URLs in `url` type fields should not auto-navigate without user action

### 12.3 Data Sensitivity

APR files may contain sensitive personal information. Implementations SHOULD:

- Not transmit file contents without explicit user action
- Support local-only storage as the default
- Warn before submitting forms to remote endpoints
- Allow users to review all data before submission

---

## 13. Accessibility

APR's structure is designed to support accessible form rendering.

### 13.1 Structural Accessibility

| APR Element | Accessibility Mapping |
|-------------|----------------------|
| `section.title` | Heading (h1-h6 based on nesting depth) |
| `section.description` | Descriptive text, group description |
| `prompt.label` | Form field label (associated with input) |
| `prompt.hints.inputHelpText` | Field description/instructions |
| `prompt.id` | Unique identifier for label association |

### 13.2 Implementation Requirements for Accessibility

Implementations targeting WCAG 2.1 Level AA SHOULD:

1. Associate each `label` with its input using `for`/`id` or ARIA
2. Expose `inputHelpText` to assistive technology
3. Provide keyboard navigation between fields
4. Maintain logical reading order matching section hierarchy
5. Announce section titles when navigating between sections
6. Indicate expected fields (`behaviorExpected: true`)

### 13.3 Template Design for Accessibility

Templates SHOULD:

1. Use clear, descriptive labels (not "Field 1", "Field 2")
2. Provide `inputHelpText` for fields that need explanation
3. Use `section.title` to group related fields
4. Avoid relying solely on `inputPlaceholder` for instructions

---

## 14. Programmatic Access

APR's JSON structure enables easy programmatic manipulation.

### 14.1 Reading Forms

```python
import json

with open('form.aprf', 'r') as f:
    doc = json.load(f)

# Access metadata
title = doc['metadata']['title']

# Iterate all responses
def get_responses(sections):
    responses = {}
    for section in sections:
        for prompt in section.get('prompts', []):
            responses[prompt['id']] = prompt['response']
        responses.update(get_responses(section.get('sections', [])))
    return responses

data = get_responses(doc['sections'])
```

### 14.2 Writing Forms

```python
import json

# Create a filled form from template
with open('template.aprt', 'r') as f:
    doc = json.load(f)

doc['documentType'] = 'filledForm'
doc['metadata']['filledBy'] = 'Script'
doc['metadata']['filledDate'] = '2025-01-15T12:00:00Z'

# Set responses by ID
def set_response(sections, prompt_id, value):
    for section in sections:
        for prompt in section.get('prompts', []):
            if prompt['id'] == prompt_id:
                prompt['response'] = str(value)
                return True
        if set_response(section.get('sections', []), prompt_id, value):
            return True
    return False

set_response(doc['sections'], 'prompt_name', 'John Doe')
set_response(doc['sections'], 'prompt_email', 'john@example.com')

with open('filled.aprf', 'w') as f:
    json.dump(doc, f, indent=2)
```

### 14.3 Database Import

APR's flat response structure maps directly to database rows:

```sql
-- Extract responses to a flat table
CREATE TABLE form_responses (
    form_id TEXT,
    prompt_id TEXT,
    response TEXT,
    PRIMARY KEY (form_id, prompt_id)
);
```

---

## 15. Template and Filled Form Workflow

APR distinguishes between **templates** (blank forms) and **filled forms** (completed forms with user data). This distinction is fundamental to the format.

### 15.1 The Core Distinction

| Aspect | Template (.aprt) | Filled Form (.aprf) |
|--------|------------------|---------------------|
| **Purpose** | Define what to collect | Store collected data |
| **`documentType`** | `"template"` | `"filledForm"` |
| **`response` fields** | Empty strings | User data |
| **Who creates it** | Form designer | Form filler |
| **Typical action** | Open and fill out | Open and review/process |
| **Modification** | Edit structure and labels | Edit responses only |

### 15.2 Analogy: Paper Forms

Think of it like paper forms:

- **Template** = A blank form you photocopy and hand out
- **Filled Form** = A completed form someone hands back to you

The blank form defines the questions. The filled form contains answers. You don't write answers on the master copy.

### 15.3 Document Lifecycle

```
Template Creation        Form Filling           Processing
─────────────────       ──────────────        ────────────

[Create .aprt] ───────► [Open .aprt] ────────► [Read .aprf]
      │                       │                      │
      │                       ▼                      ▼
      │                 [Fill responses]      [Import to DB]
      │                       │                      │
      │                       ▼                      ▼
      │                 [Save as .aprf] ───► [Archive/Delete]
      │
      ▼
[Publish/Share]
```

### 15.4 Template (.aprt) Rules

- `documentType` MUST be `"template"`
- All `response` fields SHOULD be empty strings
- Contains form structure: sections, prompts, labels, hints
- Intended to be filled out, not edited structurally by end users
- When opened for filling, implementation creates a new filled form

### 15.5 Filled Form (.aprf) Rules

- `documentType` MUST be `"filledForm"`
- `response` fields contain user data
- Structure matches original template (sections, prompts, labels)
- Can be opened, reviewed, edited, and re-saved

### 15.6 Conversion Rules

**Template → Filled Form:**
1. Change `documentType` to `"filledForm"`
2. Add `filledBy` and `filledDate` to metadata
3. User enters responses
4. Save with `.aprf` extension

**Filled Form → Template (data stripping):**
1. Change `documentType` to `"template"`
2. Clear all `response` fields to empty string
3. Remove `filledBy`, `filledDate` from metadata
4. Remove any `formSignatures`
5. Save with `.aprt` extension

---

## 16. Response Identifiers

### 16.1 Response ID Purpose

Each prompt/response pair can have an optional `responseId` that uniquely identifies that specific response instance. This enables:

- Attachments to reference specific responses they relate to
- External systems to track individual responses
- Audit trails linking actions to specific answers

### 16.2 Response ID Field

```json
{
  "id": "prompt_passport",
  "label": "Passport Number",
  "response": "AB1234567",
  "responseId": "resp_a1b2c3d4"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `responseId` | string | No | Unique identifier for this response instance |

### 16.3 Response ID Generation

**Response IDs are optional but encouraged.** They serve different purposes in templates vs filled forms:

| Context | Who Generates | Purpose |
|---------|---------------|---------|
| Template (.aprt) | Template author | Pre-assign IDs for known attachment points |
| Filled Form (.aprf) | Application or user | Identify responses for attachment annotations |

**Generation rules:**

1. Template authors MAY pre-assign `responseId` values to prompts
2. If not present in template, applications SHOULD auto-generate when creating filled forms
3. Users MAY manually assign or modify `responseId` values
4. Format: Same rules as `id` field (ASCII, no spaces, starts with letter)
5. Recommended format: `resp_` prefix + random alphanumeric (e.g., `resp_x7k9m2p4`)

**Uniqueness:**

- `responseId` MUST be unique within the document
- `responseId` is separate from `id` (prompt ID identifies the field; response ID identifies the answer instance)

### 16.4 Response ID vs Prompt ID

| Field | Identifies | Stable Across | Example |
|-------|------------|---------------|---------|
| `id` | The prompt/field definition | All copies of template | `prompt_passport` |
| `responseId` | This specific response instance | This filled form only | `resp_a1b2c3d4` |

**Example:** An employment application template has `id: "prompt_ssn"`. When Alice fills it out, her response gets `responseId: "resp_alice_ssn_001"`. When Bob fills out the same template, his response gets `responseId: "resp_bob_ssn_002"`. Both reference the same prompt (`prompt_ssn`) but are distinct response instances.

---

## 17. Localization

APR supports localized versions of all user-facing text, allowing forms to be displayed in multiple languages.

### 17.1 Localization Structure

Localizations are stored at the document level and provide alternative text for any user-facing field:

```json
{
  "version": "0.2",
  "documentType": "template",
  "metadata": { },
  "sections": [ ],
  "localizations": {
    "es": {
      "metadata": {
        "title": "Solicitud de Empleo",
        "description": "Formulario estándar de solicitud de empleo"
      },
      "translator": {
        "name": "Maria García",
        "email": "maria@translations.example.com",
        "organization": "Professional Translations Inc."
      },
      "translatedAt": "2025-01-20T00:00:00Z",
      "sections": {
        "section_personal": {
          "title": "Información Personal",
          "description": "Datos de contacto básicos"
        }
      },
      "prompts": {
        "prompt_name": {
          "label": "Nombre Completo",
          "hints": {
            "inputPlaceholder": "Juan Pérez",
            "inputHelpText": "Ingrese su nombre como aparece en su identificación"
          }
        },
        "prompt_email": {
          "label": "Correo Electrónico",
          "hints": {
            "inputPlaceholder": "usuario@ejemplo.com"
          }
        }
      }
    },
    "fr": {
      "metadata": {
        "title": "Demande d'Emploi"
      },
      "translator": {
        "name": "Jean Dupont"
      },
      "translatedAt": "2025-01-22T00:00:00Z",
      "sections": { },
      "prompts": { }
    }
  }
}
```

### 17.2 Localizations Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `localizations` | object | No | Map of language codes to Localization objects |

Keys are [BCP 47](https://www.rfc-editor.org/info/bcp47) language tags (e.g., `"en"`, `"es"`, `"fr"`, `"zh-Hans"`, `"pt-BR"`).

### 17.3 Localization Object

Each localization contains:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `metadata` | object | No | Localized metadata fields |
| `translator` | object | No | Information about who created this translation |
| `translatedAt` | string | No | ISO 8601 timestamp when translation was created |
| `sections` | object | No | Map of section IDs to localized section fields |
| `prompts` | object | No | Map of prompt IDs to localized prompt fields |

### 17.4 Translator Object

```json
{
  "translator": {
    "name": "Maria García",
    "email": "maria@translations.example.com",
    "organization": "Professional Translations Inc.",
    "url": "https://translations.example.com"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Translator's name |
| `email` | string | No | Contact email |
| `organization` | string | No | Translation company or organization |
| `url` | string | No | Translator's website or profile |

### 17.5 Localizable Fields

The following fields can be localized:

**Metadata:**
- `title`
- `description`

**Sections:**
- `title`
- `description`

**Prompts:**
- `label`
- `hints.inputPlaceholder`
- `hints.inputHelpText`
- `hints.suggestValues` (array of localized values)

**Table Columns:**
- `label`
- `placeholder`
- `helpText`
- `suggestedValues`

**Table Rows (fixed):**
- `label`

### 17.6 Localization Resolution

When rendering, implementations SHOULD:

1. Determine the user's preferred language
2. Look up the localization for that language
3. For each field, use localized value if present, otherwise fall back to base value
4. If no matching localization exists, use base values

**Example resolution for `prompt_name.label` with user language `es`:**

```
1. Check localizations.es.prompts.prompt_name.label
2. If found: use "Nombre Completo"
3. If not found: use base sections[].prompts[].label = "Full Name"
```

### 17.7 Translator Attribution

Translations may carry translator attribution metadata:

```json
{
  "localizations": {
    "es": {
      "translator": {
        "name": "Maria García",
        "email": "maria@translations.example.com",
        "organization": "Professional Translations Inc."
      },
      "translatedAt": "2025-01-20T00:00:00Z",
      "sections": { },
      "prompts": { }
    }
  }
}
```

This is attribution only. PromptResponse does not implement signatures or
verification of translations; recipients who need authenticated provenance
should rely on out-of-band channels.

### 17.8 Partial Localizations

Localizations may be partial. Implementations MUST fall back gracefully:

- If a localization exists but is missing a field → use base value
- If a localization has extra fields not in base → ignore them
- If a language code is unknown → treat as no localization

---

## 18. Attachments

APR supports embedded file attachments that can be annotated to reference specific prompt/response pairs.

### 18.1 Attachments Array

```json
{
  "version": "0.2",
  "documentType": "filledForm",
  "metadata": { },
  "sections": [ ],
  "attachments": [
    {
      "id": "att_passport_scan",
      "filename": "passport.pdf",
      "mimeType": "application/pdf",
      "description": "Scanned passport photo page",
      "addedAt": "2025-01-22T10:30:00Z",
      "addedBy": "John Doe",
      "size": "245678",
      "data": "base64...",
      "annotations": [
        {
          "responseId": "resp_passport_number",
          "note": "Passport number visible on page 1"
        }
      ]
    }
  ]
}
```

### 18.2 Attachment Object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique attachment identifier |
| `filename` | string | Yes | Original filename |
| `mimeType` | string | Yes | MIME type (e.g., `application/pdf`, `image/jpeg`) |
| `description` | string | No | Human-readable description |
| `addedAt` | string | No | ISO 8601 timestamp when attached |
| `addedBy` | string | No | Name of person who added attachment |
| `size` | string | Yes | File size in bytes (as string) |
| `data` | string | Yes | Base64-encoded file content |
| `annotations` | array | No | References to related prompt/response pairs |

### 18.3 Annotation Object

Annotations link an attachment to specific responses it supports or relates to:

```json
{
  "annotations": [
    {
      "responseId": "resp_ssn_proof",
      "promptId": "prompt_ssn",
      "note": "Social Security card showing number"
    },
    {
      "responseId": "resp_address_proof",
      "promptId": "prompt_address",
      "note": "Utility bill showing current address"
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `responseId` | string | Yes* | References a specific response instance |
| `promptId` | string | No | References the prompt definition (for context) |
| `note` | string | No | Explanation of how attachment relates to response |

*At least one of `responseId` or `promptId` should be provided.

### 18.4 Attachment Use Cases

| Use Case | Example |
|----------|---------|
| **Supporting document** | Passport scan attached to passport number field |
| **Proof of response** | Pay stub attached to salary field |
| **Signature image** | Handwritten signature image for signature field |
| **Additional context** | Resume attached to employment history section |
| **Required upload** | Photo ID required by template |

### 18.5 Template Attachment Hints

Templates can indicate that certain prompts expect attachments:

```json
{
  "id": "prompt_photo_id",
  "label": "Government-Issued Photo ID",
  "response": "",
  "hints": {
    "type": "file",
    "inputHelpText": "Please attach a scan or photo of your ID",
    "behaviorExpected": true
  }
}
```

When `type` is `file`, implementations SHOULD:
- Provide an attachment interface for that prompt
- Auto-generate annotation linking attachment to the prompt

### 18.6 Attachment ID Format

Attachment IDs follow the same rules as other IDs:

- ASCII letters, digits, underscore only
- Must start with letter
- Recommended format: `att_` prefix + descriptive name (e.g., `att_passport_scan`)

### 18.7 Supported MIME Types

Implementations SHOULD support at minimum:

| Category | MIME Types |
|----------|------------|
| **Documents** | `application/pdf` |
| **Images** | `image/jpeg`, `image/png`, `image/gif`, `image/webp` |
| **Text** | `text/plain` |

Implementations MAY support additional types but MUST preserve attachments with unrecognized MIME types.

### 18.8 Attachment Security

**Safety considerations:**

- Attachments are base64-encoded data, not executable
- Implementations SHOULD scan attachments for malware before processing
- Implementations SHOULD NOT auto-execute or auto-open attachments
- Users SHOULD be warned before opening attachments from untrusted sources

**Size considerations:**

- Large attachments significantly increase file size
- See Section 19 for file size requirements

---

## 19. Encoding, Format, and Size

### 19.1 Encoding Requirements

- **Encoding:** UTF-8 (required)
- **Line endings:** Any (LF, CRLF, CR)
- **Whitespace:** Insignificant (may pretty-print or minify)
- **MIME type:** `application/vnd.apr+json` or `application/json`

### 19.2 File Size Requirements

APR-compliant systems MUST support minimum file sizes to ensure forms with reasonable attachments can be submitted.

**Minimum acceptance requirement:**

| Requirement | Description |
|-------------|-------------|
| **Absolute minimum** | Systems MUST accept files of at least **1 MB** |
| **Template-relative minimum** | Systems MUST accept files of at least **3× the template size** |
| **Effective minimum** | Whichever is **greater** of the above |

**Examples:**

| Template Size | 3× Template | 1 MB Floor | Minimum Accepted |
|---------------|-------------|------------|------------------|
| 50 KB | 150 KB | 1 MB | **1 MB** |
| 200 KB | 600 KB | 1 MB | **1 MB** |
| 500 KB | 1.5 MB | 1 MB | **1.5 MB** |
| 2 MB | 6 MB | 1 MB | **6 MB** |

**Rationale:**

- 1 MB floor ensures basic attachments (a few images, a short PDF) are always possible
- 3× multiplier ensures filled forms have room for reasonable supporting documents
- Large templates (with many prompts or embedded localizations) get proportionally more allowance

### 19.3 Size Advisories

**Template authors SHOULD:**

- Keep base templates under 500 KB when possible
- Use `suggestValuesUrl` for large option lists instead of embedding
- Consider attachment size when designing forms requiring uploads

**Implementation authors SHOULD:**

- Display file size to users before submission
- Warn when approaching size limits
- Provide clear error messages when size limits are exceeded

**Form fillers SHOULD:**

- Compress images before attaching
- Use PDF rather than raw images for multi-page documents
- Remove unnecessary attachments before submission

### 19.4 Size Calculation

File size is calculated as the byte length of the UTF-8 encoded JSON document, including:

- All text content
- All base64-encoded attachments
- All localizations
- All whitespace (if pretty-printed)

---

## 20. Implementation Checklist

### 20.1 Minimum Viable Implementation

A minimal APR implementation must:

- [ ] Parse JSON with UTF-8 encoding
- [ ] Read `version`, `documentType`, `metadata`, `sections`
- [ ] Render sections with `title` and nested structure
- [ ] Render prompts with `label` and text input
- [ ] Store responses as strings
- [ ] Handle `.aprt` as blank, `.aprf` as filled
- [ ] Ignore unknown fields
- [ ] Accept files of at least 1 MB (or 3× template size)

### 20.2 Recommended Implementation

A recommended APR implementation should also:

- [ ] Support all Core Types (section 7.1)
- [ ] Render `suggestValues` as dropdown
- [ ] Render `boolean` as radio buttons
- [ ] Display `inputPlaceholder` and `inputHelpText`
- [ ] Support fixed and dynamic tables
- [ ] Preserve unknown fields on save
- [ ] Auto-generate `responseId` for filled forms
- [ ] Support basic attachments (PDF, images)

### 20.3 Full Implementation

A full APR implementation may also:

- [ ] Support Extended Types (section 7.2)
- [ ] Support Formatted Types (section 7.3)
- [ ] Show `inputValidationPattern` warnings
- [ ] Support localizations with language switching
- [ ] Display attachment annotations
- [ ] Support CEL expression hints (see appendix)

---

## Appendix A: Digital Signatures (Out of Scope)

Cryptographic signing of templates or filled forms is **not part of the APR
format** and PromptResponse does not implement signature creation or
verification. Earlier drafts of this specification described a `templateSignatures`
and `formSignatures` mechanism; that mechanism has been removed.

Implementations that encounter unknown fields in `metadata` (including
hypothetical signature fields produced by older drafts) MUST preserve them
unchanged on save, per Section 4.x's general round-trip rule.

Recipients who require authenticated provenance should rely on out-of-band
channels: signed PDFs, e-sign workflows, certificate-pinned distribution
servers, or transport-level (HTTPS / signed-URL) controls owned by the
publisher.

---

## Appendix B: Expression Hints with CEL (Optional)

APR supports dynamic behavior through expression hints using the Common Expression Language (CEL). This enables conditional visibility, dynamic suggested values, and cross-field validation.

### C.1 Overview

[CEL (Common Expression Language)](https://github.com/google/cel-spec) is a non-Turing-complete expression language designed for safe evaluation in configuration files. It is:

- **Sandboxed** - No I/O, no side effects, no infinite loops
- **Deterministic** - Same inputs always produce same outputs
- **Type-safe** - Expressions are validated before evaluation
- **Familiar** - C-like syntax similar to JavaScript, Java, Go

CEL is used by Google Cloud, Kubernetes, Firebase, and other systems for policy evaluation.

### C.2 Expression Hint Fields

Expression hints use the `expr*` prefix and contain CEL expression strings:

| Field | Return Type | Description |
|-------|-------------|-------------|
| `exprHidden` | boolean | If true, hide this prompt |
| `exprReadOnly` | boolean | If true, make this prompt read-only |
| `exprExpected` | boolean | If true, mark this prompt as expected |
| `exprSuggestValues` | array | List of suggested values to display |
| `exprDefaultValue` | string | Default value when creating filled form |
| `exprValidation` | string | Validation message (empty string = valid) |
| `exprHelpText` | string | Dynamic help text based on context |

### C.3 CEL Evaluation Context

When evaluating expressions, all prompt responses are exposed as variables using their prompt IDs:

```cel
// Given a form with prompts: emp_status, emp_employer, addr_country
emp_status                          // Returns response string (or "" if empty)
emp_status == "Employed"            // Boolean comparison
addr_country == "USA"               // Another field
```

**Context structure:**

```json
{
  "emp_status": "Employed",
  "emp_employer": "Acme Corporation",
  "emp_start_date": "2020-01-15",
  "addr_country": "USA",
  "addr_state": "California",
  "addr_zip": ""
}
```

Each prompt ID becomes a CEL variable containing the response string.

### C.4 Built-in Variables

In addition to prompt responses, these built-in variables are available:

| Variable | Type | Description |
|----------|------|-------------|
| `_this` | string | Current prompt's response (self-reference) |
| `_id` | string | Current prompt's ID |
| `_now` | timestamp | Current date/time (UTC) |
| `_today` | string | Current date as "YYYY-MM-DD" |

**Example using `_this`:**

```json
{
  "id": "confirm_email",
  "label": "Confirm Email",
  "hints": {
    "exprValidation": "_this == email ? '' : 'Emails must match'"
  }
}
```

### C.5 Context Dictionary

Form filler applications can provide a **context dictionary** containing user, organization, or environmental data. This data is accessible in CEL expressions via the `ctx` namespace.

#### C.5.1 Context Structure

The context dictionary is a flat or nested object provided by the form filler application:

```json
{
  "ctx": {
    "user": {
      "firstName": "John",
      "lastName": "Doe",
      "email": "john.doe@acme.com",
      "phone": "+1-555-0100",
      "employeeId": "EMP-12345"
    },
    "address": {
      "street": "123 Main Street",
      "city": "Springfield",
      "state": "IL",
      "zip": "62701",
      "country": "USA"
    },
    "org": {
      "name": "Acme Corporation",
      "department": "Engineering",
      "costCenter": "CC-4200",
      "manager": "Jane Smith"
    },
    "env": {
      "device": "desktop",
      "location": "HQ-Building-A",
      "timestamp": "2025-01-22T10:30:00Z"
    }
  }
}
```

#### C.5.2 Accessing Context in CEL

Context values are accessed via `ctx.` prefix:

```cel
// User information
ctx.user.firstName                    // "John"
ctx.user.email                        // "john.doe@acme.com"

// Address
ctx.address.city                      // "Springfield"
ctx.address.state                     // "IL"

// Organization
ctx.org.name                          // "Acme Corporation"
ctx.org.department                    // "Engineering"

// Environment
ctx.env.device                        // "desktop"
```

#### C.5.3 Auto-fill with Context

Templates can use context for default values:

```json
{
  "id": "applicant_name",
  "label": "Full Name",
  "hints": {
    "exprDefaultValue": "ctx.user.firstName + ' ' + ctx.user.lastName"
  }
},
{
  "id": "applicant_email",
  "label": "Email Address",
  "hints": {
    "exprDefaultValue": "ctx.user.email"
  }
},
{
  "id": "applicant_state",
  "label": "State",
  "hints": {
    "exprDefaultValue": "ctx.address.state",
    "exprSuggestValues": "ctx.address.country == 'USA' ? ['AL', 'AK', 'AZ', '...'] : []"
  }
}
```

#### C.5.4 Conditional Logic with Context

Use context for conditional visibility or validation:

```json
{
  "id": "manager_approval",
  "label": "Manager Approval Required",
  "hints": {
    "exprHidden": "ctx.org.department != 'Finance'",
    "inputHelpText": "Required for Finance department submissions"
  }
},
{
  "id": "expense_amount",
  "label": "Expense Amount",
  "hints": {
    "type": "currency",
    "exprValidation": "double(_this) > 5000 && ctx.org.department != 'Executive' ? 'Expenses over $5000 require Executive approval' : ''"
  }
}
```

#### C.5.5 Dynamic Lookups

Context can include lookup functions or data:

```json
{
  "ctx": {
    "employees": [
      {"id": "EMP-001", "name": "Alice Johnson", "department": "Engineering"},
      {"id": "EMP-002", "name": "Bob Smith", "department": "Sales"},
      {"id": "EMP-003", "name": "Carol White", "department": "Engineering"}
    ],
    "products": [
      {"sku": "WIDGET-A", "name": "Widget A", "price": "29.99"},
      {"sku": "WIDGET-B", "name": "Widget B", "price": "49.99"}
    ]
  }
}
```

```json
{
  "id": "select_employee",
  "label": "Select Employee",
  "hints": {
    "exprSuggestValues": "ctx.employees.filter(e, e.department == ctx.org.department).map(e, e.name)"
  }
}
```

#### C.5.6 Standard Context Keys

While context is application-defined, these keys are recommended for interoperability:

| Path | Type | Description |
|------|------|-------------|
| `ctx.user.firstName` | string | User's first name |
| `ctx.user.lastName` | string | User's last name |
| `ctx.user.fullName` | string | User's full name |
| `ctx.user.email` | string | User's email address |
| `ctx.user.phone` | string | User's phone number |
| `ctx.user.id` | string | User identifier |
| `ctx.address.street` | string | Street address |
| `ctx.address.city` | string | City |
| `ctx.address.state` | string | State/province |
| `ctx.address.zip` | string | ZIP/postal code |
| `ctx.address.country` | string | Country |
| `ctx.org.name` | string | Organization name |
| `ctx.org.department` | string | Department name |
| `ctx.org.id` | string | Organization identifier |
| `ctx.env.device` | string | Device type (desktop, mobile, tablet) |
| `ctx.env.locale` | string | User's locale (e.g., "en-US") |
| `ctx.env.timezone` | string | User's timezone |

#### C.5.7 Context Availability

Context may be:
- **Fully available** - All values populated by the application
- **Partially available** - Some values present, others missing
- **Unavailable** - No context provided (anonymous/offline use)

**Expressions MUST handle missing context gracefully:**

```cel
// Safe access with fallback
has(ctx.user) && has(ctx.user.firstName) ? ctx.user.firstName : ''

// Or use default operator (if supported)
ctx.user.firstName ?? ''
```

**Template design guidance:**
- Always provide static fallbacks for context-dependent defaults
- Don't assume context will be available
- Test templates with empty context

#### C.5.8 Context vs Response Data

| Aspect | Prompt Responses | Context Dictionary |
|--------|------------------|-------------------|
| **Source** | User input | Application/environment |
| **Stored in file** | Yes (in `response` field) | No (runtime only) |
| **Available at** | Fill time | Fill time |
| **CEL access** | `prompt_id` | `ctx.path.to.value` |
| **Editable by user** | Yes | No (read-only) |

Context is **not saved** in the APR file. It's provided at runtime by the form filler application.

#### C.5.9 Security Considerations

**For applications providing context:**

1. **Minimize sensitive data** - Only include what templates actually need
2. **User consent** - Inform users what data is being shared with forms
3. **Audit logging** - Log when context data is used in form filling
4. **Sandboxing** - Context data should not grant access beyond CEL expressions

**For template authors:**

1. **Don't assume context** - Always provide fallbacks
2. **Don't leak context** - Don't copy sensitive context into visible fields without user awareness
3. **Document requirements** - Specify what context your template expects

### C.6 Type Handling

All prompt responses are strings. CEL provides type coercion:

```cel
// String comparisons (most common)
emp_status == "Employed"

// Numeric comparisons - use int() or double()
int(emp_age) >= 18
double(emp_salary) > 50000.0

// Date comparisons - use timestamp()
timestamp(emp_start_date) < timestamp(_today)

// Empty checks
emp_employer != ""
size(emp_employer) > 0
```

**Type coercion functions:**

| Function | Description |
|----------|-------------|
| `int(string)` | Parse as integer |
| `double(string)` | Parse as decimal |
| `timestamp(string)` | Parse ISO 8601 date/time |
| `size(string)` | Length of string |
| `matches(string, regex)` | Regex match |

### C.7 Common Patterns

**Conditional visibility:**

```json
{
  "id": "emp_employer",
  "label": "Employer Name",
  "hints": {
    "exprHidden": "emp_status == 'Unemployed' || emp_status == 'Retired' || emp_status == 'Student'"
  }
}
```

**Dynamic suggested values:**

```json
{
  "id": "addr_state",
  "label": "State/Province",
  "hints": {
    "exprSuggestValues": "addr_country == 'USA' ? ['Alabama', 'Alaska', 'Arizona', 'Arkansas', 'California', '...'] : addr_country == 'Canada' ? ['Alberta', 'British Columbia', 'Manitoba', '...'] : []"
  }
}
```

**Cross-field validation:**

```json
{
  "id": "emp_end_date",
  "label": "End Date",
  "hints": {
    "type": "date",
    "exprValidation": "_this == '' || emp_start_date == '' ? '' : (timestamp(_this) > timestamp(emp_start_date) ? '' : 'End date must be after start date')"
  }
}
```

**Conditional expected/required:**

```json
{
  "id": "emp_employer",
  "label": "Employer Name",
  "hints": {
    "exprExpected": "emp_status == 'Employed' || emp_status == 'Self-Employed'"
  }
}
```

**Dynamic default value:**

```json
{
  "id": "billing_address",
  "label": "Billing Address",
  "hints": {
    "exprDefaultValue": "shipping_address"
  }
}
```

**Dynamic help text:**

```json
{
  "id": "tax_id",
  "label": "Tax ID",
  "hints": {
    "exprHelpText": "addr_country == 'USA' ? 'Enter your SSN (XXX-XX-XXXX)' : addr_country == 'Canada' ? 'Enter your SIN (XXX-XXX-XXX)' : 'Enter your national tax ID'"
  }
}
```

### C.8 Complete Example

```json
{
  "sections": [
    {
      "id": "section_employment",
      "title": "Employment Information",
      "prompts": [
        {
          "id": "emp_status",
          "label": "Employment Status",
          "hints": {
            "suggestValues": ["Employed", "Self-Employed", "Unemployed", "Retired", "Student"],
            "behaviorExpected": true
          }
        },
        {
          "id": "emp_employer",
          "label": "Employer Name",
          "hints": {
            "exprHidden": "emp_status != 'Employed'",
            "exprExpected": "emp_status == 'Employed'"
          }
        },
        {
          "id": "emp_business_name",
          "label": "Business Name",
          "hints": {
            "exprHidden": "emp_status != 'Self-Employed'",
            "exprExpected": "emp_status == 'Self-Employed'"
          }
        },
        {
          "id": "emp_start_date",
          "label": "Start Date",
          "hints": {
            "type": "date",
            "exprHidden": "emp_status == 'Unemployed' || emp_status == 'Student'"
          }
        },
        {
          "id": "emp_annual_income",
          "label": "Annual Income",
          "hints": {
            "type": "currency",
            "displayPrefix": "$",
            "exprHidden": "emp_status == 'Unemployed' || emp_status == 'Student'",
            "exprHelpText": "emp_status == 'Retired' ? 'Include pension and retirement income' : 'Enter gross annual salary'"
          }
        }
      ]
    },
    {
      "id": "section_address",
      "title": "Address",
      "prompts": [
        {
          "id": "addr_country",
          "label": "Country",
          "hints": {
            "suggestValues": ["USA", "Canada", "Mexico", "United Kingdom", "Other"],
            "behaviorExpected": true
          }
        },
        {
          "id": "addr_state",
          "label": "State/Province",
          "hints": {
            "exprHidden": "addr_country == 'Other'",
            "exprSuggestValues": "addr_country == 'USA' ? ['Alabama', 'Alaska', 'Arizona', 'California', 'Colorado', '...'] : addr_country == 'Canada' ? ['Alberta', 'British Columbia', 'Manitoba', 'Ontario', '...'] : addr_country == 'Mexico' ? ['Aguascalientes', 'Baja California', '...'] : addr_country == 'United Kingdom' ? ['England', 'Scotland', 'Wales', 'Northern Ireland'] : []"
          }
        },
        {
          "id": "addr_region",
          "label": "Region/Province",
          "hints": {
            "exprHidden": "addr_country != 'Other'",
            "inputHelpText": "Enter your state, province, or region"
          }
        },
        {
          "id": "addr_postal_code",
          "label": "Postal Code",
          "hints": {
            "exprHelpText": "addr_country == 'USA' ? 'ZIP code (12345 or 12345-6789)' : addr_country == 'Canada' ? 'Postal code (A1A 1A1)' : addr_country == 'United Kingdom' ? 'Postcode (SW1A 1AA)' : 'Enter postal code'"
          }
        }
      ]
    }
  ]
}
```

### C.9 Expression Precedence

When both static hints and expression hints are present, expression hints take precedence:

| Static Hint | Expression Hint | Behavior |
|-------------|-----------------|----------|
| `behaviorHidden: true` | `exprHidden: "..."` | Expression result used |
| `behaviorExpected: true` | `exprExpected: "..."` | Expression result used |
| `suggestValues: [...]` | `exprSuggestValues: "..."` | Expression result used |
| `inputHelpText: "..."` | `exprHelpText: "..."` | Expression result used |

Static hints serve as fallbacks if:
- The implementation doesn't support CEL
- The expression fails to evaluate
- The expression returns null/undefined

### C.10 Error Handling

Expressions may fail to evaluate due to:
- Syntax errors in the expression
- Type errors (e.g., `int("abc")`)
- Reference to non-existent prompt ID
- Division by zero

**Implementation requirements:**

1. Expressions MUST NOT crash the application
2. Failed expressions SHOULD fall back to static hints
3. Failed expressions MAY log warnings for template authors
4. Implementations SHOULD validate expressions when loading templates

**Fallback behavior:**

| Expression Type | Fallback on Error |
|-----------------|-------------------|
| `exprHidden` | `false` (show the field) |
| `exprReadOnly` | `false` (allow editing) |
| `exprExpected` | `false` (not expected) |
| `exprSuggestValues` | Empty array or static `suggestValues` |
| `exprDefaultValue` | Static `inputDefaultValue` or empty |
| `exprValidation` | Empty string (no validation error) |
| `exprHelpText` | Static `inputHelpText` |

### C.11 Security Considerations

CEL is designed to be safe, but implementations SHOULD:

1. **Limit execution time** - Set reasonable timeouts (e.g., 100ms)
2. **Limit memory** - Cap string sizes and array lengths
3. **No external access** - CEL has no I/O by design; don't add any
4. **Validate on load** - Parse and type-check expressions when loading templates
5. **Sandbox evaluation** - Use CEL's built-in sandboxing features

**CEL guarantees:**
- No file system access
- No network access
- No infinite loops (not Turing-complete)
- No side effects
- Deterministic evaluation

### C.12 Implementation Notes

**For implementations that support CEL:**

1. Use an official CEL implementation:
   - Go: `github.com/google/cel-go`
   - Java: `dev.cel:cel`
   - C++: `google/cel-cpp`
   - Python: `cel-python` (community)
   - JavaScript: `cel-js` (community)

2. Register the evaluation context with all prompt IDs as variables
3. Add built-in variables (`_this`, `_id`, `_now`, `_today`)
4. Evaluate expressions on every relevant change (field value update)
5. Cache parsed expressions for performance

**For implementations that don't support CEL:**

1. MUST preserve `expr*` fields when reading/writing
2. SHOULD fall back to static hints
3. MAY display a notice that dynamic features are not supported

---

## Appendix C: Complexity Notes

### Issues Identified in Current Implementation

1. **`responseMetadata` redundancy** - The `responseMetadata.lastModified` field duplicates functionality. Consider deprecating in favor of document-level `modified` timestamp.

2. **Type proliferation** - The current implementation has many specialized types (`ssn`, `ein`, `creditcard`, etc.). These are classified as optional "Formatted Types" to keep the core specification simple.

3. **`multichoice` response format** - Currently uses comma-separated values. This works but could be ambiguous if values contain commas. Recommendation: Allow either comma-separated or JSON array format.

### Simplifications Made

1. **Tiered type system** - Core, Extended, and Formatted types allow minimal implementations
2. **Clear extension points** - Signatures and submission are appendices, not required
3. **File extension precedence** - Simple rule eliminates ambiguity
4. **Ignore-unknown policy** - Ensures forward compatibility
5. **Prefixed hint naming** - All hints use category prefixes (`input*`, `behavior*`, `display*`, `layout*`, `suggest*`, `type*`) for easy identification and grouping

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.1 | 2025-11-12 | Initial specification |
| 0.2 | 2025-12-02 | Restructured hints with prefixed naming convention (`input*`, `behavior*`, `display*`, `layout*`, `suggest*`, `type*`), renamed `expectedDataType` to `type`, added behavioral/display/layout hints, formalized type tiers, added implementation checklist |
| 0.2 | 2025-12-03 | Added publisher metadata, semantic versioning, `submitUrls` array, submission history tracking, response identifiers (`responseId`), localization support with translator attribution, embedded attachments with annotations, file size requirements (1 MB or 3× template minimum), CEL expression hints (`expr*`) for dynamic visibility/validation/suggested values, context dictionary (`ctx`) for application-provided user/org/environment data |
| 0.2.1 | 2026-05-01 | Removed cryptographic-signature mechanism (Appendix A retained as out-of-scope note); removed `templateSourceUrl` and `s3://` submitUrl scheme; removed S3 submission-config appendix |

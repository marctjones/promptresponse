# APR File Format Specification

**Version:** 1.1
**Status:** Draft
**Last Updated:** 2025-12-02

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
  "version": "1.1",
  "documentType": "template",
  "metadata": { },
  "sections": [ ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | string | Yes | Specification version (currently "1.1") |
| `documentType` | string | Yes | Either `"template"` or `"filledForm"` |
| `metadata` | object | Yes | Document metadata |
| `sections` | array | Yes | Array of Section objects |

### 3.2 Metadata Object

```json
{
  "title": "Form Title",
  "description": "Optional description",
  "author": "Author Name",
  "created": "2025-01-15T00:00:00Z",
  "modified": "2025-01-15T00:00:00Z",
  "templateId": "unique-id",
  "templateVersion": "1.0"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `title` | string | Yes | Human-readable form title |
| `description` | string | No | Form description |
| `author` | string | No | Template author |
| `created` | string | No | ISO 8601 creation timestamp |
| `modified` | string | No | ISO 8601 last modified timestamp |
| `templateId` | string | No | Unique template identifier |
| `templateVersion` | string | No | Template version number |

**Filled Form Additional Fields:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `filledBy` | string | No | Name of person who filled the form |
| `filledDate` | string | No | ISO 8601 timestamp when filled |

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

```json
{
  "placeholder": "you@example.com",
  "expectedDataType": "email",
  "helpText": "Enter your primary email",
  "suggestedValues": ["gmail.com", "outlook.com"],
  "validationPattern": "^[^@]+@[^@]+$"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `placeholder` | string | Example text shown in empty field |
| `expectedDataType` | string | Type hint (see section 6) |
| `helpText` | string | Additional guidance shown to user |
| `suggestedValues` | array | List of suggested string values |
| `validationPattern` | string | Regex pattern (advisory only) |

### 6.1 Type-Specific Hint Fields

Some `expectedDataType` values support additional hint fields:

**For `range` type:**

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `min` | string | "0" | Minimum value |
| `max` | string | "100" | Maximum value |
| `step` | string | "1" | Step increment |

**For `table` type:**

| Field | Type | Description |
|-------|------|-------------|
| `tableDefinition` | object | Table structure (see section 8) |

---

## 7. Data Types

The `expectedDataType` field suggests how the UI should render the field.

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
2. **Missing `expectedDataType`** → Default to `text`
3. **Any response is valid** → Types never restrict what the user can enter
4. **All responses are strings** → Even numbers are stored as `"42"` not `42`

### 7.5 Types Are Hints, Not Validators

The `expectedDataType` field tells the UI how to render the input (date picker, number spinner, etc.) and tells the user what kind of data the form designer expects.

**It does NOT:**
- Prevent the user from entering any string they want
- Make the file invalid if the response doesn't match the type
- Require implementations to validate responses against the type

**Examples of valid responses regardless of `expectedDataType`:**

| expectedDataType | Valid Responses (all of these are valid) |
|------------------|------------------------------------------|
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
  "expectedDataType": "table",
  "tableDefinition": {
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
    { "id": "qty", "label": "Quantity", "type": "number" },
    { "id": "price", "label": "Price", "type": "currency" }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Column identifier |
| `label` | string | Yes | Column header text |
| `type` | string | No | Cell type: `text`, `number`, `currency`, `date`, `boolean` |
| `placeholder` | string | No | Placeholder for cells |

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
    "maxRows": 50
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `minRows` | number | 0 | Minimum rows required |
| `maxRows` | number | 100 | Maximum rows allowed |

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
    "expectedDataType": "table",
    "tableDefinition": { ... }
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
| `multichoice` | Checkboxes for each `suggestedValues` item |

### 9.2 Suggested Values Behavior

When `suggestedValues` is present:

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

- Response doesn't match `expectedDataType` → **Still valid**
- Response doesn't match `validationPattern` → **Still valid**
- Response is empty → **Still valid**
- Response contains "wrong" information → **Still valid**

**The semantic content of responses is NEVER validated by the file format.**

The format validates that responses are proper strings. It never validates what those strings mean.

### 10.4 Advisory Feedback (Optional)

Implementations MAY show advisory feedback to help users:

- Highlight fields where response doesn't match `expectedDataType`
- Show warning icon if `validationPattern` doesn't match
- Indicate which fields the form designer marked as expected

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
- Treat unknown `expectedDataType` values as `text`
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
| `prompt.hints.helpText` | Field description/instructions |
| `prompt.id` | Unique identifier for label association |

### 13.2 Implementation Requirements for Accessibility

Implementations targeting WCAG 2.1 Level AA SHOULD:

1. Associate each `label` with its input using `for`/`id` or ARIA
2. Expose `helpText` to assistive technology
3. Provide keyboard navigation between fields
4. Maintain logical reading order matching section hierarchy
5. Announce section titles when navigating between sections
6. Indicate required fields (if workflow defines them)

### 13.3 Template Design for Accessibility

Templates SHOULD:

1. Use clear, descriptive labels (not "Field 1", "Field 2")
2. Provide `helpText` for fields that need explanation
3. Use `section.title` to group related fields
4. Avoid relying solely on `placeholder` for instructions

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
| **Signature type** | Template signature (optional) | Form signature (optional) |

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
- May be digitally signed by publisher (template signature)
- Intended to be filled out, not edited structurally by end users
- When opened for filling, implementation creates a new filled form

### 15.5 Filled Form (.aprf) Rules

- `documentType` MUST be `"filledForm"`
- `response` fields contain user data
- Structure matches original template (sections, prompts, labels)
- May be digitally signed by filler (form signature)
- Signed forms become logically read-only (edits invalidate signature)
- Can be opened, reviewed, edited (if unsigned), and re-saved

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

## 16. Encoding and Format

- **Encoding:** UTF-8 (required)
- **Line endings:** Any (LF, CRLF, CR)
- **Whitespace:** Insignificant (may pretty-print or minify)
- **MIME type:** `application/vnd.apr+json` or `application/json`

---

## 17. Implementation Checklist

### 17.1 Minimum Viable Implementation

A minimal APR implementation must:

- [ ] Parse JSON with UTF-8 encoding
- [ ] Read `version`, `documentType`, `metadata`, `sections`
- [ ] Render sections with `title` and nested structure
- [ ] Render prompts with `label` and text input
- [ ] Store responses as strings
- [ ] Handle `.aprt` as blank, `.aprf` as filled
- [ ] Ignore unknown fields

### 17.2 Recommended Implementation

A recommended APR implementation should also:

- [ ] Support all Core Types (section 7.1)
- [ ] Render `suggestedValues` as dropdown
- [ ] Render `boolean` as radio buttons
- [ ] Display `placeholder` and `helpText`
- [ ] Support fixed and dynamic tables
- [ ] Preserve unknown fields on save

### 17.3 Full Implementation

A full APR implementation may also:

- [ ] Support Extended Types (section 7.2)
- [ ] Support Formatted Types (section 7.3)
- [ ] Show `validationPattern` warnings
- [ ] Support digital signatures (see appendix)
- [ ] Support submission configuration (see appendix)

---

## Appendix A: Digital Signatures (Optional)

Digital signatures are an optional extension for document authenticity. APR distinguishes between two types of signatures with different purposes.

### A.1 Two Types of Signatures

| Signature Type | Who Signs | What's Signed | Purpose |
|----------------|-----------|---------------|---------|
| **Template Signature** | Form publisher | Template structure only | Verify template hasn't been tampered with |
| **Form Signature** | Form filler | Complete filled form | Certify the responses as authentic |

### A.2 Template Signatures

Template signatures attest to the authenticity and integrity of the form structure itself:

- Signed by the organization that created/published the template
- Covers: `sections`, `prompts`, `hints`, `metadata` (except signatures)
- Does NOT cover: `response` fields (which are empty in templates)
- Use case: Government agency publishes an official form; signature proves it's genuine

**When template signature is verified:**
- User can trust the form comes from the stated publisher
- User can trust the questions haven't been modified
- User can trust the submission configuration is legitimate

### A.3 Form Signatures

Form signatures attest that a specific person filled out specific responses:

- Signed by the person who filled out the form
- Covers: entire document including all `response` values
- Use case: Employee signs their completed timesheet; signature proves they submitted it

**When form signature is verified:**
- Recipient can trust the responses came from the stated person
- Recipient can trust responses haven't been modified after signing
- Form becomes logically read-only (modifications would invalidate signature)

### A.4 Signature Data Structure

```json
{
  "metadata": {
    "templateSignatures": [ ],
    "formSignatures": [ ]
  }
}
```

**Signature Object:**

```json
{
  "signerName": "John Doe",
  "signerEmail": "john@example.com",
  "signerOrganization": "Acme Corp",
  "signedAt": "2025-01-15T12:00:00Z",
  "signatureData": "base64...",
  "signatureType": "template",
  "hashAlgorithm": "SHA256",
  "certificateChain": "base64..."
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `signerName` | Yes | Human-readable signer name |
| `signerEmail` | No | Signer's email address |
| `signerOrganization` | No | Organization name (especially for template signatures) |
| `signedAt` | Yes | ISO 8601 timestamp |
| `signatureData` | Yes | Base64-encoded signature |
| `signatureType` | Yes | `"template"` or `"form"` |
| `hashAlgorithm` | Yes | Hash algorithm used (e.g., `"SHA256"`) |
| `certificateChain` | No | Base64-encoded certificate chain for verification |

### A.5 Signature Workflow

```
Template Creation          Form Filling              Verification
──────────────────        ──────────────            ─────────────

[Create .aprt]            [Open .aprt]              [Open .aprf]
      │                         │                         │
      ▼                         ▼                         ▼
[Optionally sign     ─────►[Verify template       [Verify both
 as template]               signature if present]   signatures]
      │                         │                         │
      ▼                         ▼                         │
[Publish]                 [Fill responses]                │
                                │                         │
                                ▼                         │
                          [Sign as form] ────────────────►│
                                │                         │
                                ▼                         ▼
                          [Save .aprf]              [Trust content]
```

### A.6 Implementation Notes

Implementations that don't support signatures:
- MUST preserve signature fields when reading/writing
- SHOULD display a notice that signatures are present but unverified
- MUST NOT remove signatures when saving

Implementations that support signatures:
- SHOULD use established cryptographic libraries
- SHOULD support X.509 certificate chains
- MAY support multiple signatures of each type
- SHOULD refuse to modify signed forms without warning

---

## Appendix B: Submission Configuration (Optional)

Templates may include submission configuration for direct form submission:

```json
{
  "metadata": {
    "submissionConfig": {
      "type": "s3-presigned-post",
      "url": "https://bucket.s3.amazonaws.com/",
      "fields": { },
      "expiresAt": "2025-12-31T00:00:00Z"
    }
  }
}
```

Implementations that don't support submission should preserve this field.

---

## Appendix C: Complexity Notes

### Issues Identified in Current Implementation

1. **`responseMetadata` redundancy** - The `responseMetadata.lastModified` field duplicates functionality. Consider deprecating in favor of document-level `modified` timestamp.

2. **Type proliferation** - The current implementation has many specialized types (`ssn`, `ein`, `creditcard`, etc.). These are classified as optional "Formatted Types" to keep the core specification simple.

3. **`min`/`max`/`step` location** - For `range` type, these fields are in `hints` rather than a dedicated object. This is acceptable but slightly inconsistent with `tableDefinition`.

4. **`multichoice` response format** - Currently uses comma-separated values. This works but could be ambiguous if values contain commas. Recommendation: Allow either comma-separated or JSON array format.

### Simplifications Made

1. **Tiered type system** - Core, Extended, and Formatted types allow minimal implementations
2. **Clear extension points** - Signatures and submission are appendices, not required
3. **File extension precedence** - Simple rule eliminates ambiguity
4. **Ignore-unknown policy** - Ensures forward compatibility

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-11-12 | Initial specification |
| 1.1 | 2025-12-02 | Formalized type tiers, added implementation checklist, clarified extension handling |

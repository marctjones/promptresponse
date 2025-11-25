# APR File Format Specification

Version: 1.0
Last Updated: 2025-11-12

## Overview

The APR (Adaptive Prompt Response) format is a JSON-based file format designed for flexible, portable forms that are not constrained by the page metaphor of traditional document formats.

## Design Principles

1. **Text-First**: All responses are stored as plain text strings
2. **Type Hints, Not Restrictions**: Expected data types are suggestions, not enforced constraints
3. **Semantic Structure**: Sections provide meaningful grouping with unlimited nesting
4. **No Layout/Styling**: UI presentation is dynamic and user-configurable
5. **Human-Readable**: Standard JSON for easy parsing and debugging
6. **Portable**: Maximum interoperability across platforms and tools

## File Extension

`.apr` - Adaptive Prompt Response

## Document Types

APR files come in two types:

### 1. Template (`documentType: "template"`)

A blank form definition intended to be filled out. Contains prompts with empty responses.

### 2. Filled Form (`documentType: "filledForm"`)

A completed form with responses. Based on a template but contains user data.

## Format Specification

### Root Document Structure

```json
{
  "version": "1.0",
  "documentType": "template" | "filledForm",
  "metadata": { ... },
  "sections": [ ... ]
}
```

### Metadata Object

#### Template Metadata

```json
{
  "title": "Form Title",
  "description": "Optional form description",
  "created": "2025-11-12T00:00:00Z",
  "modified": "2025-11-12T00:00:00Z",
  "author": "Author name (optional)",
  "templateId": "unique-template-identifier",
  "version": "1.0",
  "templateSignatures": [
    {
      "signerName": "IRS Forms Division",
      "signerEmail": "forms@irs.gov",
      "signerOrganization": "Internal Revenue Service",
      "certificateIssuer": "DigiCert",
      "certificateThumbprint": "ABC123...",
      "signedAt": "2025-11-12T00:00:00Z",
      "signatureData": "Base64EncodedSignature...",
      "signatureType": "template",
      "hashAlgorithm": "SHA256",
      "signatureReason": "Official IRS form for tax year 2025"
    }
  ]
}
```

**Digital Signatures (Optional):**
- `templateSignatures`: Array of signatures from template publishers
- Establishes authenticity of the template structure
- Multiple signatures supported for co-signing

**Submission Configuration (Optional):**
Templates may include submission configuration to enable direct form submission:

```json
{
  "submissionConfig": {
    "type": "s3-presigned-post",
    "url": "https://my-bucket.s3.us-east-1.amazonaws.com/",
    "fields": {
      "key": "filled-forms/${filename}",
      "AWSAccessKeyId": "AKIAIOSFODNN7EXAMPLE",
      "policy": "eyJleHBpcmF0aW9u...",
      "signature": "0RavWzkygo6QX9caELEqKi9kDbU=",
      "acl": "private"
    },
    "expiresAt": "2025-12-31T12:00:00Z"
  }
}
```

- `type`: Submission method (currently only "s3-presigned-post")
- `url`: Target S3 bucket URL
- `fields`: AWS pre-signed POST fields (policy, signature, etc.)
- `expiresAt`: Policy expiration timestamp

This allows users to submit filled forms directly to S3 without server infrastructure.

#### Filled Form Metadata

```json
{
  "title": "Form Title",
  "description": "Optional form description",
  "templateId": "template-identifier",
  "templateVersion": "1.0",
  "filledBy": "Person filling out form",
  "filledDate": "2025-11-12T14:30:00Z",
  "modified": "2025-11-12T14:35:00Z",
  "templateSignatures": [
    {
      "signerName": "IRS Forms Division",
      "certificateThumbprint": "ABC123...",
      "signedAt": "2025-11-12T00:00:00Z",
      "signatureData": "Base64...",
      "signatureType": "template"
    }
  ],
  "formSignatures": [
    {
      "signerName": "John Doe",
      "signerEmail": "john@example.com",
      "certificateIssuer": "Self-signed",
      "certificateThumbprint": "XYZ789...",
      "signedAt": "2025-11-12T15:00:00Z",
      "signatureData": "Base64...",
      "signatureType": "filledForm",
      "hashAlgorithm": "SHA256",
      "signatureReason": "Attestation of accuracy"
    }
  ]
}
```

**Digital Signatures (Optional):**
- `templateSignatures`: Inherited from the template (establishes trust chain)
- `formSignatures`: Signatures from the person(s) who filled out the form
- Signed forms become read-only and cannot be edited

### Section Structure

Sections provide semantic grouping of prompts and can be nested to any depth.

```json
{
  "id": "section_001",
  "title": "Section Title",
  "description": "Optional section description",
  "sections": [ ... ],
  "prompts": [ ... ]
}
```

**Rules:**
- Sections can contain both nested `sections[]` and `prompts[]`
- Nested `sections` are optional (can be empty array or omitted)
- At least one of `sections` or `prompts` should contain data
- Sections can be nested to unlimited depth (sections within sections)
- No artificial depth limit - structure to match your content's natural hierarchy

### Prompt Structure

Prompts are the individual questions/fields in the form.

```json
{
  "id": "prompt_001",
  "label": "Question or field label",
  "response": "",
  "hints": {
    "placeholder": "Example input",
    "expectedDataType": "text",
    "suggestedValues": [],
    "helpText": "Additional guidance",
    "validationPattern": "optional-regex"
  },
  "responseMetadata": {
    "inferredDataType": "",
    "lastModified": ""
  }
}
```

### Hints Object

Hints provide guidance to the UI but are **never enforced**.

```json
{
  "placeholder": "Example: 2025-11-12",
  "expectedDataType": "date",
  "suggestedValues": ["2025-01-01", "2025-12-31"],
  "helpText": "Enter the start date",
  "validationPattern": "\\d{4}-\\d{2}-\\d{2}",
  "tableDefinition": null
}
```

**Fields:**
- `placeholder`: Text shown in empty input field
- `expectedDataType`: Type hint (see Data Types below)
- `suggestedValues`: Array of string suggestions for autocomplete
- `helpText`: Additional help text shown to user
- `validationPattern`: Optional regex pattern for validation (advisory only)
- `tableDefinition`: Table structure for `expectedDataType: "table"` (see Table Fields below)

**All fields are optional.**

### Response Metadata

Optional metadata about the response (primarily for filled forms).

```json
{
  "inferredDataType": "date",
  "lastModified": "2025-11-12T14:31:00Z"
}
```

**Fields:**
- `inferredDataType`: Detected/inferred data type of response
- `lastModified`: ISO 8601 timestamp of last modification

## Data Types

Suggested values for `expectedDataType` and `inferredDataType`:

| Type | Description | Example |
|------|-------------|---------|
| `text` | Plain text | "John Doe" |
| `email` | Email address | "user@example.com" |
| `phone` | Phone number | "+1-555-0100" |
| `date` | Date | "2025-11-12" |
| `time` | Time | "14:30:00" |
| `datetime` | Date and time | "2025-11-12T14:30:00Z" |
| `number` | Numeric value | "42" |
| `url` | Web URL | "https://example.com" |
| `multiline` | Multi-line text | "Line 1\nLine 2" |
| `currency` | Currency amount | "99.99" |
| `boolean` | Yes/No | "true" or "yes" |
| `table` | Tabular data | See Table Fields section |

**Important**: These are hints only. Any visible text string is always valid as a response.

## Table Fields

For tabular data entry, use `expectedDataType: "table"` with a `tableDefinition` in the hints. Tables come in two modes:

### Fixed Tables

Fixed tables have predefined rows (e.g., tax years, categories). Users can enter data in cells but cannot add or remove rows.

```json
{
  "id": "prompt_income",
  "label": "Income by Tax Year",
  "response": "",
  "hints": {
    "expectedDataType": "table",
    "tableDefinition": {
      "columns": [
        { "id": "wages", "label": "Wages", "type": "currency" },
        { "id": "interest", "label": "Interest", "type": "currency" },
        { "id": "dividends", "label": "Dividends", "type": "currency" }
      ],
      "fixedRows": [
        { "id": "year_2024", "label": "2024" },
        { "id": "year_2023", "label": "2023" },
        { "id": "year_2022", "label": "2022" }
      ]
    }
  }
}
```

**Response format for fixed tables** (JSON object keyed by row ID):
```json
{
  "year_2024": { "wages": "75000.00", "interest": "150.00", "dividends": "500.00" },
  "year_2023": { "wages": "72000.00", "interest": "125.00", "dividends": "450.00" },
  "year_2022": { "wages": "68000.00", "interest": "100.00", "dividends": "400.00" }
}
```

### Dynamic Tables

Dynamic tables allow users to add and remove rows (e.g., line items, addresses).

```json
{
  "id": "prompt_order_items",
  "label": "Order Items",
  "response": "",
  "hints": {
    "expectedDataType": "table",
    "tableDefinition": {
      "columns": [
        { "id": "item", "label": "Item Description", "type": "text" },
        { "id": "qty", "label": "Quantity", "type": "number" },
        { "id": "price", "label": "Unit Price", "type": "currency" }
      ],
      "dynamicRows": {
        "minRows": 1,
        "maxRows": 50,
        "rowLabel": "Item"
      }
    }
  }
}
```

**Response format for dynamic tables** (JSON array):
```json
[
  { "item": "Widget A", "qty": "10", "price": "25.00" },
  { "item": "Widget B", "qty": "5", "price": "42.50" }
]
```

### Table Column Properties

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | Unique column identifier (required) |
| `label` | string | Display header text (required) |
| `type` | string | Cell data type: "text", "number", "currency", "date", "boolean" |
| `placeholder` | string | Placeholder text for cells |
| `suggestedValues` | string[] | Autocomplete suggestions for cells |
| `helpText` | string | Column help text |
| `width` | string | Width hint: "25%", "100px", or "*" (relative) |

### Fixed Row Properties

| Property | Type | Description |
|----------|------|-------------|
| `id` | string | Unique row identifier (required) |
| `label` | string | Display label for row header (required) |

### Dynamic Row Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `minRows` | number | 0 | Minimum number of rows |
| `maxRows` | number | 100 | Maximum number of rows |
| `rowLabel` | string | "Row" | Prefix for auto-generated row labels |

**Note**: Use either `fixedRows` OR `dynamicRows`, not both. Table responses are stored as JSON strings.

## ID Conventions

### Recommended ID Format

- **Sections**: `section_001`, `section_002`, etc.
- **Nested Sections**: `section_001_001`, `section_001_002`, etc.
- **Prompts**: `prompt_001`, `prompt_002`, etc.

**Rules:**
- IDs must be unique within their scope (all section IDs unique, all prompt IDs unique)
- IDs should be stable across versions (don't change when prompts are reordered)
- Use alphanumeric characters and underscores only

## Complete Example: Template

```json
{
  "version": "1.0",
  "documentType": "template",
  "metadata": {
    "title": "Employment Application",
    "description": "Standard employment application form",
    "created": "2025-11-12T00:00:00Z",
    "modified": "2025-11-12T00:00:00Z",
    "author": "HR Department",
    "templateId": "employment-app-v1",
    "version": "1.0"
  },
  "sections": [
    {
      "id": "section_001",
      "title": "Personal Information",
      "description": "Basic personal details",
      "sections": [
        {
          "id": "section_001_001",
          "title": "Name and Contact",
          "prompts": [
            {
              "id": "prompt_001",
              "label": "Full Legal Name",
              "response": "",
              "hints": {
                "placeholder": "First Middle Last",
                "expectedDataType": "text",
                "helpText": "Enter your name as it appears on government ID"
              },
              "responseMetadata": {}
            },
            {
              "id": "prompt_002",
              "label": "Email Address",
              "response": "",
              "hints": {
                "placeholder": "you@example.com",
                "expectedDataType": "email"
              },
              "responseMetadata": {}
            },
            {
              "id": "prompt_003",
              "label": "Phone Number",
              "response": "",
              "hints": {
                "placeholder": "+1-555-0100",
                "expectedDataType": "phone"
              },
              "responseMetadata": {}
            }
          ]
        }
      ],
      "prompts": [
        {
          "id": "prompt_004",
          "label": "Date of Birth",
          "response": "",
          "hints": {
            "placeholder": "YYYY-MM-DD",
            "expectedDataType": "date"
          },
          "responseMetadata": {}
        }
      ]
    },
    {
      "id": "section_002",
      "title": "Employment History",
      "prompts": [
        {
          "id": "prompt_005",
          "label": "Current Employer",
          "response": "",
          "hints": {
            "expectedDataType": "text"
          },
          "responseMetadata": {}
        },
        {
          "id": "prompt_006",
          "label": "Position/Title",
          "response": "",
          "hints": {
            "expectedDataType": "text",
            "suggestedValues": [
              "Software Engineer",
              "Manager",
              "Director",
              "Analyst"
            ]
          },
          "responseMetadata": {}
        }
      ]
    }
  ]
}
```

## Complete Example: Filled Form

```json
{
  "version": "1.0",
  "documentType": "filledForm",
  "metadata": {
    "title": "Employment Application",
    "templateId": "employment-app-v1",
    "templateVersion": "1.0",
    "filledBy": "John Doe",
    "filledDate": "2025-11-12T14:30:00Z",
    "modified": "2025-11-12T14:35:00Z"
  },
  "sections": [
    {
      "id": "section_001",
      "title": "Personal Information",
      "sections": [
        {
          "id": "section_001_001",
          "title": "Name and Contact",
          "prompts": [
            {
              "id": "prompt_001",
              "label": "Full Legal Name",
              "response": "John Michael Doe",
              "hints": {
                "expectedDataType": "text"
              },
              "responseMetadata": {
                "inferredDataType": "text",
                "lastModified": "2025-11-12T14:31:00Z"
              }
            },
            {
              "id": "prompt_002",
              "label": "Email Address",
              "response": "john.doe@example.com",
              "hints": {
                "expectedDataType": "email"
              },
              "responseMetadata": {
                "inferredDataType": "email",
                "lastModified": "2025-11-12T14:32:00Z"
              }
            }
          ]
        }
      ],
      "prompts": [
        {
          "id": "prompt_004",
          "label": "Date of Birth",
          "response": "1990-05-15",
          "hints": {
            "expectedDataType": "date"
          },
          "responseMetadata": {
            "inferredDataType": "date",
            "lastModified": "2025-11-12T14:33:00Z"
          }
        }
      ]
    }
  ]
}
```

## Digital Signatures

APR documents support cryptographic digital signatures for authenticity and integrity verification.

### Signature Structure

```json
{
  "signerName": "Signer's common name",
  "signerEmail": "signer@example.com",
  "signerOrganization": "Organization name",
  "signerOrganizationalUnit": "Department/division",
  "certificateIssuer": "Certificate Authority name",
  "certificateThumbprint": "SHA-256 hash of certificate",
  "signedAt": "2025-11-12T15:00:00Z",
  "signatureData": "Base64-encoded RSA signature",
  "signatureType": "template" | "filledForm",
  "hashAlgorithm": "SHA256",
  "signatureReason": "Optional explanation for signature"
}
```

### Signature Types

#### Template Signatures (`signatureType: "template"`)

- Applied by template publishers (e.g., IRS, HR department)
- Covers the template structure: sections, nested sections, prompts, labels
- Does NOT cover response values (templates have empty responses)
- Establishes authenticity: "This is the official form"

#### Form Signatures (`signatureType: "filledForm"`)

- Applied by the person who completed the form
- Covers all response values and the template reference
- Includes template signatures if present (creates trust chain)
- Establishes attestation: "I filled out the official form and attest to my responses"

### Trust Chain

When both template and form signatures are present, a trust chain is created:

```
IRS Template Signature
  └─> Establishes: "This is official IRS Form 1040"
        └─> User's Form Signature
              └─> Establishes: "I filled out the official IRS Form 1040"
```

### Signature Verification

Implementations should verify:

1. **Cryptographic validity**: Signature matches computed hash
2. **Certificate validity**: Certificate was valid at signing time
3. **Document integrity**: Content hasn't changed since signing
4. **Trust chain**: Template signatures (if any) are included in form signature

### Read-Only Requirement

**Signed filled forms MUST be read-only.** Once a form has been digitally signed (`formSignatures` is not empty), implementations must prevent editing to preserve signature validity.

Template signatures do not make forms read-only; users can still fill out a signed template.

## Validation Rules

While APR is flexible, implementations should validate:

1. **Version**: Must be present and recognized
2. **Document Type**: Must be "template" or "filledForm"
3. **IDs**: Must be unique within scope
4. **Structure**: Sections can contain nested sections and prompts (unlimited depth)
5. **Responses**: Always strings (or empty string)

**Invalid examples:**
```json
// ❌ Missing version
{ "documentType": "template" }

// ❌ Invalid documentType
{ "version": "1.0", "documentType": "other" }

// ❌ Non-string response
{ "response": 42 }  // Must be "42"
```

## Extensibility

Future versions may add optional fields. Implementations should:
- Ignore unknown fields gracefully
- Preserve unknown fields when re-serializing
- Check `version` field for compatibility

## MIME Type

Recommended: `application/vnd.apr+json`

Alternative: `application/json`

## Character Encoding

UTF-8 required.

## Line Endings

Platform-independent (LF, CRLF, or CR all acceptable).

## Whitespace

Whitespace in JSON is insignificant. Implementations may pretty-print or minify.

## Comments

JSON does not support comments. Use `description` fields for documentation.

## Version History

- **1.0** (2025-11-12): Initial specification

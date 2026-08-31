# APR Format Example Templates

This directory contains example templates demonstrating the Adaptive Prompt Response (APR) format with real-world forms.

## Overview

These templates showcase the flexibility and power of the APR format for representing various types of forms, from simple contact forms to complex government documents. Each template demonstrates different aspects of the format:

- **Hierarchical structure** (sections and subsections)
- **Data type hints** (email, date, number, URL, phone, etc.)
- **Suggested values** (dropdowns/autocomplete)
- **Help text** (field-level guidance)
- **Validation patterns** (regex-based validation)

## Template Files

### Simple Examples

#### `contact-intake-filled.aprf`
- **Purpose**: A completed, fictional form showing the template-to-filled lifecycle
- **Features**: `filledForm`, template provenance, filled-by/date metadata, and a suggested single choice
- **Use Case**: Safe fixture for testing save/reopen and import pipelines without real personal data

#### `simple-contact-form.apr`
- **Purpose**: Basic contact form demonstration
- **Complexity**: Simple (1 section, 3 prompts)
- **Features**: Email validation, text fields, multiline text area
- **Use Case**: Contact forms, feedback forms, simple data collection

#### `field-types-showcase.aprt`
- **Purpose**: Demonstrates all supported data types
- **Complexity**: Simple (examples of each data type)
- **Features**: Every registered type hint, including `select`, `multichoice`, range, file, color, and signature
- **Use Case**: Reference for data type validation and hints

### Business Forms

#### `employment-application.apr`
- **Purpose**: Complete job application form
- **Complexity**: Medium (4 sections with subsections)
- **Features**: Multi-level structure, various field types, suggested values
- **Use Case**: HR systems, job application portals

### Government Forms (IRS - Tax Forms)

#### `irs-form-w4-2024.aprt`
- **Title**: Employee's Withholding Certificate (2024)
- **Sections**: 6 (Steps 1-5 + Employer Section)
- **Prompts**: 30+
- **Complexity**: Medium-High
- **Features**:
  - Multiple jobs/spouse withholding calculations
  - Dependent claims with dollar amounts
  - Optional adjustments (subsections 4a-4c)
  - Digital signature with certification
- **Use Case**: Payroll systems, HR onboarding, tax withholding management
- **Instructions**: Employees fill Steps 1-5; employers complete employer section

#### `irs-form-1040-simplified.aprt`
- **Title**: U.S. Individual Income Tax Return (Simplified)
- **Sections**: 10 (Filing status through signature)
- **Prompts**: 60+
- **Complexity**: High
- **Features**:
  - Income calculation sections with subsections
  - Tax deductions and credits
  - Multiple dependent tracking
  - Payment reconciliation
  - Numeric calculations throughout
- **Use Case**: Tax preparation software, accounting systems
- **Note**: This is a simplified version focusing on common scenarios

#### `irs-form-w9-2024.aprt`
- **Title**: Request for Taxpayer Identification Number and Certification
- **Sections**: 5 (Parts I-III + Address + TIN)
- **Prompts**: 20+
- **Complexity**: Medium
- **Features**:
  - Federal tax classification selection
  - SSN or EIN entry with validation
  - Backup withholding certification
  - Name/TIN change notification
  - Legal certification under penalties of perjury
- **Use Case**: Contractor payment systems, vendor management, 1099 reporting
- **Instructions**: Complete when working as an independent contractor or vendor

### Government Forms (GSA - Security Clearance)

#### `gsa-sf86-sections.aprt`
- **Title**: Questionnaire for National Security Positions
- **Sections**: 6 (representing key sections of SF-86)
- **Prompts**: 50+
- **Complexity**: Very High
- **Features**:
  - Deep hierarchical structure (section → subsection → prompts)
  - Multiple residence and employment history entries
  - Reference collection (3 references required)
  - Citizenship verification
  - Complex data relationships
- **Use Case**: Security clearance applications, background investigation systems
- **Note**: This is a partial representation showing key sections; the actual SF-86 has 30+ sections

## File Extensions

The APR format uses three file extensions to distinguish between template and filled forms:

- **`.aprt`** - Template files (to be filled out)
- **`.aprf`** - Filled form files (with responses)
- **`.apr`** - Generic APR files (type determined by `documentType` field)

All examples in this directory use `.aprt` as they are templates.

## Using These Templates

### CLI Tool

Use the APR CLI tool to work with these templates:

```bash
# View template information
apr info examples/irs-form-w4-2024.aprt

# Validate a template
apr validate examples/employment-application.apr

# Show statistics
apr stats examples/gsa-sf86-sections.aprt

# Compare two versions
apr diff examples/original.apr examples/modified.apr

# Export responses
apr export examples/filled-form.aprf --format=csv --output=responses.csv
```

### Desktop Application

Open these templates in the PromptResponse Desktop application:

1. **File → Open Template for Editing** - Edit the template structure
2. **File → Open (Fill Out Form)** - Fill out the form with responses

### Programmatic Access

Load and work with templates programmatically:

```csharp
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

// Load template
var serializer = new AprJsonSerializer();
var json = File.ReadAllText("examples/irs-form-w4-2024.aprt");
var document = serializer.Deserialize(json);

// Validate
var validator = new DocumentValidator();
var result = validator.Validate(document);

// Fill out and save
document.DocumentType = DocumentType.FilledForm;
document.Sections[0].Prompts[0].Response = "John Smith";
File.WriteAllText("my-w4.aprf", serializer.Serialize(document));
```

## Template Design Best Practices

Based on these examples, here are best practices for creating APR templates:

### 1. Structure

- Use **sections** for major groupings (e.g., "Personal Information", "Employment History")
- Use **subsections** for logical sub-groupings within sections
- Keep prompt IDs unique across the entire document
- Use descriptive IDs (e.g., `firstName`, `section1_email`) for clarity

### 2. Metadata

- Provide clear, descriptive titles
- Include comprehensive descriptions
- Set appropriate `documentType` (Template vs FilledForm)
- Use meaningful `templateId` for version tracking
- Include author and creation date

### 3. Prompts

- Write clear, concise labels
- Provide helpful placeholders showing format examples
- Use appropriate `expectedDataType` for validation hints
- Add `helpText` for complex or important fields
- Use `suggestedValues` for constrained choices

### 4. Data Types

The format supports these data type hints (all advisory, never enforced):

- **text**: Plain text (default)
- **email**: Email addresses
- **date**: Dates in YYYY-MM-DD format
- **number**: Numeric values (integers or decimals)
- **url**: Web URLs
- **phone**: Phone numbers (various formats accepted)
- **multiline**: Multi-line text
- **select**: One suggested choice
- **multichoice**: Multiple suggested choices
- **currency**, **range**, **time**, **datetime**, **password**, **file**, **color**, **signature**: host-affordance hints demonstrated in `field-types-showcase.aprt`

For a cryptographically attested example, see
[`tests/Conformance/beta6/attestations/permit.cms.attestation.jsonc`](../tests/Conformance/beta6/attestations/permit.cms.attestation.jsonc).
- Custom types are allowed and will be ignored by validation

### 5. Validation

- Use `expectedDataType` for built-in validation
- Use `validationPattern` (regex) for custom formats
- Remember: All validation is **advisory only**
- Never block user input based on validation results

### 6. Accessibility

- Use descriptive labels for screen readers
- Provide help text for complex fields
- Maintain logical tab order (prompts in document order)
- Use clear section and subsection titles

## Creating Your Own Templates

### Starting from Scratch

Use the CLI tool to create a new template interactively:

```bash
apr new my-template.aprt
```

### Starting from an Example

Copy and modify an existing template:

```bash
cp examples/employment-application.apr my-custom-form.aprt
```

Then edit the JSON file directly or use the Desktop application.

### Template Structure

Minimum required structure:

```json
{
  "version": "1.0-beta.6",
  "documentType": "template",
  "metadata": {
    "title": "My Form",
    "templateId": "my-form-v1"
  },
  "sections": [
    {
      "id": "section1",
      "title": "Section Title",
      "prompts": [
        {
          "id": "prompt1",
          "label": "Question",
          "response": "",
          "hints": {},
          "responseMetadata": {
            "lastModified": null,
            "inferredDataType": null
          }
        }
      ],
      "subsections": []
    }
  ]
}
```

## Testing Templates

Always validate your templates:

```bash
# Structural validation
apr validate my-template.aprt

# View structure
apr info my-template.aprt

# Get statistics
apr stats my-template.aprt
```

## Common Use Cases

### Forms That Work Well with APR

✅ **Excellent Fit:**
- Job applications
- Survey forms
- Registration forms
- Government forms
- Data collection forms
- Interview questionnaires

✅ **Good Fit:**
- Tax forms
- Medical intake forms
- Insurance applications
- Legal documents with fields

⚠️ **Partial Fit:**
- Forms requiring calculations (can store formulas in help text)
- Forms with complex conditional logic (requires external implementation)
- Forms with file uploads (store file paths as text)

❌ **Poor Fit:**
- Rich document editing (use word processors instead)
- Graphical forms requiring precise layout
- Interactive dashboards
- Real-time collaborative editing

## Contributing Templates

To contribute new example templates:

1. Create a real-world, useful template
2. Ensure it follows best practices
3. Add comprehensive help text and hints
4. Validate it passes all checks
5. Document it in this README
6. Submit a pull request

## License

All example templates in this directory are provided under the AGPL-3.0-or-later license, same as the PromptResponse project.

## Support

- **Documentation**: See main project README
- **Issues**: Report at https://github.com/marctjones/promptresponse/issues
- **Format Specification**: See `docs/format-specification.md`

---

**Note**: These templates demonstrate format capabilities. Always verify that filled forms meet your specific requirements and comply with applicable regulations before submission to government agencies or other entities.

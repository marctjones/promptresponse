# PromptResponse Python Library

Python library for working with APR (Adaptive Prompt Response) forms. Create, fill, validate, and sign form documents programmatically.

## Features

- 📝 **Create Templates**: Build APR templates programmatically with a fluent API
- 📋 **Fill Forms**: Fill forms programmatically or interactively
- ✅ **Validation**: Validate document structure and data types
- 🔐 **Digital Signatures**: Sign and verify templates (optional)
- 💾 **JSON Serialization**: Load and save APR documents
- 🐍 **Pythonic API**: Type hints, dataclasses, and clean interfaces

## Installation

### Basic Installation

```bash
cd python
pip install -e .
```

### With Signature Support

For digital signature features, install with the `signatures` extra:

```bash
pip install -e ".[signatures]"
```

This adds the `cryptography` library for RSA signing and verification.

### Development Installation

```bash
pip install -e ".[dev,signatures]"
```

## Quick Start

### Creating a Template

```python
from promptresponse import TemplateBuilder, AprJsonSerializer

# Build a template using the fluent API
template = (
    TemplateBuilder("Contact Form", template_id="contact-v1")
    .set_description("Simple contact information form")
    .set_author("Your Organization")
    .add_section("Personal Information")
        .add_prompt("Full Name", expected_type="text")
        .add_prompt("Email", expected_type="email")
        .add_prompt("Phone", expected_type="phone")
        .done()
    .add_section("Message")
        .add_prompt("Subject", expected_type="text")
        .add_prompt("Message", expected_type="multiline")
        .done()
    .build()
)

# Save to file
AprJsonSerializer.save_file(template, "contact-form.aprt")
```

### Filling a Form

```python
from promptresponse import AprJsonSerializer, FormFiller

# Load template
template = AprJsonSerializer.load_file("contact-form.aprt")

# Fill with responses
responses = {
    "prompt_001": "Jane Smith",
    "prompt_002": "jane@example.com",
    "prompt_003": "+1-555-0123",
    "prompt_004": "Product Inquiry",
    "prompt_005": "I'd like to know more about your services."
}

filled_form = FormFiller.fill_form(
    template,
    responses,
    filled_by="Jane Smith"
)

# Save filled form
AprJsonSerializer.save_file(filled_form, "contact-form-filled.aprf")

# Check completion
completion = FormFiller.get_completion_percentage(filled_form)
print(f"Completion: {completion:.1f}%")
```

### Validating Documents

```python
from promptresponse import AprValidator

# Validate structure
result = AprValidator.validate(template)
if result.is_valid:
    print("✓ Validation passed")
else:
    for error in result.errors:
        print(f"  - {error}")

# Validate for publishing (requires signatures)
pub_result = AprValidator.validate_for_publishing(template)
```

### Signing Templates

```python
from promptresponse import TemplateSigner, SignatureVerifier

# Generate certificate and key
signer = TemplateSigner()
private_key, certificate = signer.generate_certificate(
    name="John Doe",
    email="john@example.com",
    organization="Example Corp"
)

# Sign template
signed_template = signer.sign_template(
    template,
    private_key,
    certificate,
    signer_name="John Doe",
    signer_email="john@example.com"
)

# Verify signature
verifier = SignatureVerifier()
is_valid, message = verifier.verify_template(signed_template)
print(message)
```

### Interactive Form Filling

```python
from promptresponse import FormFiller

# Fill form interactively via console
filled_form = FormFiller.fill_form_interactive(
    template,
    filled_by="Interactive User"
)
```

## API Reference

### Models

#### `AprDocument`
Root document class. Can be either a template or filled form.

**Properties:**
- `version: str` - APR format version (always "1.0")
- `document_type: DocumentType` - TEMPLATE or FILLED_FORM
- `sections: List[Section]` - List of sections
- `metadata: Optional[Metadata]` - Document metadata

**Methods:**
- `get_all_prompts() -> List[Prompt]` - Get all prompts (flattened)
- `get_prompt_by_id(prompt_id: str) -> Optional[Prompt]` - Find prompt by ID
- `get_completion_percentage() -> float` - Calculate % filled (0-100)

#### `Section`
Top-level section in a document.

**Properties:**
- `id: str` - Unique section ID
- `title: str` - Section title
- `description: Optional[str]` - Section description
- `prompts: List[Prompt]` - Section-level prompts
- `subsections: List[Subsection]` - Nested subsections

#### `Prompt`
A single question/field in a form.

**Properties:**
- `id: str` - Unique prompt ID
- `label: str` - Prompt label/question
- `response: str` - Response value (always a string)
- `hints: Optional[PromptHints]` - Type hints and guidance
- `response_metadata: ResponseMetadata` - Response tracking

### Serialization

#### `AprJsonSerializer`

**Static Methods:**
- `serialize(document: AprDocument) -> str` - Serialize to JSON string
- `deserialize(json_str: str) -> AprDocument` - Deserialize from JSON
- `load_file(file_path: Path) -> AprDocument` - Load from file
- `save_file(document: AprDocument, file_path: Path)` - Save to file

### Validation

#### `AprValidator`

**Static Methods:**
- `validate(document: AprDocument) -> ValidationResult` - Validate structure
- `validate_for_publishing(document: AprDocument) -> ValidationResult` - Validate for publishing

#### `ValidationResult`

**Properties:**
- `is_valid: bool` - Whether validation passed
- `errors: List[ValidationError]` - List of errors/warnings

### Signatures

#### `TemplateSigner`

**Methods:**
- `generate_certificate(name, email, ...) -> (bytes, bytes)` - Generate key pair
- `sign_template(document, private_key, certificate, ...) -> AprDocument` - Sign template

#### `SignatureVerifier`

**Methods:**
- `verify_template(document: AprDocument) -> (bool, str)` - Verify all signatures
- `verify_signature(document, signature) -> (bool, str)` - Verify single signature

### High-Level API

#### `TemplateBuilder`

Fluent API for building templates.

**Methods:**
- `set_description(description: str)` - Set template description
- `set_author(author: str)` - Set template author
- `set_version(version: str)` - Set template version
- `add_section(title: str, ...) -> SectionBuilder` - Add section
- `build() -> AprDocument` - Build final document

#### `SectionBuilder`

Builder for sections (returned by `add_section()`).

**Methods:**
- `add_prompt(label, expected_type, ...)` - Add prompt to section
- `add_subsection(title, ...) -> SubsectionBuilder` - Add subsection
- `done() -> TemplateBuilder` - Finish section, return to template

#### `FormFiller`

Helper for filling forms.

**Static Methods:**
- `fill_form(template, responses, filled_by) -> AprDocument` - Fill programmatically
- `fill_form_interactive(template, filled_by) -> AprDocument` - Fill interactively
- `get_completion_percentage(document) -> float` - Get completion %
- `get_empty_prompts(document) -> Dict[str, str]` - Get unfilled prompts

## Examples

See the `examples/` directory for complete working examples:

- `create_template.py` - Creating templates programmatically
- `fill_form.py` - Filling forms with data
- `sign_and_verify.py` - Digital signatures
- `interactive_fill.py` - Interactive console form filling

## Requirements

- Python 3.8+
- `cryptography` (optional, for signatures)

## Type Safety

This library uses Python type hints throughout. Run type checking with:

```bash
mypy python/promptresponse
```

## Testing

```bash
pytest python/tests
```

## License

MIT License - see LICENSE file for details.

## Related Projects

- [PromptResponse Desktop](../src/PromptResponse.Desktop/) - Cross-platform GUI (C#/Avalonia)
- [PromptResponse CLI](../src/PromptResponse.Cli/) - Command-line tool (C#)
- [APR Format Specification](../docs/FILE_FORMAT.md) - Full format documentation

# PromptResponse Rust Library

Rust library for working with APR (Adaptive Prompt Response) forms.

## Features

- 📝 **Create Templates**: Build APR templates with builder pattern
- 📋 **Fill Forms**: Fill forms programmatically
- ✅ **Validation**: Validate document structure
- 🔐 **Digital Signatures**: Sign and verify templates (optional, with `signatures` feature)
- 💾 **JSON Serialization**: Load and save APR documents with serde
- 🦀 **Idiomatic Rust**: Type-safe, zero-cost abstractions

## Requirements

- Rust 1.70+ (2021 edition)

## Installation

Add to your `Cargo.toml`:

```toml
[dependencies]
promptresponse = "0.1"
```

### With Signatures

```toml
[dependencies]
promptresponse = { version = "0.1", features = ["signatures"] }
```

## Quick Start

### Creating a Template

```rust
use promptresponse::TemplateBuilder;
use promptresponse::serialization;

let document = TemplateBuilder::new("Contact Form", "contact-v1")
    .description("Simple contact information form")
    .author("Your Organization")
    .section("Personal Information")
        .prompt("Full Name", "text")
        .prompt("Email", "email")
        .done()
    .section("Message")
        .prompt("Subject", "text")
        .prompt("Message", "multiline")
        .done()
    .build();

serialization::save_file(&document, "contact-form.aprt")?;
```

### Filling a Form

```rust
use promptresponse::{fill_form, get_completion_percentage};
use std::collections::HashMap;

let template = serialization::load_file("contact-form.aprt")?;

let mut responses = HashMap::new();
responses.insert("prompt_001".to_string(), "Jane Smith".to_string());
responses.insert("prompt_002".to_string(), "jane@example.com".to_string());

let filled_form = fill_form(&template, responses, Some("Jane Smith".to_string()))?;

serialization::save_file(&filled_form, "contact-form-filled.aprf")?;

let completion = get_completion_percentage(&filled_form);
println!("Completion: {:.1}%", completion);
```

### Validation

```rust
use promptresponse::validation;

let result = validation::validate(&document);
if result.is_valid {
    println!("✓ Valid");
} else {
    for error in &result.errors {
        println!("- {}: {}", error.field, error.message);
    }
}
```

### Digital Signatures (requires `signatures` feature)

```rust
use promptresponse::signatures::{TemplateSigner, SignatureVerifier};

// Generate certificate
let (private_key, certificate) = TemplateSigner::generate_certificate(
    "John Doe",
    "john@example.com",
    Some("Example Corp"),
)?;

// Sign template
let signer = TemplateSigner::new();
let signed_doc = signer.sign_template(
    &document,
    &private_key,
    &certificate,
    "John Doe",
    "john@example.com",
)?;

// Verify
let verifier = SignatureVerifier::new();
let (is_valid, message) = verifier.verify_template(&signed_doc)?;
println!("{}", message);
```

## API Reference

### Models

**Core Types:**
- `AprDocument` - Root document
- `Section` - Document section
- `Subsection` - Nested subsection
- `Prompt` - Single field/question
- `DocumentType` - Enum: Template, FilledForm
- `Metadata` - Document metadata

**Key Methods:**
```rust
// AprDocument
pub fn get_all_prompts(&self) -> Vec<&Prompt>
pub fn get_prompt_by_id(&self, prompt_id: &str) -> Option<&Prompt>
pub fn get_completion_percentage(&self) -> f64
```

### TemplateBuilder

Builder pattern for templates:

```rust
TemplateBuilder::new(title, template_id)
    .description(desc)
    .author(author)
    .version(version)
    .section(title)
        .prompt(label, type)
        .done()
    .build()
```

### API Functions

Form operations:

```rust
pub fn fill_form(
    template: &AprDocument,
    responses: HashMap<String, String>,
    filled_by: Option<String>,
) -> Result<AprDocument>

pub fn get_completion_percentage(document: &AprDocument) -> f64

pub fn get_empty_prompts(document: &AprDocument) -> HashMap<String, String>
```

### Serialization

JSON I/O:

```rust
pub fn serialize(document: &AprDocument) -> Result<String>
pub fn deserialize(json: &str) -> Result<AprDocument>
pub fn load_file<P: AsRef<Path>>(path: P) -> Result<AprDocument>
pub fn save_file<P: AsRef<Path>>(document: &AprDocument, path: P) -> Result<()>
```

### Validation

Validation functions:

```rust
pub fn validate(document: &AprDocument) -> ValidationResult
pub fn validate_for_publishing(document: &AprDocument) -> ValidationResult
```

### Signatures (requires `signatures` feature)

**TemplateSigner:**
```rust
pub fn generate_certificate(name, email, org) -> Result<(String, String)>
pub fn sign_template(doc, key, cert, name, email) -> Result<AprDocument>
```

**SignatureVerifier:**
```rust
pub fn verify_template(document: &AprDocument) -> Result<(bool, String)>
pub fn verify_signature(document, signature) -> Result<bool>
```

## Examples

Run examples:

```bash
cargo run --example create_template
cargo run --example fill_form
cargo run --example sign_verify --features signatures
```

See `examples/` directory for complete code.

## Features

- `default` - Core functionality only
- `signatures` - Digital signature support (adds ring, base64, pem)

## Dependencies

- **serde** - Serialization framework
- **serde_json** - JSON support
- **chrono** - Date/time handling
- **thiserror** - Error handling
- **ring** (optional) - Cryptography for signatures

## Testing

```bash
cargo test
cargo test --all-features
```

## Documentation

Generate and view docs:

```bash
cargo doc --open
cargo doc --all-features --open
```

## License

MIT License

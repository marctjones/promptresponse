# PromptResponse Java Library

Java library for working with APR (Adaptive Prompt Response) forms.

## Features

- 📝 **Create Templates**: Build APR templates with fluent builder API
- 📋 **Fill Forms**: Fill forms programmatically
- ✅ **Validation**: Validate document structure
- 🔐 **Digital Signatures**: Sign and verify templates (optional, requires Bouncy Castle)
- 💾 **JSON Serialization**: Load and save APR documents using Jackson
- ☕ **Java 11+**: Modern Java with streams and optionals

## Requirements

- Java 11 or later
- Maven 3.6+ or Gradle 7+

## Installation

### Maven

```xml
<dependency>
    <groupId>io.promptresponse</groupId>
    <artifactId>promptresponse</artifactId>
    <version>0.1.0</version>
</dependency>
```

### Gradle

```gradle
implementation 'io.promptresponse:promptresponse:0.1.0'
```

### Build from Source

```bash
cd java
mvn clean install
```

## Quick Start

### Creating a Template

```java
import io.promptresponse.api.TemplateBuilder;
import io.promptresponse.serialization.AprSerializer;
import io.promptresponse.models.*;

var document = new TemplateBuilder("Contact Form", "contact-v1")
    .setDescription("Simple contact information form")
    .setAuthor("Your Organization")
    .addSection("Personal Information")
        .addPrompt("Full Name", "text", "John Doe", null)
        .addPrompt("Email", "email", "john@example.com", null)
        .done()
    .addSection("Message")
        .addPrompt("Subject", "text", null, null)
        .addPrompt("Message", "multiline", null, null)
        .done()
    .build();

AprSerializer.saveFile(document, "contact-form.aprt");
```

### Filling a Form

```java
import io.promptresponse.api.FormFiller;
import java.util.Map;

var template = AprSerializer.loadFile("contact-form.aprt");

var responses = Map.of(
    "prompt_001", "Jane Smith",
    "prompt_002", "jane@example.com",
    "prompt_003", "Product Inquiry",
    "prompt_004", "I would like to know more..."
);

var filledForm = FormFiller.fillForm(template, responses, "Jane Smith");

AprSerializer.saveFile(filledForm, "contact-form-filled.aprf");

double completion = FormFiller.getCompletionPercentage(filledForm);
System.out.printf("Completion: %.1f%%\n", completion);
```

### Validation

```java
import io.promptresponse.validation.AprValidator;

var result = AprValidator.validate(document);
if (result.isValid()) {
    System.out.println("✓ Valid");
} else {
    for (var error : result.getErrors()) {
        System.out.println("- " + error.getField() + ": " + error.getMessage());
    }
}
```

### Digital Signatures

```java
import io.promptresponse.signatures.*;

// Generate certificate
var keyPair = TemplateSigner.generateCertificate(
    "John Doe", "john@example.com", "Example Corp"
);

// Sign template
var signer = new TemplateSigner();
var signedDoc = signer.signTemplate(
    document,
    keyPair.getPrivateKey(),
    keyPair.getCertificate(),
    "John Doe",
    "john@example.com"
);

// Verify
var verifier = new SignatureVerifier();
var verification = verifier.verifyTemplate(signedDoc);
System.out.println(verification.getMessage());
```

## API Reference

### Models

**Core Classes:**
- `AprDocument` - Root document
- `Section` - Document section
- `Subsection` - Nested subsection
- `Prompt` - Single field/question
- `DocumentType` - Enum: TEMPLATE, FILLED_FORM
- `Metadata` - Document metadata

**Key Methods:**
```java
// AprDocument
List<Prompt> getAllPrompts()
Optional<Prompt> getPromptById(String id)
double getCompletionPercentage()
```

### TemplateBuilder

Fluent API for building templates:

```java
new TemplateBuilder(title, templateId)
    .setDescription(desc)
    .setAuthor(author)
    .setVersion(version)
    .addSection(title)
        .addPrompt(label, type, placeholder, helpText)
        .done()
    .build();
```

### FormFiller

Static methods for form operations:

```java
FormFiller.fillForm(template, responses, filledBy)
FormFiller.getCompletionPercentage(document)
FormFiller.getEmptyPrompts(document)
```

### AprSerializer

JSON serialization:

```java
String json = AprSerializer.serialize(document);
AprDocument doc = AprSerializer.deserialize(json);
AprDocument doc = AprSerializer.loadFile("form.aprt");
AprSerializer.saveFile(document, "form.aprf");
```

### AprValidator

Validation:

```java
ValidationResult result = AprValidator.validate(document);
ValidationResult result = AprValidator.validateForPublishing(document);
```

### Signatures (requires Bouncy Castle)

**TemplateSigner:**
```java
KeyPair keys = TemplateSigner.generateCertificate(name, email, org)
AprDocument signed = signer.signTemplate(doc, privateKey, cert, name, email)
```

**SignatureVerifier:**
```java
VerificationResult result = verifier.verifyTemplate(document)
boolean isValid = verifier.verifySignature(document, signature)
```

## Examples

Build and run examples:

```bash
mvn compile exec:java -Dexec.mainClass="io.promptresponse.examples.CreateTemplate"
mvn compile exec:java -Dexec.mainClass="io.promptresponse.examples.FillForm"
mvn compile exec:java -Dexec.mainClass="io.promptresponse.examples.SignAndVerify"
```

## Dependencies

- **Jackson** (required) - JSON serialization
- **Bouncy Castle** (optional) - Digital signatures

## Testing

```bash
mvn test
```

## License

MIT License

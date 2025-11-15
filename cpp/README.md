# PromptResponse C++ Library

Header-only C++ library for working with APR (Adaptive Prompt Response) forms.

## Features

- 📝 **Create Templates**: Build APR templates programmatically with fluent API
- 📋 **Fill Forms**: Fill forms with data
- ✅ **Validation**: Validate document structure
- 🔐 **Digital Signatures**: Sign and verify templates (optional, requires OpenSSL)
- 💾 **JSON Serialization**: Load and save APR documents
- 🎯 **Header-Only**: Modern C++17 header-only library
- 🔧 **CMake Integration**: Easy integration with CMake projects

## Requirements

- C++17 or later
- [nlohmann/json](https://github.com/nlohmann/json) (auto-fetched by CMake)
- OpenSSL (optional, for signatures)

## Installation

### Using CMake FetchContent

```cmake
include(FetchContent)
FetchContent_Declare(promptresponse
    GIT_REPOSITORY https://github.com/marctjones/promptresponse
    GIT_TAG main
)
FetchContent_MakeAvailable(promptresponse)

target_link_libraries(your_target promptresponse)
```

### Manual Installation

```bash
cd cpp
mkdir build && cd build
cmake ..
cmake --build .
sudo cmake --install .
```

## Quick Start

### Creating a Template

```cpp
#include <promptresponse/api.hpp>
#include <promptresponse/serialization.hpp>

using namespace promptresponse;

auto document = TemplateBuilder("Contact Form", "contact-v1")
    .setDescription("Simple contact form")
    .setAuthor("Your Organization")
    .addSection("Personal Information")
        .addPrompt("Name", "text", "John Doe")
        .addPrompt("Email", "email", "john@example.com")
        .done()
    .build();

AprSerializer::saveFile(document, "contact-form.aprt");
```

### Filling a Form

```cpp
#include <promptresponse/api.hpp>
#include <promptresponse/serialization.hpp>

auto templateDoc = AprSerializer::loadFile("contact-form.aprt");

std::map<std::string, std::string> responses = {
    {"prompt_001", "Jane Smith"},
    {"prompt_002", "jane@example.com"}
};

auto filledForm = FormFiller::fillForm(templateDoc, responses, "Jane Smith");

AprSerializer::saveFile(filledForm, "contact-form-filled.aprf");

double completion = FormFiller::getCompletionPercentage(filledForm);
std::cout << "Completion: " << completion << "%\n";
```

### Validation

```cpp
#include <promptresponse/validation.hpp>

auto result = AprValidator::validate(document);
if (result.isValid) {
    std::cout << "✓ Valid\n";
} else {
    for (const auto& error : result.errors) {
        std::cout << "- " << error.field << ": " << error.message << "\n";
    }
}
```

### Digital Signatures

```cpp
#include <promptresponse/signatures.hpp>

// Generate certificate
auto [privateKey, certificate] = TemplateSigner::generateCertificate(
    "John Doe", "john@example.com", "Example Corp"
);

// Sign template
TemplateSigner signer;
auto signedDoc = signer.signTemplate(
    document, privateKey, certificate,
    "John Doe", "john@example.com"
);

// Verify
SignatureVerifier verifier;
auto [isValid, message] = verifier.verifyTemplate(signedDoc);
std::cout << message << "\n";
```

## API Reference

### Models

- `AprDocument` - Root document class
- `Section` - Document section
- `Subsection` - Nested subsection
- `Prompt` - Single question/field
- `DocumentType` - Template or FilledForm enum
- `Metadata` - Document metadata

### TemplateBuilder

Fluent API for building templates:

```cpp
auto doc = TemplateBuilder(title, templateId)
    .setDescription(desc)
    .setAuthor(author)
    .addSection(title)
        .addPrompt(label, type, placeholder, helpText)
        .addSubsection(title)
            .addPrompt(label, type)
            .done()
        .done()
    .build();
```

### FormFiller

Static methods for form filling:

- `fillForm(template, responses, filledBy)` - Fill form programmatically
- `getCompletionPercentage(document)` - Get completion %
- `getEmptyPrompts(document)` - Get unfilled prompts

### AprSerializer

Static methods for JSON I/O:

- `serialize(document, indent)` - To JSON string
- `deserialize(json)` - From JSON string
- `loadFile(path)` - Load from file
- `saveFile(document, path, indent)` - Save to file

### AprValidator

Static validation methods:

- `validate(document)` - Validate structure
- `validateForPublishing(document)` - Validate for publishing

### Signatures (requires OpenSSL)

**TemplateSigner:**
- `generateCertificate(name, email, org, days)` - Generate key pair
- `signTemplate(doc, key, cert, name, email)` - Sign template

**SignatureVerifier:**
- `verifyTemplate(document)` - Verify all signatures
- `verifySignature(document, signature)` - Verify single signature

## Examples

See `examples/` directory:

- `create_template.cpp` - Creating templates
- `fill_form.cpp` - Filling forms
- `sign_verify.cpp` - Digital signatures

Build examples:

```bash
mkdir build && cd build
cmake -DBUILD_EXAMPLES=ON ..
cmake --build .
./examples/create_template
```

## CMake Options

- `BUILD_EXAMPLES` - Build example programs (default: ON)
- `BUILD_TESTS` - Build tests (default: OFF)
- `WITH_SIGNATURES` - Enable signature support (default: ON, requires OpenSSL)

## Dependencies

- **nlohmann/json** (required) - JSON parsing
- **OpenSSL** (optional) - Digital signatures

## License

MIT License

// Example: Creating an APR template in C++
#include <promptresponse/api.hpp>
#include <promptresponse/serialization.hpp>
#include <promptresponse/validation.hpp>
#include <iostream>

using namespace promptresponse;

int main() {
    // Build a contact form template
    auto document = TemplateBuilder("Contact Form", "contact-v1")
        .setDescription("Simple contact information form")
        .setAuthor("Example Corp")
        .setVersion("1.0")
        .addSection("Personal Information")
            .addPrompt("Full Name", "text", "John Doe")
            .addPrompt("Email Address", "email", "john@example.com")
            .addPrompt("Phone Number", "phone", "+1-555-0100")
            .done()
        .addSection("Message", "Your message to us")
            .addPrompt("Subject", "text")
            .addPrompt("Message", "multiline")
            .done()
        .build();

    // Validate
    auto result = AprValidator::validate(document);
    if (result.isValid) {
        std::cout << "✓ Validation passed\n";
    } else {
        std::cout << "✗ Validation failed:\n";
        for (const auto& error : result.errors) {
            std::cout << "  - " << error.field << ": " << error.message << "\n";
        }
        return 1;
    }

    // Save to file
    std::string outputPath = "contact-form.aprt";
    AprSerializer::saveFile(document, outputPath);
    std::cout << "✓ Template saved to: " << outputPath << "\n";
    std::cout << "  Sections: " << document.sections.size() << "\n";
    std::cout << "  Total prompts: " << document.getAllPrompts().size() << "\n";

    return 0;
}

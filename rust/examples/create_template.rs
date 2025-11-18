//! Example: Creating an APR template in Rust

use promptresponse::{TemplateBuilder, validation, serialization};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // Build a contact form template
    let document = TemplateBuilder::new("Contact Form", "contact-v1")
        .description("Simple contact information form")
        .author("Example Corp")
        .version("1.0")
        .section("Personal Information")
            .prompt("Full Name", "text")
            .prompt("Email Address", "email")
            .prompt("Phone Number", "phone")
            .done()
        .section("Message")
            .prompt("Subject", "text")
            .prompt("Message", "multiline")
            .done()
        .build();

    // Validate
    let result = validation::validate(&document);
    if result.is_valid {
        println!("✓ Validation passed");
    } else {
        println!("✗ Validation failed:");
        for error in &result.errors {
            println!("  - {}: {}", error.field, error.message);
        }
        return Ok(());
    }

    // Save to file
    let output_path = "contact-form.aprt";
    serialization::save_file(&document, output_path)?;
    println!("✓ Template saved to: {}", output_path);
    println!("  Sections: {}", document.sections.len());
    println!("  Total prompts: {}", document.get_all_prompts().len());

    Ok(())
}

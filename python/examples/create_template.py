#!/usr/bin/env python3
"""
Example: Creating an APR template programmatically.
"""
from promptresponse import TemplateBuilder, AprJsonSerializer, AprValidator

# Create a contact form template
template = (
    TemplateBuilder("Contact Form", template_id="contact-v1")
    .set_description("Simple contact information form")
    .set_author("Example Corp")
    .set_version("1.0")
    .add_section("Personal Information")
        .add_prompt("Full Name", expected_type="text", placeholder="John Doe")
        .add_prompt("Email Address", expected_type="email", placeholder="john@example.com")
        .add_prompt("Phone Number", expected_type="phone", placeholder="+1-555-0100")
        .done()
    .add_section("Message", description="Your message to us")
        .add_prompt(
            "Subject",
            expected_type="text",
            help_text="Brief description of your inquiry"
        )
        .add_prompt(
            "Message",
            expected_type="multiline",
            help_text="Detailed message (be as specific as possible)"
        )
        .done()
    .build()
)

# Validate the template
result = AprValidator.validate(template)
print(result)

# Save to file
output_path = "contact-form.aprt"
AprJsonSerializer.save_file(template, output_path)
print(f"\n✓ Template saved to: {output_path}")
print(f"  Sections: {len(template.sections)}")
print(f"  Total prompts: {len(template.get_all_prompts())}")

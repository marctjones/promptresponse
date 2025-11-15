#!/usr/bin/env python3
"""
Example: Filling a form programmatically.
"""
from promptresponse import AprJsonSerializer, FormFiller

# Load a template
template = AprJsonSerializer.load_file("contact-form.aprt")

# Fill it with responses
responses = {
    "prompt_001": "Jane Smith",
    "prompt_002": "jane.smith@example.com",
    "prompt_003": "+1-555-0123",
    "prompt_004": "Product Inquiry",
    "prompt_005": "I would like to know more about your enterprise plans."
}

filled_form = FormFiller.fill_form(template, responses, filled_by="Jane Smith")

# Check completion
completion = FormFiller.get_completion_percentage(filled_form)
print(f"Form completion: {completion:.1f}%")

# Save filled form
output_path = "contact-form-filled.aprf"
AprJsonSerializer.save_file(filled_form, output_path)
print(f"✓ Filled form saved to: {output_path}")

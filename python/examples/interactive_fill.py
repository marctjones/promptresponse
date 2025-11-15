#!/usr/bin/env python3
"""
Example: Filling a form interactively via console input.
"""
from promptresponse import AprJsonSerializer, FormFiller

# Load a template
template = AprJsonSerializer.load_file("contact-form.aprt")

# Fill interactively
filled_form = FormFiller.fill_form_interactive(template, filled_by="Interactive User")

# Check completion
completion = FormFiller.get_completion_percentage(filled_form)
empty_prompts = FormFiller.get_empty_prompts(filled_form)

print(f"\n✓ Form completion: {completion:.1f}%")
if empty_prompts:
    print(f"  Empty prompts: {len(empty_prompts)}")
    for prompt_id, label in empty_prompts.items():
        print(f"    - {label}")

# Save filled form
output_path = "contact-form-interactive.aprf"
AprJsonSerializer.save_file(filled_form, output_path)
print(f"✓ Filled form saved to: {output_path}")

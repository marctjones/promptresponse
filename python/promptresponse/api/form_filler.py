"""
High-level API for filling APR forms.
"""
from datetime import datetime
from typing import Dict, Optional
from ..models import AprDocument, DocumentType
from ..serialization import AprJsonSerializer


class FormFiller:
    """Helper for filling out APR forms programmatically."""

    @staticmethod
    def fill_form(
        template: AprDocument,
        responses: Dict[str, str],
        filled_by: Optional[str] = None
    ) -> AprDocument:
        """
        Fill a template with responses.

        Args:
            template: The template document to fill
            responses: Dictionary mapping prompt IDs to response values
            filled_by: Name of person filling the form

        Returns:
            Filled form document

        Raises:
            ValueError: If template is not actually a template
        """
        if template.document_type != DocumentType.TEMPLATE:
            raise ValueError("Document must be a template")

        # Create a deep copy by serializing and deserializing
        json_str = AprJsonSerializer.serialize(template)
        filled_form = AprJsonSerializer.deserialize(json_str)

        # Convert to filled form
        filled_form.document_type = DocumentType.FILLED_FORM

        # Update metadata
        if filled_form.metadata:
            filled_form.metadata.filled_by = filled_by
            filled_form.metadata.filled_date = datetime.utcnow()
            filled_form.metadata.modified = datetime.utcnow()

        # Apply responses
        applied_count = 0
        missing_prompts = []

        for prompt_id, response_value in responses.items():
            prompt = filled_form.get_prompt_by_id(prompt_id)
            if prompt:
                prompt.response = response_value
                prompt.response_metadata.last_modified = datetime.utcnow()
                applied_count += 1
            else:
                missing_prompts.append(prompt_id)

        # Log warnings for missing prompts (could use logging module)
        if missing_prompts:
            print(f"Warning: {len(missing_prompts)} prompt(s) not found: {', '.join(missing_prompts)}")

        return filled_form

    @staticmethod
    def get_completion_percentage(document: AprDocument) -> float:
        """
        Calculate completion percentage of a form.

        Args:
            document: The form document

        Returns:
            Completion percentage (0-100)
        """
        return document.get_completion_percentage()

    @staticmethod
    def get_empty_prompts(document: AprDocument) -> Dict[str, str]:
        """
        Get all prompts that haven't been filled.

        Args:
            document: The form document

        Returns:
            Dictionary mapping prompt IDs to labels for empty prompts
        """
        empty = {}
        for prompt in document.get_all_prompts():
            if not prompt.response.strip():
                empty[prompt.id] = prompt.label
        return empty

    @staticmethod
    def fill_form_interactive(template: AprDocument, filled_by: Optional[str] = None) -> AprDocument:
        """
        Fill a form interactively via console input.

        Args:
            template: The template to fill
            filled_by: Name of person filling the form

        Returns:
            Filled form document
        """
        if template.document_type != DocumentType.TEMPLATE:
            raise ValueError("Document must be a template")

        print(f"\n=== {template.metadata.title if template.metadata else 'APR Form'} ===")
        print("(Press Enter to skip a field, Ctrl+C to cancel)\n")

        responses = {}

        for section in template.sections:
            print(f"\n--- {section.title} ---")
            if section.description:
                print(f"    {section.description}")
            print()

            # Section-level prompts
            for prompt in section.prompts:
                response = FormFiller._prompt_user(prompt)
                if response:
                    responses[prompt.id] = response

            # Subsection prompts
            for subsection in section.subsections:
                print(f"\n  -- {subsection.title} --")
                if subsection.description:
                    print(f"     {subsection.description}")
                print()

                for prompt in subsection.prompts:
                    response = FormFiller._prompt_user(prompt)
                    if response:
                        responses[prompt.id] = response

        return FormFiller.fill_form(template, responses, filled_by)

    @staticmethod
    def _prompt_user(prompt) -> str:
        """Prompt user for a single response."""
        label = prompt.label

        # Show placeholder if available
        if prompt.hints and prompt.hints.placeholder:
            print(f"\033[90m[{prompt.hints.placeholder}]\033[0m", end=" ")

        # Show help text if available
        if prompt.hints and prompt.hints.help_text:
            print(f"\n  ℹ  {prompt.hints.help_text}")
            response = input(f"  > {label}: ")
        else:
            response = input(f"{label}: ")

        return response.strip()

"""
Validation for APR documents.
"""
from dataclasses import dataclass
from typing import List, Set
from ..models import AprDocument, DocumentType, Section, Subsection, Prompt


@dataclass
class ValidationError:
    """Represents a validation error."""
    field: str
    message: str
    severity: str = "error"  # "error" or "warning"

    def __str__(self) -> str:
        return f"[{self.severity.upper()}] {self.field}: {self.message}"


@dataclass
class ValidationResult:
    """Result of document validation."""
    is_valid: bool
    errors: List[ValidationError]

    def __bool__(self) -> bool:
        """Allow using validation result as boolean."""
        return self.is_valid

    def __str__(self) -> str:
        if self.is_valid:
            return "✓ Validation passed"
        return f"✗ Validation failed with {len(self.errors)} error(s):\n" + \
               "\n".join(f"  - {error}" for error in self.errors)


class AprValidator:
    """Validator for APR documents."""

    @staticmethod
    def validate(document: AprDocument) -> ValidationResult:
        """
        Validate an APR document for structural correctness.

        Args:
            document: The document to validate

        Returns:
            ValidationResult with any errors found
        """
        errors: List[ValidationError] = []

        # Version validation
        if not document.version:
            errors.append(ValidationError("version", "Version is required"))
        elif document.version != "1.0":
            errors.append(ValidationError("version", f"Unsupported version: {document.version}"))

        # Document type validation
        if document.document_type not in DocumentType:
            errors.append(ValidationError("documentType", f"Invalid document type: {document.document_type}"))

        # Metadata validation (if present)
        if document.metadata:
            if document.document_type == DocumentType.TEMPLATE:
                if not document.metadata.template_id:
                    errors.append(ValidationError(
                        "metadata.templateId",
                        "Template must have a templateId",
                        "warning"
                    ))

            if document.document_type == DocumentType.FILLED_FORM:
                if not document.metadata.filled_by:
                    errors.append(ValidationError(
                        "metadata.filledBy",
                        "Filled form should have filledBy set",
                        "warning"
                    ))

        # Sections validation
        if not document.sections:
            errors.append(ValidationError("sections", "Document must have at least one section"))
        else:
            section_ids: Set[str] = set()
            for idx, section in enumerate(document.sections):
                errors.extend(AprValidator._validate_section(section, idx, section_ids))

        # Check if there are any actual errors (not just warnings)
        has_errors = any(e.severity == "error" for e in errors)

        return ValidationResult(is_valid=not has_errors, errors=errors)

    @staticmethod
    def _validate_section(section: Section, index: int, section_ids: Set[str]) -> List[ValidationError]:
        """Validate a section."""
        errors: List[ValidationError] = []
        prefix = f"sections[{index}]"

        # ID validation
        if not section.id:
            errors.append(ValidationError(f"{prefix}.id", "Section ID is required"))
        elif section.id in section_ids:
            errors.append(ValidationError(f"{prefix}.id", f"Duplicate section ID: {section.id}"))
        else:
            section_ids.add(section.id)

        # Title validation
        if not section.title:
            errors.append(ValidationError(f"{prefix}.title", "Section title is required"))

        # Validate prompts in section
        prompt_ids: Set[str] = set()
        for pidx, prompt in enumerate(section.prompts):
            errors.extend(AprValidator._validate_prompt(prompt, f"{prefix}.prompts[{pidx}]", prompt_ids))

        # Validate subsections
        subsection_ids: Set[str] = set()
        for sidx, subsection in enumerate(section.subsections):
            errors.extend(AprValidator._validate_subsection(
                subsection,
                f"{prefix}.subsections[{sidx}]",
                subsection_ids,
                prompt_ids
            ))

        return errors

    @staticmethod
    def _validate_subsection(
        subsection: Subsection,
        prefix: str,
        subsection_ids: Set[str],
        prompt_ids: Set[str]
    ) -> List[ValidationError]:
        """Validate a subsection."""
        errors: List[ValidationError] = []

        # ID validation
        if not subsection.id:
            errors.append(ValidationError(f"{prefix}.id", "Subsection ID is required"))
        elif subsection.id in subsection_ids:
            errors.append(ValidationError(f"{prefix}.id", f"Duplicate subsection ID: {subsection.id}"))
        else:
            subsection_ids.add(subsection.id)

        # Title validation
        if not subsection.title:
            errors.append(ValidationError(f"{prefix}.title", "Subsection title is required"))

        # Validate prompts in subsection
        for pidx, prompt in enumerate(subsection.prompts):
            errors.extend(AprValidator._validate_prompt(prompt, f"{prefix}.prompts[{pidx}]", prompt_ids))

        return errors

    @staticmethod
    def _validate_prompt(prompt: Prompt, prefix: str, prompt_ids: Set[str]) -> List[ValidationError]:
        """Validate a prompt."""
        errors: List[ValidationError] = []

        # ID validation
        if not prompt.id:
            errors.append(ValidationError(f"{prefix}.id", "Prompt ID is required"))
        elif prompt.id in prompt_ids:
            errors.append(ValidationError(f"{prefix}.id", f"Duplicate prompt ID: {prompt.id}"))
        else:
            prompt_ids.add(prompt.id)

        # Label validation
        if not prompt.label:
            errors.append(ValidationError(f"{prefix}.label", "Prompt label is required"))

        # Response is optional (can be empty string)
        # Type hints validation (warnings only)
        if prompt.hints:
            if prompt.hints.expected_data_type:
                valid_types = ['text', 'email', 'phone', 'number', 'date', 'multiline', 'url']
                if prompt.hints.expected_data_type not in valid_types:
                    errors.append(ValidationError(
                        f"{prefix}.hints.expectedDataType",
                        f"Unknown data type: {prompt.hints.expected_data_type}",
                        "warning"
                    ))

            # Min/max length validation
            if prompt.hints.min_length is not None and prompt.hints.min_length < 0:
                errors.append(ValidationError(
                    f"{prefix}.hints.minLength",
                    "minLength cannot be negative",
                    "warning"
                ))

            if prompt.hints.max_length is not None and prompt.hints.max_length < 0:
                errors.append(ValidationError(
                    f"{prefix}.hints.maxLength",
                    "maxLength cannot be negative",
                    "warning"
                ))

            if (prompt.hints.min_length is not None and
                    prompt.hints.max_length is not None and
                    prompt.hints.min_length > prompt.hints.max_length):
                errors.append(ValidationError(
                    f"{prefix}.hints",
                    "minLength cannot be greater than maxLength",
                    "warning"
                ))

        return errors

    @staticmethod
    def validate_for_publishing(document: AprDocument) -> ValidationResult:
        """
        Validate that a document is ready for publishing as a template.

        Args:
            document: The document to validate

        Returns:
            ValidationResult
        """
        errors: List[ValidationError] = []

        # Must be a template
        if document.document_type != DocumentType.TEMPLATE:
            errors.append(ValidationError(
                "documentType",
                "Document must be a template (not a filled form)"
            ))

        # Must have metadata
        if not document.metadata:
            errors.append(ValidationError("metadata", "Document must have metadata"))
        else:
            # Must have template ID
            if not document.metadata.template_id:
                errors.append(ValidationError(
                    "metadata.templateId",
                    "Template must have a templateId"
                ))

            # Must be signed
            if not document.metadata.template_signatures:
                errors.append(ValidationError(
                    "metadata.templateSignatures",
                    "Template must be digitally signed before publishing"
                ))

        # Run standard validation
        standard_result = AprValidator.validate(document)
        errors.extend(standard_result.errors)

        # Only fail on errors, not warnings
        has_errors = any(e.severity == "error" for e in errors)

        return ValidationResult(is_valid=not has_errors, errors=errors)

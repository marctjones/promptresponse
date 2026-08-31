"""Structural APR validation; advisory analysis lives in :mod:`advisories`."""

from typing import List

from .advisories import document_advisories
from .models import AprDocument, Section
from .validation_result import ValidationError, ValidationResult, ValidationWarning
from .versioning import is_supported_version

_DYNAMIC_TABLE = "table"


def validate(document: AprDocument) -> ValidationResult:
    """Check only structural errors; response-quality findings stay advisory."""
    result = ValidationResult()
    if document is None:
        result.errors.append(ValidationError("NULL_DOCUMENT", "No document.", ""))
        return result
    _validate_document_fields(document, result)
    section_ids: List[str] = []
    prompt_ids: List[str] = []
    for section in document.sections:
        _validate_section(section, "sections", result, section_ids, prompt_ids)
    _validate_unique_ids("section", section_ids, result)
    _validate_unique_ids("prompt", prompt_ids, result)
    result.warnings.extend(document_advisories(document))
    return result


def _validate_document_fields(document: AprDocument, result: ValidationResult) -> None:
    if not (document.version or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "version is required.", "version"))
    elif not is_supported_version(document.version):
        result.errors.append(ValidationError("UNSUPPORTED_VERSION", f"version {document.version!r} declares a major version this reader does not implement. A different major may mean anything (specification 1.3.1).", "version"))
    if not (document.metadata.title or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "metadata.title is required.", "metadata.title"))
    if not document.sections:
        result.errors.append(ValidationError("REQUIRED_FIELD", "A document must have at least one section.", "sections"))
    if document.document_type == "filledForm" and not (document.metadata.template_id or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "A filled form must record the templateId it answers.", "metadata.templateId"))


def _validate_section(section: Section, path: str, result: ValidationResult, section_ids: List[str], prompt_ids: List[str]) -> None:
    here = f"{path}[{section.id or '?'}]"
    if not (section.id or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "Section id is required.", here))
    if not (section.title or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "Section title is required.", f"{here}.title"))
    section_ids.append(section.id)
    if not section.prompts and not section.sections and section.kind != _DYNAMIC_TABLE:
        result.errors.append(ValidationError("EMPTY_SECTION", "A section must contain prompts or child sections.", here))
    for prompt in section.prompts:
        prompt_path = f"{here}.{prompt.id or '?'}"
        if not (prompt.id or "").strip():
            result.errors.append(ValidationError("REQUIRED_FIELD", "Prompt id is required.", prompt_path))
        if not (prompt.label or "").strip():
            result.errors.append(ValidationError("REQUIRED_FIELD", "Prompt label is required.", f"{prompt_path}.label"))
        prompt_ids.append(prompt.id)
    for child in section.sections:
        _validate_section(child, here, result, section_ids, prompt_ids)


def _validate_unique_ids(kind: str, identifiers: List[str], result: ValidationResult) -> None:
    seen = set()
    for identifier in identifiers:
        if identifier and identifier in seen:
            result.errors.append(ValidationError("DUPLICATE_ID", f"Duplicate {kind} id: {identifier}", identifier))
        seen.add(identifier)


__all__ = ["validate", "ValidationError", "ValidationResult", "ValidationWarning"]

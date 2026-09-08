"""Structural APR validation; advisory analysis lives in :mod:`advisories`."""

import unicodedata
from typing import List
from urllib.parse import urlparse

from .advisories import document_advisories
from .models import AprDocument, Section
from .validation_result import ValidationError, ValidationResult, ValidationWarning
from .versioning import is_supported_version

_DYNAMIC_TABLE = "table"
_NUMERIC_TYPES = {"number", "currency", "range"}
_TEMPORAL_TYPES = {"date", "time", "datetime"}


def validate(document: AprDocument) -> ValidationResult:
    """Check only structural errors; response-quality findings stay advisory."""
    result = ValidationResult()
    if document is None:
        result.errors.append(ValidationError("NULL_DOCUMENT", "No document.", ""))
        return result
    _validate_document_fields(document, result)
    _validate_shape(document, result)
    _validate_text_floor(document, result)
    _validate_hint_bounds(document, result)
    section_ids: List[str] = []
    prompt_ids: List[str] = []
    for section in document.sections:
        _validate_section(section, "sections", result, section_ids, prompt_ids)
    _validate_unique_ids("section", section_ids, result)
    _validate_unique_ids("prompt", prompt_ids, result)
    _validate_tables(document, result)
    result.warnings.extend(document_advisories(document))
    return result


def _walk_sections(sections, path):
    for index, section in enumerate(sections):
        here = f"{path}[{index}]"
        yield section, here
        yield from _walk_sections(section.sections, f"{here}.sections")


def _is_uri(value: str) -> bool:
    try:
        return bool(urlparse(value).scheme)
    except ValueError:
        return False


def _is_digest(value) -> bool:
    return (
        isinstance(value, str)
        and value.startswith("sha256:")
        and len(value) == len("sha256:") + 64
        and all(character in "0123456789abcdef" for character in value[len("sha256:"):])
    )


def _validate_shape(document: AprDocument, result: ValidationResult) -> None:
    """Members whose value has a shape the format states, not just a type (7.1)."""
    template = document.metadata.template_id
    if template and not _is_uri(template):
        result.errors.append(ValidationError(
            "WRONG_TYPE", f"templateId {template!r} is not a URI.", "metadata.templateId"))
    regarding = document.metadata.extra.get("regarding")
    if isinstance(regarding, list):
        for index, entry in enumerate(regarding):
            if not _is_digest(entry):
                result.errors.append(ValidationError(
                    "WRONG_TYPE", f"regarding entry {index} is not a digest.",
                    f"metadata.regarding[{index}]"))
    for section, path in _walk_sections(document.sections, "sections"):
        if section.max_rows is not None and section.max_rows < 1:
            result.errors.append(ValidationError(
                "WRONG_TYPE",
                f"maxRows is {section.max_rows}. A table always holds at least one "
                "instance, so a cap below one describes a table that cannot exist.",
                f"{path}.maxRows"))


# Below the human-facing text floor (specification 7.2): unassigned, a surrogate,
# private-use, a control other than tab and newline, or a code point UTS #39
# classifies as default-ignorable, deprecated or not-a-character.
_BELOW_FLOOR_CATEGORIES = {"Cc", "Cf", "Cs", "Co", "Cn"}
_BELOW_FLOOR_RANGES = (
    (0x00AD, 0x00AD), (0x061C, 0x061C), (0x180B, 0x180F), (0x200B, 0x200F),
    (0x202A, 0x202E), (0x2060, 0x206F), (0xFEFF, 0xFEFF), (0xFFF0, 0xFFF8),
    (0xFFFE, 0xFFFF), (0x1D173, 0x1D17A), (0xE0000, 0xE0FFF),
)


def _below_floor(character: str) -> bool:
    value = ord(character)
    if value in (0x09, 0x0A):
        return False
    if unicodedata.category(character) in _BELOW_FLOOR_CATEGORIES:
        return True
    if any(low <= value <= high for low, high in _BELOW_FLOOR_RANGES):
        return True
    return (value & 0xFFFE) == 0xFFFE


def _hold_to_the_floor(value, path: str, result: ValidationResult) -> None:
    # NFC is not separately checked here: text.normalize() already NFC-normalises
    # every field this walks, at parse time, so a source spelling that was not
    # NFC never survives to reach validate(). What can still reach it is a
    # default-ignorable or otherwise excluded code point normalize() does not
    # strip (a zero-width space, for one) -- that is what this still catches.
    if not value:
        return
    for character in value:
        if _below_floor(character):
            result.errors.append(ValidationError(
                "FORBIDDEN_CODE_POINT",
                f"Human-facing text carries U+{ord(character):04X}, which the format "
                "excludes: a character that renders as nothing can make one label look "
                "like another.", path))
            return  # One report names the member; listing every offender adds noise.


def _validate_text_floor(document: AprDocument, result: ValidationResult) -> None:
    """A response is not human-facing text in this sense; only titles and labels are."""
    _hold_to_the_floor(document.metadata.title, "metadata.title", result)
    _hold_to_the_floor(document.metadata.description, "metadata.description", result)
    _hold_to_the_floor(document.metadata.author, "metadata.author", result)
    _hold_to_the_floor(document.metadata.publisher, "metadata.publisher", result)
    for section, path in _walk_sections(document.sections, "sections"):
        _hold_to_the_floor(section.title, f"{path}.title", result)
        _hold_to_the_floor(section.description, f"{path}.description", result)
        for index, prompt in enumerate(section.prompts):
            _hold_to_the_floor(prompt.label, f"{path}.prompts[{index}].label", result)


def _validate_hint_bounds(document: AprDocument, result: ValidationResult) -> None:
    """A bound must be comparable in the space its field lives in (specification 4.7)."""
    for section, path in _walk_sections(document.sections, "sections"):
        for index, prompt in enumerate(section.prompts):
            declared = prompt.hints.expected_data_type
            if declared in _NUMERIC_TYPES:
                wanted, spelled = (int, float), "a number"
            elif declared in _TEMPORAL_TYPES:
                wanted, spelled = (str,), "a string"
            else:
                continue
            for bound_name in ("min", "max"):
                value = getattr(prompt.hints, bound_name)
                if value is None or isinstance(value, wanted):
                    continue
                result.errors.append(ValidationError(
                    "WRONG_TYPE",
                    f"hints.{bound_name} is not {spelled} on a {declared!r} field, "
                    "where the format declares a value comparable in that field's space.",
                    f"{path}.prompts[{index}].hints.{bound_name}"))


def _validate_tables(document: AprDocument, result: ValidationResult) -> None:
    for section, path in _walk_sections(document.sections, "sections"):
        if section.kind != _DYNAMIC_TABLE:
            continue
        rows = section.sections
        if not rows:
            result.errors.append(ValidationError(
                "EMPTY_TABLE",
                "A table section has no instances. A table always has at least one "
                "row; an empty one cannot describe its own fields.", path))
            continue
        first = rows[0].prompts
        for row in rows[1:]:
            prompts = row.prompts
            mismatch = len(prompts) != len(first)
            if mismatch:
                result.warnings.append(ValidationWarning(
                    "TABLE_RAGGED",
                    f"Table instance {row.id!r} has {len(prompts)} prompts but the "
                    f"first has {len(first)}; corresponding fields cannot be aligned "
                    "by position.", f"{path}.sections"))
            for index in range(len(prompts)):
                if mismatch:
                    break
                mismatch = prompts[index].label != first[index].label
            if mismatch:
                result.warnings.append(ValidationWarning(
                    "TABLE_LABEL_MISMATCH",
                    f"Table instance {row.id!r} does not name its fields as the first "
                    "instance does; corresponding fields should share a label.",
                    f"{path}.sections"))
        if section.max_rows and section.max_rows > 0 and len(rows) > section.max_rows:
            result.warnings.append(ValidationWarning(
                "TABLE_OVER_CAPACITY",
                f"Table has {len(rows)} instances, above the advisory maximum of "
                f"{section.max_rows}.", path))


def _validate_document_fields(document: AprDocument, result: ValidationResult) -> None:
    if not (document.version or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "aprVersion is required.", "aprVersion"))
    elif not is_supported_version(document.version):
        result.errors.append(ValidationError("UNSUPPORTED_VERSION", f"Unsupported APR version {document.version!r}; this build accepts only 1.0-beta.6.", "aprVersion"))
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

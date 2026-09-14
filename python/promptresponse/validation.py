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
    for index, role in enumerate(document.roles or []):
        if not (role.id or "").strip():
            result.errors.append(ValidationError("REQUIRED_FIELD", "A role entry names its id.", f"roles[{index}].id"))
    _validate_shape(document, result)
    _validate_text_floor(document, result)
    _check_confusable_script_mix(document, result)
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
    # Specification 8.2.3 places NON_NFC_TEXT and FORBIDDEN_CODE_POINT in the
    # warnings table (7.2), not the errors table (7.1, stated exhaustive by
    # APR-VAL-007): a validator MUST report them, but a warning MUST NOT affect
    # validity or block saving (APR-VAL-006, APR-VAL-002). Reporting them as
    # errors would reject a document the format requires to stay valid.
    #
    # 8.2.3 also requires a reader to preserve this text exactly (APR-TEXT-006) -- serialization.py no
    # longer runs these fields through text.normalize() at parse time, so a
    # non-NFC spelling or an excluded code point in the source survives to be
    # reported here instead of being silently cleaned away before anyone sees it.
    if not value:
        return
    if not unicodedata.is_normalized("NFC", value):
        result.warnings.append(ValidationWarning(
            "NON_NFC_TEXT",
            "Human-facing text must be in Normalization Form C; two spellings of "
            "one word are two different strings to everything that compares "
            "them.", path))
    for character in value:
        if _below_floor(character):
            # Not uniformly "renders as nothing": this category also holds ZWJ and
            # ZWNJ, load-bearing for correct glyph shaping in Persian, Hindi and
            # other scripts. Say what the rule is, not a rendering claim that's
            # false for part of the set it covers.
            result.warnings.append(ValidationWarning(
                "FORBIDDEN_CODE_POINT",
                f"Human-facing text carries U+{ord(character):04X}, which the "
                "human-facing text floor excludes.", path))
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


# Specification 8.2.3/APR-TEXT-012: "SHOULD apply the confusable and
# mixed-script detection of UTS #39... report what it finds." Full UTS #39
# restriction-level analysis needs a declared document language to avoid
# flagging ordinary multi-script text (Japanese Han+Hiragana+Katakana, Korean
# Hangul+Han, Latin loanwords in Indic/Arabic/Hebrew text) -- APR has no
# metadata.language member yet, so that full analysis isn't attempted here.
#
# What doesn't need a declared language: Latin, Cyrillic and Greek have
# extensive letter-shape homoglyphs between them (Cyrillic а/Latin a, Greek
# Α/Latin A) and essentially no legitimate reason to co-occur within one
# title or label -- unlike CJK/Hangul/Indic scripts, which routinely mix with
# Latin for brand names, loanwords and numerals. Flagging only these three
# scripts mixing with each other is a narrow, script-agnostic slice of UTS #39
# that produces zero known false positives on real multi-script text.
#
# Not in specification 7.2's warnings table -- CONFUSABLE_SCRIPT_MIX is this
# implementation's own spelling of an APR-VAL-002 "MAY surface any warning,
# including conditions this table does not name" extension, not a code every
# implementation must use. See the tracking issue for whether it should be
# proposed for formal registration once a language declaration exists to
# support the fuller check.
_CONFUSABLE_SCRIPTS = ("LATIN", "CYRILLIC", "GREEK")


def _confusable_script_of(character: str) -> str | None:
    if not unicodedata.category(character).startswith("L"):
        return None  # Not a letter: digits, punctuation and spaces are script-neutral.
    name = unicodedata.name(character, "")
    for script in _CONFUSABLE_SCRIPTS:
        if name.startswith(script):
            return script
    return None


def _check_field_for_confusable_mix(value, path: str, result: ValidationResult) -> None:
    if not value:
        return
    scripts = {_confusable_script_of(character) for character in value}
    scripts.discard(None)
    if len(scripts) > 1:
        result.warnings.append(ValidationWarning(
            "CONFUSABLE_SCRIPT_MIX",
            f"Mixes {', '.join(sorted(scripts)).title()} letters in one field; "
            "Latin, Cyrillic and Greek share look-alike letters, and a mix within "
            "one title or label is rarely intentional.", path))


def _check_confusable_script_mix(document: AprDocument, result: ValidationResult) -> None:
    """A response is not human-facing text in this sense; only titles and labels are."""
    _check_field_for_confusable_mix(document.metadata.title, "metadata.title", result)
    _check_field_for_confusable_mix(document.metadata.description, "metadata.description", result)
    _check_field_for_confusable_mix(document.metadata.author, "metadata.author", result)
    _check_field_for_confusable_mix(document.metadata.publisher, "metadata.publisher", result)
    for section, path in _walk_sections(document.sections, "sections"):
        _check_field_for_confusable_mix(section.title, f"{path}.title", result)
        _check_field_for_confusable_mix(section.description, f"{path}.description", result)
        for index, prompt in enumerate(section.prompts):
            _check_field_for_confusable_mix(prompt.label, f"{path}.prompts[{index}].label", result)


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

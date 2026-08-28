"""Validation, and the advisories that are not validation.

Specification 6.1 gives an exhaustive list of errors, and it is exhaustive on
purpose: no error may ever arise from the content of a response, or from the
state of a signature. A validator that rejects a document because an answer
looks wrong, or because a signature is missing or broken, is not implementing
APR (6.1, 9.5).

Everything a hint suggests lives in warnings instead - "this may not be what
you meant", never "you may not write this" (6.2).
"""

import re
from dataclasses import dataclass, field
from typing import List

from .models import AprDocument, Section
from .serialization import is_supported_version

# Sections whose child sections are repeating instances may legitimately start
# empty, because a dynamic table with no rows yet is still a table.
_DYNAMIC_TABLE = "table"


@dataclass
class ValidationError:
    code: str
    message: str
    path: str


@dataclass
class ValidationWarning:
    code: str
    message: str
    path: str


@dataclass
class ValidationResult:
    errors: List[ValidationError] = field(default_factory=list)
    warnings: List[ValidationWarning] = field(default_factory=list)

    @property
    def is_valid(self) -> bool:
        """Warnings never make a document invalid."""
        return not self.errors


def validate(document: AprDocument) -> ValidationResult:
    """Checks the structural rules, and only those (specification 6.1)."""
    result = ValidationResult()

    if document is None:
        result.errors.append(ValidationError("NULL_DOCUMENT", "No document.", ""))
        return result

    if not (document.version or "").strip():
        result.errors.append(ValidationError("REQUIRED_FIELD", "version is required.", "version"))
    elif not is_supported_version(document.version):
        result.errors.append(
            ValidationError(
                "UNSUPPORTED_VERSION",
                f"version {document.version!r} declares a major version this reader does "
                "not implement. A different major may mean anything (specification 1.3.1).",
                "version",
            )
        )
    if not (document.metadata.title or "").strip():
        result.errors.append(
            ValidationError("REQUIRED_FIELD", "metadata.title is required.", "metadata.title")
        )
    if not document.sections:
        result.errors.append(
            ValidationError("REQUIRED_FIELD", "A document must have at least one section.", "sections")
        )

    if document.document_type == "filledForm" and not (document.metadata.template_id or "").strip():
        result.errors.append(
            ValidationError(
                "REQUIRED_FIELD",
                "A filled form must record the templateId it answers.",
                "metadata.templateId",
            )
        )

    # Sections and prompts occupy separate id namespaces (specification 4.4), so
    # a section and a prompt may share an id without either being wrong.
    section_ids: List[str] = []
    prompt_ids: List[str] = []

    def walk(section: Section, path: str) -> None:
        here = f"{path}[{section.id or '?'}]"
        if not (section.id or "").strip():
            result.errors.append(ValidationError("REQUIRED_FIELD", "Section id is required.", here))
        if not (section.title or "").strip():
            result.errors.append(
                ValidationError("REQUIRED_FIELD", "Section title is required.", f"{here}.title")
            )
        section_ids.append(section.id)

        if not section.prompts and not section.sections and section.kind != _DYNAMIC_TABLE:
            result.errors.append(
                ValidationError(
                    "EMPTY_SECTION",
                    "A section must contain prompts or child sections.",
                    here,
                )
            )

        for prompt in section.prompts:
            ppath = f"{here}.{prompt.id or '?'}"
            if not (prompt.id or "").strip():
                result.errors.append(ValidationError("REQUIRED_FIELD", "Prompt id is required.", ppath))
            if not (prompt.label or "").strip():
                result.errors.append(
                    ValidationError("REQUIRED_FIELD", "Prompt label is required.", f"{ppath}.label")
                )
            prompt_ids.append(prompt.id)

        for child in section.sections:
            walk(child, here)

    for section in document.sections:
        walk(section, "sections")

    for name, ids in (("section", section_ids), ("prompt", prompt_ids)):
        seen = set()
        for identifier in ids:
            if identifier and identifier in seen:
                result.errors.append(
                    ValidationError(
                        "DUPLICATE_ID", f"Duplicate {name} id: {identifier}", identifier
                    )
                )
            seen.add(identifier)

    result.warnings.extend(_advisories(document))
    return result


def _looks_like(value: str, expected: str) -> bool:
    """A cheap shape check. Never used to reject anything."""
    checks = {
        "email": lambda v: "@" in v and "." in v.split("@")[-1],
        "number": lambda v: _is_number(v),
        "range": lambda v: _is_number(v),
        "currency": lambda v: _is_number(re.sub(r"[^0-9.eE+-]", "", v) or "x"),
        "date": lambda v: bool(re.match(r"^\d{4}-\d{2}-\d{2}", v)),
        "url": lambda v: v.startswith(("http://", "https://")),
        "boolean": lambda v: v.strip().lower() in {"true", "false", "yes", "no", "1", "0"},
    }
    check = checks.get(expected)
    return True if check is None else check(value)


def _is_number(value: str) -> bool:
    try:
        float(value)
        return True
    except ValueError:
        return False


def advisories_for(prompt) -> List[ValidationWarning]:
    """Advisories for a single prompt, for checking as somebody types.

    The same rules the whole-document pass applies, per field, so an interactive
    caller and a batch one can never disagree about whether an answer looks
    right. Every one is advisory: none of them makes a document invalid.
    """
    warnings: List[ValidationWarning] = []
    if not prompt.response:
        return warnings

    pattern = prompt.hints.validation_pattern
    if pattern:
        try:
            if not re.search(pattern, prompt.response):
                warnings.append(
                    ValidationWarning(
                        "PATTERN_MISMATCH",
                        f"{prompt.response!r} does not match the suggested pattern.",
                        prompt.id,
                    )
                )
                return warnings
        except re.error:
            warnings.append(
                ValidationWarning(
                    "PATTERN_MISMATCH", "The suggested pattern is not a valid regex.", prompt.id
                )
            )
            return warnings

    expected = prompt.hints.expected_data_type
    if expected and not _looks_like(prompt.response, expected):
        warnings.append(
            ValidationWarning(
                "TYPE_MISMATCH",
                f"{prompt.response!r} does not look like {expected!r} (advisory).",
                prompt.id,
            )
        )

    offered = prompt.hints.suggested_values
    if offered and prompt.response not in offered:
        warnings.append(
            ValidationWarning(
                "OUTSIDE_SUGGESTED",
                "Not one of the suggested options, which the format allows.",
                prompt.id,
            )
        )

    for code, message in _out_of_bounds(prompt):
        warnings.append(ValidationWarning(code, message, prompt.id))

    return warnings


def _out_of_bounds(prompt):
    """min and max are an offer, so falling outside one is advisory (specification 4.7)."""
    hints = prompt.hints
    if not (hints.min or hints.max):
        return

    try:
        value = float(prompt.response)
    except ValueError:
        return   # Not a number; the type advisory above already covers that.

    for bound, name, worse in ((hints.min, "minimum", lambda a, b: a < b),
                               (hints.max, "maximum", lambda a, b: a > b)):
        if not bound:
            continue
        try:
            limit = float(bound)
        except ValueError:
            continue
        if worse(value, limit):
            yield ("OUTSIDE_BOUNDS",
                   f"Outside the suggested {name} of {bound}. Bounds describe the control "
                   "offered, not a limit on the answer.")


def _advisories(document: AprDocument) -> List[ValidationWarning]:
    """Specification 6.2. Advisory in every case; none of these is an error."""
    warnings: List[ValidationWarning] = []
    for prompt in document.all_prompts():
        warnings.extend(advisories_for(prompt))
    return warnings

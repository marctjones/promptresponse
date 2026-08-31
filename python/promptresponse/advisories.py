"""Advisory-only response checks, shared by document and interactive callers."""

import re
from typing import List

from .unicode_security import inspect_text
from .validation_result import ValidationWarning


def advisories_for(prompt) -> List[ValidationWarning]:
    warnings = [
        ValidationWarning(finding.code, "Response contains a hidden or visually deceptive character "
                          f"({finding.description}) at offset {finding.offset}. It was preserved; verify it "
                          "was intentional.", prompt.id)
        for finding in inspect_text(prompt.response)
    ]
    if not prompt.response:
        return warnings
    pattern = prompt.hints.validation_pattern
    if pattern:
        try:
            if not re.search(pattern, prompt.response):
                return warnings + [ValidationWarning("PATTERN_MISMATCH", f"{prompt.response!r} does not match the suggested pattern.", prompt.id)]
        except re.error:
            return warnings + [ValidationWarning("PATTERN_MISMATCH", "The suggested pattern is not a valid regex.", prompt.id)]
    expected = prompt.hints.expected_data_type
    if expected and not _looks_like(prompt.response, expected):
        warnings.append(ValidationWarning("TYPE_MISMATCH", f"{prompt.response!r} does not look like {expected!r} (advisory).", prompt.id))
    if prompt.hints.suggested_values and prompt.response not in prompt.hints.suggested_values:
        warnings.append(ValidationWarning("OUTSIDE_SUGGESTED", "Not one of the suggested options, which the format allows.", prompt.id))
    warnings.extend(ValidationWarning(code, message, prompt.id) for code, message in _out_of_bounds(prompt))
    return warnings


def document_advisories(document) -> List[ValidationWarning]:
    return [warning for prompt in document.all_prompts() for warning in advisories_for(prompt)]


def _looks_like(value: str, expected: str) -> bool:
    checks = {"email": lambda v: "@" in v and "." in v.split("@")[-1], "number": _is_number,
              "range": _is_number, "currency": lambda v: _is_number(re.sub(r"[^0-9.eE+-]", "", v) or "x"),
              "date": lambda v: bool(re.match(r"^\d{4}-\d{2}-\d{2}", v)), "url": lambda v: v.startswith(("http://", "https://")),
              "boolean": lambda v: v.strip().lower() in {"true", "false", "yes", "no", "1", "0"}}
    check = checks.get(expected)
    return True if check is None else check(value)


def _is_number(value: str) -> bool:
    try:
        float(value)
        return True
    except ValueError:
        return False


def _out_of_bounds(prompt):
    hints = prompt.hints
    if not (hints.min or hints.max):
        return
    try:
        value = float(prompt.response)
    except ValueError:
        return
    for bound, name, worse in ((hints.min, "minimum", lambda a, b: a < b), (hints.max, "maximum", lambda a, b: a > b)):
        if not bound:
            continue
        try:
            limit = float(bound)
        except ValueError:
            continue
        if worse(value, limit):
            yield "OUTSIDE_BOUNDS", f"Outside the suggested {name} of {bound}. Bounds describe the control offered, not a limit on the answer."

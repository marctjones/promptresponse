"""Advisory-only response checks, shared by document and interactive callers."""

import re
from typing import Dict, Iterable, List

from .unicode_security import inspect_text
from .validation_result import ValidationWarning

# Mirrors schemas/apr-types-1.0.json. A copy, not a read of that file at run
# time -- src/PromptResponse.Core/Validation/AdvisoryVocabulary.cs keeps the
# same copy for the same reason.
_REGISTERED_TYPES = frozenset({
    "boolean", "color", "currency", "date", "datetime", "email", "multichoice",
    "multiline", "number", "password", "phone", "range", "select", "text", "time",
    "url",
})

_SUBMISSION_SCHEMES = frozenset({"https", "mailto"})


def advisories_for(prompt, roles) -> List[ValidationWarning]:
    warnings: List[ValidationWarning] = []
    if prompt.role and prompt.role not in roles:
        warnings.append(ValidationWarning(
            "UNDECLARED_ROLE", f"role {prompt.role!r} is not declared in roles.", prompt.id))
    warnings.extend(_inspect_extensions(prompt.extra, prompt.id))

    hints = prompt.hints
    declared = hints.expected_data_type
    if declared and declared not in _REGISTERED_TYPES:
        warnings.append(ValidationWarning(
            "UNREGISTERED_DATA_TYPE",
            f"expectedDataType {declared!r} is not in the type registry; an unrecognised "
            "type degrades to text and never rejects a response.", prompt.id))
    if hints.validation_pattern and not _compiles(hints.validation_pattern):
        warnings.append(ValidationWarning(
            "HINT_UNUSABLE", "validationPattern is not a valid regular expression, so "
            "nothing can apply it.", prompt.id))

    warnings.extend(
        ValidationWarning(finding.code, "Response contains a hidden or visually deceptive character "
                          f"({finding.description}) at offset {finding.offset}. It was preserved; verify it "
                          "was intentional.", prompt.id)
        for finding in inspect_text(prompt.response)
    )
    if not prompt.response:
        return warnings
    pattern = hints.validation_pattern
    if pattern:
        try:
            if not re.search(pattern, prompt.response):
                warnings.append(ValidationWarning(
                    "RESPONSE_PATTERN_MISMATCH", f"{prompt.response!r} does not match the suggested pattern.", prompt.id))
        except re.error:
            warnings.append(ValidationWarning(
                "RESPONSE_PATTERN_MISMATCH", "The suggested pattern is not a valid regex.", prompt.id))
    if declared and not _looks_like(prompt.response, declared):
        warnings.append(ValidationWarning(
            "RESPONSE_CONTRADICTS_TYPE", f"{prompt.response!r} does not look like {declared!r} (advisory).", prompt.id))
    if hints.suggested_values and prompt.response not in hints.suggested_values:
        warnings.append(ValidationWarning(
            "RESPONSE_OUTSIDE_SUGGESTED_VALUES",
            "Not one of the suggested options, which the format allows.", prompt.id))
    warnings.extend(ValidationWarning(code, message, prompt.id) for code, message in _out_of_bounds(prompt))
    return warnings


def document_advisories(document) -> List[ValidationWarning]:
    roles = {role.id for role in (document.roles or []) if role.id}
    warnings = list(_inspect_extensions(document.metadata.extra, "metadata"))
    warnings.extend(_inspect_submission(document.metadata.submission_urls or []))
    for section, path in _walk_sections(document.sections, "sections"):
        if section.kind != "table" and (section.max_rows is not None or section.can_add_rows is not None):
            warnings.append(ValidationWarning(
                "TABLE_MEMBERS_ON_A_PLAIN_SECTION",
                "maxRows or canAddRows on a section that is not a table; a table is a "
                "table only by carrying kind: \"table\".", path))
        if section.role and section.role not in roles:
            warnings.append(ValidationWarning(
                "UNDECLARED_ROLE", f"role {section.role!r} is not declared in roles.", path))
        warnings.extend(_inspect_extensions(section.extra, path))
        for prompt in section.prompts:
            warnings.extend(advisories_for(prompt, roles))
    return warnings


def _walk_sections(sections, path):
    for index, section in enumerate(sections):
        here = f"{path}[{index}]"
        yield section, here
        yield from _walk_sections(section.sections, f"{here}.sections")


def _inspect_extensions(extra: Dict, path: str) -> List[ValidationWarning]:
    return [
        ValidationWarning(
            "UNPREFIXED_MEMBER",
            f"unknown member {name!r} carries no reverse-DNS prefix; unprefixed names "
            "are reserved to the specification.", f"{path}.{name}")
        for name in extra
        if "." not in name
    ]


def _inspect_submission(urls: Iterable[str]) -> List[ValidationWarning]:
    warnings = []
    for index, url in enumerate(urls):
        scheme = url.split(":", 1)[0] if ":" in url else ""
        if scheme.lower() in _SUBMISSION_SCHEMES:
            continue
        warnings.append(ValidationWarning(
            "SUBMISSION_URL_UNSUPPORTED",
            f"submission entry {index} names the scheme {scheme!r}, which this document "
            "does not define; a reader offers the entries it understands.",
            f"metadata.submissionUrls[{index}]"))
    return warnings


def _compiles(pattern: str) -> bool:
    try:
        re.compile(pattern)
        return True
    except re.error:
        return False


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
            yield "RESPONSE_OUTSIDE_BOUNDS", f"Outside the suggested {name} of {bound}. Bounds describe the control offered, not a limit on the answer."

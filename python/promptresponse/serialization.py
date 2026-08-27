"""Reading and writing APR JSON.

Two rules do most of the work here, and both come straight from the format:

Responses are strings. A response given as a JSON number or boolean is a parse
failure and is never coerced to "42" - silent coercion would make the reader
disagree with the bytes it was handed (specification 3.2).

Unknown members survive. Anything this reader does not recognise is kept and
written back, so a document from a newer minor version is not quietly stripped
by an older reader (specification 4.8).
"""

import json
from typing import Any, Dict, List, Optional

from .errors import AprParseError, AprVersionError
from .models import (
    RETIRED_MEMBERS,
    AprDocument,
    Metadata,
    Prompt,
    PromptHints,
    ResponseMetadata,
    RoleDefinition,
    Section,
)
from .text import normalize

#: The format version this reader implements. Same major, so a newer minor is
#: accepted and its unknown members preserved (specification 1.3.1).
KNOWN_MAJOR = 1
KNOWN_MINOR = 0
CURRENT_VERSION = "1.0-beta"


def _require_object(value, what):
    if not isinstance(value, dict):
        raise AprParseError(f"{what} must be a JSON object, got {type(value).__name__}")
    return value


def _string(node, key, what):
    """A string member, or a parse failure. Numbers and booleans are refused."""
    if key not in node or node[key] is None:
        return None
    value = node[key]
    if not isinstance(value, str):
        raise AprParseError(
            f"{what}.{key} must be a string; got {type(value).__name__}. "
            "Every value in an APR document is a string (specification 3.2), and a "
            "reader must refuse rather than coerce."
        )
    return value


def _rest(node, taken):
    """Members this reader did not recognise, minus anything retired."""
    return {k: v for k, v in node.items() if k not in taken and k not in RETIRED_MEMBERS}


def _parse_hints(node) -> PromptHints:
    node = _require_object(node, "hints")
    suggested = node.get("suggestedValues", [])
    if suggested is None:
        suggested = []
    if not isinstance(suggested, list) or any(not isinstance(s, str) for s in suggested):
        raise AprParseError("hints.suggestedValues must be an array of strings")

    known = {
        "expectedDataType", "placeholder", "helpText", "validationPattern",
        "suggestedValues", "min", "max", "step",
        "exprHidden", "exprValue", "exprExpected", "exprValidation", "exprReadOnly",
    }
    return PromptHints(
        expected_data_type=_string(node, "expectedDataType", "hints"),
        placeholder=normalize(_string(node, "placeholder", "hints")),
        help_text=normalize(_string(node, "helpText", "hints")),
        validation_pattern=_string(node, "validationPattern", "hints"),
        suggested_values=[normalize(s) for s in suggested],
        min=_string(node, "min", "hints"),
        max=_string(node, "max", "hints"),
        step=_string(node, "step", "hints"),
        expr_hidden=_string(node, "exprHidden", "hints"),
        expr_value=_string(node, "exprValue", "hints"),
        expr_expected=_string(node, "exprExpected", "hints"),
        expr_validation=_string(node, "exprValidation", "hints"),
        expr_read_only=_string(node, "exprReadOnly", "hints"),
        extra=_rest(node, known),
    )


def _parse_response_metadata(node) -> ResponseMetadata:
    node = _require_object(node, "responseMetadata")
    known = {"inferredDataType", "source", "lastModified"}
    return ResponseMetadata(
        inferred_data_type=_string(node, "inferredDataType", "responseMetadata"),
        source=_string(node, "source", "responseMetadata"),
        last_modified=_string(node, "lastModified", "responseMetadata"),
        extra=_rest(node, known),
    )


def _parse_prompt(node) -> Prompt:
    node = _require_object(node, "prompt")
    known = {"id", "label", "response", "role", "hints", "responseMetadata"}
    return Prompt(
        id=_string(node, "id", "prompt") or "",
        label=normalize(_string(node, "label", "prompt")) or "",
        response=normalize(_string(node, "response", "prompt")) or "",
        role=_string(node, "role", "prompt"),
        hints=_parse_hints(node["hints"]) if node.get("hints") else PromptHints(),
        response_metadata=(
            _parse_response_metadata(node["responseMetadata"])
            if node.get("responseMetadata")
            else ResponseMetadata()
        ),
        extra=_rest(node, known),
    )


def _parse_section(node) -> Section:
    node = _require_object(node, "section")
    known = {
        "id", "title", "description", "kind", "canAddRows", "maxRows",
        "role", "prompts", "sections",
    }
    prompts = node.get("prompts") or []
    sections = node.get("sections") or []
    if not isinstance(prompts, list):
        raise AprParseError("section.prompts must be an array")
    if not isinstance(sections, list):
        raise AprParseError("section.sections must be an array")

    return Section(
        id=_string(node, "id", "section") or "",
        title=normalize(_string(node, "title", "section")) or "",
        description=normalize(_string(node, "description", "section")),
        kind=_string(node, "kind", "section"),
        can_add_rows=_string(node, "canAddRows", "section"),
        max_rows=_string(node, "maxRows", "section"),
        role=_string(node, "role", "section"),
        prompts=[_parse_prompt(p) for p in prompts],
        sections=[_parse_section(s) for s in sections],
        extra=_rest(node, known),
    )


def _parse_metadata(node) -> Metadata:
    node = _require_object(node, "metadata")
    known = {
        "title", "description", "author", "created", "modified", "templateId",
        "templateVersion", "filledBy", "filledDate", "publisher", "submissionUrl",
    }
    return Metadata(
        title=normalize(_string(node, "title", "metadata")) or "",
        description=normalize(_string(node, "description", "metadata")),
        author=normalize(_string(node, "author", "metadata")),
        created=_string(node, "created", "metadata"),
        modified=_string(node, "modified", "metadata"),
        template_id=_string(node, "templateId", "metadata"),
        template_version=_string(node, "templateVersion", "metadata"),
        filled_by=normalize(_string(node, "filledBy", "metadata")),
        filled_date=_string(node, "filledDate", "metadata"),
        publisher=normalize(_string(node, "publisher", "metadata")),
        # Deliberately not normalised: machine-consumed and signature-bound, so a
        # hidden character is reported rather than quietly cleaned to another host.
        submission_url=_string(node, "submissionUrl", "metadata"),
        extra=_rest(node, known),
    )


def is_supported_version(version: Optional[str]) -> bool:
    """Whether this reader implements the document's major version.

    Not a parse question. Specification 6.1 lists UNSUPPORTED_VERSION among the
    validation errors, and the corpus files unsupported-major.aprt under
    invalid/ - so such a document parses, and then fails validation. Refusing it
    at parse time would be the same mistake as refusing to open any flawed
    document: the reader could no longer show anybody what was wrong with it.
    """
    if not version:
        return False
    core = version.split("-", 1)[0]
    parts = core.split(".")
    if len(parts) != 2 or not all(p.isdigit() for p in parts):
        return False
    return int(parts[0]) == KNOWN_MAJOR


def loads(text: str) -> AprDocument:
    """Parses APR JSON. Raises :class:`AprParseError` if it is not a document."""
    try:
        node = json.loads(text)
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise AprParseError(f"not valid JSON: {exc}") from exc

    if not isinstance(node, dict):
        raise AprParseError(
            f"an APR document is a JSON object, not {type(node).__name__}"
        )

    for required in ("version", "metadata", "sections"):
        if required not in node:
            raise AprParseError(
                f"{required} is required. A document missing it is a structurally "
                "wrong shape, which is a parse failure rather than a validation "
                "error (specification 6.3)."
            )

    if not isinstance(node["sections"], list):
        raise AprParseError("sections must be an array")

    roles = node.get("roles")
    if roles is not None and not isinstance(roles, list):
        raise AprParseError("roles must be an array")

    known = {"version", "documentType", "metadata", "sections", "roles", "signatures"}
    return AprDocument(
        version=node["version"],
        document_type=_string(node, "documentType", "document"),
        metadata=_parse_metadata(node["metadata"]),
        sections=[_parse_section(s) for s in node["sections"]],
        roles=(
            [
                RoleDefinition(
                    id=_string(r, "id", "role") or "",
                    name=normalize(_string(r, "name", "role")),
                    description=normalize(_string(r, "description", "role")),
                    extra=_rest(_require_object(r, "role"), {"id", "name", "description"}),
                )
                for r in roles
            ]
            if roles is not None
            else None
        ),
        signatures=node.get("signatures"),
        extra=_rest(node, known),
    )


def load(path) -> AprDocument:
    """Reads a document from a file. UTF-8, tolerating a byte-order mark."""
    with open(path, "r", encoding="utf-8-sig") as handle:
        return loads(handle.read())


def _compact(node: Dict[str, Any]) -> Dict[str, Any]:
    """Drops absent members so a document does not carry empty noise."""
    return {k: v for k, v in node.items() if v is not None and v != [] and v != {}}


def _hints_json(hints: PromptHints) -> Dict[str, Any]:
    node = _compact({
        "expectedDataType": hints.expected_data_type,
        "placeholder": hints.placeholder,
        "helpText": hints.help_text,
        "validationPattern": hints.validation_pattern,
        "suggestedValues": hints.suggested_values,
        "min": hints.min,
        "max": hints.max,
        "step": hints.step,
        "exprHidden": hints.expr_hidden,
        "exprValue": hints.expr_value,
        "exprExpected": hints.expr_expected,
        "exprValidation": hints.expr_validation,
        "exprReadOnly": hints.expr_read_only,
    })
    node.update(hints.extra)
    return node


def _prompt_json(prompt: Prompt) -> Dict[str, Any]:
    # response is always written, empty or not. The reference implementation does,
    # and an explicitly empty response is data: "asked and left blank" is a
    # different document from "never asked", and round-tripping must not merge them.
    node: Dict[str, Any] = {"id": prompt.id, "label": prompt.label, "response": prompt.response}
    if prompt.role:
        node["role"] = prompt.role
    hints = _hints_json(prompt.hints)
    if hints:
        node["hints"] = hints
    meta = _compact({
        "inferredDataType": prompt.response_metadata.inferred_data_type,
        "source": prompt.response_metadata.source,
        "lastModified": prompt.response_metadata.last_modified,
    })
    meta.update(prompt.response_metadata.extra)
    if meta:
        node["responseMetadata"] = meta
    node.update(prompt.extra)
    return node


def _section_json(section: Section) -> Dict[str, Any]:
    node: Dict[str, Any] = {"id": section.id, "title": section.title}
    node.update(_compact({
        "description": section.description,
        "kind": section.kind,
        "canAddRows": section.can_add_rows,
        "maxRows": section.max_rows,
        "role": section.role,
    }))
    if section.prompts:
        node["prompts"] = [_prompt_json(p) for p in section.prompts]
    if section.sections:
        node["sections"] = [_section_json(s) for s in section.sections]
    node.update(section.extra)
    return node


def dumps(document: AprDocument, indent: int = 2) -> str:
    """Writes APR JSON, preserving every member this reader did not recognise."""
    metadata = {"title": document.metadata.title}
    metadata.update(_compact({
        "description": document.metadata.description,
        "author": document.metadata.author,
        "created": document.metadata.created,
        "modified": document.metadata.modified,
        "templateId": document.metadata.template_id,
        "templateVersion": document.metadata.template_version,
        "filledBy": document.metadata.filled_by,
        "filledDate": document.metadata.filled_date,
        "publisher": document.metadata.publisher,
        "submissionUrl": document.metadata.submission_url,
    }))
    metadata.update(document.metadata.extra)

    node: Dict[str, Any] = {"version": document.version}
    if document.document_type:
        node["documentType"] = document.document_type
    node["metadata"] = metadata
    if document.roles is not None:
        node["roles"] = [
            {**_compact({"id": r.id, "name": r.name, "description": r.description}), **r.extra}
            for r in document.roles
        ]
    node["sections"] = [_section_json(s) for s in document.sections]
    if document.signatures:
        node["signatures"] = document.signatures
    node.update(document.extra)

    return json.dumps(node, indent=indent, ensure_ascii=False)


def dump(document: AprDocument, path) -> None:
    """Writes a document to a file as UTF-8, with no byte-order mark."""
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(dumps(document))

"""APR beta.6 JSONC/YAML representation and independent-stream APIs."""

from dataclasses import dataclass
import json
import re
from typing import Any, Iterable, List, Union

import yaml

from .errors import AprParseError
from .models import AprDocument
from .serialization import dumps, loads

VERSION = "1.0-beta.6"


@dataclass(frozen=True)
class Beta6FormRecord:
    """One independent complete form occurrence in a beta.6 stream."""

    document: AprDocument
    # Parsed stream records retain their full semantic JSON object, including
    # extension members that the typed model does not project.
    value: dict[str, Any] | None = None


@dataclass(frozen=True)
class Beta6AttestationRecord:
    """One opaque attestation occurrence carried independently from forms."""

    value: dict[str, Any]


Beta6Record = Union[Beta6FormRecord, Beta6AttestationRecord]


def read_beta6_stream(source: str, representation: str) -> List[Beta6Record]:
    """Read every independent beta.6 occurrence without inferring relationships."""
    if representation == "jsonc":
        raw = [_strip_jsonc(record) for record in _split_jsonc(source)]
    elif representation == "yaml":
        try:
            _reject_yaml_features(source)
            raw = [json.dumps(document, ensure_ascii=False) for document in yaml.safe_load_all(source)]
        except yaml.YAMLError as exc:
            raise AprParseError(f"invalid APR YAML: {exc}") from exc
    else:
        raise ValueError("representation must be 'jsonc' or 'yaml'")
    return [_parse_record(record) for record in raw]


def read_beta6_form(source: str, representation: str) -> AprDocument:
    """Read one form, refusing to silently select a record from a stream."""
    records = read_beta6_stream(source, representation)
    if len(records) != 1 or not isinstance(records[0], Beta6FormRecord):
        raise AprParseError("APR_STREAM_REQUIRES_ITERATION")
    return records[0].document


def write_beta6_form(document: AprDocument, representation: str) -> str:
    """Write a beta.6 form in canonical JSONC or YAML source."""
    if document.version != VERSION:
        raise AprParseError(f"APR beta.6 writers require version {VERSION}")
    return _write_json(dumps(document), representation)


def write_beta6_stream(records: Iterable[Beta6Record], representation: str) -> str:
    """Write every independent stream occurrence in its supplied order."""
    encoded = []
    for record in records:
        json_text = json.dumps(record.value, ensure_ascii=False) if isinstance(record, Beta6FormRecord) and record.value is not None else write_beta6_form(record.document, "jsonc") if isinstance(record, Beta6FormRecord) else json.dumps(record.value, indent=2, ensure_ascii=False)
        encoded.append(_write_json(json_text, representation))
    return "".join("\x1e" + record + "\n" for record in encoded) if representation == "jsonc" else "---\n".join(encoded)


def _parse_record(raw: str) -> Beta6Record:
    try:
        value = json.loads(raw, object_pairs_hook=_unique_object)
    except (json.JSONDecodeError, TypeError) as exc:
        raise AprParseError(f"not valid beta.6 representation: {exc}") from exc
    if not isinstance(value, dict):
        raise AprParseError("an APR beta.6 record must be an object")
    if value.get("version") != VERSION:
        raise AprParseError(f"APR beta.6 records must declare version {VERSION}")
    if "recordType" in value:
        if value["recordType"] != "attestation":
            raise AprParseError("unknown APR beta.6 stream record type")
        _validate_attestation(value)
        return Beta6AttestationRecord(value)
    if "signatures" in value:
        raise AprParseError("RETIRED_EMBEDDED_SIGNATURES")
    return Beta6FormRecord(loads(json.dumps(value, ensure_ascii=False)), value)


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    """JSONC object names are unique; never allow parser last-key-wins behavior."""
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            raise AprParseError(f"APR JSONC object has duplicate member {key!r}")
        value[key] = item
    return value


def _validate_attestation(value: dict[str, Any]) -> None:
    """Reject records that cannot participate in beta.6 resolution."""
    subject = value.get("subject")
    if not isinstance(subject, dict) or not _digest(subject.get("digest")) or subject.get("canonicalization") != "jcs-sha256":
        raise AprParseError("beta.6 attestation requires subject.digest and jcs-sha256 canonicalization")
    scope = value.get("scope")
    if not isinstance(scope, dict) or scope.get("kind") not in {"document", "fields"}:
        raise AprParseError("beta.6 attestation scope.kind must be document or fields")
    if scope["kind"] == "fields" and (not isinstance(scope.get("fields"), list) or not scope["fields"] or any(not isinstance(field, str) or not field.strip() for field in scope["fields"])):
        raise AprParseError("beta.6 fields attestations require non-blank scope.fields")
    manifest = value.get("manifest")
    if not isinstance(manifest, dict) or not _digest(manifest.get("root")) or not isinstance(manifest.get("entries"), list):
        raise AprParseError("beta.6 attestation requires manifest.root and manifest.entries")
    for entry in manifest["entries"]:
        if not isinstance(entry, dict) or not isinstance(entry.get("path"), str) or not _digest(entry.get("digest")):
            raise AprParseError("beta.6 manifest entries require path and digest")
    if not isinstance(value.get("proofs"), list) or not isinstance(value.get("witnesses"), list) or any(not _digest(witness) for witness in value["witnesses"]):
        raise AprParseError("beta.6 attestations require proofs and digest witnesses arrays")


def _digest(value: Any) -> bool:
    return isinstance(value, str) and len(value) == 71 and value.startswith("sha256:") and all(character in "0123456789abcdef" for character in value[7:])


def _split_jsonc(source: str) -> List[str]:
    records = [record for record in source.split("\x1e") if record.strip()] if "\x1e" in source else [source]
    if not records:
        raise AprParseError("an APR JSONC stream has no records")
    return records


def _reject_yaml_features(source: str) -> None:
    if re.search(r"(?m)(?:^|[\s\[{,])(?:[&*!]|<<\s*:)", source):
        raise AprParseError("APR YAML forbids anchors, aliases, tags, and merge keys")


def _write_json(source: str, representation: str) -> str:
    if representation == "jsonc":
        return source
    if representation == "yaml":
        return yaml.safe_dump(json.loads(source), allow_unicode=True, sort_keys=False)
    raise ValueError("representation must be 'jsonc' or 'yaml'")


def _strip_jsonc(source: str) -> str:
    result: list[str] = []
    quote = escaped = False
    index = 0
    while index < len(source):
        char = source[index]
        if quote:
            result.append(char)
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                quote = False
            index += 1
            continue
        if char == '"':
            quote = True
            result.append(char)
        elif char == "/" and index + 1 < len(source) and source[index + 1] == "/":
            index = source.find("\n", index)
            if index < 0:
                break
            result.append("\n")
        elif char == "/" and index + 1 < len(source) and source[index + 1] == "*":
            end = source.find("*/", index + 2)
            if end < 0:
                raise AprParseError("unterminated JSONC comment")
            index = end + 1
        else:
            result.append(char)
        index += 1
    import re
    return re.sub(r",(\s*[}\]])", r"\1", "".join(result))

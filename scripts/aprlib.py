#!/usr/bin/env python3
"""Reference primitives for APR conformance tooling.

This module implements, directly from the specification, the few operations the
conformance tooling needs: reading the two representations, canonicalizing a
semantic model, and taking a semantic digest.

**It deliberately does not import any APR SDK.** Tooling that measured the
corpus with an SDK's own canonicalizer would report a corpus as consistent
whenever the SDK was consistently wrong, which is the one thing conformance
tooling must never do. Where this module and an SDK disagree, the specification
decides, and this module is small enough to read against it.

Implements:

* ``strip_jsonc``      APR-JSONC comment and trailing-comma removal (#apr-jsonc)
* ``load_yaml``        APR-YAML with the specification's scalar resolution
                       (#yaml-resolution), not a YAML library's default
* ``read_records``     record framing for both representations (#streams)
* ``canonicalize``     RFC 8785 JCS serialization (#digests)
* ``digest``           ``sha256:`` + hex SHA-256 of the JCS bytes (#digests)
* ``resolve_pointer``  RFC 6901 JSON Pointer, for manifest entries (#digests)
"""
from __future__ import annotations

import hashlib
import json
import math
import re

RS = "\x1e"
DIGEST_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
# RFC 8259 section 6. APR-YAML resolves a plain scalar as a number only when it
# matches this exactly; everything else that is not a literal stays a string.
JSON_NUMBER = re.compile(r"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$")
YAML_NULL = re.compile(r"^(~|null|Null|NULL|)$")
YAML_BOOL = re.compile(r"^(true|True|TRUE|false|False|FALSE)$")
NON_FINITE = re.compile(r"^[-+]?\.(inf|Inf|INF|nan|NaN|NAN)$")


class AprError(ValueError):
    """A document that the specification says must be rejected."""


# --------------------------------------------------------------------------
# APR-JSONC
# --------------------------------------------------------------------------

def strip_jsonc(text: str) -> str:
    """Remove comments and trailing commas. String contents are never touched.

    A `//` or `/*` inside a string literal is content, not a comment, which is
    why this is a small state machine rather than a regular expression.
    """
    out: list[str] = []
    quoted = escaped = False
    i = 0
    while i < len(text):
        ch = text[i]
        if quoted:
            out.append(ch)
            if escaped:
                escaped = False
            elif ch == "\\":
                escaped = True
            elif ch == '"':
                quoted = False
            i += 1
            continue
        if ch == '"':
            quoted = True
            out.append(ch)
        elif ch == "/" and i + 1 < len(text) and text[i + 1] == "/":
            j = text.find("\n", i)
            if j < 0:
                break
            out.append("\n")
            i = j
        elif ch == "/" and i + 1 < len(text) and text[i + 1] == "*":
            j = text.find("*/", i + 2)
            if j < 0:
                raise AprError("unterminated JSONC block comment")
            i = j + 1
        else:
            out.append(ch)
        i += 1
    return re.sub(r",(\s*[}\]])", r"\1", "".join(out))


def _no_duplicates(pairs):
    seen: dict = {}
    for key, value in pairs:
        if key in seen:
            raise AprError(f"duplicate member {key!r}")
        seen[key] = value
    return seen


def load_jsonc(text: str):
    return json.loads(strip_jsonc(text), object_pairs_hook=_no_duplicates)


# --------------------------------------------------------------------------
# APR-YAML
# --------------------------------------------------------------------------

def load_yaml(text: str) -> list:
    """Parse APR-YAML with the specification's scalar resolution.

    A YAML library's own resolution is wrong here in both 1.1 and 1.2: under
    either, `Yes` or `012` or a bare date stops being the string the semantic
    model requires. The target is the JSON value space, so resolution is done
    here from the specification's table and the library is used for syntax only.
    """
    try:
        import yaml
    except ImportError as exc:  # pragma: no cover - environment problem
        raise AprError("PyYAML is required to read APR-YAML") from exc

    class Loader(yaml.SafeLoader):
        pass

    def resolve_scalar(loader, node):
        value = node.value
        if node.style:  # any quoted scalar is a string, verbatim
            return value
        if NON_FINITE.match(value):
            raise AprError("YAML_NON_FINITE_NUMBER")
        if YAML_NULL.match(value):
            return None
        if YAML_BOOL.match(value):
            return value.lower() == "true"
        if JSON_NUMBER.match(value):
            return int(value) if re.fullmatch(r"-?(0|[1-9][0-9]*)", value) else float(value)
        return value

    Loader.add_constructor("tag:yaml.org,2002:str", resolve_scalar)
    for tag in ("null", "bool", "int", "float", "timestamp", "value", "merge"):
        Loader.add_constructor(f"tag:yaml.org,2002:{tag}", resolve_scalar)
    Loader.yaml_implicit_resolvers = {}

    for event in yaml.parse(text, Loader=yaml.SafeLoader):
        if isinstance(event, yaml.events.AliasEvent):
            raise AprError("YAML_ANCHOR_FORBIDDEN")
        anchor = getattr(event, "anchor", None)
        if anchor:
            raise AprError("YAML_ANCHOR_FORBIDDEN")
        # An explicit tag is one the document wrote, whether or not it happens to
        # name a standard type: `!!str` is as excluded as `!mine`. The tag string
        # cannot tell them apart, because resolution fills the same field in, so
        # the implicit flags decide. A scalar carries a (plain, quoted) pair and a
        # collection a single flag; either way, False means the document said it.
        implicit = getattr(event, "implicit", None)
        if isinstance(event, yaml.events.ScalarEvent):
            if implicit == (False, False):
                raise AprError("YAML_TAG_FORBIDDEN")
        elif isinstance(event, (yaml.events.SequenceStartEvent,
                                yaml.events.MappingStartEvent)):
            if implicit is False:
                raise AprError("YAML_TAG_FORBIDDEN")
    if re.search(r"(?m)^%(YAML|TAG)\b", text):
        raise AprError("YAML_DIRECTIVE_FORBIDDEN")
    if re.search(r"(?m)^\s*<<\s*:", text):
        raise AprError("YAML_MERGE_KEY_FORBIDDEN")

    return [doc for doc in yaml.load_all(text, Loader=Loader) if doc is not None]


# --------------------------------------------------------------------------
# Records
# --------------------------------------------------------------------------

def read_records(text: str, representation: str) -> list:
    """Every record in a document or stream, in order."""
    if representation == "yaml":
        return load_yaml(text)
    parts = [p for p in text.split(RS) if p.strip()] if RS in text else [text]
    return [load_jsonc(p) for p in parts]


def read_file(path) -> list:
    text = path.read_text(encoding="utf-8")
    return read_records(text, "yaml" if path.suffix in {".yaml", ".yml"} else "jsonc")


def is_attestation(record) -> bool:
    return isinstance(record, dict) and record.get("recordType") == "attestation"


# --------------------------------------------------------------------------
# Canonicalization and digests
# --------------------------------------------------------------------------

def _number(value) -> str:
    """A JSON number as ES6 Number::toString renders it, per RFC 8785 3.2.2.3."""
    if isinstance(value, bool):  # bool is an int subclass; never reached via dispatch
        raise AprError("boolean is not a number")
    if isinstance(value, int):
        return str(value)
    if not math.isfinite(value):
        raise AprError("non-finite numbers have no JSON representation")
    if value == int(value) and abs(value) < 1e21:
        return str(int(value))
    text = repr(value)  # shortest round-trip, as ES6 requires
    if "e" in text:  # Python writes 1e+21, ES6 writes 1e+21 too, but normalise e-05
        mantissa, exponent = text.split("e")
        sign = "+" if not exponent.startswith("-") else "-"
        text = f"{mantissa}e{sign}{int(exponent.lstrip('+-'))}"
    return text


def canonicalize(value) -> str:
    """RFC 8785 JCS serialization of a semantic model.

    Members are sorted by UTF-16 code unit, which differs from Python's default
    code-point order only for characters outside the Basic Multilingual Plane.
    """
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, (int, float)):
        return _number(value)
    if isinstance(value, str):
        return json.dumps(value, ensure_ascii=False)
    if isinstance(value, list):
        return "[" + ",".join(canonicalize(v) for v in value) + "]"
    if isinstance(value, dict):
        def utf16(key: str):
            return [ord(c) for c in key.encode("utf-16-be").decode("utf-16-be")] \
                if all(ord(c) < 0x10000 for c in key) \
                else list(key.encode("utf-16-be"))
        members = sorted(value.items(), key=lambda kv: kv[0].encode("utf-16-be"))
        return "{" + ",".join(f"{json.dumps(k, ensure_ascii=False)}:{canonicalize(v)}"
                              for k, v in members) + "}"
    raise AprError(f"{type(value).__name__} is not an APR value")


def digest(value) -> str:
    return "sha256:" + hashlib.sha256(canonicalize(value).encode("utf-8")).hexdigest()


def envelope_digest(attestation: dict) -> str:
    """The digest a proof signs and a witness names: the record without `proofs`."""
    return digest({k: v for k, v in attestation.items() if k != "proofs"})


# --------------------------------------------------------------------------
# JSON Pointer
# --------------------------------------------------------------------------

_MISSING = object()


def resolve_pointer(document, pointer: str):
    """RFC 6901. Returns ``_MISSING`` rather than raising, so a caller can report."""
    if pointer == "":
        return document
    if not pointer.startswith("/"):
        return _MISSING
    node = document
    for token in pointer[1:].split("/"):
        token = token.replace("~1", "/").replace("~0", "~")
        if isinstance(node, dict):
            if token not in node:
                return _MISSING
            node = node[token]
        elif isinstance(node, list):
            if not token.isdigit() or int(token) >= len(node):
                return _MISSING
            node = node[int(token)]
        else:
            return _MISSING
    return node


MISSING = _MISSING

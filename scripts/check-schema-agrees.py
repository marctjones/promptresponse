#!/usr/bin/env python3
"""Check the JSON Schema declares what the specification's member tables declare.

The specification is normative and the schema is a derived projection of its
structural subset. This makes that checkable: every member a specification table
names must exist in the schema with the same requiredness, and every member the
schema declares must appear in a table.

    python3 scripts/check-schema-agrees.py
    python3 scripts/check-schema-agrees.py --json

This is an agreement check rather than a generator. The tables carry member name,
type and requiredness; the schema also carries patterns, value domains,
additionalProperties, and the anyOf that makes a section require content. Those
have no column, and inventing columns for them would turn a document written for
people into a schema dialect. Agreement catches drift in both directions, which
is what naming the schema derived is actually for.
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
BASE = ROOT / "schemas" / "apr-1.0.schema.json"
BETA = ROOT / "schemas" / "apr-1.0-beta.6.schema.json"
TYPES = ROOT / "schemas" / "apr-types-1.0.json"

HEADING = re.compile(r"^#{2,4}\s+.*\{#([a-z0-9-]+)\}\s*$")
MEMBER = re.compile(r"^\|\s*`([A-Za-z0-9_.]+)`\s*\|([^|]*)\|([^|]*)\|")

# Specification table -> the schema definition it projects onto.
TABLES = {
    "root-object": ("<root>", None),
    "metadata": ("metadata", None),
    "section-object": ("section", None),
    "prompt-object": ("prompt", None),
    "hints-object": ("promptHints", None),
    "response-metadata": ("responseMetadata", None),
    "attestation-catalogue": ("<attestation>", None),
}

# Members the schema carries for a retired feature. The specification does not
# describe them because beta.6 retires embedded signatures.
RETIRED = {"signatures", "signature", "signer", "selfSigned", "thumbprint",
           "issuer", "signedAt", "cms", "algorithm", "canonicalization",
           "identifier", "subject", "name", "scope", "fields", "id", "role"}


def spec_members() -> dict[str, dict[str, bool]]:
    """Member name -> required, per specification table."""
    text = SPEC.read_text(encoding="utf-8")
    tables: dict[str, dict[str, bool]] = {}
    anchor = None
    for line in text.split("\n"):
        heading = HEADING.match(line)
        if heading:
            anchor = heading.group(1)
            continue
        if anchor not in TABLES:
            continue
        match = MEMBER.match(line)
        if not match:
            continue
        name, _type, required = match.group(1), match.group(2), match.group(3)
        # A dotted row documents a nested member; the parent row already covers
        # the member the schema declares at this level.
        if "." in name:
            continue
        tables.setdefault(anchor, {})[name] = "yes" in required.strip().lower()
    return tables


def schema_members() -> tuple[dict[str, dict[str, bool]], dict[str, set[str]]]:
    base = json.loads(BASE.read_text(encoding="utf-8"))
    beta = json.loads(BETA.read_text(encoding="utf-8"))
    out: dict[str, dict[str, bool]] = {}

    forbidden: dict[str, set[str]] = {}

    def collect(node: dict, key: str) -> None:
        properties = node.get("properties") or {}
        required = set(node.get("required") or [])
        # A property whose schema is literally false is a prohibition, not a
        # declaration: the schema is refusing the member, not offering it.
        out[key] = {
            name: name in required
            for name, definition in properties.items()
            if definition is not False
        }
        forbidden[key] = {
            name for name, definition in properties.items() if definition is False
        }

    collect(base, "<root>")
    for name, definition in (base.get("$defs") or {}).items():
        collect(definition, name)
    attestation = next(
        (branch for branch in beta.get("oneOf", [])
         if (branch.get("properties") or {}).get("recordType")),
        None,
    )
    if attestation:
        collect(attestation, "<attestation>")
    return out, forbidden


def check_type_registry(spec_text: str, hints: dict[str, bool], problems: list[str]) -> dict:
    """The published type vocabulary is a projection of the registry section."""
    types = json.loads(TYPES.read_text(encoding="utf-8"))
    summary: dict = {}

    # The registry section names the registered values in one sentence.
    match = re.search(
        r"`expectedDataType` registry: (.+?)\n\n", spec_text, re.DOTALL
    )
    documented = set(re.findall(r"`([a-z]+)`", match.group(1))) if match else set()
    published = {entry["id"] for entry in types["expectedDataType"]["types"]}
    summary["documentedTypes"] = len(documented)
    summary["publishedTypes"] = len(published)

    for name in sorted(documented - published):
        problems.append(f"type registry: the specification registers `{name}` and the file omits it")
    for name in sorted(published - documented):
        problems.append(f"type registry: the file publishes `{name}` and the specification does not register it")

    # Every hint a type calls meaningful must be a hint the specification declares.
    for entry in types["expectedDataType"]["types"]:
        for hint in entry.get("meaningfulHints", []):
            if hint not in hints:
                problems.append(
                    f"type registry: `{entry['id']}` names hint `{hint}`, which no specification table declares")

    # The published format version must be the one the specification describes.
    declared = re.search(r"\*\*Describes format version:\*\* `([^`]+)`", spec_text)
    if declared and types.get("formatVersion") != declared.group(1):
        problems.append(
            f"type registry: publishes formatVersion {types.get('formatVersion')!r}, "
            f"specification describes {declared.group(1)!r}")

    # An enumeration for a retired member is drift.
    retired_enums = sorted(
        key for key in types.get("enumeratedValues", {})
        if key.split(".")[0] in {"signature", "signer"}
    )
    for key in retired_enums:
        problems.append(
            f"type registry: enumerates `{key}`, which beta.6 retired with embedded signatures")
    summary["retiredEnumerations"] = retired_enums
    return summary


def main() -> int:
    spec = spec_members()
    schema, forbidden = schema_members()
    spec_text = SPEC.read_text(encoding="utf-8")
    problems: list[str] = []
    rows: list[tuple[str, int, int]] = []

    for anchor, (definition, _) in TABLES.items():
        declared = spec.get(anchor)
        if not declared:
            problems.append(f"#{anchor}: the specification has no member table")
            continue
        present = schema.get(definition)
        if present is None:
            problems.append(f"#{anchor}: the schema has no definition '{definition}'")
            continue

        rows.append((anchor, len(declared), len(present)))

        for name, required in declared.items():
            if name not in present:
                problems.append(
                    f"#{anchor}: the specification documents `{name}` and the schema does not declare it")
            elif present[name] != required:
                expected = "required" if required else "optional"
                actual = "required" if present[name] else "optional"
                problems.append(
                    f"#{anchor}: `{name}` is {expected} in the specification and {actual} in the schema")

        for name in present:
            if name in declared or name in RETIRED:
                continue
            problems.append(
                f"#{anchor}: the schema declares `{name}` and no specification table documents it")

    # A member the schema forbids must be a member the specification says is
    # forbidden, or the prohibition is unexplained.
    prohibitions = sorted({name for names in forbidden.values() for name in names})
    for name in prohibitions:
        if f"`{name}`" not in spec_text:
            problems.append(
                f"the schema forbids `{name}` and the specification never mentions it")

    types_summary = check_type_registry(spec_text, spec.get("hints-object", {}), problems)

    if "--json" in sys.argv:
        print(json.dumps({"tables": [
            {"anchor": a, "specMembers": s, "schemaMembers": c} for a, s, c in rows
        ], "problems": problems}, indent=2))
        return 1 if problems else 0

    print("Schema agreement with the specification's member tables")
    for anchor, declared, present in rows:
        print(f"  {anchor:24} specification {declared:2}   schema {present:2}")
    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nThe specification is normative: where they disagree the schema has the defect.")
        return 1
    print(f"\n  prohibited members, each explained in the specification: "
          f"{', '.join(prohibitions) or 'none'}")
    print(f"  type registry: {types_summary['publishedTypes']} published, "
          f"{types_summary['documentedTypes']} registered in the specification")
    print("\nEvery documented member is declared, and every declared member is documented.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

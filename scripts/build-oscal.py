#!/usr/bin/env python3
"""Project the APR specification's rules into an OSCAL catalog.

The specification is normative; this catalog is derived from it. Every rule
identifier in the specification becomes one OSCAL control, grouped by the
rule's area (`APR-MODEL-…`, `APR-EXPR-…`, and so on). Each control carries the
rule's normative text as its statement, a link to the section anchor it lives
under, and the gate strength the coverage registry (tests/registry.json)
records for it. Nothing is added that the specification does not say.

Use the catalog to build OSCAL profiles and assessment plans for checking an
implementation's completeness against the specification — the same rule
identifiers appear in the registry, the conformance checklist and here, so
a finding in an assessment names the same thing everywhere.

    python3 scripts/build-oscal.py            # verify the committed catalog is current
    python3 scripts/build-oscal.py --write    # regenerate it

The catalog is written to docs/release/apr-oscal-catalog.json. It follows the
OSCAL 1.1.2 catalog model. Its UUID is derived from the specification's
content, so an unchanged specification yields a byte-identical catalog.
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import re
import sys
import uuid

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
REGISTRY = ROOT / "tests" / "registry.json"
OUT = ROOT / "docs" / "release" / "apr-oscal-catalog.json"

OSCAL_VERSION = "1.1.2"
NAMESPACE = uuid.UUID("6f1a0c3e-0b7e-5b2a-9c3d-4e5f60718293")  # fixed per project, never changes

HEADING = re.compile(r"^(#{2,4})\s+(.*?)\s*\{#([a-z0-9-]+)\}\s*$")
RULE = re.compile(r"\[(APR-([A-Z]+)-(\d{3}))\]")
INLINE_ANCHOR = re.compile(r"\s*\{#[a-z0-9-]+\}")

AREA_TITLES = {
    "REP": "Representations",
    "MODEL": "Document model",
    "CONF": "Conformance",
    "VAL": "Validation",
    "TEXT": "Text handling",
    "STREAM": "Streams",
    "DIGEST": "Semantic digests and manifests",
    "EXPR": "Expressions",
    "ATTEST": "Attestations",
    "RENDER": "Rendering",
    "SEC": "Security",
}


def front_matter(text: str) -> dict[str, str]:
    facts = {}
    for line in text.split("\n")[:20]:
        m = re.match(r"^\*\*(.+?):\*\*\s+`?([^`\n]+)`?\s*$", line)
        if m:
            facts[m.group(1)] = m.group(2).strip()
    return facts


def rules(text: str) -> list[dict]:
    """Every rule identifier, with the paragraph or list item that carries it."""
    out = []
    anchor = None
    heading = None
    block: list[str] = []

    def flush() -> None:
        if not block:
            return
        para = " ".join(s.strip() for s in block).strip()
        for m in RULE.finditer(para):
            ident, area, number = m.group(1), m.group(2), m.group(3)
            statement = RULE.sub("", para)
            statement = INLINE_ANCHOR.sub("", statement)
            statement = re.sub(r"\s+", " ", statement).strip()
            statement = re.sub(r"^[-*]\s+|^\d+\.\s+|^\[ \]\s+", "", statement)
            if statement.startswith("|"):
                cells = [c.strip() for c in statement.strip("|").split("|")]
                statement = " — ".join(c for c in cells if c)
            out.append({
                "id": ident, "area": area, "number": int(number),
                "anchor": anchor, "heading": heading, "statement": statement,
            })
        block.clear()

    in_code = False
    for line in text.split("\n"):
        if line.startswith("```"):
            flush()
            in_code = not in_code
            continue
        if in_code:
            continue
        h = HEADING.match(line)
        if h:
            flush()
            heading, anchor = h.group(2), h.group(3)
            continue
        stripped = line.strip()
        if not stripped:
            flush()
            continue
        # A table row or a list item is its own block: rules there carry one
        # id each, and a rule in a member table must not inherit the whole table.
        if stripped.startswith("|"):
            flush()
            block.append(line)
            flush()
            continue
        if re.match(r"^([-*]|\d+\.)\s", stripped) and block:
            flush()
        block.append(line)
    flush()
    return out


def registry_index() -> dict[str, dict]:
    data = json.loads(REGISTRY.read_text(encoding="utf-8"))
    index: dict[str, dict] = {}
    for req in data.get("requirements", []):
        for rule in req.get("rules", []):
            index[rule] = req
    return index


def build() -> dict:
    text = SPEC.read_text(encoding="utf-8")
    facts = front_matter(text)
    spec_sha = hashlib.sha256(text.encode("utf-8")).hexdigest()
    reg = registry_index()

    groups: dict[str, dict] = {}
    for rule in sorted(rules(text), key=lambda r: (r["area"], r["number"])):
        group = groups.setdefault(rule["area"], {
            "id": f"apr-{rule['area'].lower()}",
            "title": AREA_TITLES.get(rule["area"], rule["area"].title()),
            "controls": [],
        })
        req = reg.get(rule["id"])
        props = [
            {"name": "rule-id", "value": rule["id"]},
            {"name": "section", "value": rule["anchor"] or ""},
            {"name": "gate-strength", "ns": "https://skpt.cl/apr/oscal",
             "value": (req or {}).get("strength", "none")},
        ]
        if req:
            props.append({"name": "requirement", "ns": "https://skpt.cl/apr/oscal", "value": req["id"]})
        control = {
            "id": rule["id"].lower(),
            "title": f"{rule['id']} — {rule['heading']}",
            "props": props,
            "links": [{"href": f"../APR_SPECIFICATION.md#{rule['anchor']}", "rel": "reference"}],
            "parts": [{"id": f"{rule['id'].lower()}_smt", "name": "statement", "prose": rule["statement"]}],
        }
        if req and req.get("gap"):
            control["parts"].append({"id": f"{rule['id'].lower()}_gap", "name": "assessment-gap", "prose": req["gap"]})
        group["controls"].append(control)

    catalog = {
        "catalog": {
            "uuid": str(uuid.uuid5(NAMESPACE, spec_sha)),
            "metadata": {
                "title": "APR File Format Specification — rule catalog",
                "last-modified": f"{facts.get('Published', '1970-01-01')}T00:00:00Z",
                "version": facts.get("Specification document version", "unknown"),
                "oscal-version": OSCAL_VERSION,
                "props": [
                    {"name": "format-version", "ns": "https://skpt.cl/apr/oscal", "value": facts.get("Describes format version", "")},
                    {"name": "specification-sha256", "ns": "https://skpt.cl/apr/oscal", "value": spec_sha},
                ],
                "remarks": "Derived from docs/APR_SPECIFICATION.md by scripts/build-oscal.py. The specification is normative; where this catalog disagrees with it, the catalog has the defect.",
            },
            "groups": [groups[k] for k in sorted(groups)],
        }
    }
    return catalog


def render(catalog: dict) -> str:
    return json.dumps(catalog, indent=2, ensure_ascii=False) + "\n"


def main() -> int:
    catalog = build()
    text = render(catalog)
    count = sum(len(g["controls"]) for g in catalog["catalog"]["groups"])
    if "--write" in sys.argv:
        OUT.write_text(text, encoding="utf-8")
        print(f"Wrote {OUT.relative_to(ROOT)}: {count} controls in {len(catalog['catalog']['groups'])} groups")
        return 0
    if not OUT.exists():
        print(f"{OUT.relative_to(ROOT)} is missing; run scripts/build-oscal.py --write")
        return 1
    if OUT.read_text(encoding="utf-8") != text:
        print(f"{OUT.relative_to(ROOT)} is stale; run scripts/build-oscal.py --write")
        return 1
    print(f"OSCAL catalog agrees with the specification: {count} controls")
    return 0


if __name__ == "__main__":
    sys.exit(main())

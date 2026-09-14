#!/usr/bin/env python3
"""Project the APR specification's rules into an OSCAL catalog.

The specification is normative; this catalog is derived from it. Every rule
identifier in the specification becomes one OSCAL control, grouped by the chapter
that states it. The area token in an identifier is an allocation namespace rather
than a taxonomy, and grouping by it put versioning and filename rules in a group
titled Security; the area is kept as a property on each control. Each control carries the
rule's normative text as its statement, a link to the section anchor it lives
under, and the gate strength the coverage registry (tests/registry.json)
records for it. Nothing is added that the specification does not say.

Each control also carries the conformance profile its section belongs to, read from
the table in section 3; its requirement level, the BCP 14 keyword it states; and its
subject, the class of product the requirement is on, or `document` for a requirement
on a form or record. scripts/build-conformance-statement.py reads those to list what
a claim covers, so the statement and this catalog cannot disagree.

Use the catalog to build OSCAL profiles and assessment plans for checking an
implementation's completeness against the specification — the same rule
identifiers appear in the registry, the conformance statement and here, so
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
KEYWORD = re.compile(r"\*\*(MUST NOT|MUST|SHOULD NOT|SHOULD|MAY|REQUIRED|OPTIONAL|"
                     r"NOT RECOMMENDED|RECOMMENDED|SHALL NOT|SHALL)\*\*")
# The classes of product Terminology defines. Anything else a requirement is on — a
# form, a section, an attestation record, a proof — is a document.
PRODUCTS = ("implementation", "reader", "writer", "validator", "renderer", "verifier", "host")
PRODUCT = re.compile(r"\b(" + "|".join(PRODUCTS) + r")s?\b", re.IGNORECASE)
REQUIREMENT_ON = re.compile(r"requirement on (?:a|an|the) ([a-z]+)", re.IGNORECASE)

# The area token in a rule identifier is an allocation namespace, not a taxonomy:
# APR-SEC-002 is the exact-match version rule, and only three of the twelve APR-SEC
# rules are about security. Grouping by it produced a group titled "Security" holding
# versioning and filename conventions, which would mislead anyone selecting on it.
# Controls are grouped by the chapter that states them instead.
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


def chapters(text: str) -> tuple[dict[str, str], list[str]]:
    """(section anchor -> its chapter, chapters in document order)."""
    out: dict[str, str] = {}
    order: list[str] = []
    chapter = "Document"
    in_code = False
    for line in text.split("\n"):
        if line.startswith("```"):
            in_code = not in_code
            continue
        if in_code:
            continue
        match = HEADING.match(line)
        if not match:
            continue
        if len(match.group(1)) == 2:
            chapter = match.group(2)
            if chapter not in order:
                order.append(chapter)
        out[match.group(3)] = chapter
    return out, order


def outline(text: str) -> tuple[dict[str, list[str]], list[str]]:
    """(section anchor -> its enclosing anchors, outermost first; chapter anchors in order)."""
    parents: dict[str, list[str]] = {}
    top: list[str] = []
    stack: list[tuple[int, str]] = []
    in_code = False
    for line in text.split("\n"):
        if line.startswith("```"):
            in_code = not in_code
            continue
        match = None if in_code else HEADING.match(line)
        if not match:
            continue
        level, anchor = len(match.group(1)), match.group(3)
        stack[:] = [(depth, name) for depth, name in stack if depth < level]
        parents[anchor] = [name for _, name in stack]
        stack.append((level, anchor))
        if level == 2:
            top.append(anchor)
    return parents, top


def profiles(text: str, top: list[str]) -> dict[str, str]:
    """Section anchor -> the profile section 3's table names it in.

    A row names sections by link, and "A through B" names every chapter from A to B.
    A section no row names inherits the profile of the section that contains it,
    which is what the specification says under the table.
    """
    start = text.index("| Profile | Defined by |")
    named: dict[str, str] = {}
    for row in text[start:].split("\n")[2:]:
        if not row.startswith("|"):
            break
        cells = [c.strip() for c in row.strip().strip("|").split("|")]
        profile = cells[0].strip("`")
        groups = [re.findall(r"\(#([a-z0-9-]+)\)", part) for part in re.split(r"\s+through\s+", cells[1])]
        anchors: list[str] = []
        for index, group in enumerate(groups):
            if index:
                first, last = top.index(groups[index - 1][-1]), top.index(group[0])
                anchors += top[first + 1:last]
            anchors += group
        for anchor in anchors:
            if anchor in named:
                raise SystemExit(f"section 3 names #{anchor} in both {named[anchor]} and {profile}")
            named[anchor] = profile
    return named


def subject_of(text: str) -> str | None:
    """The class of product a passage names, or `document` when it says a requirement is on a document."""
    plain = re.sub(r"`[^`]*`", "", text)
    explicit = REQUIREMENT_ON.search(plain)
    if explicit:
        word = explicit.group(1).lower()
        return word if word in PRODUCTS else "document"
    product = PRODUCT.search(plain)
    return product.group(1).lower() if product else None


def rules(text: str) -> list[dict]:
    """Every rule identifier, with the paragraph or list item that carries it."""
    out = []
    anchor = None
    heading = None
    lead: str | None = None
    block: list[str] = []

    def flush() -> None:
        nonlocal lead
        if not block:
            return
        para = " ".join(s.strip() for s in block).strip()
        row = para.startswith("|")
        if not row and not RULE.search(para):
            # The paragraph before a table says what its rows are requirements on.
            lead = subject_of(para) or lead
        for m in RULE.finditer(para):
            ident, area, number = m.group(1), m.group(2), m.group(3)
            statement = RULE.sub("", para)
            statement = INLINE_ANCHOR.sub("", statement)
            statement = re.sub(r"\s+", " ", statement).strip()
            statement = re.sub(r"^[-*]\s+|^\d+\.\s+|^\[ \]\s+", "", statement)
            if statement.startswith("|"):
                cells = [c.strip() for c in statement.strip("|").split("|")]
                statement = " — ".join(c for c in cells if c)
            keyword = KEYWORD.search(para)
            if row:
                subject = lead or "document"
            else:
                head = para[:keyword.start()] if keyword else para
                subject = subject_of(head) or "document"
            out.append({
                "id": ident, "area": area, "number": int(number),
                "anchor": anchor, "heading": heading, "statement": statement,
                "level": keyword.group(1) if keyword else None, "subject": subject,
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
            heading, anchor, lead = h.group(2), h.group(3), None
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

    where, order = chapters(text)
    parents, top = outline(text)
    named = profiles(text, top)
    ordered = sorted(rules(text), key=lambda r: (r["area"], r["number"]))
    problems = []
    for rule in ordered:
        rule["profile"] = next((named[a] for a in [rule["anchor"], *parents.get(rule["anchor"], [])[::-1]]
                                if a in named), None)
        if rule["profile"] is None:
            problems.append(f"{rule['id']} is in #{rule['anchor']}, which no row of section 3's table reaches")
        if rule["level"] is None:
            problems.append(f"{rule['id']} states no BCP 14 keyword")
    if problems:
        raise SystemExit("\n".join(problems))
    groups: dict[str, dict] = {}
    for rule in ordered:
        chapter = where.get(rule["anchor"], "Document")
        slug = re.sub(r"[^a-z0-9]+", "-", chapter.lower()).strip("-")
        group = groups.setdefault(chapter, {
            "id": f"apr-{slug}"[:60],
            "title": chapter,
            "controls": [],
        })
        req = reg.get(rule["id"])
        props = [
            {"name": "rule-id", "value": rule["id"]},
            {"name": "section", "value": rule["anchor"] or ""},
            {"name": "area", "ns": "https://skpt.cl/apr/oscal", "value": rule["area"]},
            {"name": "gate-strength", "ns": "https://skpt.cl/apr/oscal",
             "value": (req or {}).get("strength", "none")},
            {"name": "profile", "ns": "https://skpt.cl/apr/oscal", "value": rule["profile"]},
            {"name": "level", "ns": "https://skpt.cl/apr/oscal", "value": rule["level"]},
            {"name": "subject", "ns": "https://skpt.cl/apr/oscal", "value": rule["subject"]},
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
            # Document order, so the catalogue reads like the specification.
            "groups": [groups[k] for k in order if k in groups],
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

#!/usr/bin/env python3
"""Check the specification against the inventories that say what it must cover.

Completeness has a deterministic half and a judgement half. This is the
deterministic half: every concept the registry names has text at the anchor it
cites, every member the schema declares has a normative sentence, and every
conformance profile has a checklist. A script can answer all of those.

It cannot answer whether a rule is stated well enough to implement from, or
whether two sections contradict each other. That is left to the opt-in local
review, which this script narrows to the rules worth asking about.

    python3 scripts/check-spec-completeness.py
    python3 scripts/check-spec-completeness.py --review-surface
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
CONCEPTS = ROOT / "docs" / "CONCEPT_REGISTRY.md"
REGISTRY = ROOT / "tests" / "registry.json"
BASE = ROOT / "schemas" / "apr-1.0.schema.json"
BETA = ROOT / "schemas" / "apr-1.0-beta.6.schema.json"

RULE_ID = re.compile(r"\[(APR-[A-Z]+-\d{3})\]")
HEADING = re.compile(r"^(#{2,4})\s+.*\{#([a-z0-9-]+)\}\s*$", re.MULTILINE)

# Members that exist only for the embedded-signature model beta.6 retired.
RETIRED_MEMBERS = {
    "signatures", "signature", "signer", "selfSigned", "thumbprint", "issuer",
    "signedAt", "cms", "algorithm", "identifier",
}


def sections(spec: str) -> dict[str, str]:
    """Anchor -> section body, including its subsections.

    A chapter's content usually lives in its subsections, so ending a section at
    the next heading of any level makes every chapter look empty. A section runs
    until the next heading at the same or a higher level.
    """
    marks = [(m.start(), len(m.group(1)), m.group(2)) for m in HEADING.finditer(spec)]
    out: dict[str, str] = {}
    for index, (position, level, anchor) in enumerate(marks):
        end = len(spec)
        for later_position, later_level, _ in marks[index + 1:]:
            if later_level <= level:
                end = later_position
                break
        out[anchor] = spec[position:end]
    return out


def schema_member_names() -> set[str]:
    names: set[str] = set()

    def walk(node: object) -> None:
        if isinstance(node, dict):
            for key, value in (node.get("properties") or {}).items():
                if value is not False:
                    names.add(key)
                walk(value)
            for key in ("items", "additionalProperties"):
                walk(node.get(key))
            for key in ("oneOf", "allOf", "anyOf"):
                for branch in node.get(key, []):
                    walk(branch)
            for definition in (node.get("$defs") or {}).values():
                walk(definition)

    walk(json.loads(BASE.read_text(encoding="utf-8")))
    walk(json.loads(BETA.read_text(encoding="utf-8")))
    return names


def main() -> int:
    spec = SPEC.read_text(encoding="utf-8")
    body = sections(spec)
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    problems: list[str] = []
    lines: list[str] = []

    # 1. Every concept resolves, and its section actually says something.
    cited = re.findall(r"APR_SPECIFICATION\.md#([a-z0-9-]+)", CONCEPTS.read_text(encoding="utf-8"))
    unresolved = sorted({anchor for anchor in cited if anchor not in body})
    empty = sorted({a for a in cited if a in body and len(body[a].split()) < 25})
    for anchor in unresolved:
        problems.append(f"the concept registry cites #{anchor}, which the specification does not define")
    for anchor in empty:
        problems.append(f"the concept registry cites #{anchor}, which is barely more than a heading")
    lines.append(f"  concepts: {len(set(cited))} cited, all resolving to substantive sections"
                 if not (unresolved or empty) else
                 f"  concepts: {len(unresolved)} unresolved, {len(empty)} insubstantial")

    # 2. Every member the schema declares is described somewhere in the prose.
    members = schema_member_names()
    undocumented = sorted(
        name for name in members
        if name not in RETIRED_MEMBERS and f"`{name}`" not in spec
    )
    for name in undocumented:
        problems.append(f"the schema declares `{name}` and the specification never mentions it")
    lines.append(f"  schema members: {len(members)} declared, "
                 f"{len(members) - len(undocumented)} described, "
                 f"{len(RETIRED_MEMBERS & members)} retired and deliberately absent")

    # 3. Every conformance profile has a checklist a reader can work through.
    profiles = sorted(set(re.findall(r"`(core(?:\+[a-z]+)?)`", body.get("conformance", ""))))
    checklist = body.get("checklist", "")
    unlisted = [p for p in profiles if p not in checklist]
    if unlisted:
        problems.append(
            f"profiles with no checklist entry: {', '.join(unlisted)}. "
            "An implementer claiming one has nothing to work through.")
    lines.append(f"  conformance profiles: {len(profiles)} defined, "
                 f"{len(profiles) - len(unlisted)} with a checklist")

    # 4. The review surface: rules that no gated requirement covers.
    gated: set[str] = set()
    ungated: dict[str, str] = {}
    for requirement in registry["requirements"]:
        for rule in requirement.get("rules", []):
            if requirement["strength"] == "gated":
                gated.add(rule)
            else:
                ungated[rule] = requirement["id"]
    all_rules = set(RULE_ID.findall(spec))
    surface = sorted(rule for rule in all_rules if rule not in gated)
    lines.append(f"  rules: {len(all_rules)} stated, {len(gated)} gated, "
                 f"{len(surface)} carrying no executable gate")

    if "--review-surface" in sys.argv:
        print(json.dumps({
            "rules": surface,
            "owners": {rule: ungated.get(rule, "unclaimed") for rule in surface},
        }, indent=2))
        return 0

    print("Specification completeness against its inventories")
    for line in lines:
        print(line)

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    print("\nEvery concept, schema member and conformance profile is accounted for.")
    print(f"Rules without an executable gate are the review surface; "
          f"list them with --review-surface.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

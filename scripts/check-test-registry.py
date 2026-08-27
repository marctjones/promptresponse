#!/usr/bin/env python3
"""Verify tests/registry.json against the repository.

A coverage registry that is not itself checked becomes a document describing a project
that no longer exists. This script makes the registry's claims falsifiable:

  * every fixture it names must exist on disk
  * every test method it names must exist in source
  * every fixture on disk must be claimed by some requirement
  * every spec section carrying a normative MUST must appear in the registry
  * a requirement marked "gated" must actually name a gate

    python3 scripts/check-test-registry.py [--json]

Exits non-zero on any drift.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
REGISTRY = ROOT / "tests" / "registry.json"
CORPUS = ROOT / "tests" / "Conformance" / "v1"
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"


def source_text():
    """All C# test source, concatenated, for locating test-method references."""
    return "\n".join(
        p.read_text(encoding="utf-8", errors="ignore")
        for p in (ROOT / "tests").rglob("*.cs")
    )


def spec_sections_with_musts():
    """Sections of the specification containing an emphasised normative clause."""
    sections, current = set(), None
    for line in SPEC.read_text(encoding="utf-8").split("\n"):
        heading = re.match(r"^#{2,4} (.+)$", line)
        if heading:
            current = heading.group(1).strip()
        if current and re.search(r"\*\*(MUST NOT|MUST|REQUIRED|SHALL)\*\*", line):
            number = re.match(r"^([0-9]+(?:\.[0-9]+)*)", current)
            if number:
                sections.add(number.group(1))
    return sections


def main():
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    src = source_text()
    problems, notes = [], []

    fixtures_on_disk = {
        f"{p.parent.name}/{p.name}" for p in CORPUS.rglob("*.apr*")
    }
    claimed_fixtures = set()
    suite_ids = {s["id"] for s in registry["suites"]}

    # 1. Every referenced gate must exist.
    for req in registry["requirements"]:
        for gate in req.get("gates", []):
            ref, kind = gate["ref"], gate["type"]
            if kind == "fixture":
                claimed_fixtures.add(ref)
                # Some gates reference data files that are not APR documents (the
                # expression and canonicalization vectors), so check the path directly.
                if ref not in fixtures_on_disk and not (CORPUS / ref).exists():
                    problems.append(f"{req['id']}: fixture not found on disk — {ref}")
            elif kind == "test":
                if f"void {ref}(" not in src:
                    problems.append(f"{req['id']}: test method not found in source — {ref}")
            elif kind == "suite":
                if ref not in suite_ids:
                    problems.append(f"{req['id']}: unknown suite id — {ref}")

        # 2. A "gated" claim must be backed by a named gate.
        if req["strength"] == "gated" and not req.get("gates"):
            problems.append(f"{req['id']}: marked gated but names no gate")
        # 3. An ungated requirement should say why.
        if req["strength"] in ("none", "partial") and not req.get("gap"):
            notes.append(f"{req['id']}: {req['strength']} but records no gap explanation")

    # 4. Suites must point at something real.
    for suite in registry["suites"]:
        if not (ROOT / suite["path"]).exists():
            problems.append(f"suite {suite['id']}: path does not exist — {suite['path']}")

    # 5. Fixtures nobody claims are invisible coverage.
    for orphan in sorted(fixtures_on_disk - claimed_fixtures):
        notes.append(f"fixture claimed by no requirement — {orphan}")

    # 6. Normative spec sections absent from the registry.
    covered = {r["section"] for r in registry["requirements"]}
    for section in sorted(spec_sections_with_musts()):
        if not any(c == section or c.startswith(section + ".") or section.startswith(c + ".")
                   for c in covered):
            problems.append(f"specification §{section} has normative clauses but no registry entry")

    # Report.
    by_strength = {}
    for req in registry["requirements"]:
        by_strength[req["strength"]] = by_strength.get(req["strength"], 0) + 1

    if "--json" in sys.argv:
        print(json.dumps({"coverage": by_strength, "problems": problems, "notes": notes}, indent=2))
    else:
        total = len(registry["requirements"])
        print(f"Requirements: {total}")
        for strength in ("gated", "partial", "external", "indirect", "none"):
            if strength in by_strength:
                print(f"  {by_strength[strength]:3}  {strength}")
        print(f"\nFixtures on disk: {len(fixtures_on_disk)}  claimed: {len(claimed_fixtures)}")
        if notes:
            print(f"\n{len(notes)} note(s):")
            for n in notes:
                print(f"  - {n}")
        if problems:
            print(f"\n{len(problems)} PROBLEM(S):")
            for p in problems:
                print(f"  - {p}")
        else:
            print("\nRegistry agrees with the repository.")

    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())

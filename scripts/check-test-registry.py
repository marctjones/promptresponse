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
CORPUS = ROOT / "tests" / "Conformance" / "beta6"
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"


def source_text():
    """All registered test source, concatenated, for locating named gates."""
    return "\n".join(
        p.read_text(encoding="utf-8", errors="ignore")
        for root, pattern in ((ROOT / "tests", "*.cs"),
                              (ROOT / "python" / "tests", "*.py"),
                              (ROOT / "typescript" / "src" / "test", "*.ts"))
        if root.exists()
        for p in root.rglob(pattern)
    )


def spec_sections_with_musts():
    """Anchors of specification sections containing an emphasised normative clause.

    Keyed on the explicit {#anchor} each heading carries, not on its section
    number. The specification says anchors are its stable identifiers precisely
    so that inserting a section cannot silently repoint a registry entry at
    different prose; a number-keyed registry drifts on every edit, in silence.
    A heading with a normative clause and no anchor is itself reported, since it
    cannot be referenced stably.
    """
    sections, current, unanchored = set(), None, set()
    for line in SPEC.read_text(encoding="utf-8").split("\n"):
        heading = re.match(r"^#{2,4} (.+)$", line)
        if heading:
            current = heading.group(1).strip()
        # The full BCP 14 set, matching how rule identifiers are assigned. A
        # section whose only requirement is a SHOULD or a MAY still states a
        # requirement, and counting it differently here than there produced
        # sections that were normative for one check and not the other.
        if current and re.search(
            r"\*\*(MUST NOT|MUST|SHALL NOT|SHALL|REQUIRED|SHOULD NOT|SHOULD|MAY)\*\*", line
        ):
            anchor = re.search(r"\{#([A-Za-z0-9_-]+)\}", current)
            if anchor:
                sections.add(anchor.group(1))
            else:
                unanchored.add(current)
    return sections, unanchored


def placeholder_test_files():
    """Template test files indicate a test project was scaffolded but not finished."""
    return sorted((ROOT / "tests").rglob("UnitTest*.cs"))



def _declares(source: str, method: str) -> bool:
    """Does this source declare a test by that name?

    The registry spans two languages now, so this knows both: C# "void Name(" and
    "Task Name(" (async tests are ordinary), and Python "def name(". A checker that
    only knew one would report a perfectly good gate in the other as missing.
    """
    return any(f"{prefix} {method}(" in source
               for prefix in ("void", "Task", "def", "function"))


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
                # Two accepted forms. A bare method name is searched across every test
                # source; "path/to/File.cs::Method" additionally pins which file it lives
                # in, so moving or renaming the file breaks the gate loudly instead of
                # silently matching a same-named method somewhere else.
                if "::" in ref:
                    rel, method = ref.split("::", 1)
                    target = ROOT / rel
                    if not target.exists():
                        problems.append(f"{req['id']}: test file not found — {rel}")
                    elif not _declares(target.read_text(encoding="utf-8"), method):
                        problems.append(
                            f"{req['id']}: test method not found in {rel} — {method}")
                elif not _declares(src, ref):
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
    #
    # A directory must contain a file, not merely exist. Git does not track an empty
    # directory, so one left behind by a deletion still exists in a working copy and
    # is absent from every fresh checkout - passing here and failing only in CI.
    for suite in registry["suites"]:
        target = ROOT / suite["path"]
        if not target.exists():
            problems.append(f"suite {suite['id']}: path does not exist — {suite['path']}")
        elif target.is_dir() and not any(target.rglob("*")):
            problems.append(
                f"suite {suite['id']}: directory is empty, so it is absent from a fresh "
                f"checkout — {suite['path']}")

    # 5. Fixtures nobody claims are invisible coverage.
    for orphan in sorted(fixtures_on_disk - claimed_fixtures):
        notes.append(f"fixture claimed by no requirement — {orphan}")

    # 6. Normative spec sections absent from the registry.
    covered = {r["section"] for r in registry["requirements"]}
    normative_anchors, unanchored = spec_sections_with_musts()
    for anchor in sorted(normative_anchors):
        if anchor not in covered:
            problems.append(
                f"specification #{anchor} has normative clauses but no registry entry")
    for heading in sorted(unanchored):
        problems.append(
            f"specification heading has normative clauses but no stable anchor — {heading}")
    for anchor in sorted(covered - normative_anchors):
        notes.append(f"registry entry cites #{anchor}, which carries no normative clause")

    # 7. Coverage is measured per rule, not per section.
    #
    # A section can hold six requirements and one vector and still look covered
    # when the unit is the section. The specification gives every normative clause
    # a stable identifier so coverage can be counted against the rule instead.
    rule_ids = re.findall(r"\[(APR-[A-Z]+-\d{3})\]", SPEC.read_text(encoding="utf-8"))
    claimed: dict[str, list[str]] = {}
    for req in registry["requirements"]:
        for rule in req.get("rules", []):
            claimed.setdefault(rule, []).append(req["id"])

    for rule in sorted(set(rule_ids) - set(claimed)):
        problems.append(f"rule {rule} is stated in the specification and claimed by no requirement")
    for rule in sorted(set(claimed) - set(rule_ids)):
        problems.append(f"registry claims rule {rule}, which the specification does not state")
    for rule, owners in sorted(claimed.items()):
        if len(owners) > 1:
            problems.append(f"rule {rule} is claimed by more than one requirement: {owners}")

    gated_rules = sum(
        len(req.get("rules", []))
        for req in registry["requirements"]
        if req["strength"] == "gated"
    )

    # 8. Do not retain IDE-generated empty test scaffolding as apparent coverage.
    for placeholder in placeholder_test_files():
        problems.append(f"placeholder test scaffold must be removed — {placeholder.relative_to(ROOT)}")

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
        print(f"Rules in the specification: {len(set(rule_ids))}  "
              f"gated: {gated_rules}  "
              f"in a partial or ungated requirement: {len(set(rule_ids)) - gated_rules}")
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

#!/usr/bin/env python3
"""Prove the OSCAL catalog generator fails a specification it would misread.

`scripts/build-oscal.py` verifies that the committed catalog matches a fresh build.
That catches drift, and nothing else: a parser that misses a rule misses it in the
committed catalog too, and the comparison stays green. Every downstream rule count
treats the catalog as the complete list of rules, so a silent miss hides a rule from
all of them.

Each mutant below is the real specification damaged in one named way, and each
declares what the generator must do about it: refuse to build, or build a catalog
that differs from the committed one in exactly the way the damage predicts. A mutant
the generator lets through unnoticed is the bug.

    python3 scripts/check-oscal-teeth.py
"""
from __future__ import annotations

import contextlib
import importlib.util
import io
import pathlib
import re
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
GENERATOR = ROOT / "scripts" / "build-oscal.py"


def load():
    spec = importlib.util.spec_from_file_location("build_oscal", GENERATOR)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def controls(catalog: dict) -> dict[str, dict]:
    return {c["props"][0]["value"]: c for g in catalog["catalog"]["groups"] for c in g["controls"]}


def attempt(oscal, text: str, workspace: pathlib.Path) -> tuple[dict | None, str, int]:
    """Build from `text`: (catalog or None, refusal, exit code of verify mode)."""
    path = workspace / "APR_SPECIFICATION.md"
    path.write_text(text, encoding="utf-8")
    oscal.SPEC = path
    try:
        catalog = oscal.build()
    except SystemExit as refusal:
        return None, str(refusal), 1
    with contextlib.redirect_stdout(io.StringIO()):
        saved, sys.argv = sys.argv, [str(GENERATOR)]
        try:
            verdict = oscal.main()
        finally:
            sys.argv = saved
    return catalog, "", verdict


def main() -> int:
    oscal = load()
    original = oscal.SPEC.read_text(encoding="utf-8")
    rules = oscal.rules(original)
    heading_of = {m.group(3): (m.group(0), len(m.group(1)))
                  for m in (oscal.HEADING.match(line) for line in original.split("\n")) if m}
    first, second = rules[0]["id"], rules[1]["id"]
    # A section below chapter level, so the damage cannot also break the profile table.
    nested = next(r for r in rules if heading_of.get(r["anchor"], ("", 2))[1] > 2)
    heading = heading_of[nested["anchor"]][0]

    def once(text: str, old: str, new: str) -> str:
        assert text.count(old) >= 1, f"the mutant's target {old!r} is not in the specification"
        return text.replace(old, new, 1)

    problems: list[str] = []
    with tempfile.TemporaryDirectory() as scratch:
        workspace = pathlib.Path(scratch)

        catalog, refusal, verdict = attempt(oscal, original, workspace)
        if catalog is None or verdict != 0:
            problems.append("the undamaged specification does not build the committed catalog, "
                            f"so no mutant's result can be told from it — {refusal or 'stale'}")
        baseline = controls(catalog) if catalog else {}
        print(f"  {'control':22} {len(baseline)} controls, agrees with the committed catalog")

        def expect_refusal(name: str, description: str, text: str, mentions: str) -> None:
            built, why, _ = attempt(oscal, text, workspace)
            if built is not None:
                problems.append(f"{name} ({description}): built a catalog instead of refusing")
            elif mentions not in why:
                problems.append(f"{name} ({description}): refused, but not for this — {why[:200]}")
            print(f"  {name:22} {'refused' if built is None else 'BUILT'}   {description}")

        def expect_change(name: str, description: str, text: str, changed) -> None:
            built, why, verdict = attempt(oscal, text, workspace)
            if built is None:
                problems.append(f"{name} ({description}): refused to build — {why[:200]}")
            else:
                wrong = changed(controls(built))
                if wrong:
                    problems.append(f"{name} ({description}): {wrong}")
                if verdict == 0:
                    problems.append(f"{name} ({description}): verify mode still passes")
            print(f"  {name:22} {'stale' if built is not None and verdict else 'MISSED'}     {description}")

        expect_change(
            "stripped-citation", f"{first}'s identifier removed from its paragraph",
            once(original, f"[{first}]", ""),
            lambda built: "" if first not in built and len(built) == len(baseline) - 1
            else f"the catalog still has {len(built)} controls, {first} included: {first in built}")

        expect_refusal(
            "duplicated-identifier", f"{second}'s paragraph cites {first} instead",
            once(original, f"[{second}]", f"[{first}]"), f"{first} is stated 2 times")

        expect_refusal(
            "malformed-anchor", f"the heading over {nested['id']} loses its closing brace",
            once(original, heading, heading.rstrip().rstrip("}")), "anchor this parser cannot read")

        marker = "MUTATEDSTATEMENT"
        expect_change(
            "changed-statement", f"{first}'s normative text changed, identifier kept",
            once(original, f"[{first}]", f"{marker} [{first}]"),
            lambda built: "" if marker in built[first]["parts"][0]["prose"]
            else "the statement did not follow the specification's text")

        area, number = re.match(r"APR-([A-Z]+)-(\d{3})", first).groups()
        expect_refusal(
            "unmatched-identifier", f"{first} written with four digits, a form the parser does not read",
            once(original, f"[{first}]", f"[APR-{area}-0{number}]"), "looks like a rule identifier")

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nA generator that builds a misread specification without saying so is how a "
              "rule\ndisappears from every count. Add the check the mutant escaped.")
        return 1
    print("\nEvery mutant is refused or leaves the catalog visibly stale, and the "
          "undamaged\nspecification builds the committed catalog.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

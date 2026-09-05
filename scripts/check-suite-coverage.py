#!/usr/bin/env python3
"""Report how much of the specification the portable conformance suite reaches.

The suite is what a new implementation is measured by. Passing it should mean
something, and how much it means is exactly the number of rules it exercises.
Nothing reported that until now: the suite could pass in full while most of the
document went unexercised, and the passing score would look identical.

    python3 scripts/check-suite-coverage.py            # summary
    python3 scripts/check-suite-coverage.py --gaps     # and every unreached rule
    python3 scripts/check-suite-coverage.py --json     # machine readable

This measures the **suite** and nothing else. `tests/registry.json` records the
wider picture, including rules held only by an SDK's own unit tests, which a
third-party implementation is never run against. The difference between the two
numbers is the point: a rule gated only inside this repository is a rule a new
implementation can violate and still score full marks.

Rules the suite cannot reach are not defects by themselves. A renderer
obligation, a security posture, or a rule about what a reader must preserve
across a round trip is not a document that is either accepted or refused. The
registry is where each of those is supposed to say so, per rule, with a named
gap, and this reports which have not.
"""
from __future__ import annotations

import collections
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
SUITE = ROOT / "tests" / "Conformance" / "beta6" / "suite.json"
REGISTRY = ROOT / "tests" / "registry.json"

RULE = re.compile(r"APR-[A-Z]+-\d{3}")


def main() -> int:
    spec_rules = sorted(set(RULE.findall(SPEC.read_text(encoding="utf-8"))))
    suite = json.loads(SUITE.read_text(encoding="utf-8"))
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))

    anchor_rules: dict[str, list[str]] = collections.defaultdict(list)
    rule_requirement: dict[str, dict] = {}
    for requirement in registry["requirements"]:
        for rule in requirement.get("rules", []):
            rule_requirement[rule] = requirement
            if requirement.get("section"):
                anchor_rules[requirement["section"]].append(rule)

    positive: collections.Counter = collections.Counter()
    negative: collections.Counter = collections.Counter()
    uncited: list[str] = []
    for case in suite["cases"]:
        anchors = case.get("anchors") or []
        reached = {r for anchor in anchors for r in anchor_rules.get(anchor, [])}
        if not reached:
            uncited.append(case["id"])
            continue
        for rule in reached:
            if case["expect"] == "reject":
                negative[rule] += 1
            else:
                positive[rule] += 1

    touched = [r for r in spec_rules if positive[r] or negative[r]]
    twice = [r for r in spec_rules if positive[r] + negative[r] > 1]
    refused = [r for r in spec_rules if negative[r]]
    unreached = [r for r in spec_rules if not (positive[r] or negative[r])]
    unexplained = [r for r in unreached
                   if not (rule_requirement.get(r) or {}).get("gap")
                   and (rule_requirement.get(r) or {}).get("strength") not in
                   {"none", "external", "informative"}]

    if "--json" in sys.argv:
        print(json.dumps({
            "suiteVersion": suite["suiteVersion"],
            "rules": len(spec_rules),
            "cases": len(suite["cases"]),
            "reached": len(touched),
            "reachedTwiceOrMore": len(twice),
            "reachedByARejectCase": len(refused),
            "unreached": unreached,
            "unreachedWithNoRecordedGap": unexplained,
            "casesCitingNoRule": uncited,
        }, indent=2))
        return 0

    print(f"suite {suite['suiteVersion']}: {len(suite['cases'])} cases "
          f"({sum(1 for c in suite['cases'] if c['expect'] == 'reject')} expect rejection)\n")
    print(f"  rules in the specification      {len(spec_rules):>4}")
    print(f"  reached by at least one case    {len(touched):>4}")
    print(f"  reached by two or more          {len(twice):>4}")
    print(f"  reached by a rejection case     {len(refused):>4}")
    print(f"  reached by nothing              {len(unreached):>4}")
    print(f"  of those, with no recorded gap  {len(unexplained):>4}")

    by_area = collections.Counter(r.split("-")[1] for r in unreached)
    print("\nunreached by area: " + ", ".join(f"{a} {n}" for a, n in by_area.most_common()))

    if uncited:
        print(f"\n{len(uncited)} case(s) cite no rule at all, so they count for nothing:")
        for case in uncited:
            print(f"  {case}")

    if "--gaps" in sys.argv:
        print("\nUnreached rules. A rule with a recorded gap is a decision; one "
              "without is an oversight.\n")
        for rule in unreached:
            requirement = rule_requirement.get(rule)
            if requirement is None:
                note = "no registry entry at all"
            elif requirement.get("gap"):
                note = f"{requirement['strength']}: {requirement['gap']}"
            else:
                note = f"{requirement['strength']}, no gap recorded"
            print(f"  {rule}  {note[:110]}")
    else:
        print("\nRun with --gaps to list every unreached rule and what the registry "
              "says about it.")
    # Coverage is a number to improve, not a gate to trip: a rule the suite cannot
    # reach is often a rule no document vector could reach. A case that cites no
    # rule is different. It is measured as nothing, so it counts for nothing, and
    # that is always a defect in the suite rather than a judgement about the format.
    return 1 if uncited else 0


if __name__ == "__main__":
    sys.exit(main())

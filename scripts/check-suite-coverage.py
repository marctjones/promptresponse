#!/usr/bin/env python3
"""Report how much of the specification the portable conformance suite reaches.

The suite is what a new implementation is measured by. Passing it should mean
something, and how much it means is exactly the number of rules it exercises.
Nothing reported that until now: the suite could pass in full while most of the
document went unexercised, and the passing score would look identical.

    python3 scripts/check-suite-coverage.py            # summary
    python3 scripts/check-suite-coverage.py --by-section  # per section of the document
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
# A renderer case reaches a rule exactly as a document case does. Twelve rules are
# reachable only this way — no file is valid or invalid because of how a renderer
# behaves — so a coverage count that read only the document suite would report them
# unreached forever, however well a renderer scored.
RENDERER_SUITE = ROOT / "tests" / "Conformance" / "beta6" / "renderer-suite.json"
REGISTRY = ROOT / "tests" / "registry.json"

RULE = re.compile(r"APR-[A-Z]+-\d{3}")
HEADING = re.compile(r"^(#{2,4})\s+(.*?)\s*\{#([a-z0-9-]+)\}\s*$")


def rules_by_section(text: str) -> list[tuple[str, str, list[str]]]:
    """(heading, anchor, rules) for each section, read from the specification.

    Attribution comes from where a rule is *stated*, not from where the registry
    files it. The two can disagree, and the document is the one that decides.
    """
    sections: list[tuple[str, str, list[str]]] = []
    heading = anchor = None
    collected: list[str] = []
    in_code = False
    for line in text.split("\n"):
        if line.startswith("```"):
            in_code = not in_code
            continue
        match = HEADING.match(line)
        if match and not in_code:
            if anchor is not None:
                sections.append((heading, anchor, collected))
            heading, anchor, collected = match.group(2), match.group(3), []
            continue
        # Only prose states a rule. An executable example cites rules in its
        # `rules:` header, and crediting the section the example happens to sit in
        # would attribute a rule to a section that does not state it.
        if anchor is not None and not in_code:
            collected.extend(RULE.findall(line))
    if anchor is not None:
        sections.append((heading, anchor, collected))
    return [(h, a, sorted(set(r))) for h, a, r in sections]


def main() -> int:
    spec_rules = sorted(set(RULE.findall(SPEC.read_text(encoding="utf-8"))))
    suite = json.loads(SUITE.read_text(encoding="utf-8"))
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    renderer = (json.loads(RENDERER_SUITE.read_text(encoding="utf-8"))
                if RENDERER_SUITE.exists() else {"cases": []})

    anchor_rules: dict[str, list[str]] = collections.defaultdict(list)
    rule_requirement: dict[str, dict] = {}
    for requirement in registry["requirements"]:
        for rule in requirement.get("rules", []):
            rule_requirement[rule] = requirement
            if requirement.get("section"):
                anchor_rules[requirement["section"]].append(rule)

    # Two counts, because they answer different questions. A case that names the
    # rules it exercises proves something about those rules. A case that only names
    # a section is credited with every rule in it, which is the most it could
    # possibly have covered rather than what it did. The first is the floor, the
    # second the ceiling, and the distance between them is citation work not done.
    positive: collections.Counter = collections.Counter()
    negative: collections.Counter = collections.Counter()
    cited: collections.Counter = collections.Counter()
    cited_negative: collections.Counter = collections.Counter()
    uncited: list[str] = []
    for case in suite["cases"] + [
            # A renderer case names its rules outright and never cites a section, so it
            # contributes to the floor and to the ceiling equally — which is what a case
            # that says exactly what it exercises should do.
            {**case, "anchors": [], "expect": "valid"} for case in renderer["cases"]]:
        anchors = case.get("anchors") or []
        # The ceiling is what the case could have covered: the rules it names
        # outright, plus every rule in any section it cites.
        reached = {r for anchor in anchors for r in anchor_rules.get(anchor, [])}
        reached |= set(case.get("rules") or [])
        if not reached:
            uncited.append(case["id"])
            continue
        for rule in reached:
            if case["expect"] == "reject":
                negative[rule] += 1
            else:
                positive[rule] += 1
        for rule in case.get("rules") or []:
            cited[rule] += 1
            if case["expect"] == "reject":
                cited_negative[rule] += 1

    named = [r for r in spec_rules if cited[r]]
    named_negative = [r for r in spec_rules if cited_negative[r]]
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
            "namedByACase": len(named),
            "namedByARejectCase": len(named_negative),
            "reachedAtMost": len(touched),
            "reachedTwiceOrMore": len(twice),
            "reachedByARejectCase": len(refused),
            "unreached": unreached,
            "unreachedWithNoRecordedGap": unexplained,
            "casesCitingNoRule": uncited,
        }, indent=2))
        return 0

    print(f"suite {suite['suiteVersion']}: {len(suite['cases'])} cases "
          f"({sum(1 for c in suite['cases'] if c['expect'] == 'reject')} expect rejection)\n")
    print(f"  rules in the specification         {len(spec_rules):>4}")
    print(f"  named outright by a case (floor)   {len(named):>4}")
    print(f"    of those, by a rejection case    {len(named_negative):>4}")
    print(f"  credited via a section (ceiling)   {len(touched):>4}")
    print(f"    of those, by a rejection case    {len(refused):>4}")
    print(f"    credited more than once          {len(twice):>4}")
    print(f"  reached by nothing at all          {len(unreached):>4}")
    print(f"    of those, with no recorded gap   {len(unexplained):>4}")
    print("\n  The floor counts cases that name the rules they exercise. The ceiling\n"
          "  credits a case with every rule in the section it cites, which is the most\n"
          "  it could have covered rather than what it did.")

    by_area = collections.Counter(r.split("-")[1] for r in unreached)
    print("\nunreached by area: " + ", ".join(f"{a} {n}" for a, n in by_area.most_common()))

    if uncited:
        print(f"\n{len(uncited)} case(s) cite no rule at all, so they count for nothing:")
        for case in uncited:
            print(f"  {case}")

    if "--by-section" in sys.argv:
        spec_text = SPEC.read_text(encoding="utf-8")
        print("\n  rules  named  credited  reject  section")
        print("  -----  -----  --------  ------  " + "-" * 44)
        for heading, anchor, rules in rules_by_section(spec_text):
            if not rules:
                continue
            named_here = sum(1 for r in rules if cited[r])
            credited_here = sum(1 for r in rules if positive[r] or negative[r])
            reject_here = sum(1 for r in rules if negative[r])
            flag = "  " if named_here else "! "
            print(f"  {len(rules):>5}  {named_here:>5}  {credited_here:>8}  "
                  f"{reject_here:>6}  {flag}{heading[:42]}")
        print("\n  named    = rules a case names outright")
        print("  credited = rules credited through the section a case cites")
        print("  reject   = rules credited by a case that expects rejection")
        print("  !        = no case names any rule in this section")
        return 1 if uncited else 0

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

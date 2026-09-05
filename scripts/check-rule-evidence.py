#!/usr/bin/env python3
"""What evidence exists, per rule, that APR is implemented and tested.

Three questions were being answered by three different scripts and never joined
up, so nothing could say whether any single rule was actually covered:

* does the file validator **enforce** the rule?
* does a conformance case demonstrate a document that **satisfies** it?
* does a conformance case demonstrate a document that **violates** it, and is
  that violation actually **caught**?

A rule needs all four. Enforcement without a violating case is a check nobody
proved works. A violating case without enforcement is a case that passes for the
wrong reason. A satisfying case alone proves only that valid input is accepted,
which is the easy half.

    python3 scripts/check-rule-evidence.py            # matrix and totals
    python3 scripts/check-rule-evidence.py --missing  # only rules lacking evidence
    python3 scripts/check-rule-evidence.py --json
    python3 scripts/check-rule-evidence.py --write    # update the ratchet baseline

**What is gated.** Two invariants, both of which must hold today:

1. A case citing a rule the OSCAL catalogue does not contain is a broken citation.
2. A case that expects rejection and cites a rule must actually be refused, with
   that refusal traceable to the rule. A vector that claims to test a rule and
   would pass an implementation that ignores it is worse than no vector.

**What is ratcheted.** The four counts are recorded in
`tests/Conformance/beta6/rule-evidence.json` and may not go down. Coverage is
not gated at a level nobody has reached; it is prevented from regressing.

`caught` is satisfied either by a finding that cites the rule, or, for rules
enforced during parsing rather than validation, by the parser raising the
diagnostic the case declares. The second form attributes the rule from the
case's own citation, so it is weaker, and the matrix marks it `parse`.
"""
from __future__ import annotations

import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent
sys.path.insert(0, str(HERE))
import aprlib  # noqa: E402

_spec = importlib.util.spec_from_file_location("validate_apr", HERE / "validate-apr.py")
validate_apr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(validate_apr)

CATALOG = ROOT / "docs" / "release" / "apr-oscal-catalog.json"
SUITE = ROOT / "tests" / "Conformance" / "beta6" / "suite.json"
BASELINE = ROOT / "tests" / "Conformance" / "beta6" / "rule-evidence.json"


def evaluate(case, members) -> tuple[str, set[str], str | None]:
    """(outcome, rules cited by errors, diagnostic raised while parsing)."""
    representation = "yaml" if case["representation"].startswith("yaml") else "jsonc"
    try:
        records = aprlib.read_records(case["document"], representation)
    except aprlib.AprError as exc:
        return "reject", set(), str(exc)
    except Exception as exc:  # noqa: BLE001
        return "reject", set(), type(exc).__name__
    report = validate_apr.Report(case["id"])
    for record in records:
        if not aprlib.is_attestation(record):
            validate_apr.validate_form(report, record, members)
    cited = {r for f in report.findings if f["severity"] == "error" for r in f["rules"]}
    return ("reject" if report.errors else "valid"), cited, None


def main() -> int:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))["catalog"]
    rules = [c["props"][0]["value"] for g in catalog["groups"] for c in g["controls"]]
    statements = {c["props"][0]["value"]: c["parts"][0]["prose"]
                  for g in catalog["groups"] for c in g["controls"]}
    suite = json.loads(SUITE.read_text(encoding="utf-8"))
    members = validate_apr.spec_members()
    enforced = validate_apr.enforced_rules()

    problems: list[str] = []
    satisfied: dict[str, int] = {r: 0 for r in rules}
    violated: dict[str, int] = {r: 0 for r in rules}
    caught: dict[str, str] = {}

    for case in suite["cases"]:
        cited = case.get("rules") or []
        for rule in cited:
            if rule not in satisfied:
                problems.append(f"{case['id']} cites {rule}, which the catalogue does not contain")
        if not cited:
            continue
        if case["expect"] == "valid":
            for rule in cited:
                satisfied[rule] = satisfied.get(rule, 0) + 1
            continue
        if case["expect"] != "reject":
            continue
        for rule in cited:
            violated[rule] = violated.get(rule, 0) + 1
        outcome, by_check, diagnostic = evaluate(case, members)
        if outcome != "reject":
            problems.append(
                f"{case['id']} expects rejection and cites {', '.join(cited)}, but the "
                f"validator accepts it: a vector that would pass an implementation "
                f"ignoring the rule tests nothing")
            continue
        for rule in cited:
            if rule in by_check:
                caught[rule] = "check"
            elif diagnostic and diagnostic == case.get("diagnostic"):
                caught.setdefault(rule, "parse")
            else:
                problems.append(
                    f"{case['id']} is refused, but not traceably for {rule}: no check "
                    f"cites it and the parser raised {diagnostic!r} where the case "
                    f"declares {case.get('diagnostic')!r}")

    counts = {
        "enforced": sum(1 for r in rules if r in enforced),
        "satisfied": sum(1 for r in rules if satisfied[r]),
        "violated": sum(1 for r in rules if violated[r]),
        "caught": sum(1 for r in rules if r in caught),
        "complete": sum(1 for r in rules
                        if r in enforced and satisfied[r] and violated[r] and r in caught),
    }

    if "--json" in sys.argv:
        print(json.dumps({
            "rules": len(rules), "counts": counts,
            "matrix": {r: {"enforced": r in enforced, "satisfied": satisfied[r],
                           "violated": violated[r], "caught": caught.get(r)}
                       for r in rules},
            "problems": problems,
        }, indent=2))
        return 1 if problems else 0

    if "--write" in sys.argv:
        BASELINE.write_text(json.dumps({
            "$comment": "Ratchet baseline for scripts/check-rule-evidence.py. These "
                        "counts may rise and may not fall. Regenerate with --write only "
                        "when they have risen.",
            "rules": len(rules), "counts": counts,
        }, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote {BASELINE.relative_to(ROOT)}: {counts}")
        return 0

    show_missing = "--missing" in sys.argv
    print(f"{'rule':<16} enforced satisfied violated caught")
    print("-" * 56)
    for rule in rules:
        complete = (rule in enforced and satisfied[rule]
                    and violated[rule] and rule in caught)
        if show_missing and complete:
            continue
        print(f"{rule:<16} {'yes' if rule in enforced else '  -':>8} "
              f"{satisfied[rule] or '-':>9} {violated[rule] or '-':>8} "
              f"{caught.get(rule) or '-':>6}")

    print(f"\nof {len(rules)} rules in the catalogue:")
    print(f"  enforced by the validator        {counts['enforced']:>4}")
    print(f"  shown satisfied by a case        {counts['satisfied']:>4}")
    print(f"  shown violated by a case         {counts['violated']:>4}")
    print(f"  and that violation is caught     {counts['caught']:>4}")
    print(f"  all four (fully evidenced)       {counts['complete']:>4}")

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    if BASELINE.exists():
        base = json.loads(BASELINE.read_text(encoding="utf-8"))["counts"]
        fell = {k: (base[k], counts[k]) for k in counts if counts[k] < base.get(k, 0)}
        if fell:
            print("\nCOVERAGE REGRESSED:")
            for name, (was, now) in fell.items():
                print(f"  - {name}: {was} -> {now}")
            print("\nEvidence may rise and may not fall. Restore it, or if a rule was "
                  "deliberately\nretired, rerun with --write.")
            return 1
        risen = {k for k in counts if counts[k] > base.get(k, 0)}
        if risen:
            print(f"\nCoverage rose for {sorted(risen)}. Record it with "
                  f"scripts/check-rule-evidence.py --write")
    return 0


if __name__ == "__main__":
    sys.exit(main())

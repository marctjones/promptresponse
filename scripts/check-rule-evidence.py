#!/usr/bin/env python3
"""What evidence exists, per rule, that APR is implemented and tested.

Three questions were being answered by three different scripts and never joined
up, so nothing could say whether any single rule was actually covered:

* is the rule **enforced** — by a check in the file validator, or, for a rule about
  what a writer preserves or what an evaluator computes, by a round-trip or
  evaluation case in the harness?
* does a conformance case demonstrate a document that **satisfies** it?
* does a conformance case demonstrate a document that **violates** it, and is
  that violation actually **caught**?

A rule needs all four. Enforcement without a violating case is a check nobody
proved works. A violating case without enforcement is a case that passes for the
wrong reason. A satisfying case alone proves only that valid input is accepted,
which is the easy half.

    python3 scripts/check-rule-evidence.py            # matrix and totals
    python3 scripts/check-rule-evidence.py --missing  # only rules lacking evidence
    python3 scripts/check-rule-evidence.py --by-section  # per section of the document
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

`caught` is satisfied by a finding that cites the rule; by the parser raising the
diagnostic the case declares, for rules decided while reading; by a lossy writer or
a wrong evaluator failing a round-trip or evaluation case; or, weakest, by
`acceptance`, where the rule says a reader must accept something and refusing it is
the whole of the violation. The second form attributes the rule from the
case's own citation, so it is weaker, and the matrix marks it `parse`.
"""
from __future__ import annotations

import datetime
import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent
sys.path.insert(0, str(HERE))
import aprlib  # noqa: E402
import aprexpr  # noqa: E402

_spec = importlib.util.spec_from_file_location("validate_apr", HERE / "validate-apr.py")
validate_apr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(validate_apr)

_cov = importlib.util.spec_from_file_location("cov", HERE / "check-suite-coverage.py")
check_suite_coverage = importlib.util.module_from_spec(_cov)
_cov.loader.exec_module(check_suite_coverage)

_run = importlib.util.spec_from_file_location("run_conformance", HERE / "run-conformance.py")
run_conformance = importlib.util.module_from_spec(_run)
_run.loader.exec_module(run_conformance)

CATALOG = ROOT / "docs" / "release" / "apr-oscal-catalog.json"
SUITE = ROOT / "tests" / "Conformance" / "beta6" / "suite.json"
BASELINE = ROOT / "tests" / "Conformance" / "beta6" / "rule-evidence.json"


def evaluate(case, members) -> tuple[str, set[str], str | None, dict]:
    """(outcome, rules cited by errors, parse diagnostic, warning code -> rules)."""
    representation = "yaml" if case["representation"].startswith("yaml") else "jsonc"
    try:
        records = aprlib.read_records(case["document"], representation)
    except aprlib.AprError as exc:
        return "reject", set(), str(exc), {}
    except Exception as exc:  # noqa: BLE001
        return "reject", set(), type(exc).__name__, {}
    report = validate_apr.Report(case["id"])
    for record in records:
        if isinstance(record, dict) and "recordType" in record:
            validate_apr.validate_attestation(report, record)
        else:
            validate_apr.validate_form(report, record, members)
    cited = {r for f in report.findings if f["severity"] == "error" for r in f["rules"]}
    warned = {f["code"]: f["rules"] for f in report.findings if f["severity"] == "warning"}
    return ("reject" if report.errors else "valid"), cited, None, warned


def naive_evaluation(document: str, representation: str, inputs: dict) -> dict:
    """What a plausibly wrong evaluator produces.

    Six mistakes the specification names and forbids, made on purpose: default an
    unbound response instead of leaving it unbound, overwrite every computed prompt
    including a person's correction, evaluate in document order rather than by
    reference, read the host clock rather than the caller's, let a prompt shadow a
    reserved name, and provide an extension library the specification forbids. A
    case that this still satisfies is testing nothing about correct evaluation.
    """
    try:
        records = aprlib.read_records(document, representation)
    except Exception:  # noqa: BLE001
        return {}
    form = records[0]
    original_bind, original_order = aprexpr.bind, aprexpr.order
    original_reserved, original_strings = aprexpr.RESERVED, aprexpr.strings_extension
    original_compose = aprexpr.compose

    def defaulting(response, declared):
        try:
            return original_bind(response, declared)
        except aprexpr.Unbound:
            kind = aprexpr.CEL_TYPE.get(declared or "", "string")
            from celpy import celtypes
            return {"double": celtypes.DoubleType(0.0),
                    "bool": celtypes.BoolType(False)}.get(kind, celtypes.StringType(""))

    stripped = json.loads(json.dumps(form))
    for prompt, _ in aprexpr.prompts_of(stripped):
        prompt.pop("responseMetadata", None)  # every computed value is overwritten
        if isinstance((prompt.get("hints") or {}).get("exprValue"), str):
            prompt.pop("response", None)
    try:
        from celpy import celtypes
        aprexpr.bind, aprexpr.order = defaulting, (lambda computed: computed)
        # A prompt named ctx now shadows the host context, by applying direct
        # bindings last instead of first.
        aprexpr.RESERVED = ()
        aprexpr.compose = lambda bound, ambient: {**ambient, **bound}
        # An implementation that provides an extension library the specification
        # forbids. The withdrawn strings extension is the realistic one to reach
        # for, since a CEL binding that ships it makes this the easy mistake.
        aprexpr.strings_extension = aprexpr._withdrawn_strings_extension
        return aprexpr.evaluate(stripped, now=inputs.get("now"),
                                today=datetime.date.today().isoformat(),
                                ctx=inputs.get("ctx"))
    except Exception:  # noqa: BLE001
        return {}
    finally:
        aprexpr.bind, aprexpr.order = original_bind, original_order
        aprexpr.RESERVED, aprexpr.strings_extension = original_reserved, original_strings
        aprexpr.compose = original_compose


def lossy(document: str, representation: str, pointers: list[str]) -> str | None:
    """The same document as a careless writer would emit it.

    Drops every unknown member and every pointer the case says must survive, then
    re-serializes. A round-trip case that this still passes is testing nothing:
    the rule is about what a writer keeps, so the vector has to notice losing it.
    """
    try:
        records = aprlib.read_records(document, representation)
    except Exception:  # noqa: BLE001
        return None

    def strip(node):
        if isinstance(node, dict):
            return {k: strip(v) for k, v in node.items() if "." not in k}
        if isinstance(node, list):
            return [strip(v) for v in node]
        if isinstance(node, str):
            return node.strip()  # the normalization the format forbids
        return node

    # A core-only reader that discards what it cannot interpret is the other way a
    # writer loses data, and dropping a record is the loudest version of it.
    damaged = [strip(r) for r in records
               if not (len(records) > 1 and isinstance(r, dict) and "recordType" in r)]
    # And collapses repeated occurrences, which a stream must never do.
    seen, deduplicated = set(), []
    for record in damaged:
        key = aprlib.canonicalize(record)
        if key in seen:
            continue
        seen.add(key)
        deduplicated.append(record)
    damaged = deduplicated
    for pointer in pointers:
        for record in damaged:
            parent = aprlib.resolve_pointer(record, pointer.rsplit("/", 1)[0] or "")
            leaf = pointer.rsplit("/", 1)[-1]
            if isinstance(parent, dict):
                parent.pop(leaf, None)
    return "".join((aprlib.RS if len(damaged) > 1 else "")
                   + json.dumps(record, indent=2, ensure_ascii=False) + "\n"
                   for record in damaged)


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
        if case.get("equivalentTo"):
            # Paired representations must produce one semantic model. A reader whose
            # scalar resolution differs between them reports two digests and fails,
            # which is the violation this case exists to catch.
            for rule in cited:
                satisfied[rule] = satisfied.get(rule, 0) + 1
                violated[rule] = violated.get(rule, 0) + 1
                caught[rule] = "equivalence"
            continue

        if case.get("acceptance"):
            # A rule of the form "a reader MUST accept this" is violated by refusing
            # the document, which the harness scores directly. Weaker than a
            # rejection case, because any conforming reader passes it by doing
            # nothing special, so the matrix marks it apart.
            for rule in cited:
                satisfied[rule] = satisfied.get(rule, 0) + 1
                violated[rule] = violated.get(rule, 0) + 1
                caught[rule] = "acceptance"
            outcome, _, _, _ = evaluate(case, members)
            if outcome != "valid":
                problems.append(
                    f"{case['id']} claims acceptance proves its rule, but the validator "
                    f"refuses it")
            continue

        if case.get("expects"):
            # An evaluation rule has no violating document either: the violation is
            # by the evaluator. The case is the negative test, and counts only if a
            # plausibly wrong evaluator fails it.
            for rule in cited:
                satisfied[rule] = satisfied.get(rule, 0) + 1
                violated[rule] = violated.get(rule, 0) + 1
            representation = ("yaml" if case["representation"].startswith("yaml")
                              else "jsonc")
            wrong = naive_evaluation(case["document"], representation,
                                     case.get("evaluate") or {})
            passes, _ = run_conformance.evaluation_ok(case, {"evaluated": wrong})
            if passes and case.get("teeth") is False:
                # Declared as documenting the surface rather than guarding it: no
                # simulated mistake reaches a rule that any CEL implementation
                # satisfies by existing. Counted as violated but never as caught.
                continue
            if passes:
                problems.append(
                    f"{case['id']} is an evaluation case that an evaluator defaulting "
                    f"unbound values, overwriting corrections, ignoring reference order "
                    f"and reading the host clock still satisfies, so it tests nothing")
            else:
                for rule in cited:
                    caught[rule] = "evaluation"
            continue

        if case.get("roundTrip"):
            # A preservation rule has no violating document: the violation is by the
            # writer. So the case itself is the negative test, and it only counts if
            # a careless writer actually fails it.
            for rule in cited:
                satisfied[rule] = satisfied.get(rule, 0) + 1
                violated[rule] = violated.get(rule, 0) + 1
            representation = ("yaml" if case["representation"].startswith("yaml")
                              else "jsonc")
            damaged = lossy(case["document"], representation,
                            case.get("preserves") or [])
            passes, _ = run_conformance.round_trip_ok(case, {"written": damaged or ""})
            if passes:
                problems.append(
                    f"{case['id']} is a round-trip case that a writer dropping unknown "
                    f"members and trimming responses still passes, so it tests nothing")
            else:
                for rule in cited:
                    caught[rule] = "roundtrip"
            continue

        if case["expect"] == "valid" and not case.get("warns"):
            for rule in cited:
                satisfied[rule] = satisfied.get(rule, 0) + 1
            continue
        if case["expect"] == "valid":
            # A case requiring an advisory is a violating case: the document breaks
            # the rule, and the rule says to report it rather than refuse it.
            for rule in cited:
                violated[rule] = violated.get(rule, 0) + 1
            outcome, _, _, warned = evaluate(case, members)
            if outcome != "valid":
                problems.append(
                    f"{case['id']} expects acceptance with a warning, but the validator "
                    f"refuses it")
                continue
            for code in case["warns"]:
                if code not in warned:
                    problems.append(
                        f"{case['id']} requires the advisory {code}, which nothing reports")
                    continue
                for rule in cited:
                    if rule in warned[code]:
                        caught[rule] = "warning"
            for rule in cited:
                if rule not in caught:
                    problems.append(
                        f"{case['id']} is warned about, but not traceably for {rule}: no "
                        f"advisory citing it was reported")
            continue
        if case["expect"] != "reject":
            continue
        for rule in cited:
            violated[rule] = violated.get(rule, 0) + 1
        outcome, by_check, diagnostic, _ = evaluate(case, members)
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

    # A preservation, evaluation, equivalence or tolerance rule is not something a
    # file validator can check: no single document is wrong. The harness enforces
    # each of them — by asking for the document back, by asking what an expression
    # produced, by comparing the digests of a paired form, and by scoring whether a
    # document that must be accepted was.
    enforced = enforced | {r for case in suite["cases"]
                           if case.get("roundTrip") or case.get("expects")
                           or case.get("equivalentTo") or case.get("acceptance")
                           for r in (case.get("rules") or [])}
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

    if "--by-section" in sys.argv:
        spec_text = (ROOT / "docs" / "APR_SPECIFICATION.md").read_text(encoding="utf-8")
        full = {r for r in rules if r in enforced and satisfied[r]
                and violated[r] and r in caught}
        print(" rules  tested  missing  section")
        print(" -----  ------  -------  " + "-" * 50)
        total = short = 0
        for heading, _anchor, section_rules in \
                check_suite_coverage.rules_by_section(spec_text):
            if not section_rules:
                continue
            done = sum(1 for r in section_rules if r in full)
            gap = len(section_rules) - done
            total += len(section_rules)
            short += gap
            mark = "   " if gap == 0 else ("!! " if done == 0 else " - ")
            print(f" {len(section_rules):>5}  {done:>6}  {gap:>7}  {mark}{heading[:47]}")
        print(f" {total:>5}  {total - short:>6}  {short:>7}   TOTAL")
        print("\n !! no rule in this section has a test;  - partly tested")
        print(" A rule is tested when it is enforced, shown satisfied, shown violated,")
        print(" and that violation is caught traceably.")
        return 1 if problems else 0

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
    print(f"  enforced by a check or a round trip{counts['enforced']:>4}")
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

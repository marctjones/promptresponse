#!/usr/bin/env python3
"""Report, per rule, whether the specification shows it with an example.

A rule is shown when an executable example in the specification satisfies it and
another violates it. The corpus and the renderer suite test rules too, but a
reader of the specification never sees those, so they are reported beside the
examples rather than counted as them.

  covered             an in-spec example satisfies the rule, and one violates it
  missing-violating   an in-spec example satisfies it; none violates it
  missing-satisfying  an in-spec example violates it; none satisfies it
  corpus-only         no in-spec example, but corpus or renderer cases cite it
  uncovered           nothing anywhere cites it
  not-expressible     recorded in tests/spec-conversion/example-exceptions.json:
                      no document can show it, and another test covers it

Examples come from the specification through extract-spec-examples.py, corpus
cases from tests/Conformance/beta6/suite.json, and renderer cases from
renderer-suite.json.

    python3 scripts/check-example-coverage.py            # report, never fails
    python3 scripts/check-example-coverage.py --missing  # only rules not covered
    python3 scripts/check-example-coverage.py --chapter 1
    python3 scripts/check-example-coverage.py --gate     # fail unless every rule is covered or excepted
    python3 scripts/check-example-coverage.py --json
    python3 scripts/check-example-coverage.py --self-test
"""
from __future__ import annotations

import importlib.util
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
SUITE = ROOT / "tests" / "Conformance" / "beta6" / "suite.json"
RENDERER_SUITE = ROOT / "tests" / "Conformance" / "beta6" / "renderer-suite.json"
EXCEPTIONS = ROOT / "tests" / "spec-conversion" / "example-exceptions.json"

_spec = importlib.util.spec_from_file_location("extract_spec_examples", ROOT / "scripts" / "extract-spec-examples.py")
extractor = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(extractor)
_spec = importlib.util.spec_from_file_location("spec_units", ROOT / "scripts" / "spec-units.py")
spec_units = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(spec_units)

STATUSES = ("covered", "missing-violating", "missing-satisfying", "corpus-only", "uncovered",
            "not-expressible")
RULE_TAG = re.compile(r"\[(APR-[A-Z]+-\d{3})\]")


def stated_rules(text: str) -> list[str]:
    return sorted(set(RULE_TAG.findall(extractor.FENCE.sub("", text))))


def exception_problems(exceptions: dict, rules: set[str], case_ids: set[str]) -> list[str]:
    problems = []
    for rule, entry in sorted(exceptions.items()):
        if rule.startswith("$"):
            continue
        if rule not in rules:
            problems.append(f"{rule}: excepted, but the specification states no such rule")
        if not str(entry.get("reason", "")).strip():
            problems.append(f"{rule}: excepted without a reason")
        covered_by = entry.get("coveredBy") or []
        if not covered_by:
            problems.append(f"{rule}: excepted without naming the test that covers it instead")
        for test in covered_by:
            if test not in case_ids and not (ROOT / test).exists():
                problems.append(f"{rule}: coveredBy {test}, which is neither a case id nor a file")
    return problems


def classify(rules: list[str], examples: list[dict], cases: list[dict], exceptions: dict) -> dict[str, dict]:
    report = {r: {"satisfying": [], "violating": [], "corpus": [], "renderer": []} for r in rules}
    for example in examples:
        for polarity, key in (("satisfying", "satisfies"), ("violating", "violates")):
            for rule in example.get(key, []):
                if rule in report:
                    report[rule][polarity].append(example["id"])
    for case in cases:
        leg = "renderer" if case["id"].startswith("renderer:") else "corpus"
        if leg == "corpus" and case.get("source") != "corpus":
            continue  # a specification example is already counted from the specification
        for rule in case.get("rules") or []:
            if rule in report:
                report[rule][leg].append(case["id"])
    for rule, row in report.items():
        if rule in exceptions:
            row["status"] = "not-expressible"
        elif row["satisfying"] and row["violating"]:
            row["status"] = "covered"
        elif row["satisfying"]:
            row["status"] = "missing-violating"
        elif row["violating"]:
            row["status"] = "missing-satisfying"
        elif row["corpus"] or row["renderer"]:
            row["status"] = "corpus-only"
        else:
            row["status"] = "uncovered"
    return report


def load() -> tuple[dict[str, dict], list[str]]:
    text = SPEC.read_text(encoding="utf-8")
    rules = stated_rules(text)
    examples, problems = extractor.extract(text)
    cases = (json.loads(SUITE.read_text(encoding="utf-8"))["cases"]
             + json.loads(RENDERER_SUITE.read_text(encoding="utf-8"))["cases"])
    exceptions = json.loads(EXCEPTIONS.read_text(encoding="utf-8")) if EXCEPTIONS.exists() else {}
    exceptions = {k: v for k, v in exceptions.items() if not k.startswith("$")}
    problems += exception_problems(exceptions, set(rules), {c["id"] for c in cases})
    report = classify(rules, examples, cases, exceptions)
    for unit in spec_units.segment(text):
        for rule in unit["rules"]:
            if rule in report:
                report[rule]["chapter"] = unit["chapter"]
    return report, problems


def counts(report: dict[str, dict]) -> dict[str, int]:
    return {s: sum(1 for row in report.values() if row["status"] == s) for s in STATUSES}


def self_test() -> int:
    problems: list[str] = []
    rules = ["APR-T-001", "APR-T-002", "APR-T-003", "APR-T-004", "APR-T-005", "APR-T-006"]
    examples = [
        {"id": "good", "satisfies": ["APR-T-001", "APR-T-002"]},
        {"id": "bad", "violates": ["APR-T-001", "APR-T-003"]},
    ]
    cases = [
        {"id": "spec:good", "source": "specification", "rules": ["APR-T-004"]},
        {"id": "corpus:x.apr.jsonc", "source": "corpus", "rules": ["APR-T-004"]},
        {"id": "renderer:y", "rules": ["APR-T-005", "APR-T-006"]},
    ]
    exceptions = {"APR-T-006": {"reason": "rendering", "coveredBy": ["renderer:y"]}}
    report = classify(rules, examples, cases, exceptions)
    wanted = {"APR-T-001": "covered", "APR-T-002": "missing-violating", "APR-T-003": "missing-satisfying",
              "APR-T-004": "corpus-only", "APR-T-005": "corpus-only", "APR-T-006": "not-expressible"}
    for rule, status in wanted.items():
        if report[rule]["status"] != status:
            problems.append(f"{rule}: classified {report[rule]['status']}, expected {status}")
    if report["APR-T-004"]["corpus"] != ["corpus:x.apr.jsonc"]:
        problems.append(f"a specification case was counted as corpus: {report['APR-T-004']['corpus']}")
    if classify(["APR-T-009"], [], [], {})["APR-T-009"]["status"] != "uncovered":
        problems.append("a rule nothing cites is not uncovered")

    ids = {c["id"] for c in cases}
    malformed = [
        ("an unstated rule", {"APR-T-099": {"reason": "r", "coveredBy": ["renderer:y"]}}, "states no such rule"),
        ("no reason", {"APR-T-006": {"coveredBy": ["renderer:y"]}}, "without a reason"),
        ("no covering test", {"APR-T-006": {"reason": "r"}}, "without naming the test"),
        ("a covering test that does not exist", {"APR-T-006": {"reason": "r", "coveredBy": ["renderer:nope"]}},
         "neither a case id nor a file"),
    ]
    if exception_problems(exceptions, set(rules), ids):
        problems.append("a well-formed exception reports a problem")
    for name, entry, message in malformed:
        found = exception_problems(entry, set(rules), ids)
        if not any(message in f for f in found):
            problems.append(f"{name}: expected {message!r}, got {found or 'none'}")

    print(f"check-example-coverage self-test: {len(STATUSES)} statuses, {len(malformed)} malformed exceptions")
    for p in problems:
        print(f"  FAIL  {p}")
    print("  all scenarios behave" if not problems else f"{len(problems)} PROBLEM(S)")
    return 1 if problems else 0


def main(argv: list[str]) -> int:
    if "--self-test" in argv:
        return self_test()
    report, problems = load()
    if "--chapter" in argv:
        chapter = int(argv[argv.index("--chapter") + 1])
        report = {rule: row for rule, row in report.items() if row.get("chapter") == chapter}
    totals = counts(report)
    if "--json" in argv:
        print(json.dumps({"counts": totals, "problems": problems, "rules": report}, indent=2))
    else:
        print(f"Example coverage — {len(report)} rules in {SPEC.relative_to(ROOT)}")
        for status, n in totals.items():
            print(f"  {n:5}  {status}")
        print()
        for rule, row in report.items():
            if "--missing" in argv and row["status"] in {"covered", "not-expressible"}:
                continue
            print(f"  {rule:16} {row['status']:19} satisfying {len(row['satisfying'])}, "
                  f"violating {len(row['violating'])}, corpus {len(row['corpus'])}, "
                  f"renderer {len(row['renderer'])}")
        for p in problems:
            print(f"  PROBLEM  {p}")
    if problems:
        return 1
    unfinished = sum(n for s, n in totals.items() if s not in {"covered", "not-expressible"})
    return 1 if "--gate" in argv and unfinished else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

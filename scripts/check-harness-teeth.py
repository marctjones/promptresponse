#!/usr/bin/env python3
"""Prove the conformance harness fails a driver that gets something wrong.

`scripts/run-conformance.py` was exercised by exactly one driver, written by its
own author, which passes everything. That is a positive control with no negative
control: if `evaluation_ok`, the digest comparison, the warning check or the
profile binding silently stopped checking, every gate in this repository would
stay green and the first symptom would be an SDK certified while wrong.

So each mutant below is a driver that is correct except in one named way, and
each declares the least damage the harness must do to it. A mutant the harness
lets through is the bug — in the harness, not in the mutant.

`echo` is the one that mattered most. Before the suite was blinded it scored a
perfect run by copying the answers it was handed, without parsing a document.

    python3 scripts/check-harness-teeth.py
    python3 scripts/check-harness-teeth.py --verbose
"""
from __future__ import annotations

import json
import pathlib
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
HARNESS = ROOT / "scripts" / "run-conformance.py"
REFERENCE = ROOT / "scripts" / "reference-driver.py"

PREAMBLE = '''
import json, sys, pathlib, importlib.util
sys.path.insert(0, {scripts!r})
import aprlib, aprexpr
spec = importlib.util.spec_from_file_location("va", {validator!r})
validate_apr = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validate_apr)
MEMBERS = validate_apr.spec_members()
suite = json.load(sys.stdin)
RUN = suite.get("profiles") or ["core", "core+streams", "core+attestations", "core+expressions"]
results = []
'''

# Each mutant is the body of a per-case loop. `case` is the projected case and
# `answer` is what the reference driver would have said; the mutant damages it.
MUTANTS = {
    "echo": (
        "reports the answers it was handed, parsing nothing",
        '''
for case in suite["cases"]:
    answer = {"id": case["id"], "outcome": case.get("expect", "valid")}
    for field, name in (("digest", "digest"), ("warns", "warnings"),
                        ("diagnostic", "diagnostic")):
        if case.get(field):
            answer[name] = case[field]
    if case.get("expects"):
        answer["evaluated"] = case["expects"]
    results.append(answer)
'''),
    "lazy": (
        "accepts every document without reading it",
        '''
for case in suite["cases"]:
    results.append({"id": case["id"], "outcome": "valid"})
'''),
    "lossy-writer": (
        "drops members it does not recognise when it writes",
        '''
def strip_unknown(node):
    if isinstance(node, dict):
        return {k: strip_unknown(v) for k, v in node.items() if "." not in k}
    if isinstance(node, list):
        return [strip_unknown(v) for v in node]
    return node
for case in suite["cases"]:
    answer = reference(case)
    if case.get("roundTrip") and "written" in answer:
        try:
            records = aprlib.read_records(answer["written"], representation(case))
            answer["written"] = "".join(
                json.dumps(strip_unknown(r), indent=2) + "\\n" for r in records)
        except Exception:
            pass
    results.append(answer)
'''),
    "naive-evaluator": (
        "recomputes over an authored response and reads the host clock",
        '''
import datetime
for case in suite["cases"]:
    answer = reference(case)
    if case.get("evaluates") or case.get("expects"):
        try:
            records = aprlib.read_records(case["document"], representation(case))
            stripped = json.loads(json.dumps(records[0]))
            for prompt, _ in aprexpr.prompts_of(stripped):
                if isinstance((prompt.get("hints") or {}).get("exprValue"), str):
                    prompt.pop("response", None)
            answer["evaluated"] = aprexpr.evaluate(
                stripped, _today=datetime.date.today().isoformat())
        except Exception:
            pass
    results.append(answer)
'''),
    "silent-reader": (
        "accepts and computes correctly but reports no advisory",
        '''
for case in suite["cases"]:
    answer = reference(case)
    answer.pop("warnings", None)
    results.append(answer)
'''),
    "over-strict-reader": (
        "refuses documents it is required to accept",
        '''
for case in suite["cases"]:
    answer = reference(case)
    if answer["outcome"] == "valid" and answer.get("warnings"):
        # An advisory is not an error. A reader that treats one as grounds for
        # refusal loses a document that is valid, which is the failure the
        # advisory rules exist to prevent.
        answer = {"id": case["id"], "outcome": "reject", "diagnostic": "WRONG_TYPE"}
    results.append(answer)
'''),
    "wrong-diagnostic": (
        "refuses for the right documents under the wrong code",
        '''
for case in suite["cases"]:
    answer = reference(case)
    if answer["outcome"] == "reject":
        answer["diagnostic"] = "NULL_DOCUMENT"
    results.append(answer)
'''),
    "over-claiming": (
        "declares every profile and answers only the core cases",
        '''
for case in suite["cases"]:
    if case.get("profile") == "core":
        results.append(reference(case))
'''),
    "attestations-without-streams": (
        "answers every case correctly but claims core+attestations without core+streams",
        '''
for case in suite["cases"]:
    results.append(reference(case))
'''),
    "core-reads-streams": (
        "claims only core, and reads a stream by choosing its records anyway",
        '''
for case in suite["cases"]:
    results.append(reference_driver.answer(case, MEMBERS))
'''),
    "core-refuses-expressions": (
        "claims only core, and refuses a document because it uses expressions",
        '''
import re
for case in suite["cases"]:
    answer = reference(case)
    if re.search(r'"?expr[A-Z]', case["document"]):
        answer = {"id": case["id"], "outcome": "reject", "diagnostic": "EXPRESSIONS_UNSUPPORTED"}
    results.append(answer)
'''),
    "core-drops-expressions": (
        "claims only core, and drops the expression hints it cannot evaluate when it writes",
        '''
def strip_expressions(node):
    if isinstance(node, dict):
        return {k: strip_expressions(v) for k, v in node.items() if not k.startswith("expr")}
    if isinstance(node, list):
        return [strip_expressions(v) for v in node]
    return node
for case in suite["cases"]:
    answer = reference(case)
    if case.get("roundTrip") and "written" in answer:
        records = aprlib.read_records(answer["written"], representation(case))
        answer["written"] = "".join(
            json.dumps(strip_expressions(r), indent=2) + "\\n" for r in records)
    results.append(answer)
'''),
    "over-claiming-run": (
        "answers the core run correctly but claims profiles the run excludes",
        '''
for case in suite["cases"]:
    results.append(reference(case))
'''),
}
PROFILES = {
    "over-claiming": ["core", "core+streams", "core+attestations", "core+expressions"],
    "attestations-without-streams": ["core", "core+attestations"],
    "over-claiming-run": ["core", "core+streams", "core+attestations", "core+expressions"],
}
# Mutants of what core owes the profiles it does not claim are scored the way such an
# implementation is: as a run limited to core.
CORE_RUN = ["--profile", "core"]
RUNS = {name: CORE_RUN for name in ("core-reads-streams", "core-refuses-expressions",
                                     "core-drops-expressions", "over-claiming-run")}

# Every mutant damages the reference driver's own answer. A hand-kept copy of it
# drifted: it named the wrong diagnostics and wrote streams that would not read
# back, so every mutant shared eleven failures that proved nothing about it.
REFERENCE_BODY = '''
spec = importlib.util.spec_from_file_location("reference_driver", {reference!r})
reference_driver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reference_driver)

def representation(case):
    return "yaml" if case["representation"].startswith("yaml") else "jsonc"

def reference(case):
    return reference_driver.answer(case, MEMBERS, RUN)
'''
CONTROL = '''
for case in suite["cases"]:
    results.append(reference(case))
'''
ALL_PROFILES = ["core", "core+streams", "core+attestations", "core+expressions"]


def run(driver: pathlib.Path, arguments: list[str] = ()) -> tuple[int, int, str]:
    """Score `driver` and return (passed, failed, first failing detail)."""
    completed = subprocess.run(
        [sys.executable, str(HARNESS), "--driver", f"{sys.executable} {driver}", "--json",
         *arguments],
        capture_output=True, text=True, cwd=ROOT)
    try:
        report = json.loads(completed.stdout)
    except json.JSONDecodeError:
        return -1, -1, (completed.stderr or completed.stdout).strip()[:300]
    tally = report.get("tally", {})
    failing = [r for r in report.get("cases", []) if r.get("status") != "pass"]
    detail = ""
    if failing:
        detail = f"{failing[0].get('id')}: {failing[0].get('detail', '')}"
    return tally.get("pass", 0), tally.get("fail", 0) + tally.get("unanswered", 0), detail


def main() -> int:
    verbose = "--verbose" in sys.argv
    problems: list[str] = []

    passed, failed, _ = run(REFERENCE)
    if failed != 0:
        problems.append(
            f"the reference driver fails {failed} cases; the mutants below mean "
            f"nothing until it passes")
    print(f"reference driver: {passed} pass, {failed} fail")

    with tempfile.TemporaryDirectory() as workspace:
        def driver(name: str, body: str, profiles: list[str]) -> pathlib.Path:
            source = (PREAMBLE.format(scripts=str(ROOT / "scripts"),
                                      validator=str(ROOT / "scripts" / "validate-apr.py"))
                      + REFERENCE_BODY.format(reference=str(REFERENCE)) + body
                      + f'\njson.dump({{"implementation": {{"name": "{name}", '
                        f'"version": "0", "profiles": {json.dumps(profiles)}}}, '
                        '"results": results}, sys.stdout)\n')
            path = pathlib.Path(workspace) / f"{name}.py"
            path.write_text(source, encoding="utf-8")
            return path

        # The control is a mutant with no damage. It must pass everything, or a
        # mutant's failures could be the base it shares with every other mutant.
        passed, failed, detail = run(driver("control", CONTROL, ALL_PROFILES))
        if failed != 0:
            problems.append(f"the undamaged mutant base fails {failed} cases, so no mutant's "
                            f"failures can be told from it — {detail}")
        print(f"  {'control':20} {passed:>4} pass {failed:>4} fail   the reference answers, undamaged")

        passed, failed, detail = run(driver("control-core", CONTROL, ["core"]), CORE_RUN)
        if failed != 0:
            problems.append(f"the undamaged mutant base fails {failed} cases in a run limited "
                            f"to core, so no core mutant's failures can be told from it — {detail}")
        print(f"  {'control-core':20} {passed:>4} pass {failed:>4} fail   the same, claiming only core in a core run")

        for name, (description, body) in MUTANTS.items():
            passed, failed, detail = run(driver(name, body, PROFILES.get(name, ["core"])),
                                         RUNS.get(name, ()))
            if failed < 0:
                problems.append(f"{name}: the mutant did not run — {detail}")
                continue
            if failed == 0:
                problems.append(
                    f"{name} ({description}) passed every case. The harness does not "
                    f"check what this mutant breaks.")
            print(f"  {name:20} {passed:>4} pass {failed:>4} fail   {description}")
            if verbose and detail:
                print(f"      first failure: {detail}")

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nA harness that passes a broken driver is not measuring anything. Add "
              "the check\nthe mutant escaped, rather than removing the mutant.")
        return 1
    print("\nEvery mutant driver fails at least one case, and the reference driver "
          "passes them all.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

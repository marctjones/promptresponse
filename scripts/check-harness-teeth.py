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
}
PROFILES = {
    "over-claiming": ["core", "core+streams", "core+attestations", "core+expressions"],
}

REFERENCE_BODY = '''
def representation(case):
    return "yaml" if case["representation"].startswith("yaml") else "jsonc"

def reference(case):
    try:
        records = aprlib.read_records(case["document"], representation(case))
    except aprlib.AprError as exc:
        return {"id": case["id"], "outcome": "reject",
                "diagnostic": getattr(exc, "code", None) or "PARSE_ERROR"}
    except Exception:
        return {"id": case["id"], "outcome": "reject", "diagnostic": "PARSE_ERROR"}
    report = validate_apr.Report(case["id"])
    for record in records:
        if isinstance(record, dict) and "recordType" in record:
            validate_apr.validate_attestation(report, record)
        else:
            validate_apr.validate_form(report, record, MEMBERS)
    if report.errors:
        return {"id": case["id"], "outcome": "reject",
                "diagnostic": next(f["code"] for f in report.findings
                                   if f["severity"] == "error")}
    answer = {"id": case["id"], "outcome": "valid",
              "warnings": sorted({f["code"] for f in report.findings
                                  if f["severity"] == "warning"})}
    if len(records) == 1:
        answer["digest"] = aprlib.digest(records[0])
    if case.get("evaluates") or case.get("expects"):
        inputs = case.get("evaluate") or {}
        try:
            answer["evaluated"] = aprexpr.evaluate(
                records[0], _now=inputs.get("_now"), _today=inputs.get("_today"),
                ctx=inputs.get("ctx"))
        except Exception as exc:
            answer["evaluated"] = {"error": type(exc).__name__}
    if case.get("roundTrip"):
        answer["written"] = "".join(
            json.dumps(record, indent=2, ensure_ascii=False) + "\\n" for record in records)
    return answer
'''


def run(driver: pathlib.Path) -> tuple[int, int, str]:
    """Score `driver` and return (passed, failed, first failing detail)."""
    completed = subprocess.run(
        [sys.executable, str(HARNESS), "--driver", f"{sys.executable} {driver}", "--json"],
        capture_output=True, text=True, cwd=ROOT)
    try:
        report = json.loads(completed.stdout)
    except json.JSONDecodeError:
        return -1, -1, (completed.stderr or completed.stdout).strip()[:300]
    tally = report.get("tally", {})
    failing = [r for r in report.get("results", []) if r.get("status") != "pass"]
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
        for name, (description, body) in MUTANTS.items():
            source = (PREAMBLE.format(scripts=str(ROOT / "scripts"),
                                      validator=str(ROOT / "scripts" / "validate-apr.py"))
                      + REFERENCE_BODY + body
                      + f'\njson.dump({{"implementation": {{"name": "{name}", '
                        f'"version": "0", "profiles": {json.dumps(PROFILES.get(name, ["core"]))}}}, '
                        '"results": results}, sys.stdout)\n')
            path = pathlib.Path(workspace) / f"{name}.py"
            path.write_text(source, encoding="utf-8")
            passed, failed, detail = run(path)
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

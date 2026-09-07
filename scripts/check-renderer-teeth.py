#!/usr/bin/env python3
"""Prove the renderer harness fails a renderer that gets something wrong.

The same argument as `check-harness-teeth.py`, one layer up. A scorer exercised
only by a reference driver that passes everything is a positive control with no
negative control, and the renderer scorer is more exposed than the document one:
its checks are judgements about a snapshot rather than comparisons against a
digest, so a check that quietly stopped deciding would look exactly like a check
that passes.

Each mutant renders correctly except in one named way, and each names the rule it
breaks. A mutant that passes every case is a defect in the scorer.

    python3 scripts/check-renderer-teeth.py
    python3 scripts/check-renderer-teeth.py --verbose
"""
from __future__ import annotations

import json
import pathlib
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
HARNESS = ROOT / "scripts" / "run-renderer-conformance.py"
REFERENCE = ROOT / "scripts" / "reference-renderer-driver.py"

# Each mutant is Python applied to the reference driver's snapshot, and the rule it
# is expected to break. `damage(snapshot, case)` returns the damaged snapshot.
MUTANTS = {
    "nameless": ("gives no element an accessible name", "APR-RENDER-001", '''
for node in snapshot["nodes"]:
    node["name"] = ""
'''),
    "role-in-the-name": ("appends a prompt's role to its accessible name", "APR-RENDER-001", '''
document = json.loads(case["document"])
names = {role["id"]: role.get("name") or role["id"] for role in document.get("roles") or []}
for section in document.get("sections") or []:
    for index, prompt in enumerate(section.get("prompts") or []):
        if not prompt.get("role"):
            continue
        pointer = "/sections/0/prompts/%d" % index
        for node in snapshot["nodes"]:
            if node.get("documentPointer") == pointer:
                node["name"] = "%s For %s" % (node["name"], names.get(prompt["role"], prompt["role"]))
'''),
    "placeholder-as-label": ("names a field by its placeholder", "APR-RENDER-002", '''
document = json.loads(case["document"])
for section in document.get("sections") or []:
    for index, prompt in enumerate(section.get("prompts") or []):
        placeholder = (prompt.get("hints") or {}).get("placeholder")
        if not placeholder:
            continue
        pointer = "/sections/0/prompts/%d" % index
        for node in snapshot["nodes"]:
            if node.get("documentPointer") == pointer:
                node["name"] = placeholder
'''),
    "help-text-adjacent": ("puts help text beside a field, not on it", "APR-RENDER-003", '''
for node in snapshot["nodes"]:
    node.pop("helpText", None)
'''),
    "flat": ("renders nesting as indentation, with no structure", "APR-RENDER-004", '''
for node in snapshot["nodes"]:
    node.pop("headingLevel", None)
    if node.get("role") in ("group", "table", "row"):
        node["role"] = "text"
'''),
    "unreachable": ("leaves one field out of the keyboard order", "APR-RENDER-005", '''
for node in snapshot["nodes"]:
    if node.get("role") == "textbox":
        node.pop("keyboardOrder", None)
        break
'''),
    "blocks-saving": ("refuses to save while an advisory stands", "APR-RENDER-006", '''
snapshot["saveResult"] = {"written": False, "blockedBy": "the response is not a number"}
'''),
    "visual-grid": ("draws a table without header association", "APR-RENDER-007", '''
for node in snapshot["nodes"]:
    node.pop("columnHeader", None)
    node.pop("isColumnHeader", None)
'''),
    "reordering": ("reorders the fields it shows", "APR-RENDER-008", '''
orders = sorted(n["keyboardOrder"] for n in snapshot["nodes"] if n.get("keyboardOrder") is not None)
for node in reversed([n for n in snapshot["nodes"] if n.get("keyboardOrder") is not None]):
    node["keyboardOrder"] = orders.pop(0)
'''),
    "writes-back-an-export": ("changes the document when it exports", "APR-RENDER-009", '''
if "exportedDocument" in snapshot:
    exported = json.loads(snapshot["exportedDocument"])
    exported["metadata"]["title"] = exported["metadata"]["title"] + " (exported)"
    snapshot["exportedDocument"] = json.dumps(exported)
'''),
    "executes": ("runs what it should have shown as text", "APR-SEC-009", '''
for node in snapshot["nodes"]:
    node["executed"] = True
'''),
    "fetches-on-open": ("contacts a submission target while rendering", "APR-SEC-010", '''
snapshot["requests"] = ["https://example.gov/drop/a"]
'''),
    "locks-a-computed-field": ("makes a computed total read-only", "APR-EXPR-014", '''
for node in snapshot["nodes"]:
    node["editable"] = False
'''),
    "silent": ("reports a snapshot with no nodes at all", "APR-RENDER-001", '''
snapshot["nodes"] = []
'''),
}

DRIVER = '''
import json, sys, pathlib
sys.path.insert(0, {scripts!r})
import importlib.util
spec = importlib.util.spec_from_file_location("ref", {reference!r})
ref = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ref)

suite = json.load(sys.stdin)
results = []
for case in suite["cases"]:
    snapshot = ref.answer(case)
{damage}
    results.append(snapshot)
json.dump({{"implementation": {{"name": {name!r}, "version": "0",
           "surfaces": ["renderer", "exporter"]}}, "results": results}}, sys.stdout)
'''


def run(driver: pathlib.Path) -> tuple[int, int, list[str]]:
    completed = subprocess.run(
        [sys.executable, str(HARNESS), "--driver", f"{sys.executable} {driver}", "--json"],
        capture_output=True, text=True, cwd=ROOT)
    try:
        report = json.loads(completed.stdout)
    except json.JSONDecodeError:
        return -1, -1, [(completed.stderr or completed.stdout).strip()[:300]]
    tally = report.get("tally", {})
    broken = sorted({rule for row in report.get("results", [])
                     if row.get("status") == "fail" for rule in row.get("rules", [])})
    return tally.get("pass", 0), tally.get("fail", 0), broken


def main() -> int:
    verbose = "--verbose" in sys.argv
    problems: list[str] = []

    passed, failed, _ = run(REFERENCE)
    print(f"reference renderer: {passed} pass, {failed} fail")
    if failed:
        problems.append("the reference renderer fails; the mutants below mean nothing "
                        "until it passes")

    with tempfile.TemporaryDirectory() as workspace:
        for name, (description, rule, damage) in MUTANTS.items():
            body = "\n".join("    " + line for line in damage.strip().splitlines())
            path = pathlib.Path(workspace) / f"{name}.py"
            path.write_text(DRIVER.format(
                scripts=str(ROOT / "scripts"), reference=str(REFERENCE),
                damage=body, name=name), encoding="utf-8")
            passed, failed, broken = run(path)
            if failed < 0:
                problems.append(f"{name}: the mutant did not run — {broken[0]}")
                continue
            if failed == 0:
                problems.append(
                    f"{name} ({description}) passed every case. The scorer does not "
                    f"check what {rule} asks.")
            elif rule not in broken:
                # Failing for the wrong reason is not evidence about this rule.
                problems.append(
                    f"{name} ({description}) failed, but no case citing {rule} was "
                    f"among them: it broke {', '.join(broken) or 'nothing named'}")
            print(f"  {name:24} {passed:>3} pass {failed:>3} fail   {description}")
            if verbose:
                print(f"      broke: {', '.join(broken)}")

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nA scorer that passes a broken renderer measures nothing. Add the check "
              "the mutant\nescaped, rather than removing the mutant.")
        return 1
    print("\nEvery mutant renderer fails at least one case citing the rule it breaks.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

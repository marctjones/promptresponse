#!/usr/bin/env python3
"""Check the specification rewrite against its ledger, so no sentence is missed.

tests/spec-conversion/dispositions.json records one decision for every unit of the
specification as it stood at the baseline commit (units come from spec-units.py).
Each entry keeps the unit's anchor, kind and text, so the ledger is self-contained
and an entry whose identifier no longer matches its own text is caught.

What is checked:

  coverage     every baseline unit has a disposition and a status, and every unit in
               the current specification is either a baseline unit or named in some
               entry's replacedBy; nothing new appears without a decision
  applied      an entry marked applied is true of the current text: a requirement
               carries a keyword and a rule identifier, a deletion is gone, a table
               row sits in a table with Requirement and Rule columns, a rationale
               carries no keyword, and every replacedBy unit exists
  reopened     an applied entry whose unit, or whose replacement, changed since

    python3 scripts/check-spec-conversion.py            # report, never fails
    python3 scripts/check-spec-conversion.py --gate     # fail on any finding
    python3 scripts/check-spec-conversion.py --chapter 5
    python3 scripts/check-spec-conversion.py --json
    python3 scripts/check-spec-conversion.py --self-test
"""
from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
LEDGER = ROOT / "tests" / "spec-conversion" / "dispositions.json"
APPROACH = ROOT / "tests" / "spec-conversion" / "approach.json"
FIXTURE = ROOT / "tests" / "spec-conversion" / "fixture.md"

_spec = importlib.util.spec_from_file_location("spec_units", ROOT / "scripts" / "spec-units.py")
spec_units = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(spec_units)

DISPOSITIONS = {"keep", "requirement", "table-row", "rationale", "definition", "example",
                "delete", "move", "split"}
STATUSES = {"todo", "decided", "applied"}
RULE_ID = re.compile(r"\bAPR-[A-Z]+-\d{3}\b")
# Dispositions whose unit survives unchanged unless replacedBy says otherwise.
SURVIVES = {"keep", "requirement", "table-row", "rationale", "definition", "example"}


def unit_id(anchor: str, kind: str, text: str) -> str:
    digest = hashlib.sha256(" ".join(text.split()).encode("utf-8")).hexdigest()[:10]
    return f"{anchor}/{kind}/{digest}"


def table_of(units: list[dict]) -> dict[str, dict | None]:
    """For each table row, the header of the table it sits in."""
    owner: dict[str, dict | None] = {}
    header = None
    for u in units:
        if u["kind"] == "table-header":
            header = u
        elif u["kind"] == "table-row":
            owner[u["id"]] = header
        else:
            header = None
    return owner


def header_columns(header: dict | None) -> list[str]:
    if not header:
        return []
    return [c.strip().lower() for c in header["text"].strip("|").split("|")]


def load_approach() -> dict:
    return json.loads(APPROACH.read_text(encoding="utf-8")) if APPROACH.exists() else {}


def check(ledger: dict, current: list[dict], approach: dict, chapter: int | None = None) -> dict:
    by_id = {u["id"]: u for u in current}
    rows = table_of(current)
    classes = set(approach.get("classes", []))
    findings: list[dict] = []
    entries = ledger.get("units", {})

    def finding(kind: str, unit: str, detail: str, entry_chapter) -> None:
        if chapter is None or entry_chapter == chapter:
            findings.append({"check": kind, "unit": unit, "detail": detail})

    accounted: set[str] = set()
    for key, e in entries.items():
        ch = e.get("chapter")
        if not key.startswith(unit_id(e.get("anchor", ""), e.get("kind", ""), e.get("text", ""))):
            finding("integrity", key, "the entry's identifier does not match its own anchor, kind and text", ch)
        disposition, status = e.get("disposition"), e.get("status")
        if disposition not in DISPOSITIONS:
            finding("coverage", key, f"disposition {disposition!r} is not one of {sorted(DISPOSITIONS)}", ch)
        if status not in STATUSES:
            finding("coverage", key, f"status {status!r} is not one of {sorted(STATUSES)}", ch)
        replaced = e.get("replacedBy") or []
        accounted.add(key)
        accounted.update(replaced)
        if status != "applied":
            continue

        if classes and e.get("class") and e["class"] not in classes:
            finding("applied", key, f"class {e['class']!r} is not a defined class of product", ch)
        missing = [r for r in replaced if r not in by_id]
        if missing:
            finding("reopened", key, f"replacedBy names units that no longer exist: {missing}", ch)
        if disposition in SURVIVES and not replaced and key not in by_id:
            finding("reopened", key, "the unit changed or moved since it was decided, and names no replacement", ch)
        results = [by_id[r] for r in replaced if r in by_id] or ([by_id[key]] if key in by_id else [])

        if disposition == "delete":
            if key in by_id or any(" ".join(e["text"].split()) == " ".join(u["text"].split()) for u in current):
                finding("applied", key, "marked deleted, but its text is still in the specification", ch)
        elif disposition == "requirement":
            for u in results:
                if not u["keywords"]:
                    finding("applied", key, f"requirement {u['id']} carries no requirement keyword", ch)
                if not RULE_ID.search(u["text"]):
                    finding("applied", key, f"requirement {u['id']} carries no rule identifier", ch)
        elif disposition == "table-row":
            for u in results:
                cols = header_columns(rows.get(u["id"]))
                if u["kind"] != "table-row" or "requirement" not in cols or "rule" not in cols:
                    finding("applied", key, f"{u['id']} is not a row of a table with Requirement and Rule columns", ch)
                elif not RULE_ID.search(u["text"]):
                    finding("applied", key, f"table row {u['id']} carries no rule identifier", ch)
        elif disposition == "rationale":
            for u in results:
                if u["kind"] != "rationale" or u["keywords"]:
                    finding("applied", key, f"{u['id']} is not a rationale block free of requirement keywords", ch)
        elif disposition == "example":
            if not any(u["kind"] == "example" for u in results):
                finding("applied", key, "marked as an example, but no example unit carries it", ch)
        elif disposition == "split":
            if len([r for r in replaced if r in by_id]) < 2:
                finding("applied", key, "marked split, but fewer than two replacement units exist", ch)
        elif disposition == "move" and not replaced:
            finding("applied", key, "marked moved, but names no replacement", ch)

    for u in current:
        if u["id"] not in accounted:
            finding("coverage", u["id"], "a unit in the specification that no ledger entry accounts for", u["chapter"])

    progress: dict[str, dict] = {}
    for e in entries.values():
        if chapter is not None and e.get("chapter") != chapter:
            continue
        row = progress.setdefault(str(e.get("chapter")), {s: 0 for s in sorted(STATUSES)})
        row[e.get("status")] = row.get(e.get("status"), 0) + 1
    return {"baselineCommit": ledger.get("baselineCommit"), "entries": len(entries),
            "currentUnits": len(current), "findings": findings, "progress": progress}


def ledger_from(units: list[dict], disposition: str = "keep", status: str = "applied") -> dict:
    return {"baselineCommit": "fixture", "units": {
        u["id"]: {"anchor": u["anchor"], "chapter": u["chapter"], "kind": u["kind"], "text": u["text"],
                  "disposition": disposition, "status": status} for u in units}}


def self_test() -> int:
    fixture = FIXTURE.read_text(encoding="utf-8")
    units = spec_units.segment(fixture)
    base = ledger_from(units)
    by_text = {u["text"]: u["id"] for u in units}
    problems: list[str] = []

    def expect(name: str, ledger: dict, current: list[dict], wanted: str | None, approach: dict | None = None):
        found = check(ledger, current, approach or {})["findings"]
        kinds = {f["check"] for f in found}
        if wanted is None and found:
            problems.append(f"{name}: expected no findings, got {found[:2]}")
        elif wanted is not None and wanted not in kinds:
            problems.append(f"{name}: expected a {wanted!r} finding, got {sorted(kinds) or 'none'}")

    expect("an untouched specification with every unit kept", base, units, None)

    missing = copy.deepcopy(base)
    del missing["units"][by_text["Alpha paragraph opens the chapter."]]
    expect("a unit with no ledger entry", missing, units, "coverage")

    deleted = copy.deepcopy(base)
    deleted["units"][by_text["An earlier draft said something else."]]["disposition"] = "delete"
    expect("a deletion whose text is still present", deleted, units, "applied")

    no_keyword = copy.deepcopy(base)
    no_keyword["units"][by_text["Alpha paragraph opens the chapter."]]["disposition"] = "requirement"
    expect("a requirement with no keyword", no_keyword, units, "applied")

    dangling = copy.deepcopy(base)
    dangling["units"][by_text["It has two sentences."]]["replacedBy"] = ["first/sentence/0000000000"]
    expect("a replacedBy naming nothing", dangling, units, "reopened")

    tampered = copy.deepcopy(base)
    tampered["units"][by_text["It has two sentences."]]["text"] = "It has three sentences."
    expect("an entry whose text was edited by hand", tampered, units, "integrity")

    edited = spec_units.segment(fixture.replace("one word changes", "one term changes", 1))
    expect("a kept unit whose text changed afterwards", base, edited, "reopened")

    not_a_row = copy.deepcopy(base)
    not_a_row["units"][by_text["A plain quotation."]]["disposition"] = "table-row"
    expect("a table-row that is not in a normative table", not_a_row, units, "applied")

    row_ok = copy.deepcopy(base)
    row_ok["units"][by_text["| `title` | REQUIRED | APR-TEST-002 |"]]["disposition"] = "table-row"
    expect("a row of a table with Requirement and Rule columns", row_ok, units, None)

    lowercase_requirement = copy.deepcopy(base)
    key = by_text["A writer must not do that, and one word changes here. [APR-TEST-001]"]
    lowercase_requirement["units"][key]["disposition"] = "requirement"
    expect("a requirement worded with a lowercase must", lowercase_requirement, units, "applied")

    wrong_class = copy.deepcopy(base)
    wrong_class["units"][by_text["A reader **MUST** keep this."]]["class"] = "wizard"
    expect("a class that is not a defined class of product", wrong_class, units, "applied",
           approach={"classes": ["reader", "writer"]})

    todo = ledger_from(units, status="todo")
    expect("a ledger of undecided units is complete, only unfinished", todo, units, None)

    print(f"check-spec-conversion self-test: {len(units)} fixture units, 12 scenarios")
    for p in problems:
        print(f"  FAIL  {p}")
    print("  all scenarios behave" if not problems else f"{len(problems)} PROBLEM(S)")
    return 1 if problems else 0


def main(argv: list[str]) -> int:
    if "--self-test" in argv:
        return self_test()
    gate = "--gate" in argv
    chapter = int(argv[argv.index("--chapter") + 1]) if "--chapter" in argv else None
    if not LEDGER.exists():
        print(f"{LEDGER.relative_to(ROOT)} does not exist yet; the baseline is committed by #472.")
        return 1 if gate else 0
    ledger = json.loads(LEDGER.read_text(encoding="utf-8"))
    current = spec_units.segment(SPEC.read_text(encoding="utf-8"))
    result = check(ledger, current, load_approach(), chapter)
    unfinished = sum(v for row in result["progress"].values() for k, v in row.items() if k != "applied")
    if "--json" in argv:
        print(json.dumps(result, indent=2))
    else:
        print(f"Specification rewrite — baseline {result['baselineCommit']}, "
              f"{result['entries']} ledger entries, {result['currentUnits']} current units")
        print(f"  {'chapter':>8}  {'todo':>6}  {'decided':>8}  {'applied':>8}")
        for ch, row in sorted(result["progress"].items(), key=lambda kv: (kv[0] == 'None', kv[0].zfill(3))):
            print(f"  {ch:>8}  {row.get('todo', 0):6}  {row.get('decided', 0):8}  {row.get('applied', 0):8}")
        print(f"\n  {len(result['findings'])} finding(s), {unfinished} unit(s) not yet applied")
        for f in result["findings"][:40]:
            print(f"  {f['check']:10} {f['unit']}: {f['detail']}")
    return 1 if gate and (result["findings"] or unfinished) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

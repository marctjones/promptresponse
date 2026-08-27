#!/usr/bin/env python3
"""Measure the real form files against the current specification.

These files are not oracles. Every one was produced by reading a PDF of a government
form and guessing at the APR that would represent it, and most were generated before
the table redesign, the type registry and the canonical write forms existed. Asserting
that they are correct would make stale guesses authoritative over the specification.

So this measures instead of asserting. It reports, per file, how far the document has
drifted from the vocabulary and structure the format now defines. Nothing here fails a
build; the output is evidence for deciding which files to regenerate, and in what order.

    python3 scripts/score-real-files.py [--json tests/real-file-scorecard.json]

The distinction that matters: a synthetic corpus fixture carries its own verdict, and
fixture-plus-verdict together are the specification. A transcribed government form
carries no verdict at all. Its value is shape -- depth, size, hint combinations no
hand-written fixture would think to produce -- not correctness.
"""
import argparse
import json
import pathlib
import sys
from collections import Counter

ROOT = pathlib.Path(__file__).resolve().parent.parent
TYPES = ROOT / "schemas" / "apr-types-1.0.json"
SEARCH_ROOTS = [ROOT / "examples", ROOT / "tests" / "Fixtures"]


def vocabulary():
    registry = json.loads(TYPES.read_text(encoding="utf-8"))
    types = {entry["id"]: entry for entry in registry["expectedDataType"]["types"]}
    # Expression hints are universal under the expression profile, not per-type. An
    # earlier version of this script read only universalHints.always and reported every
    # exprValue/exprHidden in the corpus as an inapplicable hint - which graded a
    # perfectly good file D on the strength of a bug in the grader.
    universal = set(registry["universalHints"]["always"]) | set(
        registry["universalHints"]["expressionProfile"])
    return types, universal


def sections_of(document):
    def walk(sections, path):
        for index, section in enumerate(sections):
            here = f"{path}[{index}]"
            yield here, section
            yield from walk(section.get("sections", []), here + ".sections")
    return walk(document.get("sections", []), "sections")


def looks_like_a_table(section):
    """Rows are sibling sections that all carry prompts and share a prompt shape."""
    children = section.get("sections", [])
    if len(children) < 2 or not all(child.get("prompts") for child in children):
        return False
    shapes = {tuple(p.get("label", "") for p in child["prompts"]) for child in children}
    # Same columns repeated down the rows is what makes it a table rather than
    # a section that happens to have children.
    return len(shapes) == 1


def score(path, types, universal):
    document = json.loads(path.read_text(encoding="utf-8"))
    findings = Counter()
    detail = {}

    unregistered = Counter()
    inapplicable = Counter()
    labels = []
    labels_by_section = []
    missing_help = 0
    prompts_total = 0
    untitled = 0
    unmarked_tables = []

    for where, section in sections_of(document):
        if not (section.get("title") or "").strip():
            untitled += 1
        if looks_like_a_table(section) and section.get("kind") != "table":
            # Advisory. Repeated sections with identical columns are usually a table
            # written before kind existed, but not always: nesting sections without
            # claiming tablehood is perfectly valid (4.5), and at least one showcase
            # section does it deliberately. Reported for review, weighted lightly.
            unmarked_tables.append(f"{where} ({section.get('id')})")

        here_labels = []
        for prompt in section.get("prompts", []):
            prompts_total += 1
            labels.append(prompt.get("label", ""))
            here_labels.append(prompt.get("label", ""))
            hints = prompt.get("hints") or {}
            declared = hints.get("expectedDataType")

            if declared and declared not in types:
                unregistered[declared] += 1

            if not (hints.get("helpText") or "").strip():
                missing_help += 1

            if declared in types:
                allowed = set(types[declared]["meaningfulHints"]) | universal
                for hint in hints:
                    if hint in ("expectedDataType",):
                        continue
                    if hint not in allowed:
                        inapplicable[f"{hint} on {declared}"] += 1

        labels_by_section.append((section.get("id"), here_labels))

    # Two different questions. A form that asks for "City" in a previous-address block
    # and again in an employer-address block is behaving normally - the section is the
    # context that tells them apart. Two prompts labelled "City" inside the SAME section
    # are genuinely ambiguous, to a screen reader and to a person.
    duplicate_labels = [
        f"{section_id}: {label}"
        for section_id, section_labels in labels_by_section
        for label, n in Counter(section_labels).items()
        if n > 1 and label
    ]
    repeated_across_sections = sum(
        1 for label, n in Counter(labels).items() if n > 1 and label
    )

    findings["unregistered-type-uses"] = sum(unregistered.values())
    findings["table-shaped-but-unmarked"] = len(unmarked_tables)
    findings["inapplicable-hints"] = sum(inapplicable.values())
    findings["untitled-sections"] = untitled
    findings["duplicate-labels-in-one-section"] = len(duplicate_labels)
    findings["prompts-without-help-text"] = missing_help

    detail["unregisteredTypes"] = dict(unregistered)
    detail["unmarkedTables"] = unmarked_tables
    detail["inapplicableHints"] = dict(inapplicable)
    detail["duplicateLabelsInOneSection"] = duplicate_labels[:10]
    detail["labelsRepeatedAcrossSections"] = repeated_across_sections
    detail["prompts"] = prompts_total
    detail["version"] = document.get("version")
    detail["documentType"] = document.get("documentType")

    # A grade, weighted by how much each drift actually costs a reader.
    # Unregistered types and unmarked tables change how a document is understood;
    # missing help text only makes it poorer.
    weighted = (
        findings["unregistered-type-uses"] * 2
        + findings["table-shaped-but-unmarked"] * 1
        + findings["inapplicable-hints"] * 1
        + findings["untitled-sections"] * 5
        + findings["duplicate-labels-in-one-section"] * 3
    )
    per_prompt = weighted / prompts_total if prompts_total else 0
    grade = "A" if per_prompt == 0 else "B" if per_prompt < 0.1 else \
            "C" if per_prompt < 0.3 else "D" if per_prompt < 0.6 else "F"

    return {
        "file": str(path.relative_to(ROOT)),
        "grade": grade,
        "driftPerPrompt": round(per_prompt, 3),
        "findings": dict(findings),
        "detail": detail,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", type=pathlib.Path, default=ROOT / "tests" / "real-file-scorecard.json")
    args = parser.parse_args()

    types, universal = vocabulary()
    files = sorted(
        (f for root in SEARCH_ROOTS if root.is_dir()
           for f in root.rglob("*.apr*")
           if "bin" not in f.parts and "obj" not in f.parts),
        key=str,
    )
    if not files:
        sys.exit("no real form files found under examples/ or tests/Fixtures/")

    results = [score(f, types, universal) for f in files]

    print(f"{'file':56} {'grade':6} {'drift':>6}  findings")
    for r in results:
        summary = ", ".join(f"{k}={v}" for k, v in r["findings"].items() if v)
        print(f"{r['file']:56} {r['grade']:6} {r['driftPerPrompt']:>6}  {summary or 'clean'}")

    grades = Counter(r["grade"] for r in results)
    print("\ngrades: " + "  ".join(f"{g}={grades[g]}" for g in "ABCDF" if grades[g]))
    print(f"registered expectedDataType values: {len(types)}")

    args.json.write_text(json.dumps({
        "$comment": "Measurement, not a gate. These files are transcriptions, not oracles; "
                    "see scripts/score-real-files.py for why they are never asserted correct.",
        "registeredTypes": sorted(types),
        "files": results,
    }, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {args.json.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

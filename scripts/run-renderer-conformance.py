#!/usr/bin/env python3
"""Score a renderer driver against the renderer conformance suite.

A renderer driver reads `tests/Conformance/beta6/renderer-suite.json` on standard
input and writes an interaction snapshot per case on standard output, in the shape
`docs/RENDERER_CONFORMANCE.md` defines.

Each check below is one rule from chapter 13 or 14, turned into a question about
the snapshot rather than about the document. Where a check cannot be decided from
what the driver reported, the case fails: a renderer that says less is not a
renderer that did more.

    python3 scripts/run-renderer-conformance.py --driver "./my-renderer-driver"
    python3 scripts/run-renderer-conformance.py --driver "…" --json
"""
from __future__ import annotations

import json
import pathlib
import shlex
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SUITE = ROOT / "tests" / "Conformance" / "beta6" / "renderer-suite.json"

# Fields that state the answer. A driver never sees them, for the same reason the
# document driver does not: a driver handed the expected result can report it.
#
# `hidden` lists the prompts a truthy `exprHidden` asks a renderer to hide. The scorer
# cannot work that out for itself without evaluating CEL, and the harness keeps its
# oracle free of any SDK, so the case states it and the driver is not told.
ANSWERS = ("asks", "hidden")

STRUCTURAL = {"group", "heading", "table", "row"}


def blind(suite: dict) -> dict:
    """The suite as a driver sees it: the documents, never what is asked of them."""
    return {**suite, "cases": [{k: v for k, v in case.items() if k not in ANSWERS}
                               for case in suite["cases"]]}


def nodes_of(snapshot: dict) -> list[dict]:
    return [n for n in (snapshot.get("nodes") or []) if isinstance(n, dict)]


def by_pointer(snapshot: dict) -> dict[str, dict]:
    return {n["documentPointer"]: n for n in nodes_of(snapshot) if n.get("documentPointer")}


def walk(document: dict):
    """(pointer, kind, member) for every section and prompt, in document order."""
    def sections(items, prefix):
        for index, section in enumerate(items or []):
            pointer = f"{prefix}/sections/{index}"
            yield pointer, "section", section
            for position, prompt in enumerate(section.get("prompts") or []):
                yield f"{pointer}/prompts/{position}", "prompt", prompt
            yield from sections(section.get("sections"), pointer)
    yield from sections(document.get("sections"), "")


def depth_of(pointer: str) -> int:
    return pointer.count("/sections/")


# ── one function per rule ───────────────────────────────────────────────────────

def accessible_name(case, document, snapshot):
    found, hidden = by_pointer(snapshot), hidden_prompts(case)
    for pointer, kind, member in walk(document):
        node = found.get(pointer)
        if node is None:
            if pointer in hidden:
                continue    # asked to be hidden, and hiding it is not a defect
            return f"nothing was rendered for {pointer}"
        expected = member.get("title") if kind == "section" else member.get("label")
        if (node.get("name") or "") != expected:
            return (f"{pointer} is named {node.get('name')!r}, where the document "
                    f"names it {expected!r}")
    return None


def placeholder_is_not_a_label(case, document, snapshot):
    found = by_pointer(snapshot)
    for pointer, kind, member in walk(document):
        if kind != "prompt":
            continue
        placeholder = (member.get("hints") or {}).get("placeholder")
        node = found.get(pointer)
        if placeholder and node and node.get("name") == placeholder:
            return (f"{pointer} took its name from the placeholder; a placeholder "
                    f"disappears when somebody types and is invisible to much "
                    f"assistive technology")
    return None


def help_text_is_associated(case, document, snapshot):
    found = by_pointer(snapshot)
    for pointer, kind, member in walk(document):
        if kind != "prompt":
            continue
        help_text = (member.get("hints") or {}).get("helpText")
        if not help_text:
            continue
        node = found.get(pointer) or {}
        described = node.get("helpText") or node.get("describedBy")
        if not described:
            return (f"{pointer} carries helpText that reached no node; adjacent is "
                    f"not associated")
    return None


def nesting_is_structural(case, document, snapshot):
    found = by_pointer(snapshot)
    levels = {}
    for pointer, kind, _ in walk(document):
        if kind != "section":
            continue
        node = found.get(pointer) or {}
        level = node.get("headingLevel")
        role = node.get("role")
        if level is None and role not in STRUCTURAL:
            return (f"{pointer} conveys its nesting neither as a heading level nor as "
                    f"a group; indentation alone is not structure")
        levels[depth_of(pointer)] = level
    deeper = [d for d in sorted(levels) if levels.get(d) is not None]
    for outer, inner in zip(deeper, deeper[1:]):
        if levels[inner] is not None and levels[outer] is not None \
                and levels[inner] <= levels[outer]:
            return (f"a section nested at depth {inner} reports heading level "
                    f"{levels[inner]}, no deeper than its parent's {levels[outer]}")
    return None


def hidden_prompts(case) -> set[str]:
    """The prompts this case's expressions ask a renderer to hide.

    A renderer implementing `core+expressions` hides them; a core one does not, and
    both conform. So this exempts rather than requires: a prompt named here may be
    absent from the interface, and a prompt not named here may not.
    """
    return set(case.get("hidden") or ())


def every_prompt_is_reachable(case, document, snapshot):
    """APR-RENDER-005: reachable *and completable*, and the second half is optional.

    A driver that renders to markup cannot type, so it cannot honestly say whether a
    field can be filled in from the keyboard; one driving a live application can. So
    `completedByKeyboard` is a claim a driver makes only where it proved it, and saying
    it is false fails the case. Absent means unproven, which is not the same as passing
    and is why the driver that can prove it says so.
    """
    found, hidden = by_pointer(snapshot), hidden_prompts(case)
    for pointer, kind, _ in walk(document):
        if kind != "prompt" or pointer in hidden:
            continue
        node = found.get(pointer) or {}
        if node.get("keyboardOrder") is None:
            return f"{pointer} is not in the keyboard order, so it cannot be completed"
        if node.get("completedByKeyboard") is False:
            return (f"{pointer} takes focus and does not accept a response from the "
                    f"keyboard; reaching a field is not completing it")
        if node.get("reachableBackwards") is False:
            return (f"{pointer} is reachable forwards and not backwards; a field somebody "
                    f"can Tab into and not Tab back to is reachable only on a checklist")
    return None


def saving_is_not_blocked(case, document, snapshot):
    save = snapshot.get("saveResult") or {}
    if save.get("written") is not True:
        return (f"the save was not written, blocked by {save.get('blockedBy')!r}; an "
                f"advisory never prevents saving")
    return None


def cells_name_their_header(case, document, snapshot):
    found = by_pointer(snapshot)
    for pointer, kind, section in walk(document):
        if kind != "section" or section.get("kind") != "table":
            continue
        rows = section.get("sections") or []
        for row_index, row in enumerate(rows):
            for cell_index, _ in enumerate(row.get("prompts") or []):
                cell = found.get(
                    f"{pointer}/sections/{row_index}/prompts/{cell_index}") or {}
                header = cell.get("columnHeader")
                if not header:
                    return (f"a cell in {pointer} names no column header, so it is a "
                            f"visual grid rather than a table")
                named = next((n for n in nodes_of(snapshot)
                              if n.get("id") == header or n.get("documentPointer") == header), None)
                if named is None or not named.get("isColumnHeader"):
                    return (f"a cell in {pointer} names {header!r} as its header, and "
                            f"that node does not say it is one")
                # And the right one. A cell that names a column header — any column
                # header — has an association a screen reader will read out wrongly,
                # which is worse than none: the correspondence between an instance's
                # fields and the first instance's is positional, so the header for
                # position N carries the label of the first instance's Nth field.
                column = (rows[0].get("prompts") or [])[cell_index:cell_index + 1]
                expected = column[0].get("label") if column else None
                if expected is not None and (named.get("name") or "") != expected:
                    return (f"a cell in column {cell_index} of {pointer} names a header "
                            f"called {named.get('name')!r}, where that column is "
                            f"{expected!r}")
    return None


def order_is_document_order(case, document, snapshot):
    found = by_pointer(snapshot)
    previous_pointer, previous_order = None, None
    for pointer, kind, _ in walk(document):
        if kind != "prompt":
            continue
        order = (found.get(pointer) or {}).get("keyboardOrder")
        if order is None:
            continue
        if previous_order is not None and order < previous_order:
            return (f"{pointer} comes before {previous_pointer} in the keyboard order "
                    f"and after it in the document; a renderer may paginate but not "
                    f"reorder")
        previous_pointer, previous_order = pointer, order
    return None


def nothing_executes(case, document, snapshot):
    for node in nodes_of(snapshot):
        if node.get("executed"):
            return f"{node.get('documentPointer')} reports having executed something"
        if node.get("role") in {"script", "iframe", "embed", "object"}:
            return (f"{node.get('documentPointer')} was rendered as "
                    f"{node.get('role')!r}; a document's text is text")
    return None


def opening_fetches_nothing(case, document, snapshot):
    requests = snapshot.get("requests")
    if requests is None:
        return "the driver did not say what it requested, so nothing shows it fetched nothing"
    if requests:
        return (f"rendering made {len(requests)} request(s), the first to "
                f"{requests[0]!r}; a submission target is data until somebody acts")
    return None


def the_required_depth_renders(case, document, snapshot):
    deepest = max((depth_of(p) for p, kind, _ in walk(document) if kind == "section"),
                  default=0)
    if deepest < 16:
        return f"this case is meant to be sixteen levels deep and is {deepest}"
    if not by_pointer(snapshot):
        return "nothing was rendered; sixteen levels is a depth the format requires"
    return every_prompt_is_reachable(case, document, snapshot)


def computed_stays_editable(case, document, snapshot):
    found = by_pointer(snapshot)
    for pointer, kind, member in walk(document):
        if kind != "prompt" or not (member.get("hints") or {}).get("exprValue"):
            continue
        node = found.get(pointer) or {}
        if node.get("editable") is not True:
            return (f"{pointer} is computed and not editable; a total that is wrong "
                    f"must be correctable by the person filling the form")
    return None


def an_export_is_not_written_back(case, document, snapshot):
    exported = snapshot.get("exportedDocument")
    if exported is None:
        return "the driver reported no exportedDocument, so nothing shows the document survived"
    if json.loads(exported) != document:
        return "the document changed when it was exported; an export is a derived artefact"
    return None


CHECKS = {
    "APR-RENDER-001": accessible_name,
    "APR-RENDER-002": placeholder_is_not_a_label,
    "APR-RENDER-003": help_text_is_associated,
    "APR-RENDER-004": nesting_is_structural,
    "APR-RENDER-005": every_prompt_is_reachable,
    "APR-RENDER-006": saving_is_not_blocked,
    "APR-RENDER-007": cells_name_their_header,
    "APR-RENDER-008": order_is_document_order,
    "APR-RENDER-009": an_export_is_not_written_back,
    "APR-SEC-009": nothing_executes,
    "APR-SEC-010": opening_fetches_nothing,
    "APR-SEC-011": the_required_depth_renders,
    "APR-EXPR-014": computed_stays_editable,
    "APR-MODEL-004": saving_is_not_blocked,
    "APR-VAL-006": saving_is_not_blocked,
    "APR-MODEL-015": the_required_depth_renders,
}


def score(suite: dict, response: dict) -> tuple[list[dict], dict]:
    reported = {r.get("id"): r for r in (response.get("results") or []) if isinstance(r, dict)}
    surfaces = set(response.get("implementation", {}).get("surfaces") or ["renderer"])
    rows, tally = [], {"pass": 0, "fail": 0, "unanswered": 0}

    for case in suite["cases"]:
        row = {"id": case["id"], "rules": case["rules"]}
        if case["surface"] not in surfaces:
            # A surface may decline what it is not. An exporter answers the export
            # rule; a renderer that does not export is not failing it.
            row["status"] = "unanswered"
            row["detail"] = f"no {case['surface']} surface was declared"
            tally["unanswered"] += 1
            rows.append(row)
            continue
        snapshot = reported.get(case["id"])
        if snapshot is None:
            row["status"] = "fail"
            row["detail"] = "the driver declared this surface and did not answer this case"
            tally["fail"] += 1
            rows.append(row)
            continue

        document = json.loads(case["document"])
        failure = next((detail for rule in case["rules"]
                        if (detail := CHECKS[rule](case, document, snapshot))), None)
        row["status"] = "fail" if failure else "pass"
        if failure:
            row["detail"] = failure
        tally["fail" if failure else "pass"] += 1
        rows.append(row)
    return rows, tally


def main() -> int:
    driver = next((sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--driver"), None)
    if not driver:
        print('Usage: run-renderer-conformance.py --driver "./renderer-driver" [--json]')
        return 2
    if not SUITE.exists():
        print("renderer-suite.json is missing; run scripts/build-renderer-suite.py --write")
        return 2

    suite = json.loads(SUITE.read_text(encoding="utf-8"))
    asked = json.dumps(suite if "--with-answers" in sys.argv else blind(suite), indent=2)
    completed = subprocess.run(shlex.split(driver), input=asked,
                               capture_output=True, text=True, timeout=600)
    if completed.returncode != 0:
        print(f"driver exited {completed.returncode}")
        print(completed.stderr.strip()[:2000])
        return 2
    try:
        response = json.loads(completed.stdout)
    except json.JSONDecodeError as exc:
        print(f"driver did not write one JSON object on stdout: {exc}")
        return 2

    rows, tally = score(suite, response)
    if "--json" in sys.argv:
        print(json.dumps({"implementation": response.get("implementation"),
                          "tally": tally, "results": rows}, indent=2))
        return 1 if tally["fail"] else 0

    implementation = response.get("implementation", {})
    print(f"{implementation.get('name', 'unnamed')} "
          f"{implementation.get('version', '')}   "
          f"surfaces: {', '.join(implementation.get('surfaces') or ['renderer'])}")
    print(f"suite {suite['suiteVersion']}, {len(suite['cases'])} cases\n")
    for row in rows:
        if row["status"] != "pass":
            print(f"  {row['status'].upper():10} {row['id']}")
            print(f"        {row['detail']}")
    print(f"\npassed {tally['pass']}, failed {tally['fail']}, "
          f"unanswered {tally['unanswered']}")
    return 1 if tally["fail"] else 0


if __name__ == "__main__":
    sys.exit(main())

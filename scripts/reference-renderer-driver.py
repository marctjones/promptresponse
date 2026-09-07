#!/usr/bin/env python3
"""A worked renderer driver: renders each case and reports what a person faces.

Not a user interface. It walks the semantic model and emits the interaction
snapshot `docs/RENDERER_CONFORMANCE.md` defines — the accessible names, heading
levels, help-text associations, header relations and keyboard order a conforming
renderer would produce. What it demonstrates is the shape of a correct answer, so
a real renderer's driver has something to be measured against and the scorer has
something honest to score.

It declares both surfaces, because it both "renders" and "exports": a real
project splits those across an interactive client and an exporter.

    python3 scripts/run-renderer-conformance.py \\
        --driver "python3 scripts/reference-renderer-driver.py"
"""
from __future__ import annotations

import json
import sys


def render(document: dict) -> dict:
    """Walk the document and describe the interface it should produce."""
    nodes: list[dict] = []
    order = iter(range(10_000))

    def section(node: dict, pointer: str, depth: int) -> None:
        heading = min(depth, 6)
        table = node.get("kind") == "table"
        nodes.append({
            "id": node.get("id"),
            "role": "table" if table else "group",
            "name": node.get("title"),
            # Depth is conveyed as a heading level, not as indentation. Six is the
            # floor every accessibility layer carries; deeper nesting keeps the group
            # relation, which is why both are reported.
            "headingLevel": heading,
            "documentPointer": pointer,
            "keyboardOrder": next(order),
        })

        rows = node.get("sections") or []
        # A table's first instance carries its field names, so its cells are the
        # column headers every other instance's cells point at.
        headers = {}
        if table and rows:
            for index, cell in enumerate(rows[0].get("prompts") or []):
                headers[index] = f"{pointer}/sections/0/prompts/{index}"

        for index, prompt in enumerate(node.get("prompts") or []):
            emit_prompt(prompt, f"{pointer}/prompts/{index}", None, False)

        for row_index, row in enumerate(rows):
            row_pointer = f"{pointer}/sections/{row_index}"
            if table:
                nodes.append({
                    "id": row.get("id"),
                    "role": "row",
                    "name": row.get("title"),
                    "documentPointer": row_pointer,
                    "keyboardOrder": next(order),
                })
                for index, cell in enumerate(row.get("prompts") or []):
                    emit_prompt(cell, f"{row_pointer}/prompts/{index}",
                                headers.get(index), row_index == 0)
                for nested_index, nested in enumerate(row.get("sections") or []):
                    section(nested, f"{row_pointer}/sections/{nested_index}", depth + 2)
            else:
                section(row, row_pointer, depth + 1)

    def emit_prompt(prompt: dict, pointer: str, header: str | None, is_header: bool) -> None:
        hints = prompt.get("hints") or {}
        node = {
            "id": prompt.get("id"),
            "role": "textbox",
            # The label, never the placeholder. A placeholder disappears when
            # somebody types and leaves the field permanently unnamed.
            "name": prompt.get("label"),
            "documentPointer": pointer,
            "keyboardOrder": next(order),
            # Being computed does not make a field read-only: a total that is wrong
            # must be correctable by the person filling the form.
            "editable": True,
            "value": prompt.get("response", ""),
        }
        if hints.get("helpText"):
            node["helpText"] = hints["helpText"]
        if header is not None:
            node["columnHeader"] = header
        if is_header:
            node["isColumnHeader"] = True
        nodes.append(node)

    for index, top in enumerate(document.get("sections") or []):
        section(top, f"/sections/{index}", 1)
    return {"nodes": nodes}


def answer(case: dict) -> dict:
    document = json.loads(case["document"])
    snapshot = render(document)
    snapshot["id"] = case["id"]
    # A hint mismatch is advisory, so a save is written. Nothing here blocks it,
    # which is the whole of what the rule asks.
    snapshot["saveResult"] = {"written": True, "blockedBy": None}
    # Rendering fetches nothing. `submissionUrls` is data until somebody acts.
    snapshot["requests"] = []
    if case.get("surface") == "exporter":
        # An export is a derived artefact and is never written back, so the document
        # leaves exactly as it arrived.
        snapshot["exportedDocument"] = case["document"]
    return snapshot


def main() -> int:
    suite = json.load(sys.stdin)
    json.dump({
        "implementation": {
            "name": "APR reference renderer",
            "version": suite.get("formatVersion", ""),
            "surfaces": ["renderer", "exporter"],
        },
        "results": [answer(case) for case in suite["cases"]],
    }, sys.stdout)
    return 0


if __name__ == "__main__":
    sys.exit(main())

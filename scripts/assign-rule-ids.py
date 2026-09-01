#!/usr/bin/env python3
"""Assign a stable identifier to every normative clause in the specification.

Run once. Identifiers are then fixed in the document and are never regenerated:
scripts/check-spec-shape.py only verifies that every requirement carries one and
that none repeats.

    python3 scripts/assign-rule-ids.py --write

Identifiers are APR-<AREA>-<NNN>, allocated per area in document order at the
time of assignment. They are append-only: a new rule takes the next free number
in its area, a deleted rule's number is retired rather than reused, and moving a
rule to another section does not renumber it. Nothing is positional, so inserting
a requirement cannot renumber its neighbours.

The unit is one requirement-bearing block: a list item, a table row, or a
paragraph. A paragraph stating one obligation in three sentences takes one
identifier; the renderer requirements, which are genuinely independent, are a
list and take one each.
"""
from __future__ import annotations

import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"

NORMATIVE = re.compile(r"\*\*(MUST NOT|MUST|SHALL NOT|SHALL|REQUIRED|SHOULD NOT|SHOULD|MAY)\*\*")
HEADING = re.compile(r"^(#{2,4})\s+.*\{#([a-z0-9-]+)\}\s*$")
EXISTING = re.compile(r"\[APR-[A-Z]+-\d{3}\]")

# Sections that use the keywords to define or illustrate them rather than to
# impose a requirement.
NOT_REQUIREMENTS = {"normative-language", "conventions"}

AREAS = [
    ("REP", {"representations", "model-layers", "encoding", "syntax-conventions",
             "apr-jsonc", "apr-yaml", "yaml-resolution", "json-subset"}),
    ("MODEL", {"form-model", "root-object", "metadata", "section-object", "prompt-object",
               "response-metadata", "tables", "table-assertion", "table-no-layout",
               "table-rows", "table-ragged", "nesting", "hints-object", "data-types",
               "extensions", "retired-members", "canonical-values", "roles",
               "responses", "any-string", "hints-advisory"}),
    ("VAL", {"validation", "structural-validation", "warnings", "parse-errors",
             "semantic-validation"}),
    ("TEXT", {"text-handling", "text-responses", "authoring-vs-filled",
              "filled-never-rewritten", "authoring-strictness"}),
    ("STREAM", {"streams", "jsonc-framing", "yaml-framing", "stream-equivalence"}),
    ("DIGEST", {"digests"}),
    ("EXPR", {"expressions", "expr-what", "expr-invariants", "expr-language",
              "expr-activation", "expr-binding", "expr-unbound", "expr-context",
              "expr-fallback", "expr-computed", "expr-authoring", "expr-limits"}),
    ("ATTEST", {"attestations", "attestation-model", "attestation-catalogue",
                "attestation-scope", "proofs", "witnesses", "changed-forms",
                "verification", "never-gate"}),
    ("RENDER", {"renderers", "renderer-requirements", "ordering", "export"}),
    ("CONF", {"conformance", "profile-core", "profile-streams", "profile-attestations",
              "profile-expressions", "declaring-conformance", "checklist"}),
    ("SEC", {"security", "media-types", "compatibility", "forward-compatibility",
             "version-compatibility", "extension-governance", "scope",
             "version-numbers", "beta6-boundary", "authority"}),
]


def area_for(anchor: str) -> str:
    for code, anchors in AREAS:
        if anchor in anchors:
            return code
    return "DOC"


def is_block_start(line: str) -> bool:
    stripped = line.lstrip()
    return stripped.startswith(("- ", "* ", "| ")) or bool(re.match(r"^\d+\.\s", stripped))


def main() -> int:
    lines = SPEC.read_text(encoding="utf-8").split("\n")
    if any(EXISTING.search(line) for line in lines):
        print("Rule identifiers are already assigned; this script runs once.")
        return 1

    anchor = "document"
    counters: dict[str, int] = {}
    in_fence = False
    out: list[str] = []
    # Index of the last line of the block currently being accumulated.
    block: list[int] = []
    assigned = 0

    def close_block() -> None:
        nonlocal assigned
        if not block:
            return
        text = "\n".join(out[i] for i in block)
        if NORMATIVE.search(text) and anchor not in NOT_REQUIREMENTS:
            code = area_for(anchor)
            counters[code] = counters.get(code, 0) + 1
            identifier = f"[APR-{code}-{counters[code]:03d}]"
            last = block[-1]
            if out[last].rstrip().endswith("|"):
                out[last] = out[last].rstrip()[:-1].rstrip() + f" {identifier} |"
            else:
                out[last] = out[last].rstrip() + f" {identifier}"
            assigned += 1
        block.clear()

    for raw in lines:
        out.append(raw)
        index = len(out) - 1

        if raw.startswith("```"):
            close_block()
            in_fence = not in_fence
            continue
        if in_fence:
            continue

        heading = HEADING.match(raw)
        if heading:
            close_block()
            anchor = heading.group(2)
            continue
        if raw.startswith(">"):
            close_block()
            continue
        if not raw.strip():
            close_block()
            continue
        if is_block_start(raw):
            close_block()
        block.append(index)

    close_block()

    if "--write" in sys.argv:
        SPEC.write_text("\n".join(out), encoding="utf-8")
        print(f"Assigned {assigned} rule identifiers across {len(counters)} areas.")
        for code in sorted(counters):
            print(f"  APR-{code}-001 .. APR-{code}-{counters[code]:03d}")
        return 0

    print(f"Would assign {assigned} identifiers across {len(counters)} areas. "
          "Re-run with --write.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

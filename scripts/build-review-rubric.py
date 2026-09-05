#!/usr/bin/env python3
"""Build the completeness rubric the local review actually needs.

The first rubric asked seven generic questions — does the document identify its
version, does it describe an abstract model — written before the concept registry
and the member catalogues existed. Every answer came back `addressed`, which is
what a rubric returns when it cannot see anything.

The question worth asking is narrower and only a reader can answer it: **is this
rule stated completely enough to implement from, without inferring anything the
document does not say?** That is asked once per rule, against the rule's own
section rather than the whole specification, so the reviewer has the text in
front of it and nothing else.

The surface is the rules no executable gate covers, from
``scripts/check-spec-completeness.py --review-surface``. A rule a vector already
catches has been answered by a machine; asking a model about it wastes the one
thing the model is for.

    python3 scripts/build-review-rubric.py
    python3 scripts/build-review-rubric.py --area ATTEST --write
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
OUT = ROOT / "tests" / "spec-semantic" / "completeness-rubric.json"
VERSION = "apr-specification-completeness-rubric-1"

HEADING = re.compile(r"^(#{2,4})\s+(.*?)\s*\{#([a-z0-9-]+)\}\s*$", re.MULTILINE)
RULE_ID = re.compile(r"\[(APR-[A-Z]+-\d{3})\]")

QUESTION = (
    "Is {rule} stated completely enough to implement from, using only the text "
    "below and nothing inferred? Answer `addressed` only if an implementer could "
    "build it without guessing. Answer `missing` and say what a reader would have "
    "to invent if any of these is true: the obligation names no actor, the "
    "condition that triggers it is unstated, what to do when it is violated is "
    "unstated, or a term it depends on is used here and defined nowhere."
)
CONTRADICTION = (
    "Do the two passages below state anything that cannot both be true — a rule "
    "one permits and the other forbids, or one term used for two things? Answer "
    "`addressed` only if they are consistent. Quote both sides if they are not."
)


def sections(spec: str) -> list[tuple[int, str, str, str]]:
    """(level, title, anchor, body) with a section running to the next peer."""
    marks = [(m.start(), len(m.group(1)), m.group(2), m.group(3)) for m in HEADING.finditer(spec)]
    out = []
    for index, (position, level, title, anchor) in enumerate(marks):
        end = len(spec)
        for later, later_level, _, _ in marks[index + 1:]:
            if later_level <= level:
                end = later
                break
        out.append((level, title, anchor, spec[position:end]))
    return out


def review_surface() -> list[str]:
    result = subprocess.run(
        [sys.executable, str(ROOT / "scripts" / "check-spec-completeness.py"), "--review-surface"],
        capture_output=True, text=True, check=False)
    return json.loads(result.stdout or "{}").get("rules", [])


def owning_section(body_by_anchor, rule: str) -> tuple[str, str]:
    """The deepest section that states the rule, and its text.

    Deepest, because a chapter contains its subsections: crediting the chapter
    would hand the reviewer ten pages when the rule lives in one paragraph.
    """
    best = None
    for level, title, anchor, body in body_by_anchor:
        if f"[{rule}]" in body and (best is None or level > best[0]):
            best = (level, f"{title} (#{anchor})", body)
    return (best[1], best[2]) if best else ("", "")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--area", help="Restrict to one rule area, e.g. ATTEST.")
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()

    spec = SPEC.read_text(encoding="utf-8")
    bodies = sections(spec)
    surface = review_surface()
    if args.area:
        surface = [r for r in surface if r.split("-")[1] == args.area.upper()]

    items, orphans = [], []
    for rule in surface:
        where, body = owning_section(bodies, rule)
        if not body:
            orphans.append(rule)
            continue
        items.append({
            "id": f"completeness:{rule}",
            "question": QUESTION.format(rule=rule),
            "rule": rule,
            "section": where,
            "excerpt": body.strip(),
        })

    # Contradiction pairs: sections that state rules about the same member or
    # concept. Cheap to build, and the failure it looks for is the one no
    # per-rule question can see.
    PAIRS = [
        ("hints-advisory", "structural-validation"),
        ("responses", "canonical-values"),
        ("warnings", "human-text"),
        ("never-gate", "verification"),
        ("extensions", "retired-members"),
        ("expr-computed", "hints-advisory"),
        ("submission", "attestation-scope"),
    ]
    by_anchor = {anchor: (title, body) for _, title, anchor, body in bodies}
    for left, right in PAIRS:
        if left in by_anchor and right in by_anchor:
            items.append({
                "id": f"consistency:{left}-vs-{right}",
                "question": CONTRADICTION,
                "rule": "",
                "section": f"#{left} against #{right}",
                "excerpt": f"PASSAGE A\n\n{by_anchor[left][1].strip()}\n\nPASSAGE B\n\n{by_anchor[right][1].strip()}",
            })

    rubric = {"version": VERSION, "surface": len(surface), "items": items}
    if args.write:
        OUT.write_text(json.dumps(rubric, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"Wrote {OUT.relative_to(ROOT)}: {len(items)} items over {len(surface)} ungated rules")
    else:
        print(f"{len(items)} items over {len(surface)} ungated rules "
              f"({len(items) - len(surface) + len(orphans)} consistency pairs)")
        print(f"Largest excerpt: {max((len(i['excerpt']) for i in items), default=0)} characters")
    for rule in orphans:
        print(f"  no section states {rule}", file=sys.stderr)
    return 1 if orphans else 0


if __name__ == "__main__":
    sys.exit(main())

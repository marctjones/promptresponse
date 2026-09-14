#!/usr/bin/env python3
"""Generate the APR Implementation Conformance Statement from the OSCAL catalog.

A hand-written list of what a conformance claim covers is a second copy of the
rules, and a second copy disagrees with the first as soon as either changes. This
statement is derived instead: scripts/build-oscal.py records each rule's profile,
requirement level and subject, and this lists every rule by profile and by the
class of product it is a requirement on. An implementer fills in the last column.

    python3 scripts/build-conformance-statement.py            # verify it is current
    python3 scripts/build-conformance-statement.py --write    # regenerate it

The statement is written to docs/release/APR_CONFORMANCE_STATEMENT.md.
"""
from __future__ import annotations

import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
CATALOG = ROOT / "docs" / "release" / "apr-oscal-catalog.json"
OUT = ROOT / "docs" / "release" / "APR_CONFORMANCE_STATEMENT.md"

PROFILES = ("core", "core+streams", "core+attestations", "core+expressions")
SUBJECTS = {
    "implementation": "Implementation",
    "reader": "Reader",
    "writer": "Writer",
    "validator": "Validator",
    "renderer": "Renderer",
    "verifier": "Verifier",
    "host": "Host",
    "document": "Documents",
}


def prop(control: dict, name: str) -> str:
    return next(p["value"] for p in control["props"] if p["name"] == name)


def cell(text: str) -> str:
    # A rule's own links point into the specification, and this file is not it.
    text = text.replace("](#", "](../APR_SPECIFICATION.md#")
    return " ".join(text.split()).replace("|", "\\|")


def build() -> str:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))["catalog"]
    controls = [c for group in catalog["groups"] for c in group["controls"]]
    unknown = sorted({prop(c, "profile") for c in controls} - set(PROFILES)
                     | {prop(c, "subject") for c in controls} - set(SUBJECTS))
    if unknown:
        raise SystemExit(f"the catalog names profiles or subjects this statement does not list: {unknown}")

    version = next((p["value"] for p in catalog["metadata"]["props"] if p["name"] == "format-version"), "")
    lines = [
        f"# APR {version} Implementation Conformance Statement",
        "",
        "Generated from [the OSCAL rule catalog](apr-oscal-catalog.json) by",
        "`scripts/build-conformance-statement.py`. Do not edit; regenerate it.",
        "",
        "The [specification](../APR_SPECIFICATION.md) is normative, and every row here",
        "links to the rule it lists. Rules are grouped by the conformance profile that",
        "defines them and by the class of product each is a requirement on. A claim of a",
        "profile covers every row under it, and a claim of an optional profile also covers",
        "`core`. Documents lists requirements on a form or record, which a validator or",
        "reader checks.",
        "",
        "To state conformance, copy this file and fill in the last column of each row a",
        "claim covers: **Yes**, **No**, or **N/A** where the implementation is not that",
        "class of product.",
        "",
    ]
    for profile in PROFILES:
        mine = [c for c in controls if prop(c, "profile") == profile]
        lines += [f"## `{profile}`", "", f"{len(mine)} rules.", ""]
        for subject, title in SUBJECTS.items():
            rows = [c for c in mine if prop(c, "subject") == subject]
            if not rows:
                continue
            lines += [f"### {title}", "", "| Rule | Level | Requirement | Supported |", "| --- | --- | --- | --- |"]
            for control in sorted(rows, key=lambda c: (prop(c, "area"), prop(c, "rule-id"))):
                rule = prop(control, "rule-id")
                href = control["links"][0]["href"]
                statement = next(p["prose"] for p in control["parts"] if p["name"] == "statement")
                lines.append(f"| [{rule}]({href}) | {prop(control, 'level')} | {cell(statement)} | |")
            lines.append("")
    return "\n".join(lines)


def main() -> int:
    text = build()
    if "--write" in sys.argv:
        OUT.write_text(text, encoding="utf-8")
        print(f"Wrote {OUT.relative_to(ROOT)}")
        return 0
    if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
        print(f"{OUT.relative_to(ROOT)} is stale; run scripts/build-conformance-statement.py --write")
        return 1
    print("The conformance statement agrees with the OSCAL catalog.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

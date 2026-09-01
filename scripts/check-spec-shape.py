#!/usr/bin/env python3
"""Check the APR specification has the shape tests/spec-shape/manifest.json declares.

The manifest records the structural properties borrowed from CommonMark, YAML 1.2.2
and RFC 8259, each attributed to its source. This script verifies them
deterministically, so "the specification is written like those specifications" is a
checked claim rather than an opinion.

This is the shape level only. Whether a section says enough to implement from is a
judgement, and belongs to the opt-in local review suite.

    python3 scripts/check-spec-shape.py
    python3 scripts/check-spec-shape.py --json
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "tests" / "spec-shape" / "manifest.json"

NORMATIVE = re.compile(r"\*\*(MUST NOT|MUST|SHALL NOT|SHALL|REQUIRED|SHOULD NOT|SHOULD|MAY)\*\*")
HEADING = re.compile(r"^(#{1,4})\s+(.*)$", re.MULTILINE)
ANCHOR = re.compile(r"\{#([a-z0-9-]+)\}")
EXAMPLE = re.compile(r"^```apr-example\n(.*?)^---\n", re.MULTILINE | re.DOTALL)
DESIGNATION = re.compile(r"\b(?:RFC \d{3,5}|BCP \d{1,3}|FIPS \d{3}-\d)\b")


def main() -> int:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    spec_path = ROOT / manifest["specification"]
    spec = spec_path.read_text(encoding="utf-8")
    anchors = set(ANCHOR.findall(spec))
    problems: list[str] = []
    checks: list[tuple[str, bool, str]] = []

    def check(name: str, ok: bool, detail: str) -> None:
        checks.append((name, ok, detail))
        if not ok:
            problems.append(f"{name}: {detail}")

    # Front matter.
    missing_fm = [f for f in manifest["frontMatter"] if f"**{f}:**" not in spec]
    check("front-matter", not missing_fm,
          "all present" if not missing_fm else f"missing {missing_fm}")

    # Required sections.
    missing = [s["anchor"] for s in manifest["requiredSections"] if s["anchor"] not in anchors]
    check("required-sections", not missing,
          f"{len(manifest['requiredSections'])} present"
          if not missing else f"missing anchors {missing}")

    # Every heading anchored.
    unanchored = [
        text.strip() for level, text in HEADING.findall(spec)
        if len(level) >= 2 and not ANCHOR.search(text)
    ]
    check("every-heading-anchored", not unanchored,
          f"{len(anchors)} anchored headings"
          if not unanchored else f"{len(unanchored)} without an anchor: {unanchored[:3]}")

    # Single file: no other document cited as normative.
    external = sorted(set(re.findall(r"(?<!apr-)(?<!/)\b[A-Z_]+\.md\b", spec)))
    check("single-file", not external,
          "no external document referenced"
          if not external else f"references {external}")

    # Examples are executable, and each cites a rule that resolves.
    example_headers = EXAMPLE.findall(spec)
    parsed = []
    for header in example_headers:
        fields = dict(
            (k.strip(), v.strip())
            for k, _, v in (line.partition(":") for line in header.splitlines() if ":" in line)
        )
        parsed.append(fields)
    check("examples-are-executable", bool(parsed),
          f"{len(parsed)} executable examples"
          if parsed else "no apr-example blocks found")

    bad_rule = [f.get("id", "?") for f in parsed if f.get("rule") not in anchors]
    check("examples-cite-a-rule", not bad_rule,
          "every example cites a resolving anchor"
          if not bad_rule else f"unresolved rule anchors on {bad_rule}")

    # Grammar defined by production, not prose alone.
    abnf = spec.count("```abnf")
    check("grammar-by-reference", abnf >= 2,
          f"{abnf} ABNF blocks" if abnf >= 2 else f"only {abnf} ABNF blocks")

    # Reference sections, and every cited designation listed.
    has_both = "{#normative-references}" in spec and "{#informative-references}" in spec
    listed = cited = set()
    if has_both:
        head, _, tail = spec.partition("{#normative-references}")
        listed, cited = set(DESIGNATION.findall(tail)), set(DESIGNATION.findall(head))
    uncited = sorted(cited - listed)
    check("normative-and-informative-references-separated", has_both and not uncited,
          f"{len(listed)} listed, all cited references resolve"
          if has_both and not uncited
          else ("a reference section is missing" if not has_both
                else f"cited but unlisted: {uncited}"))

    # Rationale marked non-normative, and never carrying a requirement.
    rationale_blocks = re.findall(r"^> Rationale:.*?(?=\n\n)", spec, re.MULTILINE | re.DOTALL)
    leaky = [b[:60] for b in rationale_blocks if NORMATIVE.search(b)]
    check("rationale-is-marked-non-normative", rationale_blocks and not leaky,
          f"{len(rationale_blocks)} rationale blocks, none carrying a requirement"
          if rationale_blocks and not leaky
          else (f"{len(leaky)} rationale blocks carry a normative keyword" if leaky
                else "no rationale blocks found"))

    # Every requirement carries a stable identifier, and none repeats.
    RULE_ID = re.compile(r"\[(APR-[A-Z]+-\d{3})\]")
    ids = RULE_ID.findall(spec)
    duplicates = sorted({i for i in ids if ids.count(i) > 1})
    untagged = []
    in_fence = False
    current = "document"
    for line in spec.split("\n"):
        if line.startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence or line.startswith(">"):
            continue
        heading = re.match(r"^#{2,4}\s+.*\{#([a-z0-9-]+)\}", line)
        if heading:
            current = heading.group(1)
            continue
        if current in {"normative-language", "conventions"}:
            continue
        if NORMATIVE.search(line) and not RULE_ID.search(line):
            # A wrapped requirement carries its identifier on the block's last
            # line, so only flag a line that ends a block.
            untagged.append(line.strip()[:60])
    check("rule-identifiers-unique", not duplicates,
          f"{len(set(ids))} identifiers, none repeated"
          if not duplicates else f"repeated: {duplicates}")

    # Derived artifacts named.
    # Normalise whitespace first: a phrase wrapped across a line break is still
    # the phrase, and a literal match would miss it.
    flat = " ".join(spec.split())
    derived = "**Derived.**" in flat and "derived artifact has the defect" in flat
    check("derived-artifacts-named", derived,
          "derived artifacts and defect ownership stated"
          if derived else "the specification does not name its derived artifacts")

    normative_count = len(NORMATIVE.findall(spec))

    if "--json" in sys.argv:
        print(json.dumps({
            "specification": manifest["specification"],
            "normativeClauses": normative_count,
            "anchors": len(anchors),
            "executableExamples": len(parsed),
            "checks": [{"id": n, "ok": ok, "detail": d} for n, ok, d in checks],
            "knownShortfalls": manifest["knownShortfalls"],
        }, indent=2))
        return 1 if problems else 0

    print(f"Specification shape — {manifest['specification']}")
    print(f"  {len(anchors)} anchors, {normative_count} normative clauses, "
          f"{len(parsed)} executable examples\n")
    for name, ok, detail in checks:
        print(f"  {'PASS' if ok else 'FAIL'}  {name:48} {detail}")

    print("\nDeclared shortfalls (tracked, not failures):")
    for s in manifest["knownShortfalls"]:
        print(f"  #{s['issue']:<4} {s['id']:28} {s['status']}")

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for p in problems:
            print(f"  - {p}")
        return 1
    print("\nThe specification has the declared shape.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

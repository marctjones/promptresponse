#!/usr/bin/env python3
"""Lint the whole specification against the writing approach, without the ledger.

tests/spec-conversion/approach.json configures the approach: the classes of product a
requirement may bind, the sections and unit kinds that are informative, and the
requirement level each BCP 14 keyword belongs to. Units come from spec-units.py.

  classes-defined-in-terminology   every class of product a requirement names is a
                                   term defined in Terminology
  no-lowercase-obligations         normative text says must, shall, should, required
                                   or may only as a BCP 14 keyword
  no-history                       nothing describes an earlier version, draft or a
                                   retired or legacy feature
  requirement-tables-carry-rules   a table with a Requirement column has a Rule
                                   column, and every row carries a rule identifier
  keywords-only-in-requirements    no rationale block and no heading carries a keyword
  one-level-per-rule               a unit carrying a rule states one requirement level
  examples-captioned               every executable example is captioned with its
                                   number, "**Example 4.5.1-3.**", and no caption
                                   names anything else

    python3 scripts/check-spec-approach.py            # report, never fails
    python3 scripts/check-spec-approach.py --chapter 1
    python3 scripts/check-spec-approach.py --gate     # fail on any finding
    python3 scripts/check-spec-approach.py --json
    python3 scripts/check-spec-approach.py --self-test
"""
from __future__ import annotations

import importlib.util
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
APPROACH = ROOT / "tests" / "spec-conversion" / "approach.json"
FIXTURE = ROOT / "tests" / "spec-conversion" / "approach-fixture.md"

_spec = importlib.util.spec_from_file_location("spec_units", ROOT / "scripts" / "spec-units.py")
spec_units = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(spec_units)
_spec = importlib.util.spec_from_file_location("extract_spec_examples", ROOT / "scripts" / "extract-spec-examples.py")
extractor = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(extractor)

CAPTION = re.compile(r"^\*\*Example ([^*]+?)\.\*\*", re.MULTILINE)
CHECKS = (
    "classes-defined-in-terminology",
    "no-lowercase-obligations",
    "no-history",
    "requirement-tables-carry-rules",
    "keywords-only-in-requirements",
    "one-level-per-rule",
    "examples-captioned",
)
RULE_ID = re.compile(r"\bAPR-[A-Z]+-\d{3}\b")
DEFINED_TERM = re.compile(r"^\*\*([^*]+)\*\*")
INLINE_CODE = re.compile(r"`[^`]*`")
STATEMENT_KINDS = {"sentence", "list-item", "table-row"}
# A member table's "Required" column states a requirement level as much as a
# "Requirement" column does, so both need a Rule column beside them.
REQUIREMENT_COLUMNS = {"requirement", "required"}


def columns(unit: dict) -> list[str]:
    return [c.strip().strip("*").lower() for c in unit["text"].strip().strip("|").split("|")]


def lint(units: list[dict], approach: dict) -> list[dict]:
    informative_sections = set(approach.get("informativeSections", []))
    informative_kinds = set(approach.get("informativeKinds", []))
    history_exempt = set(approach.get("historyExemptSections", []))
    levels = approach.get("levels", {})
    findings: list[dict] = []

    def finding(check: str, unit: dict, detail: str) -> None:
        findings.append({"check": check, "unit": unit["id"], "line": unit["line"], "chapter": unit["chapter"],
                         "detail": detail})

    def normative(u: dict) -> bool:
        return u["anchor"] not in informative_sections and u["kind"] not in informative_kinds

    defined = {m.group(1).strip().lower() for u in units if u["anchor"] == "terminology"
               for m in [DEFINED_TERM.match(u["text"])] if m}
    requirements = [u for u in units if u["keywords"] and u["kind"] in STATEMENT_KINDS and normative(u)]
    for cls in approach.get("classes", []):
        if cls in defined:
            continue
        pattern = re.compile(rf"\b{re.escape(cls)}s?\b", re.IGNORECASE)
        naming = [u for u in requirements if pattern.search(INLINE_CODE.sub("", u["text"]))]
        if naming:
            finding("classes-defined-in-terminology", naming[0],
                    f"'{cls}' is named by {len(naming)} requirement(s) and not defined in Terminology")

    header = None
    for u in units:
        if u["kind"] != "table-row":
            header = u if u["kind"] == "table-header" else None
        if normative(u) and u["kind"] != "heading" and u["lowercaseObligation"]:
            finding("no-lowercase-obligations", u, u["text"][:90])
        if u["historyVocabulary"] and u["anchor"] not in history_exempt:
            finding("no-history", u, u["text"][:90])
        if u["kind"] == "table-header" and REQUIREMENT_COLUMNS & set(columns(u)) and "rule" not in columns(u):
            finding("requirement-tables-carry-rules", u, "a Requirement column without a Rule column")
        if (u["kind"] == "table-row" and header and REQUIREMENT_COLUMNS & set(columns(header))
                and not RULE_ID.search(u["text"])):
            finding("requirement-tables-carry-rules", u, f"row carries no rule identifier: {u['text'][:70]}")
        if u["kind"] in {"rationale", "heading"} and u["keywords"]:
            finding("keywords-only-in-requirements", u, f"{u['kind']} carries {', '.join(u['keywords'])}")
        if u["kind"] in STATEMENT_KINDS and (u["rules"] or RULE_ID.search(u["text"])):
            stated = sorted({levels.get(k, k) for k in u["keywords"]})
            if len(stated) > 1:
                finding("one-level-per-rule", u, f"states {' and '.join(stated)}: {u['text'][:70]}")
    return findings


def counts(findings: list[dict]) -> dict[str, int]:
    return {c: sum(1 for f in findings if f["check"] == c) for c in CHECKS}


def load_approach() -> dict:
    return json.loads(APPROACH.read_text(encoding="utf-8"))


def caption_findings(markdown: str) -> list[dict]:
    """Each executable example is captioned with the number extraction gives it.

    The caption is the paragraph immediately before the fence. A caption anywhere
    else, or one naming a different number, is a finding too, so a renumbered
    example cannot leave its old caption behind.
    """
    examples, _ = extractor.extract(markdown)
    numbers = {e["id"]: e["number"] for e in examples}
    headings = [(m.start(), m.group(1)) for m in extractor.HEADING.finditer(markdown)]
    findings: list[dict] = []
    consumed: set[int] = set()

    def finding(offset: int, number: str, detail: str) -> None:
        # The chapter is where the text sits; a stale caption's number says nothing about that.
        section = next((n for start, n in reversed(headings) if start < offset), "")
        chapter = section.split(".")[0]
        findings.append({"check": "examples-captioned", "unit": f"offset/{offset}",
                         "line": markdown.count("\n", 0, offset) + 1,
                         "chapter": int(chapter) if chapter.isdigit() else None, "detail": detail})

    for match in extractor.FENCE.finditer(markdown):
        ident = re.search(r"^id:\s*(\S+)", match.group("header"), re.MULTILINE)
        number = numbers.get(ident.group(1), "") if ident else ""
        before = markdown[:match.start()].rstrip("\n")
        start = before.rfind("\n\n") + 2 if "\n\n" in before else 0
        caption = CAPTION.match(before, start)
        if caption:
            consumed.add(caption.start())
        if not caption or caption.group(1) != number:
            got = f"captioned {caption.group(1)}" if caption else "uncaptioned"
            finding(match.start(), number, f"example {ident.group(1) if ident else '?'} is {got}; "
                                           f"its caption is **Example {number}.**")
    for caption in CAPTION.finditer(markdown):
        if caption.start() not in consumed:
            finding(caption.start(), caption.group(1),
                    f"**Example {caption.group(1)}.** captions no executable example")
    return findings


def run(markdown: str | None = None) -> list[dict]:
    text = SPEC.read_text(encoding="utf-8") if markdown is None else markdown
    return lint(spec_units.segment(text), load_approach()) + caption_findings(text)


def self_test() -> int:
    fixture = FIXTURE.read_text(encoding="utf-8")
    approach = load_approach()
    problems: list[str] = []
    scenarios: list[str] = []

    def expect(name: str, markdown: str, wanted: str | None) -> None:
        scenarios.append(name)
        found = {f["check"] for f in lint(spec_units.segment(markdown), approach) + caption_findings(markdown)}
        if wanted is None and found:
            problems.append(f"{name}: expected no findings, got {sorted(found)}")
        elif wanted is not None and found != {wanted}:
            problems.append(f"{name}: expected only {wanted!r}, got {sorted(found) or 'none'}")

    def mutate(old: str, new: str) -> str:
        if old not in fixture:
            problems.append(f"fixture no longer contains {old!r}")
        return fixture.replace(old, new, 1)

    expect("the clean fixture", fixture, None)
    expect("a class of product with no definition",
           mutate("**writer** — software that produces a document.\n", ""),
           "classes-defined-in-terminology")
    expect("a lowercase obligation in normative text",
           mutate("A writer **SHOULD** emit", "A writer should emit"), "no-lowercase-obligations")
    expect("history vocabulary",
           mutate("## 3. Documents {#documents}\n",
                  "## 3. Documents {#documents}\n\nAn earlier draft allowed tabs.\n"), "no-history")
    expect("a Requirement column without a Rule column",
           mutate("| Member | Requirement | Rule |\n| --- | --- | --- |\n"
                  "| `title` | REQUIRED | APR-TEST-004 |\n| `note` | OPTIONAL | APR-TEST-005 |",
                  "| Member | Requirement |\n| --- | --- |\n| `title` | REQUIRED |\n| `note` | OPTIONAL |"),
           "requirement-tables-carry-rules")
    expect("a requirement row without a rule identifier",
           mutate("| OPTIONAL | APR-TEST-005 |", "| OPTIONAL | — |"), "requirement-tables-carry-rules")
    expect("a Required column without a Rule column",
           mutate("| Member | Requirement | Rule |\n| --- | --- | --- |",
                  "| Member | Required | Notes |\n| --- | --- | --- |"), "requirement-tables-carry-rules")
    expect("a keyword in a rationale block",
           mutate("a writer that must reorder", "a writer that MUST reorder"), "keywords-only-in-requirements")
    expect("a keyword in a heading",
           mutate("### 3.1 Members {#members}", "### 3.1 Members are REQUIRED {#members}"),
           "keywords-only-in-requirements")
    expect("one rule stating two levels",
           mutate("keep member order. [APR-TEST-001]",
                  "keep member order and **MAY** sort them. [APR-TEST-001]"), "one-level-per-rule")
    expect("a lowercase obligation in an informative section",
           mutate("such as must and should.", "such as must, should and may."), None)
    expect("an uncaptioned example",
           mutate("**Example 3.1-1.** Member order is kept.\n\n", ""), "examples-captioned")
    expect("a caption naming the wrong number",
           mutate("**Example 3.1-1.**", "**Example 3.1-2.**"), "examples-captioned")
    expect("a caption on no example",
           mutate("## 4. Normative references", "**Example 1.** An illustration.\n\n## 4. Normative references"),
           "examples-captioned")

    print(f"check-spec-approach self-test: {len(CHECKS)} checks, {len(scenarios)} scenarios")
    for p in problems:
        print(f"  FAIL  {p}")
    print("  all scenarios behave" if not problems else f"{len(problems)} PROBLEM(S)")
    return 1 if problems else 0


def main(argv: list[str]) -> int:
    if "--self-test" in argv:
        return self_test()
    findings = run()
    if "--chapter" in argv:
        chapter = int(argv[argv.index("--chapter") + 1])
        findings = [f for f in findings if f["chapter"] == chapter]
    if "--json" in argv:
        print(json.dumps({"counts": counts(findings), "findings": findings}, indent=2))
    else:
        print(f"Writing approach — {SPEC.relative_to(ROOT)}")
        for check, n in counts(findings).items():
            print(f"  {n:5}  {check}")
        for f in findings:
            print(f"  {f['check']:32} line {f['line']:<5} {f['detail']}")
    return 1 if "--gate" in argv and findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

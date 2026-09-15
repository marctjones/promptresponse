#!/usr/bin/env python3
"""Segment the APR specification into stable review units.

Every heading, prose sentence, list item, table row, rationale block, other
blockquote, and example or code block becomes one unit, with an identifier that
survives lines moving and changes when its text changes. The approach lint and the
example-coverage report read the specification through these units.

    python3 scripts/spec-units.py                    # summary of the specification
    python3 scripts/spec-units.py --self-test        # check segmentation on the fixture
    python3 scripts/spec-units.py --self-test --update   # rewrite the fixture's expected units

Flags on each unit (keywords, lowercase obligations, history vocabulary) are hints
for the approach lint, which decides what they mean.
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
FIXTURE = ROOT / "tests" / "spec-conversion" / "fixture.md"
FIXTURE_EXPECTED = ROOT / "tests" / "spec-conversion" / "fixture.expected.jsonl"

KEYWORDS = re.compile(r"\b(MUST NOT|MUST|SHALL NOT|SHALL|SHOULD NOT|SHOULD|NOT RECOMMENDED|"
                      r"RECOMMENDED|REQUIRED|MAY|OPTIONAL)\b")
LOWERCASE = re.compile(r"\b(must|shall|should|required|may)\b")
HISTORY = re.compile(r"\b(earlier (?:draft|version|scheme|baseline|design|ordering)|"
                     # No version literal here. Naming one made the specification unable
                     # to state its own version without the linter calling that history,
                     # and every bump then meant editing this pattern too.
                     r"previous (?:version|draft|specification)|formerly|retire[ds]?|legacy)\b",
                     re.IGNORECASE)
RULE = re.compile(r"\[(APR-[A-Z]+-\d{3})\]")
LEADING_RULES = re.compile(r"^((?:\[APR-[A-Z]+-\d{3}\]\s*)+)(.*)$", re.DOTALL)
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*(?:\{#([a-z0-9-]+)\})?\s*$")
CHAPTER = re.compile(r"^(\d+)\.")
LIST_ITEM = re.compile(r"^(?:[-*+]|\d+\.)\s+(?:\[[ xX]\]\s+)?")
SEPARATOR_ROW = re.compile(r"^\|(?:\s*:?-{3,}:?\s*\|)+\s*$")
RULE_LINE = re.compile(r"^\s*(?:---|\*\*\*)\s*$")
# Inline code and link text can contain ". X", which is not a sentence boundary.
PROTECT = re.compile(r"`[^`]*`|\[[^\]]*\]\([^)]*\)")
BOUNDARY = re.compile(r"([.!?][)\"'*_\]]*)\s+(?=[A-Z*_\"(\[`\x00])")
ABBREVIATIONS = ("e.g.", "i.e.", "etc.", "vs.", "cf.", "No.", "U.S.")


def split_sentences(text: str) -> list[str]:
    shelf: list[str] = []

    def hold(match: re.Match) -> str:
        shelf.append(match.group(0))
        return f"\x00{len(shelf) - 1}\x00"

    held = PROTECT.sub(hold, text)
    for abbreviation in ABBREVIATIONS:
        held = held.replace(abbreviation, abbreviation.replace(".", "\x01"))
    pieces, start = [], 0
    for match in BOUNDARY.finditer(held):
        pieces.append(held[start:match.end(1)])
        start = match.end()
    pieces.append(held[start:])

    def restore(piece: str) -> str:
        piece = re.sub(r"\x00(\d+)\x00", lambda m: shelf[int(m.group(1))], piece)
        return piece.replace("\x01", ".").strip()

    sentences: list[str] = []
    for piece in (restore(p) for p in pieces):
        if not piece:
            continue
        # A rule tag closes the sentence before it, even when the next sentence
        # starts on the same line.
        leading = LEADING_RULES.match(piece)
        if leading and sentences:
            sentences[-1] = f"{sentences[-1]} {leading.group(1).strip()}"
            piece = leading.group(2).strip()
            if not piece:
                continue
        sentences.append(piece)
    return sentences


def markdown_blocks(lines: list[str]):
    """(kind, first line number, lines) for fences, headings and blank-line blocks."""
    i, n = 0, len(lines)
    while i < n:
        line = lines[i]
        if line.startswith("```"):
            j = i + 1
            while j < n and not lines[j].startswith("```"):
                j += 1
            yield "fence", i + 1, lines[i:j + 1]
            i = j + 1
        elif not line.strip() or RULE_LINE.match(line):
            i += 1
        elif HEADING.match(line):
            yield "heading", i + 1, [line]
            i += 1
        else:
            j = i
            while j < n and lines[j].strip() and not lines[j].startswith("```") \
                    and not HEADING.match(lines[j]):
                j += 1
            yield "block", i + 1, lines[i:j]
            i = j


def line_groups(first_line: int, lines: list[str]):
    """Split a block where it changes between prose, list, table and quotation."""
    groups: list[tuple[str, int, list[str]]] = []
    previous = None
    for offset, line in enumerate(lines):
        if line.startswith("|"):
            kind = "table"
        elif line.startswith(">"):
            kind = "quote"
        elif LIST_ITEM.match(line) or (line[:1] in (" ", "\t") and previous == "list"):
            kind = "list"
        else:
            kind = "prose"
        if groups and groups[-1][0] == kind:
            groups[-1][2].append(line)
        else:
            groups.append((kind, first_line + offset, [line]))
        previous = kind
    return groups


def segment(markdown: str) -> list[dict]:
    units: list[dict] = []
    seen: dict[str, int] = {}
    anchor, chapter = "document", None

    def emit(kind: str, line: int, text: str) -> None:
        normalised = " ".join(text.split())
        digest = hashlib.sha256(normalised.encode("utf-8")).hexdigest()[:10]
        key = f"{anchor}/{kind}/{digest}"
        seen[key] = seen.get(key, 0) + 1
        prose = kind not in ("example", "code")
        units.append({
            "id": key if seen[key] == 1 else f"{key}-{seen[key]}",
            "anchor": anchor,
            "chapter": chapter,
            "kind": kind,
            "line": line,
            "text": text,
            "keywords": list(dict.fromkeys(KEYWORDS.findall(text))) if prose else [],
            "rules": list(dict.fromkeys(RULE.findall(text))),
            "lowercaseObligation": bool(prose and LOWERCASE.search(text) and not KEYWORDS.search(text)),
            "historyVocabulary": bool(prose and HISTORY.search(text)),
        })

    for kind, line, lines in markdown_blocks(markdown.split("\n")):
        if kind == "fence":
            emit("example" if lines[0].startswith("```apr-example") else "code", line,
                 "\n".join(lines))
            continue
        if kind == "heading":
            level, title, heading_anchor = HEADING.match(lines[0]).groups()
            if heading_anchor:
                anchor = heading_anchor
            if len(level) == 2:
                number = CHAPTER.match(title)
                chapter = int(number.group(1)) if number else None
            emit("heading", line, title)
            continue
        for group, group_line, group_lines in line_groups(line, lines):
            if group == "table":
                rows = [(group_line + k, row.strip()) for k, row in enumerate(group_lines)]
                for index, (row_line, row) in enumerate(rows):
                    if SEPARATOR_ROW.match(row):
                        continue
                    is_header = index + 1 < len(rows) and SEPARATOR_ROW.match(rows[index + 1][1])
                    emit("table-header" if is_header else "table-row", row_line, row)
            elif group == "quote":
                text = " ".join(l.lstrip(">").strip() for l in group_lines).strip()
                emit("rationale" if text.startswith("Rationale:") else "quote", group_line, text)
            elif group == "list":
                items: list[tuple[int, list[str]]] = []
                for k, l in enumerate(group_lines):
                    if LIST_ITEM.match(l):
                        items.append((group_line + k, [LIST_ITEM.sub("", l, count=1).strip()]))
                    elif items:
                        items[-1][1].append(l.strip())
                for item_line, parts in items:
                    emit("list-item", item_line, " ".join(parts))
            else:
                text = " ".join(l.strip() for l in group_lines)
                for sentence in split_sentences(text):
                    emit("sentence", group_line, sentence)
    return units


def render(units: list[dict]) -> str:
    return "".join(json.dumps(u, ensure_ascii=False) + "\n" for u in units)


def self_test(update: bool) -> int:
    fixture = FIXTURE.read_text(encoding="utf-8")
    units = segment(fixture)
    if update:
        FIXTURE_EXPECTED.write_text(render(units), encoding="utf-8")
        print(f"Wrote {FIXTURE_EXPECTED.relative_to(ROOT)}: {len(units)} units")
        return 0
    problems: list[str] = []
    if not FIXTURE_EXPECTED.exists() or FIXTURE_EXPECTED.read_text(encoding="utf-8") != render(units):
        problems.append("the fixture no longer segments to fixture.expected.jsonl")

    needed = {"heading", "sentence", "list-item", "table-header", "table-row",
              "rationale", "quote", "example", "code"}
    missing = needed - {u["kind"] for u in units}
    if missing:
        problems.append(f"the fixture produced no unit of kind {sorted(missing)}")

    texts = {u["text"] for u in units}
    for whole in ("Inline `a. B` code stays whole.",
                  "See [the link. Text](#first) for more.",
                  "Beta paragraph follows, e.g. with an abbreviation.",
                  "A writer must not do that, and one word changes here. [APR-TEST-001]"):
        if whole not in texts:
            problems.append(f"a sentence was split or merged wrongly: {whole!r}")
    if any(u["kind"] == "table-row" and "---" in u["text"] for u in units):
        problems.append("a table separator row became a unit")

    alpha = "Alpha paragraph opens the chapter. It has two sentences."
    beta = next(p for p in fixture.split("\n\n") if p.startswith("Beta paragraph"))
    moved = fixture.replace(f"{alpha}\n\n{beta}", f"{beta}\n\n{alpha}")
    if moved == fixture:
        problems.append("the fixture's movable paragraphs were not found")
    elif {u["id"] for u in segment(moved)} != {u["id"] for u in units}:
        problems.append("moving a paragraph changed unit identifiers")

    changed = fixture.replace("one word changes", "one term changes", 1)
    before, after = {u["id"] for u in units}, {u["id"] for u in segment(changed)}
    if len(before - after) != 1 or len(after - before) != 1:
        problems.append(f"changing one word changed {len(before - after)} identifiers, not 1")

    flags = {u["text"]: u for u in units}
    if not flags["An earlier draft said something else."]["historyVocabulary"]:
        problems.append("history vocabulary was not flagged")
    if not flags["A writer must not do that, and one word changes here. [APR-TEST-001]"]["lowercaseObligation"]:
        problems.append("a lowercase obligation was not flagged")
    if [u["id"] for u in units if u["text"] == "Repeated sentence."] != \
            [f"{u['anchor']}/sentence/{u['id'].split('/')[2].split('-')[0]}" + ("" if i == 0 else "-2")
             for i, u in enumerate(x for x in units if x["text"] == "Repeated sentence.")]:
        problems.append("identical sentences in one section did not get distinct identifiers")

    print(f"spec-units self-test: {len(units)} units from the fixture")
    for p in problems:
        print(f"  FAIL  {p}")
    print("  all checks pass" if not problems else f"{len(problems)} PROBLEM(S)")
    return 1 if problems else 0


def main(argv: list[str]) -> int:
    if "--self-test" in argv:
        return self_test("--update" in argv)
    units = segment(SPEC.read_text(encoding="utf-8"))
    kinds: dict[str, int] = {}
    for u in units:
        kinds[u["kind"]] = kinds.get(u["kind"], 0) + 1
    print(f"{SPEC.relative_to(ROOT)}: {len(units)} units")
    for kind, count in sorted(kinds.items(), key=lambda kv: -kv[1]):
        print(f"  {count:5}  {kind}")
    print(f"  {sum(bool(u['keywords']) for u in units):5}  units with a requirement keyword")
    print(f"  {sum(u['lowercaseObligation'] for u in units):5}  units with a lowercase obligation word")
    print(f"  {sum(u['historyVocabulary'] for u in units):5}  units with history vocabulary")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

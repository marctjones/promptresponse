#!/usr/bin/env python3
"""Extract the executable examples embedded in the APR specification.

The specification is normative and the conformance corpus is derived from it.
This script is what makes that true rather than merely asserted: the examples
live inside docs/APR_SPECIFICATION.md, and the machine-readable vectors are
generated from them, so the two cannot drift.

This is the pattern CommonMark uses. Its ~650 examples live in spec.txt and
spec_tests.py extracts them; there is no separately maintained corpus that could
disagree with the prose.

    python3 scripts/extract-spec-examples.py              # verify, exit non-zero on drift
    python3 scripts/extract-spec-examples.py --write      # regenerate the vectors
    python3 scripts/extract-spec-examples.py --dump       # print the vectors
    python3 scripts/extract-spec-examples.py --self-test  # every header key, and its errors

An example is a fenced block whose info string is "apr-example":

    ```apr-example
    id: jsonc-trailing-comma
    rule: apr-jsonc
    satisfies: APR-REP-005
    representation: jsonc
    expect: valid
    ---
    { "aprVersion": "1.0-beta.6", ... }
    ```

Header keys are:

    id              unique, stable, cited by tests and by the registry
    rule            the specification anchor the example demonstrates
    satisfies       comma separated: the rule identifiers the document conforms to
    violates        comma separated: the rule identifiers the document breaks; a
                    rejected example names at least one, and a valid one only
                    alongside the warnings the violation raises
    representation  jsonc | yaml | jsonc-stream | yaml-stream
    expect          valid | reject
    diagnostic      required when expect is reject: the reported code
    warns           comma separated: warning codes a reader reports for a valid example
    round-trip      true: a writer returns the same document
    preserves       comma separated JSON Pointers that survive the round trip
    evaluate        a JSON object: the evaluation inputs, such as _today or ctx
    expects         a JSON object: {group: {prompt id: value}} evaluation produces
    digest          the jcs-sha256 digest of a valid single-record example

Every example names at least one rule in satisfies or violates. Each example is
also numbered within its section, "5.8-1" for the first in section 5.8, for
readers; the id stays the stable handle.
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
VECTORS = ROOT / "tests" / "Conformance" / "beta6" / "spec-examples.json"

FENCE = re.compile(
    r"^```apr-example\n(?P<header>.*?)^---\n(?P<body>.*?)^```$",
    re.MULTILINE | re.DOTALL,
)
HEADING = re.compile(r"^#{2,6}\s+(\d+(?:\.\d+)*)\.?\s", re.MULTILINE)
WARNING_CODE = re.compile(r"^\|\s*`([A-Z][A-Z0-9_]*)`\s*\|", re.MULTILINE)
POINTER = re.compile(r"^(?:/(?:[^~/]|~[01])*)+$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")

REPRESENTATIONS = {"jsonc", "yaml", "jsonc-stream", "yaml-stream"}
OUTCOMES = {"valid", "reject"}
KEYS = {"id", "rule", "satisfies", "violates", "representation", "expect", "diagnostic",
        "warns", "round-trip", "preserves", "evaluate", "expects", "digest"}


def listed(value: str) -> list[str]:
    return [item.strip() for item in value.split(",") if item.strip()]


def warning_codes(text: str) -> set[str]:
    """The codes in the warnings table, which is the only place a warning is named."""
    _, _, tail = text.partition("{#warnings}")
    section = re.split(r"^#{1,6}\s", tail, maxsplit=1, flags=re.MULTILINE)[0]
    return set(WARNING_CODE.findall(section))


def extract(text: str) -> tuple[list[dict], list[str]]:
    known_anchors = set(re.findall(r"\{#([a-z0-9-]+)\}", text))
    # A rule is stated by prose, so the examples' own headers cannot be what states it.
    known_rules = set(re.findall(r"APR-[A-Z]+-\d{3}", FENCE.sub("", text)))
    known_warnings = warning_codes(text)
    headings = [(m.start(), m.group(1)) for m in HEADING.finditer(text)]
    numbering: dict[str, int] = {}
    examples: list[dict] = []
    problems: list[str] = []
    seen: set[str] = set()

    for match in FENCE.finditer(text):
        header: dict[str, str] = {}
        for line in match.group("header").splitlines():
            if not line.strip():
                continue
            if ":" not in line:
                problems.append(f"example header line is not 'key: value' — {line!r}")
                continue
            key, _, value = line.partition(":")
            header[key.strip()] = value.strip()

        ident = header.get("id", "")
        if not ident:
            problems.append("an example has no id")
            continue
        if ident in seen:
            problems.append(f"{ident}: duplicate example id")
            continue
        seen.add(ident)

        for key in sorted(set(header) - KEYS):
            problems.append(f"{ident}: header key {key!r} is not recognised")

        rule = header.get("rule", "")
        if rule not in known_anchors:
            problems.append(f"{ident}: rule #{rule} is not an anchor in the specification")

        # `rule` names the section an example demonstrates; `satisfies` and
        # `violates` name the identifiers it exercises and which way, so coverage
        # credits exactly what the example proves.
        satisfies, violates = listed(header.get("satisfies", "")), listed(header.get("violates", ""))
        if not satisfies and not violates:
            problems.append(f"{ident}: names no rule in satisfies or violates")
        for identifier in satisfies + violates:
            if identifier not in known_rules:
                problems.append(f"{ident}: names {identifier}, which the specification does not state")
        for identifier in sorted(set(satisfies) & set(violates)):
            problems.append(f"{ident}: both satisfies and violates {identifier}")

        representation = header.get("representation", "")
        if representation not in REPRESENTATIONS:
            problems.append(f"{ident}: representation {representation!r} is not recognised")

        expect = header.get("expect", "")
        if expect not in OUTCOMES:
            problems.append(f"{ident}: expect {expect!r} is not recognised")
        if expect == "reject" and not header.get("diagnostic"):
            problems.append(f"{ident}: expect is reject but no diagnostic is named")
        if expect == "reject" and not violates:
            problems.append(f"{ident}: expect is reject but violates names no rule")

        warns = listed(header.get("warns", ""))
        for code in warns:
            if code not in known_warnings:
                problems.append(f"{ident}: warns {code}, which the warnings table does not define")
        if warns and expect != "valid":
            problems.append(f"{ident}: warns is only meaningful when expect is valid")
        if violates and expect != "reject" and not warns:
            problems.append(f"{ident}: violates a rule but is neither rejected nor warned about")

        round_trip = header.get("round-trip")
        if round_trip is not None and round_trip not in {"true", "false"}:
            problems.append(f"{ident}: round-trip {round_trip!r} is not true or false")
        if round_trip == "true" and expect != "valid":
            problems.append(f"{ident}: round-trip is only meaningful when expect is valid")
        preserves = listed(header.get("preserves", ""))
        if preserves and round_trip != "true":
            problems.append(f"{ident}: preserves needs round-trip: true")
        for pointer in preserves:
            if not POINTER.match(pointer):
                problems.append(f"{ident}: preserves {pointer!r}, which is not a JSON Pointer")

        parsed: dict[str, object] = {}
        for key in ("evaluate", "expects"):
            if key in header:
                try:
                    parsed[key] = json.loads(header[key])
                except json.JSONDecodeError as exc:
                    problems.append(f"{ident}: {key} is not JSON: {exc.msg}")
                    continue
                if not isinstance(parsed[key], dict):
                    problems.append(f"{ident}: {key} is not a JSON object")
        if "evaluate" in header and "expects" not in header:
            problems.append(f"{ident}: evaluate states inputs but expects states no result")
        if isinstance(parsed.get("expects"), dict):
            if expect != "valid":
                problems.append(f"{ident}: expects is only meaningful when expect is valid")
            for group, values in parsed["expects"].items():
                if not isinstance(values, dict):
                    problems.append(f"{ident}: expects.{group} is not an object of prompt ids")

        digest = header.get("digest")
        if digest is not None:
            if not DIGEST.match(digest):
                problems.append(f"{ident}: digest {digest!r} is not sha256: and 64 hex digits")
            if expect != "valid" or representation.endswith("stream"):
                problems.append(f"{ident}: digest is only meaningful for a valid single-record example")

        body = match.group("body")
        if not body.strip():
            problems.append(f"{ident}: example body is empty")

        section = next((n for start, n in reversed(headings) if start < match.start()), "0")
        numbering[section] = numbering.get(section, 0) + 1

        example = {
            "id": ident,
            "number": f"{section}-{numbering[section]}",
            "rule": rule,
            "representation": representation,
            "expect": expect,
            "document": body,
            "rules": list(dict.fromkeys(satisfies + violates)),
        }
        if satisfies:
            example["satisfies"] = satisfies
        if violates:
            example["violates"] = violates
        if header.get("diagnostic"):
            example["diagnostic"] = header["diagnostic"]
        if warns:
            example["warns"] = warns
        if round_trip == "true":
            example["roundTrip"] = True
            if preserves:
                example["preserves"] = preserves
        for key in ("evaluate", "expects"):
            if isinstance(parsed.get(key), dict):
                example[key] = parsed[key]
        if digest:
            example["digest"] = digest
        examples.append(example)

    examples.sort(key=lambda e: e["id"])
    return examples, problems


def render(examples: list[dict]) -> str:
    return json.dumps(
        {
            "$comment": (
                "GENERATED from the examples embedded in docs/APR_SPECIFICATION.md. "
                "Do not edit. Run scripts/extract-spec-examples.py --write."
            ),
            "formatVersion": aprlib.format_version(),
            "examples": examples,
        },
        indent=2,
    ) + "\n"


SELF_TEST_SPEC = """\
## 2. Warnings {#warnings}

| Code | Condition |
| --- | --- |
| `TEST_WARNING` | Something advisory. |

## 3. Rules {#rules}

A reader **MUST** keep order. [APR-TEST-001]

A writer **SHOULD** keep comments. [APR-TEST-002]

```apr-example
id: every-key
rule: rules
satisfies: APR-TEST-001
violates: APR-TEST-002
representation: jsonc
expect: valid
warns: TEST_WARNING
round-trip: true
preserves: /metadata/title, /sections/0/prompts/0/x~1y
evaluate: {"_today": "1970-01-01", "ctx": {"org": "Example"}}
expects: {"responses": {"b": "99"}}
digest: sha256:%s
---
{"aprVersion": "1.0-beta.6"}
```

### 3.1 More {#more}

```apr-example
id: second-in-section
rule: warnings
violates: APR-TEST-001
representation: yaml
expect: reject
diagnostic: TEST_ERROR
---
aprVersion: "1.0-beta.6"
```
""" % ("0" * 64)


def self_test() -> int:
    problems: list[str] = []
    examples, found = extract(SELF_TEST_SPEC)
    if found:
        problems.append(f"the clean fixture reports {found}")
    by_id = {e["id"]: e for e in examples}
    wanted = {
        "id": "every-key", "number": "3-1", "rule": "rules", "representation": "jsonc",
        "expect": "valid", "document": '{"aprVersion": "1.0-beta.6"}\n',
        "rules": ["APR-TEST-001", "APR-TEST-002"], "satisfies": ["APR-TEST-001"],
        "violates": ["APR-TEST-002"], "warns": ["TEST_WARNING"], "roundTrip": True,
        "preserves": ["/metadata/title", "/sections/0/prompts/0/x~1y"],
        "evaluate": {"_today": "1970-01-01", "ctx": {"org": "Example"}},
        "expects": {"responses": {"b": "99"}}, "digest": "sha256:" + "0" * 64,
    }
    if by_id.get("every-key") != wanted:
        problems.append(f"every-key extracted as {by_id.get('every-key')}")
    if by_id.get("second-in-section", {}).get("number") != "3.1-1":
        problems.append(f"second-in-section numbered {by_id.get('second-in-section', {}).get('number')}")

    mutants = [
        ("an unrecognised key", "violates: APR-TEST-002\n", "rules: APR-TEST-002\n", "not recognised"),
        ("no rule named", "satisfies: APR-TEST-001\nviolates: APR-TEST-002\n", "", "names no rule"),
        ("an unstated rule", "satisfies: APR-TEST-001", "satisfies: APR-TEST-009", "does not state"),
        ("a rule both ways", "violates: APR-TEST-002", "violates: APR-TEST-001", "both satisfies and violates"),
        ("a rejection violating nothing", "violates: APR-TEST-001\nrepresentation: yaml",
         "satisfies: APR-TEST-001\nrepresentation: yaml", "violates names no rule"),
        ("a valid violation with no warning", "warns: TEST_WARNING\n", "", "neither rejected nor warned"),
        ("an undefined warning", "warns: TEST_WARNING", "warns: NO_SUCH_WARNING", "does not define"),
        ("a round trip that is not boolean", "round-trip: true", "round-trip: yes", "not true or false"),
        ("preserves without a round trip", "round-trip: true\n", "", "needs round-trip"),
        ("a malformed pointer", "/metadata/title,", "metadata/title,", "not a JSON Pointer"),
        ("evaluate without expects", 'expects: {"responses": {"b": "99"}}\n', "", "states no result"),
        ("expects that is not JSON", '{"responses": {"b": "99"}}', "{responses: b}", "not JSON"),
        ("an expects group that is not an object", '{"responses": {"b": "99"}}',
         '{"responses": "99"}', "not an object of prompt ids"),
        ("a malformed digest", "digest: sha256:" + "0" * 64, "digest: sha256:abc", "64 hex digits"),
        ("a digest on a stream", "representation: jsonc\n", "representation: jsonc-stream\n",
         "valid single-record example"),
    ]
    for name, old, new, message in mutants:
        if old not in SELF_TEST_SPEC:
            problems.append(f"{name}: the fixture no longer contains {old!r}")
            continue
        _, found = extract(SELF_TEST_SPEC.replace(old, new, 1))
        if not any(message in f for f in found):
            problems.append(f"{name}: expected a problem containing {message!r}, got {found or 'none'}")

    print(f"extract-spec-examples self-test: every header key, {len(mutants)} malformed headers")
    for p in problems:
        print(f"  FAIL  {p}")
    print("  all scenarios behave" if not problems else f"{len(problems)} PROBLEM(S)")
    return 1 if problems else 0


def main() -> int:
    if "--self-test" in sys.argv:
        return self_test()
    examples, problems = extract(SPEC.read_text(encoding="utf-8"))
    if problems:
        print("Specification examples are malformed:",
              *[f"- {p}" for p in problems], sep="\n")
        return 1

    generated = render(examples)

    if "--dump" in sys.argv:
        print(generated, end="")
        return 0

    if "--write" in sys.argv:
        VECTORS.parent.mkdir(parents=True, exist_ok=True)
        VECTORS.write_text(generated, encoding="utf-8")
        print(f"Wrote {len(examples)} examples to {VECTORS.relative_to(ROOT)}")
        return 0

    if not VECTORS.exists():
        print(f"{VECTORS.relative_to(ROOT)} does not exist; "
              "run scripts/extract-spec-examples.py --write")
        return 1

    if VECTORS.read_text(encoding="utf-8") != generated:
        print(f"{VECTORS.relative_to(ROOT)} does not match the specification.")
        print("The corpus is derived from the specification, so the specification is "
              "right and the vectors are stale.")
        print("Run scripts/extract-spec-examples.py --write")
        return 1

    print(f"Specification examples agree with the derived vectors: {len(examples)} examples.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

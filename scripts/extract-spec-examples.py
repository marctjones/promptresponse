#!/usr/bin/env python3
"""Extract the executable examples embedded in the APR specification.

The specification is normative and the conformance corpus is derived from it.
This script is what makes that true rather than merely asserted: the examples
live inside docs/APR_SPECIFICATION.md, and the machine-readable vectors are
generated from them, so the two cannot drift.

This is the pattern CommonMark uses. Its ~650 examples live in spec.txt and
spec_tests.py extracts them; there is no separately maintained corpus that could
disagree with the prose.

    python3 scripts/extract-spec-examples.py            # verify, exit non-zero on drift
    python3 scripts/extract-spec-examples.py --write    # regenerate the vectors
    python3 scripts/extract-spec-examples.py --dump     # print the vectors

An example is a fenced block whose info string is "apr-example":

    ```apr-example
    id: jsonc-trailing-comma
    rule: apr-jsonc
    representation: jsonc
    expect: valid
    ---
    { "version": "1.0-beta.6", ... }
    ```

Header keys are:

    id              unique, stable, cited by tests and by the registry
    rule            the specification anchor the example demonstrates
    representation  jsonc | yaml | jsonc-stream | yaml-stream
    expect          valid | reject | equivalent
    diagnostic      required when expect is reject: the reported code
    equivalent-to   required when expect is equivalent: another example id
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
VECTORS = ROOT / "tests" / "Conformance" / "beta6" / "spec-examples.json"

FENCE = re.compile(
    r"^```apr-example\n(?P<header>.*?)^---\n(?P<body>.*?)^```$",
    re.MULTILINE | re.DOTALL,
)

REPRESENTATIONS = {"jsonc", "yaml", "jsonc-stream", "yaml-stream"}
OUTCOMES = {"valid", "reject", "equivalent"}


def anchors() -> set[str]:
    return set(re.findall(r"\{#([a-z0-9-]+)\}", SPEC.read_text(encoding="utf-8")))


def extract() -> tuple[list[dict], list[str]]:
    text = SPEC.read_text(encoding="utf-8")
    known_anchors = anchors()
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

        rule = header.get("rule", "")
        if rule not in known_anchors:
            problems.append(f"{ident}: rule #{rule} is not an anchor in the specification")

        representation = header.get("representation", "")
        if representation not in REPRESENTATIONS:
            problems.append(f"{ident}: representation {representation!r} is not recognised")

        expect = header.get("expect", "")
        if expect not in OUTCOMES:
            problems.append(f"{ident}: expect {expect!r} is not recognised")
        if expect == "reject" and not header.get("diagnostic"):
            problems.append(f"{ident}: expect is reject but no diagnostic is named")
        if expect == "equivalent" and not header.get("equivalent-to"):
            problems.append(f"{ident}: expect is equivalent but no equivalent-to is named")

        body = match.group("body")
        if not body.strip():
            problems.append(f"{ident}: example body is empty")

        example = {
            "id": ident,
            "rule": rule,
            "representation": representation,
            "expect": expect,
            "document": body,
        }
        if header.get("diagnostic"):
            example["diagnostic"] = header["diagnostic"]
        if header.get("equivalent-to"):
            example["equivalentTo"] = header["equivalent-to"]
        examples.append(example)

    ids = {e["id"] for e in examples}
    for e in examples:
        target = e.get("equivalentTo")
        if target and target not in ids:
            problems.append(f"{e['id']}: equivalent-to names {target}, which is not an example")

    examples.sort(key=lambda e: e["id"])
    return examples, problems


def render(examples: list[dict]) -> str:
    return json.dumps(
        {
            "$comment": (
                "GENERATED from the examples embedded in docs/APR_SPECIFICATION.md. "
                "Do not edit. Run scripts/extract-spec-examples.py --write."
            ),
            "formatVersion": "1.0-beta.6",
            "examples": examples,
        },
        indent=2,
    ) + "\n"


def main() -> int:
    examples, problems = extract()
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

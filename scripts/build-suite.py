#!/usr/bin/env python3
"""Assemble one portable conformance suite from everything the corpus asserts.

An implementation is held to two collections today, and they are not the same
shape: the executable examples extracted from the specification, and the fixture
files under the corpus. Each has its own runner, so a new implementation has to
be taught both before it can be measured at all.

This merges them into `tests/Conformance/beta6/suite.json`: one self-contained
file listing every vector, its representation, the outcome the specification
requires, the diagnostic where one is named, and the document itself. An
implementation in any language reads one file and needs nothing else from this
repository.

    python3 scripts/build-suite.py            # verify the committed suite is current
    python3 scripts/build-suite.py --write    # regenerate it

The suite is derived. The specification is normative, the examples come from it,
and the corpus expectations are declared in corpus.map.json.
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import re
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"
CORPUS = ROOT / "tests" / "Conformance" / "beta6"
EXAMPLES = CORPUS / "spec-examples.json"
MAP = CORPUS / "corpus.map.json"
OUT = CORPUS / "suite.json"

SUITE_VERSION = "apr-beta6-conformance-1"
SKIP_NAMES = {"spec-examples.json", "suite.json", "corpus.map.json"}
SKIP_DIRS = {"keys", "digests"}


def representation_of(path: pathlib.Path, text: str) -> str:
    if path.suffix in {".yaml", ".yml"}:
        return "yaml-stream" if text.count("\n---") >= 1 else "yaml"
    return "jsonc-stream" if aprlib.RS in text else "jsonc"


def build() -> dict:
    mapping = json.loads(MAP.read_text(encoding="utf-8"))
    expectations = {k: v for k, v in mapping.get("expectations", {}).items()
                    if not k.startswith("$")}
    cases: list[dict] = []

    for example in json.loads(EXAMPLES.read_text(encoding="utf-8"))["examples"]:
        document = example["document"]
        if example["representation"] == "jsonc-stream":
            # The specification separates records in a prose example with a `---`
            # line, because a record separator is an invisible control character
            # and a document nobody can read is a poor example. The suite is read
            # by machines, so it carries the framing the format actually uses.
            document = "".join(
                aprlib.RS + part.strip("\n") + "\n"
                for part in re.split(r"(?m)^---$", document) if part.strip())
        case = {
            "id": f"spec:{example['id']}",
            "source": "specification",
            "rule": example["rule"],
            "anchors": [example["rule"]],
            "rules": example.get("rules", []),
            "representation": example["representation"],
            "expect": example["expect"],
            "document": document,
        }
        if example["expect"] == "valid" and not example["representation"].endswith("stream"):
            # Acceptance alone is a weak assertion: a reader that resolves `012` to
            # the number 12 accepts the document too. The digest pins the semantic
            # model the document must produce, which is what the rule is about.
            try:
                records = aprlib.read_records(
                    document,
                    "yaml" if example["representation"] == "yaml" else "jsonc")
                if len(records) == 1:
                    case["digest"] = aprlib.digest(records[0])
            except Exception:  # noqa: BLE001 - a vector we cannot read states no digest
                pass
        if example.get("diagnostic"):
            case["diagnostic"] = example["diagnostic"]
        if example.get("equivalentTo"):
            case["equivalentTo"] = f"spec:{example['equivalentTo']}"
        cases.append(case)

    for path in sorted(CORPUS.rglob("*")):
        if not path.is_file() or path.suffix not in {".jsonc", ".yaml", ".yml"}:
            continue
        relative = path.relative_to(CORPUS)
        if path.name in SKIP_NAMES or SKIP_DIRS & set(relative.parts):
            continue
        text = path.read_text(encoding="utf-8")
        expected = expectations.get(str(relative), {"expect": "valid"})
        case = {
            "id": f"corpus:{relative.as_posix()}",
            "source": "corpus",
            "rule": (expected.get("anchors") or [relative.parts[0]])[0],
            "anchors": expected.get("anchors") or [],
            "rules": expected.get("rules", []),
            "representation": representation_of(path, text),
            "expect": expected["expect"],
            "document": text,
        }
        if expected.get("diagnostic"):
            case["diagnostic"] = expected["diagnostic"]
        cases.append(case)

    return {
        "$comment": "GENERATED by scripts/build-suite.py from the specification's "
                    "executable examples and the conformance corpus. Do not edit. "
                    "An implementation reads this one file and needs nothing else.",
        "suiteVersion": SUITE_VERSION,
        "formatVersion": "1.0-beta.6",
        "specificationSha256": "sha256:" + hashlib.sha256(
            SPEC.read_bytes()).hexdigest(),
        "outcomes": {
            "valid": "The reader accepts the document and reports no error.",
            "reject": "The reader refuses the document. Where `diagnostic` is given, "
                      "that is the code it must report.",
            "equivalent": "The document has the same semantic model, and therefore the "
                          "same digest, as the case named in `equivalentTo`.",
        },
        "cases": cases,
    }


def main() -> int:
    suite = build()
    rendered = json.dumps(suite, indent=2, ensure_ascii=False) + "\n"
    counts: dict[str, int] = {}
    for case in suite["cases"]:
        counts[case["expect"]] = counts.get(case["expect"], 0) + 1
    summary = ", ".join(f"{v} {k}" for k, v in sorted(counts.items()))
    if "--write" in sys.argv:
        OUT.write_text(rendered, encoding="utf-8")
        print(f"Wrote {OUT.relative_to(ROOT)}: {len(suite['cases'])} cases ({summary})")
        return 0
    if not OUT.exists() or OUT.read_text(encoding="utf-8") != rendered:
        print(f"{OUT.relative_to(ROOT)} is stale; run scripts/build-suite.py --write")
        return 1
    print(f"Conformance suite is current: {len(suite['cases'])} cases ({summary})")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Validate the APR conformance corpus against schemas/apr-1.0.schema.json.

This is the language-neutral half of the conformance gate: the .NET runner in
tests/PromptResponse.Core.Tests/Conformance/ConformanceCorpusTests.cs proves the
reference implementation behaves correctly, while this script proves the published
schema agrees with it. Any SDK in any language can be held to the same two checks.

    pip install jsonschema && python3 scripts/check-schema.py

Exits non-zero if any fixture disagrees with the schema.
"""
import json
import pathlib
import re
import sys

try:
    from jsonschema import Draft202012Validator
except ImportError:
    sys.exit("jsonschema is required: pip install jsonschema")

ROOT = pathlib.Path(__file__).resolve().parent.parent
SCHEMA = ROOT / "schemas" / "apr-1.0.schema.json"
CORPUS = ROOT / "tests" / "Conformance" / "v1"

# Rules the schema provably cannot express (document-wide uniqueness), so these
# fixtures are expected to pass the schema and be caught by a validator instead.
VALIDATOR_ONLY = {"duplicate-prompt-id.aprt", "duplicate-section-id.aprt"}


def load(path):
    """Return (document, parse_error)."""
    try:
        return json.loads(path.read_text(encoding="utf-8")), None
    except json.JSONDecodeError as exc:
        return None, str(exc)


def check_type_registry():
    """The type registry, the schema, and the specification must name the same types.

    The vocabulary of a format is the classic thing to state in several places and then
    let drift. Here it is stated once in schemas/apr-types-1.0.json; this confirms the
    other two copies still agree with it.
    """
    registry_path = ROOT / "schemas" / "apr-types-1.0.json"
    if not registry_path.is_file():
        return

    registry = json.loads(registry_path.read_text(encoding="utf-8"))
    registered = [t["id"] for t in registry["expectedDataType"]["types"]]

    schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    in_schema = schema["$defs"]["promptHints"]["properties"]["expectedDataType"].get("examples", [])

    spec = (ROOT / "docs" / "APR_SPECIFICATION.md").read_text(encoding="utf-8")
    block = re.search(r"`expectedDataType` registry:(.+?)\n\n", spec, re.S)
    in_spec = re.findall(r"`([a-z]+)`", block.group(1)) if block else []

    print("type registry — schema and specification must name the same types")
    problems = []
    for label, other in (("schema examples", in_schema), ("specification 4.7", in_spec)):
        missing = sorted(set(registered) - set(other))
        extra = sorted(set(other) - set(registered))
        if missing or extra:
            problems.append(f"{label}: missing {missing}, unexpected {extra}")
        print(f"  {'PASS' if not (missing or extra) else 'FAIL'}  {label} ({len(other)} types)")
    if problems:
        for p in problems:
            print(f"    - {p}")
        raise SystemExit(1)


def check_real_files(validator, failures):
    """Hold the shipped examples and fixtures to the published schema.

    The corpus is synthetic on purpose - one rule per fixture - but the files people
    actually open are the examples, and a third-party reader built from the schema will
    meet those first. An example that the schema rejects is worse than no example.

    This is how examples/field-types-showcase.aprt was found shipping with two dynamic
    tables whose rows shared an id.
    """
    roots = [ROOT / "examples", ROOT / "tests" / "Fixtures"]
    files = sorted(
        (f for root in roots if root.is_dir()
           for f in root.rglob("*.apr*")
           if "bin" not in f.parts and "obj" not in f.parts),
        key=lambda f: str(f),
    )

    if not files:
        failures.append("no real form files found under examples/ or tests/Fixtures/")
        return

    print("\nreal files — schema MUST accept every shipped example and fixture")
    for path in files:
        document, parse_error = load(path)
        rel = path.relative_to(ROOT)
        if parse_error:
            print(f"  FAIL  {rel}  (not valid JSON)")
            failures.append(f"{rel}: {parse_error}")
            continue

        errors = sorted(validator.iter_errors(document), key=lambda e: e.path)
        print(f"  {'PASS' if not errors else 'FAIL'}  {rel}")
        if errors:
            failures.append(f"{rel}: {errors[0].message}")


def main():
    validator = Draft202012Validator(json.loads(SCHEMA.read_text(encoding="utf-8")))
    Draft202012Validator.check_schema(json.loads(SCHEMA.read_text(encoding="utf-8")))
    failures = []

    def check(folder, expect_schema_valid, label):
        directory = CORPUS / folder
        if not directory.is_dir():
            return
        print(f"\n{label}")
        for path in sorted(directory.glob("*.apr*")):
            document, parse_error = load(path)
            if parse_error is not None:
                # Only the malformed corpus may fail to parse as JSON at all.
                ok = folder == "malformed"
                print(f"  {'PASS' if ok else 'FAIL'}  {path.name}  (not valid JSON)")
                if not ok:
                    failures.append(f"{path.name}: unparseable JSON")
                continue

            errors = sorted(validator.iter_errors(document), key=lambda e: list(e.path))
            schema_valid = not errors

            if path.name in VALIDATOR_ONLY:
                ok = schema_valid  # expected to slip past the schema
                note = "  (validator-only rule: document-wide id uniqueness)"
            else:
                ok = schema_valid is expect_schema_valid
                note = ""

            print(f"  {'PASS' if ok else 'FAIL'}  {path.name}{note}")
            if not ok:
                detail = errors[0].message if errors else "unexpectedly passed the schema"
                failures.append(f"{path.name}: {detail}")

    check_type_registry()
    check("valid", True, "valid/ — schema MUST accept every file")
    check("canonicalization", True, "canonicalization/ — signing vector input")
    check("signatures", True, "signatures/ — structurally valid; only verification fails")
    check("invalid", False, "invalid/ — schema catches all but the validator-only rules")
    check("malformed", False, "malformed/ — rejected at the JSON or type layer")
    check_real_files(validator, failures)

    if failures:
        print(f"\n{len(failures)} failure(s):")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("\nAll corpus fixtures and shipped files agree with the schema.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

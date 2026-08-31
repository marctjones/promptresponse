#!/usr/bin/env python3
"""Validate the APR beta.6 conformance corpus and shipped examples.

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
from urllib.parse import urljoin

try:
    from jsonschema import Draft202012Validator
except ImportError:
    sys.exit("jsonschema is required: pip install jsonschema")

ROOT = pathlib.Path(__file__).resolve().parent.parent
BETA6_SCHEMA = ROOT / "schemas" / "apr-1.0-beta.6.schema.json"
BETA6_CORPUS = ROOT / "tests" / "Conformance" / "beta6"


def load(path):
    """Return one JSON document and a parse-error message, if any."""
    try:
        return json.loads(path.read_text(encoding="utf-8")), None
    except json.JSONDecodeError as exc:
        return None, str(exc)


def strip_jsonc(text):
    """Small string-aware JSONC normalizer for schema-gate fixtures."""
    output, quoted, escaped, index = [], False, False, 0
    while index < len(text):
        char = text[index]
        if quoted:
            output.append(char)
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                quoted = False
            index += 1
            continue
        if char == '"': quoted = True; output.append(char)
        elif char == "/" and index + 1 < len(text) and text[index + 1] == "/":
            index = text.find("\n", index)
            if index < 0: break
            output.append("\n")
        elif char == "/" and index + 1 < len(text) and text[index + 1] == "*":
            index = text.find("*/", index + 2)
            if index < 0: raise ValueError("unterminated JSONC block comment")
            index += 1
        else: output.append(char)
        index += 1
    return re.sub(r",(\s*[}\]])", r"\1", "".join(output))


def beta6_records(path):
    text = path.read_text(encoding="utf-8")
    if path.suffix in {".yaml", ".yml"}:
        try:
            import yaml
        except ImportError as exc:
            raise RuntimeError("PyYAML is required for beta.6 YAML schema fixtures") from exc
        if re.search(r"(?m)(?:^|[\s\[{,])(?:[&*!]|<<\s*:)", text):
            raise ValueError("APR YAML forbids anchors, aliases, tags, and merge keys")
        return list(yaml.safe_load_all(text))
    parts = [part for part in text.split("\x1e") if part.strip()] if "\x1e" in text else [text]
    def unique_object(pairs):
        value = {}
        for key, item in pairs:
            if key in value:
                raise ValueError(f"duplicate JSONC member {key!r}")
            value[key] = item
        return value
    return [json.loads(strip_jsonc(part), object_pairs_hook=unique_object) for part in parts]


def check_real_files(validator, failures):
    """Hold the shipped examples and fixtures to the published schema.

    The corpus is synthetic on purpose - one rule per fixture - but the files people
    actually open are the examples, and a third-party reader built from the schema will
    meet those first. An example that the schema rejects is worse than no example.

    This is how examples/field-types-showcase.aprt was found shipping with two dynamic
    tables whose rows shared an id.
    """
    roots = [ROOT / "examples"]
    files = sorted(
        (f for root in roots if root.is_dir()
           for f in root.rglob("*.apr*")
           if "bin" not in f.parts and "obj" not in f.parts),
        key=lambda f: str(f),
    )

    if not files:
        failures.append("no real form files found under examples/")
        return

    print("\nreal files — schema MUST accept every shipped example")
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


def check_beta6_examples(validator, failures):
    """Shipped examples are release-surface beta.6 data, not historical corpus."""
    examples = sorted((ROOT / "examples").glob("*.apr*"))
    print()
    print("beta6 shipped examples — every user-facing example MUST be beta.6")
    for path in examples:
        rel = path.relative_to(ROOT)
        try:
            records = beta6_records(path)
        except (ValueError, json.JSONDecodeError) as exc:
            print("  FAIL  %s  (not valid beta.6 representation)" % rel)
            failures.append("%s: %s" % (rel, exc))
            continue
        errors = [error for record in records for error in validator.iter_errors(record)]
        ok = len(records) == 1 and not errors
        print("  %s  %s" % ("PASS" if ok else "FAIL", rel))
        if not ok:
            detail = errors[0].message if errors else "expected one beta.6 record, found %d" % len(records)
            failures.append("%s: %s" % (rel, detail))


def main():
    beta_schema = json.loads(BETA6_SCHEMA.read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(beta_schema)
    # The beta schema deliberately reuses stable core definitions by relative
    # reference. Give jsonschema the on-disk schema URI so that reference is
    # resolved exactly as it is for external consumers.
    from jsonschema import RefResolver
    validator = Draft202012Validator(beta_schema, resolver=RefResolver(BETA6_SCHEMA.as_uri(), beta_schema))
    failures = []
    check_real_files(validator, failures)
    check_beta6_examples(validator, failures)
    print("\nbeta6/ — schema MUST accept paired forms and independent attestation records")
    for path in sorted(p for p in BETA6_CORPUS.rglob("*") if p.is_file() and p.suffix in {".jsonc", ".yaml", ".yml"}):
        try:
            records = beta6_records(path)
            errors = [error for record in records for error in validator.iter_errors(record)]
            ok = not errors and bool(records)
        except Exception as exc:
            errors, ok = [exc], False
        expected_valid = "malformed" not in path.parts
        passed = ok is expected_valid
        print(f"  {'PASS' if passed else 'FAIL'}  {path.relative_to(ROOT)}")
        if not passed:
            detail = str(errors[0]) if errors else "unexpectedly passed schema"
            failures.append(f"{path.relative_to(ROOT)}: {detail}")

    if failures:
        print(f"\n{len(failures)} failure(s):")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("\nAll corpus fixtures and shipped files agree with the schema.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

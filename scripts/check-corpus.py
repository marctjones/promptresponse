#!/usr/bin/env python3
"""Check that every digest in the conformance corpus is the digest it claims.

The corpus asserts integrity, and until now nothing checked its own. A fixture
carries a subject digest, a manifest root, per-path entry digests and witness
digests, all of them values computed over other records. Any of them can go
stale silently: edit a form and the attestations that cover it become wrong,
and every gate still passes, because the schema checks shape and the SDK suites
check the SDKs against the same stale bytes.

This recomputes all of it from the records themselves, using scripts/aprlib.py
rather than any SDK, so a consistently wrong implementation cannot make the
corpus look right.

    python3 scripts/check-corpus.py            # verify
    python3 scripts/check-corpus.py --verbose  # and show every check

What it checks, per the specification:

* a form written in both representations has one semantic model, so one digest
  (#stream-equivalence)
* an attestation's `subject.digest` names a form present in the corpus
  (#attestation-catalogue)
* `manifest.root` is the digest of that subject (#digests)
* every manifest entry digest is the digest of the JCS encoding of the value at
  its JSON Pointer, entries are ordered by path, no path repeats, and the root
  pointer is present (#digests)
* every witness names the envelope digest of an attestation in the corpus, and
  witnesses are duplicate-free (#witnesses)
* the published digest vectors under digests/ agree with the forms they cite

Fixtures under malformed/ are excluded: they exist to be rejected.
"""
from __future__ import annotations

import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
CORPUS = ROOT / "tests" / "Conformance" / "beta6"
SKIP_DIRS = {"malformed"}
SKIP_NAMES = {"spec-examples.json"}


def corpus_files() -> list[pathlib.Path]:
    return sorted(
        path for path in CORPUS.rglob("*")
        if path.is_file()
        and path.suffix in {".jsonc", ".yaml", ".yml"}
        and not SKIP_DIRS & set(path.relative_to(CORPUS).parts)
        and path.name not in SKIP_NAMES
    )


def main() -> int:
    verbose = "--verbose" in sys.argv
    problems: list[str] = []
    checks = 0

    forms: dict[str, tuple[str, dict]] = {}       # digest -> (where, form)
    envelopes: dict[str, str] = {}                # envelope digest -> where
    attestations: list[tuple[str, dict]] = []     # (where, attestation)

    # Pass one: read every record and index it by what it digests to.
    for path in corpus_files():
        where = str(path.relative_to(ROOT))
        try:
            records = aprlib.read_file(path)
        except aprlib.AprError as exc:
            problems.append(f"{where}: will not parse: {exc}")
            continue
        for index, record in enumerate(records):
            if not isinstance(record, dict):
                problems.append(f"{where}[{index}]: record is not an object")
                continue
            if aprlib.is_attestation(record):
                envelopes[aprlib.envelope_digest(record)] = f"{where}[{index}]"
                attestations.append((f"{where}[{index}]", record))
            else:
                forms.setdefault(aprlib.digest(record), (f"{where}[{index}]", record))

    # Paired representations must produce one semantic model, so one digest.
    for path in corpus_files():
        if path.suffix != ".jsonc":
            continue
        pair = path.with_suffix(".yaml")
        if not pair.exists():
            continue
        checks += 1
        try:
            left = [aprlib.digest(r) for r in aprlib.read_file(path)]
            right = [aprlib.digest(r) for r in aprlib.read_file(pair)]
        except aprlib.AprError as exc:
            problems.append(f"{path.name}: paired file will not parse: {exc}")
            continue
        if left != right:
            problems.append(
                f"{path.relative_to(ROOT)} and its .yaml pair differ semantically; "
                f"paired documents must have identical digests")
        elif verbose:
            print(f"  pair    {path.stem}: both representations digest alike")

    # Pass two: every claimed digest must be the digest it claims.
    for where, attestation in attestations:
        subject = (attestation.get("subject") or {}).get("digest")
        checks += 1
        if subject not in forms:
            problems.append(
                f"{where}: subject {subject} names no form in the corpus, so nothing "
                f"proves the manifest was ever computed over anything")
            continue
        subject_where, form = forms[subject]
        if verbose:
            print(f"  subject {where} -> {subject_where}")

        manifest = attestation.get("manifest") or {}
        checks += 1
        if manifest.get("root") != subject:
            problems.append(
                f"{where}: manifest.root {manifest.get('root')} is not the subject "
                f"digest {subject}")

        entries = manifest.get("entries") or []
        paths = [entry.get("path") for entry in entries]
        checks += 1
        if paths != sorted(paths):
            problems.append(f"{where}: manifest entries are not ordered by path")
        checks += 1
        if len(set(paths)) != len(paths):
            problems.append(f"{where}: manifest repeats a path")
        checks += 1
        if "" not in paths and paths:
            problems.append(f"{where}: manifest carries entries but not the root pointer")
        for entry in entries:
            checks += 1
            value = aprlib.resolve_pointer(form, entry.get("path", ""))
            if value is aprlib.MISSING:
                problems.append(
                    f"{where}: manifest entry {entry.get('path')!r} resolves to nothing "
                    f"in {subject_where}")
                continue
            actual = aprlib.digest(value)
            if actual != entry.get("digest"):
                problems.append(
                    f"{where}: manifest entry {entry.get('path')!r} claims "
                    f"{entry.get('digest')} but the value digests to {actual}")

        witnesses = attestation.get("witnesses") or []
        checks += 1
        if len(set(witnesses)) != len(witnesses):
            problems.append(f"{where}: witnesses repeat; the list must be duplicate-free")
        for witness in witnesses:
            checks += 1
            if witness not in envelopes:
                problems.append(
                    f"{where}: witness {witness} names no attestation envelope in the corpus")
            elif verbose:
                print(f"  witness {where} -> {envelopes[witness]}")

    # The published digest vectors are a third statement of the same facts.
    for path in sorted((CORPUS / "digests").glob("*.json")):
        where = str(path.relative_to(ROOT))
        vector = json.loads(path.read_text(encoding="utf-8"))
        stated = vector.get("documentDigest")
        checks += 1
        if stated not in forms:
            problems.append(f"{where}: documentDigest {stated} names no form in the corpus")
            continue
        _, form = forms[stated]
        checks += 1
        if vector.get("canonicalJson") != aprlib.canonicalize(form):
            problems.append(f"{where}: canonicalJson is not the JCS encoding of that form")
        for entry in vector.get("entries") or []:
            checks += 1
            value = aprlib.resolve_pointer(form, entry.get("path", ""))
            if value is aprlib.MISSING or aprlib.digest(value) != entry.get("digest"):
                problems.append(f"{where}: entry {entry.get('path')!r} does not match the form")
        if verbose:
            print(f"  vector  {where}: agrees with the form it cites")

    print(f"\nforms: {len(forms)}  attestations: {len(attestations)}  checks: {checks}")
    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nA digest is derived from a record. Regenerate with "
              "scripts/build-corpus.py --write rather than editing one by hand.")
        return 1
    print("Every digest in the corpus is the digest it claims.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""Fail CI when the small set of release-status documents contradict the tree."""

import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
registry = json.loads((ROOT / "tests/registry.json").read_text(encoding="utf-8"))
spec = (ROOT / "docs/APR_SPECIFICATION.md").read_text(encoding="utf-8")
readme = (ROOT / "README.md").read_text(encoding="utf-8")
sdk = (ROOT / "docs/SDK_CONFORMANCE.md").read_text(encoding="utf-8")
signing = (ROOT / "docs/SIGNING.md").read_text(encoding="utf-8")
canonical = (ROOT / "tests/Conformance/v1/canonicalization/README.md").read_text(encoding="utf-8")

problems = []
match = re.search(r"Specification document version:\*\* ([^\s]+)", spec)
if not match or match.group(1) != registry["specVersion"]:
    problems.append("APR specification document version must equal tests/registry.json specVersion")

for name in (".NET", "Python", "TypeScript"):
    if name not in readme or name not in sdk:
        problems.append(f"README and SDK conformance status must name {name}")

for path, text in {
    "docs/SIGNING.md": signing,
    "docs/SDK_CONFORMANCE.md": sdk,
    "canonicalization README": canonical,
}.items():
    if "apr-sig-v3" not in text:
        problems.append(f"{path} must identify the current apr-sig-v3 payload")
    if "apr-sig-v2" in text:
        problems.append(f"{path} still calls apr-sig-v2 current")

if "issue #88" not in spec or "Beta boundary" not in signing:
    problems.append("signature documents must disclose the apr-sig-v3 beta boundary and v4 follow-up")

if problems:
    print("Documentation consistency check failed:", *[f"- {p}" for p in problems], sep="\n")
    sys.exit(1)
print("Documentation consistency check passed.")

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
roadmap = (ROOT / "ROADMAP.md").read_text(encoding="utf-8")
sdk = (ROOT / "docs/SDK_CONFORMANCE.md").read_text(encoding="utf-8")
signing = (ROOT / "docs/SIGNING.md").read_text(encoding="utf-8")
problems = []
migrating_beta6 = "**Specification document version:** 1.0.0-beta.6-draft" in spec

for path in (
    "docs/README.md", "docs/PRODUCT.md", "docs/ARCHITECTURE.md",
    "docs/UX_ACCESSIBILITY.md", "docs/CONCEPT_REGISTRY.md",
    "docs/IMPLEMENTATION_REGISTRY.md", "docs/USER_GUIDE.md",
    "docs/DEVELOPMENT.md", "docs/IMPORT.md", "docs/SIGNING.md",
):
    if not (ROOT / path).is_file():
        problems.append(f"missing canonical documentation: {path}")

for path in (
    "VISION.md", "ACCESSIBILITY.md", "LAUNCHER.md", "CLEANUP.md",
    "docs/FILE_FORMAT.md", "docs/APR_SPECIFICATION_v0.2.md",
    "docs/FEATURES.md", "docs/IMPLEMENTATION_PLAN.md",
):
    if (ROOT / path).exists():
        problems.append(f"superseded documentation must not return: {path}")

match = re.search(r"Specification document version:\*\* ([^\s]+)", spec)
if not match:
    problems.append("APR specification document version is missing")
elif not migrating_beta6 and match.group(1) != registry["specVersion"]:
    problems.append("APR specification document version must equal tests/registry.json specVersion")
elif migrating_beta6:
    for path in (
        "schemas/apr-1.0-beta.6.schema.json",
        "tests/Conformance/beta6/README.md",
        "tests/Conformance/beta6/digests/permit.json",
    ):
        if not (ROOT / path).is_file():
            problems.append(f"beta.6 migration is missing its contract artifact: {path}")

for name in (".NET", "Python", "TypeScript"):
    if name not in readme or name not in sdk:
        problems.append(f"README and SDK conformance status must name {name}")

if "Planning authority:" not in roadmap or "GitHub milestones and issues" not in roadmap:
    problems.append("ROADMAP.md must identify GitHub issues and milestones as planning authority")
for retired_surface in ("Rust and Java (real)", "C++ skeleton", "Rust/Java/Python/C++ runners"):
    if retired_surface in roadmap:
        problems.append(f"ROADMAP.md retains retired SDK status claim: {retired_surface}")

if "WCAG 2.1 Level AA compliance built-in" in readme:
    problems.append("README overstates accessibility evidence; link to UX_ACCESSIBILITY.md instead")

if "Java / Rust / C++ | Not implemented" in sdk:
    problems.append("SDK conformance status contradicts the shipped Java SDK")

# A specification that cites a standard it does not list, or lists one it never
# cites, is drifting from its own bibliography. Restricted to designations with an
# unambiguous shape, so ordinary prose cannot trip it.
DESIGNATION = re.compile(r"\b(?:RFC \d{3,5}|BCP \d{1,3}|FIPS \d{3}-\d)\b")
if "{#normative-references}" not in spec:
    problems.append("APR specification has no normative references section")
else:
    head, _, tail = spec.partition("{#normative-references}")
    listed = set(DESIGNATION.findall(tail))
    cited = set(DESIGNATION.findall(head))
    for designation in sorted(cited - listed):
        problems.append(f"specification cites {designation} but does not list it in the references")
    for designation in sorted(listed - cited):
        problems.append(f"specification lists {designation} in the references but never cites it")

if problems:
    print("Documentation consistency check failed:", *[f"- {p}" for p in problems], sep="\n")
    sys.exit(1)
print("Documentation consistency check passed.")

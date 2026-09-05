#!/usr/bin/env python3
"""Score any implementation against the APR conformance suite.

An implementation is validated by answering questions, not by being written in a
particular language or wired into this repository's test projects. It provides a
**driver**: a program that reads the suite on standard input and writes what it
did on standard output. Everything else is this script's job.

    python3 scripts/run-conformance.py --driver "python3 scripts/reference-driver.py"
    python3 scripts/run-conformance.py --driver "./my-sdk-driver" --json
    python3 scripts/run-conformance.py --driver "..." --oscal results.json

The contract, in full:

**Input** on stdin is `tests/Conformance/beta6/suite.json` verbatim. Each case
carries `id`, `representation` (`jsonc`, `yaml`, `jsonc-stream`, `yaml-stream`)
and `document`, the source text.

**Output** on stdout is one JSON object::

    {
      "implementation": {"name": "...", "version": "...", "profiles": ["core"]},
      "results": [
        {"id": "spec:table-section", "outcome": "valid"},
        {"id": "spec:yaml-tag", "outcome": "reject", "diagnostic": "YAML_TAG_FORBIDDEN"},
        {"id": "spec:ragged", "outcome": "valid", "warnings": ["TABLE_RAGGED"]}
      ]
    }

`outcome` is `valid` when the implementation read the document and found no
error, and `reject` when it refused it. Report `reject` for a document you
cannot parse as well as for one you parse and find invalid; the specification
distinguishes those two, but a case that expects rejection accepts either.

`diagnostic` is the code you reported. Give it whenever you have one. Where the
suite names a diagnostic, a different one is recorded as a discrepancy rather
than a failure, because a case can be refused for the right reason under a
different name.

`warnings` is the list of advisory codes you reported while accepting the
document. Where a case names `warns`, every code it names must appear or the case
fails, because an advisory rule is only tested if the advisory can be required.
Reporting more than the suite names is a discrepancy, not a failure: an
implementation may legitimately warn about more.

`evaluated` is what evaluating the expression hints produced, required by any case
carrying `expects`. Report it as
`{"responses": {...}, "hidden": {...}, "expected": {...}, "readOnly": {...},
"validation": {...}}`, keyed by prompt id. The case supplies `_now`, `_today` and
`ctx` under `evaluate`; take them from there and never from the host clock, which
is what makes the same form evaluate the same way twice. Only what a case names is
checked.

`written` is the document as you would serialize it after reading, required by any
case marked `roundTrip`. The harness reads it back and checks it is the same
document, and that every pointer in `preserves` survived. This is the only part of
the contract that tests writing, and preservation is what makes additive change
safe: a reader that quietly drops an unknown member accepts every document it is
ever given.

`digest` is the `jcs-sha256` semantic digest, and reporting it is how a case
proves more than acceptance. Most valid cases state the digest the document must
produce; if you report one and it differs, the case fails even though you
accepted the document. That is deliberate, and it is where a reader whose scalar
resolution is wrong gets caught: it reads `012` happily, as the number twelve,
and produces a different form. Omitting the digest is allowed and skips that
check, which makes your score weaker rather than better. Cases expecting
`equivalent` are scored on it alone.

Every case carries the `profile` it belongs to. A profile is optional to claim and
**binding once claimed**: a case belonging to a profile you declare must be
answered, and omitting it fails. A case outside every profile you declare may be
omitted and is reported as unanswered. Answering one anyway is allowed, and it is
scored, because you volunteered it.

What this still cannot check is whether the profiles you declare are the ones you
implement, which is why declaring conformance remains a statement a person makes.
"""
from __future__ import annotations

import json
import pathlib
import shlex
import subprocess
import sys
import uuid

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
SUITE = ROOT / "tests" / "Conformance" / "beta6" / "suite.json"
REGISTRY = ROOT / "tests" / "registry.json"
NAMESPACE = uuid.UUID("6f1a0c3e-0b7e-5b2a-9c3d-4e5f60718293")


def option(name: str, default=None):
    argv = sys.argv
    if name in argv and argv.index(name) + 1 < len(argv):
        return argv[argv.index(name) + 1]
    return default


def rules_for_anchor() -> dict[str, list[str]]:
    """Section anchor -> the rule identifiers stated under it."""
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    out: dict[str, list[str]] = {}
    for requirement in registry.get("requirements", []):
        anchor = requirement.get("section")
        if anchor:
            out.setdefault(anchor, []).extend(requirement.get("rules", []))
    return out


def evaluation_ok(case: dict, result: dict) -> tuple[bool, str]:
    """Did evaluating the expressions produce what the specification requires?

    Only what the case names is checked, so a case can assert one computed value
    without stating every hint's result.
    """
    produced = result.get("evaluated")
    if not isinstance(produced, dict):
        return False, "did not report what evaluating the expressions produced"
    for group, wanted in case["expects"].items():
        got = produced.get(group) or {}
        for identifier, value in wanted.items():
            if identifier not in got:
                return False, f"reported no {group} for {identifier!r}"
            if got[identifier] != value:
                return False, (f"{group} for {identifier!r} came back "
                               f"{got[identifier]!r}, not {value!r}")
    return True, ""


def round_trip_ok(case: dict, result: dict) -> tuple[bool, str]:
    """Did writing the document back out keep it the same document?

    A reader that drops an unknown member, normalizes a response or renames an id
    accepts every document it is given. The only way to see it is to ask it to
    write, and then read what it wrote.
    """
    written = result.get("written")
    if not written:
        return False, "did not return the document it would write"
    representation = "yaml" if case["representation"].startswith("yaml") else "jsonc"
    try:
        records = aprlib.read_records(written, representation)
    except Exception as exc:  # noqa: BLE001
        return False, f"wrote something that will not read back: {exc}"
    if not records:
        return False, "wrote nothing"
    original = aprlib.read_records(case["document"], representation)
    if len(records) != len(original):
        return False, (f"wrote {len(records)} record(s) where it read "
                       f"{len(original)}: a stream keeps every record")
    for index, (before, after) in enumerate(zip(original, records)):
        if aprlib.digest(before) != aprlib.digest(after):
            return False, (f"record {index} came back as a different document: "
                           f"{aprlib.digest(after)} where the input was "
                           f"{aprlib.digest(before)}")
    for pointer in case.get("preserves") or []:
        before = aprlib.resolve_pointer(original[0], pointer)
        after = aprlib.resolve_pointer(records[0], pointer)
        if after is aprlib.MISSING:
            return False, f"dropped {pointer}, which must survive a round trip"
        if after != before:
            return False, f"changed {pointer} from {before!r} to {after!r}"
    return True, ""


def score(suite: dict, response: dict) -> tuple[list[dict], dict]:
    reported = {r["id"]: r for r in response.get("results", []) if isinstance(r, dict)}
    # "Optional to claim; binding once claimed." A case belonging to a profile the
    # implementation declares must be answered. Skipping it is a failure, not a
    # shrug, or a driver could claim every profile and answer nothing.
    claimed = set(response.get("implementation", {}).get("profiles") or ["core"])
    by_id = {c["id"]: c for c in suite["cases"]}
    rows: list[dict] = []
    tally = {"pass": 0, "fail": 0, "unanswered": 0, "discrepancy": 0}

    for case in suite["cases"]:
        result = reported.get(case["id"])
        row = {"id": case["id"], "rule": case["rule"], "expect": case["expect"]}
        row["profile"] = case.get("profile", "core")
        if result is None:
            if row["profile"] in claimed:
                row["status"] = "fail"
                row["detail"] = (f"unanswered, and this implementation claims "
                                 f"{row['profile']}, which binds it to every case in it")
                tally["fail"] += 1
            else:
                row["status"] = "unanswered"
                tally["unanswered"] += 1
            rows.append(row)
            continue

        outcome = result.get("outcome")
        row["outcome"] = outcome
        if case["expect"] == "equivalent":
            other = by_id.get(case.get("equivalentTo", ""))
            mine = result.get("digest")
            theirs = (reported.get(other["id"], {}) or {}).get("digest") if other else None
            ok = bool(mine) and mine == theirs
            if not ok:
                row["detail"] = ("digests differ or were not reported: "
                                 f"{mine} vs {theirs}")
        else:
            ok = outcome == ("valid" if case["expect"] == "valid" else "reject")
            if not ok:
                row["detail"] = f"expected {case['expect']}, reported {outcome}"
            elif case.get("digest") and result.get("digest") \
                    and result["digest"] != case["digest"]:
                # Accepted, but not as the same document. This is where a reader
                # whose scalar resolution is wrong is caught: it read the file
                # happily and produced a different semantic model.
                ok = False
                row["detail"] = (f"accepted, but produced {result['digest']} where the "
                                 f"suite requires {case['digest']}")

        # An advisory rule is only tested if a case can require the advisory. A
        # document that is valid either way cannot tell a reader that says the
        # right thing from one that says nothing.
        if ok and case.get("warns"):
            raised = set(result.get("warnings") or [])
            missing = [w for w in case["warns"] if w not in raised]
            if missing:
                ok = False
                row["detail"] = (f"accepted, but did not report "
                                 f"{', '.join(missing)}, which this case requires")
            else:
                extra = sorted(raised - set(case["warns"]))
                if extra:
                    row["discrepancy"] = (f"also warned {', '.join(extra)}, which the "
                                          f"suite does not name")
                    tally["discrepancy"] += 1

        if ok and case.get("expects"):
            ok, detail = evaluation_ok(case, result)
            if not ok:
                row["detail"] = detail

        if ok and case.get("roundTrip"):
            ok, detail = round_trip_ok(case, result)
            if not ok:
                row["detail"] = detail

        row["status"] = "pass" if ok else "fail"
        tally["pass" if ok else "fail"] += 1

        wanted = case.get("diagnostic")
        if ok and wanted and result.get("diagnostic") and result["diagnostic"] != wanted:
            row["status"] = "pass"
            row["discrepancy"] = f"reported {result['diagnostic']}, suite names {wanted}"
            tally["discrepancy"] += 1
        rows.append(row)
    return rows, tally


def oscal(suite: dict, response: dict, rows: list[dict], path: pathlib.Path) -> None:
    """Assessment results against the rule catalog, so a claim is machine-readable."""
    anchors = rules_for_anchor()
    implementation = response.get("implementation", {})
    observations, findings = [], []
    for row in rows:
        observations.append({
            "uuid": str(uuid.uuid5(NAMESPACE, row["id"])),
            "title": row["id"],
            "description": f"Suite case {row['id']} expecting {row['expect']}.",
            "methods": ["TEST"],
            "props": [{"name": "status", "ns": "https://skpt.cl/apr/oscal",
                       "value": row["status"]}],
        })
        if row["status"] == "fail":
            findings.append({
                "uuid": str(uuid.uuid5(NAMESPACE, "finding:" + row["id"])),
                "title": f"{row['id']} did not behave as the specification requires",
                "description": row.get("detail", ""),
                "target": {
                    "type": "objective-id",
                    "target-id": (anchors.get(row["rule"]) or [row["rule"]])[0],
                    "status": {"state": "not-satisfied"},
                },
                "related-observations": [
                    {"observation-uuid": str(uuid.uuid5(NAMESPACE, row["id"]))}],
            })
    document = {"assessment-results": {
        "uuid": str(uuid.uuid5(NAMESPACE, json.dumps(rows, sort_keys=True))),
        "metadata": {
            "title": f"APR conformance results — {implementation.get('name', 'unnamed')}",
            "last-modified": "1970-01-01T00:00:00Z",
            "version": str(implementation.get("version", "unknown")),
            "oscal-version": "1.1.2",
            "props": [{"name": "suite-version", "ns": "https://skpt.cl/apr/oscal",
                       "value": suite["suiteVersion"]},
                      {"name": "specification-sha256", "ns": "https://skpt.cl/apr/oscal",
                       "value": suite["specificationSha256"]}],
        },
        "import-ap": {"href": "../../docs/release/apr-oscal-catalog.json"},
        "results": [{
            "uuid": str(uuid.uuid5(NAMESPACE, "result")),
            "title": "Conformance suite run",
            "description": "Every case in the APR conformance suite, as answered by "
                           "the implementation's driver.",
            "start": "1970-01-01T00:00:00Z",
            "reviewed-controls": {"control-selections": [{"include-all": {}}]},
            "observations": observations,
            "findings": findings or [{
                "uuid": str(uuid.uuid5(NAMESPACE, "finding:none")),
                "title": "No case failed",
                "description": "Every answered case behaved as the specification requires.",
                "target": {"type": "objective-id", "target-id": "apr",
                           "status": {"state": "satisfied"}},
            }],
        }],
    }}
    path.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n",
                    encoding="utf-8")


def main() -> int:
    driver = option("--driver")
    if not driver:
        print(__doc__.strip().split("\n\n")[2])
        return 2
    if not SUITE.exists():
        print("tests/Conformance/beta6/suite.json is missing; "
              "run scripts/build-suite.py --write")
        return 2

    suite_text = SUITE.read_text(encoding="utf-8")
    suite = json.loads(suite_text)
    try:
        completed = subprocess.run(shlex.split(driver), input=suite_text,
                                   capture_output=True, text=True, timeout=600)
    except FileNotFoundError:
        print(f"driver not found: {driver}")
        return 2
    if completed.returncode != 0:
        print(f"driver exited {completed.returncode}")
        print(completed.stderr.strip()[:2000])
        return 2
    try:
        response = json.loads(completed.stdout)
    except json.JSONDecodeError as exc:
        print(f"driver did not write one JSON object on stdout: {exc}")
        print(completed.stdout[:500])
        return 2

    rows, tally = score(suite, response)
    implementation = response.get("implementation", {})

    if "--oscal" in sys.argv:
        oscal(suite, response, rows, pathlib.Path(option("--oscal")))

    if "--json" in sys.argv:
        print(json.dumps({"implementation": implementation, "suiteVersion":
                          suite["suiteVersion"], "tally": tally, "cases": rows},
                         indent=2, ensure_ascii=False))
    else:
        name = implementation.get("name", "unnamed implementation")
        version = implementation.get("version", "?")
        profiles = ", ".join(implementation.get("profiles") or []) or "none declared"
        print(f"{name} {version}   profiles: {profiles}")
        print(f"suite {suite['suiteVersion']}, {len(suite['cases'])} cases\n")
        for row in rows:
            if row["status"] == "pass" and "discrepancy" not in row:
                continue
            mark = {"fail": "FAIL", "unanswered": "----", "pass": "note"}[row["status"]]
            print(f"  {mark}  {row['id']}")
            if row.get("detail"):
                print(f"        {row['detail']}")
            if row.get("discrepancy"):
                print(f"        {row['discrepancy']}")
        print(f"\npassed {tally['pass']}, failed {tally['fail']}, "
              f"unanswered {tally['unanswered']}, "
              f"diagnostic discrepancies {tally['discrepancy']}")
    return 1 if tally["fail"] else 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""A conformance driver, written to demonstrate the contract and to gate it.

This answers the suite using scripts/aprlib.py and scripts/validate-apr.py, the
same reference primitives the corpus tooling uses. It is not an SDK and is not
an implementation of APR: it exists so the contract in
scripts/run-conformance.py has a worked example, and so the harness itself is
tested rather than assumed.

    python3 scripts/run-conformance.py --driver "python3 scripts/reference-driver.py"

A real driver is this shape in whatever language the implementation is written
in, and is usually shorter: read the suite, try each document, say what happened.
"""
from __future__ import annotations

import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import aprlib  # noqa: E402
import aprverify  # noqa: E402

_spec = importlib.util.spec_from_file_location("validate_apr", HERE / "validate-apr.py")
validate_apr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(validate_apr)


PROFILES = ["core", "core+streams", "core+attestations", "core+expressions"]


def answer(case, members, profiles=PROFILES):
    representation = "yaml" if case["representation"].startswith("yaml") else "jsonc"
    try:
        records = aprlib.read_records(case["document"], representation)
    except aprlib.AprError as exc:
        return {"id": case["id"], "outcome": "reject", "diagnostic": str(exc)}
    except Exception as exc:  # noqa: BLE001 - anything unparseable is a rejection
        return {"id": case["id"], "outcome": "reject",
                "diagnostic": type(exc).__name__}

    if not records:
        return {"id": case["id"], "outcome": "reject", "diagnostic": "NULL_DOCUMENT"}

    # Without core+streams a reader reads one form. Handed anything else, it says so
    # rather than choosing a record by position. [APR-CONF-001]
    if "core+streams" not in profiles and (
            len(records) != 1 or (isinstance(records[0], dict) and "recordType" in records[0])):
        return {"id": case["id"], "outcome": "reject",
                "diagnostic": "APR_STREAM_REQUIRES_ITERATION"}

    # A stream framed as a single document is not a stream: two JSON texts with no
    # record separator between them parse as one, and the second is lost.
    if case["representation"] == "jsonc-stream" and aprlib.RS not in case["document"]:
        return {"id": case["id"], "outcome": "reject",
                "diagnostic": "APR_STREAM_MISSING_RECORD_SEPARATOR"}

    report = validate_apr.Report(case["id"])
    for record in records:
        if isinstance(record, dict) and "recordType" in record:
            validate_apr.validate_attestation(report, record)
        else:
            validate_apr.validate_form(report, record, members)
    if report.errors:
        return {"id": case["id"], "outcome": "reject",
                "diagnostic": next(f["code"] for f in report.findings
                                   if f["severity"] == "error")}
    answer = {"id": case["id"], "outcome": "valid", "digest": aprlib.digest(records[0]),
              "warnings": sorted({f["code"] for f in report.findings
                                  if f["severity"] == "warning"})}
    if (case.get("evaluates") or case.get("expects")) and "core+expressions" in profiles:
        import aprexpr
        inputs = case.get("evaluate") or {}
        try:
            answer["evaluated"] = aprexpr.evaluate(
                records[0], _now=inputs.get("_now"), _today=inputs.get("_today"),
                ctx=inputs.get("ctx"))
        except Exception as exc:  # noqa: BLE001
            answer["evaluated"] = {"error": type(exc).__name__}
    if case.get("verifies") and "core+attestations" in profiles:
        answer["verified"] = aprverify.verify(records)
    if case.get("roundTrip"):
        # Writing is serializing the semantic model. Nothing is filtered on the way
        # out, which is the whole of what preservation asks for.
        answer["written"] = "".join(
            (aprlib.RS if len(records) > 1 else "")
            + json.dumps(record, indent=2, ensure_ascii=False) + "\n"
            for record in records)
    return answer


def main() -> int:
    suite = json.loads(sys.stdin.read())
    members = validate_apr.spec_members()
    # Attestations are checked structurally, streams are framed and read, and
    # expressions are evaluated through scripts/aprexpr.py. A run that names fewer
    # profiles gets an implementation claiming only those.
    claimed = [p for p in PROFILES if p in (suite.get("profiles") or PROFILES)]
    json.dump({
        "implementation": {
            "name": "APR reference tooling driver",
            "version": suite["formatVersion"],
            "profiles": claimed,
        },
        "results": [answer(case, members, claimed) for case in suite["cases"]],
    }, sys.stdout, indent=2, ensure_ascii=False)
    return 0


if __name__ == "__main__":
    sys.exit(main())

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

_spec = importlib.util.spec_from_file_location("validate_apr", HERE / "validate-apr.py")
validate_apr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(validate_apr)


def answer(case, members):
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
    return {"id": case["id"], "outcome": "valid", "digest": aprlib.digest(records[0])}


def main() -> int:
    suite = json.loads(sys.stdin.read())
    members = validate_apr.spec_members()
    json.dump({
        "implementation": {
            "name": "APR reference tooling driver",
            "version": suite["formatVersion"],
            "profiles": ["core", "core+streams"],
        },
        "results": [answer(case, members) for case in suite["cases"]],
    }, sys.stdout, indent=2, ensure_ascii=False)
    return 0


if __name__ == "__main__":
    sys.exit(main())

#!/usr/bin/env python3
"""The Python SDK's conformance driver.

Answers `tests/Conformance/beta6/suite.json` (with the answers withheld) using
the shipped `promptresponse` package — the actual library a caller installs,
not `scripts/aprlib.py`'s SDK-free oracle. See `docs/SDK_CONFORMANCE.md` for
the driver contract this implements, and `scripts/reference-driver.py` for the
worked example this is modeled on.

    python3 scripts/run-conformance.py --driver "python3 python/conformance_driver.py"

Not part of the distributed package (`pyproject.toml` includes only
`promptresponse*`): this is a driver over the SDK, the same relationship
`tools/PromptResponse.ConformanceDriver` has to `PromptResponse.Core`, not a
module the SDK ships.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from promptresponse import beta6, beta6_integrity, expressions, validation

RS = "\x1e"


def _diagnostic(exc: Exception) -> str:
    code = getattr(exc, "code", None)
    return code if code else type(exc).__name__


def _digest_of(record) -> str:
    # record.value is the document exactly as parsed -- an absent optional member
    # stays absent. form_value(record.document) goes through the typed model
    # instead, whose dataclass fields default in an explicit "" for an absent
    # response, so it digests a document that was never the one on the wire.
    # Both are "correct" readings of the same bytes; the suite's reference
    # digests are computed from the parsed value, so that's the comparable one.
    return beta6_integrity.digest(record.value)


def _evaluate(document, inputs: dict) -> dict:
    # Order matters: hidden/validation see only the responses as read, then
    # computed values (exprValue) fill in -- mirroring
    # scripts/aprexpr.py's evaluate(), which is the contract's own worked example.
    today = inputs.get("_today")
    now = inputs.get("_now")
    ctx = inputs.get("ctx")
    context = expressions.build_expression_context(document, today, ctx, now)

    hidden: dict[str, bool] = {}
    expected: dict[str, bool] = {}
    read_only: dict[str, bool] = {}
    result: dict[str, str] = {}
    for prompt in document.all_prompts():
        if prompt.hints.expr_hidden:
            hidden[prompt.id] = expressions.condition(prompt, prompt.hints.expr_hidden, context)
        if prompt.hints.expr_expected:
            expected[prompt.id] = expressions.condition(prompt, prompt.hints.expr_expected, context)
        if prompt.hints.expr_read_only:
            read_only[prompt.id] = expressions.condition(prompt, prompt.hints.expr_read_only, context)
        if prompt.hints.expr_validation:
            result[prompt.id] = expressions.validation_message(prompt, context) or ""

    expressions.recompute_computed_values(document, today, ctx, now)
    responses = {
        prompt.id: prompt.response
        for prompt in document.all_prompts()
        if prompt.hints.expr_value
    }
    return {"responses": responses, "hidden": hidden, "expected": expected,
            "readOnly": read_only, "validation": result}


def _written(records) -> str:
    # write_beta6_stream, given a form record that still carries the raw value
    # read_beta6_stream attached, echoes that raw text back rather than
    # re-serializing the typed document -- a legitimate preservation path, but
    # one that would let this driver claim a round trip without exercising the
    # SDK's own writer at all. Clearing it forces write_beta6_form's typed-model
    # path, which is the one part of this contract that tests writing.
    typed = [
        beta6.Beta6FormRecord(record.document, None) if isinstance(record, beta6.Beta6FormRecord)
        else record
        for record in records
    ]
    if len(typed) == 1 and isinstance(typed[0], beta6.Beta6FormRecord):
        return beta6.write_beta6_form(typed[0].document, "jsonc")
    return beta6.write_beta6_stream(typed, "jsonc")


def answer(case: dict, profiles: list[str]) -> dict:
    case_id = case["id"]
    representation = "yaml" if case["representation"].startswith("yaml") else "jsonc"

    if case["representation"] == "jsonc-stream" and RS not in case["document"]:
        # Two JSON texts with no record separator between them parse as one
        # document, silently losing the second -- refused before parsing gets
        # the chance to merge them.
        return {"id": case_id, "outcome": "reject",
                "diagnostic": "APR_STREAM_MISSING_RECORD_SEPARATOR"}

    try:
        if "core+streams" not in profiles:
            # Without core+streams a caller asks for one form, and the SDK refuses a
            # stream rather than choose a record from it. [APR-CONF-001]
            beta6.read_beta6_form(case["document"], representation)
        records = beta6.read_beta6_stream(case["document"], representation)
    except Exception as exc:  # noqa: BLE001 - anything unparseable is a rejection
        return {"id": case_id, "outcome": "reject", "diagnostic": _diagnostic(exc)}

    if not records:
        return {"id": case_id, "outcome": "reject", "diagnostic": "NULL_DOCUMENT"}

    # Attestation records are structurally validated during parsing itself
    # (beta6._validate_attestation); only form records need the separate
    # validate() pass.
    all_findings: list = []
    for record in records:
        if isinstance(record, beta6.Beta6FormRecord):
            report = validation.validate(record.document)
            all_findings.extend(report.errors)
    if all_findings:
        return {"id": case_id, "outcome": "reject", "diagnostic": all_findings[0].code}

    first = records[0]
    warnings: list[str] = []
    if isinstance(first, beta6.Beta6FormRecord):
        warnings = sorted({w.code for w in validation.validate(first.document).warnings})

    result = {
        "id": case_id, "outcome": "valid",
        "digest": _digest_of(first),
        "warnings": warnings,
    }

    if (case.get("evaluates") or case.get("expects")) and isinstance(first, beta6.Beta6FormRecord):
        try:
            result["evaluated"] = _evaluate(first.document, case.get("evaluate") or {})
        except Exception as exc:  # noqa: BLE001
            result["evaluated"] = {"error": type(exc).__name__}

    if case.get("roundTrip"):
        result["written"] = _written(records)

    return result


PROFILES = ["core", "core+streams", "core+expressions"]


def main() -> int:
    suite = json.loads(sys.stdin.read())
    # core+attestations is deliberately unclaimed, matching the .NET drivers:
    # those cases ask a verifier to resolve a manifest and report what it found,
    # which this reads and structurally accepts but does not yet verify. A case
    # outside every claimed profile is left unanswered rather than guessed at. A run
    # that names fewer profiles gets an implementation claiming only those.
    profiles = [p for p in PROFILES if p in (suite.get("profiles") or PROFILES)]
    claimed = [case for case in suite["cases"] if case.get("profile") in profiles]
    json.dump({
        "implementation": {
            "name": "PromptResponse (Python)",
            "version": suite["formatVersion"],
            "profiles": profiles,
        },
        "results": [answer(case, profiles) for case in claimed],
    }, sys.stdout, indent=2, ensure_ascii=False)
    return 0


if __name__ == "__main__":
    sys.exit(main())

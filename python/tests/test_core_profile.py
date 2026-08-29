"""What the Python expression profile must preserve when it does not verify.

Specification 2.2 and 2.3. These two rules cannot be tested from inside the
.NET implementation at all, because it implements every profile and can
therefore never exhibit core-only behaviour - they have sat ungated in
tests/registry.json for exactly that reason.

This implementation evaluates expressions but deliberately does not verify
signatures. Expression hints and signatures still round-trip without loss.
"""

import json
import pathlib

import pytest

import promptresponse as pr

CORPUS = pathlib.Path(__file__).resolve().parents[2] / "tests" / "Conformance" / "v1" / "valid"


def test_the_package_declares_the_profile_it_implements():
    assert pr.PROFILE == "core+expressions"


def test_no_verification_is_reachable():
    for forbidden in ("verify", "verify_all", "sign", "AprVerifier"):
        assert not hasattr(pr, forbidden), (
            f"promptresponse.{forbidden} exists; a core reader must not report a "
            "signature as verified (specification 2.3)"
        )


# ── 2.2 — expressions ────────────────────────────────────────────────────────

EXPRESSION_FIXTURE = CORPUS / "table-and-expressions.aprt"


def test_a_document_using_expressions_is_accepted():
    """An expression-capable reader still accepts expression-bearing forms."""
    document = pr.load(EXPRESSION_FIXTURE)
    assert pr.validate(document).is_valid, (
        "an expression-capable reader must not reject a document for using expressions "
        "(specification 2.2)"
    )


def test_expression_strings_survive_a_round_trip_untouched():
    """Expression source is carried through exactly, not normalised away.

    If a reader dropped these, a form would lose its logic on its next save.
    """
    source = json.loads(EXPRESSION_FIXTURE.read_text(encoding="utf-8-sig"))
    written = json.loads(pr.dumps(pr.load(EXPRESSION_FIXTURE)))

    def expressions(node, found):
        if isinstance(node, dict):
            for key, value in node.items():
                if key.startswith("expr") and isinstance(value, str):
                    found.append((key, value))
                expressions(value, found)
        elif isinstance(node, list):
            for item in node:
                expressions(item, found)
        return found

    before = expressions(source, [])
    assert before, "the fixture must actually contain expressions for this to prove anything"
    assert expressions(written, []) == before, "expression hints must survive untouched"


# ── 2.3 — signatures ─────────────────────────────────────────────────────────

SIGNED_FIXTURE = CORPUS / "signed-template.aprt"


def test_a_signed_document_is_accepted_and_readable():
    document = pr.load(SIGNED_FIXTURE)
    assert pr.validate(document).is_valid, (
        "a core-only reader must not reject a signed document (specification 2.3)"
    )
    assert list(document.all_prompts()), "and its content must be readable"


def test_the_signatures_array_survives_a_round_trip():
    source = json.loads(SIGNED_FIXTURE.read_text(encoding="utf-8-sig"))
    written = json.loads(pr.dumps(pr.load(SIGNED_FIXTURE)))

    assert source.get("signatures"), "the fixture must actually be signed"
    assert written.get("signatures") == source["signatures"], (
        "a core reader preserves signatures byte for byte. Re-serialising them "
        "differently would invalidate them, which is a worse outcome than "
        "refusing to open the document"
    )


def test_nothing_reports_a_signature_as_verified():
    """The document model exposes signatures as opaque data, with no verdict."""
    document = pr.load(SIGNED_FIXTURE)
    for signature in document.signatures:
        assert isinstance(signature, dict), (
            "signatures are carried as opaque data; a core reader has no opinion "
            "about them"
        )
        assert "verified" not in signature and "contentValid" not in signature, (
            "a core reader must not add a verdict it is not entitled to give "
            "(specification 2.3)"
        )

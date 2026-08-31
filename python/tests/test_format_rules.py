"""Format rules a corpus fixture cannot express on its own.

The corpus proves this reader agrees with the reference about specific
documents. These check the rules that are about *behaviour* - what happens on a
round trip, what is refused, what is left alone.
"""

import pytest

import promptresponse as pr


def _doc(**overrides):
    text = overrides.pop("text", None)
    if text is not None:
        return pr.loads(text)
    raise AssertionError("give text=")


# ── 3.2 strings only ─────────────────────────────────────────────────────────

@pytest.mark.parametrize("literal", ["42", "true", "3.14"])
def test_a_response_given_as_a_json_scalar_is_refused(literal):
    """Refused, never coerced. Silent coercion makes the reader disagree with
    the bytes it was handed, and a reader that quietly rewrites data is worse
    than one that declines it (specification 3.2)."""
    with pytest.raises(pr.AprParseError):
        pr.loads(
            '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
            '[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":'
            + literal + "}]}]}"
        )


def test_json_null_is_accepted_on_read_and_becomes_the_empty_string():
    """valid/null-response-coercion.aprf rules on this: null is tolerated when
    reading and coerced to "", and a conforming writer never emits it.

    Not a contradiction of the rule above. Refusing a number protects against a
    reader disagreeing with the bytes about a *value*; null carries no value to
    disagree about, and tolerating it is what lets a document written by a
    careless generator still be opened.
    """
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":null}]}]}'
    )
    assert next(document.all_prompts()).response == ""
    assert "null" not in pr.dumps(document), "and a writer must not emit it back"


def test_a_response_that_is_a_string_of_a_number_is_fine():
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":"42"}]}]}'
    )
    assert next(document.all_prompts()).response == "42"


# ── 3.3 any text is a valid response ─────────────────────────────────────────

@pytest.mark.parametrize("answer", [
    "some time last spring", "n/a", "see attached", "", "   ", "🙂", "'; DROP TABLE--",
])
def test_any_text_is_a_valid_response_whatever_the_hint_asked_for(answer):
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"When?","response":'
        + pr.serialization.json.dumps(answer)
        + ',"hints":{"expectedDataType":"date"}}]}]}'
    )
    assert pr.validate(document).is_valid, (
        "a hint suggests an affordance and never restricts what may be written; "
        "no error may arise from the content of a response (specification 6.1)"
    )


# ── 4.8 unknown members ──────────────────────────────────────────────────────

def test_a_member_from_a_newer_minor_version_survives_being_saved():
    source = (
        '{"version":"1.1","metadata":{"title":"T","futureThing":"kept"},"sections":'
        '[{"id":"s","title":"S","somethingNew":"also kept","prompts":'
        '[{"id":"p","label":"L","response":"","inventedLater":"kept too"}]}]}'
    )
    written = pr.dumps(pr.loads(source))

    for expected in ("futureThing", "somethingNew", "inventedLater", "kept"):
        assert expected in written, (
            f"{expected} was dropped. Without preservation every additive change to "
            "the format is destructive: an older reader silently strips what it does "
            "not recognise the first time somebody saves (specification 4.8)"
        )


def test_retired_members_are_dropped_rather_than_carried_forward():
    written = pr.dumps(pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","tableLayout":{"columns":[]},"prompts":'
        '[{"id":"p","label":"L","response":""}]}]}'
    ))
    assert "tableLayout" not in written, (
        "a retired member is dropped, not preserved, so a document does not carry "
        "a contradiction forward (specification 4.8.1)"
    )


# ── 6.1 the error list is exhaustive ─────────────────────────────────────────

def test_no_error_ever_arises_from_a_signature():
    """Specification 6.1 and 9.5: a validator that rejects a document because a
    signature is missing or broken is not implementing APR."""
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":""}]}],'
        '"signatures":[{"id":"x","role":"filler","cms":"not-even-base64"}]}'
    )
    assert pr.validate(document).is_valid


def test_a_section_and_a_prompt_may_share_an_id():
    """Separate namespaces (specification 4.4)."""
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"address","title":"Address","prompts":'
        '[{"id":"address","label":"Street","response":""}]}]}'
    )
    assert pr.validate(document).is_valid


# ── 7.1 normalisation ────────────────────────────────────────────────────────

def test_labels_are_nfc_normalised():
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"Cafe\\u0301","prompts":'
        '[{"id":"p","label":"L","response":""}]}]}'
    )
    assert document.sections[0].title == "Caf\u00e9", (
        "the same word typed on two keyboards must compare equal"
    )


def test_a_bidi_override_is_preserved_and_reported_in_a_response():
    """Responses are evidence: safety presentation warns without rewriting it."""
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":'
        '[{"id":"p","label":"L","response":"safe\\u202etxt.exe"}]}]}'
    )
    assert "\u202e" in next(document.all_prompts()).response
    assert any(w.code == "BIDI_OVERRIDE" for w in pr.validate(document).warnings)


def test_an_odd_but_harmless_character_is_left_alone_in_a_response():
    """A response is what a person typed. Zero-width spaces are reported by a
    tool that looks for them, never silently removed (specification 3.3)."""
    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":'
        '[{"id":"p","label":"L","response":"a\\u200bb"}]}]}'
    )
    assert next(document.all_prompts()).response == "a\u200bb"


# ── 4.10 roles ───────────────────────────────────────────────────────────────

def test_a_prompt_role_overrides_its_sections():
    from promptresponse import roles

    document = pr.loads(
        '{"version":"1.0-beta.6","metadata":{"title":"T"},'
        '"roles":[{"id":"nurse","name":"Nurse"}],"sections":'
        '[{"id":"s","title":"S","role":"nurse","prompts":['
        '{"id":"a","label":"A","response":""},'
        '{"id":"b","label":"B","response":"","role":"patient"}]}]}'
    )
    resolved = {p.id: r for p, r in roles.resolve(document)}
    assert resolved == {"a": "nurse", "b": "patient"}
    assert roles.display_name(document, "nurse") == "Nurse"
    assert roles.display_name(document, "notary") == "notary", (
        "an undeclared role shows its identifier rather than erroring; the "
        "vocabulary is open (specification 4.10)"
    )

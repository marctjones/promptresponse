"""Format rules a corpus fixture cannot express on its own.

The corpus proves this reader agrees with the reference about specific
documents. These check the rules that are about *behaviour* - what happens on a
round trip, what is refused, what is left alone.
"""

import json

import pytest

import promptresponse as pr

# ── 3.2 strings only ─────────────────────────────────────────────────────────

@pytest.mark.parametrize("literal", ["42", "true", "3.14"])
def test_a_response_given_as_a_json_scalar_is_refused(literal):
    """Refused, never coerced. Silent coercion makes the reader disagree with
    the bytes it was handed, and a reader that quietly rewrites data is worse
    than one that declines it (specification 3.2)."""
    with pytest.raises(pr.AprParseError):
        pr.loads(
            '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
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
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":null}]}]}'
    )
    assert next(document.all_prompts()).response == ""
    assert "null" not in pr.dumps(document), "and a writer must not emit it back"


def test_a_response_that_is_a_string_of_a_number_is_fine():
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":"42"}]}]}'
    )
    assert next(document.all_prompts()).response == "42"


# ── 3.3 any text is a valid response ─────────────────────────────────────────

@pytest.mark.parametrize("answer", [
    "some time last spring", "n/a", "see attached", "", "   ", "🙂", "'; DROP TABLE--",
])
def test_any_text_is_a_valid_response_whatever_the_hint_asked_for(answer):
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":[{"id":"p","label":"When?","response":'
        + pr.serialization.json.dumps(answer)
        + ',"hints":{"expectedDataType":"date"}}]}]}'
    )
    assert pr.validate(document).is_valid, (
        "a hint suggests an affordance and never restricts what may be written; "
        "no error may arise from the content of a response (specification 6.1)"
    )


# ── 4.8 unknown members ──────────────────────────────────────────────────────

def test_an_old_version_is_rejected():
    with pytest.raises(pr.AprParseError, match="1.0-beta.6"):
        pr.loads('{"aprVersion":"1.1","metadata":{"title":"T"},"sections":[]}')


# ── 6.1 the error list is exhaustive ─────────────────────────────────────────


def test_a_section_and_a_prompt_may_share_an_id():
    """Separate namespaces (specification 4.4)."""
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"address","title":"Address","prompts":'
        '[{"id":"address","label":"Street","response":""}]}]}'
    )
    assert pr.validate(document).is_valid


# ── 7.1 normalisation ────────────────────────────────────────────────────────

def test_non_nfc_titles_are_preserved_and_reported_not_silently_cleaned():
    """Specification 8.2.3: a reader that meets non-NFC human-facing text in a
    published form 'renders it defensively and never rewrites it'. NON_NFC_TEXT
    is a warning (7.2), not an error (7.1 is exhaustive) - it must be reported,
    but must never change what was read or block the document from validating."""
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"Cafe\\u0301","prompts":'
        '[{"id":"p","label":"L","response":""}]}]}'
    )
    assert document.sections[0].title == "Cafe\u0301", (
        "the exact bytes on the wire, not a normalised rewrite"
    )
    report = pr.validate(document)
    assert report.is_valid
    assert any(w.code == "NON_NFC_TEXT" for w in report.warnings)


# ── APR-TEXT-004 / 007 responses and submission targets held to the floor ────

def _codes(document):
    return [w.code for w in pr.validate(document).warnings]


def test_a_response_carrying_an_excluded_code_point_warns_and_a_carriage_return_does_not():
    def form(response):
        return pr.loads(json.dumps({
            "aprVersion": "1.0-beta.6", "metadata": {"title": "T"},
            "sections": [{"id": "s", "title": "S", "prompts": [{"id": "p", "label": "L", "response": response}]}]}))
    assert "RESPONSE_FORBIDDEN_CODE_POINT" in _codes(form("Ad​a"))
    assert "RESPONSE_FORBIDDEN_CODE_POINT" not in _codes(form("one\ttwo\r\nthree"))


def test_a_submission_url_carrying_an_excluded_code_point_warns():
    document = pr.loads(json.dumps({
        "aprVersion": "1.0-beta.6",
        "metadata": {"title": "T", "submissionUrls": ["https://uploads.exa​mple.gov/permits"]},
        "sections": [{"id": "s", "title": "S", "prompts": [{"id": "p", "label": "L"}]}]}))
    assert "SUBMISSION_URL_FORBIDDEN_CODE_POINT" in _codes(document)


# ── 5.2.1 submission targets ─────────────────────────────────────────────────

def _submitting(*entries):
    return json.dumps({
        "aprVersion": "1.0-beta.6",
        "metadata": {"title": "T", "submissionUrls": list(entries)},
        "sections": [{"id": "s", "title": "S", "prompts": [{"id": "p", "label": "L"}]}]})


def test_a_string_entry_reads_as_a_put_to_that_url():
    target, = pr.loads(_submitting("https://uploads.example.gov/abc")).metadata.submission_urls
    assert (target.kind, target.url) == ("put", "https://uploads.example.gov/abc")


def test_a_post_entry_exposes_the_policy_fields_it_is_sent_with():
    fields = {"key": "submissions/licence", "policy": "eyJjb25kaXRpb25zIjpbXX0="}
    document = pr.loads(_submitting({"kind": "post", "url": "https://uploads.example.gov/", "fields": fields,
                                     "expires": "2026-09-08T18:00:00Z", "refresh": "https://forms.example.gov/r"}))
    target, = document.metadata.submission_urls
    assert (target.kind, target.fields, target.expires, target.refresh) == (
        "post", fields, "2026-09-08T18:00:00Z", "https://forms.example.gov/r")
    assert pr.validate(document).is_valid


def test_mixed_entries_round_trip_as_written():
    """A string stays a string, and an object keeps every member it had, an empty
    `fields` and an unknown member included."""
    entries = [
        {"kind": "post", "url": "https://uploads.example.gov/", "fields": {}, "com.example.note": {"kept": True}},
        {"kind": "put", "url": "https://uploads.example.gov/licences/abc"},
        "mailto:licences@example.gov",
    ]
    written = json.loads(pr.dumps(pr.loads(_submitting(*entries))))
    assert written["metadata"]["submissionUrls"] == entries


@pytest.mark.parametrize("entry, path", [
    ({"url": "https://uploads.example.gov/abc"}, "metadata.submissionUrls[0].kind"),
    ({"kind": "put"}, "metadata.submissionUrls[0].url"),
    ({"kind": "post", "url": "https://uploads.example.gov/"}, "metadata.submissionUrls[0].fields"),
])
def test_an_entry_missing_a_required_member_is_reported(entry, path):
    errors = pr.validate(pr.loads(_submitting(entry))).errors
    assert [(error.code, error.path) for error in errors] == [("REQUIRED_FIELD", path)]


@pytest.mark.parametrize("entry", [
    42,
    {"kind": "post", "url": "https://uploads.example.gov/", "fields": "key=submissions/licence"},
    {"kind": "put", "url": "https://uploads.example.gov/abc", "expires": 1788890400},
    {"kind": "put", "url": "https://uploads.example.gov/abc", "refresh": {"url": "https://forms.example.gov/r"}},
])
def test_an_entry_member_of_the_wrong_type_is_refused(entry):
    with pytest.raises(pr.AprParseError) as excinfo:
        pr.loads(_submitting(entry))
    assert excinfo.value.code == "WRONG_TYPE"


def test_an_unrecognised_kind_is_neither_an_error_nor_a_warning():
    result = pr.validate(pr.loads(_submitting({"kind": "patch", "url": "https://uploads.example.gov/abc"})))
    assert result.is_valid and not result.warnings


def test_the_url_advisories_apply_to_an_object_entry_at_its_url():
    result = pr.validate(pr.loads(_submitting(
        {"kind": "put", "url": "ftp://example.com/drop"},
        {"kind": "put", "url": "https://uploads.exa​mple.gov/permits"})))
    assert {(w.code, w.path) for w in result.warnings} == {
        ("SUBMISSION_URL_UNSUPPORTED", "metadata.submissionUrls[0].url"),
        ("SUBMISSION_URL_FORBIDDEN_CODE_POINT", "metadata.submissionUrls[1].url"),
    }


# ── APR-TEXT-010 ids are machine keys ────────────────────────────────────────

@pytest.mark.parametrize("identifier, warns", [
    ("first name", True), ("café", True), ("q:1", True), ("first_name.v2-A", False)])
def test_an_id_outside_the_machine_key_characters_warns(identifier, warns):
    document = pr.loads(json.dumps({
        "aprVersion": "1.0-beta.6", "metadata": {"title": "T"},
        "sections": [{"id": "s", "title": "S", "prompts": [{"id": identifier, "label": "L"}]}]}))
    report = pr.validate(document)
    assert report.is_valid, "a warning, never a refusal"
    assert any(w.code == "ID_FORBIDDEN_CHARACTER" for w in report.warnings) is warns


def test_a_section_and_a_role_id_outside_the_machine_key_characters_warn():
    document = pr.loads(json.dumps({
        "aprVersion": "1.0-beta.6", "metadata": {"title": "T"},
        "roles": [{"id": "the notary", "name": "Notary"}],
        "sections": [{"id": "the applicant", "title": "S", "prompts": [{"id": "p", "label": "L"}]}]}))
    report = pr.validate(document)
    assert sum(w.code == "ID_FORBIDDEN_CHARACTER" for w in report.warnings) == 2


# ── APR-TEXT-012 confusable/mixed-script detection ──────────────────────────

def test_a_cyrillic_letter_hidden_in_a_latin_title_is_reported():
    """A Cyrillic 'a' (U+0430) inside an otherwise-Latin title is the classic
    homoglyph spoof (APR-TEXT-012): reported, never rejected -- Latin,
    Cyrillic and Greek share look-alike letters with no legitimate reason to
    co-occur, unlike CJK/Hangul/Indic scripts routinely mixing with Latin."""
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"P\\u0430yPal Permit","prompts":'
        '[{"id":"p","label":"L","response":""}]}]}'
    )
    report = pr.validate(document)
    assert report.is_valid
    assert any(w.code == "CONFUSABLE_SCRIPT_MIX" for w in report.warnings)


def test_legitimate_multi_script_titles_are_not_flagged():
    """CJK+Latin, Hangul+Latin and accented-Latin text are ordinary multi-script
    or single-script text, not a confusable mix, and must never warn."""
    for title in ("Toyota パーツ", "한국 Corp", "Café", "PayPal"):
        document = pr.loads(json.dumps({
            "aprVersion": "1.0-beta.6",
            "metadata": {"title": title},
            "sections": [{"id": "s", "title": "S", "prompts":
                          [{"id": "p", "label": "L", "response": ""}]}],
        }))
        codes = [w.code for w in pr.validate(document).warnings]
        assert "CONFUSABLE_SCRIPT_MIX" not in codes, title


def test_a_bidi_override_is_preserved_and_reported_in_a_response():
    """Responses are evidence: safety presentation warns without rewriting it."""
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":'
        '[{"id":"p","label":"L","response":"safe\\u202etxt.exe"}]}]}'
    )
    assert "\u202e" in next(document.all_prompts()).response
    assert any(w.code == "BIDI_OVERRIDE" for w in pr.validate(document).warnings)


def test_an_odd_but_harmless_character_is_left_alone_in_a_response():
    """A response is what a person typed. Zero-width spaces are reported by a
    tool that looks for them, never silently removed (specification 3.3)."""
    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":'
        '[{"id":"s","title":"S","prompts":'
        '[{"id":"p","label":"L","response":"a\\u200bb"}]}]}'
    )
    assert next(document.all_prompts()).response == "a\u200bb"


# ── 4.10 roles ───────────────────────────────────────────────────────────────

def test_a_prompt_role_overrides_its_sections():
    from promptresponse import roles

    document = pr.loads(
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},'
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

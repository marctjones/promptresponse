"""Reader refusal stages: parse failures vs validation failures (APR-VAL-003/004).

The portable suite cannot observe the stage at which a reader refuses a
document: a parse failure and a validation failure both answer reject. Each
reader must still refuse at the specified stage under the specified code, so
that distinction is asserted per implementation rather than per document.
"""

import pytest

from promptresponse.beta6 import read_beta6_stream
from promptresponse.errors import AprParseError

FORM = ('{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},'
        '"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}')


def _refusal_code(source: str, representation: str = "jsonc") -> str:
    with pytest.raises(AprParseError) as excinfo:
        read_beta6_stream(source, representation)
    return excinfo.value.code


def test_malformed_source_is_a_parse_error():
    assert _refusal_code("{not json") == "PARSE_ERROR"
    assert _refusal_code("[1, 2,") == "PARSE_ERROR"


def test_non_object_record_is_wrong_type_not_a_parse_failure():
    # Well-formed JSON parses (APR-VAL-003), so a value that is not an object is a record
    # that is neither a form nor an attestation (APR-STREAM-007), not a parse failure.
    assert _refusal_code("[1, 2, 3]") == "WRONG_TYPE"
    assert _refusal_code('"just a string"') == "WRONG_TYPE"


def test_a_json_array_after_a_form_is_wrong_type_not_mixed_representations():
    stream = "\x1e" + FORM + "\n\x1e[1, 2]\n"
    assert _refusal_code(stream) == "WRONG_TYPE"


def test_a_yaml_record_in_a_jsonc_stream_is_still_mixed_representations():
    stream = "\x1e" + FORM + "\n\x1e" + "aprVersion: 1.0-beta.6\nmetadata:\n  title: T\n"
    assert _refusal_code(stream) == "APR_STREAM_MIXED_REPRESENTATIONS"


def test_excess_nesting_is_refused_cleanly_and_ordinary_nesting_is_not():
    # APR-SEC-011 and APR-SEC-019: a reader bounds nesting depth and refuses past the bound,
    # rather than recursing until the interpreter gives out.
    hostile = "[" * 20_000 + "]" * 20_000
    assert _refusal_code('{"aprVersion":"1.0-beta.6","x":' + hostile + "}") == "PARSE_ERROR"
    ordinary = "[" * 100 + "]" * 100
    assert _refusal_code('{"aprVersion":"1.0-beta.6","x":' + ordinary + "}") != "PARSE_ERROR"


def test_a_bracket_inside_a_string_does_not_count_as_nesting():
    records = read_beta6_stream(FORM.replace('"T"', '"' + "[" * 500 + '"'), "jsonc")
    assert len(records) == 1


def test_missing_apr_version_is_required_field_not_unsupported():
    # An absent or blank aprVersion states no version, so it is the missing
    # member it is rather than a version this reader does not accept.
    assert _refusal_code(FORM.replace('"aprVersion":"1.0-beta.6",', "")) == "REQUIRED_FIELD"
    assert _refusal_code(FORM.replace("1.0-beta.6", "")) == "REQUIRED_FIELD"
    assert _refusal_code(FORM.replace("1.0-beta.6", "  ")) == "REQUIRED_FIELD"


def test_wrong_version_is_unsupported():
    assert _refusal_code(FORM.replace("1.0-beta.6", "1.0-beta.3")) == "UNSUPPORTED_VERSION"


def test_unknown_record_type_is_wrong_type():
    assert _refusal_code(FORM.replace('"aprVersion"', '"recordType":"signature","aprVersion"')) == "WRONG_TYPE"


def test_a_new_document_states_the_current_version_and_an_older_one_is_read_as_stated():
    from promptresponse import ACCEPTED_VERSIONS, CURRENT_VERSION, loads
    from promptresponse.models import AprDocument

    assert CURRENT_VERSION == "1.0-beta.7" and CURRENT_VERSION in ACCEPTED_VERSIONS
    assert AprDocument().version == CURRENT_VERSION
    older = loads(FORM)
    assert older.version == "1.0-beta.6", "a reader keeps the version a document states"

import pytest

from promptresponse.errors import AprParseError
from promptresponse.serialization import loads
from promptresponse.validation import validate
from promptresponse.wire import compact_members, string_list_member, string_member, unknown_members
from promptresponse.versioning import is_supported_version


def _table(max_rows) -> str:
    return (
        '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},'
        '"sections":[{"id":"t","title":"T","kind":"table","maxRows":' + repr(max_rows) + ','
        '"sections":[{"id":"r","title":"R","prompts":[{"id":"p","label":"P"}]}]}]}'
    )


def test_max_rows_must_be_an_integer_and_at_least_one():
    # docs/BETA6_WIRE_DELTA.md types maxRows as "integer, at least 1". .NET's
    # model declares it C# int?, so a fractional JSON number fails to
    # deserialize at all; JSON has no separate integer type, so loads() must
    # check the value explicitly rather than trust json.loads's int-vs-float
    # spelling -- 5.0 and 5 are the same integer, and only 2.5 is not one
    # (issue #378).
    with pytest.raises(AprParseError) as excinfo:
        loads(_table(2.5))
    assert excinfo.value.code == "WRONG_TYPE"

    for max_rows in (0, -3):
        document = loads(_table(max_rows))
        result = validate(document)
        assert [error.code for error in result.errors] == ["WRONG_TYPE"]

    assert validate(loads(_table(5))).is_valid
    assert validate(loads(_table(5.0))).is_valid


def test_wire_helpers_reject_non_string_members_without_coercion():
    with pytest.raises(AprParseError, match="prompt.response must be a string"):
        string_member({"response": 42}, "response", "prompt")
    with pytest.raises(AprParseError, match="array of strings"):
        string_list_member({"submissionUrls": ["https://example.test", 4]}, "submissionUrls", "metadata")


def test_wire_helpers_preserve_unknown_members_and_omit_empty_members():
    assert unknown_members({"id": "one", "future": {"kept": True}}, {"id"}) == {"future": {"kept": True}}
    assert compact_members({"kept": "value", "none": None, "empty": [], "object": {}}) == {"kept": "value"}


@pytest.mark.parametrize(("version", "expected"), [("1.0-beta.6", True), ("1.0-beta", False), ("1.7", False), ("2.0", False), ("not-a-version", False)])
def test_version_policy_accepts_only_beta6(version, expected):
    assert is_supported_version(version) is expected

import pytest

from promptresponse.errors import AprParseError
from promptresponse.wire import compact_members, string_list_member, string_member, unknown_members
from promptresponse.versioning import is_supported_version


def test_wire_helpers_reject_non_string_members_without_coercion():
    with pytest.raises(AprParseError, match="prompt.response must be a string"):
        string_member({"response": 42}, "response", "prompt")
    with pytest.raises(AprParseError, match="array of strings"):
        string_list_member({"submissionUrls": ["https://example.test", 4]}, "submissionUrls", "metadata")


def test_wire_helpers_preserve_extensions_and_omit_retired_or_empty_members():
    assert unknown_members({"id": "one", "future": {"kept": True}, "tableLayout": {}}, {"id"}) == {"future": {"kept": True}}
    assert compact_members({"kept": "value", "none": None, "empty": [], "object": {}}) == {"kept": "value"}


@pytest.mark.parametrize(("version", "expected"), [("1.0-beta", True), ("1.7", True), ("2.0", False), ("not-a-version", False)])
def test_version_policy_is_independent_of_model_parsing(version, expected):
    assert is_supported_version(version) is expected

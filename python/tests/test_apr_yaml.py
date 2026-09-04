"""APR-YAML exclusions are structural, not textual (specification 4.5).

The corpus and the specification's own examples prove that an anchor, alias,
tag, merge key or directive is refused. These tests prove the converse the
corpus does not: the indicator characters are ordinary content anywhere the
YAML grammar says they are.
"""

import pytest

import promptresponse as pr
from promptresponse.apr_yaml import AprYamlError, load_all

FORM = """version: "1.0-beta.6"
metadata:
  title: Fee schedule
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        hints:
          exprValue: string(fee_count * 8.0)
        response: {value}
"""


def read(value: str):
    return pr.read_beta6_stream(FORM.format(value=value), "yaml")[0].value


def test_plain_scalar_containing_an_asterisk_is_a_string():
    record = read("total * 2")
    prompt = record["sections"][0]["prompts"][0]
    assert prompt["hints"]["exprValue"] == "string(fee_count * 8.0)"
    assert prompt["response"] == "total * 2"


@pytest.mark.parametrize(
    "value",
    ["a & b", "x! y", "'*not an alias'", '"&not an anchor"', "'!not a tag'"],
    ids=["ampersand", "bang", "quoted-alias", "quoted-anchor", "quoted-tag"],
)
def test_indicator_characters_inside_scalar_content_are_ordinary(value):
    assert read(value)["sections"][0]["prompts"][0]["response"] == value.strip("'\"")


def test_indicator_characters_inside_flow_collections_are_ordinary():
    assert load_all("a: [a & b, c * d, e! f]\nb: {k: a * b}\n") == [
        {"a": ["a & b", "c * d", "e! f"], "b": {"k": "a * b"}}
    ]


def test_quoted_merge_key_spelling_is_a_string_key():
    assert load_all('"<<": 1\n') == [{"<<": 1.0}]


def test_merge_key_inside_a_sequence_is_a_string_item():
    assert load_all("- <<\n") == [["<<"]]


@pytest.mark.parametrize(
    "source, message",
    [
        ("a: &m\n  b: 1\n", "anchors"),
        ("a: [*m]\n", "aliases"),
        ("a: !!str 1\n", "tags"),
        ("a: ! 1\n", "tags"),
        ("a:\n  <<: {b: 1}\n", "merge keys"),
        ("a: {<<: {b: 1}}\n", "merge keys"),
        ("%YAML 1.2\n---\na: 1\n", "directives"),
        ("%TAG !e! tag:example.com,2000:\n---\na: 1\n", "directives"),
        ("a: 1\n...\n%TAG !e! tag:example.com,2000:\n---\nb: 2\n", "directives"),
        ("a: [.inf]\n", "non-finite"),
    ],
    ids=["anchor", "alias", "tag", "non-specific-tag", "merge-key", "flow-merge-key", "yaml-directive", "tag-directive", "later-document-directive", "non-finite-in-sequence"],
)
def test_real_yaml_features_are_refused(source, message):
    with pytest.raises(AprYamlError, match=message):
        load_all(source)


def test_reader_reports_excluded_constructs_as_parse_errors():
    with pytest.raises(pr.AprParseError, match="anchors"):
        pr.read_beta6_form(FORM.format(value="&r 1"), "yaml")

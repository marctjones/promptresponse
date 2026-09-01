import json

import pytest

from promptresponse.spec_semantic import (
    SemanticReviewError,
    build_prompt,
    load_rubric,
    parse_model_report,
)


def rubric():
    return load_rubric(
        {
            "version": "test-rubric-1",
            "items": [
                {"id": "authority", "question": "Is authority defined?"},
                {"id": "errors", "question": "Are errors defined?"},
            ],
        }
    )


def test_prompt_requires_non_authoritative_evidence_review():
    version, items = rubric()
    prompt = build_prompt(specification="# APR\nRules", rubric_version=version, items=items)

    assert "specification is conformant or correct" in prompt
    assert "exact quotation" in prompt
    assert "authority" in prompt


def test_model_report_accepts_one_evidence_backed_finding_per_rubric_item():
    version, items = rubric()
    payload = {
        "rubric_version": version,
        "findings": [
            {
                "id": "authority",
                "status": "addressed",
                "evidence": ["Authority is explicit."],
                "reason": "The source names it.",
            },
            {
                "id": "errors",
                "status": "needs_human_review",
                "evidence": [],
                "reason": "No error section found.",
            },
        ],
    }

    assert parse_model_report(json.dumps(payload), rubric_version=version, items=items) == payload


@pytest.mark.parametrize(
    "payload",
    [
        "not-json",
        json.dumps({"rubric_version": "wrong", "findings": []}),
        json.dumps(
            {
                "rubric_version": "test-rubric-1",
                "findings": [
                    {
                        "id": "authority",
                        "status": "pass",
                        "evidence": ["x"],
                        "reason": "x",
                    },
                    {
                        "id": "errors",
                        "status": "addressed",
                        "evidence": ["x"],
                        "reason": "x",
                    },
                ],
            }
        ),
    ],
)
def test_model_report_rejects_untrusted_or_invalid_output(payload):
    version, items = rubric()
    with pytest.raises(SemanticReviewError):
        parse_model_report(payload, rubric_version=version, items=items)


def test_rubric_rejects_duplicate_identifiers():
    with pytest.raises(SemanticReviewError):
        load_rubric(
            {
                "version": "test",
                "items": [
                    {"id": "same", "question": "one"},
                    {"id": "same", "question": "two"},
                ],
            }
        )


def test_extract_json_object_strips_reasoning_and_fences():
    """A reasoning model answers with its chain of thought before the JSON."""
    from promptresponse.spec_semantic import extract_json_object

    wrapped = (
        "<think>Let me read the specification carefully.</think>\n"
        "```json\n"
        '{"rubric_version": "v1", "findings": []}\n'
        "```"
    )
    assert json.loads(extract_json_object(wrapped)) == {
        "rubric_version": "v1",
        "findings": [],
    }


def test_extract_json_object_handles_braces_inside_strings():
    from promptresponse.spec_semantic import extract_json_object

    payload = '{"reason": "a } inside a string", "findings": []}'
    assert json.loads(extract_json_object("noise " + payload + " trailing"))["reason"] == (
        "a } inside a string"
    )


def test_extract_json_object_rejects_a_response_with_no_object():
    from promptresponse.spec_semantic import SemanticReviewError, extract_json_object

    with pytest.raises(SemanticReviewError):
        extract_json_object("I could not complete the review.")

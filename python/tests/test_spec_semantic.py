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

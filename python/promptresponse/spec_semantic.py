"""Non-authoritative, local-model review helpers for the APR specification.

This module deliberately does not decide APR conformance.  It turns a fixed
rubric plus the normative source into a bounded prompt and validates that a
model returned evidence a human can inspect.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any


class SemanticReviewError(ValueError):
    """Raised when a semantic-review report is not safe to consume."""


@dataclass(frozen=True)
class RubricItem:
    id: str
    question: str


def load_rubric(payload: dict[str, Any]) -> tuple[str, list[RubricItem]]:
    version = payload.get("version")
    items = payload.get("items")
    if not isinstance(version, str) or not version.strip():
        raise SemanticReviewError("rubric version must be a non-blank string")
    if not isinstance(items, list) or not items:
        raise SemanticReviewError("rubric items must be a non-empty list")

    parsed: list[RubricItem] = []
    seen: set[str] = set()
    for item in items:
        if not isinstance(item, dict):
            raise SemanticReviewError("each rubric item must be an object")
        item_id = item.get("id")
        question = item.get("question")
        if not isinstance(item_id, str) or not item_id.strip():
            raise SemanticReviewError("rubric item id must be a non-blank string")
        if item_id in seen:
            raise SemanticReviewError(f"duplicate rubric item id: {item_id}")
        if not isinstance(question, str) or not question.strip():
            raise SemanticReviewError(f"rubric question missing for {item_id}")
        seen.add(item_id)
        parsed.append(RubricItem(item_id, question))
    return version, parsed


def build_prompt(*, specification: str, rubric_version: str, items: list[RubricItem]) -> str:
    """Build a constrained reviewer prompt with no authority to alter APR."""
    questions = "\n".join(f"- {item.id}: {item.question}" for item in items)
    return f"""You are a meticulous specification reviewer. Review only the supplied APR beta
specification text. Do not propose new APR features, do not infer unstated
behavior, and do not claim that the specification is conformant or correct.

For every rubric item, return exactly one object. Mark it `addressed` only if
the supplied text directly answers the question. Otherwise mark it
`needs_human_review`. Evidence must be a short exact quotation from the
supplied text, or an empty list if there is no supporting text. Add a concise
reason. Do not use Markdown or prose outside the JSON value.

Return this exact JSON shape:
{{
  "rubric_version": "{rubric_version}",
  "findings": [
    {{"id": "...", "status": "addressed|needs_human_review",
      "evidence": ["exact quotation"], "reason": "..."}}
  ]
}}

Rubric:
{questions}

APR beta specification begins:
---
{specification}
---
APR beta specification ends.
"""


def parse_model_report(
    text: str, *, rubric_version: str, items: list[RubricItem]
) -> dict[str, Any]:
    """Validate a model report before it is written as review evidence."""
    try:
        payload = json.loads(text)
    except json.JSONDecodeError as exc:
        raise SemanticReviewError("model response was not valid JSON") from exc

    if not isinstance(payload, dict) or payload.get("rubric_version") != rubric_version:
        raise SemanticReviewError("model response has a wrong rubric version")
    findings = payload.get("findings")
    expected_ids = {item.id for item in items}
    if not isinstance(findings, list) or len(findings) != len(expected_ids):
        raise SemanticReviewError("model response must contain one finding per rubric item")

    received_ids: set[str] = set()
    for finding in findings:
        if not isinstance(finding, dict):
            raise SemanticReviewError("each finding must be an object")
        item_id = finding.get("id")
        status = finding.get("status")
        evidence = finding.get("evidence")
        reason = finding.get("reason")
        if item_id not in expected_ids or item_id in received_ids:
            raise SemanticReviewError("findings must use each rubric id exactly once")
        if status not in {"addressed", "needs_human_review"}:
            raise SemanticReviewError("finding status is invalid")
        if not isinstance(evidence, list) or not all(
            isinstance(quote, str) and quote.strip() for quote in evidence
        ):
            raise SemanticReviewError("finding evidence must be a list of non-blank strings")
        if not isinstance(reason, str) or not reason.strip():
            raise SemanticReviewError("finding reason must be a non-blank string")
        received_ids.add(item_id)
    if received_ids != expected_ids:
        raise SemanticReviewError("model response omitted a rubric item")
    return payload

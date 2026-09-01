"""Non-authoritative, local-model review helpers for the APR specification.

This module deliberately does not decide APR conformance.  It turns a fixed
rubric plus the normative source into a bounded prompt and validates that a
model returned evidence a human can inspect.
"""

from __future__ import annotations

import json
import re
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


_THINK = re.compile(r"<think>.*?</think>", re.DOTALL | re.IGNORECASE)
_FENCE = re.compile(r"^```[a-zA-Z]*\n|\n```$", re.MULTILINE)


def extract_json_object(text: str) -> str:
    """Return the first balanced JSON object in a model response.

    A reasoning model emits its chain of thought before the answer, and a chat
    model likes to wrap JSON in a code fence. Neither is a defect in the report
    itself, so the wrapper is stripped rather than the report rejected. Every
    check that matters - rubric version, one finding per item, known ids,
    permitted status values - still runs against the parsed object, unchanged.
    """
    cleaned = _FENCE.sub("", _THINK.sub("", text)).strip()
    start = cleaned.find("{")
    if start < 0:
        raise SemanticReviewError("model response contained no JSON object")
    depth = 0
    in_string = False
    escaped = False
    for index in range(start, len(cleaned)):
        char = cleaned[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return cleaned[start : index + 1]
    raise SemanticReviewError("model response contained no balanced JSON object")


def parse_model_report(
    text: str, *, rubric_version: str, items: list[RubricItem]
) -> dict[str, Any]:
    """Turn a model response into one finding per rubric item.

    Every rubric item gets an entry. An item the model answered well keeps its
    answer; an item it skipped, duplicated, or answered malformedly is recorded as
    ``no_model_answer`` with the reason it was not usable.

    An earlier contract required exactly one well-formed finding per item and
    raised otherwise. Against a local model that meant one null id discarded six
    good findings, so the suite usually produced nothing at all. A review that
    reports "the model did not answer this" is more useful than no review, and it
    is still honest: nothing is invented, and an unusable answer is never counted
    as addressed.
    """
    try:
        payload = json.loads(extract_json_object(text))
    except json.JSONDecodeError as exc:
        raise SemanticReviewError("model response was not valid JSON") from exc

    if not isinstance(payload, dict):
        raise SemanticReviewError("model response is not an object")
    if payload.get("rubric_version") != rubric_version:
        raise SemanticReviewError("model response has a wrong rubric version")

    raw = payload.get("findings")
    if not isinstance(raw, list):
        raise SemanticReviewError("model response has no findings list")

    expected = {item.id: item for item in items}
    accepted: dict[str, dict[str, Any]] = {}
    rejected: list[dict[str, str]] = []

    for index, finding in enumerate(raw):
        def discard(why: str) -> None:
            rejected.append({"index": index, "reason": why})

        if not isinstance(finding, dict):
            discard("finding is not an object")
            continue
        item_id = finding.get("id")
        if item_id not in expected:
            discard(f"unknown rubric id {item_id!r}")
            continue
        if item_id in accepted:
            discard(f"duplicate finding for {item_id}")
            continue
        if finding.get("status") not in {"addressed", "needs_human_review"}:
            discard(f"{item_id}: status {finding.get('status')!r} is not permitted")
            continue
        evidence = finding.get("evidence")
        if not isinstance(evidence, list) or not all(
            isinstance(quote, str) and quote.strip() for quote in evidence
        ):
            discard(f"{item_id}: evidence must be a list of non-blank quotations")
            continue
        reason = finding.get("reason")
        if not isinstance(reason, str) or not reason.strip():
            discard(f"{item_id}: reason must be a non-blank string")
            continue
        accepted[item_id] = {
            "id": item_id,
            "status": finding["status"],
            "evidence": evidence,
            "reason": reason.strip(),
        }

    findings = []
    for item in items:
        if item.id in accepted:
            findings.append(accepted[item.id])
        else:
            findings.append({
                "id": item.id,
                "status": "no_model_answer",
                "evidence": [],
                "reason": "the model returned no usable finding for this rubric item",
            })

    answered = sum(1 for f in findings if f["status"] != "no_model_answer")
    if answered == 0:
        raise SemanticReviewError("model response contained no usable finding")

    return {
        "rubric_version": rubric_version,
        "findings": findings,
        "coverage": {
            "rubricItems": len(items),
            "answered": answered,
            "unanswered": len(items) - answered,
        },
        "discardedFindings": rejected,
    }

"""Stage 4 — write the APR document.

APR is layout-free: the document that comes out of here has no coordinates in
it at all. The geometry every earlier stage worked in was the means, not the
product, and it stays behind in `Conversion.blanks` for anything that needs to
point back at the page.
"""
from __future__ import annotations

import re
from typing import Any

from .model import Question

__all__ = ["build_document", "APR_VERSION"]

APR_VERSION = "1.0-beta.6"

_UNSAFE = re.compile(r"[^a-z0-9]+")


def _identifier(text: str, taken: set[str], fallback: str) -> str:
    base = _UNSAFE.sub("-", text.strip().lower()).strip("-")[:48] or fallback
    candidate, n = base, 2
    while candidate in taken:
        candidate, n = f"{base}-{n}", n + 1
    taken.add(candidate)
    return candidate


def build_document(title: str, questions: list[Question],
                   sections: list[dict[str, Any]]) -> dict[str, Any]:
    """Arrange questions into a valid APR template."""
    titles = {s["id"]: s["title"] for s in sections if s.get("id")}

    # Grouped by the section the model put them in, in first-seen order, so the
    # questions come out in the order the form asks them.
    order: list[str | None] = []
    by_section: dict[str | None, list[Question]] = {}
    for q in questions:
        key = q.section if q.section in titles else None
        if key not in by_section:
            by_section[key] = []
            order.append(key)
        by_section[key].append(q)

    taken_sections: set[str] = set()
    out_sections = []
    for key in order:
        members = by_section[key]
        name = titles.get(key or "") or "Form"
        taken_prompts: set[str] = set()
        out_sections.append({
            "id": _identifier(name, taken_sections, "section"),
            "title": name,
            "prompts": [
                _prompt(q, taken_prompts, i) for i, q in enumerate(members, start=1)
            ],
        })

    return {
        "aprVersion": APR_VERSION,
        "documentType": "template",
        "metadata": {"title": title},
        "sections": out_sections,
    }


def _prompt(q: Question, taken: set[str], index: int) -> dict[str, Any]:
    hints: dict[str, Any] = {"expectedDataType": q.data_type}
    if q.choices:
        hints["suggestedValues"] = list(q.choices)

    return {
        "id": _identifier(q.label, taken, f"field-{index}"),
        "label": q.label,
        "response": "",
        "hints": hints,
    }

"""Stage 3 — ask a local vision model what each numbered blank asks for.

Two jobs, and only two: name each box, and say which boxes are one question the
form split up. It is not asked to find fields, because it is measurably worse
at that than reading the drawn lines, and because letting it add fields also
stopped it grouping them -- 0.79 mean F1 with the instruction closed, 0.70 with
it open.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .model import Blank, MarkedPage, Question

__all__ = ["Namer", "MlxNamer", "DEFAULT_MODEL", "PROMPT"]

DEFAULT_MODEL = "mlx-community/Qwen3-VL-4B-Instruct-4bit"
"""Measured best of seven on this corpus, and beats its own 8B sibling on both
quality and speed (F1 0.71 vs 0.66; 106s vs 255s a form)."""

PROMPT = """You are looking at one page of a government form. Every fillable field on this page has already been located for you and is outlined in red with a number beside it.

Your job is ONLY to say what each numbered field asks for. Do not look for fields yourself; do not add or remove any.

Respond with ONLY a single JSON object, no prose:

{
  "sections": [{"id": "kebab-case-id", "title": "Section title as printed"}],
  "fields": [
    {"n": 1, "label": "The exact visible label/question text for field 1",
     "section_id": "id of the section this field is in",
     "field_kind": "text_line | multiline | checkbox | choice | signature | date_field | table_cell",
     "expected_data_type": "text | multiline | email | phone | url | number | currency | date | time | datetime | boolean",
     "group": "optional: a name shared by every numbered field that is really ONE question split into several boxes, e.g. a social security number in three boxes"}
  ]
}

Rules:
- Output one entry per numbered box, using its number in "n".
- The label is the question printed on the form for that box, not your description of it.
- If several numbered boxes are pieces of ONE answer (a number split by printed dashes, a date split into day/month/year), give them the same "group" value and the same label.
- checkbox/yes-no -> expected_data_type "boolean".
- Output strict JSON: double-quoted keys, no trailing commas, no comments."""


class Namer:
    """What a naming backend has to provide.

    Substitute your own to use a different runtime or model; the rest of the
    pipeline only needs `name_page`.
    """

    def name_page(self, page: MarkedPage) -> tuple[list[Question], list[dict], str]:
        """Return the page's questions, its sections, and the raw reply."""
        raise NotImplementedError

    def close(self) -> None:
        """Release the model. Always called, even when a page fails."""


@dataclass
class MlxNamer(Namer):
    """Runs a vision model locally through MLX, on Apple silicon."""

    model_id: str = DEFAULT_MODEL
    max_tokens: int = 4000

    def __post_init__(self) -> None:
        from mlx_vlm import load  # imported late: a heavy, platform-specific dep

        self._model, self._processor = load(self.model_id)

    def name_page(self, page: MarkedPage) -> tuple[list[Question], list[dict], str]:
        from mlx_vlm import generate
        from mlx_vlm.prompt_utils import apply_chat_template

        formatted = apply_chat_template(
            self._processor, self._model.config, PROMPT, num_images=1)
        reply = generate(
            self._model, self._processor, formatted, [str(page.image)],
            max_tokens=self.max_tokens, temperature=0.0, verbose=False,
        )
        text = reply.text if hasattr(reply, "text") else str(reply)
        questions, sections = parse_reply(text, page.blanks)
        return questions, sections, text

    def close(self) -> None:
        import mlx.core as mx

        self._model = None
        self._processor = None
        mx.clear_cache()


def parse_reply(text: str, blanks: list[Blank]) -> tuple[list[Question], list[dict]]:
    """Turn one page's reply into questions, or raise if it is not usable."""
    try:
        body = text[text.index("{"): text.rindex("}") + 1]
        obj = json.loads(body)
    except (ValueError, json.JSONDecodeError) as exc:
        raise ValueError(f"model did not return usable JSON: {exc}") from exc

    sections = [
        {"id": s.get("id") or "", "title": s.get("title") or s.get("id") or ""}
        for s in obj.get("sections", []) if s.get("id")
    ]

    # Boxes the model says are one answer collapse into one question, keeping
    # every box so the geometry still describes the page.
    grouped: dict[str, Question] = {}
    order: list[str] = []
    for i, entry in enumerate(obj.get("fields", [])):
        label = (entry.get("label") or "").strip()
        if not label:
            continue
        n = entry.get("n")

        # A number past the last box is a question the model saw that stage 1
        # did not: W-9's signature and date lines are printed on the page but
        # are not AcroForm widgets, and the model numbers them 24, 25, 26.
        # Those are right, and dropping them cost four points of F1.
        #
        # Note this is NOT the same as inviting additions. Asked to add
        # anything it sees, the model stops grouping and the mean falls from
        # 0.79 to 0.70; left alone it adds a few and keeps grouping. So they
        # are accepted when offered and never solicited.
        in_range = isinstance(n, int) and 1 <= n <= len(blanks)
        blank = blanks[n - 1] if in_range else None
        key = entry.get("group") or (f"#{n}" if in_range else f"~{i}")
        if key in grouped:
            if blank is not None:
                grouped[key].blanks.append(blank)
            continue
        order.append(key)
        grouped[key] = Question(
            label=label,
            blanks=[blank] if blank is not None else [],
            section=entry.get("section_id"),
            field_kind=entry.get("field_kind") or "text_line",
            data_type=entry.get("expected_data_type") or "text",
            choices=entry.get("choices") or None,
        )

    return [grouped[k] for k in order], sections

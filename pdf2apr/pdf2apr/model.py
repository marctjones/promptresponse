"""The types that cross the boundary between stages.

Deliberately plain dataclasses. Every stage of the conversion takes one of
these and returns another, so a caller can run the stages separately, look at
what came out of each, and substitute their own.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

__all__ = ["Blank", "MarkedPage", "Question", "Report", "Stage", "Conversion"]


@dataclass(frozen=True, slots=True)
class Blank:
    """One place on the page a person is meant to write.

    Found deterministically, from the form's AcroForm widgets where it has
    them and from the lines the page draws where it does not. Coordinates are
    PDF user space: origin bottom-left, 72 to the inch.
    """

    id: str
    page: int
    left: float
    bottom: float
    right: float
    top: float
    data_type: str = "text"
    label: str | None = None
    """What the deterministic pass thought this asks, if anything."""

    @property
    def width(self) -> float:
        return self.right - self.left

    @property
    def height(self) -> float:
        return self.top - self.bottom


@dataclass(frozen=True, slots=True)
class MarkedPage:
    """A rendered page with every blank outlined and numbered."""

    page: int
    image: Path
    blanks: list[Blank]
    """In the order they were numbered on the image, so index i is box i+1."""


@dataclass(frozen=True, slots=True)
class Question:
    """One question, as it will appear in the APR document."""

    label: str
    blanks: list[Blank]
    """Every blank this one question is answered in. Usually one."""
    section: str | None = None
    field_kind: str = "text_line"
    data_type: str = "text"
    choices: list[str] | None = None

    @property
    def page(self) -> int:
        return self.blanks[0].page if self.blanks else 1


Stage = Literal["detect", "mark", "name", "assemble"]


@dataclass(slots=True)
class Report:
    """What each stage did, in terms a person can check."""

    lines: list[tuple[Stage, str]] = field(default_factory=list)

    def add(self, stage: Stage, message: str) -> None:
        self.lines.append((stage, message))

    def __str__(self) -> str:
        width = max((len(s) for s, _ in self.lines), default=0)
        return "\n".join(f"  {s:<{width}}  {m}" for s, m in self.lines)


@dataclass(slots=True)
class Conversion:
    """Everything one conversion produced."""

    source: Path
    blanks: list[Blank]
    questions: list[Question]
    document: dict[str, Any] | None
    report: Report
    marked_pages: list[MarkedPage] = field(default_factory=list)
    used_model: bool = False
    """Whether a model named the blanks. Recorded rather than inferred from the
    report: the skipped path reports on the naming stage too, so reading it back
    out of the log said yes when the model never loaded."""

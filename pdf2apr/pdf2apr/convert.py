"""The whole conversion, as one call."""
from __future__ import annotations

import tempfile
from dataclasses import dataclass
from pathlib import Path

from .assemble import build_document
from .detect import detect_blanks
from .mark import DEFAULT_DPI, mark_pages
from .model import Blank, Conversion, Question, Report
from .name import DEFAULT_MODEL, MlxNamer, Namer

__all__ = ["convert", "Options"]


@dataclass(slots=True)
class Options:
    """Everything a caller might reasonably want to change."""

    title: str | None = None
    """Document title. Defaults to the file's name."""

    use_model: bool = True
    """Ask a model to name the blanks. With this off you get the deterministic
    labels alone, which is faster and reproducible but measurably weaker: mean
    F1 0.58 against 0.79 on the benchmark forms."""

    model_id: str = DEFAULT_MODEL
    namer: Namer | None = None
    """Supply your own naming backend instead of loading MLX."""

    dpi: int = DEFAULT_DPI
    keep_marked_pages: Path | None = None
    """Where to keep the numbered page images. They are the best way to see
    what the model was actually shown."""


def convert(pdf: str | Path, options: Options | None = None) -> Conversion:
    """Convert a PDF form into an APR template."""
    pdf = Path(pdf)
    opts = options or Options()
    report = Report()

    blanks = detect_blanks(pdf)
    pages = sorted({b.page for b in blanks})
    report.add("detect", f"{len(blanks)} blank(s) across {len(pages)} page(s)")

    if not opts.use_model:
        questions = [
            Question(label=b.label or b.id, blanks=[b], data_type=b.data_type)
            for b in blanks
        ]
        named = sum(1 for b in blanks if b.label)
        report.add("name", f"skipped; {named} of {len(blanks)} named deterministically")
        document = build_document(opts.title or pdf.stem, questions, [])
        report.add("assemble", f"{len(document['sections'])} section(s), {len(questions)} prompt(s)")
        return Conversion(pdf, blanks, questions, document, report, used_model=False)

    with tempfile.TemporaryDirectory() as tmp:
        into = opts.keep_marked_pages or Path(tmp)
        marked = mark_pages(pdf, blanks, into, dpi=opts.dpi)
        report.add("mark", f"{len(marked)} page(s) rendered at {opts.dpi} DPI, every blank numbered")

        namer = opts.namer or MlxNamer(model_id=opts.model_id)
        questions: list[Question] = []
        sections: list[dict] = []
        failed: list[int] = []
        try:
            for page in marked:
                try:
                    page_questions, page_sections = namer.name_page(page)[:2]
                except ValueError:
                    failed.append(page.page)
                    # A page whose reply could not be read falls back to what the
                    # deterministic pass found, rather than losing its fields.
                    page_questions = [
                        Question(label=b.label or b.id, blanks=[b], data_type=b.data_type)
                        for b in page.blanks
                    ]
                    page_sections = []
                questions.extend(page_questions)
                for s in page_sections:
                    if s["id"] not in {x["id"] for x in sections}:
                        sections.append(s)
        finally:
            namer.close()

        collapsed = len(blanks) - sum(len(q.blanks) for q in questions)
        report.add(
            "name",
            f"{len(questions)} question(s) from {len(blanks)} blank(s)"
            + (f"; {abs(collapsed)} box(es) joined into shared questions" if collapsed else "")
            + (f"; page(s) {failed} fell back to deterministic labels" if failed else ""),
        )

        if opts.keep_marked_pages:
            marked_pages = list(marked)
        else:
            marked_pages = []

    document = build_document(opts.title or pdf.stem, questions, sections)
    report.add("assemble", f"{len(document['sections'])} section(s), {len(questions)} prompt(s)")
    return Conversion(pdf, blanks, questions, document, report, marked_pages, used_model=True)

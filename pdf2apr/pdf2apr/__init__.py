"""Convert a PDF form into an APR template.

    from pdf2apr import convert

    result = convert("w9.pdf")
    print(result.report)
    result.document          # the APR template, as a dict

Four stages, each usable on its own:

    detect_blanks(pdf)              -> [Blank]      where a person writes
    mark_pages(pdf, blanks, into)   -> [MarkedPage] those blanks, drawn and numbered
    Namer.name_page(page)           -> [Question]   what each one asks
    build_document(title, qs, secs) -> dict         the APR template

The split is the point. Finding blanks is geometry and a model cannot do it —
a blank is the absence of ink. Naming them is reading, and a model does it
better than any rule we measured. Each stage does the half it is good at.
"""
from .assemble import APR_VERSION, build_document
from .convert import Options, convert
from .detect import ConvertPdfNotFound, detect_blanks
from .mark import DEFAULT_DPI, mark_pages
from .model import Blank, Conversion, MarkedPage, Question, Report
from .name import DEFAULT_MODEL, PROMPT, MlxNamer, Namer, parse_reply

__version__ = "0.1.0"

__all__ = [
    "APR_VERSION", "Blank", "Conversion", "ConvertPdfNotFound", "DEFAULT_DPI",
    "DEFAULT_MODEL", "MarkedPage", "MlxNamer", "Namer", "Options", "PROMPT",
    "Question", "Report", "build_document", "convert", "detect_blanks",
    "mark_pages", "parse_reply", "__version__",
]

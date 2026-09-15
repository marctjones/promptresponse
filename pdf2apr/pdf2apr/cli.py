"""The `pdf2apr` command."""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

from . import __version__
from .convert import Options, convert
from .detect import ConvertPdfNotFound

EPILOG = """\
examples:
  pdf2apr w9.pdf                     convert, writing w9.aprt beside it
  pdf2apr w9.pdf -o out.aprt         choose the output file
  pdf2apr w9.pdf --no-model          deterministic only: instant, weaker labels
  pdf2apr w9.pdf --show-pages ./look keep the numbered page images to inspect
  pdf2apr w9.pdf --fields fields.json  also write where each blank sits

how it works:
  1. detect    reads the form's own fields, or the lines the page draws
  2. mark      renders each page with every blank outlined and numbered
  3. name      a local vision model says what each numbered blank asks for
  4. assemble  writes the APR template

  Stage 1 is geometry, which no model can do: a blank is the absence of ink.
  Stage 3 is reading, which a model does better than any rule. The model is
  never asked to find fields, only to name the ones already found.
"""


def _parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="pdf2apr",
        description="Convert a PDF form into an APR template (.aprt).",
        epilog=EPILOG,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    p.add_argument("pdf", type=Path, help="the form to convert")
    p.add_argument("-o", "--output", type=Path, metavar="FILE",
                   help="where to write the .aprt (default: beside the PDF)")
    p.add_argument("-t", "--title", metavar="TEXT",
                   help="document title (default: the file's name)")
    p.add_argument("--no-model", action="store_true",
                   help="skip the model; deterministic labels only")
    p.add_argument("--model", metavar="ID", default=None,
                   help="model to name the blanks (default: Qwen3-VL-4B)")
    p.add_argument("--show-pages", type=Path, metavar="DIR",
                   help="keep the numbered page images the model was shown")
    p.add_argument("--fields", type=Path, metavar="FILE",
                   help="also write where each blank sits, as JSON")
    p.add_argument("--dpi", type=int, default=None, metavar="N",
                   help="render resolution (default: 150)")
    p.add_argument("-q", "--quiet", action="store_true", help="only report problems")
    p.add_argument("--version", action="version", version=f"pdf2apr {__version__}")
    return p


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)

    if not args.pdf.exists():
        print(f"pdf2apr: no such file: {args.pdf}", file=sys.stderr)
        return 2

    options = Options(
        title=args.title,
        use_model=not args.no_model,
        keep_marked_pages=args.show_pages,
    )
    if args.model:
        options.model_id = args.model
    if args.dpi:
        options.dpi = args.dpi

    if not args.quiet and options.use_model:
        print(f"Reading {args.pdf.name} … the model runs locally and may take a minute.",
              file=sys.stderr)

    started = time.time()
    try:
        result = convert(args.pdf, options)
    except ConvertPdfNotFound as exc:
        print(f"pdf2apr: {exc}", file=sys.stderr)
        return 3
    except FileNotFoundError as exc:
        missing = getattr(exc, "filename", None) or exc
        print(f"pdf2apr: a program this needs is not installed: {missing}", file=sys.stderr)
        print("  pdftoppm comes from poppler (brew install poppler).", file=sys.stderr)
        return 3
    except ImportError as exc:
        print(f"pdf2apr: {exc}", file=sys.stderr)
        print("  The model needs mlx-vlm: pip install 'pdf2apr[model]'.", file=sys.stderr)
        print("  Or run with --no-model for deterministic labels only.", file=sys.stderr)
        return 3
    except RuntimeError as exc:
        print(f"pdf2apr: {exc}", file=sys.stderr)
        return 1

    if result.document is None:
        print(f"pdf2apr: found nothing fillable in {args.pdf.name}", file=sys.stderr)
        return 1

    destination = args.output or args.pdf.with_suffix(".aprt")
    destination.write_text(json.dumps(result.document, indent=2) + "\n")

    if args.fields:
        args.fields.write_text(json.dumps([
            {"id": b.id, "page": b.page, "left": b.left, "bottom": b.bottom,
             "right": b.right, "top": b.top, "dataType": b.data_type}
            for b in result.blanks
        ], indent=2) + "\n")

    if not args.quiet:
        print(result.report, file=sys.stderr)
        print(f"  {'':<8}  took {time.time() - started:.0f}s", file=sys.stderr)
        if args.show_pages:
            print(f"  {'':<8}  page images in {args.show_pages}", file=sys.stderr)

    questions = len(result.questions)
    print(f"Wrote {destination} — {questions} question"
          f"{'' if questions == 1 else 's'}"
          f" from {len(result.blanks)} field{'' if len(result.blanks) == 1 else 's'}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

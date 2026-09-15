#!/usr/bin/env python3
"""Render every page of a PDF to a PNG using poppler's pdftoppm.

Usage: render_pdf.py <form_id> [--dpi 200]
Writes results/pages/<form_id>/page-1.png, page-2.png, ...
"""
import argparse
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
CORPUS_DIR = HERE / "corpus"
PAGES_DIR = HERE / "results" / "pages"


def render(form_id: str, dpi: int = 200) -> list[Path]:
    src = CORPUS_DIR / f"{form_id}.pdf"
    if not src.exists():
        raise FileNotFoundError(f"no corpus PDF for {form_id!r} at {src}")
    out_dir = PAGES_DIR / form_id
    out_dir.mkdir(parents=True, exist_ok=True)
    prefix = out_dir / "page"
    subprocess.run(
        ["pdftoppm", "-png", "-r", str(dpi), str(src), str(prefix)],
        check=True,
    )
    pages = sorted(out_dir.glob("page-*.png"))
    if not pages:
        # pdftoppm omits the "-N" suffix for single-page PDFs
        pages = sorted(out_dir.glob("page*.png"))
    return pages


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("form_id")
    ap.add_argument("--dpi", type=int, default=200)
    args = ap.parse_args()
    pages = render(args.form_id, args.dpi)
    for p in pages:
        print(p)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

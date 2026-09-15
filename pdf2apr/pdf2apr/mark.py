"""Stage 2 — draw the blanks on the page.

The model is shown where the fields are rather than told, because a picture of
a numbered box beside a caption is a much easier thing for a vision model to
read than a list of coordinates it has to relate to what it sees.
"""
from __future__ import annotations

import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from .model import Blank, MarkedPage

__all__ = ["mark_pages", "DEFAULT_DPI"]

DEFAULT_DPI = 150
"""Enough to read 7pt form text; higher costs time for no measured gain."""

_OUTLINE = (220, 0, 0)
_LABEL_TEXT = (255, 255, 255)


def _font(size: int = 22) -> ImageFont.ImageFont:
    for candidate in (
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ):
        try:
            return ImageFont.truetype(candidate, size)
        except OSError:
            continue
    return ImageFont.load_default()


def mark_pages(pdf: Path, blanks: list[Blank], into: Path,
               dpi: int = DEFAULT_DPI) -> list[MarkedPage]:
    """Render each page that has blanks, with every blank outlined and numbered."""
    into.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(
            ["pdftoppm", "-png", "-r", str(dpi), str(pdf), str(Path(tmp) / "p")],
            check=True, capture_output=True,
        )
        rendered = sorted(Path(tmp).glob("p-*.png"))
        pages: list[MarkedPage] = []
        scale = dpi / 72.0
        font = _font()

        for png in rendered:
            page_no = int(png.stem.rsplit("-", 1)[-1])
            mine = [b for b in blanks if b.page == page_no]
            if not mine:
                continue
            # Reading order, so the numbering runs the way the form does.
            mine.sort(key=lambda b: (-b.top, b.left))

            image = Image.open(png).convert("RGB")
            height = image.size[1]
            draw = ImageDraw.Draw(image)

            for n, blank in enumerate(mine, start=1):
                x0, x1 = blank.left * scale, blank.right * scale
                y0, y1 = height - blank.top * scale, height - blank.bottom * scale
                draw.rectangle([x0, y0, x1, y1], outline=_OUTLINE, width=3)

                # Inside the box where it fits. A blank is empty by definition,
                # so nothing is hidden; outside, the number lands on the caption
                # the model needs to read.
                tag = 26 if n < 10 else (34 if n < 100 else 42)
                if (x1 - x0) > tag + 10 and (y1 - y0) > 24:
                    tx, ty = x0 + 2, y0 + 2
                else:
                    tx, ty = max(0, x0 - tag - 2), max(0, y0 - 2)
                draw.rectangle([tx, ty, tx + tag, ty + 22], fill=_OUTLINE)
                draw.text((tx + 4, ty + 1), str(n), fill=_LABEL_TEXT, font=font)

            out = into / f"page-{page_no}.png"
            image.save(out)
            pages.append(MarkedPage(page=page_no, image=out, blanks=mine))

    return pages

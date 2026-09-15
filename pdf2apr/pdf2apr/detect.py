"""Stage 1 — find the blanks, deterministically.

Delegates to the `convert-pdf` tool, which reads a form's AcroForm widgets
where it has them and the lines the page draws where it does not. That is the
one part of this no model can do: a blank is the absence of ink, and a model
that reads a page has nothing to read there.
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path

from .model import Blank

__all__ = ["detect_blanks", "ConvertPdfNotFound"]

_ENV_VAR = "PDF2APR_CONVERT_PDF"


class ConvertPdfNotFound(RuntimeError):
    """The `convert-pdf` tool could not be located."""


def _tool() -> list[str]:
    override = os.environ.get(_ENV_VAR)
    if override:
        return [override]
    on_path = shutil.which("convert-pdf")
    if on_path:
        return [on_path]

    # Running from a checkout: fall back to the project, so the tool works
    # before anything has been packaged or installed.
    here = Path(__file__).resolve()
    for parent in here.parents:
        project = parent / "src" / "PromptResponse.Conversion.Pdf.Tool"
        if project.is_dir():
            dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet" / "dotnet")
            return [dotnet, "run", "--project", str(project), "--"]

    raise ConvertPdfNotFound(
        "convert-pdf was not found. Put it on PATH, or set "
        f"{_ENV_VAR} to its location."
    )


def detect_blanks(pdf: Path) -> list[Blank]:
    """Every place on the form a person is meant to write."""
    with tempfile.TemporaryDirectory() as tmp:
        fields = Path(tmp) / "fields.json"
        aprt = Path(tmp) / "out.aprt"
        proc = subprocess.run(
            [*_tool(), str(pdf), f"--output={aprt}", f"--fields={fields}", "--quiet"],
            capture_output=True, text=True,
        )
        if not fields.exists():
            detail = (proc.stderr or proc.stdout or "").strip()
            raise RuntimeError(
                f"convert-pdf found no fields in {pdf.name}"
                + (f": {detail.splitlines()[-1]}" if detail else "")
            )
        rows = json.loads(fields.read_text())

    return [
        Blank(
            id=r["id"], page=r["page"],
            left=r["left"], bottom=r["bottom"], right=r["right"], top=r["top"],
            data_type=r.get("dataType") or "text",
            label=r.get("label"),
        )
        for r in rows
    ]

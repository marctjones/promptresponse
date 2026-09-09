#!/usr/bin/env python3
"""Derive non-AcroForm fixtures from the corpus's fillable forms.

Why this exists
---------------
Agencies have almost entirely moved to fillable PDFs: all five federal forms in
the corpus carry an AcroForm, and the only non-fillable forms that could be
found in the wild are eight municipal ones from a single Connecticut town. That
left two gaps the converter has to handle but had no fixture for -- a federal
form with no AcroForm, and a page with no text layer at all.

Both are produced here by putting a fillable form through the same
transformations the world puts them through: "printing" it to a new PDF, and
scanning a printout. All five federal forms are now flattened, so every one
exists in both a fillable and a non-fillable version.

The derived-oracle trick
------------------------
A derived fixture is worth more than a form found in the wild, because its
source's AcroForm is a complete, mechanical answer key for it. `fed-w9-flat`
has no fields of its own, but every field `fed-w9` declares -- name, type,
options, and `Rect` geometry -- is exactly what a converter reading the flat
version *should* recover. That is ground truth with no human and no model in
the loop, for the converter's actual target case. `derivedFrom` in the manifest
is what makes the link, and `PdfWidgetManifest.Extract` on the source is the
key.

The catch to keep in view: these are derived, not observed. They inherit their
source's layout conventions, and a real scan has skew, speckle, and JPEG noise
that `rasterize` does not reproduce. They widen coverage and supply answer
keys; they are not evidence that the converter handles forms in the wild.

Tool choice
-----------
`pdftocairo -pdf`, which renders the page the way a print-to-PDF driver does.

Ghostscript with `-dPreserveAnnots=false` was measured against it and, on this
corpus, produces an equivalent result. Rendering each of the eleven fillable
forms with and without annotations shows that nine draw their field boxes in
the page content stream, so no annotation handling is involved for them at
all. Only two have widgets that draw anything:

- `ct-w4` -- the JavaScript "Print Form" and "Clear Fields" push-buttons plus
  three checkbox glyphs. Both tools keep the checkboxes and drop the buttons,
  which is the right outcome: a printed form has no buttons. That loss is why
  ct-w4-flat retains 99.75% of its source's words rather than 100%.
- `fed-i9` -- a single checkbox on page 1, which both tools bake in
  identically (mean ink 158.7 in that region for each, against 255.0 blank).

Cairo is used because it is the smaller dependency and preserves text and page
geometry exactly (`verify_flatten` checks both), not because Ghostscript was
found wanting. An earlier version of this file claimed gs erased ct-w4's field
boxes; that was wrong, and the amplified diffs above are what corrected it.
"""
import argparse
import glob
import json
import re
import shutil
import subprocess
import sys
import tempfile
import unicodedata
from collections import Counter
from pathlib import Path

HERE = Path(__file__).parent
MANIFEST = HERE / "corpus_manifest.json"
CORPUS_DIR = HERE / "corpus"

# Scanner-typical settings. 150 DPI bitonal is what a government office
# document feeder produces, and CCITT G4 keeps a 2-page fixture near the size
# of the digital original (89 KB vs 81 KB for fed-8822) rather than the ~600 KB
# a grayscale encoding would commit to the repository.
RASTER_DPI = 150


def flatten(source: Path, dest: Path) -> None:
    """Print a fillable PDF to a new PDF: no widgets, text layer intact."""
    subprocess.run(
        ["pdftocairo", "-pdf", str(source), str(dest)],
        check=True, capture_output=True,
    )


def rasterize(source: Path, dest: Path) -> None:
    """Scan a printout: every page becomes a bitonal image, no text at all."""
    from PIL import Image

    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(
            ["pdftoppm", "-gray", "-r", str(RASTER_DPI), "-png", str(source), f"{tmp}/page"],
            check=True, capture_output=True,
        )
        pages = [Image.open(p).convert("1") for p in sorted(glob.glob(f"{tmp}/page-*.png"))]
        if not pages:
            raise RuntimeError(f"{source.name}: pdftoppm produced no pages")
        pages[0].save(
            dest, save_all=True, append_images=pages[1:], resolution=float(RASTER_DPI)
        )


SYNTHESIZERS = {"flatten": flatten, "rasterize": rasterize}

def pdf_info(pdf: Path) -> str:
    return subprocess.run(
        ["pdfinfo", "-l", "9999", str(pdf)], capture_output=True, text=True, check=True
    ).stdout


def page_sizes(info: str) -> list[str]:
    per_page = re.findall(r"Page +\d+ size: +([\d.]+ x [\d.]+)", info)
    return per_page or re.findall(r"Page size: +([\d.]+ x [\d.]+)", info)


def has_acroform(info: str) -> bool:
    """Whether a PDF declares an AcroForm, read from a parsed document.

    Deliberately not a search for b"/AcroForm" in the raw bytes. PDF stores
    objects in compressed object streams (/ObjStm), where a perfectly live
    AcroForm is invisible to a byte scan -- a fixture rebuilt with
    `qpdf --object-streams=generate` greps clean while pdfinfo still reports
    `Form: AcroForm`. A raw-byte check here would be a gate that passes
    whatever it is shown.
    """
    return bool(re.search(r"^Form: +(?!none\b)\S+", info, re.MULTILINE))


# A flattened form may legitimately shed a few words: the JavaScript "Print
# Form" and "Clear Fields" push-buttons are widget labels, and a printed form
# is supposed to lose them. Measured across the five flattened fixtures, four
# retain 100% of their source's words and ct-w4 retains 99.75% -- losing
# exactly {print, form, clear, fields}. Anything below this bar is real content
# loss, not button chrome, and the check names the missing words so the
# difference is never a judgement call.
MIN_WORD_RETENTION = 0.99

_DASHES = str.maketrans({c: "-" for c in "‐‑‒–—"})


def word_counts(pdf: Path) -> Counter:
    """Words in a PDF, as a multiset.

    Compared as a multiset rather than as a string because printing to PDF
    legitimately re-orders text runs -- cairo emits fed-8822's page 2 in a
    different sequence with identical content -- and normalizes typography,
    turning CT-W4's U+2011 non-breaking hyphens into ASCII. Neither changes
    what the form says, and neither should fail a fixture.
    """
    out = subprocess.run(
        ["pdftotext", "-layout", str(pdf), "-"], capture_output=True, text=True, check=True
    ).stdout
    normalized = unicodedata.normalize("NFKC", out).translate(_DASHES)
    return Counter(re.findall(r"[a-z0-9]+", normalized.lower()))


def verify_flatten(source: Path, dest: Path) -> str | None:
    """Confirm a flattened fixture is its source minus the widgets, and nothing else.

    Checked mechanically rather than by eye, because the failure this guards
    against is invisible: a flattened form that quietly lost text, gained a page,
    or kept its AcroForm still detects as text-layer-only and still passes every
    test downstream, while no longer being the document it claims to be.

    Deliberately *not* checked: how closely the flattened page resembles a render
    of the original. Whole-page pixel drift is dominated by font re-encoding
    rather than by content -- measured on ct-w4, a Ghostscript flatten drifts
    *less* (0.471) than the cairo output (0.551) despite the two being
    equivalent in what they keep -- so the metric ranks outputs by how they
    antialias text. Words and page geometry are exact, and are what matter.
    """
    source_info, dest_info = pdf_info(source), pdf_info(dest)

    before_sizes, after_sizes = page_sizes(source_info), page_sizes(dest_info)
    if len(before_sizes) != len(after_sizes):
        return f"page count changed: {len(before_sizes)} -> {len(after_sizes)}"
    for i, (a, b) in enumerate(zip(before_sizes, after_sizes), start=1):
        if a != b:
            return f"page {i} size changed: {a} -> {b} (widget Rects would no longer line up)"

    before, after = word_counts(source), word_counts(dest)
    total = sum(before.values())
    if total:
        retained = sum((before & after).values()) / total
        if retained < MIN_WORD_RETENTION:
            lost = before - after
            worst = ", ".join(f"{w}x{n}" if n > 1 else w for w, n in lost.most_common(12))
            return (
                f"only {retained:.2%} of the source's {total} words survived "
                f"(floor {MIN_WORD_RETENTION:.0%}); missing: {worst}"
            )

    if has_acroform(dest_info):
        return "output still declares an AcroForm; it would not be a non-fillable fixture"

    return None


def verify_rasterize(source: Path, dest: Path) -> str | None:
    """Confirm a rasterized fixture really is pixels and nothing else."""
    source_info, dest_info = pdf_info(source), pdf_info(dest)

    before, after = len(page_sizes(source_info)), len(page_sizes(dest_info))
    if before != after:
        return f"page count changed: {before} -> {after}"

    if has_acroform(dest_info):
        return "output still declares an AcroForm; scanning a printout cannot preserve one"

    # The whole point of this fixture is that there is nothing to read but pixels.
    # The bar matches PdfSourceDetector.MinCharactersForTextLayer.
    characters = sum(len(w) * n for w, n in word_counts(dest).items())
    if characters >= 25:
        return f"output still yields {characters} characters of text; it is not image-only"

    return None


VERIFIERS = {"flatten": verify_flatten, "rasterize": verify_rasterize}


def derived_forms(manifest: dict) -> list[dict]:
    return [f for f in manifest["forms"] if "synthesis" in f]


def synthesize(form: dict, force: bool, verify_only: bool = False) -> str:
    dest = CORPUS_DIR / f"{form['id']}.pdf"
    source = CORPUS_DIR / f"{form['derivedFrom']}.pdf"

    if verify_only:
        verifier = VERIFIERS.get(form["synthesis"])
        if verifier is None:
            return f"  n/a        {form['id']} (no verifier for {form['synthesis']})"
        if not dest.exists():
            raise FileNotFoundError(f"{form['id']} has not been built")
        problem = verifier(source, dest)
        if problem:
            raise RuntimeError(problem)
        return f"  verified   {form['id']}"

    if dest.exists() and dest.stat().st_size > 0 and not force:
        return f"  skip       {form['id']} (already built)"

    if not source.exists():
        raise FileNotFoundError(
            f"{form['id']} derives from {form['derivedFrom']}, which is not in "
            f"{CORPUS_DIR}. Run fetch_corpus.py first."
        )

    synthesizer = SYNTHESIZERS.get(form["synthesis"])
    if synthesizer is None:
        raise ValueError(
            f"{form['id']}: unknown synthesis {form['synthesis']!r} "
            f"(known: {', '.join(sorted(SYNTHESIZERS))})"
        )

    synthesizer(source, dest)

    verifier = VERIFIERS.get(form["synthesis"])
    if verifier and (problem := verifier(source, dest)):
        dest.unlink(missing_ok=True)
        raise RuntimeError(problem)

    return (
        f"  built      {form['id']}  ({dest.stat().st_size:,} bytes)  "
        f"<- {form['synthesis']}({form['derivedFrom']})"
    )


def check_tools() -> list[str]:
    missing = [t for t in ("pdftocairo", "pdftoppm") if shutil.which(t) is None]
    if missing:
        return [
            f"missing required tool(s): {', '.join(missing)}. "
            f"Install poppler (brew install poppler)."
        ]
    try:
        import PIL  # noqa: F401
    except ImportError:
        return ["missing Pillow. Install with: pip install Pillow"]
    return []


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--force", action="store_true", help="rebuild fixtures that already exist")
    ap.add_argument("--forms", nargs="*", help="subset of derived form ids to build")
    ap.add_argument(
        "--verify", action="store_true",
        help="check the committed fixtures against their sources without rebuilding",
    )
    args = ap.parse_args()

    for problem in check_tools():
        print(problem, file=sys.stderr)
        return 2

    manifest = json.loads(MANIFEST.read_text())
    forms = derived_forms(manifest)
    if args.forms:
        forms = [f for f in forms if f["id"] in args.forms]
    if not forms:
        print("no derived fixtures to build")
        return 0

    CORPUS_DIR.mkdir(exist_ok=True)
    failures = []
    for form in forms:
        try:
            print(synthesize(form, args.force, verify_only=args.verify))
        except Exception as exc:  # noqa: BLE001 - report and continue
            failures.append(form["id"])
            print(f"  FAILED     {form['id']}: {exc}", file=sys.stderr)

    if failures:
        print(f"\n{len(failures)} fixture(s) failed: {', '.join(failures)}", file=sys.stderr)
        return 1
    verb = "verified" if args.verify else "present in"
    print(f"\nAll {len(forms)} derived fixture(s) {verb} {'' if args.verify else CORPUS_DIR}".rstrip())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

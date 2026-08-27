# Importing existing forms into APR

You have a form already — a PDF, a Word/OpenDocument file, a scan, or a photo —
and you want it as a PromptResponse template (`.aprt`) you can fill, export, and
share. There are **two paths**; pick by what your source actually is.

| Your source | Use | Why |
|-------------|-----|-----|
| A **fillable PDF** whose fields have tooltips | **`apr import`** (code) | Deterministic, instant, exact field extraction. |
| A **flat / printed / scanned PDF**, an **image** (PNG/JPG), **Word**, or **OpenDocument** | **the `document-to-apr` skill** (AI) | No machine-readable fields to extract — an agent reads the form like a person. |
| A fillable PDF with **no tooltips** (e.g. IRS Form 990) | the **skill** | `apr import` works but produces cryptic labels (`f1_1[0]`); the skill recovers real labels. |

Not sure? Run `apr import` first — it tells you if there are no fields, and you
can eyeball whether the labels came out human-readable. If not, use the skill.

---

## Path 1 — `apr import` (fillable PDFs)

The deterministic importer reads a PDF's AcroForm fields and writes a template.

```bash
# CLI
apr import form.pdf                       # -> form.aprt
apr import form.pdf --output=intake.aprt  # explicit output
apr import form.pdf --title="Intake Form" # set the document title
apr import form.pdf --report              # print a quality score + per-field flags
```

### It tells you how good the import is

Quality hinges on whether the PDF's fields carry tooltips, which you can't know
until you try — so the importer **scores itself** (no AI) and recommends what to
do. Every run prints a one-line verdict; `--report` adds the breakdown and
per-field flags:

```
Quality: Good (84/100, B). 99% of 6197 fields have human-readable labels — use directly.   # SF-86
Quality: Poor (0/100, F). Only 0% of 1075 fields have readable labels … use the skill.      # IRS 990
```

The score comes from tooltip coverage, the ratio of cryptic (raw-field-name)
labels, and duplicate labels; the recommendation is **use directly** (≥70),
**review** (40–69), or **use the skill** (<40). In the desktop app a poor import
asks before opening. Don't trust "it validated" as a proxy for quality — a 100%
valid import can still be 100% useless labels.

What it does:
- Each form field becomes a prompt; fields are grouped **one section per page**.
- Field kind → data-type hint: text, checkbox → `boolean`, dropdown → `suggestedValues`.
- The field **tooltip** (`/TU`) becomes the label (the most valuable thing a form
  carries); without one it falls back to the raw field name.
- The result is a valid template with every `response` blank.

If the PDF has no AcroForm (flat/scanned), the command stops with a clear message
pointing you to the skill.

**Desktop:** **File → Import from PDF…** does the same thing with a file picker,
then opens the result as a new untitled template (use **Save As** to keep it).

**Library** (programmatic):

```csharp
using PromptResponse.Rendering.Pdf;
var doc = new PdfFormImporter().Import("form.pdf", title: "Intake Form");
```

### Known limits

- Radio-button groups currently import as a generic `boolean` — review and change
  to a dropdown where appropriate.
- Sectioning is per-PDF-page, not the form's own Part/Section structure.
- Long tooltips become long labels.

The desktop importer shows a review dialog when quality is weak: it includes the
score, recommendation, flag counts, and sample fields with cryptic labels,
duplicate labels, or likely radio-group ambiguity before you decide whether to
open the imported template anyway.

---

## Path 2 — the `document-to-apr` skill (everything else)

A portable AI skill that turns a PDF / Word / OpenDocument / **image** of a form
into a valid `.aprt`. It works in any agent that can see the document — Claude
Code, Claude in the workspace, Gemini CLI, Codex — because it's just instructions
plus the format spec.

**In Claude Code** (this repo): the skill is auto-discovered. Just ask —
*"turn `application.pdf` into an APR form"* — or invoke `/document-to-apr`.

**In another agent:**
1. Get the skill bundle — download `document-to-apr-skill-<version>.zip` from a
   [GitHub Release](https://github.com/marctjones/promptresponse/releases), or
   copy the `.claude/skills/document-to-apr/` folder from the repo.
2. Point your agent at `SKILL.md` (and let it read the two `reference/` files).
3. Give it the form and ask it to produce a `.aprt`.

The skill is versioned independently of the app (see `version:` in its
`SKILL.md`); its behavior spec lives in
[`.claude/skills/document-to-apr/SKILL.md`](../.claude/skills/document-to-apr/SKILL.md).

---

## Best of both — the hybrid workflow

The two paths aren't rivals; chained, they beat either alone. The importer
guarantees **completeness and exact field identity** (every field, with its real
PDF name as the prompt id — the hook for pushing values back into the original
PDF). The skill is best at **meaning and structure** (real labels, sections,
types, choices). So for an important fillable PDF, especially a tooltip-less one:

1. `apr import form.pdf -o form.aprt` — get the complete, exactly-identified
   skeleton (and its quality score).
2. Hand **both** `form.aprt` and `form.pdf` to the `document-to-apr` skill and
   ask it to **enrich the skeleton**: it fixes the cryptic labels, regroups the
   per-page sections into the form's real structure, and sets types — while
   **keeping every prompt id** so values still round-trip to the PDF.

This gives you the importer's completeness + exact ids *and* the skill's
human-quality labels and structure. The skill has an explicit "enrich an imported
skeleton" mode for exactly this (see its `SKILL.md`).

---

## After importing

Always validate, then use the template:

```bash
apr validate form.aprt                          # structural check
apr export form.aprt --format=html --fillable   # interactive web form
apr export form.aprt --format=pdf  --fillable   # fillable PDF
```

Or open `form.aprt` in the desktop app to refine labels, types, sections, and
help text before sharing it.

---

## Real-world examples

See `tests/Fixtures/import-corpus/` for both paths run against two real federal
forms (IRS Form 990, SF-86), with a findings write-up — a concrete look at when
each path shines. Tracked in
[issue #64](https://github.com/marctjones/promptresponse/issues/64).

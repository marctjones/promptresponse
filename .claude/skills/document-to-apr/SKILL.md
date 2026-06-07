---
name: document-to-apr
description: >-
  Convert an existing form into a PromptResponse APR template (.aprt). Use when
  the user wants to import, recreate, or "turn into a fillable form" a PDF, Word
  (.docx), OpenDocument (.odt/.odp), or an image (PNG/JPG/GIF/screenshot/scan) of
  a paper or printed form. Produces a valid .aprt JSON file that opens in the
  PromptResponse editor and can be exported to a fillable PDF or web form.
---

# Document → APR

Turn any form — a PDF, a Word/OpenDocument file, or just a **picture of a paper
form** — into a PromptResponse **APR template** (`.aprt`). APR is a small,
layout-free JSON format: a document is sections of prompts (questions). The
editor and the `apr` CLI then render it to a fillable PDF, an interactive web
form, plain text, etc.

You (the AI agent) are the importer. You read the source however you read any
file or image, understand the form's structure, and emit a valid `.aprt`. This
works in any agent that can see the document — Claude Code, Claude in the
workspace, Gemini CLI, Codex — because the skill is just instructions + the
format spec; nothing here is tool-specific.

## Why an agent and not a parser

Most real forms are **not** machine-readable: a PDF is usually flat printed text
(no form fields), a scan or photo is pixels, a Word form's fields are buried in
XML. A mechanical parser fails on all of these. You can *look at the form* the
way a person does — read the labels, see the checkboxes, group the sections — and
reconstruct it faithfully. That is the whole point of doing this as a skill.

> **Shortcut for *fillable* PDFs:** if the PDF already has real AcroForm fields
> *with tooltips*, the deterministic `apr import <file.pdf>` command extracts them
> directly — try it first for those. But it only sees real widgets, and many
> government PDFs ship without tooltips (e.g. IRS Form 990 → fields named
> `f1_1[0]`), so its labels can be useless. When `apr import` produces cryptic
> labels, or the PDF is flat/scanned/an image/Word, fall back to this skill.

## Procedure

1. **Take in the source.** Open/look at the file the user pointed you to (PDF,
   .docx, .odt/.odp, or an image). If it's an image or a flat PDF, read it
   visually. If it's a zipped Office/OpenDocument file and you can't open it
   directly, unzip it and read `word/document.xml` (DOCX) or `content.xml`
   (ODT/ODP) as text — but you usually don't need to: read what the form *says*.

2. **Identify the structure**, top to bottom:
   - **Sections** — visual groupings, headed areas, "Part I / Part II", boxes.
     Every field belongs to a section. If the form has no groupings, make one
     section like "Form".
   - **Fields (prompts)** — each blank, checkbox, line, or labelled space a
     person fills in. Capture its **label** (the visible question text), and:
     - the expected **data type** (see the vocabulary below),
     - any **help text** / instructions printed near it,
     - a **placeholder** example if one is shown,
     - **choices** if it's "check one of …" / a list of options,
     - whether it looks **required** (asterisk, "required", bold "must").
   - **Tables / grids** — a repeating row/column area (see "Tables" below).

3. **Map to APR.** Build the JSON per `reference/apr-format.md`. Follow every
   rule in "Hard rules" below or the file won't validate.

4. **Write the file** as `<sensible-name>.aprt` (kebab-case). It's a template, so
   every `response` is `""`.

5. **Validate.** Run the CLI from the repo root:
   ```bash
   dotnet run --project src/PromptResponse.Cli -- validate <file>.aprt
   ```
   (or `apr validate <file>.aprt` if the `apr` tool is installed). Fix any
   reported errors and re-validate until it passes.

6. **Hand off.** Tell the user the file is ready, summarise what you captured
   (sections / field count / any tables), and note anything you were unsure about
   (ambiguous labels, unreadable areas) so they can correct it. Mention they can
   open it in the editor or export it: `apr export <file>.aprt --format=html
   --fillable` (web form) or `--format=pdf --fillable` (PDF form).

## Hard rules (these make or break validation)

- `version` is `"1.0"`; `documentType` is `"template"`.
- `metadata.title` is **required** and non-empty.
- At least one section; **every section needs a non-empty `title`** and a unique
  non-empty `id`.
- **Every prompt needs a non-empty `label`** and a unique non-empty `id`.
- **All ids are unique across the whole document.** Use stable, descriptive,
  kebab/snake ids (e.g. `applicant-name`, not `q1`).
- **Every `response` is a string** — always `""` in a template. There are no
  typed values; a number field still stores `""`.
- Type hints are **advisory only** — they guide the UI, they never restrict input.
- **No layout.** Do not invent fonts, colours, positions, page numbers. APR is
  content only.
- **Accessibility is required, not optional:** give every prompt a real, unique,
  human label (never a placeholder as the only label, never duplicate labels);
  give every section a title; add `helpText` whenever the form prints guidance.

## Data-type hint vocabulary

Set `hints.expectedDataType` to the closest of: `text`, `multiline`, `email`,
`phone`, `url`, `number`, `currency`, `date`, `time`, `datetime`, `boolean`.
- Checkbox / yes-no → `boolean`.
- A field offering a fixed set of options → keep the type (often `text`) and add
  `hints.suggestedValues: ["…","…"]` (this becomes a dropdown).
- A large free-text area → `multiline`.

## Tables

If the form has a grid where columns are fields and rows repeat (e.g. "Income by
year", line items, a schedule), model it as a **table section** — see the table
example in `reference/examples.md`. Key points:
- The section carries a `tableLayout` (`columns`, plus either `fixedRows` for a
  known set of rows, or `dynamicRows` for user-added rows).
- For **fixed** rows, also create one child section per row whose prompts are the
  cells, with ids `"{rowId}.{columnId}"`. These cells become individually
  fillable in the PDF/web exports.
- For **dynamic** rows (unbounded line items), define `dynamicRows` and no child
  sections.

## Computed & conditional fields (optional, advanced)

If the form has obvious math ("Total = sum of lines") or conditional fields
("if yes, explain"), you may add expression hints — but only when confident:
- `exprValue` — a computed value (read-only), e.g. `double(qty) * double(price)`.
- `exprHidden` — hide unless truthy, e.g. `is_gift != 'true'`.
- `exprExpected` — mark required when truthy.
These reference other prompts by id. **Any id used in an expression must be
identifier-safe — letters, digits, underscores only, no hyphens** (`unit_price`,
not `unit-price`, which parses as subtraction). See the expression example in
`reference/examples.md`. When unsure, leave them out — a plain field is always
correct.

## References

- `reference/apr-format.md` — the complete, self-contained format spec (the
  ground truth; read it before emitting JSON).
- `reference/examples.md` — worked input → output examples (simple form, a table,
  a computed/conditional field).

When this skill lives in the PromptResponse repo, `docs/FILE_FORMAT.md` and the
`examples/*.aprt` files are additional ground truth. When you've copied this
skill into another agent, the two `reference/` files are self-sufficient.

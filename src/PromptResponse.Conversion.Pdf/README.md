# PDF → APR conversion pipeline

Deliberately segregated. This library and the `convert-pdf` tool are **not
referenced by `apr`, the Desktop app, or `PromptResponse.Rendering.Pdf`** — they
read from Rendering.Pdf and change nothing there, so an unfinished converter
cannot regress `apr import`. Folding it in is a decision to take once the phases
work, not a side effect of starting them.

## The phases, and what each one does

| # | phase | what it does | status |
| --- | --- | --- | --- |
| 1 | `detect-sources` | Decides what the PDF offers: AcroForm, text layer, or neither. Everything downstream routes on this. | **built** |
| 2 | `extract-text` | Reads the printed text with exact page coordinates — no OCR, no model. | **built** |
| 3 | `discover-acroform` | Takes the fields the PDF already declares, with types and positions. | **built** |
| 4 | `discover-text-layer` | Finds blanks and checkboxes on a form that declares no fields, from drawn vector primitives. | #424 |
| 5 | `classify-spans` | Separates headings and questions from instructions and page furniture. | #422 |
| 6 | `recover-labels` | Turns a cryptic field name into the question printed beside it. | #426 |
| 7 | `assemble` | Arranges what was found into a valid APR template. Invents nothing. | **built** |
| 8 | `model-touch-up` | Asks a small local model only about fields determinism could not resolve. | #423 |

Phases run in that order for a reason. Detection routes; text extraction supplies
the evidence every later phase reasons over; the AcroForm is taken first because
nothing inferred can improve on what the PDF states outright; and the model runs
**last, on the residue only**, so the deterministic phases are as good as they
can be before anything is asked of a model.

## Why the report lists phases that did nothing

An end-to-end score says a conversion was bad without saying *which step* was
bad, which is the only actionable part. The clearest example from the model
benchmark: Granite-Docling produced 130 "fields" for a W-4 whose ground truth is
19, nearly all of it instructional prose read as fields. "F1 0.18" does not say
that; "the span classifier cannot separate instructions from labels" does.

A phase that is not built reports `NOT BUILT` rather than quietly passing the
state through. A pipeline that looks like it ran eight steps when it ran three is
the more expensive kind of wrong.

The same care applies to skips. "No fields need a label" and "no fields were
found" produce the same empty set, and reporting the second as the first is a
phase claiming credit for work that never happened — so they are worded
differently and a test holds them apart.

## What it does today

```
convert-pdf scripts/pdf-form-benchmark/corpus/fed-w9.pdf
```

- **A fillable form** converts, losing no field and inventing none — but all 23
  of W-9's labels are cryptic XFA names, so 23 fields land in the review queue.
- **A flat form** — the converter's actual target — produces **nothing**, because
  phase 4 is unbuilt. The text is read (725 spans with geometry); it is the
  field-finding that is missing.
- **An image-only scan** skips text extraction rather than returning empty text.

## Known duplication

`ImportQualityHeuristics.LooksCryptic` copies a private helper in
`PdfImportQualityAssessor`, because segregation rules out making that helper
public for now. A duplicate nobody checks is a duplicate that drifts, so a test
compares the two across all 11 corpus forms via the public `CrypticLabelRatio`.
Delete the copy when the converter is folded in.

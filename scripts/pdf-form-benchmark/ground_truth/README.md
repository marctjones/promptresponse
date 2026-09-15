# Ground truth answer key

Hand-verified by reading each source PDF directly (not model-generated). Each
`<form_id>.json` enumerates the form's real sections and fillable fields per
the schema in the benchmark task description.

## Field counts per form

| form_id         | sections | fields | tables | notes |
|-----------------|---------:|-------:|-------:|-------|
| fed-w9          | 3        | 16     | 0      | |
| fed-w4          | 7        | 19     | 0      | Worksheets on pages 3-4 ("Keep for your records") excluded -- not part of the submitted form. |
| fed-ss4         | 4        | 52     | 0      | Many small checkbox+write-in pairs; see judgment calls below. |
| fed-8822        | 2        | 31     | 0      | |
| fed-i9          | 4        | 44     | 3      | Three repeating grids modeled as tables instead of flattened fields. |
| ct-w4           | 2        | 25     | 0      | Filing-status/gross-income grid at the top is a reference lookup table, not fillable fields. |
| ct-dmv-j23      | 9        | 41     | 0      | |
| ct-dmv-b58ind   | 8        | 27     | 1      | Vehicles/Vessels list modeled as a 3-row table. |
| ct-dmv-a25      | 3        | 15     | 0      | |
| ct-dmv-a83      | 3        | 15     | 0      | Prose-style Power of Attorney; blanks embedded mid-sentence. |
| ct-dmv-b225p    | 2        | 28     | 0      | "DMV USE ONLY" box excluded (not applicant/physician-fillable). |
| **Total**       |          | **313**| **4**  | |

## Conventions used throughout

- **`page_count`** is the PDF's actual page count (as delivered to the models
  under test), including instruction/reference pages -- not just the count of
  pages containing fillable content. If a different convention is wanted
  (e.g. "pages with fillable fields only"), it should be changed uniformly
  across all 11 files, not per-file.
- **Checkbox groups**: a set of mutually-exclusive checkboxes for one logical
  question (e.g. filing status, entity type, "check one box") is modeled as a
  single field with `field_kind: "choice"` and a `choices` array, per the task
  instructions. A checkbox with its own independent yes/no meaning (not part
  of a "check one of the following" group) is a separate `checkbox` field.
- **Write-in blanks attached to a choice**: several forms pair a checkbox
  option with its own blank (e.g. W-9 line 3a's LLC tax-classification code,
  SS-4's "Other (specify)" boxes, I-9's alien-authorization identifiers).
  These are modeled as separate, `required: false`, conditional fields
  alongside the parent `choice` field -- not merged into it -- because a
  filler must both pick the option and supply the extra text, and a
  model-generated APR template should capture both as distinct answerable
  prompts. Several of these write-in fields share literal label text with
  other write-ins elsewhere on the same form (e.g. multiple "Other (specify)"
  blanks on SS-4); each such field's `notes` flags the duplication so a
  string-matching scorer can disambiguate by section/position instead.
- **Two labeled entry areas that are alternatives, not checkboxes** (e.g.
  W-9's SSN-or-EIN boxes): kept as two separate fields with `required: null`
  and a note explaining the alternative relationship, rather than forced into
  a `choice` field, since the form presents them as two distinct blanks
  joined by "or," not as checkbox options.
- **Repeating grids/tables**: a form section that repeats an identical block
  of fields several times (I-9 Section 2's List A documents, I-9 Supplement A
  preparer/translator certifications, I-9 Supplement B reverification blocks,
  B-58's vehicle/vessel list) is modeled as one entry in `tables` with
  `row_count_visible` and a `columns` array, rather than flattened into N
  copies of the same fields, per the task's table-modeling guidance.
- **Reference/lookup tables are not fields.** CT-W4's filing-status/gross-
  income grid (used to look up a withholding code) and I-9's "Lists of
  Acceptable Documents" page are informational, not fillable, and are
  excluded entirely.
- **"DMV/IRS/agency use only" boxes are excluded.** Boxes explicitly marked
  as completed by the agency after submission (I-9's implicit EIN box is not
  present, but B-225P's "DMV USE ONLY" permit-number/plate-number/expiration
  box is) are not applicant-fillable and are excluded.
- **`required`** is `true` only when the form marks it explicitly (asterisk,
  "(Required)", "must," or it is clearly load-bearing like a signature),
  `false` when explicitly optional or conditional on another field/choice,
  and `null` when genuinely ambiguous (noted per-field).

## Forms/fields most worth a second look

- **fed-ss4 (SS-4)**: The largest field count (52) relative to its single
  page, because lines 9a/10/16 are checkbox-groups where several options
  carry their own write-in blank (8 write-ins under line 9a's entity type
  alone). A reviewer disagreeing with modeling each write-in as its own
  field (vs. treating the whole group as one field with a single free-text
  "specify" fallback) would change this count significantly.
- **fed-w4 (W-4)**: The Step 2(b) Multiple Jobs Worksheet (page 3) and Step
  4(b) Deductions Worksheet (page 4) are both explicitly "Keep for your
  records" -- calculation aids, not part of the certificate returned to the
  employer -- and were excluded from `fields`/`tables`. If the benchmark
  intends to score a model's handling of these worksheets too, they need to
  be added back in (each is itself a small multi-line calculation form).
- **fed-i9 (I-9)**: Whether Section 1's SSN field is `required` is
  genuinely conditional (voluntary unless the employer uses E-Verify) --
  modeled as `null`. The three repeating grids are modeled as tables; a
  scorer that expects flattened per-instance fields instead would need a
  different convention here.
- **ct-dmv-b225p (B-225P)**: The exclusion of the top "DMV USE ONLY" box is
  a judgment call -- it contains "NEW"/"REPLACEMENT" checkboxes that visually
  echo the applicant's own "TYPE OF APPLICATION" choice field just below it;
  a model under test may mistakenly emit fields for both boxes.
- **ct-dmv-a83 (Special Power of Attorney)**: A prose-style legal form where
  several blanks fall mid-sentence (e.g. the execution date is split across
  three separate blanks for day/month/year). Modeled as three distinct
  fields per date rather than one combined date field, matching what is
  visually on the page; a model that instead emits one `date_field` per date
  is arguably also reasonable and should not be scored as badly wrong.
- **ct-w4 (CT-W4)**: The large filing-status/gross-income table at the top
  of the form is a lookup reference (to determine the Withholding Code
  letter for Line 1), not a set of fillable fields -- excluded. A model that
  tries to convert it into form fields should be penalized as over-counting,
  not rewarded.

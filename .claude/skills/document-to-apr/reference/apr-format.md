# APR format — condensed spec

APR (`.aprt` for templates, `.aprf` for filled forms, `.apr` generic) is UTF-8
JSON. A document is a tree: **Document → Sections → (nested Sections) → Prompts**.
It carries content only — no layout, no styling, no code.

## Top level

```json
{
  "version": "1.0",
  "documentType": "template",
  "metadata": { ... },
  "sections": [ ... ]
}
```

- `version` — always `"1.0"`.
- `documentType` — `"template"` (blank form) or `"filledForm"` (has answers). When
  importing, always `"template"`.
- `metadata` — see below.
- `sections` — at least one; order is preserved.

## metadata

```json
{
  "title": "New Contact Intake",
  "description": "Collect information from new contacts",
  "created": "2026-06-06T00:00:00Z",
  "modified": "2026-06-06T00:00:00Z",
  "author": "…",
  "templateId": "contact-intake",
  "templateVersion": "1.0"
}
```

- `title` is **required** and non-empty. Everything else is optional.
- `templateId` — a stable kebab-case id for the template (recommended).
- Dates are ISO-8601 UTC strings.

## Section

```json
{
  "id": "section_basic_info",
  "title": "Basic Information",
  "description": "Personal and contact details",
  "prompts": [ ... ],
  "sections": [ ... ]
}
```

- `id` — **required**, unique across the whole document.
- `title` — **required**, non-empty.
- `description` — optional.
- `prompts` — the fields directly in this section (may be empty if it only nests
  sub-sections).
- `sections` — nested sub-sections, unlimited depth.
- A section must contain at least one prompt **or** one child section (exception:
  a table section using `dynamicRows`, which may legitimately start empty).
- `tableLayout` — present only on table sections (see "Table section").

## Prompt (a field)

```json
{
  "id": "prompt_email",
  "label": "Email Address",
  "response": "",
  "hints": {
    "placeholder": "john.smith@example.com",
    "expectedDataType": "email",
    "helpText": "Primary email address",
    "suggestedValues": ["A", "B"],
    "validationPattern": "…optional regex (advisory)…"
  },
  "responseMetadata": {}
}
```

- `id` — **required**, unique across the document.
- `label` — **required**, non-empty, human-readable, unique (don't reuse labels).
- `response` — **always a string**; `""` in a template. Even numbers/dates are
  strings (`"42"`, `"2026-06-06"`).
- `hints` — all optional, all advisory:
  - `expectedDataType` — one of `text`, `multiline`, `email`, `phone`, `url`,
    `number`, `currency`, `date`, `time`, `datetime`, `boolean`.
  - `placeholder` — example text shown in an empty field.
  - `helpText` — guidance shown with the field.
  - `suggestedValues` — array of options → rendered as a dropdown.
  - `validationPattern` — optional regex; advisory only, never enforced.
  - Expression hints (advanced): `exprValue`, `exprHidden`, `exprExpected`,
    `exprReadOnly`, `exprValidation` — see "Expressions".
- `responseMetadata` — leave as `{}` for a template.

## Table section

A section becomes a table when it has a `tableLayout`. Columns are fields; rows
repeat.

```json
{
  "id": "tbl_income",
  "title": "Income by Tax Year",
  "tableLayout": {
    "columns": [
      { "id": "wages",    "label": "Wages",    "type": "currency", "placeholder": "0.00" },
      { "id": "interest", "label": "Interest", "type": "currency" }
    ],
    "fixedRows": [
      { "id": "year_2024", "label": "2024" },
      { "id": "year_2023", "label": "2023" }
    ]
  },
  "sections": [
    {
      "id": "year_2024",
      "title": "2024",
      "prompts": [
        { "id": "year_2024.wages",    "label": "Wages",    "response": "", "hints": { "expectedDataType": "currency" } },
        { "id": "year_2024.interest", "label": "Interest", "response": "", "hints": { "expectedDataType": "currency" } }
      ]
    }
    // …one child section per fixed row…
  ]
}
```

- `tableLayout.columns[]` — `{ id, label, type?, placeholder?, suggestedValues?, helpText? }`.
  `type` uses the same vocabulary as `expectedDataType` (`text`, `currency`,
  `number`, `date`, `boolean`, …).
- Use **either** `fixedRows` **or** `dynamicRows`:
  - `fixedRows[]` — `{ id, label }`. For each fixed row, add a **child section**
    whose `id` equals the row id and whose prompts are the cells. Each cell prompt
    id is `"{rowId}.{columnId}"`. These cells become individually fillable.
  - `dynamicRows` — `{ minRows?, maxRows?, rowLabel? }` for user-added rows; do
    not create child sections.

## Expressions (advanced, optional)

Expression hints use a safe CEL-style subset (no code execution). They reference
other prompts by **id**. Strings are the value type, so compare with `''` and
convert with `double(...)` / `int(...)` as needed.

- `exprValue` — computed, read-only value: `double(quantity) * double(unit_price)`.
- `exprHidden` — hide the prompt when truthy: `is_gift != 'true'`.
- `exprExpected` — mark required when truthy: `rush == 'true'`.
- `exprReadOnly` — make read-only when truthy.
- `exprValidation` — advisory cross-field check.

Only add these when the form clearly implies them. A plain field is always valid.

**Ids used in expressions must be identifier-safe** — letters, digits, and
underscores only (no hyphens). `unit_price` is fine; `unit-price` parses as
`unit` minus `price`. Ids not referenced by any expression may use hyphens.

## Validation checklist (what the validator enforces)

- `version == "1.0"`, `metadata.title` non-empty, ≥ 1 section.
- Every section: non-empty `id` (unique) and non-empty `title`; not empty (has a
  prompt or child section, unless it's a `dynamicRows` table).
- Every prompt: non-empty `id` (unique) and non-empty `label`.
- All ids unique across the entire document.
- Hints are advisory — they never cause validation failure. (Type mismatches at
  most produce warnings, never errors.)

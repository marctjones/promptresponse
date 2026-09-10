# APR format — condensed spec

APR (`.aprt` for templates, `.aprf` for filled forms, `.apr` generic) is UTF-8
JSON. A document is a tree: **Document → Sections → (nested Sections) → Prompts**.
It carries content only — no layout, no styling, no code.

## Top level

```json
{
  "aprVersion": "1.0-beta.6",
  "documentType": "template",
  "metadata": { ... },
  "sections": [ ... ]
}
```

- `aprVersion` — the format version this document declares. Use
  `"1.0-beta.6"`: that is what the current validator accepts, and a document
  declaring anything else is rejected. (The key is `aprVersion`, not
  `version`; a document using `version` fails validation with
  `UNSUPPORTED_VERSION`.)
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
  "templateId": "tag:example.com,2026:contact-intake",
  "templateVersion": "1.0"
}
```

- `title` is **required** and non-empty. Everything else is optional.
- `templateId` — **must be a URI** when present. Use a tag URI such as
  `tag:example.com,2026:contact-intake`: a domain or email address you held on a
  date, then a name. A bare `contact-intake` is rejected (`WRONG_TYPE`). A filled
  form must carry one.
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
- A section must contain at least one prompt **or** one child section. There is no
  exception: a table needs at least one row.
- `kind` — `"table"` only on a table section (see "Table section"). `canAddRows`
  and `maxRows` belong only on a table.

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
  }
}
```

- `id` — **required**, unique across the document.
- `label` — **required**, non-empty, human-readable, unique (don't reuse labels).
- `response` — **always a string**; `""` in a template. Even numbers/dates are
  strings (`"42"`, `"2026-06-06"`).
- `hints` — all optional, all advisory:
  - `expectedDataType` — one of the registered types: `text`, `multiline`, `email`,
    `phone`, `url`, `date`, `time`, `datetime`, `number`, `currency`,
    `boolean`, `select`, `multichoice`, `password`, `range`, `color`.
  - `placeholder` — example text shown in an empty field.
  - `helpText` — guidance shown with the field.
  - `suggestedValues` — options offered to the filler; a response outside them is still valid.
    Pair with `select` for choose-one or `multichoice` for choose-several.
  - `validationPattern` — optional regex; advisory only, never enforced.
  - Expression hints (advanced): `exprValue`, `exprHidden`, `exprExpected`,
    `exprReadOnly`, `exprValidation` — see "Expressions".

## Table section

A section becomes a table by carrying `"kind": "table"` — and only by carrying it.
Rows are ordinary child sections; cells are ordinary prompts.

```json
{
  "id": "tbl_income",
  "title": "Income by Tax Year",
  "kind": "table",
  "sections": [
    {
      "id": "year_2024",
      "title": "2024",
      "prompts": [
        { "id": "year_2024.wages",    "label": "Wages",    "response": "", "hints": { "expectedDataType": "currency" } },
        { "id": "year_2024.interest", "label": "Interest", "response": "", "hints": { "expectedDataType": "currency" } }
      ]
    },
    {
      "id": "year_2023",
      "title": "2023",
      "prompts": [
        { "id": "year_2023.wages",    "label": "Wages",    "response": "", "hints": { "expectedDataType": "currency" } },
        { "id": "year_2023.interest", "label": "Interest", "response": "", "hints": { "expectedDataType": "currency" } }
      ]
    }
  ]
}
```

- **There are no column definitions.** A column header is the `label` of the prompt
  in that position, and its type is that prompt's `expectedDataType`. Prompts in
  the same position correspond across every row.
- Each row's `title` identifies it. Ids follow `"{rowId}.{columnId}"`.
- `canAddRows: true` lets a filler add or remove rows; absent means fixed.
  `maxRows` is an advisory cap.
- A table has **at least one** row, even one a filler can add to — otherwise
  `EMPTY_TABLE`.
- `tableLayout`, `columns`, `fixedRows` and `dynamicRows` are **not APR**; an
  earlier design used them. The validator still reports such a document valid,
  with an `UNPREFIXED_MEMBER` warning, and the table is gone.

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

- `aprVersion` is `"1.0-beta.6"`, `documentType` is `"template"` or `"filledForm"`,
  `metadata.title` is non-empty, and there is at least one section.
- `metadata.templateId`, when present, is a URI; a `filledForm` must have one.
- Every section has a non-empty `id` and `title` and at least one prompt or child
  section — tables included.
- Every prompt has a non-empty `id` and `label`.
- Section ids are unique among all sections, and prompt ids among all prompts,
  across the whole document. The two are separate namespaces.
- Every `response` is a JSON string.
- A `kind: "table"` section has at least one child section.
- Hints are advisory: they never cause a validation failure, at most a warning.
- Read the warnings anyway. `UNPREFIXED_MEMBER` means a member that is not APR.

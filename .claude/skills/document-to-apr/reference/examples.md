# Worked examples: source form → APR

Three patterns. Each shows the kind of source you'd see and the `.aprt` to emit.

---

## 1. A simple form

**Source (what the form shows):**

> **Volunteer Sign-Up**
> Help us staff the community fair.
>
> *Your details*
> - Full name: ____________________
> - Email: ____________________  (we'll send a confirmation)
> - T-shirt size:  ☐ S  ☐ M  ☐ L  ☐ XL
>
> *Availability*
> - Which day can you help?  ☐ Saturday  ☐ Sunday
> - Notes / anything we should know: __________________________

**APR (`volunteer-sign-up.aprt`):**

```json
{
  "aprVersion": "1.0-beta.6",
  "documentType": "template",
  "metadata": {
    "title": "Volunteer Sign-Up",
    "description": "Help us staff the community fair.",
    "templateId": "tag:example.com,2026:volunteer-sign-up",
    "templateVersion": "1.0"
  },
  "sections": [
    {
      "id": "your-details",
      "title": "Your details",
      "prompts": [
        {
          "id": "full-name",
          "label": "Full name",
          "response": "",
          "hints": { "expectedDataType": "text" }
        },
        {
          "id": "email",
          "label": "Email",
          "response": "",
          "hints": { "expectedDataType": "email", "helpText": "We'll send a confirmation." }
        },
        {
          "id": "tshirt-size",
          "label": "T-shirt size",
          "response": "",
          "hints": { "expectedDataType": "select", "suggestedValues": ["S", "M", "L", "XL"] }
        }
      ]
    },
    {
      "id": "availability",
      "title": "Availability",
      "prompts": [
        {
          "id": "day",
          "label": "Which day can you help?",
          "response": "",
          "hints": { "expectedDataType": "select", "suggestedValues": ["Saturday", "Sunday"] }
        },
        {
          "id": "notes",
          "label": "Notes / anything we should know",
          "response": "",
          "hints": { "expectedDataType": "multiline" }
        }
      ]
    }
  ]
}
```

Notes:
- The two visual groups became two sections, each with a `title`.
- "T-shirt size" and "day" are option lists → `select` with `suggestedValues`, not
  several boolean fields, because the user picks one.
- The printed "(we'll send a confirmation)" became `helpText`.
- Every `response` is `""`; every id is unique and descriptive.

---

## 2. A table / grid

**Source:** a "Quarterly figures" grid — one row per quarter, columns *Revenue* and
*Expenses*.

```json
{
  "id": "quarterly",
  "title": "Quarterly figures",
  "kind": "table",
  "sections": [
    { "id": "q1", "title": "Q1", "prompts": [
      { "id": "q1.revenue",  "label": "Revenue",  "response": "", "hints": { "expectedDataType": "currency" } },
      { "id": "q1.expenses", "label": "Expenses", "response": "", "hints": { "expectedDataType": "currency" } } ] },
    { "id": "q2", "title": "Q2", "prompts": [
      { "id": "q2.revenue",  "label": "Revenue",  "response": "", "hints": { "expectedDataType": "currency" } },
      { "id": "q2.expenses", "label": "Expenses", "response": "", "hints": { "expectedDataType": "currency" } } ] },
    { "id": "q3", "title": "Q3", "prompts": [
      { "id": "q3.revenue",  "label": "Revenue",  "response": "", "hints": { "expectedDataType": "currency" } },
      { "id": "q3.expenses", "label": "Expenses", "response": "", "hints": { "expectedDataType": "currency" } } ] },
    { "id": "q4", "title": "Q4", "prompts": [
      { "id": "q4.revenue",  "label": "Revenue",  "response": "", "hints": { "expectedDataType": "currency" } },
      { "id": "q4.expenses", "label": "Expenses", "response": "", "hints": { "expectedDataType": "currency" } } ] }
  ]
}
```

Why:
- `kind: "table"` is what makes it a table. Each quarter is a row — a child section
  whose `title` names it.
- The column headers *are* the prompt labels, "Revenue" and "Expenses", in the same
  position in every row. There is no separate list of columns.

If instead the form lets the filler add as many rows as they like (line items,
expenses), write one row as the pattern and allow more:

```json
{
  "id": "line_items",
  "title": "Line items",
  "kind": "table",
  "canAddRows": true,
  "maxRows": 50,
  "sections": [
    { "id": "item_1", "title": "Item 1", "prompts": [
      { "id": "item_1.item",  "label": "Item",  "response": "" },
      { "id": "item_1.qty",   "label": "Qty",   "response": "", "hints": { "expectedDataType": "number" } },
      { "id": "item_1.price", "label": "Price", "response": "", "hints": { "expectedDataType": "currency" } } ] }
  ]
}
```

A table always has at least one row; one with none is reported `EMPTY_TABLE`.

---

## 3. Computed & conditional fields

**Source:** an order line that shows "Line total = Qty × Unit price", and a "Gift
message" box marked "only if this is a gift".

**APR (prompts within a section):**

> **Important:** any prompt referenced in an expression must have an
> **identifier-safe id** — letters, digits, and underscores only, no hyphens.
> `unit_price` works; `unit-price` would be read as `unit` minus `price`. (Ids
> *not* used in expressions may use hyphens freely, as in examples 1 and 2.)

```json
{
  "id": "is_gift",
  "label": "Is this a gift?",
  "response": "",
  "hints": { "expectedDataType": "boolean" }
},
{
  "id": "gift_message",
  "label": "Gift message",
  "response": "",
  "hints": {
    "expectedDataType": "multiline",
    "helpText": "Shown only when the order is a gift.",
    "exprHidden": "is_gift != 'true'"
  }
},
{
  "id": "quantity",
  "label": "Quantity",
  "response": "",
  "hints": { "expectedDataType": "number" }
},
{
  "id": "unit_price",
  "label": "Unit price",
  "response": "",
  "hints": { "expectedDataType": "currency" }
},
{
  "id": "line_total",
  "label": "Line total",
  "response": "",
  "hints": {
    "expectedDataType": "currency",
    "helpText": "Calculated automatically.",
    "exprValue": "quantity == '' || unit_price == '' ? '' : double(quantity) * double(unit_price)"
  }
}
```

Expressions reference other prompts by id; values are strings, so guard with
`== ''` and convert with `double(...)`. Omit expressions when the form doesn't
clearly call for them.

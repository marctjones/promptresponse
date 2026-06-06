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
  "version": "1.0",
  "documentType": "template",
  "metadata": {
    "title": "Volunteer Sign-Up",
    "description": "Help us staff the community fair.",
    "templateId": "volunteer-sign-up",
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
          "hints": { "expectedDataType": "text", "suggestedValues": ["S", "M", "L", "XL"] }
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
          "hints": { "expectedDataType": "text", "suggestedValues": ["Saturday", "Sunday"] }
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
- "T-shirt size" and "day" are option lists → `suggestedValues` (a dropdown), not
  several boolean fields, because the user picks one.
- The printed "(we'll send a confirmation)" became `helpText`.
- Every `response` is `""`; every id is unique and descriptive.

---

## 2. A table / grid

**Source:** a grid titled "Quarterly figures" with columns *Revenue*, *Expenses*
and rows *Q1…Q4*.

**APR (table section):**

```json
{
  "id": "quarterly",
  "title": "Quarterly figures",
  "tableLayout": {
    "columns": [
      { "id": "revenue",  "label": "Revenue",  "type": "currency" },
      { "id": "expenses", "label": "Expenses", "type": "currency" }
    ],
    "fixedRows": [
      { "id": "q1", "label": "Q1" },
      { "id": "q2", "label": "Q2" },
      { "id": "q3", "label": "Q3" },
      { "id": "q4", "label": "Q4" }
    ]
  },
  "sections": [
    {
      "id": "q1",
      "title": "Q1",
      "prompts": [
        { "id": "q1.revenue",  "label": "Revenue",  "response": "", "hints": { "expectedDataType": "currency" } },
        { "id": "q1.expenses", "label": "Expenses", "response": "", "hints": { "expectedDataType": "currency" } }
      ]
    }
    // …repeat a child section for q2, q3, q4 (ids q2.revenue, q2.expenses, …)…
  ]
}
```

If instead the form lets the user add as many rows as they like (line items,
expenses), drop `fixedRows` and the child sections, and use:

```json
"tableLayout": {
  "columns": [
    { "id": "item",  "label": "Item",  "type": "text" },
    { "id": "qty",   "label": "Qty",   "type": "number" },
    { "id": "price", "label": "Price", "type": "currency" }
  ],
  "dynamicRows": { "minRows": 1, "maxRows": 50, "rowLabel": "Item" }
}
```

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

# APR File Format Specification

**Specification document version:** 1.0.0-beta.3 (tracks the PromptResponse release)
**Status:** 🚧 **BETA — the format is not frozen and breaking changes may still occur**
**Describes format version:** `1.0-beta`
**History:** pre-1.0 drafts are preserved in Git history and are not implementation references.
**Machine-readable schema:** [`schemas/apr-1.0.schema.json`](../schemas/apr-1.0.schema.json)
**Conformance corpus:** [`tests/Conformance/v1/`](../tests/Conformance/v1/)

---

## 1. About this document

APR (Adaptive Prompt Response) is a JSON file format for forms. An APR file
describes *what to collect*, never *how to display it*. A blank form and a
completed form are the same structure, distinguished by one field.

This specification is **descriptive**: it documents the format as implemented and
tested, not as hoped for. Three artifacts define APR 1.0 together, and where they
disagree the order of authority is:

1. **The conformance corpus** (`tests/Conformance/v1/`) — executable, and always right.
2. **The JSON Schema** (`schemas/apr-1.0.schema.json`) — structural rules, machine-checkable.
3. **This prose** — everything the first two cannot express.

If prose contradicts a fixture, the prose is a bug. Report it.

### Navigation

Read the specification in this order when implementing APR:

1. [Conformance profiles](#2-conformance-profiles) identify the required and
   optional behavior.
2. [Encoding and strings-only responses](#3-encoding-and-the-strings-only-rule),
   [document structure](#4-document-structure), [document type](#5-document-type-and-file-extensions),
   [validation](#6-validation), and [text handling](#7-text-handling) define core.
3. [Expressions](#8-profile-expressions) and [signatures](#9-profile-signatures)
   are optional profiles.
4. [Rendering](#10-rendering), [security](#11-security-considerations), and the
   [conformance checklist](#12-conformance-checklist) constrain hosts and verify
   an implementation.

This navigation section is informative. The requirement language in the sections
it links to remains authoritative.

### 1.1 Requirement language

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHOULD**,
**SHOULD NOT**, **MAY**, and **OPTIONAL** are to be interpreted as described in
RFC 2119.

### 1.2 What changed from the v0.2 draft

The v0.2 draft described a system that no longer exists. Substantive changes:

| Area | v0.2 draft | 1.0 |
|---|---|---|
| Version string | `"0.2"` | **`"1.0-beta"`** — one identifier for the wire format, not a per-release number (§1.3) |
| Document type | Filename extension **overrides** `documentType` | **`documentType` is authoritative**; the extension is a desktop affordance (§5) |
| Nesting | "Unlimited" | **At least 16 levels REQUIRED**; unbounded depth is not implementable (§4.6) |
| Strings-only | Absolute | **One documented exception** — `signer.selfSigned`, derived from a certificate rather than authored (§3.2). The table redesign removed the other two |
| Tables | `tableLayout` with a `columns` array and a `fixedRows` list | **`kind: "table"` only.** Rows are sections, cells are prompts, headers are the prompts' own labels — no column records, no duplicated row list (§4.5) |
| Table column widths | Specified | **Removed.** Presentation data has no place in APR |
| Expressions | Absent (engine deleted) | **CEL**, an optional profile (§8). The language is borrowed with its conformance suite; only the binding is APR's |
| Signatures | Absent | A defined **optional profile** (§9) |
| Text handling | Unspecified | **Normative** (§7) — it was silently rewriting filler answers while leaving author-supplied URLs unchecked |

Migration: change `"version": "0.2"` to `"version": "1.0-beta"` and delete any
table column width fields. Nothing else in a v0.2 file needs to change.

### 1.3 Three version numbers, and which one is which

Conflating these is the most common way to break a file format, so they are kept
strictly apart.

| Number | Changes | Lives in | Today |
|---|---|---|---|
| **Format version** | only on a **breaking change to the wire format** | the `version` field of every file | `1.0-beta` |
| **Specification document version** | **every release** | this document's header | `1.0.0-beta.3` |
| **Conformance corpus tag** | **every release** | `tests/Conformance/v1/` + a git tag | `corpus/v1 @ <sha>` |

**The format version MUST NOT track releases.** Two releases that do not change the
wire format declare the same format version, and that is correct.

#### 1.3.1 MAJOR.MINOR compatibility

The format version is `MAJOR.MINOR`, optionally followed by a pre-release suffix.
**Compatibility is decided by MAJOR.MINOR alone.**

| Declared version | A `1.0` reader MUST |
|---|---|
| same MAJOR, same or older MINOR | read it normally |
| same MAJOR, **newer MINOR** | **read it**, warn (`NEWER_MINOR_VERSION`), ignore members it does not know — and **preserve them** (§4.8) |
| **different MAJOR** | **reject it** — `UNSUPPORTED_VERSION` |
| unparseable | reject it |

This is what makes the format extensible. A minor release adds optional members and
bumps MINOR; every existing reader keeps working, and nothing a newer writer added
is lost when an older reader saves the file. **Newer-minor tolerance and unknown-member
preservation are a pair — neither is useful without the other.**

A pre-release suffix (`-beta`) is **informational and ignored** when deciding
compatibility, so `1.0-beta` and `1.0` are the same format. The stable tag therefore
needs no migration, no legacy-version list, and no change to any existing file.

**What BETA means here:** breaking changes MAY still occur, and each one bumps MAJOR.
Implementers **SHOULD** record the corpus tag they pass, not just the format version.

---

## 2. Conformance profiles

APR is deliberately layered so that a complete, useful implementation can be
written in an afternoon, in any language, on any device. Only the core is
required.

### 2.1 `core` — REQUIRED of every implementation

Parse, validate, fill, and write documents per §3–§7. A core implementation is
fully conformant. It is not a degraded one, and it need not emit HTML, PDF, or
native controls. It exposes the semantic document and its advisory hints for a
host application or renderer to use.

### 2.2 `core+expressions` — OPTIONAL

Additionally evaluates the `expr*` hint family (§8).

A core-only implementation **MUST NOT** reject a document that uses expressions.
It **MUST** preserve the expression strings when writing the document back. If a
host renders the document, it presents those prompts as ordinary editable fields:
a computed field simply becomes a field the user can type into — degraded, but
never broken, and never lost.

### 2.3 `core+signatures` — OPTIONAL

Additionally verifies detached CMS signatures (§9).

A core-only implementation **MUST NOT** reject a signed document, **MUST**
preserve the `signatures` array on round-trip, and **MUST NOT** report a document
as verified. It **SHOULD** indicate that signatures are present but unchecked.

This profile is optional for a reason of policy, not merely of cost. **Nobody is
obliged to sign, and nobody is obliged to care that something was signed.** A
recipient may have every reason to trust a document's contents by other means —
they know the sender, they requested the form, the data is low-stakes, or they
simply want to read it. Requiring verification before the data can be used would
impose the form author's threat model on every reader, which is not a decision the
file format gets to make. See §9.5.

### 2.4 Declaring conformance

State the profiles you implement and the corpus commit you pass. "APR 1.0 core,
corpus v1 @ `<sha>`" is a complete and honest claim.

---

## 3. Encoding and the strings-only rule

### 3.1 File encoding

An APR file **MUST** be UTF-8-encoded JSON (RFC 8259). A byte-order mark
**SHOULD NOT** be written; a reader **SHOULD** tolerate a leading BOM.

Media type: `application/vnd.apr+json`. (Not yet IANA-registered.)

### 3.2 Responses are strings

A `prompt.response` **MUST** be a JSON string. A response given as a JSON number or
boolean **MUST** be rejected at parse time.
It **MUST NOT** be coerced to `"42"` or `"true"`. Silent coercion is worse than
rejection: it produces a file that looks conformant while having invented data
that no user entered. See `malformed/response-is-number.aprt`.

`"response": null` and an absent `response` key are both read as `""`. A writer
**MUST NOT** emit null. See `valid/null-response-coercion.aprf`.

### 3.3 Any string is a valid response

**This is the rule the rest of the format exists to protect.**

A response field **MAY** contain any string. The format has no opinion about
whether that string is "correct".

| `expectedDataType` | Response | File validity |
|---|---|---|
| `number` | `"about twelve"` | **Valid** |
| `email` | `"call me instead"` | **Valid** |
| `date` | `"the summer of 1985"` | **Valid** |
| anything | `""` | **Valid** |

The distinction is between **file validity** — is this well-formed APR? — and
**workflow acceptance** — will the receiving office act on it? A benefits office
may reject a form for a blank field or an unparseable date. That is a workflow
decision, made by a workflow, and it has nothing to do with whether the file is
valid APR.

This matters because forms are filled by people under conditions the form author
did not anticipate. Someone whose legal name does not fit the field, whose
address is not a street address, whose answer is "I don't know" — all of them
produce valid APR. A format that rejected them would be a format that discards
true information because it was inconveniently shaped.

See `valid/hints-contradicted.aprf`, which contradicts every hint it declares and
**MUST** validate cleanly.

### 3.4 Hints never enforce

Every member of `prompt.hints` is advisory. A hint **MUST NOT** cause a response
to be rejected, altered, truncated, or blocked from being saved. This applies to
`validationPattern` — a non-matching response is a warning at most — and to every
member of the `expr*` family.

An implementation **MAY** surface a hint mismatch as an advisory warning. It
**MUST NOT** prevent the user from saving.

---

## 4. Document structure

### 4.1 Document

```json
{
  "version": "1.0-beta",
  "documentType": "template",
  "metadata": { "title": "Permit Application" },
  "sections": [ ... ],
  "signatures": [ ... ]
}
```

| Member | Type | Required | Notes |
|---|---|---|---|
| `version` | string | **Yes** | A compatible `1.MINOR` value per §1.3.1; current writers emit `"1.0-beta"`. |
| `documentType` | string | No | `"template"` or `"filledForm"`. Absent means `"template"`. Authoritative — see §5. |
| `metadata` | object | **Yes** | §4.2 |
| `sections` | array | **Yes** | **MUST** contain at least one section. |
| `signatures` | array | No | Absent when unsigned. §9. |

### 4.2 Metadata

`title` is **REQUIRED** and **MUST** contain a non-whitespace character.
All other scalar members are OPTIONAL strings; timestamps are ISO-8601.
`submissionUrls`, when present, is an ordered array of strings. Even one delivery
choice is represented as a one-element array; a scalar `submissionUrl` is not valid.
Order is the author's preferred display order, never permission for a client to choose
or fall back to a target automatically; submitting remains an explicit user action.

`created`, `modified`, `author` (a person), `publisher` (the organization
standing behind the form), `templateId`, `templateVersion`, `filledBy`,
`filledDate`, `submissionUrls`.

When `documentType` is `"filledForm"`, `templateId` is **REQUIRED**: a completed
form that cannot name the form it completes is not traceable.

When a publisher signature is present, the certificate identity is authoritative
over the `publisher` string, and `submissionUrls` is bound into the signed payload
so it cannot be redirected without breaking the signature (§9.3).

### 4.3 Section

| Member | Type | Required | Notes |
|---|---|---|---|
| `id` | string | **Yes** | Non-whitespace. Unique document-wide among sections. |
| `title` | string | **Yes** | Non-whitespace. **Never optional.** |
| `description` | string | No | |
| `sections` | array | No | Child sections — recursive. |
| `prompts` | array | No | |
| `kind` | string | No | `"table"` when this section's child sections are repeating instances. §4.5 |
| `canAddRows` | string | No | `"true"` if a filler may add or remove instances. Default fixed. §4.5 |
| `maxRows` | string | No | Advisory cap on instance count. §4.5 |

A section **MUST** carry content: at least one prompt or at least one child section.
There is no exception — tables included (§4.5).

**Section titles are required, not optional.** The section tree is the document
outline that a screen-reader user navigates by. An untitled section is a hole in
that outline, so the format refuses to produce one.

### 4.4 Prompt

| Member | Type | Required | Notes |
|---|---|---|---|
| `id` | string | **Yes** | Non-whitespace. Unique document-wide among prompts. |
| `label` | string | **Yes** | Non-whitespace. This is the accessible name. |
| `response` | string | No | Absent means `""`. §3.2. |
| `hints` | object | No | §4.7. Advisory in full. |
| `responseMetadata` | object | No | `inferredDataType`, `lastModified`. Never authoritative. |

**`label` is required and placeholder text is never a substitute for it.** A
placeholder disappears when the user types, is invisible to many assistive
technologies, and leaves the field permanently unnamed. A prompt with a
placeholder and no label is invalid APR. See `invalid/missing-prompt-label.aprt`.

Section ids and prompt ids occupy **separate namespaces**: a section and a prompt
MAY share an id. Within each namespace, ids **MUST** be unique across the whole
document, not merely among siblings — a filled form is consumed by field id, and
a duplicate makes the data ambiguous.

Ids **SHOULD** be stable across template versions and **MUST NOT** change when
prompts are reordered. Reordering a form is a presentation change; changing an id
silently breaks every downstream consumer and every signature covering it.

### 4.5 Tables

A table introduces **no new primitive**. Rows are ordinary sections; cells are
ordinary prompts. A section becomes a table by carrying `kind: "table"`.

```jsonc
{
  "id": "expenses",
  "title": "Expense line items",
  "kind": "table",
  "canAddRows": "true",
  "maxRows": "25",
  "sections": [
    {
      "id": "item_1",
      "title": "Item 1",
      "prompts": [
        { "id": "item_1.description", "label": "Description", "response": "Train fare" },
        { "id": "item_1.amount", "label": "Amount", "response": "42.50",
          "hints": { "expectedDataType": "currency" } }
      ]
    }
  ]
}
```

#### 4.5.1 What `kind: "table"` asserts

It is **a claim about structure, not appearance**:

| | |
|---|---|
| child sections are **instances**, not free-standing subsections | |
| prompts at the **same position correspond** across instances | this is what makes "the Amount field" a thing that exists in every row |
| an instance's `title` **identifies** it | `Q1`, `Item 2` |
| a prompt's `label` **names the corresponding field** across every instance | |

There is deliberately **no column definition**. A column header *is* the
corresponding prompt's label; a column's type hint *is* that prompt's
`expectedDataType`. Declaring columns separately would state twice what the prompts
already state, and anything stated twice can disagree — which is the failure this
design removes rather than manages.

Correspondence is **by position**. Ids are free-form; the convention
`{rowId}.{columnId}` is **RECOMMENDED** for addressability and database import, but
it carries no meaning the renderer depends on.

#### 4.5.2 A table licenses no layout

A renderer **MAY** present a table as a grid, as stacked cards, as a flat sequence of
prompts, or as speech. **All are conformant**, and none is a fallback.

This matters most where tables are hardest: a six-column grid is unusable on a phone
and at 200% zoom, and many screen-reader users prefer the linear reading. Under this
model, choosing the linear presentation is not a degraded rendering of a table — it
is an equally valid reading of the same claim, and a capability profile may simply
select it.

`kind: "table"` **MUST NOT** be treated as licence for width, alignment, colour, or
font data. Those member names are retired (§4.8.1) and are dropped on read.

#### 4.5.3 Rows and instances

`canAddRows` is `"true"` when a filler may add or remove instances; absent means
fixed. The default is deliberately restrictive: a fixed table that silently gained a
row is a worse failure than a line-item table needing one explicit property.

**Mutability and population are independent.** Whether instances may be added has
nothing to do with whether they currently hold values — a filled table may still
accept new rows, and a fixed table may be entirely blank.

**A table always has at least one instance.** An "empty" table was never empty: a UI
offering to add the first row is already presenting a row, and how that row is shown
— as a blank line, a ghost row, or an add button — is a display decision. The
instance also carries the table's field names, so a table without one cannot describe
itself.

`maxRows` is an advisory string. A table carrying more instances is still valid and
is reported as a warning (§6.2).

#### 4.5.4 Ragged tables

Instances **SHOULD** agree in prompt count and in the label at each position. When
they disagree the document is still **valid**; a validator reports
`TABLE_RAGGED` or `TABLE_LABEL_MISMATCH` (§6.2) and a renderer presents what is
there. Refusing to open the document would discard whatever a filler had already
written, which §3.3 exists to prevent.

### 4.6 Nesting depth

Sections nest recursively. Every implementation **MUST** support at least **16
levels** of section nesting; `valid/deep-nesting.aprt` pins this floor.
Implementations **MAY** support more.

Unbounded depth is not implementable: every real JSON parser has a depth limit,
and a format that promises infinity promises a stack overflow. The reference
implementation currently fails above 30 levels, because each section level costs
two JSON depth levels against a parser limit of 64. That ceiling is an
implementation detail and **MUST NOT** be relied upon.

Authors **SHOULD** stay far below the floor. Forms nested more than four or five
levels deep are difficult to navigate with any input method.

### 4.7 Hints

All OPTIONAL, all advisory (§3.4).

`placeholder`, `expectedDataType`, `suggestedValues[]`, `helpText`,
`validationPattern`, the `expr*` family (§8), and the bounds family `min`, `max`,
`step`.

`expectedDataType` registry: `text`, `multiline`, `email`, `phone`, `url`,
`date`, `time`, `datetime`, `number`, `currency`, `boolean`, `select`,
`multichoice`, `signature`, `file`, `password`, `range`, `color`.

Country-specific field types are deliberately absent. A postcode, a national
identity number or a tax reference is `text` with a `validationPattern`: baking
one country's formats into the vocabulary would oblige every reader everywhere to
carry them.

The machine-readable form, with each type's canonical write form, accepted read forms,
CEL type, and meaningful hints, is
[`schemas/apr-types-1.0.json`](../schemas/apr-types-1.0.json) — one published vocabulary
rather than the same facts restated here, in the schema, and in the code.

**This registry is open.** An unrecognized value **MUST** degrade to a plain text
field. It **MUST NOT** cause an error — that is what lets the registry grow
without breaking every existing reader. See `valid/unknown-fields.aprt`.

`suggestedValues` offers options; a response outside the list is still valid. On a
`boolean` it names the two options — `["Yes", "No"]`, `["Agree", "Disagree"]` — so a
renderer can label them as the author intended without changing the CEL type.

**Bounds are an offer, not a limit.** `min`, `max` and `step` describe the range a
widget should offer: the ends of a slider, the increment of a spinner. They are
written as strings like every other value (`"min": "0"`, `"step": "0.5"`), and they
are meaningful only on ordered types — `schemas/apr-types-1.0.json` records which.
On `date`, `time` and `datetime`, `min` and `max` are the earliest and latest
suggested values.

A response outside them is **still valid**, exactly as for `suggestedValues`. A slider
that stops at 100 does not make `"120"` a wrong answer, and a validator **MUST NOT**
reject one (§6.1). Bounds shape the affordance offered to someone who wants it; they
never shrink what a person is allowed to say. A reader that ignores them entirely is
still conformant — it simply offers a plainer control.
`select` and `multichoice` are suggestions about presentation, not constraints on
content.

### 4.8 Unknown members

A reader **MUST** ignore members it does not recognise, at every level, and **MUST
NOT** reject a document for carrying them.

A reader **MUST** also **preserve** them: an unrecognised member present on read
**MUST** still be present, unchanged, on write. Without this, every additive change
to the format is destructive — a document written by a newer minor version would
lose its new members the first time an older reader opened and saved it, silently,
with no error anywhere. Preservation is what makes §1.3.1 usable rather than
theoretical.

In the reference implementation this is a `[JsonExtensionData]` bag on every
JSON-visible class; `valid/newer-minor-accepted.aprt` and `valid/unknown-fields.aprt`
pin the behaviour.

**Member names are case-sensitive.** `Version` is not `version`. A wrongly-cased
member is an unknown member: it is preserved as data, and the property it resembles
takes its default. This is a common source of "my field vanished" reports.

#### 4.8.1 Retired members are the exception

Members the specification has **removed** are dropped rather than preserved. Today
that is the table-column presentation set — `width`, `alignment`, `color`,
`background`, `fontSize`, `bold`, `style` (§4.5).

Retirement has to mean something. If a removed member were preserved as an unknown
one, a renderer could keep writing column widths forever and "APR carries no
presentation data" would be unenforceable. Dropping them is how the removal takes
effect.

A name is added to the retired list only when this specification retires it. A member
that is merely unfamiliar — from a future minor version — is preserved, not dropped.

### 4.9 Canonical value forms

Any string remains a valid response (§3.3). This section governs only what a
renderer **writes** when it controls the value — a date picker, a checkbox, a
multi-select list. Without it, the same template filled in two implementations
yields two different datasets, and "database-ready" stops being true.

A reader **MUST** accept every listed read form. A writer **SHOULD** emit the
canonical form. Neither rule ever makes a document invalid.

| Hint | Canonical write form | Also accepted on read |
|---|---|---|
| `date` | `YYYY-MM-DD` (RFC 3339 full-date) | anything |
| `time` | `HH:MM` or `HH:MM:SS` (24-hour) | anything |
| `datetime` | RFC 3339, e.g. `2026-08-25T14:30:00Z` | anything |
| `boolean` | `"true"` / `"false"` | `yes`, `y`, `1`, `on`, `x`, `checked` / `no`, `n`, `0`, `off`, `unchecked` (case-insensitive) |
| `number`, `currency` | digits with `.` as decimal separator, no grouping | anything, including symbols and words |
| `multichoice` | selections separated by `\n` (U+000A), one per line | a single line separated by `, ` (legacy) |
| `select` | exactly one value, verbatim from `suggestedValues` | anything |

**Why `true`/`false` and not `yes`/`no`.** `yes` is English. A format that renders
to voice, to other languages, and into database columns cannot make its canonical
boolean depend on one language. `true`/`false` also matches what the expression
profile already compares against (§8).

**Why newline and not comma for `multichoice`.** A suggested value may itself
contain a comma — `"Bloomfield, CT"` is an ordinary option in a municipal form.
Comma separation silently turns one selection into two, which is data loss, and
§3.3 exists to prevent exactly that. A newline cannot appear inside a single-line
option, so the encoding is lossless. Readers **MUST** still accept the legacy
comma form, and **SHOULD** treat it as one selection when a suggested value
matches the whole string.

Empty string means "no selection" for every hint above.

`valid/canonical-values.aprf` pins both halves: the canonical forms, and the
non-canonical equivalents that a reader must still accept. Note the fixture's
`multi_comma_value` — a single selection whose text contains a comma, which comma
separation would silently split into two.


---

### 4.10 Roles — who each part is for

Most real forms are filled by more than one person. A patient completes an intake,
a nurse records observations, the office stamps a reference. With nowhere to say
so, all three arrive as one undifferentiated list and the patient is left guessing
which questions are theirs.

A section or a prompt **MAY** carry `role`: a short string naming who is meant to
fill it in — `"patient"`, `"nurse"`, `"office"`. A prompt's role overrides the role
of the section containing it, so a single field can be handed back to the patient
without splitting the section in two. The vocabulary is **open**: roles are
domain-specific, and a reader that does not recognise one **MUST** present the
field normally rather than erroring.

A document **MAY** declare its roles in a top-level `roles` array, so the
identifiers have names worth showing:

```json
"roles": [
  { "id": "patient", "name": "Patient",
    "description": "The person receiving care" },
  { "id": "nurse", "name": "Nurse",
    "description": "Clinical staff recording observations" },
  { "id": "office", "name": "Office use" }
]
```

Each entry **MUST** carry `id`; `name` and `description` are OPTIONAL, and a
reader with no `name` **MUST** fall back to the identifier. Declaring is itself
optional and **MUST NOT** be required: a section or prompt **MAY** reference a role
the document never declares, and a reader **MUST** show the identifier rather than
erroring. A validator **MAY** warn about an undeclared role (§6.2); it **MUST NOT**
reject one. Without this the vocabulary would not be open, and every industry with
a party nobody enumerated in advance would be locked out.

**A role says who a field is for. It never says who may type into it.** The format
has no identity at fill time — nothing in a JSON document knows who is at the
keyboard — so a reader **MUST NOT** refuse input to a field because of its role.
Any string is a valid response (§3.3), and that does not stop being true because
the field was labelled for the office.

What a reader **SHOULD** do is make the answer obvious without being asked. Where a
document declares roles, a reader **SHOULD** let the person say which role they are
filling and then show plainly which fields are theirs, so *"is this one mine?"* is
answered by the form rather than by a phone call. Fields belonging to others stay
visible and stay editable; they are marked, not locked. A reader **SHOULD** also
make a role legible to assistive technology, since a visual treatment alone
communicates nothing to a screen reader.

**Accountability comes from signatures, not from the widget.** A greyed-out box is
evidence of nothing: whoever holds the file can edit the JSON directly. A scoped
filler signature (§9.3) over those fields, made with the nurse's certificate, is
evidence the nurse filled them. Roles describe intent; signatures establish fact.
An implementation that treats a role as a security control has misread this
section.

## 5. Document type and file extensions

`documentType` in the JSON is **authoritative**. A reader **MUST** determine
whether a document is a template or a filled form from that field alone.

| Extension | Meaning | Status |
|---|---|---|
| `.aprt` | Template | Convention |
| `.aprf` | Filled form | Convention |
| `.apr` | Either | Convention |

A filename extension is a **desktop affordance** — it drives icons, file
associations, and save dialogs. It is not part of the data model.

> **This inverts the v0.2 draft**, which made the extension override
> `documentType`. That rule cannot be implemented anywhere a filename does not
> exist: an HTTP request body, a database column, a clipboard paste, a mobile
> share intent, a `postMessage` between frames, a byte array in an enterprise
> queue. Under the old rule a browser-based reader and a desktop reader would
> reach *different conclusions about identical bytes*, which is precisely the
> interoperability failure the format exists to prevent. A document must mean the
> same thing everywhere, including where it has no name.

An implementation **SHOULD** write the extension matching `documentType`, and
**SHOULD** warn on mismatch rather than silently honoring either one.

Converting a template to a filled form is an explicit act: set `documentType` to
`"filledForm"` and record `templateId`. Implementations **SHOULD** prompt for a
new filename so the blank template is not overwritten.

---

## 6. Validation

Validation produces **errors** and **warnings**. A document is valid if and only
if it has zero errors. Warnings never affect validity.

### 6.1 Errors — structure only

| Code | Condition |
|---|---|
| `NULL_DOCUMENT` | No document. |
| `REQUIRED_FIELD` | `version`, `metadata.title`, section `id`/`title`, prompt `id`/`label` blank; `sections` empty; `templateId` absent on a filled form. |
| `UNSUPPORTED_VERSION` | `version` has an incompatible major version or is unparseable (§1.3.1). |
| `DUPLICATE_ID` | A section or prompt id repeats within its namespace. |
| `EMPTY_SECTION` | A section has no prompts and no child sections, and is not a dynamic table. |

This list is exhaustive. **No error may ever arise from the content of a
response**, and none may ever arise from the state of a signature (§9.5). A
validator that rejects a document because a response is badly formatted, or
because a signature is missing or invalid, is not implementing APR.

### 6.2 Warnings — advisory only

A response contradicting `expectedDataType`; a response not matching
`validationPattern`; a response outside `suggestedValues`; a blank response the
workflow may consider required; text advisories (§7.3); and the table advisories
`TABLE_NO_ROWS`, `TABLE_RAGGED`, `TABLE_LABEL_MISMATCH`, `TABLE_OVER_CAPACITY`
(§4.5).

Warnings are how an implementation tells a user "this may not be what you meant"
without ever telling them "you may not write this."

### 6.3 Parse errors are not validation errors

Malformed JSON, a response given as a number or boolean, or a structurally wrong
shape are **parse failures**, and a reader **MUST** fail rather than validate.

Documents in `invalid/` parse cleanly and fail validation. Documents in
`malformed/` **MUST NOT** parse at all. Keeping these stages distinct is what
lets a reader load a flawed document and show the user what is wrong with it,
rather than refusing to open it.

---

## 7. Text handling

### 7.1 Scope

Responses are evidence supplied by a person. A reader **MUST** preserve a response
exactly on read and write: it MUST NOT normalize, strip, or otherwise rewrite it.
Escaping and visibly marking deceptive text are rendering responsibilities, not
licences to alter stored data. See `valid/response-edge-cases.aprf`.

### 7.2 Why responses and authoring data differ

The following distinction explains where strictness is appropriate; it does not
override the response and hint rules in §3.

### 7.3 Authoring data and filled data are governed differently

The two halves of an APR document come from two different people under two
different conditions, and they **MUST NOT** be treated alike.

| | **Authoring data** | **Filled data** |
|---|---|---|
| Written by | the form author | the person filling the form |
| Fields | `metadata.*` except `filledBy`/`filledDate`, section `id`/`title`/`description`, prompt `id`/`label`, all of `hints`, all of `tableLayout` | `prompt.response`, `metadata.filledBy`, `responseMetadata.*` |
| Conditions | deliberate, repeatable, reviewable before publication | once, under time pressure, often on someone else's behalf |
| Consumed by | machines and every future reader | the receiving workflow |
| Policy | **Strict rules are appropriate.** Reject or warn at authoring time. | **Maximum tolerance.** Accept any string; never rewrite. |

Strictness at authoring time costs the author one correction before publishing.
Strictness at fill time costs a person their answer, silently, at the moment they
are least able to notice.

#### 7.3.1 Filled data — never rewritten

A response **MUST NOT** be altered on the basis of any hint. A `url` or `email` hint describes what the author
*hoped* to receive; it does not license editing what was actually written.

Suspicious characters in a response **MUST** be surfaced as a warning (§6.2) and
**SHOULD** be rendered visibly — escaped or badged — leaving the stored bytes
exactly as entered. The consuming workflow decides what to do about them; it is
the only party that knows what the answer is for.

`valid/hidden-characters-preserved.aprf` and
`valid/unicode-security-advisories.aprf` pin this: hidden and bidi characters
survive in responses hinted `url`, `email`, and `text` alike, alongside a
legitimate Persian ZWNJ and emoji ZWJ sequence. A reader that "cleans" any of
them has let a hint enforce something, which §3.4 forbids.

#### 7.3.2 Authoring data — strictness is appropriate

Authoring fields **MAY** be held to strict rules, and the fields a machine acts
on **SHOULD** be.

**Strictness here means refusing, not rewriting.** No party's data is ever silently
edited — the difference between an author and a filler is that an author *can* be
stopped and asked to fix something, while a filler must never be blocked. Rewriting an
authored value is not the strict option; it is the same silent edit wearing a different
hat.

`metadata.submissionUrls` is the strongest case in the format. It is an ordered,
author-supplied array of explicit delivery choices,
machine-consumed, security-critical, and bound into the publisher signature payload
(§9.3) specifically so a submission cannot be redirected. An implementation:

- **MUST NOT** rewrite any `submissionUrls` entry to remove hidden characters. Cleaning
  `https://bloomfield<U+200B>ct.gov/submit` to `bloomfieldct.gov` picks a destination
  host on the author's behalf, which is precisely the decision that must not be made
  automatically.
- **SHOULD** report hidden characters in it as an advisory
  (`SUBMISSION_URL_HIDDEN_CHARS`), since such a URL renders to a reviewer as one host
  while being another.
- **MUST NOT** produce a publisher signature over `submissionUrls` containing them.
  Binding an address that displays as one host and resolves as another defeats the
  binding.

Implementations **SHOULD** also warn at authoring time on mixed-script or
bidirectional content in `metadata.title`, `metadata.publisher`, section titles,
and prompt labels — the text a person reads when deciding whether to trust a
form. These are warnings to the author, before publication, and never
modifications.

Ids are machine keys. Implementations **SHOULD** warn when an id contains
characters outside `[A-Za-z0-9_.-]`, since ids appear in signature payloads,
database columns, and cell addresses.

---


---

## 8. Profile: expressions

**OPTIONAL.** Core implementations skip this section entirely.

### 8.1 What expressions are

Five advisory hints that let a form react to its own answers: showing a field
only when relevant, computing a total, flagging a cross-field inconsistency.

| Hint | Effect when truthy |
|---|---|
| `exprHidden` | Hide this prompt |
| `exprValue` | Computed value (still editable — §8.6) |
| `exprExpected` | Mark as expected (advisory; never blocks) |
| `exprValidation` | Returns a message; `""` means valid |
| `exprReadOnly` | Make read-only |

### 8.2 Language

**APR expressions are CEL** — the Common Expression Language. This specification does
not define the language: grammar, operators, functions, and type rules come from
[cel-spec](https://github.com/cel-expr/cel-spec) (Apache-2.0), and language conformance
is that project's own test suite, which APR neither writes nor maintains.

CEL is non-Turing-complete, terminates by construction, and has no I/O or host access,
which is why it is safe to evaluate on a document from an untrusted sender.

#### 8.2.1 The type environment

CEL is statically typed. `expectedDataType` supplies the types:

| `expectedDataType` | CEL type |
|---|---|
| `number`, `currency` | `double` |
| `boolean` | `bool` |
| `date`, `time`, `datetime` | `timestamp` |
| `multichoice` | `list<string>` |
| everything else, or absent | `string` |

Plus `_this` (the current prompt's response, typed by its own hint), `_today`
(`timestamp`), and `ctx` (`map`).

This is what lets an author write `quantity * unit_price` rather than
`double(quantity) * double(unit_price)`, and what lets a type checker tell them an
expression is wrong before a filler ever sees the form.

#### 8.2.2 Values that will not bind

A response that cannot be converted to its declared type — `"about twelve"` in a
`number` field, an unparseable date, or an empty one — **MUST** be treated as an error,
**never** as a default. The expression errors and degrades to the stored response
(§8.3).

Binding an empty number as `0` would make a blank field silently total as zero: a wrong
answer rather than no answer.

**Nothing about the response changes.** It is stored verbatim, displayed verbatim, and
the document stays valid. It simply does not participate in a calculation — which is
what "advisory" has meant all along. A hint never constrains what may be *stored*; here
it determines what an optional feature can *compute with*. Expressions are themselves
hints (§4.7), so one hint informing another stays inside the advisory layer.

#### 8.2.3 Results

A result is marshalled back to a stored string through the canonical write forms of
§4.9, which serve both directions: `double` per the number rule, `bool` as
`"true"`/`"false"`, `timestamp` as RFC 3339, `list` newline-separated.

#### 8.2.4 Authoring-time checking

An implementation **SHOULD** type-check expressions against the document's type
environment when a template is authored, and report failures to the author with
position information.

This is exactly where §7.3 says strictness belongs. The **author** is stopped and asked
to fix something before publication; the **filler** is never blocked, because at fill
time the same expression degrades to the stored response.

### 8.3 Requirements

- Evaluation **MUST** be pure: no filesystem, network, environment, or host access.
- An implementation **MUST** bound parse depth and regex execution time.
- An expression that fails to parse, references an unknown field, or errors at
  runtime **MUST** degrade to the field's stored `response`. It **MUST NOT**
  throw to the user, block saving, or discard data.
- `exprValue` **MUST NOT** overwrite a non-empty response with an error result.
- A computed value is a **convenience, not an authority**. A consumer **MUST**
  read the stored `response` and **MUST NOT** assume it was recomputed.

### 8.4 Conformance

Conformance splits in two, and only half is APR's.

| Layer | Tested by |
|---|---|
| **Language** — grammar, operators, functions, type rules | cel-spec's own suite. Borrowed, not maintained here |
| **Binding** — type environment, unbindable values, marshalling back, error degradation | `tests/Conformance/v1/expressions/vectors.json` |

A stored `response` remains authoritative over any recomputation. A consumer **MUST**
read the stored value and **MUST NOT** assume anything recomputed it.

### 8.5 A computed value is a suggestion, not a lock

**A computed field MUST remain editable.** Any string is a valid response (§3.3), and a
renderer that refuses to accept typing into a computed field has stopped implementing
the format. A total that is wrong — because the form's arithmetic does not match what
was actually agreed — must be correctable by the person filling it in.

Being computed does not make a field read-only. `exprReadOnly` asks for that
*presentation*, and even then it is an affordance rather than a wall: a renderer
**SHOULD** offer a way in.

**A correction MUST survive recomputation.** `responseMetadata.source` is `"computed"`
when an `exprValue` produced the current response, and absent when a person or an API
wrote it. Recomputation **MUST NOT** overwrite a non-empty response whose `source` is
absent.

Without that distinction a stale computed value and a human correction are
indistinguishable, and the next recompute silently reverts the correction — losing an
answer, which is the one thing this format exists to prevent. Any write to a response
clears `source`, so an authored answer is marked simply by being written.

A reader that ignores `source` still holds a valid document; it will just overwrite
corrections, which is why the rule is stated as a MUST for anything that recomputes.

### 8.6 Migrating from the pre-CEL engine

Earlier revisions shipped a hand-written interpreter with JavaScript-style truthiness,
where a non-empty string was true in a condition. CEL requires `bool` there.

- Bare field references as conditions (`exprHidden: "some_field"`) become type errors.
  Under §8.3 they degrade to the stored response, so a form stops applying a hint rather
  than doing something wrong — detectable, not silent.
- Conversion wrappers become unnecessary where a hint declares the type:
  `quantity == '' || unit_price == '' ? '' : double(quantity) * double(unit_price)`
  is now `quantity * unit_price`, because an empty `number` does not bind and the field
  keeps its stored value.

---

## 9. Profile: signatures

**OPTIONAL.** Core implementations preserve the `signatures` array and report it
as unchecked.

### 9.1 Model

> **BETA profile boundary.** `apr-sig-v3` binds covered content and detects a
> subsequent edit to that content. It does not yet carry the `apr-sig-v4`
> witnessed manifest proposed in [issue #88](https://github.com/marctjones/promptresponse/issues/88),
> which would report fields that appeared or changed outside a signer's scope.
> Implementations and workflows **MUST NOT** represent v3 alone as complete
> attestation history. It remains a useful integrity/provenance profile, but an
> external high-stakes workflow needs its own policy and evidence trail.

Detached CMS/PKCS#7 `SignedData` over a canonical payload, with the signer's
X.509 certificate chain embedded. Verification is a pure computation over bytes
already in the file: **a signed APR document is exactly as safe to open as an
unsigned one.** APR never executes anything.

Two roles:

- **Publisher** — the organization attesting "this is our form, unaltered, and it
  submits here." Covers the form definition and binds `submissionUrls`.
- **Filler** — a person attesting to the responses in their scope. Covers a
  listed set of prompt ids.

### 9.2 Signature object

`id`, `role`, `signer`, `scope` (`"template"` or `"fields"`), `fields[]`,
`algorithm` (default `cms/ecdsa-p256-sha256`), `canonicalization` (**MUST** be
`"apr-sig-v3"`), `signedAt`, `cms` (base64).

A signature **MUST NOT** carry its own copy of the submission URL. It binds
`metadata.submissionUrls` by reading it from the document at both signing and
verification time (§9.3).

A verifier that does not recognize `canonicalization` **MUST** report the
signature as **unverifiable**, never as invalid. "I cannot check this" and "this
is forged" are different statements and **MUST NOT** be conflated in the UI.

The `signer` object is a human-readable projection of the embedded certificate.
The certificate is authoritative; a verifier **MUST NOT** trust `signer` fields
over it, and **MUST** recompute `selfSigned`.

### 9.3 Canonicalization (`apr-sig-v3`)

The signed payload is **not** canonical JSON. It is a fixed, ordered sequence of
`label=base64(value)` lines, deliberately chosen so that any language can
reproduce it byte-for-byte without a canonical-JSON implementation, and so that
signatures survive re-serialization, re-indentation, and key reordering.

**Publisher payload:** `scheme`, `role`, `templateId`, `templateVersion`,
`submissionUrl`, `formDefDigest` (SHA-256 over the canonical form definition),
`signedAt`.

`submissionUrl` here is the ordered `metadata.submissionUrls` array joined with U+001F
(an ASCII control character not permitted in a URI). This preserves the `apr-sig-v3`
payload for an existing one-element array while binding every choice and its order.
A verifier **MUST NOT** recompute it from any other source.

> **Why this is stated so firmly.** An earlier revision stored the URL a second time on
> the signature object and verified against *that* copy. Redirecting
> `metadata.submissionUrls` to another host therefore left the signature reporting
> **valid**, defeating the binding entirely. Two copies of one fact is a correctness
> bug everywhere in this format; here it was a security hole.
> `signatures/tampered-metadata-url.aprt` pins the fix.

**Filler payload:** `scheme`, `role`, `templateId`, `templateVersion`, then each
covered field — **sorted by id, ordinal** — as four lines:

| Line | Covers |
|---|---|
| `field.{id}` | the response |
| `field.{id}.label` | the question as it was worded |
| `field.{id}.type` | `expectedDataType`, since `"10"` means something else when the field is money |
| `field.{id}.options` | `suggestedValues`, joined with U+001F — the shortlist they chose from |

then `signedAt`. Sorting makes the payload independent of the order fields happen
to be listed in.

**A filler signs the question, not only the answer.** Anything less is not a
signature on a form. Only what the person could see and act on is covered —
deliberately *not* the whole document, because a filler signs their part and
someone else editing an unrelated section **MUST NOT** invalidate them.

> **Why `apr-sig-v3` exists.** In `apr-sig-v2` the filler payload was the response
> alone. Sign "No" to *"Have you ever been convicted of a felony?"*, let someone
> afterwards change the label to *"Do you enjoy long walks?"*, and the signature
> still verified — putting a person on record as having answered a question they
> never saw. The same family of bug as the `submissionUrl` hole above: a signature
> verifying over something other than what was presented. The bump also brought the
> bounds family (§4.7) and `role` (§4.10) under the publisher signature; both were
> added after the list of signed hints was written, so a signed template's slider
> could be re-ranged, or a section reassigned from the patient to the office,
> without breaking the signature.

**Form definition digest** covers title, template ids, and the ordered
section/prompt structure with labels, hints (including bounds, §4.7), roles
(§4.10), and table layout. It excludes
responses, response metadata, and the `signatures` array — so filling a form does
not break its publisher signature, and adding a second signature does not break
the first.

It also excludes the `version` field and every unrecognised member (§4.8): the
canonical payload enumerates known fields only. A document re-saved under a later
format version keeps its publisher signature intact — but by the same token,
**extension members on a signed document can be altered without invalidating the
signature**, so a verifier **MUST NOT** treat extension data as attested. A consequence worth knowing is that `version` is not
signature-protected, so a verifier **MUST** decide whether it can read a document
from the version field itself and **MUST NOT** infer that a valid signature
vouches for it.

### 9.4 What signatures do and do not mean

A valid publisher signature means the form definition and the document's
`metadata.submissionUrls` are unaltered since signing. Editing a covered field invalidates the signatures
covering it — by design; that is the detection working.

Signature validity is **independent of certificate trust**. A self-signed
certificate can produce a perfectly valid signature that proves nothing about
identity. Implementations **MUST** report these separately: content validity and
trust are different questions, and collapsing them into one green checkmark
teaches users to trust a checkmark that does not mean what they think.

`signatures/tampered-submission-url.aprt` pins the case that matters: a
structurally valid document whose submission URL was redirected, which **MUST**
fail verification — and which **MUST** still parse, still validate, and still
hand over its data (§9.5).

### 9.5 Signatures never gate the data

**A signature is an assertion about a document, never a permission to read it.**

Both directions of this are normative.

**Signing is never required.** A document without a `signatures` array is a
complete, ordinary, fully valid APR document. An implementation **MUST NOT**
require a signature in order to save, send, accept, or process a form, and
**MUST NOT** present an unsigned document as deficient.

**Acting on a signature is never required.** An implementation **MUST NOT**
refuse to parse, validate, render, print, export, or extract data from a document
because its signatures are absent, unrecognized, expired, untrusted, or outright
invalid. Signature state **MUST NOT** appear in the validation error list (§6.1);
a document whose signature fails is structurally valid and its responses are
readable in full.

An implementation **MAY** warn, badge, or refuse to *act* on a document by its own
policy — a receiving workflow is entitled to reject an unsigned permit
application. That is the workflow's decision, made by the workflow. It is not the
file format's decision, and a reader that enforces it on the workflow's behalf has
taken a choice away from every other consumer of the same file.

The reasoning is the same one behind §3.3. A format that withheld data until a
cryptographic condition was met would fail exactly when it is most needed: an
expired certificate, a verifier that does not recognize `apr-sig-v3`, a corporate
proxy that re-encoded the bytes, an archived form whose signing authority no
longer exists. In every one of those cases the answers a person wrote are still
there, still true, and still the reason the file exists. **The data outlives the
signature, and the format must let it.**

---

## 10. Rendering

APR carries no presentation data. A renderer decides everything, and a GUI, web
page, terminal, voice system, and API client are equally legitimate.

### 10.1 Requirements for renderers

- Section titles and prompt labels **MUST** be presented as the accessible name.
- A placeholder **MUST NOT** be the only label.
- `helpText` **MUST** be programmatically associated with its field, not merely
  adjacent to it.
- Section nesting **MUST** be conveyed structurally (heading levels, groups,
  landmarks), not by indentation alone.
- Every prompt **MUST** be reachable and completable by keyboard.
- A renderer **MUST NOT** block saving because of a hint mismatch.
- Table sections **SHOULD** be presented as tables with header association, not
  as a visual grid.

These are format-level requirements, not house style. APR's structure is what
makes an accessible rendering possible; a renderer that discards it discards the
reason to use APR.

### 10.2 Ordering

Presentation is otherwise free, but **order is data**. A form asks its questions in
a sequence its author chose, and two renderers that disagree about that sequence
are showing two different forms.

1. Sections are presented in array order.
2. Prompts within a section are presented in array order.
3. **A section's own prompts are presented BEFORE its child sections.**

Rule 3 follows document convention: a heading's own content precedes its
subheadings. It is normative, and `valid/section-ordering.aprt` pins it.

A renderer **MAY** paginate, group, or lazily load, but **MUST NOT** reorder. A
wizard that shows one section at a time still visits them in array order.

### 10.3 Export

Exports to PDF, HTML, or print **MAY** introduce layout — page size, margins,
footers. That layout belongs to the renderer's options and **MUST NOT** be
written back into the APR document. The file stays presentation-free no matter
how many ways it has been rendered.

---

## 11. Security considerations

**No executable content.** APR contains no scripts, macros, formulas with host
access, or external references. Opening an APR file from an untrusted sender
executes nothing. This is the format's most important security property and
**MUST NOT** be weakened. Expressions (§8) are pure, bounded, and
non-Turing-complete; they are not an exception to this rule.

**No network access on open.** Reading a document **MUST NOT** fetch anything.
`submissionUrls` is data — no entry **MUST** be contacted without an explicit user
action.

**Resource bounds.** A reader **MUST** bound nesting depth (§4.6) and **SHOULD**
bound document size, and fail cleanly rather than exhausting memory.

**Deceptive text.** §7 requires a filler's response to be preserved and rendered
defensively instead of silently cleaned. Author-supplied fields a machine acts on —
above all `metadata.submissionUrls` — are checked and **refused** at authoring time.
Spending strictness on the answer rather than the submission target protects nothing
and destroys data.

**Signatures are not authorization.** A valid signature proves bytes are
unaltered. It does not establish that the signer is who they claim, that they
were entitled to sign, or that the form should be acted on. Trust evaluation is
the workflow's job (§9.4). Conversely, an absent or failing signature is not a
reason to withhold data from a reader (§9.5) — it is information the reader is
entitled to have alongside the data, not instead of it.

**Responses may be sensitive.** APR files routinely hold personal data in plain
text. The format provides no encryption; protection at rest and in transit is the
surrounding system's responsibility.

---

## 12. Conformance checklist

An implementation claiming **APR 1.0 core** MUST:

- [ ] Parse UTF-8 JSON; reject malformed input rather than coercing it
- [ ] Reject a `response` given as a JSON number or boolean
- [ ] Read `null`/absent `response` as `""`; never write null
- [ ] Apply the compatibility rules in §1.3.1; current writers emit `"1.0-beta"`
- [ ] Treat `documentType` as authoritative; never infer type from a filename
- [ ] Require `metadata.title`, section `id`/`title`, prompt `id`/`label`
- [ ] Enforce document-wide id uniqueness in both namespaces
- [ ] Require content in every section, tables included
- [ ] Treat `kind: "table"` as structure, never as licence for layout data
- [ ] Derive table headers from the corresponding prompts' labels; correspond by position
- [ ] Require `templateId` on a filled form
- [ ] Support at least 16 levels of section nesting
- [ ] Ignore unknown members without rejecting them
- [ ] Degrade an unrecognized `expectedDataType` to text
- [ ] **Never reject, alter, or block a response because of a hint**
- [ ] Never alter a response on the basis of a hint (§7.3.1)
- [ ] Report — never rewrite — hidden characters in every `submissionUrls` entry, and refuse to sign one (§7.3.2)
- [ ] Preserve every response byte-for-byte across a round-trip
- [ ] Preserve `signatures` and `expr*` strings even when not implementing them
- [ ] Never gate parsing, validation, rendering, or data extraction on signature state (§9.5)
- [ ] Pass every fixture in `tests/Conformance/v1/`

Run `python3 scripts/check-schema.py` for the structural half, and the corpus
behaviours in §12 for the rest.

---

## 13. Open questions

Honest list of what 1.0 does not settle.

1. **No registry for extension members.** Preservation (§4.8) and newer-minor
   tolerance (§1.3.1) now make additive change safe, but nothing coordinates *who*
   may add which member name. A reserved prefix or a registry is needed before
   independent parties start extending the format.
2. **Expression interop across implementations** is now testable but untested by a
   second party: the language is CEL, with cel-spec's own suite, and the APR binding has
   published vectors (§8.4). No non-.NET implementation has run either yet.
3. **Submission profiles are deliberately narrow.** `submissionUrls` names explicit
   choices. The stable-beta HTTPS profile POSTs the completed APR JSON as
   `application/vnd.apr+json` to a user-selected `https:` entry, accepts only a 2xx
   response, follows no redirects, has a 30-second client timeout, and never falls
   back to another target. Other transports remain out of scope.
4. **Media type unregistered.** `application/vnd.apr+json` has not been filed
   with IANA.
5. **No governance.** A format used by public institutions eventually needs
   stewardship that is not a single repository.
6. **Attachments** have no representation. A `file` hint stores a reference, and
   what it references is undefined.

---

## Appendix A: Minimal valid document

```json
{
  "version": "1.0-beta",
  "documentType": "template",
  "metadata": { "title": "Contact" },
  "sections": [
    {
      "id": "contact",
      "title": "Contact",
      "prompts": [
        { "id": "full_name", "label": "Full name", "response": "" }
      ]
    }
  ]
}
```

## Appendix B: The rule to remember

If you implement nothing else correctly, implement this:

> **Any string is a valid response, and a hint never says otherwise.**

Everything else in APR is structure. That rule is the point.

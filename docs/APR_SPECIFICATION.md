# APR File Format Specification {#apr-specification}

**Specification document version:** 1.0.0-beta.6-draft
**Describes format version:** `1.0-beta.6`
**Status:** BETA — the format is not frozen and breaking changes are intentional
**Published:** 2026-09-01
**Editor:** Marc Jones
**Normative schema:** `schemas/apr-1.0-beta.6.schema.json`
**Conformance corpus:** `tests/Conformance/beta6/`
**Repository:** <https://github.com/marctjones/promptresponse>

---

## 1. About this document {#scope}

APR (Adaptive Prompt Response) is a file format for forms. An APR document
describes *what to collect*, never *how to display it*. A blank form and a
completed form are the same structure, distinguished by one member.

**This document is the normative definition of APR `1.0-beta.6`.** Everything
else that describes the format is derived from it:

| Artifact | Role |
| --- | --- |
| This document | **Normative.** Defines the format completely: grammar, structure, semantics, and the rules no machine-readable artifact can express. |
| `schemas/apr-1.0-beta.6.schema.json` | **Derived.** A machine-checkable projection of the structural subset. |
| `schemas/apr-types-1.0.json` | **Derived.** A machine-readable projection of the type registry ([Hints](#hints-object)). |
| `tests/Conformance/beta6/` | **Derived.** Executable vectors exercising the rules stated here. |

Where a derived artifact disagrees with this document, **the derived artifact has
the defect**. A schema that admits something this document forbids is a schema
bug; a fixture that expects something this document does not require is a corpus
bug.

> Rationale: a schema can only state what a schema can state. This one cannot
> express that ids are unique document-wide, that any string is a valid response,
> that hints never enforce, or that attestation state never gates data — and its
> own `$comment` says so. Ranking it above this document would leave every rule
> it cannot express with no authority at all. The corpus is ranked below for a
> different reason: it is a finite set of examples, and no finite set of examples
> defines a format.

**A failing fixture is still evidence.** Ranking the corpus below this document
does not make it less useful — it is usually the fastest way to discover that a
sentence here is wrong. The ordering says only which artifact gets corrected once
the disagreement is understood.

> Decision (beta.6): this inverts the earlier ordering, under which the corpus
> outranked the schema, which outranked this prose. That ordering suited a
> descriptive specification that documented what had been built. This document is
> no longer descriptive: it states what APR is, and an implementation, a schema,
> or a fixture that departs from it is wrong rather than authoritative.

This document has not been ratified. Nothing in it designates APR 1.0.

### 1.1 Navigation {#navigation}

Read the specification in this order when implementing APR:

1. [Conformance profiles](#conformance) identify the required and optional
   behaviour.
2. [Representations](#representations), [document structure](#form-model),
   [document type](#media-types), [validation](#validation), and
   [text handling](#text-handling) define `core`.
3. [Streams](#streams) and [digests](#digests) define `core+streams`.
4. [Expressions](#expressions) and [attestations](#attestations) are the
   remaining optional profiles.
5. [Rendering](#renderers), [security](#security), and the
   [conformance checklist](#checklist) constrain hosts and verify an
   implementation.

This navigation is informative. The requirement language in the sections it
links to remains authoritative.

### 1.2 Requirement language {#normative-language}

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**,
**SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** are to be
interpreted as described in BCP 14 (RFC 2119, RFC 8174) when, and only when,
they appear in all capitals.

Lowercase uses carry their ordinary English meaning and impose no requirement.

### 1.3 Document conventions {#conventions}

Four kinds of text appear here and are distinguished deliberately.

- **Normative text** states requirements, using the keywords of
  [Requirement language](#normative-language).
- **Examples** are captioned `Example N` and are illustrative. Where an example
  and normative text disagree, the normative text governs.
- **Rationale** appears in blockquotes beginning `Rationale:` and is
  non-normative. Removing every rationale block would not change the format.
- **Decisions** appear in blockquotes beginning `Decision (beta.6):` and mark a
  rule this document originates rather than inherits. They are normative, and
  marked so that every such rule can be found.

Each heading carries an explicit anchor, written `{#anchor-name}`.

**Every normative clause carries a rule identifier**, written `[APR-AREA-NNN]` at
the end of the requirement it names. A test, a coverage manifest, or a defect
report cites the identifier rather than a section, so what is being referred to
does not depend on where it currently sits.

Identifiers are append-only. A new rule takes the next free number in its area, a
deleted rule's number is retired rather than reused, and moving a rule between
sections does not renumber it. Nothing about an identifier is positional, so
inserting a requirement cannot renumber its neighbours.

> Rationale: CommonMark numbers its examples and YAML numbers its grammar
> productions, and in both the numbered thing — not the section — is the unit a
> test cites. APR states most of its requirements in prose, so the prose needs
> the same treatment.

> Rationale: **anchors, not section numbers, are the stable identifiers.**
> Section numbers renumber whenever material is inserted, so a citation to a
> number silently comes to mean something else, while one to `#responses` either
> resolves or fails loudly. Cite anchors.

### 1.4 Three version numbers {#version-numbers}

Conflating these is the most common way to break a file format, so they are kept
strictly apart.

| Number | Changes | Lives in | Today |
| --- | --- | --- | --- |
| **Format version** | only on a breaking change to the wire format | the `aprVersion` member of every record | `1.0-beta.6` |
| **Specification document version** | every release | this document's header | `1.0.0-beta.6-draft` |
| **Conformance corpus tag** | every release | `tests/Conformance/beta6/` and a git tag | `corpus/beta6` |

The format version **MUST NOT** track releases. Two releases that do not change
the wire format declare the same format version, and that is correct. [APR-SEC-001]

#### 1.4.1 Version compatibility {#version-compatibility}

`aprVersion` **MUST** be exactly `"1.0-beta.6"`. A record declaring any other
value **MUST** be rejected with `UNSUPPORTED_VERSION`, including `1.0-beta`,
`1.0-beta.3`, and any later beta. [APR-SEC-002]

> Decision (beta.6): the member is named `aprVersion`, not `version`. A bare
> `version` in a document that also carries `templateVersion` reads as though
> it were the form author's own version number, which is the one thing it is
> not. The name says whose version it is.

> Decision (beta.6): version handling is **exact-match rejection**. An earlier
> baseline decided compatibility by MAJOR.MINOR, so that a newer MINOR was read
> with a warning and its unknown members preserved. That tolerance is withdrawn.
>
> The reason is that beta.6 changes the meaning of an existing document rather
> than adding to it: embedded signatures are retired, and a beta.3 document read
> as beta.6 would silently lose its cryptographic assertions. Reading it "with a
> warning" would be worse than refusing it. Negotiation returns when there is a
> released version to negotiate with.

Unknown-member preservation ([Unknown members](#extensions)) is unaffected and
remains **REQUIRED**. It is what keeps additive change safe within a version. [APR-SEC-003]

**What BETA means here:** breaking changes are intentional until the first
public release. Implementers **SHOULD** record the corpus tag they pass, not
just the format version. [APR-SEC-004]

---

## 2. Terminology {#terminology}

These terms carry the meanings below throughout. Where a term is also an
ordinary English word, the definition here governs.

**form** — the complete semantic model of one APR document: its version,
document type, metadata, section tree, and roles. A form is ordinary data and
carries no cryptographic assertion of its own.

**template** — a form whose `documentType` is `template`: the questions, without
any particular respondent's answers.

**filled form** — a form whose `documentType` is `filledForm`: the questions
together with one respondent's answers, naming the template it answers.

**section** — a named node of the form tree, holding prompts, child sections, or
both.

**prompt** — a single question, owning a label, an optional response, and
optional advisory hints.

**response** — the answer stored for a prompt, always a string.

**hint** — advisory guidance attached to a prompt, describing how a response
might be presented or checked, never whether it is acceptable.

**advisory** — never rejecting, altering, or blocking a response. The opposite
of binding, not of important.

**instance** — one repetition of a table section's shape.

**semantic model** — the information APR defines, independent of how it is
written down. Two documents with the same semantic model are the same form.

**source trivia** — everything in the bytes but absent from the semantic model:
comments, whitespace, indentation, quoting style, mapping order, and record
framing. Trivia carries no APR meaning.

**representation** — a concrete spelling of the semantic model. APR defines two,
APR-JSONC and APR-YAML.

**record** — one complete unit in a stream: either a whole form or one
attestation.

**stream** — an ordered transport carrying independent records. Order is
presentation, not meaning.

**occurrence** — one appearance of a form in a stream. Two occurrences with
identical semantic models remain two occurrences; an occurrence is neither a
copy to be collapsed nor a revision of an earlier one.

**digest** — the value identifying a semantic model, computed over its canonical
serialization rather than over its bytes.

**manifest** — the list of digests describing a form's parts, holding no
plaintext of the values it describes.

**attestation** — an independent record making a cryptographic assertion about a
form identified by digest.

**envelope** — an attestation record excluding its own proofs; this is what a
proof signs and what a witness references.

**subject** — the form an attestation asserts about, identified by digest.

**scope** — how much of the subject an attestation covers: the whole document,
or a named set of prompts.

**proof** — a verifiable assertion over an envelope.

**witness** — a reference from one attestation to the digest of an earlier
envelope, recording that its signer saw that assertion.

**profile** — an optional conformance capability an implementation may claim.
Optional to claim; binding once claimed.

**extension member** — a member APR does not define, named with a reverse-DNS
prefix by the producer that owns it, carried by a document and preserved across
a round trip ([Unknown members](#extensions)).

**non-blank string** — a string containing at least one non-whitespace
character. A whitespace-only string is treated as absent.

---

## 3. Conformance profiles {#conformance}

APR is deliberately layered so that a complete, useful implementation can be
written in an afternoon, in any language, on any device. Only the core is
required.

### 3.1 `core` — REQUIRED of every implementation {#profile-core}

Parse, validate, fill, and write one document in both representations, per
[Representations](#representations) through [Text handling](#text-handling).

A core implementation is fully conformant. It is not a degraded one, and it need
not emit HTML, PDF, or native controls. It exposes the semantic document and its
advisory hints for a host application or renderer to use.

### 3.2 `core+streams` — OPTIONAL {#profile-streams}

Additionally reads and writes streams of independent records
([Streams](#streams)).

A core-only implementation given a stream **MUST** report
`APR_STREAM_REQUIRES_ITERATION` and **MUST NOT** select a record by position. [APR-CONF-001]

### 3.3 `core+attestations` — OPTIONAL {#profile-attestations}

Additionally computes semantic digests and manifests, resolves attestations
against forms, looks up witnesses, and reports the verification vocabulary
([Attestations](#attestations)). Requires `core+streams`.

A core-only implementation **MUST NOT** reject a stream containing attestations,
and **MUST** preserve attestation records on round-trip. [APR-CONF-002]

It **MUST NOT** report a document as verified, and **SHOULD** indicate that
attestations are present but unchecked. Saying nothing is better than saying
verified; saying "present, unchecked" is better than both. [APR-CONF-005]

This profile is optional for a reason of policy, not merely of cost. **Nobody is
obliged to sign, and nobody is obliged to care that something was signed.** A
recipient may have every reason to trust a document by other means — they know
the sender, they requested the form, the data is low-stakes, or they simply want
to read it. Requiring verification before data can be used would impose the form
author's threat model on every reader, which is not a decision the file format
gets to make. See [Attestations never gate the data](#never-gate).

### 3.4 `core+expressions` — OPTIONAL {#profile-expressions}

Additionally evaluates the `expr*` hint family ([Expressions](#expressions)).

A core-only implementation **MUST NOT** reject a document that uses expressions
and **MUST** preserve the expression strings when writing it back. A host
rendering the document presents those prompts as ordinary editable fields: a
computed field simply becomes a field the user can type into — degraded, but
never broken, and never lost. [APR-CONF-003]

### 3.5 Declaring conformance {#declaring-conformance}

State the profiles you implement, the submission transports you implement
(`https`, `mailto`, both, or none — [Submission targets](#submission)), and the
corpus commit you pass. "APR 1.0-beta.6 core+streams, submits https, corpus
beta6 @ `<sha>`" is a complete and honest claim.

An implementation **MUST NOT** claim a profile without passing the corpus
revision it names. [APR-CONF-004]

---

## 4. Representations {#representations}

The semantic model is representation-neutral. The same form or attestation may
be written as APR-JSONC or APR-YAML; comments, whitespace, indentation, scalar
spelling, and mapping order have no semantic effect.

### 4.1 Model, serialization, presentation {#model-layers}

APR separates three layers. A rule stated at one layer does not constrain
another.

1. **Semantic model** — the information APR defines. All APR semantics are
   properties of this layer.
2. **Serialization** — the JSON data model the semantic model maps onto:
   objects, arrays, strings, numbers, booleans, and null.
3. **Presentation** — the bytes actually written.

Two documents with the same semantic model are the same form, whatever their
presentation. This is what makes a semantic digest ([Digests](#digests))
meaningful: it is computed over the model, never over the bytes.

Information is discarded deliberately when moving from presentation to model.
Source trivia **MUST NOT** carry APR meaning, and a writer is under no obligation
to reproduce it. Everything else — including members APR does not define — **MUST**
survive a round trip. [APR-REP-001]

### 4.2 Syntax conventions {#syntax-conventions}

APR does not define a grammar of its own. It defines a **delta** against
grammars that already exist, so that an implementer reuses a JSON or YAML parser
rather than writing an APR one.

Grammar in this document is ABNF (RFC 5234). Two conventions apply:

- **Rules are imported by name.** A rule referenced but not defined here is the
  rule of that name in the cited specification, unchanged. `value`, `string`,
  `number`, `member`, and the structural characters are RFC 8259's.
- **A redefinition replaces the imported rule wherever it appears.** Where this
  document defines a rule that the cited specification also defines, the
  definition here governs for APR, and every imported rule that references it
  picks up the replacement.

Nothing else in the imported grammar changes. A construct this document does not
mention is permitted exactly as the cited specification permits it, and one it
excludes is excluded wherever it would otherwise appear.

### 4.3 Encoding {#encoding}

A document **MUST** be encoded as UTF-8 (RFC 3629). A byte-order mark
**SHOULD NOT** be written; a reader **SHOULD** tolerate a leading one. [APR-REP-002]

A reader **MUST** reject ill-formed UTF-8 rather than substituting replacement
characters silently. [APR-REP-003]

Every string **MUST NOT** contain U+0000, and **MUST NOT** contain an unpaired
surrogate in the range U+D800 to U+DFFF. Control characters U+0001 through
U+001F **MUST NOT** appear, except tab (U+0009), line feed (U+000A), and
carriage return (U+000D), which are permitted so that a multiline response can
hold the line breaks a person typed. [APR-REP-004]

### 4.4 APR-JSONC {#apr-jsonc}

APR-JSONC is the JSON grammar of RFC 8259 with comments and trailing commas
admitted. It is defined as a delta against that grammar, per
[Syntax conventions](#syntax-conventions).

```abnf
; Imported unchanged from RFC 8259: value, member, string, number,
; begin-object, end-object, begin-array, end-array,
; name-separator, value-separator, and everything they reference.

apr-jsonc-text  = ws value ws

; REDEFINED. Whitespace admits comments, so a comment is legal
; wherever whitespace is, and nowhere else.
ws              = *( %x20 / %x09 / %x0A / %x0D / comment )

comment         = line-comment / block-comment
line-comment    = %x2F.2F *( %x00-09 / %x0B-10FFFF )
block-comment   = %x2F.2A *( not-star / star-not-slash ) %x2A.2F
not-star        = %x00-29 / %x2B-10FFFF
star-not-slash  = %x2A ( %x00-2E / %x30-10FFFF )

; REDEFINED. A trailing comma is permitted after the final element.
object          = begin-object
                  [ member *( value-separator member ) [ value-separator ] ]
                  end-object
array           = begin-array
                  [ value *( value-separator value ) [ value-separator ] ]
                  end-array
```

Because a comment is a production of `ws`, and `ws` never appears inside
`string`, **a comment sequence inside a string literal is not a comment.** The
`string` rule is imported unchanged, so `"// not a comment"` is an ordinary
string value. This is the first question an implementer asks, and the grammar
answers it rather than leaving it to prose.

Once comments and trailing commas are removed, the text **MUST** decode as JSON
(RFC 8259) to the semantic model. Comments are source trivia and cannot carry
APR meaning. [APR-REP-005]

**One constraint the grammar cannot express.** RFC 8259 §4 says object member
names SHOULD be unique. APR raises this: a parser **MUST** reject a duplicate
member name rather than applying a last-key-wins rule. [APR-REP-006]

> Rationale: last-key-wins makes a document's meaning depend on which parser
> reads it, which is precisely what a semantic digest cannot tolerate.

*Negative case:* `malformed/duplicate-member.apr.jsonc`.

These examples are executable. `scripts/extract-spec-examples.py` derives the
conformance vectors from them, so a change to the rule and a change to its
evidence are the same edit ([Authority](#scope)).

```apr-example
id: jsonc-trailing-comma
rule: apr-jsonc
rules: APR-REP-005
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Trailing commas" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" }, ] },
  ],
}
```

A comment sequence inside a string is not a comment, because `ws` never occurs
inside `string`.

```apr-example
id: jsonc-comment-inside-string
rule: apr-jsonc
rules: APR-REP-005
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "// not a comment /* nor this */" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

```apr-example
id: jsonc-duplicate-member
rule: apr-jsonc
rules: APR-REP-006
representation: jsonc
expect: reject
diagnostic: DUPLICATE_MEMBER
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "first" },
  "metadata": { "title": "second" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```


**Example 1.** A form in APR-JSONC, with comments that carry no meaning.

```jsonc
{
  // Comments are trivia: they survive a byte-level copy and vanish from
  // the semantic model. They are never hashed and never attested.
  "aprVersion": "1.0-beta.6",
  "documentType": "template",
  "metadata": { "title": "Permit Application" },
  "sections": [
    {
      "id": "applicant",
      "title": "Applicant",
      "prompts": [
        { "id": "full_name", "label": "Full name", "response": "" },
      ],
    },
  ],
}
```

### 4.5 APR-YAML {#apr-yaml}

APR-YAML is **YAML 1.2.2 syntax carrying the JSON value space**. Its syntax is
referenced rather than restated; only its resolution and its exclusions are
stated here, because those are the parts APR constrains.

A conforming APR-YAML document:

1. **MUST** be a well-formed YAML 1.2.2 document, per that specification's
   character, structural, flow, block, and document-stream productions
   (chapters 5 to 9). [APR-REP-007]
2. **MUST** resolve every scalar to a value of the JSON data model (RFC 8259):
   null, boolean, number, string, array, or object, and nothing else. [APR-REP-008]
3. **MUST** resolve every mapping key to a string. [APR-REP-009]
4. **MUST NOT** use the constructs excluded below. [APR-REP-010]

### 4.5.1 Scalar resolution {#yaml-resolution}

Resolution is stated exhaustively, because it is where YAML and JSON genuinely
differ and where a reference alone would be ambiguous.

| Scalar | Resolves to |
| --- | --- |
| Any quoted scalar | string, verbatim |
| Plain `null`, `Null`, `NULL`, `~`, or empty | null |
| Plain `true`, `True`, `TRUE`, `false`, `False`, `FALSE` | boolean |
| A plain scalar matching JSON's `number` production (RFC 8259 §6) | number |
| **Any other plain scalar** | **string** |

A plain scalar denoting a non-finite float — `.inf`, `-.inf`, `.nan`, in any
capitalization — **MUST** be rejected. JSON has no representation for it, so
there is no value for it to resolve to. [APR-REP-011]

**APR defines its own YAML schema.** YAML 1.2.2 chapter 10 presents failsafe,
JSON, and core as *recommended* schemas rather than mandatory ones, and a
processor may define another. The table above is APR's.

An implementation therefore uses a YAML library for **syntax** — characters,
structure, flow and block style, document streams — and **MUST NOT** use that
library's default scalar resolution. [APR-REP-012]

> Rationale: this is close to YAML's Core Schema, restricted to what JSON can
> represent. The narrower JSON Schema is deliberately *not* used: under it a
> plain scalar that is not a literal has no resolution at all, so `title: Permit
> Application` would be invalid and every string in an APR-YAML document would
> have to be quoted. That is not a document anyone would write.
>
> Choosing a YAML 1.2 library instead of a 1.1 one does not remove the need for
> this table. Under YAML 1.2's Core Schema `012` resolves as the number 12 and
> `.inf` as a float, so a leading-zero identifier is still corrupted and a
> non-finite value still appears. The distance being closed here is not between
> YAML versions; it is between any YAML schema and the JSON value space.

Because the target is the JSON value space rather than YAML's, an entire class of
YAML-only behaviour disappears without APR enumerating it. Sexagesimals,
implicit timestamps, and the YAML 1.1 `y`/`n`/`on`/`off` boolean spellings are
not JSON values and therefore resolve as ordinary strings.

**Excluded constructs.** These are structural rather than resolution behaviour,
so the JSON Schema does not exclude them and this document must:

| Excluded | Defined in YAML 1.2.2 | Example |
| --- | --- | --- |
| Anchors and aliases | §3.2.2.2, node properties and alias nodes | `yaml-anchor` |
| Tags, including `!!binary` and language-specific tags | §3.2.1.2, node properties | `yaml-tag` |
| Merge keys | the `<<` mapping-merge convention | `yaml-merge-key` |
| Directives, including `%YAML` and `%TAG` | §6.8 | `yaml-directive` |

Implementations **MUST** use a safe loader: one that constructs only the JSON
Schema value types and never instantiates a host-language object from document
content. [APR-REP-013]

Responses remain strings even where a scalar would otherwise resolve as a number
or boolean under the JSON Schema, because
[Responses are strings](#responses) governs the semantic model regardless of how
a scalar resolved. Anywhere else, an author who means the *string* `true` or
`25` — as a suggested value, say — **MUST** quote it, exactly as a JSON author
must; the table above is the only thing that decides. [APR-REP-017]


The excluded constructs, each with its vector.

```apr-example
id: yaml-anchor
rule: apr-yaml
rules: APR-REP-010
representation: yaml
expect: reject
diagnostic: YAML_ANCHOR_FORBIDDEN
---
aprVersion: "1.0-beta.6"
metadata: &meta
  title: Anchored
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

```apr-example
id: yaml-tag
rule: apr-yaml
rules: APR-REP-010
representation: yaml
expect: reject
diagnostic: YAML_TAG_FORBIDDEN
---
aprVersion: "1.0-beta.6"
metadata:
  title: !!str Tagged
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

```apr-example
id: yaml-merge-key
rule: apr-yaml
rules: APR-REP-010
representation: yaml
expect: reject
diagnostic: YAML_MERGE_KEY_FORBIDDEN
---
aprVersion: "1.0-beta.6"
metadata:
  title: Merged
sections:
  - id: s
    title: S
    <<: { description: merged in }
    prompts:
      - id: p
        label: P
```

```apr-example
id: yaml-directive
rule: apr-yaml
rules: APR-REP-010
representation: yaml
expect: reject
diagnostic: YAML_DIRECTIVE_FORBIDDEN
---
%YAML 1.2
---
aprVersion: "1.0-beta.6"
metadata:
  title: Directed
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

Resolution, including the cases that separate APR from YAML's own schemas. A bare
word is a string, and a non-finite float has no JSON value to resolve to.

```apr-example
id: yaml-bare-word-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Permit Application
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: about twelve
```

```apr-example
id: yaml-legacy-boolean-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Legacy spellings
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: yes
```

```apr-example
id: yaml-leading-zero-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Leading zero
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: 012
```

```apr-example
id: yaml-date-like-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Date-like
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: 2026-01-01
```

These pin the surface where YAML schemas and the JSON value space differ. Each
is a spelling some YAML schema resolves to a non-string; none is a JSON number,
so each is a string here.

A table is a structural claim, and an unregistered affordance degrades rather
than failing.

```apr-example
id: table-section
rule: tables
rules: APR-MODEL-038, APR-REP-015
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Expenses" },
  "sections": [
    {
      "id": "expenses",
      "title": "Expense line items",
      "kind": "table",
      "canAddRows": true,
      "maxRows": 25,
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
  ]
}
```

```apr-example
id: structural-member-wrong-type
rule: json-subset
rules: APR-REP-015
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Stringly typed" },
  "sections": [
    {
      "id": "rows",
      "title": "Rows",
      "kind": "table",
      "canAddRows": "true",
      "maxRows": "25",
      "sections": [
        { "id": "row_1", "title": "Row 1",
          "prompts": [ { "id": "row_1.note", "label": "Note" } ] }
      ]
    }
  ]
}
```

```apr-example
id: unregistered-data-type-degrades
rule: hints-object
rules: APR-MODEL-018
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Unregistered affordance" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "p", "label": "P", "response": "anything",
          "hints": { "expectedDataType": "holographic-signature" } }
      ]
    }
  ]
}
```

```apr-example
id: yaml-sexagesimal-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Resolution
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: 1:30
```

```apr-example
id: yaml-hex-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Resolution
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: 0x1F
```

```apr-example
id: yaml-underscored-number-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Resolution
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: 1_000
```

```apr-example
id: yaml-bare-decimal-is-a-string
rule: yaml-resolution
rules: APR-REP-008, APR-REP-012
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Resolution
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: .5
```

```apr-example
id: yaml-quoted-null-is-a-string
rule: yaml-resolution
rules: APR-REP-008
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Resolution
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: "null"
```

A plain `null` is the null value, and a null response reads as the empty string.

```apr-example
id: yaml-plain-null-response-is-empty
rule: yaml-resolution
rules: APR-REP-008, APR-REP-014
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Resolution
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: null
```

```apr-example
id: yaml-non-finite-float
rule: yaml-resolution
rules: APR-REP-011
representation: yaml
expect: reject
diagnostic: YAML_NON_FINITE_NUMBER
---
aprVersion: "1.0-beta.6"
metadata:
  title: Non-finite
sections:
  - id: s
    title: S
    kind: table
    maxRows: .inf
    prompts:
      - id: p
        label: P
```

Every exclusion above names the example that exercises it, and those examples are
in this document ([Authority](#scope)).

**Example 2.** The same form as Example 1, in APR-YAML. Both have identical
semantic models and therefore identical digests.

```yaml
aprVersion: "1.0-beta.6"
documentType: template
metadata:
  title: Permit Application
sections:
  - id: applicant
    title: Applicant
    prompts:
      - id: full_name
        label: Full name
        response: ""
```

### 4.6 Value types {#json-subset}

APR uses a restricted subset of the JSON data model.

A **response** is always a JSON string ([Responses are strings](#responses)). It
is never a number, boolean, array, or object, whatever the prompt's advisory
type suggests.

Every other member is **structural**: it describes the form rather than carrying
what a person typed. A structural member uses the **JSON type that fits it**,
natively: a flag is a JSON boolean, a count is a JSON integer, a numeric bound
is a JSON number, and a name, identifier, or piece of text is a JSON string. A
writer **MUST** emit each structural member in the type its member table
declares, and a reader **MUST** report a structural member of another type as
`WRONG_TYPE` ([Errors](#structural-validation)). `"canAddRows": "true"`
is not a boolean and `"maxRows": "25"` is not a count. [APR-REP-015]

Where JSON has no type for a value, the member is a string in a stated form:
timestamps are RFC 3339 strings, and a bound on a temporal field is a string in
that field's canonical write form ([Canonical value forms](#canonical-values)).
A structural member is never a string *because* it is structural; it is a
string only when a string is the fitting type. [APR-REP-016]

> Rationale: the strings-only rule once applied to the whole document, and the
> reason for it never did. A response is a string because it carries what a
> person typed, and typing produces text. A row count does not come from a
> person; spelling it `"25"` obliges every reader to parse a number out of a
> string and to decide what `"25.0"` or `" 25"` mean, which is exactly the class
> of silent divergence the format exists to remove. Native types give the schema
> the check and give readers nothing to interpret.

`null` is not an APR value. A writer **MUST NOT** emit it. A reader tolerates it
in a response position only, coercing it to the empty string; anywhere else it is
a parse failure. [APR-REP-014]

### 4.7 Responses are strings {#responses}

A `prompt.response` **MUST** be a JSON string, and a response given as a JSON
number or boolean **MUST** be rejected at parse time. [APR-MODEL-001]

It **MUST NOT** be coerced to `"42"` or `"true"`. Rejecting and coercing are
different failures: one refuses the document, the other accepts it having invented
data. [APR-MODEL-049]

> Rationale: silent coercion is worse than rejection. It produces a document that
> looks conformant while having invented data that no person entered.

A `null` response and an absent `response` member are both read as the empty
string.

### 4.8 Any string is a valid response {#any-string}

**This is the rule the rest of the format exists to protect.**

A response **MAY** contain any string. The format has no opinion about whether
that string is "correct". [APR-MODEL-002]

| `expectedDataType` | Response | Document validity |
| --- | --- | --- |
| `number` | `about twelve` | **Valid** |
| `email` | `call me instead` | **Valid** |
| `date` | `the summer of 1985` | **Valid** |
| anything | empty | **Valid** |

The distinction is between **document validity** — is this well-formed APR? —
and **workflow acceptance** — will the receiving office act on it? A benefits
office may reject a form for a blank field or an unparseable date. That is a
workflow decision, made by a workflow, and it has nothing to do with whether the
document is valid APR.

> Rationale: forms are filled by people under conditions the author did not
> anticipate. Someone whose legal name does not fit the field, whose address is
> not a street address, whose answer is "I don't know" — all produce valid APR. A
> format that rejected them would discard true information because it was
> inconveniently shaped.

### 4.9 Hints never enforce {#hints-advisory}

Every member of `prompt.hints` is advisory. A hint **MUST NOT** cause a response
to be rejected, altered, truncated, or blocked from being saved. This applies to
`validationPattern` — a non-matching response is a warning at most — and to every
member of the `expr*` family. [APR-MODEL-003]

An implementation **MAY** surface a hint mismatch as an advisory warning. It
**MUST NOT** prevent the user from saving. [APR-MODEL-004]

---

## 5. Document structure {#form-model}

### 5.1 Document {#root-object}

**Example 3.** The shape of a form.

```jsonc
{
  "aprVersion": "1.0-beta.6",
  "documentType": "template",
  "metadata": { "title": "Permit Application" },
  "sections": [ /* ... */ ],
  "roles": [ /* ... */ ]
}
```

| Member | Type | Required | Notes |
| --- | --- | --- | --- |
| `aprVersion` | string | **Yes** | Exactly `"1.0-beta.6"` — the version of *this specification* the record is written to, never the form's own ([Version compatibility](#version-compatibility)). |
| `documentType` | string | No | `template` or `filledForm`. Absent means `template`. Authoritative — see [Document type](#media-types). |
| `metadata` | object | **Yes** | [Metadata](#metadata) |
| `sections` | array | **Yes** | **MUST** contain at least one section. [APR-MODEL-005] |
| `roles` | array | No | [Roles](#roles) |

A form **MUST NOT** carry a `signatures` member. A reader encountering one
**MUST** report `RETIRED_EMBEDDED_SIGNATURES`
([Retired members](#retired-members)). [APR-MODEL-006]

### 5.2 Metadata {#metadata}

| Member | Type | Required | Notes |
| --- | --- | --- | --- |
| `title` | non-blank string | **Yes** | The form's name. |
| `description` | string | No | Prose about the form as a whole. |
| `created` | date-time | No | RFC 3339. |
| `modified` | date-time | No | RFC 3339. |
| `author` | string | No | A person. |
| `publisher` | string | No | The organization standing behind the form. |
| `templateId` | string | No | Required on a `filledForm`. A URI identifying the template it answers ([Template identity](#template-identity)). |
| `templateVersion` | string | No | The template revision answered. |
| `submissionUrls` | array of string | No | Ordered explicit delivery choices; `https` or `mailto` ([Submission targets](#submission)). |
| `regarding` | array of string | No | Digests of the records this form was completed with reference to ([Related records](#regarding)). |

`title` **MUST** contain a non-whitespace character. [APR-MODEL-007]

`submissionUrls`, when present, **MUST** be an ordered array of strings. Even one
delivery choice is represented as a one-element array; a scalar `submissionUrl`
**MUST NOT** be written, and a reader encountering one **MUST** treat it as an
unknown member rather than honouring it. [APR-MODEL-052] Order is the author's preferred display order, never permission for a
client to choose or fall back to a target automatically; submitting remains an
explicit user action.

When `documentType` is `filledForm`, `templateId` is **REQUIRED**: a completed
form that cannot name the form it completes is not traceable. [APR-MODEL-008]

**Template identity.** {#template-identity} `templateId` **MUST** be a URI
(RFC 3986). It identifies; it need not resolve. The **RECOMMENDED** form is a
tag URI (RFC 4151), `tag:skpt.cl,2026:dog-license`, which is unique by
construction — it is minted from a domain or email address the author held on
a date — and needs no server. Any URI the author controls the uniqueness of is
acceptable, an email address as a `mailto` URI included. A reader **MUST NOT**
fetch a `templateId`. [APR-MODEL-036]

> Rationale: a filled form is consumed by the template it answers, so the
> identifier has to be unique across every author who will ever publish a
> form. Relying on a URI puts that uniqueness on a namespace someone already
> owns rather than on a registry. `file:///example.apr` is a valid URI and a
> poor identifier, and the format does not stop an author choosing badly; it
> only gives them a good form to choose.

**Workflow state is not form data.** When a form was received, by whom, and
what happened to it next are facts the *receiver* tracks against the form,
not members of it. This document defines no member for them, and a workflow
that needs to record them writes its own form and names what that form was
about ([Related records](#regarding)), never adding them to a form somebody
else wrote. [APR-MODEL-037]

> Rationale: an earlier draft carried `filledBy` and `filledDate`. They were
> written by the wrong party — the filler asserting facts about the
> submission — and sat inside the object an attestation covers, so a
> receiver's bookkeeping could not change without disturbing a signature.

#### 5.2.1 Submission targets {#submission}

A `submissionUrls` entry is one of exactly two kinds, told apart by its scheme.
This document defines no other transport. [APR-MODEL-032]

| Scheme | Meaning | Defined by |
| --- | --- | --- |
| `https` | A pre-signed object-store PUT target | HTTP (RFC 9110); the S3 pre-signed URL convention |
| `mailto` | An email address to attach the document to | RFC 6068 |

**`https`.** The entry is a URL to which the complete document is delivered by a
single HTTP `PUT`, with the document as the body and the `vnd.apr` media type
of its representation as `Content-Type` ([Document type](#media-types)). The
URL is used verbatim, query string included: this is the contract of an S3
pre-signed PUT URL, which carries its own authorisation and expiry in the
query, and any receiver that accepts a plain `PUT` of a body satisfies it
identically. A client **MUST** send only the document — no credentials,
cookies, or headers derived from it — **MUST** treat any status other than
2xx as failure, **MUST NOT** follow a redirect, and **MUST NOT** retry
without a fresh user action. [APR-MODEL-033]

**`mailto`.** The entry is an RFC 6068 address, with any header fields it
carries such as `subject` passed through. Submitting means composing a message
to that address with the **stream** as a single attachment, never inlined in
the body; a lone form is a one-record stream. A client **SHOULD** hand the
composition to the user's mail client, and **MAY** instead send natively if it
has that ability; either way the message leaves only on an explicit user
action. [APR-MODEL-034]

What the receiver holds after a `PUT` is the request body, byte for byte: the
stream as the client wrote it, already a valid APR file. No processing on the
receiving side is assumed or permitted to be needed. A WebDAV collection
(RFC 4918) is an ordinary `PUT` target and needs no separate treatment.

Each transport is **OPTIONAL** and claimed separately
([Declaring conformance](#declaring-conformance)). A client **MUST NOT** act
on an entry whose scheme it does not recognise or does not implement, and a
validator **SHOULD** report an entry of a scheme this document does not define
as `SUBMISSION_URL_UNSUPPORTED` ([Warnings](#warnings)). An `http` entry is
unsupported. [APR-MODEL-035]

> Rationale: the format defines *where* a completed form may go and borrows
> *how* from transports that already exist, rather than specifying one. A
> pre-signed PUT is the industry's common way to accept an upload without
> handing out credentials, and every S3-compatible store implements it; a
> `mailto` address is the way an office without any server at all still
> receives forms. Redirects are refused for the same reason hidden characters
> are reported: following one delivers the form to a host the author never
> named. A pre-signed browser POST is deliberately absent — it needs policy
> fields beyond the URL, which a string entry cannot carry, and every
> S3-compatible store accepts a pre-signed PUT. No authentication step is
> defined because a pre-signed URL *is* the authorisation: the grant travels
> in the query string, so the client never holds a credential.

#### 5.2.2 Related records {#regarding}

`regarding` names the records this form was completed with reference to, each
by its semantic digest ([Digests](#digests)).

A step of a process is an ordinary form. The office publishes a template for
it — a receipt, a review, a routing decision — somebody fills that template in
while looking at what came before, and the form they produce names what they
looked at. Every record they looked at is left exactly as it was, so its digest
holds and the attestations over it stay `valid`.

`regarding` is an ordered, duplicate-free array of digest strings, each
matching the digest form ([Digests](#digests)). Order is the author's preferred
display order and means nothing else. An entry **MAY** name a form or an
attestation, so a form can pin not only what it was about but the attestation
state it was about. [APR-MODEL-043]

**A reference asserts context and nothing else.** It records that whoever
completed this form had those records in front of them. It creates no revision,
no supersession, no chronology, no authority, and no trust relationship, and a
reader **MUST NOT** present one as any of those. [APR-MODEL-044]

Unsigned, a reference is a claim anyone could write. An attestation over the
form binds it, because a form digest covers `metadata`. Proving a history is
therefore what it always was — attestations, never position and never
assertion ([Changed forms](#changed-forms)).

A referenced digest matching no available record is `unresolved`
([Verification vocabulary](#verification)). The document is **valid**: a reader
**MUST NOT** reject it, report it as damaged, or withhold its data. [APR-MODEL-045]

> References are acyclic by construction. A form's digest covers its own
> `regarding` list, so a record can only name one whose digest was already
> fixed. Nothing needs to forbid a cycle, because none can be built.

A receipt naming the submission it was written against:

```apr-example
id: regarding-reference
rule: regarding
rules: APR-MODEL-043, APR-MODEL-045
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "documentType": "filledForm",
  "metadata": {
    "title": "Intake Receipt",
    "templateId": "tag:example.com,2026:intake-receipt",
    "regarding": ["sha256:b4363edd8ccc7f2e2acca6786a73a1f855fb8e9d8cad0245c16934107cfc4c28"]
  },
  "sections": [
    {
      "id": "intake",
      "title": "Intake",
      "prompts": [
        { "id": "received", "label": "Date received", "response": "2026-09-04",
          "hints": { "expectedDataType": "date" } }
      ]
    }
  ]
}
```

The referenced record need not accompany it. Alone, the receipt above is a
valid form whose one reference is `unresolved`. In a stream with its subject,
the reference resolves and the chain is legible to a reader.

```apr-example
id: regarding-chain
rule: regarding
rules: APR-MODEL-043
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","documentType":"filledForm","metadata":{"title":"Permit Application","templateId":"tag:example.com,2026:permit"},"sections":[{"id":"applicant","title":"Applicant","prompts":[{"id":"full_name","label":"Full name","response":"Ada Lovelace"}]}]}
---
{"aprVersion":"1.0-beta.6","documentType":"filledForm","metadata":{"title":"Intake Receipt","templateId":"tag:example.com,2026:intake-receipt","regarding":["sha256:b4363edd8ccc7f2e2acca6786a73a1f855fb8e9d8cad0245c16934107cfc4c28"]},"sections":[{"id":"intake","title":"Intake","prompts":[{"id":"received","label":"Date received","response":"2026-09-04"}]}]}
```

> Rationale: a workflow that wants to record something about a form has two bad
> options and one good one. Editing the form destroys the digest every
> attestation over it depends on. Widening the form with the receiver's data
> puts one party's assertions inside another party's document. Writing a new
> form and naming what it was about leaves every earlier record untouched, and
> makes each step of a process a document a person can read, a role can own,
> and a signature can cover — using the primitive the format already has
> instead of inventing a second one.

### 5.3 Section {#section-object}

| Member | Type | Required | Notes |
| --- | --- | --- | --- |
| `id` | string | **Yes** | Non-whitespace. Unique document-wide among sections. |
| `title` | string | **Yes** | Non-whitespace. **Never optional.** |
| `description` | string | No | |
| `sections` | array | No | Child sections — recursive. |
| `prompts` | array | No | |
| `kind` | string | No | `table` when this section's child sections are repeating instances ([Tables](#tables)). |
| `canAddRows` | boolean | No | `true` if a filler may add or remove instances. Absent means fixed. |
| `maxRows` | integer | No | Advisory cap on instance count. **MUST** be at least 1. [APR-MODEL-047] |
| `role` | string | No | [Roles](#roles) |

A section **MUST** carry content: at least one prompt or at least one child
section. There is no exception — tables included. [APR-MODEL-009]

**Section titles are required, not optional.** The section tree is the document
outline that a screen-reader user navigates by. An untitled section is a hole in
that outline, so the format refuses to produce one.

### 5.4 Prompt {#prompt-object}

| Member | Type | Required | Notes |
| --- | --- | --- | --- |
| `id` | string | **Yes** | Non-whitespace. Unique document-wide among prompts. |
| `label` | string | **Yes** | Non-whitespace. This is the accessible name. |
| `response` | string | No | Absent means empty ([Responses are strings](#responses)). |
| `hints` | object | No | [Hints](#hints-object). Advisory in full. |
| `role` | string | No | Overrides the containing section's role. |

**`label` is required and placeholder text is never a substitute for it.** A
placeholder disappears when the user types, is invisible to many assistive
technologies, and leaves the field permanently unnamed. A prompt with a
placeholder and no label is invalid APR.

Section ids and prompt ids occupy **separate namespaces**: a section and a prompt
**MAY** share an id. Within each namespace, ids **MUST** be unique across the
whole document, not merely among siblings — a filled form is consumed by field
id, and a duplicate makes the data ambiguous. [APR-MODEL-010]

Ids are compared by exact code-point equality; no normalization, case folding, or
trimming is applied. Ids **SHOULD** be stable across template versions and
**MUST NOT** change when prompts are reordered. Reordering a form is a
presentation change; changing an id silently breaks every downstream consumer and
every attestation covering it. [APR-MODEL-011]

#### 5.4.1 Generated ids {#generated-ids}

An author names things. A writer **MUST** preserve every valid id it read, and
**MUST NOT** invent or replace an id unless the caller explicitly asks it to
repair the document; by default a blank id or a duplicate is simply the error
[Errors](#structural-validation) says it is. [APR-MODEL-040]

When asked to repair, a writer generates ids by **content**, so that every
implementation repairing the same document arrives at the same names:

1. Take the **title path**: the titles of the enclosing sections from the
   outermost inward, followed by the member's own `title` (a section) or
   `label` (a prompt), as a JSON array of strings.
2. Serialize that array with JCS (RFC 8785), digest it with SHA-256, and
   encode the digest with base32 (RFC 4648 §6) in lowercase, without padding.
3. The id is `s` for a section or `p` for a prompt, followed by the first
   five characters. `sdwlrh` names the section titled *Applicant*; `pnzapj` names
   the prompt *Full name* inside it; `sasnpa` names row *Item 1* of a table
   titled *Expense line items*.
4. If the result collides with any id already in its namespace, append the
   member's 1-based position among its siblings to the array and repeat;
   if it still collides, append its parent's position, and so on outward.

A blank or whitespace-only id receives a generated one. Where two members
share an id, **both** are renamed, since neither has a better claim to the
name than the other. [APR-MODEL-041]

Generated ids are short, typeable, and carry no order: two adjacent prompts
get unrelated names, and nothing about a name says where it sits. A writer
**MUST NOT** generate ids that encode position — `q1`, `q2`, `row_3` —
because a person reading them will take the sequence to mean something, and
inserting one row would then appear to renumber the rest. A renamed id is a
changed id, with everything the stability rule above warns of: a `fields` attestation
naming the old id resolves to nothing afterwards, which is the honest
outcome, since the thing it named is no longer there under that name. [APR-MODEL-042]

> Rationale: ids are how a filled form is consumed, so a document with a
> missing or duplicated id is unusable, and *somebody* has to name the
> member. Deriving the name from what the member is — its label in its
> place in the outline — means the name is stable for as long as the thing it
> names is, and means a document repaired on two machines is the same
> document. Deriving it from a counter would make it stable only by luck and
> would make the names look like an order. Rows in a table follow the same
> rule for the same reason: a row's position is already carried by the
> document order, which is meaningful, and a name that repeats it is a second
> copy of one fact.

### 5.5 Tables {#tables}

A table introduces **no new primitive**. Rows are ordinary sections; cells are
ordinary prompts. A section becomes a table by carrying `kind: "table"`, and
**only** by carrying it: a reader **MUST NOT** infer a table from `maxRows`,
`canAddRows`, or the presence of child sections, and **MUST** preserve and ignore
those members on a section that is not a table. [APR-MODEL-038]

> Rationale: a plain section may have child sections too, so inference would
> have to guess, and two readers guessing differently about the same document
> is the failure the format exists to prevent.

**Example 4.** A table section.

```jsonc
{
  "id": "expenses",
  "title": "Expense line items",
  "kind": "table",
  "canAddRows": true,
  "maxRows": 25,
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

#### 5.5.1 What a table asserts {#table-assertion}

It is **a claim about structure, not appearance**:

- child sections are **instances**, not free-standing subsections;
- prompts at the **same position correspond** across instances — this is what
  makes "the Amount field" a thing that exists in every row;
- an instance's `title` **identifies** it; and
- a prompt's `label` **names the corresponding field** across every instance.

There is deliberately **no column definition**. A column header *is* the
corresponding prompt's label; a column's type hint *is* that prompt's
`expectedDataType`. Declaring columns separately would state twice what the
prompts already state, and anything stated twice can disagree — which is the
failure this design removes rather than manages.

Correspondence is **by position**. Ids are free-form; the convention
`{rowId}.{columnId}` is **RECOMMENDED** for addressability and database import,
but carries no meaning the renderer depends on. [APR-MODEL-048]

#### 5.5.2 A table licenses no layout {#table-no-layout}

A renderer **MAY** present a table as a grid, as stacked cards, as a flat
sequence of prompts, or as speech. **All are conformant**, and none is a
fallback. [APR-MODEL-012]

> Rationale: this matters most where tables are hardest. A six-column grid is
> unusable on a phone and at 200% zoom, and many screen-reader users prefer the
> linear reading. Choosing the linear presentation is not a degraded rendering of
> a table — it is an equally valid reading of the same claim.

A table **MUST NOT** be treated as licence for width, alignment, colour, or font
data. Those member names are retired ([Retired members](#retired-members)) and
are dropped on read. [APR-MODEL-013]

#### 5.5.3 Rows and instances {#table-rows}

`canAddRows` is `true` when a filler may add or remove instances; absent means
fixed.

> Rationale: the default is deliberately restrictive. A fixed table that silently
> gained a row is a worse failure than a line-item table needing one explicit
> property.

**Mutability and population are independent.** Whether instances may be added has
nothing to do with whether they currently hold values — a filled table may still
accept new rows, and a fixed table may be entirely blank.

**A table always has at least one instance.** A section carrying
`kind: "table"` **MUST** carry at least one child section. Prompts alone satisfy
[Section](#section-object) but not this: a table's cells live in its instances,
so a table without one has nowhere to put them. A reader **MUST** report
`EMPTY_TABLE`. [APR-MODEL-046]

An "empty" table was never empty: a UI offering to add the first row is already
presenting a row, and how that row is shown is a display decision. The instance
also carries the table's field names, so a table without one cannot describe
itself.

> Rationale: this was stated here in plain prose for the whole of the beta, as
> "a table always has at least one instance", with no keyword and no identifier.
> It read as a requirement, nothing could cite it, and no test could reach it.
> The check that reports it had to attribute itself to a rule about layout data,
> which was simply wrong.

`maxRows` is advisory. A table carrying more instances is still valid and is
reported as a warning ([Warnings](#warnings)).

#### 5.5.4 Ragged tables {#table-ragged}

Instances **SHOULD** agree in prompt count and in the label at each position.
When they disagree the document is still **valid**; a validator reports
`TABLE_RAGGED` or `TABLE_LABEL_MISMATCH` and a renderer presents what is there. [APR-MODEL-014]

> Rationale: refusing to open the document would discard whatever a filler had
> already written, which [Any string is a valid response](#any-string) exists to
> prevent.

### 5.6 Nesting depth {#nesting}

Sections nest recursively. Every implementation **MUST** support at least **16
levels** of section nesting. Implementations **MAY** support more. [APR-MODEL-015]

Any particular ceiling above that floor is an implementation detail and
**MUST NOT** be relied upon by a document author. [APR-MODEL-016]

> Rationale: unbounded depth is not implementable. Every real parser has a depth
> limit, and a format that promises infinity promises a stack overflow.

Authors **SHOULD** stay far below the floor. Forms nested more than four or five
levels deep are difficult to navigate with any input method. [APR-MODEL-017]

### 5.7 Hints {#hints-object}

All OPTIONAL, all advisory ([Hints never enforce](#hints-advisory)).

| Member | Type | Required | Notes |
| --- | --- | --- | --- |
| `placeholder` | string | No | Text shown in an empty control. Never a substitute for `label`. |
| `expectedDataType` | string | No | Suggested input affordance. Open registry; see below. |
| `suggestedValues` | array of string | No | Offered as options. A response outside the list is still valid. |
| `helpText` | string | No | Explanatory text for the prompt. |
| `validationPattern` | string | No | Advisory regular expression. |
| `min` | number or string | No | Suggested lower bound for an ordered field. A number on `number`, `currency`, and `range`; a canonical-form string on `date`, `time`, and `datetime`. |
| `max` | number or string | No | Suggested upper bound for an ordered field. Typed as `min`. |
| `step` | number | No | Suggested increment for an ordered field. Meaningful on `number`, `currency`, and `range`. |
| `exprHidden` | string | No | CEL. Truthy hides this prompt ([Expressions](#expressions)). |
| `exprValue` | string | No | CEL. Computed value. |
| `exprExpected` | string | No | CEL. Truthy marks the prompt as expected. |
| `exprValidation` | string | No | CEL. Returns a message; empty means valid. |
| `exprReadOnly` | string | No | CEL. Truthy makes this prompt read-only in a renderer. |

`expectedDataType` registry: `text`, `multiline`, `email`, `phone`, `url`,
`date`, `time`, `datetime`, `number`, `currency`, `boolean`, `select`,
`multichoice`, `password`, `range`, `color`.

**There is no `signature` type and no `file` type.** A signature is not a
response: evidence that a person stood behind a form is an attestation record
travelling beside it in the stream ([Attestations](#attestations)), never a
drawn image or a typed name in a field. An attachment is not a response either:
a form carries what a person typed, and the format defines no representation
for bytes that were not typed. A reader encountering either name treats it as
any other unregistered value and degrades it to text. [APR-MODEL-030]

> Rationale: both names once stood in the registry with their meaning marked as
> unspecified, so two implementations could accept the same form and store
> different things. A signature field in particular invited the mistake the
> roles section warns against — treating a widget as evidence.

Country-specific field types are deliberately absent. A postcode, a national
identity number, or a tax reference is `text` with a `validationPattern`: baking
one country's formats into the vocabulary would oblige every reader everywhere to
carry them.

**This registry is open.** An unrecognized value **MUST** degrade to a plain text
field. It **MUST NOT** cause an error — that is what lets the registry grow
without breaking every existing reader. [APR-MODEL-018]

**A hint a reader cannot use is not an error.** A hint that is unrecognised,
unsupported, or malformed — an unparseable `validationPattern`, a bound of the
wrong type, an expression that will not compile — **MUST** be preserved and
**MAY** be reported as a warning; it **MUST NOT** make the document invalid.
Hints are applied in the order the member table lists them, and where two
hints on one prompt conflict, the earlier one stands and the later one is
skipped and **MAY** be reported. [APR-MODEL-039]

The list above is the normative registry. `schemas/apr-types-1.0.json` publishes
it in machine-readable form, together with each type's canonical write form,
accepted read forms, expression type, and meaningful hints. That file is a
**derived projection** of this section: where the two disagree, this section
governs and the file is a defect.

> Rationale: the same facts stated in prose, in a schema, and in code will
> eventually disagree, which is the failure [Tables](#table-assertion) removes by
> refusing to declare a column twice. Naming one source and deriving the rest is
> the same move applied to the type vocabulary.

`suggestedValues` offers options; a response outside the list is still valid. On
a `boolean` it names the two options, so a renderer can label them as the author
intended without changing the type.

**Bounds are an offer, not a limit.** `min`, `max`, and `step` describe the range
a widget should offer: the ends of a slider, the increment of a spinner. They are
meaningful only on ordered types. On `number`, `currency`, and `range` they are
JSON numbers. On `date`, `time`, and `datetime`, `min` and `max` are the earliest
and latest suggested values, written as strings in that type's canonical form
([Value types](#json-subset)); `step` has no meaning there.

A response outside them is **still valid**, exactly as for `suggestedValues`. A
slider that stops at 100 does not make `120` a wrong answer, and a validator
**MUST NOT** reject one. Bounds shape the affordance offered to someone who wants
it; they never shrink what a person is allowed to say. [APR-MODEL-019]

#### 5.7.1 Types are affordances, not validators {#data-types}

`expectedDataType` tells a renderer which input affordance to offer and tells the
person filling the form what the author expected. It does nothing else.

Every response below is valid for its prompt:

| `expectedDataType` | Responses that are all valid |
| --- | --- |
| `date` | `2025-01-15`, `January 15th`, `next Tuesday`, `TBD`, empty |
| `number` | `42`, `forty-two`, `~50`, `N/A`, empty |
| `email` | `user@example.com`, `none`, `see attached`, empty |
| `phone` | `+1-555-0100`, `unlisted`, `ask my assistant`, empty |
| `boolean` | `Yes`, `No`, `Maybe`, `It's complicated`, empty |

> Rationale: the author's intent and what the person actually wrote are two
> different facts. APR preserves the second exactly. Whether it is acceptable is
> a decision for the workflow that consumes the form.

### 5.8 Unknown members {#extensions}

A reader **MUST** ignore members it does not recognise, at every level, and
**MUST NOT** reject a document for carrying them. [APR-MODEL-020]

A reader **MUST** also **preserve** them: an unrecognised member present on read
**MUST** still be present, unchanged, on write. [APR-MODEL-021]

> Rationale: without preservation, every additive change to the format is
> destructive. A document written by a newer version would lose its new members
> the first time an older reader opened and saved it, silently, with no error
> anywhere.

**Member names are case-sensitive.** A wrongly-cased member is an unknown member:
it is preserved as data, and the property it resembles takes its default. This is
a common source of "my field vanished" reports.

Extension members participate in whole-document digests ([Digests](#digests)),
so an attestation over a form covers them.

**Unprefixed names belong to the specification.** Every member this document
defines is unprefixed, and every member a later version of APR adds will be.
A producer **MUST NOT** add a member whose name carries no prefix. [APR-MODEL-031]

**An extension member is named by its owner.** An extension member name
**MUST** begin with a reverse-DNS prefix owned by the producer, followed by a
dot: `com.example.priority`, `gov.ct.dmv.routing`. A reader identifies an
extension member by the dot in its name. A validator **MAY** report an
unrecognised undotted member as `UNPREFIXED_MEMBER`; it **MUST NOT** reject
the document, and **MUST** still preserve the member
([Warnings](#warnings)). [APR-MODEL-029]

**An extension member is the author's own data.** It is written by whoever wrote
the form, travels inside it, and is covered by its digest. Data *about* a form
written by somebody else — a receipt, a review, a routing decision — is not an
extension member: it is a new form naming what it was about
([Related records](#regarding)).

> Rationale: two producers choosing the same member name produce documents that
> round-trip correctly and mean different things, and a format that cannot add
> a member without colliding with somebody's private one cannot grow. Reserving
> the unprefixed space and requiring an owned prefix for everything else closes
> both problems without a registry, which is what OpenAPI's `x-` and reverse-DNS
> naming in Java and Apple platforms do. A domain is the one namespace every
> producer already owns, so no one has to run anything.

```apr-example
id: extension-member-prefixed
rule: extensions
rules: APR-MODEL-020, APR-MODEL-029, APR-MODEL-031
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Extended", "com.example.routing": "desk-4" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "com.example.priority": 2 } ] }
  ]
}
```

#### 5.8.1 Retired members are the exception {#retired-members}

Members the specification has **removed** are dropped rather than preserved.
Today those are:

- the table-column presentation set — `width`, `alignment`, `color`,
  `background`, `fontSize`, `bold`, `style`;
- `signatures`, the embedded-signature array retired in beta.6
  ([Attestations](#attestations)); and
- `responseMetadata` and every member it carried, retired in beta.6 as workflow
  state ([A computed value is a suggestion](#expr-computed)).

`signatures` is reported rather than silently dropped: a reader **MUST** report
`RETIRED_EMBEDDED_SIGNATURES`, because a document carrying it was making a
cryptographic claim that beta.6 cannot honour, and losing that silently would be
worse than refusing it. [APR-MODEL-022]

> Rationale: retirement has to mean something. If a removed member were preserved
> as an unknown one, a renderer could keep writing column widths forever and "APR
> carries no presentation data" would be unenforceable. Dropping them is how the
> removal takes effect.

A name is added to the retired list only when this specification retires it. A
member that is merely unfamiliar is preserved, not dropped.

### 5.9 Canonical value forms {#canonical-values}

Any string remains a valid response. This section governs only what a renderer
**writes** when it controls the value — a date picker, a checkbox, a
multi-select list.

> Rationale: without it, the same template filled in two implementations yields
> two different datasets, and "database-ready" stops being true.

A reader **MUST** accept every listed read form. A writer **SHOULD** emit the
canonical form. Neither rule ever makes a document invalid. [APR-MODEL-023]

| Hint | Canonical write form | Also accepted on read |
| --- | --- | --- |
| `date` | `YYYY-MM-DD` (RFC 3339 full-date) | anything |
| `time` | `HH:MM` or `HH:MM:SS`, 24-hour | anything |
| `datetime` | RFC 3339 | anything |
| `boolean` | `true` / `false` | `yes`, `y`, `1`, `on`, `x`, `checked` / `no`, `n`, `0`, `off`, `unchecked`, case-insensitive |
| `number`, `currency` | digits with `.` as decimal separator, no grouping | anything, including symbols and words |
| `multichoice` | selections separated by U+000A, one per line | a single line separated by comma and space, legacy |
| `select` | exactly one value, verbatim from `suggestedValues` | anything |

**Why `true`/`false` and not `yes`/`no`.** `yes` is English. A format that
renders to voice, to other languages, and into database columns cannot make its
canonical boolean depend on one language.

**Why newline and not comma for `multichoice`.** A suggested value may itself
contain a comma — `Bloomfield, CT` is an ordinary option in a municipal form.
Comma separation silently turns one selection into two, which is data loss. A
newline cannot appear inside a single-line option, so the encoding is lossless.
Readers **MUST** still accept the legacy comma form. [APR-MODEL-024]

An empty string means "no selection" for every hint above.

### 5.10 Roles — who each part is for {#roles}

Most real forms are filled by more than one person. A patient completes an
intake, a nurse records observations, the office stamps a reference. With nowhere
to say so, all three arrive as one undifferentiated list and the patient is left
guessing which questions are theirs.

A section or a prompt **MAY** carry `role`: a short string naming who is meant to
fill it in. A prompt's role overrides the role of the section containing it, so a
single field can be handed back to the patient without splitting the section in
two. The vocabulary is **open**: a reader that does not recognise a role **MUST**
present the field normally rather than erroring. [APR-MODEL-025]

**Example 5.** Declared roles.

```jsonc
"roles": [
  { "id": "patient", "name": "Patient",
    "description": "The person receiving care" },
  { "id": "nurse", "name": "Nurse",
    "description": "Clinical staff recording observations" },
  { "id": "office", "name": "Office use" }
]
```

Each entry **MUST** carry `id`; `name` and `description` are OPTIONAL. [APR-MODEL-026]

A reader with no `name` **MUST** fall back to the identifier, and **MUST** show
the identifier rather than erroring where a role is referenced but never
declared. [APR-MODEL-050]

Declaring is itself optional and **MUST NOT** be required: a section or prompt
**MAY** reference a role the document never declares. A validator **MAY** warn
about an undeclared role; it **MUST NOT** reject one. [APR-MODEL-051]

**A role says who a field is for. It never says who may type into it.** The
format has no identity at fill time — nothing in a document knows who is at the
keyboard — so a reader **MUST NOT** refuse input to a field because of its role. [APR-MODEL-027]

What a reader **SHOULD** do is make the answer obvious without being asked. Where
a document declares roles, a reader **SHOULD** let the person say which role they
are filling and then show plainly which fields are theirs. Fields belonging to
others stay visible and stay editable; they are marked, not locked. A reader
**SHOULD** also make a role legible to assistive technology, since a visual
treatment alone communicates nothing to a screen reader. [APR-MODEL-028]

**Accountability comes from attestations, not from the widget.** A greyed-out box
is evidence of nothing: whoever holds the document can edit it directly. A
fields-scoped attestation over those prompts, made with the nurse's certificate,
is evidence the nurse filled them. Roles describe intent; attestations establish
fact. An implementation that treats a role as a security control has misread this
section.

---

## 6. Document type and file extensions {#media-types}

`documentType` is **authoritative**. A reader **MUST** determine whether a
document is a template or a filled form from that member alone. [APR-SEC-005]

| Extension | Meaning | Status |
| --- | --- | --- |
| `.aprt` | Template | Convention |
| `.aprf` | Filled form | Convention |
| `.apr` | Either | Convention |
| `.apr.jsonc` | An APR-JSONC document or stream | Convention |
| `.apr.yaml` | An APR-YAML document or stream | Convention |

A filename extension is a **desktop affordance** — it drives icons, file
associations, and save dialogs. It is not part of the data model.

> Rationale: an earlier draft made the extension override `documentType`. That
> rule cannot be implemented anywhere a filename does not exist: an HTTP request
> body, a database column, a clipboard paste, a mobile share intent, a
> `postMessage` between frames, a byte array in an enterprise queue. Under it a
> browser-based reader and a desktop reader would reach *different conclusions
> about identical bytes* — precisely the interoperability failure the format
> exists to prevent. A document must mean the same thing everywhere, including
> where it has no name.

An implementation **SHOULD** write the extension matching `documentType`, and
**SHOULD** warn on mismatch rather than silently honouring either one. [APR-SEC-006]

Representation is determined by content, not by name: a reader **MUST NOT**
reject a document because its extension disagrees with its content, and
**MUST NOT** infer `documentType` from an extension. [APR-SEC-007]

Converting a template to a filled form is an explicit act: set `documentType` to
`filledForm` and record `templateId`. Implementations **SHOULD** prompt for a new
filename so the blank template is not overwritten. [APR-SEC-008]

**Media types.** Where a document travels with a media type, the type names the
representation, and the `documentType` member remains authoritative for what the
document is.

| Content | Media type |
| --- | --- |
| An APR-JSONC document | `application/vnd.apr+json` |
| An APR-YAML document or document stream | `application/vnd.apr+yaml` |
| An APR-JSONC record stream | `application/vnd.apr+json-seq` |

A producer that labels APR content **MUST** use the type above for its
representation. [APR-SEC-012]

A reader **MUST NOT** select APR behaviour from the generic `application/json`,
`application/yaml`, or `application/json-seq` types. [APR-SEC-013]

A reader **MUST NOT** treat a mismatch between a media type and the content as
grounds for rejection: the content decides, exactly as for a filename
extension. [APR-SEC-014]

> Rationale: these three fail independently, and a case citing one bundle could
> not say which broke. A producer labelling APR-YAML as `application/json` is a
> mislabelled document; a reader inferring APR from `application/json` treats
> every JSON file it meets as a form; a reader refusing a mismatch loses a
> readable document to a header. Only the last is data loss, which is why the
> content deciding is stated as its own rule.

The types are in the vendor tree of RFC 6838, with the `+json` (RFC 6839),
`+yaml` (RFC 9512) and `+json-seq` (RFC 7464) structured syntax suffixes.
Registration with IANA by Skeptical Engineering is **pending**
([Open questions](#open-questions)); until it completes the names are a
declaration of intent that no other party has claimed.

> Rationale: a media type is the name a document carries where a filename does
> not exist — an HTTP body, an attachment, a share sheet. The `+yaml` suffix
> answers the concern that YAML's own type would mislead a reader: it says YAML
> syntax and APR meaning, which is the constrained profile APR-YAML is. The
> `+json` suffix is a mild stretch, since an APR-JSONC document may carry
> comments; they are representation trivia removed before the text is JSON
> ([APR-JSONC](#apr-jsonc)). The product-only name `vnd.apr` rather than a
> vendor-qualified one is deliberate: a media type is never renamed, and the
> format is meant to outlive its first steward.

---

## 7. Validation {#validation}

Validation produces **errors** and **warnings**. A document is valid if and only
if it has zero errors: a validator **MUST NOT** report a document as invalid for
any reason not in the errors table, and a warning **MUST NOT** affect
validity. [APR-VAL-007]

### 7.1 Errors — structure only {#structural-validation}

| Code | Condition |
| --- | --- |
| `NULL_DOCUMENT` | No document. |
| `REQUIRED_FIELD` | `aprVersion`, `metadata.title`, section `id` or `title`, prompt `id` or `label` blank; `sections` empty; `templateId` absent on a filled form. |
| `UNSUPPORTED_VERSION` | `aprVersion` is not exactly `1.0-beta.6` ([Version compatibility](#version-compatibility)). |
| `DUPLICATE_ID` | A section or prompt id repeats within its namespace. |
| `EMPTY_SECTION` | A section has no prompts and no child sections. |
| `EMPTY_TABLE` | A `kind: "table"` section has no child sections, so it has no instances ([Rows and instances](#table-rows)). |
| `RETIRED_EMBEDDED_SIGNATURES` | The document carries a `signatures` member. |
| `WRONG_TYPE` | A structural member is not the JSON type its member table declares ([Value types](#json-subset)). |

This list is exhaustive, and a validator **MUST NOT** raise an error outside
it. [APR-VAL-008] **No error may ever arise from the content of a
response**, and none may ever arise from the state of an attestation
([Attestations never gate the data](#never-gate)). A validator that rejects a
document because a response is badly formatted, or because an attestation is
missing or invalid, is not implementing APR.

A validator **MUST** also enforce the two rules a schema cannot express: section
ids unique document-wide, and prompt ids unique document-wide, in separate
namespaces. [APR-VAL-001]

### 7.2 Warnings — advisory only {#warnings}

| Code | Condition |
| --- | --- |
| `RESPONSE_CONTRADICTS_TYPE` | A response contradicts `expectedDataType`. |
| `RESPONSE_PATTERN_MISMATCH` | A response does not match `validationPattern`. |
| `RESPONSE_OUTSIDE_BOUNDS` | A response falls outside the bounds family ([Hints](#hints-object)). |
| `RESPONSE_OUTSIDE_SUGGESTED_VALUES` | A response is not one of `suggestedValues`. |
| `HINT_UNUSABLE` | A hint cannot be applied at all — a `validationPattern` that is not a valid regular expression, a bound that will not parse. |
| `UNREGISTERED_DATA_TYPE` | `expectedDataType` names a type the registry does not carry ([Types are affordances](#data-types)). |
| `UNDECLARED_ROLE` | A `role` names a role `metadata.roles` does not declare ([Roles](#roles)). |
| `TABLE_RAGGED` | Instances of a table do not carry the same prompt ids ([Ragged tables](#table-ragged)). |
| `TABLE_LABEL_MISMATCH` | A cell's label differs across instances of the same column. |
| `TABLE_OVER_CAPACITY` | A table carries more instances than `maxRows`. |
| `TABLE_MEMBERS_ON_A_PLAIN_SECTION` | `maxRows` or `canAddRows` on a section that is not a table ([Tables](#tables)). |
| `UNPREFIXED_MEMBER` | An unrecognised member with no reverse-DNS prefix ([Unknown members](#extensions)). |
| `SUBMISSION_URL_UNSUPPORTED` | A submission entry of a scheme this document does not define ([Submission targets](#submission)). |
| `NON_NFC_TEXT` | Human-facing text is not in Normalization Form C ([Human-facing text](#human-text)). |
| `FORBIDDEN_CODE_POINT` | Human-facing text carries a code point the floor excludes ([Human-facing text](#human-text)). |

An implementation that reports one of these conditions **MUST** report it under
the code named here. [APR-VAL-009]

Warnings are how an implementation tells a person "this may not be what you
meant" without ever telling them "you may not write this."

An implementation **MAY** surface any warning, including conditions this table
does not name — a blank response the workflow may consider required, or the
confusable and mixed-script findings the text-handling section asks for. Such feedback
**MUST NOT** be reported as the document being invalid. [APR-VAL-002]

> Rationale: the table fixes spellings, not obligations. Whether to report a
> condition stays the implementation's choice, and this list is not exhaustive
> the way the error list is. But a warning exists to be understood by whoever
> reads it next, and two implementations reporting the same condition under two
> names have produced advice that only travels as prose. `NON_NFC_TEXT` and
> `FORBIDDEN_CODE_POINT` are the exception to the choice, not to the spelling:
> the human-facing text floor requires a validator to report those at authoring time.

It **MUST NOT** prevent saving, and **MUST NOT** prevent entering any text. [APR-VAL-006]

### 7.3 Parse errors are not validation errors {#parse-errors}

Malformed input, a response given as a number or boolean, or a structurally wrong
shape are **parse failures**, and a reader **MUST** fail rather than validate. [APR-VAL-003]

Documents that parse cleanly and fail validation are a different class from those
that **MUST NOT** parse at all. Keeping these stages distinct is what lets a
reader load a flawed document and show what is wrong with it, rather than
refusing to open it. [APR-VAL-004]

A reader that reports a parse failure **MUST** report it under a parse-stage
code: the code this document names for that condition where it names one —
`DUPLICATE_MEMBER`, the `YAML_*` refusals, `APR_STREAM_MIXED_REPRESENTATIONS` —
and `PARSE_ERROR` where it does not. No parse-stage code is an entry in the error
table above, and none disturbs its exhaustiveness: a document that will not parse
was never validated, so no validation error can describe it. [APR-VAL-010]

### 7.4 Semantic validation is never required {#semantic-validation}

A validator **MUST NOT** reject a document because of what a response means. [APR-VAL-005]

Each of the following is a valid document: a response that does not match its
`expectedDataType`; one that does not match `validationPattern`; an empty
response, including on a prompt marked expected; one outside `suggestedValues`,
`min`, or `max`; and one a reader considers factually wrong.

The format validates that a response is a well-formed string. It never validates
what that string says.

---

## 8. Text handling {#text-handling}

### 8.1 Responses are evidence {#text-responses}

A reader **MUST** preserve a response exactly on read and write: it **MUST NOT**
normalize, strip, or otherwise rewrite it. Escaping and visibly marking deceptive
text are rendering responsibilities, not licences to alter stored data. [APR-TEXT-001]

### 8.2 Authoring data and filled data differ {#authoring-vs-filled}

The two halves of an APR document come from two different people under two
different conditions, and they **MUST NOT** be treated alike. [APR-TEXT-002]

| | **Authoring data** | **Filled data** |
| --- | --- | --- |
| Written by | the form author | the person filling the form |
| Members | `metadata`, section `id`, `title`, `description`, prompt `id`, `label`, all of `hints` | `prompt.response` |
| Conditions | deliberate, repeatable, reviewable before publication | once, under time pressure, often on someone else's behalf |
| Consumed by | machines and every future reader | the receiving workflow |
| Policy | **Strict rules are appropriate.** Reject or warn at authoring time. | **Maximum tolerance.** Accept any string; never rewrite. |

> Rationale: strictness at authoring time costs the author one correction before
> publishing. Strictness at fill time costs a person their answer, silently, at
> the moment they are least able to notice.

#### 8.2.1 Filled data — never rewritten {#filled-never-rewritten}

A response **MUST NOT** be altered on the basis of any hint. A `url` or `email`
hint describes what the author *hoped* to receive; it does not license editing
what was actually written. [APR-TEXT-003]

Suspicious characters in a response **MUST** be surfaced as a warning and
**SHOULD** be rendered visibly — escaped or badged — leaving the stored bytes
exactly as entered. The consuming workflow decides what to do about them; it is
the only party that knows what the answer is for. [APR-TEXT-004]

A reader that "cleans" a hidden or bidirectional character has let a hint enforce
something, which [Hints never enforce](#hints-advisory) forbids. Legitimate uses
exist: a Persian ZWNJ and an emoji ZWJ sequence are ordinary text.

#### 8.2.2 Authoring data — strictness is appropriate {#authoring-strictness}

Authoring members **MAY** be held to strict rules, and the members a machine acts
on **SHOULD** be. [APR-TEXT-005]

**Strictness here means refusing, not rewriting.** No party's data is ever
silently edited — the difference between an author and a filler is that an author
*can* be stopped and asked to fix something, while a filler must never be
blocked. Rewriting an authored value is not the strict option; it is the same
silent edit wearing a different hat.

`metadata.submissionUrls` is the strongest case in the format. It is an ordered,
author-supplied array of explicit delivery choices, machine-consumed and
security-critical. An implementation:

- **MUST NOT** rewrite any entry to remove hidden characters. Cleaning a
  zero-width character out of a hostname picks a destination on the author's
  behalf, which is precisely the decision that must not be made automatically. [APR-TEXT-006]
- **SHOULD** report hidden characters in it as an advisory, since such a URL
  renders to a reviewer as one host while being another. [APR-TEXT-007]
- **MUST NOT** produce an attestation over a document whose `submissionUrls`
  contains them. Binding an address that displays as one host and resolves as
  another defeats the binding. [APR-TEXT-008]

Implementations **SHOULD** also warn at authoring time on mixed-script or
bidirectional content in `metadata.title`, `metadata.publisher`, section titles,
and prompt labels — the text a person reads when deciding whether to trust a
form. These are warnings to the author, before publication, and never
modifications. [APR-TEXT-009]

Ids are machine keys. Implementations **SHOULD** warn when an id contains
characters outside `[A-Za-z0-9_.-]`, since ids appear in attestation manifests,
database columns, and cell addresses. [APR-TEXT-010]

#### 8.2.3 Human-facing text {#human-text}

Some members exist to be read or heard by a person: `metadata.title`,
`description`, `author` and `publisher`; a role's `name` and `description`; a
section's `title` and `description`; a prompt's `label`; and the `placeholder`,
`helpText` and `suggestedValues` hints. Every one of them is a string, because
its whole purpose is to be rendered as text or speech, and every one of them is
authoring data.

Human-facing text **MUST** be in Normalization Form C (UAX #15) and **MUST
NOT** contain a code point that is unassigned, a surrogate, private-use, a
control other than U+0009 and U+000A, or that UTS #39 classifies with an
`Identifier_Type` of `Default_Ignorable`, `Deprecated`, or `Not_Character`.
Bidirectional and joining behaviour comes from the characters' own properties
(UAX #9), never from explicit control characters, which the preceding sentence
excludes. A validator **MUST** report a violation at authoring time; a reader
that meets one in a published form renders it defensively and never rewrites
it ([Responses are evidence](#text-responses)). [APR-TEXT-011]

Beyond that floor, an implementation **SHOULD** apply the confusable and
mixed-script detection of UTS #39 to human-facing text and report what it
finds, as the authoring-time warning above already asks for titles and labels. [APR-TEXT-012]

**A response is not human-facing text in this sense.** It is what a person
typed, and [Filled data — never rewritten](#filled-never-rewritten) governs it:
suspicious characters in a response are surfaced and rendered visibly, and the
document stays valid.

> Rationale: the format is meant to be safe for a person to read and to edit
> by hand, so text addressed to a person must be text a person can see. The
> Unicode Consortium maintains the list of what is invisible, deprecated, or
> deceptive; restating it here would put a copy of that list in this document
> to drift. The rule therefore names the properties and cites the standard
> that defines them, and the floor is the mechanical part — the part a script
> can check — while confusable detection, which depends on context and script
> tables, is recommended rather than required.

---

## 9. Streams {#streams}

A stream is an ordered transport of independent records. Physical order is
presentation only: a reader **MUST NOT** derive a subject, a revision, a
chronology, or a trust relationship from the position of a record. [APR-STREAM-005]

Each record is exactly one of:

- a complete standalone APR form; or
- an APR attestation.

A stream **MUST NOT** mix representations. [APR-STREAM-001]

It **MUST NOT** deduplicate repeated form occurrences, even when their semantic
digests are identical. Two occurrences of one form are two records, and a reader
that collapses them has lost a fact the sender stated. [APR-STREAM-003]

A single-form API given a stream **MUST** return `APR_STREAM_REQUIRES_ITERATION`
and **MUST NOT** select a record by position. A streaming API yields every record
and may hold an unresolved attestation until its subject form has been
observed. [APR-STREAM-004]

> Rationale: a stream exists so that a form and the assertions about it can
> travel together, and so that several related forms can be one file. It is
> deliberately *not* a revision history: nothing in the ordering says one form
> supersedes another. A workflow that wants revisions builds them from
> attestations, where the relationship is proved rather than positional.

### 9.1 JSONC framing {#jsonc-framing}

APR-JSONC streams use the JSON text sequence framing of RFC 7464, with the
element grammar replaced by APR-JSONC.

```abnf
apr-jsonc-stream = *record
record           = RS apr-jsonc-text LF
RS               = %x1E
LF               = %x0A
```

A comment is confined to its one JSONC record: `apr-jsonc-text` bounds it, so no
comment can span the separator. A record not preceded by `RS` is a framing
failure.

*Negative case:* `malformed/missing-record-separator.apr.jsonc`.

### 9.2 YAML framing {#yaml-framing}

APR-YAML streams are YAML streams: the document productions of YAML 1.2.2
chapter 9 apply unchanged, and every YAML document in the stream is exactly one
APR record. No additional framing is defined, because YAML already has one.

**Example 6.** A YAML stream carrying two independent forms.

```yaml
---
aprVersion: "1.0-beta.6"
metadata:
  title: Household contact card
sections:
  - id: contact
    title: Contact
    prompts:
      - id: full_name
        label: Full name
        response: ""
---
aprVersion: "1.0-beta.6"
metadata:
  title: Emergency contact card
sections:
  - id: emergency
    title: Emergency contact
    prompts:
      - id: emergency_name
        label: Name
        response: ""
```


A stream carries one representation throughout.

```apr-example
id: stream-mixed-representations
rule: streams
rules: APR-STREAM-001
representation: jsonc-stream
expect: reject
diagnostic: APR_STREAM_MIXED_REPRESENTATIONS
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"first"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
---
aprVersion: "1.0-beta.6"
metadata:
  title: second
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

### 9.3 Equivalence {#stream-equivalence}

The corpus supplies paired streams whose records have equal semantic models
across the two representations. A stream reader **MUST** produce the same
sequence of semantic records from either member of such a pair. [APR-STREAM-002]

---

## 10. Semantic digests and manifests {#digests}

`jcs-sha256` is the beta.6 semantic digest algorithm. A digest **MUST** be
computed over the RFC 8785 JCS serialization of the fully parsed JSON semantic
model, encoded as UTF-8, and expressed as lowercase hexadecimal SHA-256
(FIPS 180-4) prefixed with `sha256:`. Source syntax **MUST NOT** be
hashed. [APR-DIGEST-006]

A digest value **MUST** match `^sha256:[0-9a-f]{64}$`. [APR-DIGEST-001]

A form digest includes every APR-defined member and every unknown extension
member that survived parsing. It excludes only representation trivia. A verifier
that cannot preserve or digest an extension member **MUST** report the assertion
as `unverifiable`, not valid. [APR-DIGEST-002]

> Rationale: including extensions prevents a whole-form attestation from silently
> omitting a meaningful member. An earlier signature scheme enumerated known
> fields only, which meant extension data on a signed document could be altered
> without invalidating the signature.

An integrity manifest does not duplicate plaintext. It contains `root`, the
digest of the subject form, and `entries`. Each entry has a JSON Pointer `path`
(RFC 6901) and a digest of the JCS encoding of the value at that path.

`entries` **MUST** be ordered by `path`, compared as strings, and **MUST NOT**
repeat a path. [APR-DIGEST-003]

`entries` **MUST** contain the root pointer, and **SHOULD** contain one entry for
every value in the semantic model at every depth, extension members included. A
`fields` scope is the exception: what it carries is stated in
[Scope](#attestation-scope), and nothing further is expected of it. [APR-DIGEST-004]

Integrity comes from `root` alone. Entries are how a verifier explains *which*
values differ without the manifest retaining what they used to be, so a manifest
missing a path explains less and proves exactly as much. A verifier that finds a
subject differing from `root` **MUST** report the difference at the most specific
path the manifest carries, and **MUST NOT** report a path it does not. [APR-DIGEST-005]

> Rationale: an earlier draft asserted completeness in prose without requiring
> it, so a manifest could omit half a document without breaking a rule while a
> reader was entitled to expect otherwise. Ordering is required because two
> manifests over one form should be one manifest. Completeness is recommended
> rather than required because a whole-document manifest over a large form runs
> to thousands of entries, and buying diagnosis with size is the signer's call.

---

## 11. Profile: expressions {#expressions}

**OPTIONAL.** Core implementations skip this section entirely.

### 11.1 What expressions are {#expr-what}

Five advisory hints that let a form react to its own answers: showing a field
only when relevant, computing a total, flagging a cross-field inconsistency.

| Hint | Effect when truthy |
| --- | --- |
| `exprHidden` | Hide this prompt |
| `exprValue` | Computed value, still editable |
| `exprExpected` | Mark as expected; advisory, never blocks |
| `exprValidation` | Returns a message; empty means valid |
| `exprReadOnly` | Make read-only |

### 11.2 Invariants {#expr-invariants}

1. Stored responses remain authoritative. An expression **MUST NOT** reject,
   rewrite, or invalidate a response. [APR-EXPR-001]
2. Evaluation is pure. An implementation **MUST NOT** expose filesystem,
   network, process, clock, randomness, reflection, environment, or
   document-mutation access to an expression. [APR-EXPR-002]
3. Failure preserves data. A failed evaluation produces a diagnostic and the
   fallback below; it **MUST NOT** propagate as an error into a filling
   workflow. [APR-EXPR-003]

### 11.3 Language {#expr-language}

**APR expressions are CEL**, the Common Expression Language. This specification
does not define the language: grammar, operators, functions, and type rules come
from cel-spec, and language conformance is that project's own test suite, which
APR neither writes nor maintains.

CEL is non-Turing-complete, terminates by construction, and has no I/O or host
access, which is why it is safe to evaluate on a document from an untrusted
sender.

The language is **cel-spec release `v0.25.3`**: its language definition, its
standard library, and its standard macros. An implementation claiming
`core+expressions` **MUST** evaluate expressions as that release specifies and
**MUST** pass that release's conformance suite for the surface it exposes. [APR-EXPR-012]

An implementation **MUST** provide the standard library and the standard macros,
and **MUST NOT** provide any extension library or custom function. An expression
naming a function outside that surface is an evaluation failure, and the per-hint
fallback applies ([Results and fallback](#expr-fallback)). [APR-EXPR-013]

> Decision (beta.6): the CEL strings extension is **not** required. An earlier
> draft of this baseline required it, on the reasoning that trimming, splitting
> and case-folding are what form authors reach for first. It was withdrawn on
> evidence: the extension is defined by cel-go and is not carried by every CEL
> binding — the Python one ships the standard library and macros without it — so
> requiring it would have obliged an implementer to write the extension before
> they could claim the profile. A surface every binding already has is worth more
> than a convenient one only some of them do.

> Rationale: without a pin, a function that did not exist when a form was written
> is neither clearly valid nor clearly invalid, and two conforming readers may
> evaluate the same form to different values. Pinning the specification release
> rather than a library means the four implementations may each track their own
> language's library, provided each conforms to the same definition. A later
> baseline moves the pin deliberately, as a behavioural change.

### 11.4 Activation {#expr-activation}

An expression **MUST** be evaluated against this read-only activation and nothing
else. [APR-EXPR-015]

| Name | Type | Meaning |
| --- | --- | --- |
| a prompt's `id` | that prompt's bound type | Direct binding, where the id is a valid CEL identifier and not reserved. |
| `_this` | the owning prompt's bound type | The response of the prompt carrying this hint. |
| `_id` | `string` | The owning prompt's id. |
| `_now` | `timestamp` | The evaluation instant, supplied by the caller. |
| `_today` | `string` | The evaluation date, supplied by the caller. |
| `ctx` | `map` | Host-supplied context ([Context](#expr-context)). |

`_this`, `_id`, `_now`, `_today`, and `ctx` are reserved and **MUST NOT** be
shadowed by a direct binding. [APR-EXPR-004]

A prompt whose id is not a valid CEL identifier **MUST NOT** be given a direct
binding, and **MUST NOT** be reachable from an expression by any other
name. [APR-EXPR-016]

`_now` and `_today` **MUST** be supplied by the caller rather than read from the
host clock during evaluation, so that evaluating the same form twice with the
same inputs yields the same result. [APR-EXPR-005]

### 11.5 The type environment {#expr-binding}

CEL is statically typed. `expectedDataType` supplies the types:

| `expectedDataType` | CEL type |
| --- | --- |
| `number`, `currency`, `range` | `double` |
| `boolean` | `bool` |
| `date`, `time`, `datetime` | `timestamp` |
| `multichoice` | `list<string>` |
| everything else, or absent | `string` |

> Rationale: this is what lets an author write `quantity * unit_price` rather
> than wrapping every reference in a conversion, and what lets a type checker
> tell them an expression is wrong before a filler ever sees the form.

### 11.6 Values that will not bind {#expr-unbound}

A response that cannot be converted to its declared type — free text in a
`number` field, an unparseable date, or an empty one — **MUST** be treated as
unbound, **never** as a default. The expression errors and applies the fallback. [APR-EXPR-006]

> Rationale: binding an empty number as zero would make a blank field silently
> total as zero — a wrong answer rather than no answer. Unbound also keeps
> short-circuiting usable: a conjunction whose first operand is false does not
> need its second operand to bind.

**Nothing about the response changes.** It is stored verbatim, displayed
verbatim, and the document stays valid. It simply does not participate in a
calculation — which is what advisory has meant all along.

### 11.7 Context {#expr-context}

`ctx` carries data the host application supplies — the person's own details,
their organization, their environment — so a form can offer what it already
knows.

A host **MUST NOT** place credentials, secrets, authorization decisions, or
private server-side facts in `ctx`. An expression is document-supplied text; what
it can read, a document author can read. [APR-EXPR-007]

### 11.8 Results and fallback {#expr-fallback}

A result is marshalled back to a stored string through the canonical write forms
of [Canonical value forms](#canonical-values), which serve both directions.

Each hint requires a result type. Any failure — a compile error, an evaluation
error, an unbound reference, or a result of the wrong type — applies the
fallback.

| Hint | Required result | Fallback |
| --- | --- | --- |
| `exprHidden` | `bool` | false — show the prompt |
| `exprExpected` | `bool` | false — do not mark expected |
| `exprReadOnly` | `bool` | false — keep editable |
| `exprValidation` | `string` | empty — no advisory |
| `exprValue` | the prompt's bound type | Do not write; retain the stored response exactly |

Every fallback shows more and blocks less. A form whose expressions all fail is a
plain form.

### 11.9 A computed value is a suggestion, not a lock {#expr-computed}

**A computed prompt MUST remain editable.** Any string is a valid response, and a
renderer that refuses typing into a computed field has stopped implementing the
format. A total that is wrong — because the form's arithmetic does not match what
was actually agreed — must be correctable by the person filling it in. [APR-EXPR-014]

Being computed does not make a prompt read-only. `exprReadOnly` asks for that
*presentation*, and even then it is an affordance rather than a wall.

**A correction MUST survive recomputation.** Every non-empty response in a
document as it was read is **authored**, whatever produced it, and recomputation
**MUST NOT** overwrite an authored response. A reader presents the recomputed
value as a suggestion instead. [APR-EXPR-008]

Within a session a reader knows which responses it computed itself and may
replace those freely; that knowledge is the reader's own state and the document
records nothing about it.

> Rationale: a document cannot tell a stale computed value from a correction
> someone typed, and the earlier design that tried — a `computed` marker in
> `responseMetadata` — rested a prohibition on a member every reader was free to
> ignore, which is not a guarantee. Treating every response already in the file as
> authored needs no marker and errs toward keeping an answer, which is the one
> thing this format exists to prevent losing. The cost is that a stale value does
> not silently refresh across a save and reopen, and that is the correct cost: a
> computed value is a suggestion, not a lock.

An implementation **MUST** order computed prompts by their direct references so
that a subtotal feeds a tax feeds a total in one pass. A self-reference or a
dependency cycle is an authoring error. [APR-EXPR-009]

### 11.10 Authoring-time checking {#expr-authoring}

An implementation **SHOULD** type-check expressions against the document's type
environment when a template is authored, and report failures to the author with
position information. [APR-EXPR-010]

This is exactly where [Authoring data](#authoring-strictness) says strictness
belongs. The **author** is stopped and asked to fix something before publication;
the **filler** is never blocked, because at fill time the same expression
degrades.

### 11.11 Bounds {#expr-limits}

Evaluation **MUST** terminate. An implementation bounds expression size,
complexity, and evaluation cost, and **MUST** report reaching a bound as a
failure that applies the fallback rather than as partial mutation. [APR-EXPR-011]

Exact bounds are implementation-defined in this baseline, for the reason given in
[Security considerations](#security).

---

## 12. Profile: attestations {#attestations}

**OPTIONAL.** Core implementations preserve attestation records and report them
as unchecked.

### 12.1 Model {#attestation-model}

Beta.6 retires the embedded `signatures` array. An APR form is ordinary form
data; cryptographic assertions live in **independent attestation records** that
travel in the same stream.

> Rationale: an embedded signature made the document and the assertion about it
> one object, so every reader had to understand signatures to read a form, and
> every signature had to describe its own scope inside the thing it was signing.
> Separating them means a reader that ignores attestations simply reads forms,
> and an attestation identifies its subject by content rather than by position or
> filename — so it does not matter what order records arrive in, or whether the
> subject is even present.

Verification is a pure computation over bytes already in hand: **a form carrying
attestations is exactly as safe to open as one without.** APR never executes
anything.

### 12.2 Attestation record {#attestation-catalogue}

**Example 7.** An attestation.

```jsonc
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": { "digest": "sha256:...", "canonicalization": "jcs-sha256" },
  "scope": { "kind": "document" },
  "manifest": { "root": "sha256:...", "entries": [] },
  "proofs": [],
  "witnesses": []
}
```

| Member | Type | Required | Domain |
| --- | --- | --- | --- |
| `recordType` | string | **Yes** | **MUST** be `attestation`. [APR-ATTEST-001] |
| `aprVersion` | string | **Yes** | **MUST** be `1.0-beta.6`. [APR-ATTEST-002] |
| `subject` | object | **Yes** | `digest` and `canonicalization`, no other members. |
| `subject.digest` | string | **Yes** | `sha256:` and 64 lowercase hex characters. |
| `subject.canonicalization` | string | **Yes** | **MUST** be `jcs-sha256`. [APR-ATTEST-003] |
| `scope` | object | **Yes** | `document` or `fields` form. |
| `manifest` | object | **Yes** | `root` and `entries`, no other members. |
| `manifest.root` | string | **Yes** | Digest of the subject form. |
| `manifest.entries` | array | **Yes** | Entries of `path` and `digest`, no other members. |
| `proofs` | array | **Yes** | Entries of `type` and `value`. May be empty. |
| `witnesses` | array | **Yes** | Unique digests of earlier envelopes. May be empty. |

`subject`, `scope`, `manifest`, and their entries admit no additional members. An
attestation record itself **MAY** carry extension members, which round-trip. [APR-ATTEST-004]

`subject.digest` **MUST** identify the complete form semantic model, and a
verifier **MUST NOT** resolve a subject by stream position, filename, or document
id. [APR-ATTEST-017]

### 12.3 Scope {#attestation-scope}

`scope.kind` is `document` or `fields`.

`document` covers the complete form, including its extension members.

A `fields` scope lists prompt ids, and the manifest **MUST** include each
selected prompt, its response and hints, and every ancestor section's id, title,
description, kind, and role. [APR-ATTEST-005]

**A filler attests to the question, not only the answer.** Anything less is not
an attestation on a form.

> Rationale: an earlier scheme covered the response alone. Sign "No" to *"Have
> you ever been convicted of a felony?"*, let someone afterwards change the label
> to *"Do you enjoy long walks?"*, and the signature still verified — putting a
> person on record as having answered a question they never saw. Covering the
> question, its type, and its offered options is what closes that.

A fields scope is deliberately *not* the whole document: a filler attests to
their part, and someone else editing an unrelated section **MUST NOT** be
reported as making their attestation `invalid`. [APR-ATTEST-006]

**What that protection is, exactly.** `subject.digest` names the complete form,
so an edit anywhere produces a changed form, and against *that* form the
attestation is `unresolved` rather than invalid
([Changed forms](#changed-forms), [Verification vocabulary](#verification)). It
stays `valid` against the form it was made over, which is why a workflow retains
the original record rather than replacing it. A verifier **MUST NOT** report
`invalid` merely because the form it holds is a later one. [APR-ATTEST-015]

A verifier **MAY** additionally compare a `fields` manifest's entries against a
changed form and report which attested paths still match. That is a diagnostic
and **MUST NOT** be reported as a verification result: the attestation remains an
assertion about its original subject and is never transferred to another. [APR-ATTEST-016]

> Rationale: the promise above is worth making and was worth stating precisely.
> Read loosely it suggests a fields attestation keeps verifying across edits,
> which nothing in the format delivers, because a digest over the whole form
> moves when any part of it does. What the format does deliver is that a filler
> is never reported as having signed something false — and, for a reader that
> wants it, a per-path answer to what actually changed.

### 12.4 Proofs {#proofs}

`proofs` are assertions over the JCS serialization of the attestation envelope
after omitting `proofs` themselves.

Beta.6 defines one proof type, `cms/ecdsa-p256-sha256`: ECDSA over the P-256
curve with SHA-256 (FIPS 186-5), carried as CMS SignedData (RFC 5652), encoded as
base64 (RFC 4648), with the X.509 certificate chain (RFC 5280) included.

A proof **MUST NOT** invent a second copy of the subject digest or scope. [APR-ATTEST-007]

> Rationale: two copies of one fact is a correctness bug everywhere in this
> format, and here it was a security hole. An earlier scheme stored the
> submission URL a second time on the signature object and verified against
> *that* copy, so redirecting the document's real URL left the signature
> reporting valid.

A verifier that does not recognize a proof type **MUST** report it as
**unverifiable**, never as invalid, and **MUST** preserve it. "I cannot check
this" and "this is forged" are different statements and **MUST NOT** be conflated
in a user interface. [APR-ATTEST-008]

**A proof MAY carry a claimed signing time.** In `cms/ecdsa-p256-sha256` that is
the CMS signing-time signed attribute (RFC 5652), which sits inside the signature
and therefore cannot be altered without breaking it. What nothing vouches for is
the clock: the value is the signer's assertion that they signed then, and no
more. A reader that shows it **MUST** show it as claimed rather than proven, and
**MUST NOT** derive from it that one record precedes another. [APR-ATTEST-014]

> Rationale: every signature format works this way, and pretending otherwise is
> how a plausible timestamp becomes evidence it was never entitled to be.
> Trusted time needs a time authority, which this baseline does not define. A
> claimed time is still worth carrying, because it is what the signer said.

### 12.5 Witnesses {#witnesses}

`witnesses` is an ordered, duplicate-free list of semantic digests of earlier
attestation envelopes, again excluding `proofs`. It records that this
attestation's signer explicitly witnessed those assertions.

Witnessing neither authorizes a change nor proves a clock order, workflow
acceptance, real-world identity, or trusted time.

### 12.6 Changed forms {#changed-forms}

A changed form is another complete form occurrence with a different subject
digest. Earlier attestations remain assertions about their original subject and
**MUST NOT** be transferred to the changed form. [APR-ATTEST-009]

Multiple attestations may target one unchanged form, and an attestation may be
encountered before its subject.

**Say what happened, not only what is missing.** An attestation whose subject
resolves to nothing is `unresolved` ([Verification vocabulary](#verification)),
and that is the whole of what the vocabulary states. Where a verifier holds a
form occurrence that is not the subject, it **SHOULD** report both facts: that
the attested form is absent, and that a different form is present. [APR-ATTEST-020]

> Rationale: the common cause of an unresolved attestation is not a lost file. It
> is that somebody opened the form, corrected a typo and saved. Reporting only
> that the signed document is missing is true and sends that person looking for a
> file that was never lost, when what they need to know is that the one in front
> of them is no longer the one that was signed. The result stays `unresolved`,
> because the assertion really does have no subject here; what this adds is the
> second fact beside it.

> Rationale: this is how a workflow builds revision history without APR defining
> one. A sequence of forms with attestations that witness each other is a chain
> whose relationships are proved. A sequence of forms without them is just
> several forms, and the format declines to guess.

### 12.7 Verification vocabulary {#verification}

Verification reports these independent facts:

| Result | Meaning |
| --- | --- |
| `valid` | The subject resolved, digest and manifest match, and a recognized proof verifies. |
| `invalid` | A recognized proof fails, or a resolved subject differs from the attested digest or manifest. |
| `unresolved` | No matching form occurrence is available. |
| `unverifiable` | Required representation, extension, digest, or proof support is unavailable. |
| `witnessed` | One or more referenced envelopes resolve and match. |

These are independent, and a verifier **MUST** report them independently: an
attestation may be both `unverifiable` and `witnessed`, and `unresolved`
**MUST NOT** be reported as a failure of the assertion. [APR-ATTEST-018]

**Validity is independent of trust.** A self-signed certificate can produce a
perfectly valid proof that proves nothing about identity. Implementations
**MUST** report these separately. [APR-ATTEST-010]

> Rationale: collapsing content validity and certificate trust into one green
> checkmark teaches people to trust a checkmark that does not mean what they
> think.

### 12.8 Attestations never gate the data {#never-gate}

**An attestation is an assertion about a document, never a permission to read
it.** An implementation **MUST NOT** treat the presence, absence, or state of an
attestation as authorization to read, or to withhold, the data a form
carries. [APR-ATTEST-019]

**Attesting is never required.** A form with no attestation is a complete,
ordinary, fully valid APR document. An implementation **MUST NOT** require one in
order to save, send, accept, or process a form, and **MUST NOT** present an
unattested document as deficient. [APR-ATTEST-011]

**Acting on an attestation is never required.** An implementation **MUST NOT**
refuse to parse, validate, render, print, export, or extract data from a document
because its attestations are absent, unrecognized, expired, untrusted, or
outright invalid. Attestation state **MUST NOT** appear in the validation error
list. [APR-ATTEST-012]

An implementation **MAY** warn, badge, or refuse to *act* on a document by its
own policy — a receiving workflow is entitled to reject an unattested permit
application. That is the workflow's decision. It is not the file format's, and a
reader that enforces it on the workflow's behalf has taken a choice away from
every other consumer of the same document. [APR-ATTEST-013]

> Rationale: the reasoning is the same one behind
> [Any string is a valid response](#any-string). A format that withheld data
> until a cryptographic condition was met would fail exactly when it is most
> needed: an expired certificate, a verifier that does not recognize a proof
> type, a proxy that re-encoded the bytes, an archived form whose signing
> authority no longer exists. In every one of those cases the answers a person
> wrote are still there, still true, and still the reason the document exists.
> **The data outlives the attestation, and the format must let it.**

---

## 13. Rendering {#renderers}

APR carries no presentation data. A renderer decides everything, and a GUI, web
page, terminal, voice system, and API client are equally legitimate.

### 13.1 Requirements for renderers {#renderer-requirements}

- Section titles and prompt labels **MUST** be presented as the accessible name. [APR-RENDER-001]
- A placeholder **MUST NOT** be the only label. [APR-RENDER-002]
- `helpText` **MUST** be programmatically associated with its prompt, not merely
  adjacent to it. [APR-RENDER-003]
- Section nesting **MUST** be conveyed structurally — heading levels, groups,
  landmarks — not by indentation alone. [APR-RENDER-004]
- Every prompt **MUST** be reachable and completable by keyboard. [APR-RENDER-005]
- A renderer **MUST NOT** block saving because of a hint mismatch. [APR-RENDER-006]
- Table sections **SHOULD** be presented with header association, not as a purely
  visual grid. [APR-RENDER-007]

These are format-level requirements, not house style. APR's structure is what
makes an accessible rendering possible; a renderer that discards it discards the
reason to use APR.

### 13.2 Ordering {#ordering}

Presentation is otherwise free, but **order is data**. A form asks its questions
in a sequence its author chose, and two renderers that disagree about that
sequence are showing two different forms.

1. Sections are presented in array order.
2. Prompts within a section are presented in array order.
3. **A section's own prompts are presented BEFORE its child sections.**

Rule 3 follows document convention: a heading's own content precedes its
subheadings. It is normative.

A renderer **MAY** paginate, group, or lazily load, but **MUST NOT** reorder. A
wizard that shows one section at a time still visits them in array order. [APR-RENDER-008]

### 13.3 Export {#export}

Exports to PDF, HTML, or print **MAY** introduce layout — page size, margins,
footers. That layout belongs to the renderer's options and **MUST NOT** be
written back into the APR document. The document stays presentation-free no
matter how many ways it has been rendered. [APR-RENDER-009]

---

## 14. Security considerations {#security}

**No executable content.** APR contains no scripts, macros, formulas with host
access, or external references. Opening an APR document from an untrusted sender
executes nothing. This is the format's most important security property and
**MUST NOT** be weakened. Expressions are pure, bounded, and non-Turing-complete;
they are not an exception. [APR-SEC-009]

**No network access on open.** Reading a document **MUST NOT** fetch anything.
`submissionUrls` is data — no entry **MUST** be contacted without an explicit
user action, and neither **MUST** a certificate endpoint. [APR-SEC-010]

**Resource bounds.** A reader **MUST** bound nesting depth and **SHOULD** bound
document size, stream length, and evaluation cost, failing cleanly rather than
exhausting memory. Parsing **MUST** terminate. [APR-SEC-011]

The depth bound has a floor this document states: sixteen levels
([Nesting depth](#nesting)). The others are deliberately the implementation's
to choose, because the right limit for a phone and for a batch importer are not
the same number, and a number here would be wrong for one of them. What is not
optional is that a limit exists and that reaching it is a clean refusal rather
than a crash.

> Decision (beta.6): concrete limits above the 16-level nesting floor are
> **implementation-defined**. No numeric ceiling is specified because no test
> enforces one, and a limit that nothing verifies is a limit implementations will
> disagree about.

**Deceptive text.** A filler's response is preserved and rendered defensively
instead of silently cleaned. Author-supplied members a machine acts on — above
all `metadata.submissionUrls` — are checked and refused at authoring time.
Spending strictness on the answer rather than on the submission target protects
nothing and destroys data.

**Attestations are not authorization.** A valid proof shows bytes are unaltered.
It does not establish that the signer is who they claim, that they were entitled
to sign, or that the form should be acted on. Conversely, an absent or failing
attestation is not a reason to withhold data from a reader — it is information
the reader is entitled to have alongside the data, not instead of it.

**What an attestation reveals.** A manifest reveals the *shape* of a form: its
pointers name every attested path. A `fields` attestation additionally reveals
which prompts were selected. Neither reveals response values. A certificate chain
in a proof carries the signer's identity in cleartext.

**Responses may be sensitive.** APR documents routinely hold personal data in
plain text. The format provides no encryption, no access control, and no
redaction; protection at rest and in transit is the surrounding system's
responsibility.

---

## 15. Conformance checklist {#checklist}

An implementation claiming **APR 1.0-beta.6 core** MUST:

- [ ] Parse UTF-8 in both representations; reject malformed input rather than coercing it
- [ ] Reject a response given as a JSON number or boolean
- [ ] Reject a structural member given in the wrong JSON type; `canAddRows` is a boolean, `maxRows` an integer
- [ ] Read a null or absent response as the empty string; never write null
- [ ] Reject any `aprVersion` other than `1.0-beta.6`
- [ ] Report `RETIRED_EMBEDDED_SIGNATURES` for a `signatures` member
- [ ] Treat `documentType` as authoritative; never infer type from a filename
- [ ] Label APR content with its `vnd.apr` media type; never infer behaviour from a generic one
- [ ] Require `metadata.title`, section `id` and `title`, prompt `id` and `label`
- [ ] Enforce document-wide id uniqueness in both namespaces; never generate or replace an id unless asked, and then only by the content rule
- [ ] Require content in every section, tables included, and at least one instance in a table
- [ ] Treat a table as structure, never as licence for layout data
- [ ] Derive table headers from the corresponding prompts' labels; correspond by position
- [ ] Require `templateId` on a filled form, as a URI, and never fetch it
- [ ] Preserve `metadata.regarding`, treat an unresolved reference as valid, and never read a reference as revision, authority or trust
- [ ] Support at least 16 levels of section nesting
- [ ] Ignore unknown members without rejecting them, and preserve them on write
- [ ] Name every extension member with an owned reverse-DNS prefix; add no unprefixed member
- [ ] Drop retired members rather than preserving them
- [ ] Degrade an unrecognized `expectedDataType` to text
- [ ] Treat `signature` and `file` as unregistered types; never present a signature widget as evidence
- [ ] **Never reject, alter, or block a response because of a hint**
- [ ] Never alter a response on the basis of a hint
- [ ] Report — never rewrite — hidden characters in every `submissionUrls` entry
- [ ] Refuse, at authoring time, human-facing text that is not NFC or carries invisible, deprecated or control code points
- [ ] Deliver only by a single `PUT` to an `https` entry or an attachment to a `mailto` entry, on an explicit user action, never following a redirect
- [ ] Preserve every response byte-for-byte across a round-trip
- [ ] Produce identical semantic models from paired JSONC and YAML documents
- [ ] Preserve attestation records and `expr*` strings even when not implementing them
- [ ] Never gate parsing, validation, rendering, or data extraction on attestation state
- [ ] Pass every fixture in `tests/Conformance/beta6/`, and every executable
      example in this document

An implementation additionally claiming **`core+streams`** MUST:

- [ ] Read and write RS-framed APR-JSONC streams and APR-YAML document streams
- [ ] Yield every record, including repeated identical form occurrences
- [ ] Never deduplicate, reorder, or select a record by position
- [ ] Return `APR_STREAM_REQUIRES_ITERATION` from a single-form API given a stream
- [ ] Refuse a stream that mixes representations
- [ ] Produce the same semantic records from either member of a paired stream

An implementation additionally claiming **`core+attestations`** MUST, and MUST
also satisfy `core+streams`:

- [ ] Compute `jcs-sha256` digests over the semantic model, extension members included
- [ ] Build manifests that hold no plaintext of the values they describe
- [ ] Resolve an attestation to its subject by digest, whatever the record order
- [ ] Hold an unresolved attestation until its subject is observed, and report `unresolved` if it never is
- [ ] Verify a `cms/ecdsa-p256-sha256` proof over the proof-free envelope
- [ ] Report an unrecognized proof type as `unverifiable`, never `invalid`, and preserve it
- [ ] Resolve a witness to the exact earlier envelope it names
- [ ] Report `valid`, `invalid`, `unresolved`, `unverifiable` and `witnessed` independently
- [ ] Keep certificate trust separate from cryptographic validity
- [ ] **Never gate parsing, validation, rendering, export or extraction on attestation state**

An implementation additionally claiming **`core+expressions`** MUST:

- [ ] Evaluate CEL as cel-spec `v0.25.3` defines it, and pass that release's conformance suite
- [ ] Provide the CEL standard library and standard macros, and no extension library or custom function
- [ ] Bind each response by its prompt's declared type
- [ ] Treat an unconvertible or blank typed response as unbound, never as a default
- [ ] Supply `_this`, `_id`, `_now`, `_today` and `ctx`, and let no prompt id shadow them
- [ ] Take `_now` and `_today` from the caller, never from the host clock
- [ ] Apply the per-hint fallback on any failure, showing more and blocking less
- [ ] Never overwrite a non-empty response found in a document being read; offer the recomputed value as a suggestion
- [ ] Order computed prompts by their direct references
- [ ] Bound evaluation, and report a reached bound as a fallback rather than partial mutation

---

## 16. Open questions {#open-questions}

An honest list of what this baseline does not settle.

1. **Media types not yet registered.** `application/vnd.apr+json`,
   `application/vnd.apr+yaml` and `application/vnd.apr+json-seq` are defined
   ([Document type](#media-types)) but the IANA vendor-tree registration has
   not been filed.
2. **No governance.** A format used by public institutions eventually needs
   stewardship that is not a single repository.

---

## 17. Change history {#history}

| Format version | Change |
| --- | --- |
| `1.0-beta.6` | Retired embedded `signatures` and `apr-sig-v3` in favour of independent attestation records. Added the APR-JSONC and APR-YAML representations, representation-neutral record streams, `jcs-sha256` semantic digests, integrity manifests, and the verification vocabulary. Replaced MAJOR.MINOR compatibility with exact-match version rejection. Structural members now use native JSON types; only responses are always strings. Removed the `signature` and `file` data types: signing is an attestation, and attachments have no representation. Reserved unprefixed member names to the specification; extension members carry a reverse-DNS prefix. Defined the `vnd.apr` media type family. Defined submission as a pre-signed HTTPS PUT or a mailto attachment, and nothing else. `templateId` is a URI. Removed `filledBy`, `filledDate` and `responseMetadata` entirely as workflow state: `inferredDataType` and `lastModified` went first, and `source` followed once recomputation was restated to protect every response already in the file rather than only an unmarked one. Human-facing text is held to UTS #39 by reference. Defined content-derived generated ids for repair. Renamed the format-version member from `version` to `aprVersion` on both record kinds. Added `metadata.regarding`, so a workflow step is an ordinary form naming the records it was completed against. Made a table's first instance required rather than merely described, adding `EMPTY_TABLE`. Gave identifiers to four obligations that had none, and split five rules that bundled obligations failing independently. Made manifest ordering and completeness normative, stated that a proof may carry a claimed signing time, and stated precisely what a `fields` scope protects. |
| `1.0-beta` | Made `documentType` authoritative over the filename extension. Replaced the table layout model with a structural table claim, removing column records and width data. Adopted CEL for expressions. Added roles, the bounds family, and normative text handling. Set the 16-level nesting floor. Removed localization, attachments, response identifiers, submission history, and the structured publisher and version objects. |

---

## 18. Normative references {#normative-references}

Compliance with this specification requires the editions below.

| Designation | Title |
| --- | --- |
| BCP 14 | Key words for use in RFCs (RFC 2119 and RFC 8174) |
| RFC 3339 | Date and Time on the Internet: Timestamps |
| RFC 3629 | UTF-8, a transformation format of ISO 10646 |
| RFC 3986 | Uniform Resource Identifier (URI): Generic Syntax |
| RFC 4151 | The 'tag' URI Scheme |
| RFC 4648 | The Base16, Base32, and Base64 Data Encodings |
| RFC 4918 | HTTP Extensions for Web Distributed Authoring and Versioning (WebDAV) |
| RFC 5234 | Augmented BNF for Syntax Specifications (ABNF), the notation used for the grammars here |
| RFC 5280 | Internet X.509 Public Key Infrastructure Certificate and CRL Profile |
| RFC 5652 | Cryptographic Message Syntax (CMS) |
| RFC 6068 | The 'mailto' URI Scheme |
| RFC 6838 | Media Type Specifications and Registration Procedures |
| RFC 6839 | Additional Media Type Structured Syntax Suffixes |
| RFC 6901 | JavaScript Object Notation (JSON) Pointer |
| RFC 7464 | JavaScript Object Notation (JSON) Text Sequences |
| RFC 8259 | The JavaScript Object Notation (JSON) Data Interchange Format |
| RFC 8785 | JSON Canonicalization Scheme (JCS) |
| RFC 9110 | HTTP Semantics |
| RFC 9512 | The application/yaml media type |
| FIPS 180-4 | Secure Hash Standard, for SHA-256 |
| FIPS 186-5 | Digital Signature Standard, for ECDSA over the P-256 curve |
| UAX #9 | Unicode Bidirectional Algorithm |
| UAX #15 | Unicode Normalization Forms |
| UTS #39 | Unicode Security Mechanisms |
| YAML 1.2.2 | YAML Ain't Markup Language, revision 1.2.2 |
| S3 pre-signed URL | Amazon S3, *Authenticating Requests: Using Query Parameters (AWS Signature Version 4)*, <https://docs.aws.amazon.com/AmazonS3/latest/API/sigv4-query-string-auth.html> |
| CEL | Common Expression Language, cel-spec release `v0.25.3`, <https://github.com/cel-expr/cel-spec/releases/tag/v0.25.3> |

The CEL entry is normative for the `core+expressions` profile only.

## 19. Informative references {#informative-references}

| Designation | Title |
| --- | --- |
| ISO 8601 | Date and time representations. RFC 3339 is the normative profile used here. |
| ECMA-404 | The JSON Data Interchange Syntax, the parallel standardization of RFC 8259 |
| CommonMark | A strongly defined, highly compatible specification of Markdown |
| UTR 36 | Unicode Security Considerations |

---

## 20. Appendix A: Minimal valid document {#appendix-minimal}

**Example 8.** The smallest conformant APR form.

```jsonc
{
  "aprVersion": "1.0-beta.6",
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

## 21. Appendix B: The rule to remember {#appendix-rule}

If you implement nothing else correctly, implement this:

> **Any string is a valid response, and a hint never says otherwise.**

Everything else in APR is structure. That rule is the point.

## 22. Appendix C: Provenance of this text {#provenance}

Non-normative.

This document is written from APR's design record, not from any implementation.
It merges the beta.3 specification, which supplies the core form profile, with
the beta.6 design decisions, which supply the changes made deliberately since.
The schema and conformance corpus scope which features this baseline carries: a
feature an earlier text described and beta.6 dropped is not revived here by being
written down again.

**Implementations are not a source.** Where a shipped implementation and this
document disagree, the implementation has a defect to fix.

> Rationale: the alternative — writing a specification by reading the code —
> produces a document that ratifies accidents and cannot be used to judge whether
> the code is right.

## 23. Appendix D: Corpus gaps {#corpus-gaps}

Non-normative.

Every rule in this document is normative. A rule with no vector is a corpus
defect rather than a specification gap, because the corpus is derived from this
document ([Authority](#scope)).

Closed by the examples embedded here: the forbidden APR-YAML constructs — tags,
merge keys, and directives as well as anchors — scalar resolution of bare words,
legacy boolean spellings and non-finite floats, JSONC trailing commas and
comments inside strings, and mixed-representation stream rejection.

Still without a vector:

- manifest vectors across the full range of changed member kinds. Each needs a
  computed `jcs-sha256` digest, so these belong in the corpus beside the existing
  digest vectors rather than embedded here.

---

## 24. Appendix E: What earns a primitive {#primitives}

Non-normative.

This document is asked from time to time to add a record kind or a structural
element for something common and important. The answer has usually been no.
This appendix records the test, so the next such question is decided the same
way as the last.

**A concept earns a primitive when its content cannot be posed as questions and
answers, and when a reader computes over it rather than rendering it.**

The two halves are one distinction seen from either side.

An attestation earns its record kind on both counts. Nobody types a digest, so
there is no question to ask and no label to render, and its correctness is
settled by computation — a mode this format refuses for responses
([Any string is a valid response](#any-string)). Nothing about it would fit
inside a form either: a manifest is nested computed structure, and a response is
always a string a person typed ([Responses are strings](#responses)).

A table does not, and [Tables](#tables) says so directly. Rows are ordinary
sections and cells are ordinary prompts, and carrying `kind` is a claim about
structure that already exists rather than a new thing. Tables are as common as
anything proposed since, and commonness is not the test.

A receipt does not either. What it holds — when something arrived, under what
reference, by what route — is answers with labels, read by people. It is a form
an office fills in, and [Related records](#regarding) is how it names what it
was about. The separate claim that the bytes received are the bytes sent is an
attestation made by the receiver, which needs nothing new either.

The failure this test prevents is a format that grows a document kind for every
common step of every process. Fixing what a receipt contains would mean
modelling one jurisdiction's intake — the mistake [Hints](#hints-object) already
refuses at the field level when it declines country-specific types. A vocabulary
of published templates carries that weight one layer up, where two offices
disagreeing costs nobody else anything.

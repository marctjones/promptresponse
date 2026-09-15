# APR File Format Specification {#apr-specification}

**Specification document version:** 1.0.0-beta.7-draft
**Describes format version:** `1.0-beta.7`
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

**This document is the normative definition of APR `1.0-beta.7`.** Everything
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

**An implementation is not a source either.** Where an implementation and this
document disagree, the implementation has the defect. A rule that no example or
vector exercises is a defect in the corpus, not a gap in this document.

> Rationale: a specification written by reading code ratifies that code's
> accidents, and can no longer judge whether the code is right.

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
5. [Rendering](#renderers) and [security](#security) constrain renderers,
   readers, and hosts.

This navigation is informative. The requirement language in the sections it
links to remains authoritative.

### 1.2 Requirement language {#normative-language}

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**,
**SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **NOT RECOMMENDED**, **MAY**, and
**OPTIONAL** in this document are to be interpreted as described in BCP 14
(RFC 2119, RFC 8174) when, and only when, they appear in all capitals, as shown
here.

Lowercase uses carry their ordinary English meaning and impose no requirement.

### 1.3 Document conventions {#conventions}

This document is written to W3C QA SpecGL, the W3C QA Framework: Specification
Guidelines ([Informative references](#informative-references)). Four kinds of text appear
here and are distinguished deliberately.

- **Normative text** states requirements. A requirement names the class of
  product it binds, from [Terminology](#terminology), uses one keyword of
  [Requirement language](#normative-language), and ends with its rule
  identifier.
- **Normative tables** state uniform requirements, one per row, with a
  Requirement column holding the keyword and a Rule column holding the rule
  identifier.
- **Examples** are numbered within their section and captioned
  `Example 4.5.1-3` for the third example in section 4.5.1. Each is executable:
  its header names the rules its document satisfies or violates and the outcome
  an implementation reports, and the conformance suite is extracted from it.
  Examples are informative. Where an example and normative text disagree, the
  normative text governs.
- **Rationale** appears in blockquotes beginning `Rationale:` and is
  non-normative. Removing every rationale block would not change the format.

An informative section says so in its opening or closing sentence and states no
requirement.

Each heading carries an explicit anchor, written `{#anchor-name}`.

**Every requirement carries a rule identifier**, written `[APR-AREA-NNN]` at the
end of the requirement it names. A test, a coverage manifest, or a defect report
cites the identifier rather than a section, so what is being referred to does
not depend on where it currently sits.

Identifiers are append-only. A new rule takes the next free number in its area, a
deleted rule's number is never reused, and moving a rule between sections does
not renumber it. Nothing about an identifier is positional, so inserting a
requirement cannot renumber its neighbours.

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
| **Format version** | only on a breaking change to the wire format | the `aprVersion` member of every record | `1.0-beta.7` |
| **Specification document version** | every release | this document's header | `1.0.0-beta.7-draft` |
| **Conformance corpus tag** | when the corpus is revised | `tests/Conformance/beta6/` and a git tag | `corpus/beta6` |

The format version changes only with the wire format. Two releases that do not
change the wire format declare the same format version.

The corpus tag names a revision of the conformance corpus, not a release of this
document. It is a path and a git tag rather than a version of the format, so a
conformance claim cites the tag it was measured against
([Declaring conformance](#declaring-conformance)) and nothing has to be renamed
because a version number moved.

#### 1.4.1 Version compatibility {#version-compatibility}

A reader **MUST** reject a record that states an `aprVersion` other than
`"1.0-beta.7"` or `"1.0-beta.6"`, reporting `UNSUPPORTED_VERSION`. [APR-SEC-002]

A record states a version by carrying a non-empty `aprVersion`. An absent or
blank member states none, so it is the missing member it is, reported as
`REQUIRED_FIELD` ([Errors](#structural-validation)). Only a version a record
actually carries can be unsupported.

`1.0-beta.7` is the current format version. `1.0-beta.6` is accepted alongside it
because beta.7 adds no member to a record and removes none: every beta.6 record is
a beta.7 record already, and refusing one would discard a document for a number
rather than for its content.

The bump is a breaking change in the forward direction only. A reader that knows
only beta.6 refuses a beta.7 record, which is what makes it a format version rather
than a document release; tolerance runs the other way, so nothing written before
this version stops being readable.

**Example 1.4.1-1.** A record at this format version.

```apr-example
id: version-exact
rule: version-compatibility
satisfies: APR-SEC-002
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 1.4.1-2.** A record at any other format version.

```apr-example
id: version-other
rule: version-compatibility
violates: APR-SEC-002
representation: jsonc
expect: reject
diagnostic: UNSUPPORTED_VERSION
---
{
  "aprVersion": "2.0",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

Unknown members are preserved across a round trip within a version
([Unknown members](#extensions)), which is what keeps additive change safe.

**What BETA means here:** breaking changes are intentional until the first
public release.

---

## 2. Terminology {#terminology}

These terms carry the meanings below throughout. Where a term is also an
ordinary English word, the definition here governs.

Every requirement names its subject from the classes of product defined first
below. A requirement on a document names the form.

**implementation** — software that claims conformance to APR: to `core` and to
each profile it names ([Declaring conformance](#declaring-conformance)). An
implementation is one or more of a reader, writer, validator, renderer, or
verifier, and a requirement on an implementation applies to each of these it is.

**reader** — software that reads a representation into the semantic model,
accepting or rejecting each record.

**writer** — software that serializes a semantic model into a representation.

**validator** — software that checks a form against this document and reports
errors and warnings ([Validation](#validation)).

**renderer** — software that presents a form to a person and collects their
responses ([Rendering](#renderers)).

**verifier** — software that evaluates attestations against the forms they
identify ([Attestations](#attestations)).

**host** — the software that embeds an implementation and supplies what a
document leaves outside itself: evaluation context, the current time, and policy
([Expressions](#expressions)).

**form** — the complete semantic model of one APR document: its version,
document type, metadata, section tree, and roles. A form is ordinary data and
carries no cryptographic assertion of its own.

**template** — a form whose `documentType` is `template`: the questions, without
any particular respondent's answers.

**filled form** — a form whose `documentType` is `filledForm`: the questions
together with one respondent's answers, naming the template it answers.

**filler** — the person who answers a form's prompts.

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

**profile** — a named set of conformance requirements beyond `core` that an
implementation chooses whether to claim. Optional to claim; binding once
claimed.

**extension member** — a member APR does not define, named with a reverse-DNS
prefix by the producer that owns it, carried by a document and preserved across
a round trip ([Unknown members](#extensions)).

**non-blank string** — a string containing at least one non-whitespace
character. A whitespace-only string is treated as absent.

---

## 3. Conformance profiles {#conformance}

APR is deliberately layered so that a complete, useful implementation can be
written in an afternoon, in any language, on any device.

Conformance is stated per profile. To conform to a profile, an implementation
meets every requirement in the sections that define it. The level of each
profile is stated below, one per row.

| Profile | Defined by | Requirement | Rule |
| --- | --- | --- | --- |
| `core` | [About this document](#scope), [Conformance profiles](#conformance), [Representations](#representations) through [Text handling](#text-handling), [Rendering](#renderers), and [Security considerations](#security) | **REQUIRED** | [APR-CONF-006] |
| `core+streams` | [`core+streams`](#profile-streams), [Streams](#streams), and [Semantic digests](#digests) | **OPTIONAL** | [APR-CONF-007] |
| `core+attestations` | [`core+attestations`](#profile-attestations) and [Attestations](#attestations) | **OPTIONAL** | [APR-CONF-008] |
| `core+expressions` | [`core+expressions`](#profile-expressions) and [Expressions](#expressions) | **OPTIONAL** | [APR-CONF-009] |

A subsection belongs to the profile of the section that contains it, unless a row
names the subsection itself.

An implementation that claims `core+attestations` **MUST** also claim
`core+streams`. [APR-CONF-010]

### 3.1 `core` {#profile-core}

`core` covers reading, validating, filling, and writing one document in both
representations, as the sections the `core` row names define.

A core implementation is fully conformant. It is not a degraded one, and it need
not emit HTML, PDF, or native controls. It exposes the semantic document and its
advisory hints for a host or renderer to use.

What a profile adds is optional, and what an implementation does with a document
or stream that uses a profile it does not claim is part of `core`.

A reader that does not claim `core+streams`, given a stream, **MUST** report
`APR_STREAM_REQUIRES_ITERATION` rather than select a record by position. [APR-CONF-001]

An implementation that does not claim `core+attestations` **MUST NOT** report a
document as verified. [APR-CONF-005]

An implementation that does not claim `core+attestations` **SHOULD** indicate
that attestations are present but unchecked. [APR-CONF-012]

A reader that does not claim `core+expressions` **MUST NOT** reject a document
because it uses expressions. [APR-CONF-003]

A writer that does not claim `core+expressions` **MUST** preserve expression
strings across a round trip. [APR-CONF-013]

A host rendering such a document presents those prompts as ordinary editable
fields: a computed field becomes a field a person can type into, degraded but
never broken and never lost.

**Example 3.1-1.** An expression a writer preserves without evaluating it.

```apr-example
id: expression-preserved
rule: profile-core
satisfies: APR-CONF-003, APR-CONF-013
representation: jsonc
expect: valid
round-trip: true
preserves: /sections/0/prompts/1/hints/exprValue
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "a", "label": "A", "response": "2" },
        { "id": "total", "label": "Total", "hints": { "exprValue": "string(a)" } }
      ]
    }
  ]
}
```

### 3.2 `core+streams` {#profile-streams}

`core+streams` adds reading and writing streams of independent records
([Streams](#streams)).

A reader that claims `core+streams` but not `core+attestations` **MUST NOT**
reject a stream because it contains attestation records. [APR-CONF-002]

A writer that claims `core+streams` but not `core+attestations` **MUST** preserve
attestation records across a round trip. [APR-CONF-011]

### 3.3 `core+attestations` {#profile-attestations}

`core+attestations` adds computing semantic digests and manifests, resolving
attestations against forms, looking up witnesses, and reporting the verification
vocabulary ([Attestations](#attestations)).

> Rationale: `core+attestations` is optional for a reason of policy, not merely
> of cost. **Nobody is obliged to sign, and nobody is obliged to care that
> something was signed.** A recipient may have every reason to trust a document
> by other means — they know the sender, they requested the form, the data is
> low-stakes, or they simply want to read it. Requiring verification before data
> can be used would impose the form author's threat model on every reader, which
> is not a decision the file format gets to make. Saying nothing is better than
> saying verified, and saying "present, unchecked" is better than both. See
> [Attestations never gate the data](#never-gate).

### 3.4 `core+expressions` {#profile-expressions}

`core+expressions` adds evaluating the `expr*` hint family
([Expressions](#expressions)).

### 3.5 Declaring conformance {#declaring-conformance}

An implementation **MUST** state, in its conformance claim, the profiles it
claims, the submission transports it implements (`https`, `mailto`, both, or
none — [Submission targets](#submission)), and the corpus revision it passes. [APR-CONF-014]

"APR 1.0-beta.7 core+streams, submits https, corpus beta6 @ `<sha>`" is a
complete claim.

An implementation **MUST NOT** claim a profile without passing the corpus
revision it names. [APR-CONF-004]

An Implementation Conformance Statement lists, for each profile and class of
product, every requirement a claim covers.

---

## 4. Representations {#representations}

APR has two representations, APR-JSONC and APR-YAML, and each spells the same
semantic model. Comments, whitespace, indentation, scalar spelling, and mapping
order are source trivia, with no semantic effect.

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

Moving from presentation to model discards source trivia, which carries no APR
meaning, so a round trip is free to change it.

An implementation that reads a document and writes it back **MUST** preserve
every part of its semantic model, including members APR does not define.
[APR-REP-001]

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

Each production this document defines carries a number in brackets, as `[1]`, and
the text cites a production by that number. A production number in
[APR-YAML](#apr-yaml) is YAML 1.2.2's own.

### 4.3 Encoding {#encoding}

A document **MUST** be encoded as UTF-8 (RFC 3629). [APR-REP-002]

A writer **SHOULD NOT** write a byte-order mark. [APR-REP-018]

A reader **SHOULD** accept a document that begins with a byte-order mark.
[APR-REP-019]

A reader **MUST** reject ill-formed UTF-8 rather than substituting replacement
characters. [APR-REP-003]

A string in a document **MUST NOT** contain U+0000, an unpaired surrogate in the
range U+D800 to U+DFFF, or a control character in the range U+0001 to U+001F
other than tab (U+0009), line feed (U+000A), and carriage return (U+000D).
[APR-REP-004]

> Rationale: tab, line feed, and carriage return are allowed so that a multiline
> response holds the line breaks a person typed.

**Example 4.3-1.** A response carrying a tab, a carriage return and a line feed. These
are the three control characters the rule admits, so the document is valid and
the response keeps them.

```apr-example
id: encoding-allowed-controls
rule: encoding
satisfies: APR-REP-004, APR-TEXT-004, APR-VAL-036
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Notes" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "p", "label": "Observations",
          "response": "one\ttwo\r\nthree" }
      ]
    }
  ]
}
```

**Example 4.3-2.** A title carrying an escaped U+0000. The escape is well-formed
JSON, and the string it decodes to is not an APR string.

```apr-example
id: encoding-nul-refused
rule: encoding
violates: APR-REP-004
representation: jsonc
expect: reject
diagnostic: PARSE_ERROR
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Notes " },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 4.3-3.** A label carrying an unpaired surrogate.

```apr-example
id: encoding-unpaired-surrogate-refused
rule: encoding
violates: APR-REP-004
representation: jsonc
expect: reject
diagnostic: PARSE_ERROR
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Notes" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "Half \ud83d" } ] }
  ]
}
```

### 4.4 APR-JSONC {#apr-jsonc}

APR-JSONC is the JSON grammar of RFC 8259 with comments and trailing commas
admitted. It is defined as a delta against that grammar, per
[Syntax conventions](#syntax-conventions).

```abnf
; Imported unchanged from RFC 8259: value, member, string, number,
; begin-object, end-object, begin-array, end-array,
; name-separator, value-separator, and everything they reference.

apr-jsonc-text  = ws value ws                                  ; [1]

; REDEFINED. Whitespace admits comments, so a comment is legal
; wherever whitespace is, and nowhere else.
ws              = *( %x20 / %x09 / %x0A / %x0D / comment )     ; [2]

comment         = line-comment / block-comment                 ; [3]
line-comment    = %x2F.2F *( %x00-09 / %x0B-10FFFF )           ; [4]
block-comment   = %x2F.2A *( not-star / star-not-slash ) %x2A.2F  ; [5]
not-star        = %x00-29 / %x2B-10FFFF                        ; [6]
star-not-slash  = %x2A ( %x00-2E / %x30-10FFFF )               ; [7]

; REDEFINED. A trailing comma is permitted after the final element.
object          = begin-object                                 ; [8]
                  [ member *( value-separator member ) [ value-separator ] ]
                  end-object
array           = begin-array                                  ; [9]
                  [ value *( value-separator value ) [ value-separator ] ]
                  end-array
```

Because a comment is a production of `ws` [2], and `ws` never appears inside
`string`, **a comment sequence inside a string literal is not a comment.** The
`string` rule is imported unchanged, so `"// not a comment"` is an ordinary
string value. This is the first question an implementer asks, and the grammar
answers it rather than leaving it to prose.

Removing an APR-JSONC document's comments and trailing commas leaves JSON
(RFC 8259), which decodes to the semantic model.

An APR-JSONC document **MUST** match production [1] `apr-jsonc-text`.
[APR-REP-005]

The grammar cannot express one constraint. RFC 8259 §4 leaves an object with
two members of the same name to the parser, and a last-key-wins parser keeps
whichever came last.

A reader **MUST** reject an APR-JSONC object that has two members of the same
name, reporting `DUPLICATE_MEMBER`. [APR-REP-006]

> Rationale: last-key-wins makes a document's meaning depend on which parser
> reads it, which is precisely what a semantic digest cannot tolerate.

**Example 4.4-1.** A trailing comma after the final element.

```apr-example
id: jsonc-trailing-comma
rule: apr-jsonc
satisfies: APR-REP-005, APR-REP-006
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

**Example 4.4-2.** A comment sequence inside a string is not a comment, because
`ws` never occurs inside `string`.

```apr-example
id: jsonc-comment-inside-string
rule: apr-jsonc
satisfies: APR-REP-005
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "// not a comment /* nor this */" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 4.4-3.** A missing value separator. The comment is legal; the text
around it does not match `apr-jsonc-text`.

```apr-example
id: jsonc-missing-separator
rule: apr-jsonc
violates: APR-REP-005
representation: jsonc
expect: reject
diagnostic: PARSE_ERROR
---
// a comment
{
  "aprVersion": "1.0-beta.6"
  "metadata": { "title": "T" }
}
```

**Example 4.4-4.** Two members named `metadata`.

```apr-example
id: jsonc-duplicate-member
rule: apr-jsonc
violates: APR-REP-006
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

**Example 4.4-5.** A form in APR-JSONC, with comments that carry no meaning.

```apr-example
id: jsonc-permit-application
rule: apr-jsonc
satisfies: APR-REP-005
representation: jsonc
expect: valid
digest: sha256:2ceb181e7fa718df949ba40c13d8bbb02f3b0385565660166a29b534964cfc16
---
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

An APR-YAML document **MUST** be a well-formed YAML 1.2.2 stream, per that
specification's character, structural, flow, block, and document-stream
productions (chapters 5 to 9, through [211] `l-yaml-stream`). [APR-REP-007]

A member name in JSON is always a string, so a mapping key that resolves to
anything else has no JSON spelling.

Every mapping key in an APR-YAML document **MUST** resolve to a string by
[Scalar resolution](#yaml-resolution). [APR-REP-009]

**Excluded constructs.** Each construct below is YAML structure rather than
scalar resolution, so no choice of schema removes it. Each row is a requirement
on an APR-YAML document, and names the diagnostic a reader reports for a
document that breaks it.

| Construct | YAML 1.2.2 | Requirement | Diagnostic | Rule |
| --- | --- | --- | --- | --- |
| Anchors and aliases | [101] `c-ns-anchor-property`, [104] `c-ns-alias-node` | **MUST NOT** | `YAML_ANCHOR_FORBIDDEN` | [APR-REP-010] |
| Tags, including `!!str`, `!!binary`, and local tags | [97] `c-ns-tag-property` | **MUST NOT** | `YAML_TAG_FORBIDDEN` | [APR-REP-020] |
| Merge keys: a plain `<<` in key position | none; the merge key is a YAML 1.1 type | **MUST NOT** | `YAML_MERGE_KEY_FORBIDDEN` | [APR-REP-021] |
| Directives, including `%YAML` and `%TAG` | [82] `l-directive` | **MUST NOT** | `YAML_DIRECTIVE_FORBIDDEN` | [APR-REP-022] |

A character inside a scalar's content is not a construct. `&`, `*`, and `!`
within a plain scalar, as in `string(fee_count * 8.0)`, are ordinary characters,
and a quoted `"<<"` is an ordinary key.

A reader **MUST** use a safe loader: one that constructs only JSON values and
never instantiates a host-language object from document content. [APR-REP-013]

**Example 4.5-1.** The form of Example 4.4-5 in APR-YAML. The two have the same
semantic model, and therefore the same digest.

```apr-example
id: yaml-permit-application
rule: apr-yaml
satisfies: APR-REP-008, APR-REP-026
representation: yaml
expect: valid
digest: sha256:2ceb181e7fa718df949ba40c13d8bbb02f3b0385565660166a29b534964cfc16
---
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

**Example 4.5-2.** Indicator characters inside content, and quoted keys that
would otherwise be a merge key and a boolean. None is an excluded construct.

```apr-example
id: yaml-indicators-in-content
rule: apr-yaml
satisfies: APR-REP-009, APR-REP-010, APR-REP-020, APR-REP-021, APR-REP-022
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Fish & chips
  com.example.flags:
    "<<": kept
    "true": kept
sections:
  - id: s
    title: S
    prompts:
      - id: fee
        label: Fee
        response: string(fee_count * 8.0) !important
```

**Example 4.5-3.** A plain `true` as a key resolves to a boolean.

```apr-example
id: yaml-boolean-key
rule: apr-yaml
violates: APR-REP-009
representation: yaml
expect: reject
diagnostic: PARSE_ERROR
---
aprVersion: "1.0-beta.6"
metadata:
  title: Boolean key
  com.example.flags:
    true: kept
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

**Example 4.5-4.** An anchor.

```apr-example
id: yaml-anchor
rule: apr-yaml
violates: APR-REP-010
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

**Example 4.5-5.** A tag, though it names the type the scalar would have anyway.

```apr-example
id: yaml-tag
rule: apr-yaml
violates: APR-REP-020
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

**Example 4.5-6.** A merge key.

```apr-example
id: yaml-merge-key
rule: apr-yaml
violates: APR-REP-021
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

**Example 4.5-7.** A `%YAML` directive.

```apr-example
id: yaml-directive
rule: apr-yaml
violates: APR-REP-022
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

**Example 4.5-8.** A well-formed APR-YAML document.

```apr-example
id: yaml-well-formed
rule: apr-yaml
satisfies: APR-REP-007
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: T
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

**Example 4.5-9.** A member indented as if it were nested, which YAML cannot parse.

```apr-example
id: yaml-malformed-indentation
rule: apr-yaml
violates: APR-REP-007
representation: yaml
expect: reject
diagnostic: PARSE_ERROR
---
aprVersion: "1.0-beta.6"
metadata:
  title: T
 sections:
   - id: s
```

### 4.5.1 Scalar resolution {#yaml-resolution}

Resolution is stated exhaustively, because it is where YAML and JSON genuinely
differ and where a reference alone would be ambiguous. Each row is a requirement
on a reader resolving a scalar of an APR-YAML document, keys included.

| Scalar | Resolves to | Requirement | Rule |
| --- | --- | --- | --- |
| Any quoted scalar | a string, verbatim | **MUST** | [APR-REP-026] |
| Plain `null`, `Null`, `NULL`, `~`, or empty | null | **MUST** | [APR-REP-027] |
| Plain `true`, `True`, `TRUE`, `false`, `False`, `FALSE` | a boolean | **MUST** | [APR-REP-028] |
| A plain scalar matching JSON's `number` production (RFC 8259 §6) | a number | **MUST** | [APR-REP-029] |
| Any other plain scalar | a string | **MUST** | [APR-REP-008] |

A plain scalar denoting a non-finite float, such as `.inf`, `-.inf`, or `.nan` in
any capitalization, matches no row that JSON can represent.

A reader **MUST** reject a document containing a plain scalar that denotes a
non-finite float, reporting `YAML_NON_FINITE_NUMBER`. [APR-REP-011]

**APR defines its own YAML schema.** YAML 1.2.2 chapter 10 presents failsafe,
JSON, and core as *recommended* schemas and leaves a processor free to define
another. The table above is APR's.

An implementation therefore uses a YAML library for **syntax** — characters,
structure, flow and block style, document streams — and not for resolution.

A reader **MUST NOT** resolve a scalar by a YAML library's default schema.
[APR-REP-012]

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

The table alone decides a scalar's type, whatever the member expects. An
unquoted `metadata.templateVersion: 1.0` resolves as the number `1.0`, and
`templateVersion` is declared a string, so a reader rejects the document with
`WRONG_TYPE`, as it rejects `"templateVersion": 1.0` in APR-JSONC
([Value types](#json-subset)). Quoting the scalar spells the string `"1.0"`. A
response is no exception: an unquoted `response: 42` is the number 42, which
[Responses are strings](#responses) rejects.

**Example 4.5.1-1.** A bare word is a string.

```apr-example
id: yaml-bare-word-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-2.** A YAML 1.1 boolean spelling is a string.

```apr-example
id: yaml-legacy-boolean-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-3.** A leading zero is not a JSON number, so `012` is a string.

```apr-example
id: yaml-leading-zero-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-4.** An implicit timestamp is a string.

```apr-example
id: yaml-date-like-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-5.** A sexagesimal is a string.

```apr-example
id: yaml-sexagesimal-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-6.** A hexadecimal integer is a string.

```apr-example
id: yaml-hex-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-7.** An underscored number is a string.

```apr-example
id: yaml-underscored-number-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-8.** A decimal with no leading digit is a string.

```apr-example
id: yaml-bare-decimal-is-a-string
rule: yaml-resolution
satisfies: APR-REP-008, APR-REP-012
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

**Example 4.5.1-9.** A quoted `null` is a string.

```apr-example
id: yaml-quoted-null-is-a-string
rule: yaml-resolution
satisfies: APR-REP-026
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

**Example 4.5.1-10.** A quoted `1.0` is the string a string member declares.

```apr-example
id: yaml-quoted-scalar-stays-string
rule: yaml-resolution
satisfies: APR-REP-015, APR-REP-026
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Quoted template version
  templateVersion: "1.0"
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

**Example 4.5.1-11.** An unquoted `1.0` is a number, which a string member
refuses.

```apr-example
id: yaml-unquoted-scalar-wrong-type
rule: yaml-resolution
violates: APR-REP-015
representation: yaml
expect: reject
diagnostic: WRONG_TYPE
---
aprVersion: "1.0-beta.6"
metadata:
  title: Unquoted template version
  templateVersion: 1.0
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

**Example 4.5.1-12.** A plain `null` is the null value, and a null response reads
as the empty string.

```apr-example
id: yaml-plain-null-response-is-empty
rule: yaml-resolution
satisfies: APR-REP-014, APR-REP-024, APR-REP-027
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

**Example 4.5.1-13.** A plain `true` is a boolean and a plain `25` a number,
each the type its member declares.

```apr-example
id: yaml-boolean-and-number
rule: yaml-resolution
satisfies: APR-REP-011, APR-REP-028, APR-REP-029
representation: yaml
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: Expenses
sections:
  - id: expenses
    title: Expense line items
    kind: table
    canAddRows: true
    maxRows: 25
    sections:
      - id: item_1
        title: Item 1
        prompts:
          - id: item_1.description
            label: Description
```

**Example 4.5.1-14.** A non-finite float has no JSON value to resolve to.

```apr-example
id: yaml-non-finite-float
rule: yaml-resolution
violates: APR-REP-011
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

### 4.6 Value types {#json-subset}

APR uses a restricted subset of the JSON data model. A response is a string
([Responses are strings](#responses)). Every other member is **structural**: it
describes the form rather than carrying what a person typed.

A structural member uses the **JSON type that fits it**, natively: a flag is a
JSON boolean, a count is a JSON integer, a numeric bound is a JSON number, and a
name, identifier, or piece of text is a JSON string. Its member table declares
that type.

A writer **MUST** emit each structural member in the JSON type its member table
declares. [APR-REP-023]

A reader **MUST** reject a structural member of any other JSON type, reporting
`WRONG_TYPE` ([Errors](#structural-validation)). [APR-REP-015]

`"canAddRows": "true"` is not a boolean, and `"maxRows": "25"` is not a count.

A structural member that no JSON type fits **MUST** be a string in the form its
member table states: an RFC 3339 string for a timestamp, and for a bound on a
temporal field, that field's canonical write form
([Canonical value forms](#canonical-values)). [APR-REP-016]

A structural member is a string only when a string is the fitting type.

> Rationale: a response is a string because it carries what a person typed, and
> typing produces text. A row count does not come from a person; spelling it
> `"25"` obliges every reader to parse a number out of a string and to decide
> what `"25.0"` or `" 25"` mean, which is exactly the class of silent divergence
> the format exists to remove. Native types give the schema the check and give
> readers nothing to interpret.

`null` is not an APR value.

A writer **MUST NOT** emit `null`. [APR-REP-025]

A reader **MUST** read a `null` or absent `response` as the empty string.
[APR-REP-024]

A reader **MUST** reject a document in which a member other than `response` is
`null`. [APR-REP-014]

**Example 4.6-1.** A table section, with a boolean flag and an integer count.

```apr-example
id: table-section
rule: tables
satisfies: APR-MODEL-038, APR-REP-015
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

**Example 4.6-2.** The same flag and count, spelled as strings.

```apr-example
id: structural-member-wrong-type
rule: json-subset
violates: APR-REP-015
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

**Example 4.6-3.** A `null` outside a response.

```apr-example
id: null-outside-response
rule: json-subset
violates: APR-REP-014
representation: jsonc
expect: reject
diagnostic: PARSE_ERROR
---
{
  "aprVersion": "1.0-beta.6",
  "documentType": null,
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 4.6-4.** A date bound written as an RFC 3339 string.

```apr-example
id: date-bound-as-string
rule: json-subset
satisfies: APR-REP-016
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "p", "label": "P", "hints": { "expectedDataType": "date", "min": "2026-01-01" } }
      ]
    }
  ]
}
```

**Example 4.6-5.** A date bound written as a number.

```apr-example
id: date-bound-as-number
rule: json-subset
violates: APR-REP-016
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "p", "label": "P", "hints": { "expectedDataType": "date", "min": 2026 } }
      ]
    }
  ]
}
```

### 4.7 Responses are strings {#responses}

A reader **MUST** reject, at parse time, a `prompt.response` that is a JSON
number, boolean, array, or object. [APR-MODEL-001]

A reader **MUST NOT** coerce such a response into a string: `42` does not become
`"42"`, nor `true` `"true"`. [APR-MODEL-049]

Rejecting and coercing are different failures: one refuses the document, the
other accepts it having invented data.

> Rationale: silent coercion is worse than rejection. It produces a document that
> looks conformant while having invented data that no person entered.

**Example 4.7-1.** A response of digits, written as a string.

```apr-example
id: response-digits-string
rule: responses
satisfies: APR-MODEL-001, APR-MODEL-049
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S",
    "prompts": [ { "id": "p", "label": "P", "response": "42" } ] } ]
}
```

**Example 4.7-2.** A response written as a JSON number.

```apr-example
id: response-number
rule: responses
violates: APR-MODEL-001, APR-MODEL-049
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S",
    "prompts": [ { "id": "p", "label": "P", "response": 42 } ] } ]
}
```

### 4.8 Any string is a valid response {#any-string}

**This is the rule the rest of the format exists to protect.**

A `prompt.response` **MAY** be any string, whatever its hints ask for.
[APR-MODEL-002]

An implementation **MUST** support a `prompt.response` of at least **1 MiB**
(1,048,576 bytes) encoded as UTF-8. [APR-MODEL-127]

Above that floor the limit is the implementation's, for the reason
[Security considerations](#security) gives. A reader that applies one refuses
the document cleanly rather than truncating the response to fit.

> Rationale: "any string" is a promise no reader can keep without a number
> attached to it. A reader is free to bound document size, and with no floor
> stated, a reader that refused a two-kilobyte answer would be as conformant as
> one that accepted a book — so a filler would have no way to know whether what
> they wrote survives being saved. A megabyte is roughly 150,000 words: longer
> than any answer a person types into a form, and small enough that a phone can
> hold one. Above it, the ceiling is the implementation's, because the right
> limit for a phone and for a batch importer are not the same number.

The format has no opinion about whether that string is "correct".

| `expectedDataType` | Response | Document validity |
| --- | --- | --- |
| `number` | `about twelve` | **Valid** |
| `email` | `call me instead` | **Valid** |
| `date` | `the summer of 1985` | **Valid** |
| anything | empty | **Valid** |

The distinction is between **document validity** — is this well-formed APR? —
and **workflow acceptance** — will the receiving office act on it? A benefits
office is free to refuse a form for a blank field or an unparseable date. That is
a workflow decision, made by a workflow, and it has nothing to do with whether
the document is valid APR.

> Rationale: forms are filled by people under conditions the author did not
> anticipate. Someone whose legal name does not fit the field, whose address is
> not a street address, whose answer is "I don't know" — all produce valid APR. A
> format that rejected them would discard true information because it was
> inconveniently shaped.

### 4.9 Hints never enforce {#hints-advisory}

Every member of `prompt.hints` is advisory.

An implementation **MUST NOT** reject, alter, truncate, or refuse to save a
response because of a hint, including `validationPattern` and every member of
the `expr*` family. [APR-MODEL-003]

A response that does not match its `validationPattern` is a warning at most.

**Example 4.9-1.** A response that does not match its pattern. The document is
valid, and the mismatch is a warning.

```apr-example
id: pattern-mismatch-is-a-warning
rule: hints-advisory
satisfies: APR-MODEL-003
representation: jsonc
expect: valid
warns: RESPONSE_PATTERN_MISMATCH
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S",
    "prompts": [ { "id": "p", "label": "P", "response": "twelve",
      "hints": { "validationPattern": "^[0-9]+$" } } ] } ]
}
```

---

## 5. Document structure {#form-model}

### 5.1 Document {#root-object}

A form is a JSON object. Each row below is a requirement on a form, and its
Requirement column says whether the member is present. A validator reports a
member its row requires, missing or blank, and a member of another JSON type,
by the codes [Errors](#structural-validation) names.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `aprVersion` | string | **REQUIRED** | [APR-MODEL-053] | The version of *this specification* the record is written to, never the form's own ([Version compatibility](#version-compatibility)). |
| `documentType` | string | **OPTIONAL** | [APR-MODEL-054] | `template` or `filledForm`; absent means `template` ([Document type](#media-types)). |
| `metadata` | object | **REQUIRED** | [APR-MODEL-055] | [Metadata](#metadata) |
| `sections` | array | **REQUIRED** | [APR-MODEL-005] | At least one section ([Section](#section-object)). |
| `roles` | array | **OPTIONAL** | [APR-MODEL-056] | [Roles](#roles) |

**Example 5.1-1.** A form with no `metadata`.

```apr-example
id: form-without-metadata
rule: root-object
violates: APR-MODEL-055
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.1-2.** A form whose `sections` array is empty.

```apr-example
id: form-with-no-sections
rule: root-object
violates: APR-MODEL-005
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": []
}
```

**Example 5.1-3.** A form that carries no `aprVersion`. It states no version, so
it is refused as the missing member it is.

```apr-example
id: form-without-aprversion
rule: root-object
violates: APR-MODEL-053
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.1-4.** A form whose `aprVersion` is blank. A blank member states no
version either.

```apr-example
id: form-with-blank-aprversion
rule: root-object
violates: APR-MODEL-053
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

### 5.2 Metadata {#metadata}

Each row below is a requirement on `metadata`.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `title` | non-blank string | **REQUIRED** | [APR-MODEL-007] | The form's name. |
| `description` | string | **OPTIONAL** | [APR-MODEL-057] | Prose about the form as a whole. |
| `created` | date-time | **OPTIONAL** | [APR-MODEL-058] | RFC 3339. |
| `modified` | date-time | **OPTIONAL** | [APR-MODEL-059] | RFC 3339. |
| `author` | string | **OPTIONAL** | [APR-MODEL-060] | A person. |
| `publisher` | string | **OPTIONAL** | [APR-MODEL-061] | The organization standing behind the form. |
| `language` | string | **OPTIONAL** | [APR-MODEL-062] | A BCP 47 language tag for the form's human-facing authoring text. |
| `templateId` | string | **REQUIRED** when `documentType` is `filledForm` | [APR-MODEL-008] | A URI identifying the template a filled form answers ([Template identity](#template-identity)). Optional on a template. |
| `templateVersion` | string | **OPTIONAL** | [APR-MODEL-063] | The template revision answered. |
| `submissionUrls` | array of string or object | **OPTIONAL** | [APR-MODEL-064] | Ordered delivery choices, each `https` or `mailto` ([Submission targets](#submission)). |
| `regarding` | array of string | **OPTIONAL** | [APR-MODEL-065] | Digests of the records this form was completed with reference to ([Related records](#regarding)). |

`submissionUrls` is ordered by the author's preferred display order. The order
never permits an implementation to choose a target, or fall back to another,
without the user: submitting is an explicit user action.

A completed form that cannot name the form it completes is not traceable, which
is why a filled form carries `templateId`.

**Template identity.** {#template-identity}

A `templateId` **MUST** be a URI (RFC 3986). [APR-MODEL-036]

A `templateId` identifies a template; it is not a location. A tag URI
(RFC 4151), such as `tag:skpt.cl,2026:dog-license`, is unique by construction,
because it is minted from a domain or email address the author held on a date,
and it needs no server. Any URI whose uniqueness the author controls serves, an
email address as a `mailto` URI included.

A reader **MUST NOT** fetch a `templateId`. [APR-MODEL-082]

> Rationale: a filled form is consumed by the template it answers, so the
> identifier has to be unique across every author who will ever publish a
> form. Relying on a URI puts that uniqueness on a namespace someone already
> owns rather than on a registry. `file:///example.apr` is a valid URI and a
> poor identifier, and the format does not stop an author choosing badly; it
> only gives them a good form to choose.

**Language.** `language` is a BCP 47 language tag (RFC 5646) naming the language
of the author's human-facing text:
titles, descriptions, labels, help text, placeholders, and suggested values. It
lets a renderer present that text in its language, to a screen reader above
all. It never describes a response, and it applies to a template and a filled
form alike. A section or a prompt carries its own `language` where its text is
in another language.

A reader **MUST** take the language of a section or prompt that declares none
from the nearest enclosing section that declares one, and otherwise from
`metadata.language`. [APR-MODEL-083]

A writer filling a form **MUST NOT** add or change a `language` member.
[APR-MODEL-084]

A validator **MUST NOT** relax a check on human-facing text because of a
`language` member. [APR-MODEL-085]

> Rationale: a declared language is a claim by the author, so it cannot excuse
> the author's own text from a check. Mixing look-alike scripts is suspicious
> whatever the form says its language is. A filler's answers are in whatever
> language the filler typed; a workflow that needs to know which asks for it as
> a prompt.

**Example 5.2-1.** The same title, carrying a zero-width space, in a form that declares
a language and in one that does not. Both are valid and both warn: the `language`
member changes nothing about the check.

```apr-example
id: language-does-not-relax-the-check
rule: metadata
satisfies: APR-VAL-032
representation: jsonc
expect: valid
warns: FORBIDDEN_CODE_POINT
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "Inta\u200bke",
    "language": "ar"
  },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 5.2-2.** A form in English, with one section in French and one of its
prompts in German.

```apr-example
id: language-overrides
rule: metadata
satisfies: APR-MODEL-062, APR-MODEL-075, APR-MODEL-081
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Travel declaration", "language": "en" },
  "sections": [
    {
      "id": "declaration",
      "title": "Déclaration",
      "language": "fr",
      "prompts": [
        { "id": "name", "label": "Nom complet" },
        { "id": "origin", "label": "Herkunftsland", "language": "de" }
      ]
    }
  ]
}
```

**Example 5.2-3.** A title of spaces only.

```apr-example
id: title-blank
rule: metadata
violates: APR-MODEL-007
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "   " },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.2-4.** A filled form naming the template it answers.

```apr-example
id: filled-form-with-template-id
rule: metadata
satisfies: APR-MODEL-007, APR-MODEL-008, APR-MODEL-036
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "documentType": "filledForm",
  "metadata": { "title": "T", "templateId": "tag:example.com,2026:t" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.2-5.** A filled form without a `templateId`.

```apr-example
id: filled-form-without-template-id
rule: metadata
violates: APR-MODEL-008
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "documentType": "filledForm",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.2-6.** A `templateId` that is not a URI.

```apr-example
id: template-id-not-a-uri
rule: metadata
violates: APR-MODEL-036
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T", "templateId": "just-a-name" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Workflow state is not form data.** When a form was received, by whom, and what
happened to it next are facts the receiver tracks against the form. This
document defines no member for them.

A writer **MUST NOT** add a member to a form to record when it was received, by
whom, or what happened to it next. [APR-MODEL-037]

A workflow that needs to record them writes a form of its own and names the
form it was about ([Related records](#regarding)).

#### 5.2.1 Submission targets {#submission}

A `submissionUrls` entry names one of two transports, told apart by its scheme.
This document defines no other.

| Scheme | Meaning | Defined by |
| --- | --- | --- |
| `https` | A pre-signed object-store PUT target | HTTP (RFC 9110); the S3 pre-signed URL convention |
| `mailto` | An email address to attach the document to | RFC 6068 |

**`https`.** The entry names a pre-signed object-store target. Two kinds are
defined, told apart by the entry's `kind`: a `post`, which carries its policy
with it, and a `put`, which does not.

A `submissionUrls` entry **MUST** be a string or an object. [APR-MODEL-133]

A reader **MUST** read a string entry as an object whose `kind` is `put` and
whose `url` is that string. [APR-MODEL-134]

**Example 5.2.1-1.** Two entries in the shorthand string form, ordered by the author's
preference.

```apr-example
id: submission-string-entries
rule: submission
satisfies: APR-MODEL-133, APR-TEXT-007, APR-VAL-037
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "Well permit",
    "submissionUrls": [
      "https://uploads.example.gov/permits/abc?X-Amz-Signature=deadbeef",
      "mailto:permits@example.gov?subject=Well%20permit"
    ]
  },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 5.2.1-2.** An entry that is neither a string nor an object.

```apr-example
id: submission-entry-not-string-or-object
rule: submission
violates: APR-MODEL-133
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "Well permit",
    "submissionUrls": [ 42 ]
  },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

The string form is the shorthand in ordinary use, and it stays. An object entry
carries these members.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `kind` | string | **REQUIRED** | [APR-MODEL-128] | `put` or `post`. |
| `url` | string | **REQUIRED** | [APR-MODEL-129] | The target the request is sent to, used verbatim, query string included. |
| `fields` | object | **OPTIONAL** | [APR-MODEL-130] | The policy fields a `post` target requires, each sent verbatim. |
| `expires` | string | **OPTIONAL** | [APR-MODEL-131] | An RFC 3339 instant after which the grant is spent. |
| `refresh` | string | **OPTIONAL** | [APR-MODEL-132] | A URL that issues a replacement entry, described below. |

A `post` entry **MUST** carry `fields`. [APR-MODEL-135]

An implementation **MUST NOT** act on an entry whose `kind` it does not
recognise or does not implement. [APR-MODEL-139]

Each row below is a requirement on an implementation submitting to an `https`
entry, of either kind.

| Behaviour | Requirement | Rule |
| --- | --- | --- |
| Send the document as the body of one `PUT` to a `put` entry, with the `vnd.apr` media type of its representation as `Content-Type` ([Document type](#media-types)) | **MUST** | [APR-MODEL-033] |
| Send the document to a `post` entry as one `multipart/form-data` POST, every member of `fields` as a form field ahead of the document, the document last and carrying that same media type | **MUST** | [APR-MODEL-136] |
| Send credentials, cookies, or headers derived from the document | **MUST NOT** | [APR-MODEL-086] |
| Treat any status other than 2xx as failure | **MUST** | [APR-MODEL-087] |
| Follow a redirect | **MUST NOT** | [APR-MODEL-088] |
| Retry without a fresh user action | **MUST NOT** | [APR-MODEL-089] |

A writer **SHOULD** name the stored object by the form's semantic digest, as
`submissions/` followed by the digest ([Digests](#digests)). [APR-MODEL-138]

What a receiver holds after either request is the document as the implementation
wrote it, byte for byte: already a valid APR file, needing no processing. A
WebDAV collection (RFC 4918) is an ordinary `PUT` target.

**Which kind an author chooses.** A `post` entry is the one to reach for where
the filler is anonymous and untrusted, which is the ordinary case for a public
form. Its policy travels inside the credential, so the object store enforces a
size ceiling — `content-length-range`, which has no `PUT` equivalent — and an
overwrite rule before it accepts a byte. A `put` entry is simpler to issue and
every S3-compatible store accepts one, but it carries no such conditions: a
filler can send arbitrarily many gigabytes, and every safeguard against that is
left to whoever deploys it.

**Refreshing a spent grant.** A pre-signed grant expires. `refresh` names a URL that issues a replacement
entry, so that a form filled after its target went stale is still deliverable
without reissuing the form.

An implementation **MUST NOT** fetch `refresh` without an explicit user action.
[APR-MODEL-137]

What `refresh` returns is one submission entry, of the same shape, to be used in
place of the spent one. It is a live endpoint of the office that issued the
form, and it is not `templateId`: nothing here relaxes the rule that a reader
never fetches a `templateId` ([Metadata](#metadata)), and `refresh` never names
the document.

**`mailto`.** The entry is an RFC 6068 address, with any header fields it
carries, such as `subject`, passed through. A lone form is a one-record stream,
and the message leaves only on an explicit user action
([Security considerations](#security)).

An implementation submitting to a `mailto` entry **MUST** compose a message to
that address carrying the stream as a single attachment, and **MUST NOT** inline
it in the message body. [APR-MODEL-140]

An implementation submitting to a `mailto` entry **SHOULD** hand the
composition to the user's mail program. [APR-MODEL-034]

An implementation submitting to a `mailto` entry **MAY** send the message
itself instead. [APR-MODEL-090]

Each transport is claimed separately ([Declaring conformance](#declaring-conformance)).

An implementation **MUST NOT** act on an entry whose scheme it does not
recognise or does not implement. [APR-MODEL-035]

A validator **SHOULD** report an entry whose scheme this document does not
define as `SUBMISSION_URL_UNSUPPORTED` ([Warnings](#warnings)). [APR-MODEL-091]

An `http` entry is one such scheme.

> Rationale: the format defines *where* a completed form may go and borrows
> *how* from transports that already exist, rather than specifying one. A
> `mailto` address is the way an office without any server at all still
> receives forms. Redirects are refused for the same reason hidden characters
> are reported: following one delivers the form to a host the author never
> named. No authentication step is defined because a pre-signed grant *is* the
> authorisation, so the implementation never holds a credential.
>
> The object entry exists because a policy does not fit in a URL. A pre-signed
> POST puts the size ceiling and the overwrite rule inside the credential, where
> the store enforces them against an anonymous sender; with a bare pre-signed
> PUT those protections are a fronting service the deployer has to build, and a
> form nobody is authenticated to send is exactly the case that needs them. The
> string entry stays because it is what documents in the field carry and because
> some stores accept no POST at all.
>
> Naming the stored object by the form's own digest makes a resubmission of an
> unchanged form idempotent, gives a changed form a distinct name by itself, and
> lets a receiver check the name without trusting the sender to have chosen it
> honestly.

#### 5.2.2 Related records {#regarding}

`regarding` names the records this form was completed with reference to, each
by its semantic digest ([Digests](#digests)).

A step of a process is an ordinary form. The office publishes a template for
it — a receipt, a review, a routing decision — somebody fills that template in
while looking at what came before, and the form they produce names what they
looked at. Every record they looked at is left exactly as it was, so its digest
holds and the attestations over it stay `valid`.

`regarding` **MUST** be an array of distinct digest strings, each in the digest
form ([Digests](#digests)). [APR-MODEL-043]

Order is the author's preferred display order and means nothing else. An entry
can name a form or an attestation, so a form can pin not only what it was about
but the attestation state it was about.

**A reference asserts context and nothing else.** It records that whoever
completed this form had those records in front of them. It creates no revision,
no supersession, no chronology, no authority, and no trust relationship.

A reader **MUST NOT** present a reference as a revision, a supersession, a
chronology, an authority, or a trust relationship. [APR-MODEL-044]

Unsigned, a reference is a claim anyone could write. An attestation over the
form binds it, because a form digest covers `metadata`. Proving a history is
therefore what it always was — attestations, never position and never
assertion ([Changed forms](#changed-forms)).

A referenced digest matching no available record is `unresolved`
([Verification vocabulary](#verification)), and the document is valid.

A reader **MUST NOT** reject a document for an unresolved reference, report it
as damaged, or withhold its data. [APR-MODEL-045]

> References are acyclic by construction. A form's digest covers its own
> `regarding` list, so a record can only name one whose digest was already
> fixed. Nothing needs to forbid a cycle, because none can be built.

**Example 5.2.2-1.** A receipt naming the submission it was written against. The
referenced record does not accompany it, so the one reference is `unresolved`,
and the receipt is valid.

```apr-example
id: regarding-reference
rule: regarding
satisfies: APR-MODEL-043, APR-MODEL-045
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

**Example 5.2.2-2.** The same receipt in a stream with its subject. The
reference resolves, and the chain is legible to a reader.

```apr-example
id: regarding-chain
rule: regarding
satisfies: APR-MODEL-043
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","documentType":"filledForm","metadata":{"title":"Permit Application","templateId":"tag:example.com,2026:permit"},"sections":[{"id":"applicant","title":"Applicant","prompts":[{"id":"full_name","label":"Full name","response":"Ada Lovelace"}]}]}
---
{"aprVersion":"1.0-beta.6","documentType":"filledForm","metadata":{"title":"Intake Receipt","templateId":"tag:example.com,2026:intake-receipt","regarding":["sha256:b4363edd8ccc7f2e2acca6786a73a1f855fb8e9d8cad0245c16934107cfc4c28"]},"sections":[{"id":"intake","title":"Intake","prompts":[{"id":"received","label":"Date received","response":"2026-09-04"}]}]}
```

**Example 5.2.2-3.** A `regarding` entry that is not a digest.

```apr-example
id: regarding-not-a-digest
rule: regarding
violates: APR-MODEL-043
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T", "regarding": [ "sha256:NOTHEX" ] },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
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

Each row below is a requirement on a section.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `id` | non-blank string | **REQUIRED** | [APR-MODEL-066] | Unique document-wide among sections ([Prompt](#prompt-object)). |
| `title` | non-blank string | **REQUIRED** | [APR-MODEL-067] | The section's heading in the document outline. |
| `description` | string | **OPTIONAL** | [APR-MODEL-068] | |
| `sections` | array | **OPTIONAL** | [APR-MODEL-069] | Child sections, recursively. |
| `prompts` | array | **OPTIONAL** | [APR-MODEL-070] | |
| `kind` | string | **OPTIONAL** | [APR-MODEL-071] | `table` when this section's child sections are repeating instances ([Tables](#tables)). |
| `canAddRows` | boolean | **OPTIONAL** | [APR-MODEL-072] | Whether a filler can add or remove instances ([Rows and instances](#table-rows)). |
| `maxRows` | integer | **OPTIONAL** | [APR-MODEL-073] | Advisory cap on instance count. |
| `role` | string | **OPTIONAL** | [APR-MODEL-074] | [Roles](#roles) |
| `language` | string | **OPTIONAL** | [APR-MODEL-075] | A BCP 47 language tag, overriding the language the section inherits ([Metadata](#metadata)). |

A section's `maxRows` **MUST** be at least 1. [APR-MODEL-047]

A section **MUST** contain at least one prompt or at least one child section,
tables included. [APR-MODEL-009]

**Every section has a title.** The section tree is the document outline that a
screen-reader user navigates by. An untitled section is a hole in that outline,
so the format refuses to produce one.

**Example 5.3-1.** A section with no title.

```apr-example
id: section-without-title
rule: section-object
violates: APR-MODEL-067
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.3-2.** A section with no id.

```apr-example
id: section-without-id
rule: section-object
violates: APR-MODEL-066
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "title": "S", "prompts": [ { "id": "p", "label": "P" } ] } ]
}
```

**Example 5.3-3.** A table allowing at most one row.

```apr-example
id: table-max-rows-one
rule: section-object
satisfies: APR-MODEL-009, APR-MODEL-047
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "t",
      "title": "T",
      "kind": "table",
      "maxRows": 1,
      "sections": [ { "id": "r", "title": "R", "prompts": [ { "id": "r.a", "label": "A" } ] } ]
    }
  ]
}
```

**Example 5.3-4.** A section with neither prompts nor child sections.

```apr-example
id: section-empty
rule: section-object
violates: APR-MODEL-009
representation: jsonc
expect: reject
diagnostic: EMPTY_SECTION
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S" } ]
}
```

**Example 5.3-5.** A table allowing no rows.

```apr-example
id: table-max-rows-zero
rule: section-object
violates: APR-MODEL-047
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "t",
      "title": "T",
      "kind": "table",
      "maxRows": 0,
      "sections": [ { "id": "r", "title": "R", "prompts": [ { "id": "r.a", "label": "A" } ] } ]
    }
  ]
}
```

### 5.4 Prompt {#prompt-object}

Each row below is a requirement on a prompt.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `id` | non-blank string | **REQUIRED** | [APR-MODEL-076] | Unique document-wide among prompts. |
| `label` | non-blank string | **REQUIRED** | [APR-MODEL-077] | The accessible name. |
| `response` | string | **OPTIONAL** | [APR-MODEL-078] | Absent means empty ([Responses are strings](#responses)). |
| `hints` | object | **OPTIONAL** | [APR-MODEL-079] | [Hints](#hints-object). Advisory in full. |
| `role` | string | **OPTIONAL** | [APR-MODEL-080] | Overrides the containing section's role ([Roles](#roles)). |
| `language` | string | **OPTIONAL** | [APR-MODEL-081] | A BCP 47 language tag, overriding the language the prompt inherits ([Metadata](#metadata)). |

**Placeholder text never substitutes for a label.** A placeholder disappears
when the user types, is invisible to many assistive technologies, and leaves the
field permanently unnamed. A prompt with a placeholder and no label has no
`label`, and a validator reports it as `REQUIRED_FIELD`.

**Example 5.4-1.** A prompt with a placeholder and no label.

```apr-example
id: prompt-without-label
rule: prompt-object
violates: APR-MODEL-077
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S",
    "prompts": [ { "id": "p", "hints": { "placeholder": "Full name" } } ] } ]
}
```

**Example 5.4-2.** A prompt with no id.

```apr-example
id: prompt-without-id
rule: prompt-object
violates: APR-MODEL-076
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S", "prompts": [ { "label": "P" } ] } ]
}
```

Section ids and prompt ids occupy separate namespaces.

A section and a prompt **MAY** share an id. [APR-MODEL-092]

Within each namespace, an id **MUST** be unique across the whole document, not
merely among siblings. [APR-MODEL-010]

A filled form is consumed by field id, and a duplicate makes the data ambiguous.

A validator **MUST** compare ids by exact code-point equality, applying no
normalization, case folding, or trimming. [APR-MODEL-011]

A template **SHOULD** keep each id unchanged across its versions. [APR-MODEL-093]

A writer **MUST NOT** change an id when it reorders prompts. [APR-MODEL-094]

Reordering a form is a presentation change; changing an id silently breaks every
downstream consumer and every attestation covering it.

**Example 5.4-3.** Two prompt ids that are canonically equivalent under Unicode
normalization. Compared by code point they are distinct, so neither is a
duplicate. Each carries a character outside `[A-Za-z0-9_.-]`, which is a warning
([Authoring data](#authoring-strictness)).

```apr-example
id: id-code-point-equality
rule: prompt-object
satisfies: APR-MODEL-011
violates: APR-TEXT-010, APR-VAL-038
representation: jsonc
expect: valid
warns: ID_FORBIDDEN_CHARACTER
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Caf\u00e9" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "caf\u00e9", "label": "Composed" },
        { "id": "cafe\u0301", "label": "Decomposed" }
      ]
    }
  ]
}
```

**Example 5.4-4.** A section and a prompt sharing an id, and two prompt ids that
differ only by case. All three ids are distinct within their namespaces.

```apr-example
id: id-namespaces
rule: prompt-object
satisfies: APR-MODEL-055, APR-MODEL-066, APR-MODEL-067, APR-MODEL-076, APR-MODEL-077, APR-MODEL-092
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Contact" },
  "sections": [
    {
      "id": "name",
      "title": "Name",
      "prompts": [
        { "id": "name", "label": "Full name" },
        { "id": "Name", "label": "Name as printed on the card" }
      ]
    }
  ]
}
```

**Example 5.4-5.** Prompt ids unique across two sections.

```apr-example
id: prompt-ids-unique
rule: prompt-object
satisfies: APR-MODEL-010
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s1", "title": "S1", "prompts": [ { "id": "a", "label": "A" } ] },
    { "id": "s2", "title": "S2", "prompts": [ { "id": "b", "label": "B" } ] }
  ]
}
```

**Example 5.4-6.** Two prompts sharing an id.

```apr-example
id: prompt-ids-repeated
rule: prompt-object
violates: APR-MODEL-010
representation: jsonc
expect: reject
diagnostic: DUPLICATE_ID
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [ { "id": "p", "label": "A" }, { "id": "p", "label": "B" } ]
    }
  ]
}
```

#### 5.4.1 Generated ids {#generated-ids}

An author names things.

A writer **MUST** preserve every valid id it read. [APR-MODEL-040]

A writer **MUST NOT** invent or replace an id unless the caller explicitly asks
it to repair the document. [APR-MODEL-095]

By default, a blank id or a duplicate is the error [Errors](#structural-validation)
says it is. When asked to repair, a writer generates ids by **content**, so that
every implementation repairing the same document arrives at the same names:

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

A writer repairing a document **MUST** give a blank or whitespace-only id an id
generated by the steps above. [APR-MODEL-041]

A writer repairing a document **MUST** give both members sharing an id an id
generated by the steps above, since neither has a better claim to the name.
[APR-MODEL-096]

Generated ids are short, typeable, and carry no order: two adjacent prompts get
unrelated names, and nothing about a name says where it sits.

A writer **MUST NOT** generate an id that encodes position, such as `q1`, `q2`,
or `row_3`. [APR-MODEL-042]

A person reading such ids takes the sequence to mean something, and inserting
one row would appear to renumber the rest. A renamed id is a changed id: a
`fields` attestation naming the old id resolves to nothing afterwards, which is
the honest outcome, since the thing it named is no longer there under that name.

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

A table is a section carrying `kind: "table"`. Its child sections are its
instances and their prompts are its cells; a table adds no member a section
lacks.

A reader **MUST NOT** treat a section as a table unless it carries
`kind: "table"`, whatever its `maxRows`, `canAddRows`, or child sections. [APR-MODEL-038]

A reader **MUST** preserve a `maxRows` or `canAddRows` member on a section that is
not a table. [APR-MODEL-097]

> Rationale: a plain section may have child sections too, so inference would
> have to guess, and two readers guessing differently about the same document
> is the failure the format exists to prevent.

**Example 5.5-1.** A table with two instances.

```apr-example
id: table-with-two-instances
rule: tables
satisfies: APR-MODEL-014, APR-MODEL-046
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
        { "id": "item_1", "title": "Item 1",
          "prompts": [
            { "id": "item_1.description", "label": "Description", "response": "Train fare" },
            { "id": "item_1.amount", "label": "Amount", "response": "42.50",
              "hints": { "expectedDataType": "currency" } }
          ] },
        { "id": "item_2", "title": "Item 2",
          "prompts": [
            { "id": "item_2.description", "label": "Description", "response": "Hotel" },
            { "id": "item_2.amount", "label": "Amount", "response": "120.00",
              "hints": { "expectedDataType": "currency" } }
          ] }
      ]
    }
  ]
}
```

**Example 5.5-2.** Table members on a section that is not a table.

```apr-example
id: table-members-on-a-plain-section
rule: tables
satisfies: APR-MODEL-038, APR-MODEL-097
representation: jsonc
expect: valid
warns: TABLE_MEMBERS_ON_A_PLAIN_SECTION
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Plain section" },
  "sections": [
    { "id": "s", "title": "S", "maxRows": 5,
      "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

#### 5.5.1 What a table asserts {#table-assertion}

A table is a claim about structure, not appearance:

- its child sections are **instances**, not free-standing subsections;
- prompts at the **same position correspond** across instances, which is what
  makes "the Amount field" a thing that exists in every row;
- an instance's `title` **identifies** it; and
- a prompt's `label` **names the corresponding field** across every instance.

A table has no column definition. A column's header is the corresponding prompt's
`label`, and a column's type is that prompt's `expectedDataType`.

> Rationale: declaring columns separately would state twice what the prompts
> already state, and anything stated twice can disagree.

Correspondence is by position, never by id.

A writer **SHOULD** give a cell the id `{instanceId}.{columnId}`, which helps
addressing and database import. [APR-MODEL-048]

#### 5.5.2 A table licenses no layout {#table-no-layout}

A renderer **MAY** present a table as a grid, as stacked cards, as a flat
sequence of prompts, or as speech. [APR-MODEL-012]

Each of these is a conforming presentation, and none is a fallback.

> Rationale: this matters most where tables are hardest. A six-column grid is
> unusable on a phone and at 200% zoom, and many screen-reader users prefer the
> linear reading. Choosing the linear presentation is not a degraded rendering of
> a table — it is an equally valid reading of the same claim.

A writer **MUST NOT** add a member to a table that states width, alignment,
colour, or font. [APR-MODEL-013]

#### 5.5.3 Rows and instances {#table-rows}

A filler can add or remove the instances of a table whose `canAddRows` is `true`.

A reader **MUST** treat a table without `canAddRows` as fixed. [APR-MODEL-098]

> Rationale: the default is deliberately restrictive. A fixed table that silently
> gained a row is a worse failure than a line-item table needing one explicit
> property.

Whether instances can be added is independent of whether they hold values: a
filled table can still accept new instances, and a fixed table can be entirely
blank.

A section carrying `kind: "table"` **MUST** carry at least one child section. [APR-MODEL-046]

Prompts alone satisfy [Section](#section-object) but not this rule.

> Rationale: a table's cells live in its instances, so a table without one has
> nowhere to put them, and the instance carries the table's field names. A form
> offering to add the first row is already presenting a row; how that row is
> shown is a display decision.

`maxRows` is an advisory cap on the number of instances.

A validator **MUST NOT** reject a table that carries more instances than its
`maxRows`. [APR-MODEL-099]

**Example 5.5.3-1.** A table without instances.

```apr-example
id: table-without-instances
rule: table-rows
violates: APR-MODEL-046
representation: jsonc
expect: reject
diagnostic: EMPTY_TABLE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Empty table" },
  "sections": [
    { "id": "t", "title": "T", "kind": "table",
      "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 5.5.3-2.** A table over its `maxRows`.

```apr-example
id: table-over-capacity
rule: table-rows
satisfies: APR-MODEL-099
representation: jsonc
expect: valid
warns: TABLE_OVER_CAPACITY
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Over capacity" },
  "sections": [
    { "id": "t", "title": "T", "kind": "table", "maxRows": 1,
      "sections": [
        { "id": "r1", "title": "Row 1", "prompts": [ { "id": "r1.x", "label": "X" } ] },
        { "id": "r2", "title": "Row 2", "prompts": [ { "id": "r2.x", "label": "X" } ] }
      ] }
  ]
}
```

#### 5.5.4 Ragged tables {#table-ragged}

The instances of a table **SHOULD** carry the same number of prompts and the same
label at each position. [APR-MODEL-014]

A validator **MUST NOT** reject a table whose instances disagree. [APR-MODEL-100]

A renderer **MUST** present every prompt an instance carries, whether or not the
other instances carry one at that position. [APR-MODEL-101]

> Rationale: refusing to open the document would discard whatever a filler had
> already written, which [Any string is a valid response](#any-string) exists to
> prevent.

**Example 5.5.4-1.** A ragged table.

```apr-example
id: ragged-table
rule: table-ragged
satisfies: APR-MODEL-100
violates: APR-MODEL-014
representation: jsonc
expect: valid
warns: TABLE_RAGGED, TABLE_LABEL_MISMATCH
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Ragged" },
  "sections": [
    { "id": "t", "title": "T", "kind": "table",
      "sections": [
        { "id": "r1", "title": "Row 1",
          "prompts": [ { "id": "r1.x", "label": "X" }, { "id": "r1.y", "label": "Y" } ] },
        { "id": "r2", "title": "Row 2",
          "prompts": [ { "id": "r2.x", "label": "X" } ] }
      ] }
  ]
}
```

### 5.6 Nesting depth {#nesting}

Sections nest recursively.

An implementation **MUST** support at least **16 levels** of section nesting. [APR-MODEL-015]

Beyond 16 levels, whether a form can be read depends on the implementation.

> Rationale: unbounded depth is not implementable. Every real parser has a depth
> limit, and a format that promises infinity promises a stack overflow.

A writer **SHOULD NOT** nest sections more than five levels deep. [APR-MODEL-017]

Deeper forms are difficult to navigate with any input method.

**Example 5.6-1.** Six levels of nesting. The advice above is advice, so a deeper
document is still valid and a reader opens it.

```apr-example
id: nesting-six-levels
rule: nesting
satisfies: APR-MODEL-017
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s1", "title": "S1", "sections": [
      { "id": "s2", "title": "S2", "sections": [
        { "id": "s3", "title": "S3", "sections": [
          { "id": "s4", "title": "S4", "sections": [
            { "id": "s5", "title": "S5", "sections": [
              { "id": "s6", "title": "S6", "prompts": [ { "id": "p", "label": "P" } ] }
            ] }
          ] }
        ] }
      ] }
    ] }
  ]
}
```

### 5.7 Hints {#hints-object}

A prompt's `hints` object holds advisory guidance
([Hints never enforce](#hints-advisory)). Each row below is a member of it, and
its Requirement column says whether the member is present.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `placeholder` | string | **OPTIONAL** | [APR-MODEL-102] | Text shown in an empty control. Never a substitute for `label`. |
| `expectedDataType` | string | **OPTIONAL** | [APR-MODEL-103] | Suggested input affordance, from the registry below. |
| `suggestedValues` | array of string | **OPTIONAL** | [APR-MODEL-104] | Offered as options. A response outside the list is still valid. |
| `helpText` | string | **OPTIONAL** | [APR-MODEL-105] | Explanatory text for the prompt. |
| `validationPattern` | string | **OPTIONAL** | [APR-MODEL-106] | Advisory regular expression. |
| `min` | number or string | **OPTIONAL** | [APR-MODEL-107] | Suggested lower bound for an ordered field. A number on `number`, `currency`, and `range`; a canonical-form string on `date`, `time`, and `datetime`. |
| `max` | number or string | **OPTIONAL** | [APR-MODEL-108] | Suggested upper bound for an ordered field. Typed as `min`. |
| `step` | number | **OPTIONAL** | [APR-MODEL-109] | Suggested increment for an ordered field. Applies to `number`, `currency`, and `range`. |
| `exprHidden` | string | **OPTIONAL** | [APR-MODEL-110] | CEL. Truthy hides this prompt ([Expressions](#expressions)). |
| `exprValue` | string | **OPTIONAL** | [APR-MODEL-111] | CEL. Computed value. |
| `exprExpected` | string | **OPTIONAL** | [APR-MODEL-112] | CEL. Truthy marks the prompt as expected. |
| `exprValidation` | string | **OPTIONAL** | [APR-MODEL-113] | CEL. Returns a message; empty means valid. |
| `exprReadOnly` | string | **OPTIONAL** | [APR-MODEL-114] | CEL. Truthy makes this prompt read-only in a renderer. |

`expectedDataType` registry: `text`, `multiline`, `email`, `phone`, `url`,
`date`, `time`, `datetime`, `number`, `currency`, `boolean`, `select`,
`multichoice`, `password`, `range`, `color`.

> Rationale: the registry has no `signature` type and no `file` type. A
> signature is not a response: evidence that a person stood behind a form is an
> attestation record travelling beside it in the stream
> ([Attestations](#attestations)), never a drawn image or a typed name in a field.
> An attachment is not a response either: a form carries what a person typed, and
> the format defines no representation for bytes that were not typed.

> Rationale: country-specific field types are absent. A postcode, a national
> identity number, or a tax reference is `text` with a `validationPattern`: baking
> one country's formats into the vocabulary would oblige every reader everywhere to
> carry them.

The registry is open.

A validator **MUST NOT** reject a form because its `expectedDataType` is not in
the registry. [APR-MODEL-018]

A renderer **MUST** present a prompt whose `expectedDataType` it does not
recognise as a `text` prompt. [APR-MODEL-115]

> Rationale: an open registry can grow without breaking every existing reader.

**Example 5.7-1.** An `expectedDataType` outside the registry.

```apr-example
id: unregistered-data-type-degrades
rule: hints-object
satisfies: APR-MODEL-018
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

A hint a reader cannot use is not an error.

A reader **MUST** preserve a hint that is unrecognised, unsupported, or
malformed. [APR-MODEL-039]

A validator **MUST NOT** reject a form because a hint is unusable, such as a
`validationPattern` that is not a regular expression, a bound of the wrong type,
or an expression that does not compile. [APR-MODEL-116]

A renderer **MUST** apply a prompt's hints in the order the table above lists
them, and where two conflict, apply the earlier and skip the later. [APR-MODEL-117]

**Example 5.7-2.** A `validationPattern` that is not a regular expression.

```apr-example
id: unusable-hint
rule: hints-object
satisfies: APR-MODEL-039, APR-MODEL-116
representation: jsonc
expect: valid
warns: HINT_UNUSABLE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Unusable hint" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "hints": { "validationPattern": "(" } } ] }
  ]
}
```

The registry above is the normative registry. `schemas/apr-types-1.0.json`
publishes it in machine-readable form, together with each type's canonical write
form, accepted read forms, expression type, and meaningful hints. That file is a
**derived projection** of this section: where the two disagree, this section
governs and the file is a defect.

> Rationale: the same facts stated in prose, in a schema, and in code will
> eventually disagree, which is the failure [Tables](#table-assertion) removes by
> refusing to declare a column twice. Naming one source and deriving the rest is
> the same move applied to the type vocabulary.

On a `boolean`, `suggestedValues` names the two options, so a renderer can label
them as the author intended without changing the type.

**Bounds are an offer, not a limit.** `min`, `max`, and `step` describe the range
a widget offers: the ends of a slider, the increment of a spinner. They apply
only to ordered types. On `number`, `currency`, and `range` they are JSON
numbers. On `date`, `time`, and `datetime`, `min` and `max` are the earliest and
latest suggested values, written as strings in that type's canonical form
([Value types](#json-subset)), and `step` does not apply.

A response outside the bounds is still valid
([Hints never enforce](#hints-advisory)): a slider that stops at 100 does not make
`120` a wrong answer.

#### 5.7.1 Types are affordances, not validators {#data-types}

`expectedDataType` tells a renderer which input affordance to offer and tells the
person filling the form what the author expected. It does nothing else.

Every response below is valid for its prompt ([Any string](#any-string)):

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

A reader **MUST** compare member names case-sensitively, so a member whose name
matches a defined member only when case is ignored is an unknown member. [APR-MODEL-118]

The defined member it resembles takes its default.

**Example 5.8-1.** A section whose `id` is spelled `ID`.

```apr-example
id: member-name-case
rule: extensions
violates: APR-MODEL-066
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Wrong case" },
  "sections": [
    { "ID": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Unprefixed names belong to the specification.** Every member this document
defines is unprefixed, and so is every member a later version of APR adds.

A writer **MUST NOT** add a member whose name carries no prefix. [APR-MODEL-031]

**An extension member is named by its owner.** An extension member name
**MUST** begin with a reverse-DNS prefix its writer owns, followed by a dot:
`com.example.priority`, `gov.ct.dmv.routing`. [APR-MODEL-029]

A reader identifies an extension member by the dot in its name.

**An extension member is the author's own data.** It is written by whoever wrote
the form, travels inside it, and is covered by its digest ([Digests](#digests)).
Data *about* a form written by somebody else — a receipt, a review, a routing
decision — is not an extension member: it is a new form naming what it was about
([Related records](#regarding)).

> Rationale: two producers choosing the same member name produce documents that
> round-trip correctly and mean different things, and a format that cannot add
> a member without colliding with somebody's private one cannot grow. Reserving
> the unprefixed space and requiring an owned prefix for everything else closes
> both problems without a registry, which is what OpenAPI's `x-` and reverse-DNS
> naming in Java and Apple platforms do. A domain is the one namespace every
> producer already owns, so no one has to run anything.

**Example 5.8-2.** Extension members with a prefix.

```apr-example
id: extension-member-prefixed
rule: extensions
satisfies: APR-MODEL-020, APR-MODEL-029, APR-MODEL-031
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

**Example 5.8-3.** An unknown member with no prefix.

```apr-example
id: unprefixed-member
rule: extensions
violates: APR-MODEL-029, APR-MODEL-031
representation: jsonc
expect: valid
warns: UNPREFIXED_MEMBER
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Unprefixed", "routing": "desk-4" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

### 5.9 Canonical value forms {#canonical-values}

Any string remains a valid response ([Any string](#any-string)). The forms below
apply only where an implementation chooses the value itself, as a date picker, a
checkbox, or a multi-select list does.

> Rationale: without them, the same template filled in two implementations yields
> two different datasets, and "database-ready" stops being true.

A writer choosing a response **SHOULD** write it in the canonical write form the
table below gives for the prompt's `expectedDataType`. [APR-MODEL-119]

**Example 5.9-1.** Three responses a writer chose, each in the canonical write form its
`expectedDataType` names.

```apr-example
id: canonical-write-forms
rule: canonical-values
satisfies: APR-MODEL-119
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Permit" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "issued", "label": "Issued",
          "hints": { "expectedDataType": "date" }, "response": "2026-01-15" },
        { "id": "agreed", "label": "Agreed",
          "hints": { "expectedDataType": "boolean" }, "response": "true" },
        { "id": "fee", "label": "Fee",
          "hints": { "expectedDataType": "currency" }, "response": "1250.00" }
      ]
    }
  ]
}
```

A reader **MUST** read every form the table lists, canonical or accepted on read,
as the value it spells. [APR-MODEL-023]

Neither rule makes a response invalid.

| Hint | Canonical write form | Also accepted on read |
| --- | --- | --- |
| `date` | `YYYY-MM-DD` (RFC 3339 full-date) | anything |
| `time` | `HH:MM` or `HH:MM:SS`, 24-hour | anything |
| `datetime` | RFC 3339 | anything |
| `boolean` | `true` / `false` | `yes`, `y`, `1`, `on`, `x`, `checked` / `no`, `n`, `0`, `off`, `unchecked`, case-insensitive |
| `number`, `currency` | digits with `.` as decimal separator, no grouping | anything, including symbols and words |
| `multichoice` | selections separated by U+000A, one per line | a single line separated by comma and space |
| `select` | exactly one value, verbatim from `suggestedValues` | anything |

A reader **MUST** read an empty response to a prompt of any type above as no
selection. [APR-MODEL-120]

**Example 5.9-2.** A boolean answered `yes` and a multichoice answered as one
comma-separated line. Both are forms the table accepts on read, so both are read
as the value they spell.

```apr-example
id: canonical-accepted-on-read
rule: canonical-values
satisfies: APR-MODEL-023
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Intake" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "consent", "label": "Consent given",
          "hints": { "expectedDataType": "boolean" }, "response": "yes" },
        { "id": "days", "label": "Days available",
          "hints": { "expectedDataType": "multichoice",
                     "suggestedValues": [ "Monday", "Tuesday" ] },
          "response": "Monday, Tuesday" }
      ]
    }
  ]
}
```

**Example 5.9-3.** An empty response to a `select` prompt. It is no selection, not an
invalid one.

```apr-example
id: canonical-empty-is-no-selection
rule: canonical-values
satisfies: APR-MODEL-120
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Intake" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "ward", "label": "Ward",
          "hints": { "expectedDataType": "select",
                     "suggestedValues": [ "North", "South" ] },
          "response": "" }
      ]
    }
  ]
}
```

> Rationale: the canonical boolean is `true`/`false`, not `yes`/`no`, because
> `yes` is English. A format that renders to voice, to other languages, and into
> database columns cannot make its canonical boolean depend on one language.

> Rationale: `multichoice` separates selections by newline, not comma, because a
> suggested value can itself contain a comma — `Bloomfield, CT` is an ordinary
> option in a municipal form — and comma separation silently turns one selection
> into two. A newline cannot appear inside a single-line option, so the encoding
> is lossless.

### 5.10 Roles — who each part is for {#roles}

Most real forms are filled by more than one person. A patient completes an
intake, a nurse records observations, the office stamps a reference. With nowhere
to say so, all three arrive as one undifferentiated list and the patient is left
guessing which questions are theirs.

`role` on a section or a prompt names who is meant to fill it in.

A reader **MUST** take a prompt's role from the prompt where it carries one, and
otherwise from the section containing it. [APR-MODEL-121]

This lets a single field be handed back to the patient without splitting the
section in two.

A reader **MUST** present a field whose role it does not recognise as it presents
any other field, without an error. [APR-MODEL-025]

**Example 5.10-1.** A prompt whose role no entry declares and which this document never
defines. The field is an ordinary field.

```apr-example
id: unrecognised-role-is-ordinary
rule: roles
satisfies: APR-MODEL-025
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Intake" },
  "roles": [ { "id": "patient" } ],
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "role": "notary" } ] }
  ]
}
```

A form declares roles in its `roles` array ([Document](#root-object)). Each entry
is an object with these members:

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `id` | string | **REQUIRED** | [APR-MODEL-026] | The identifier a `role` member names. |
| `name` | string | **OPTIONAL** | [APR-MODEL-123] | The name shown to a person. |
| `description` | string | **OPTIONAL** | [APR-MODEL-124] | Who this role is, where the name alone is not obvious. |

**Example 5.10-2.** Declared roles, and a prompt handed back to the patient.

```apr-example
id: declared-roles
rule: roles
satisfies: APR-MODEL-026
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Intake" },
  "roles": [
    { "id": "patient", "name": "Patient", "description": "The person receiving care" },
    { "id": "nurse", "name": "Nurse", "description": "Clinical staff recording observations" },
    { "id": "office", "name": "Office use" }
  ],
  "sections": [
    { "id": "observations", "title": "Observations", "role": "nurse",
      "prompts": [
        { "id": "pulse", "label": "Pulse" },
        { "id": "pain", "label": "Pain today, 0 to 10", "role": "patient" }
      ] }
  ]
}
```

**Example 5.10-3.** A role entry without an `id`.

```apr-example
id: role-without-id
rule: roles
violates: APR-MODEL-026
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Intake" },
  "roles": [ { "name": "Patient" } ],
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

A reader **MUST** show a role's `id` where the role has no `name`. [APR-MODEL-050]

A reader **MUST** show the identifier of a role no entry declares, without an
error. [APR-MODEL-122]

A validator **MUST NOT** reject a form whose `role` names a role that `roles` does
not declare. [APR-MODEL-051]

**Example 5.10-4.** A role no entry declares.

```apr-example
id: undeclared-role
rule: roles
satisfies: APR-MODEL-051
representation: jsonc
expect: valid
warns: UNDECLARED_ROLE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Intake" },
  "roles": [ { "id": "patient" } ],
  "sections": [
    { "id": "s", "title": "S", "role": "nurse",
      "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**A role says who a field is for, never who can type into it.**

A reader **MUST NOT** refuse input to a field because of its role. [APR-MODEL-027]

> Rationale: the format has no identity at fill time. Nothing in a document knows
> who is at the keyboard.

Where a form declares roles, a reader **SHOULD** let the person say which role
they are filling. [APR-MODEL-028]

A reader **SHOULD** mark which fields belong to the person's role, leaving the
fields of other roles visible and editable. [APR-MODEL-125]

A reader **SHOULD** make a field's role available to assistive technology. [APR-MODEL-126]

> Rationale: a visual treatment alone communicates nothing to a screen reader.

> Rationale: accountability comes from attestations, not from the widget. A
> greyed-out box is evidence of nothing: whoever holds the document can edit it
> directly. A fields-scoped attestation over those prompts, made with the nurse's
> certificate, is evidence the nurse filled them. Roles describe intent;
> attestations establish fact, and an implementation that treats a role as a
> security control has misread this section.

---

## 6. Document type and file extensions {#media-types}

`documentType` is **authoritative**.

A reader **MUST** determine whether a document is a template or a filled form
from its `documentType` member alone. [APR-SEC-005]

| Extension | Meaning | Status |
| --- | --- | --- |
| `.aprt` | Template | Convention |
| `.aprf` | Filled form | Convention |
| `.apr` | Either | Convention |
| `.apr.jsonc` | An APR-JSONC document or stream | Convention |
| `.apr.yaml` | An APR-YAML document or stream | Convention |

A filename extension is a **desktop affordance**: it drives icons, file
associations, and save dialogs. It is not part of the data model.

> Rationale: an extension cannot decide what a document is anywhere a filename
> does not exist: an HTTP request body, a database column, a clipboard paste, a
> mobile share intent, a `postMessage` between frames, a byte array in an
> enterprise queue. A browser-based reader and a desktop reader would reach
> *different conclusions about identical bytes*, which is the interoperability
> failure the format exists to prevent. A document means the same thing
> everywhere, including where it has no name.

A writer **SHOULD** give a file the extension that matches its `documentType`. [APR-SEC-006]

A reader **SHOULD** warn when a file's extension and its `documentType` disagree,
rather than silently honouring either. [APR-SEC-015]

Representation is determined by content, not by name.

A reader **MUST NOT** reject a document because its extension disagrees with its
content. [APR-SEC-007]

A template becomes a filled form when its `documentType` is set to `filledForm`
and its `templateId` is recorded.

A host **SHOULD** ask for a new filename when it turns a template into a filled
form, so the blank template is not overwritten. [APR-SEC-008]

**Media types.** Where a document travels with a media type, the type names the
representation, and the `documentType` member remains authoritative for what the
document is.

| Content | Media type |
| --- | --- |
| An APR-JSONC document | `application/vnd.apr+json` |
| An APR-YAML document or document stream | `application/vnd.apr+yaml` |
| An APR-JSONC record stream | `application/vnd.apr+json-seq` |

A writer that labels APR content **MUST** use the type above for its
representation. [APR-SEC-012]

A reader **MUST NOT** select APR behaviour from the generic `application/json`,
`application/yaml`, or `application/json-seq` types. [APR-SEC-013]

A reader **MUST NOT** reject a document because its media type and its content
disagree. [APR-SEC-014]

The content decides, as it does for a filename extension.

> Rationale: a writer labelling APR-YAML as `application/json` mislabels a
> document; a reader inferring APR from `application/json` treats every JSON file
> it meets as a form; a reader refusing a mismatch loses a readable document to a
> header. Only the last loses data, which is why the content deciding is a rule
> of its own.

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
if it has no errors.

A validator **MUST NOT** report a document as invalid for any reason the
[Errors](#structural-validation) table does not list. [APR-VAL-007]

**Example 7-1.** A document carrying an unknown member, a response that matches
neither its `expectedDataType` nor its `validationPattern`, and a value outside
`suggestedValues`. None of that is a condition the errors table lists, so the
document is valid.

```apr-example
id: no-unlisted-reason-to-refuse
rule: validation
satisfies: APR-VAL-007
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "com.example.note": "not a defined member",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "p",
          "label": "P",
          "hints": {
            "expectedDataType": "number",
            "validationPattern": "^[0-9]+$",
            "suggestedValues": [ "1", "2" ]
          },
          "response": "not a number at all"
        }
      ]
    }
  ]
}
```

### 7.1 Errors — structure only {#structural-validation}

Each row below is a requirement on a validator: when its condition holds, the
validator reports an error under the row's code.

| Code | Condition | Requirement | Rule |
| --- | --- | --- | --- |
| `NULL_DOCUMENT` | No document. | **MUST** | [APR-VAL-011] |
| `REQUIRED_FIELD` | `aprVersion`, `metadata.title`, section `id` or `title`, prompt `id` or `label` absent or blank; `metadata` or `sections` absent; `sections` empty; `templateId` absent on a filled form; a role entry without `id`; a member the attestation record table requires, absent. | **MUST** | [APR-VAL-012] |
| `UNSUPPORTED_VERSION` | `aprVersion` is stated and is neither `1.0-beta.7` nor `1.0-beta.6` ([Version compatibility](#version-compatibility)). | **MUST** | [APR-VAL-013] |
| `DUPLICATE_ID` | A section or prompt id repeats within its namespace. | **MUST** | [APR-VAL-014] |
| `EMPTY_SECTION` | A section has no prompts and no child sections. | **MUST** | [APR-VAL-015] |
| `EMPTY_TABLE` | A `kind: "table"` section has no child sections, so it has no instances ([Rows and instances](#table-rows)). | **MUST** | [APR-VAL-016] |
| `WRONG_TYPE` | A structural member is not the JSON type its member table declares ([Value types](#json-subset)); an attestation member outside what its row allows. | **MUST** | [APR-VAL-017] |

Neither what a response says ([Semantic validation](#semantic-validation))
nor the state of an attestation ([Attestations never gate the data](#never-gate))
is ever a reason for an error.

**Example 7.1-1.** No document.

```apr-example
id: no-document
rule: structural-validation
violates: APR-VAL-011
representation: yaml
expect: reject
diagnostic: NULL_DOCUMENT
---
# a comment, and no document
```

**Example 7.1-2.** A prompt id used twice.

```apr-example
id: duplicate-prompt-id
rule: structural-validation
violates: APR-VAL-014
representation: jsonc
expect: reject
diagnostic: DUPLICATE_ID
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "A" }, { "id": "p", "label": "B" } ] }
  ]
}
```

**Example 7.1-3.** A section with no prompts and no child sections.

```apr-example
id: empty-section
rule: structural-validation
violates: APR-VAL-015
representation: jsonc
expect: reject
diagnostic: EMPTY_SECTION
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [ { "id": "s", "title": "S" } ]
}
```

### 7.2 Warnings — advisory only {#warnings}

Each row below is a requirement on a validator that reports its condition: it
reports a warning under the row's code.

| Code | Condition | Requirement | Rule |
| --- | --- | --- | --- |
| `RESPONSE_CONTRADICTS_TYPE` | A response contradicts `expectedDataType`. | **MUST** | [APR-VAL-018] |
| `RESPONSE_PATTERN_MISMATCH` | A response does not match `validationPattern`. | **MUST** | [APR-VAL-019] |
| `RESPONSE_OUTSIDE_BOUNDS` | A response falls outside the bounds family ([Hints](#hints-object)). | **MUST** | [APR-VAL-020] |
| `RESPONSE_OUTSIDE_SUGGESTED_VALUES` | A response is not one of `suggestedValues`. | **MUST** | [APR-VAL-021] |
| `HINT_UNUSABLE` | A hint cannot be applied at all — a `validationPattern` that is not a valid regular expression, a bound that will not parse. | **MUST** | [APR-VAL-022] |
| `UNREGISTERED_DATA_TYPE` | `expectedDataType` names a type the registry does not carry ([Types are affordances](#data-types)). | **MUST** | [APR-VAL-023] |
| `UNDECLARED_ROLE` | A `role` names a role `roles` does not declare ([Roles](#roles)). | **MUST** | [APR-VAL-024] |
| `TABLE_RAGGED` | Instances of a table do not carry the same number of prompts ([Ragged tables](#table-ragged)). | **MUST** | [APR-VAL-025] |
| `TABLE_LABEL_MISMATCH` | A cell's label differs across instances of the same column. | **MUST** | [APR-VAL-026] |
| `TABLE_OVER_CAPACITY` | A table carries more instances than `maxRows`. | **MUST** | [APR-VAL-027] |
| `TABLE_MEMBERS_ON_A_PLAIN_SECTION` | `maxRows` or `canAddRows` on a section that is not a table ([Tables](#tables)). | **MUST** | [APR-VAL-028] |
| `UNPREFIXED_MEMBER` | An unrecognised member with no reverse-DNS prefix ([Unknown members](#extensions)). | **MUST** | [APR-VAL-029] |
| `SUBMISSION_URL_UNSUPPORTED` | A submission entry of a scheme this document does not define ([Submission targets](#submission)). | **MUST** | [APR-VAL-030] |
| `NON_NFC_TEXT` | Human-facing text is not in Normalization Form C ([Human-facing text](#human-text)). | **MUST** | [APR-VAL-031] |
| `FORBIDDEN_CODE_POINT` | Human-facing text carries a code point the floor excludes ([Human-facing text](#human-text)). | **MUST** | [APR-VAL-032] |
| `CONFUSABLE_SCRIPT_MIX` | One member of human-facing text mixes letters of two or more of the Latin, Cyrillic and Greek scripts ([Human-facing text](#human-text)). | **MUST** | [APR-VAL-035] |
| `RESPONSE_FORBIDDEN_CODE_POINT` | A response carries a code point the floor excludes, other than a carriage return ([Human-facing text](#human-text)). | **MUST** | [APR-VAL-036] |
| `SUBMISSION_URL_FORBIDDEN_CODE_POINT` | A `submissionUrls` entry carries a code point the floor excludes ([Human-facing text](#human-text)). | **SHOULD** | [APR-VAL-037] |
| `ID_FORBIDDEN_CHARACTER` | An id carries a character outside `[A-Za-z0-9_.-]` ([Ids](#prompt-object)). | **SHOULD** | [APR-VAL-038] |

Warnings are how an implementation tells a person "this might not be what you
meant" without ever telling them "you are not allowed to write this."

An implementation **MAY** report a warning for a condition the table does not
name, such as a blank response a workflow treats as required. [APR-VAL-034]

An implementation **MUST NOT** report a warning as the document being
invalid. [APR-VAL-002]

> Rationale: the table fixes spellings, not obligations. Whether to report a
> condition stays the implementation's choice, and this list is not exhaustive
> the way the error list is. But a warning exists to be understood by whoever
> reads it next, and two implementations reporting the same condition under two
> names have produced advice that only travels as prose. `NON_NFC_TEXT` and
> `FORBIDDEN_CODE_POINT` are the exception to the choice, not to the spelling:
> the human-facing text floor requires a validator to report those at authoring time.

A renderer **MUST NOT** let a warning prevent saving. [APR-VAL-006]

A renderer **MUST NOT** let a warning prevent entering text. [APR-VAL-033]

**Example 7.2-1.** An `expectedDataType` outside the registry.

```apr-example
id: unregistered-data-type-warns
rule: warnings
satisfies: APR-VAL-023
representation: jsonc
expect: valid
warns: UNREGISTERED_DATA_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "hints": { "expectedDataType": "holographic-signature" } } ] }
  ]
}
```

**Example 7.2-2.** A submission entry of an undefined scheme.

```apr-example
id: submission-scheme-unsupported
rule: warnings
satisfies: APR-VAL-030
representation: jsonc
expect: valid
warns: SUBMISSION_URL_UNSUPPORTED
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T", "submissionUrls": [ "ftp://example.com/drop" ] },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 7.2-3.** A title that is not in Normalization Form C.

```apr-example
id: title-not-nfc
rule: warnings
satisfies: APR-VAL-031, APR-TEXT-014
representation: jsonc
expect: valid
warns: NON_NFC_TEXT
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Cafe\u0301 permit" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 7.2-4.** A title carrying a zero-width space.

```apr-example
id: title-forbidden-code-point
rule: warnings
satisfies: APR-VAL-032, APR-TEXT-014
representation: jsonc
expect: valid
warns: FORBIDDEN_CODE_POINT
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Permit\u200bApplication" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

### 7.3 Parse errors are not validation errors {#parse-errors}

Malformed input is a **parse failure**.

A reader **MUST NOT** validate input it could not parse. [APR-VAL-003]

A reader **MUST** read a document that parses, even when that document fails
validation. [APR-VAL-004]

> Rationale: keeping the two stages distinct is what lets a reader load a flawed
> document and show what is wrong with it, rather than refusing to open it.

A reader that reports a parse failure **MUST** report it under a parse-stage
code: the code this document names for that condition where it names one —
`DUPLICATE_MEMBER`, the `YAML_*` refusals, `APR_STREAM_MIXED_REPRESENTATIONS` —
and `PARSE_ERROR` where it does not. [APR-VAL-010]

No parse-stage code is an entry in the error table above, and none disturbs its
exhaustiveness: a document that will not parse was never validated, so no
validation error can describe it.

### 7.4 Semantic validation is never required {#semantic-validation}

A validator **MUST NOT** reject a document because of what a response means. [APR-VAL-005]

Each of the following is a valid document: a response that does not match its
`expectedDataType`; one that does not match `validationPattern`; an empty
response, including on a prompt marked expected; one outside `suggestedValues`,
`min`, or `max`; and one a reader considers factually wrong.

The format validates that a response is a well-formed string. It never validates
what that string says.

**Example 7.4-1.** Words where a number was expected.

```apr-example
id: response-contradicts-type
rule: semantic-validation
satisfies: APR-VAL-005, APR-VAL-018
representation: jsonc
expect: valid
warns: RESPONSE_CONTRADICTS_TYPE
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "response": "about twelve",
                     "hints": { "expectedDataType": "number" } } ] }
  ]
}
```

**Example 7.4-2.** A number above its `max`.

```apr-example
id: response-outside-bounds
rule: semantic-validation
satisfies: APR-VAL-005, APR-VAL-020
representation: jsonc
expect: valid
warns: RESPONSE_OUTSIDE_BOUNDS
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "response": "12",
                     "hints": { "expectedDataType": "number", "max": 10 } } ] }
  ]
}
```

**Example 7.4-3.** A selection outside `suggestedValues`.

```apr-example
id: response-outside-suggested-values
rule: semantic-validation
satisfies: APR-VAL-005, APR-VAL-021
representation: jsonc
expect: valid
warns: RESPONSE_OUTSIDE_SUGGESTED_VALUES
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    { "id": "s", "title": "S",
      "prompts": [ { "id": "p", "label": "P", "response": "blue",
                     "hints": { "expectedDataType": "select", "suggestedValues": [ "red", "green" ] } } ] }
  ]
}
```

---

## 8. Text handling {#text-handling}

### 8.1 Responses are evidence {#text-responses}

An implementation **MUST NOT** normalize, strip, or otherwise rewrite a response
when it reads or writes one. [APR-TEXT-001]

Escaping and visibly marking deceptive text are rendering responsibilities, not
licences to alter stored data.

### 8.2 Authoring data and filled data differ {#authoring-vs-filled}

The two halves of an APR document come from two different people under two
different conditions, and this document governs them differently.

| | **Authoring data** | **Filled data** |
| --- | --- | --- |
| Written by | the form author | the person filling the form |
| Members | `metadata`, section `id`, `title`, `description`, prompt `id`, `label`, all of `hints` | `prompt.response` |
| Conditions | deliberate, repeatable, reviewable before publication | once, under time pressure, often on someone else's behalf |
| Consumed by | machines and every future reader | the receiving workflow |
| Policy | **Strict rules are appropriate.** Warn at authoring time; refuse to attest a deceptive value. | **Maximum tolerance.** Accept any string; never rewrite. |

> Rationale: strictness at authoring time costs the author one correction before
> publishing. Strictness at fill time costs a person their answer, silently, at
> the moment they are least able to notice.

#### 8.2.1 Filled data — never rewritten {#filled-never-rewritten}

A `url` or `email` hint describes what the author *hoped* to receive; it does not
license editing what was actually written.

A validator **MUST** report a warning, `RESPONSE_FORBIDDEN_CODE_POINT`
([Warnings](#warnings)), for a response that contains a code point
[Human-facing text](#human-text) excludes, other than a carriage return
(U+000D). [APR-TEXT-004]

> Rationale: a response keeps the line breaks a person typed ([Encoding](#encoding)),
> and a carriage return is one of them.

**Example 8.2.1-1.** A response carrying a zero-width space. The document is valid,
the response is kept exactly as written, and the validator warns.

```apr-example
id: response-forbidden-code-point
rule: filled-never-rewritten
violates: APR-TEXT-004, APR-VAL-036
representation: jsonc
expect: valid
warns: RESPONSE_FORBIDDEN_CODE_POINT
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Registration" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "name", "label": "Name", "response": "Ad​a" }
      ]
    }
  ]
}
```

The floor is the whole of it: the excluded set is the one
[Human-facing text](#human-text) states, not a shorter list of the invisible
characters that happen to be best known. An unassigned, private-use, surrogate or
deprecated code point in an answer is as much a surprise to the person reading it
as a zero-width space is.

The consuming workflow decides what to do about such a response; it is the only
party that knows what the answer is for.

A renderer **SHOULD** show a code point that [Human-facing text](#human-text)
excludes visibly, escaped or badged, wherever it presents one, without altering
the stored value. [APR-TEXT-013]

A reader that "cleans" a hidden or bidirectional character has let a hint enforce
something, which [Hints never enforce](#hints-advisory) forbids. Legitimate uses
exist: a Persian ZWNJ and an emoji ZWJ sequence are ordinary text; the warning
says so without altering them.

#### 8.2.2 Authoring data — strictness is appropriate {#authoring-strictness}

An implementation **MAY** hold authoring members to rules stricter than this
document states. [APR-TEXT-005]

**Strictness here means refusing, not rewriting.** No party's data is ever
silently edited — the difference between an author and a filler is that an author
*can* be stopped and asked to fix something, while a filler is never blocked.
Rewriting an authored value is not the strict option; it is the same silent edit
wearing a different hat.

An implementation **MUST NOT** Unicode-normalize authoring data or remove a code
point from it. [APR-TEXT-006]

**Example 8.2.2-1.** A label written in decomposed form. A reader that writes the
document back leaves the code points as it found them, rather than composing them
into the canonically equivalent single character.

```apr-example
id: authoring-not-normalized
rule: authoring-strictness
satisfies: APR-TEXT-006
representation: jsonc
expect: valid
round-trip: true
preserves: /sections/0/prompts/0/label
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Registration" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "p", "label": "Cafe\u0301 name" }
      ]
    }
  ]
}
```

`metadata.submissionUrls` is the strongest case in the format. It is an ordered,
author-supplied array of explicit delivery choices, machine-consumed and
security-critical. Cleaning a zero-width character out of a hostname picks a
destination on the author's behalf, a decision only the author can make.

A validator **SHOULD** report a warning, `SUBMISSION_URL_FORBIDDEN_CODE_POINT`
([Warnings](#warnings)), for a `submissionUrls` entry that contains a code point
[Human-facing text](#human-text) excludes. [APR-TEXT-007]

An implementation **MUST NOT** produce an attestation over a form whose
`submissionUrls` has such an entry. [APR-TEXT-008]

> Rationale: such a URL renders to a reviewer as one host while being another.
> An attestation binding it binds an address nobody reviewing the form could see.

**Example 8.2.2-2.** A submission target with a zero-width space inside its host. The
document is valid, the entry is kept exactly as written, and the validator warns.

```apr-example
id: submission-url-forbidden-code-point
rule: authoring-strictness
violates: APR-TEXT-007, APR-VAL-037
representation: jsonc
expect: valid
warns: SUBMISSION_URL_FORBIDDEN_CODE_POINT
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "Well permit",
    "submissionUrls": [ "https://uploads.exa​mple.gov/permits" ]
  },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

Ids are machine keys: they appear in attestation manifests, database columns, and
cell addresses.

A validator **SHOULD** report a warning, `ID_FORBIDDEN_CHARACTER`
([Warnings](#warnings)), for an id that contains a character outside
`[A-Za-z0-9_.-]`. [APR-TEXT-010]

**Example 8.2.2-3.** A prompt id with a space in it. The document is valid, and the
id draws a warning.

```apr-example
id: id-forbidden-character
rule: authoring-strictness
violates: APR-TEXT-010, APR-VAL-038
representation: jsonc
expect: valid
warns: ID_FORBIDDEN_CHARACTER
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Registration" },
  "sections": [
    {
      "id": "applicant",
      "title": "Applicant",
      "prompts": [
        { "id": "first name", "label": "First name" }
      ]
    }
  ]
}
```

**Example 8.2.2-4.** Ids using every character the rule admits. No warning.

```apr-example
id: id-allowed-characters
rule: authoring-strictness
satisfies: APR-TEXT-010, APR-VAL-038
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Registration" },
  "sections": [
    {
      "id": "applicant.Details-2",
      "title": "Applicant",
      "prompts": [
        { "id": "first_name.v2-A", "label": "First name" }
      ]
    }
  ]
}
```

#### 8.2.3 Human-facing text {#human-text}

Some members exist to be read or heard by a person: `metadata.title`,
`description`, `author` and `publisher`; a role's `name` and `description`; a
section's `title` and `description`; a prompt's `label`; and the `placeholder`,
`helpText` and `suggestedValues` hints. Every one of them is a string, because
its whole purpose is to be rendered as text or speech, and every one of them is
authoring data.

The human-facing text of a form **MUST** be in Normalization Form C (UAX #15) and
**MUST NOT** contain a code point that is unassigned, a surrogate, private-use, a
control other than U+0009 and U+000A, or that UTS #39 classifies with an
`Identifier_Type` of `Default_Ignorable`, `Deprecated`, or `Not_Character`. [APR-TEXT-011]

Bidirectional and joining behaviour comes from the characters' own properties
(UAX #9), never from explicit control characters, which the rule above excludes.

A validator **MUST** report a warning for human-facing text that is not in
Normalization Form C or that contains a code point the rule above excludes. [APR-TEXT-014]

A validator **SHOULD** apply the confusable and mixed-script detection of UTS #39
to human-facing text and report what it finds. [APR-TEXT-012]

**Example 8.2.3-1.** A Cyrillic letter in a Latin title.

```apr-example
id: title-confusable-script-mix
rule: human-text
satisfies: APR-TEXT-012, APR-VAL-035
representation: jsonc
expect: valid
warns: CONFUSABLE_SCRIPT_MIX
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "P\u0430ypal Permit" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 8.2.3-2.** A title in Normalization Form C with no excluded code point.

```apr-example
id: title-clean
rule: human-text
satisfies: APR-TEXT-011
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Permit Application" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

**Example 8.2.3-3.** A zero-width space, which is `Default_Ignorable`, inside a title.

```apr-example
id: title-zero-width-space
rule: human-text
violates: APR-TEXT-011
representation: jsonc
expect: valid
warns: FORBIDDEN_CODE_POINT
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Permit\u200bApplication" },
  "sections": [
    { "id": "s", "title": "S", "prompts": [ { "id": "p", "label": "P" } ] }
  ]
}
```

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
presentation only.

A reader **MUST NOT** derive a subject, a revision, a chronology, or a trust
relationship from the position of a record. [APR-STREAM-005]

Each record is exactly one of:

- a complete standalone APR form; or
- an APR attestation.

A reader **MUST** read a record that carries `recordType` as an attestation, and any
other record as a form. [APR-STREAM-008]

A reader **MUST** reject a record that is neither a form nor an attestation. [APR-STREAM-007]

**Example 9-1.** A record that is neither a form nor an attestation.

```apr-example
id: stream-record-unknown-type
rule: streams
violates: APR-STREAM-007
representation: jsonc-stream
expect: reject
diagnostic: WRONG_TYPE
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
---
{"recordType":"note","aprVersion":"1.0-beta.6"}
```

A reader **MUST** reject a stream that mixes representations. [APR-STREAM-001]

A reader **MUST NOT** deduplicate repeated form occurrences, even when their
semantic digests are identical. [APR-STREAM-003]

Two occurrences of one form are two records, and a reader that collapses them has
lost a fact the sender stated.

A reader asked for a single form that is given a stream **MUST** report
`APR_STREAM_REQUIRES_ITERATION` and **MUST NOT** select a record by position. [APR-STREAM-004]

Iterating a stream yields every record.

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
comment can span the separator.

A reader **MUST** reject an APR-JSONC stream in which a record is not preceded by
`RS`. [APR-STREAM-006]

**Example 9.1-1.** Two records, each preceded by a separator.

```apr-example
id: stream-jsonc-framed
rule: jsonc-framing
satisfies: APR-STREAM-006, APR-STREAM-007
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"first"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"second"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
```

**Example 9.1-2.** A second record with no separator before it.

```apr-example
id: stream-missing-record-separator
rule: jsonc-framing
violates: APR-STREAM-006
representation: jsonc-stream
expect: reject
diagnostic: PARSE_ERROR
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"first"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
{"aprVersion":"1.0-beta.6","metadata":{"title":"second"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
```

### 9.2 YAML framing {#yaml-framing}

APR-YAML streams are YAML streams: the document productions of YAML 1.2.2
chapter 9 apply unchanged, and every YAML document in the stream is exactly one
APR record. No additional framing is defined, because YAML already has one.

**Example 9.2-1.** A YAML stream carrying two independent forms.

```apr-example
id: yaml-stream-two-forms
rule: yaml-framing
satisfies: APR-STREAM-001, APR-STREAM-007
representation: yaml-stream
expect: valid
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

**Example 9.2-2.** A stream that switches representation.

```apr-example
id: stream-mixed-representations
rule: streams
violates: APR-STREAM-001
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

A reader **MUST** produce the same sequence of semantic records from an APR-JSONC
stream and an APR-YAML stream whose records, in order, have equal semantic
models. [APR-STREAM-002]

**Example 9.3-1.** The same two records, framed as an APR-JSONC stream and as an
APR-YAML stream. Each reader produces the same two semantic records from either.

```apr-example
id: stream-equivalence-jsonc
rule: stream-equivalence
satisfies: APR-STREAM-002
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"First"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
---
{"aprVersion":"1.0-beta.6","metadata":{"title":"Second"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}
```

**Example 9.3-2.** The same two records as APR-YAML.

```apr-example
id: stream-equivalence-yaml
rule: stream-equivalence
satisfies: APR-STREAM-002
representation: yaml-stream
expect: valid
---
aprVersion: "1.0-beta.6"
metadata:
  title: First
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
---
aprVersion: "1.0-beta.6"
metadata:
  title: Second
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

---

## 10. Semantic digests and manifests {#digests}

`jcs-sha256` is the semantic digest algorithm.

An implementation that computes a semantic digest **MUST** compute it over the
RFC 8785 JCS serialization of the fully parsed JSON semantic model, encoded as
UTF-8, and express it as lowercase hexadecimal SHA-256 (FIPS 180-4) prefixed with
`sha256:`. [APR-DIGEST-006]

Source syntax is never hashed: one form has one digest, whichever representation,
comments, whitespace or member order it is written with.

**Example 10-1.** A form written with a comment and its members out of order.

```apr-example
id: digest-ignores-source-syntax
rule: digests
satisfies: APR-DIGEST-006
representation: jsonc
expect: valid
digest: sha256:dcf5ed7ce101fb3ed43e67d9d1006eb15834330860e85ad572ec7940e6fc25d6
---
{
  // a comment
  "sections": [ { "prompts": [ { "label": "P", "id": "p" } ], "title": "S", "id": "s" } ],
  "metadata": { "title": "T" },
  "aprVersion": "1.0-beta.6"
}
```

**Example 10-2.** The same form in APR-YAML, with the same digest.

```apr-example
id: digest-same-in-yaml
rule: digests
satisfies: APR-DIGEST-006
representation: yaml
expect: valid
digest: sha256:dcf5ed7ce101fb3ed43e67d9d1006eb15834330860e85ad572ec7940e6fc25d6
---
aprVersion: "1.0-beta.6"
metadata:
  title: T
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
```

A digest value **MUST** match `^sha256:[0-9a-f]{64}$`. [APR-DIGEST-001]

An implementation **MUST** include in a form digest every APR-defined member and
every unknown member that survived parsing, and exclude only source
trivia. [APR-DIGEST-002]

A verifier that cannot preserve or digest an unknown member **MUST** report the
assertion as `unverifiable`, not valid. [APR-DIGEST-007]

> Rationale: including unknown members prevents a whole-form attestation from
> silently omitting a meaningful member, so data written beside the members this
> document defines cannot change without changing the digest.

An integrity manifest describes a form without holding its plaintext. It carries
`root`, the digest of the subject form, and `entries`
([Attestation record](#attestation-catalogue)). Each entry has these members:

| Member | Type | Requirement | Meaning | Rule |
| --- | --- | --- | --- | --- |
| `path` | string | **REQUIRED** | A JSON Pointer (RFC 6901) to a value in the semantic model. | [APR-DIGEST-008] |
| `digest` | string | **REQUIRED** | The digest of the JCS encoding of the value at `path`. | [APR-DIGEST-009] |

`entries` **MUST** be ordered by `path`, compared as strings, and **MUST NOT**
repeat a path. [APR-DIGEST-003]

`entries` **MUST** contain the root pointer. [APR-DIGEST-004]

**Example 10-3.** A manifest whose entries carry both members, start at the root
pointer, and are ordered by path.

```apr-example
id: manifest-well-formed
rule: digests
satisfies: APR-DIGEST-001, APR-DIGEST-003, APR-DIGEST-004, APR-DIGEST-008, APR-DIGEST-009
representation: jsonc
expect: valid
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:abababababababababababababababababababababababababababababababab","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"path":"","digest":"sha256:abababababababababababababababababababababababababababababababab"},{"path":"/metadata","digest":"sha256:cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd"}]},"proofs":[],"witnesses":[]}
```

**Example 10-4.** A digest spelled in uppercase hexadecimal.

```apr-example
id: digest-uppercase
rule: digests
violates: APR-DIGEST-001
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:ABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABAB","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"path":"","digest":"sha256:abababababababababababababababababababababababababababababababab"}]},"proofs":[],"witnesses":[]}
```

**Example 10-5.** Manifest entries out of path order.

```apr-example
id: manifest-entries-unordered
rule: digests
violates: APR-DIGEST-003
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:abababababababababababababababababababababababababababababababab","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"path":"","digest":"sha256:abababababababababababababababababababababababababababababababab"},{"path":"/b","digest":"sha256:cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd"},{"path":"/a","digest":"sha256:efefefefefefefefefefefefefefefefefefefefefefefefefefefefefefefef"}]},"proofs":[],"witnesses":[]}
```

**Example 10-6.** A manifest without the root pointer.

```apr-example
id: manifest-without-root-pointer
rule: digests
violates: APR-DIGEST-004
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:abababababababababababababababababababababababababababababababab","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"path":"/a","digest":"sha256:cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd"}]},"proofs":[],"witnesses":[]}
```

**Example 10-7.** A manifest entry without `path`.

```apr-example
id: manifest-entry-without-path
rule: digests
violates: APR-DIGEST-008
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:abababababababababababababababababababababababababababababababab","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"digest":"sha256:abababababababababababababababababababababababababababababababab"}]},"proofs":[],"witnesses":[]}
```

**Example 10-8.** A manifest entry without `digest`.

```apr-example
id: manifest-entry-without-digest
rule: digests
violates: APR-DIGEST-009
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:abababababababababababababababababababababababababababababababab","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"path":""}]},"proofs":[],"witnesses":[]}
```

An implementation producing a manifest **SHOULD** give it one entry for every
value in the semantic model at every depth, unknown members included. [APR-DIGEST-010]

A `fields` scope is the exception: what it carries is stated in
[Scope](#attestation-scope), and nothing further is expected of it.

Integrity comes from `root` alone. Entries are how a verifier explains *which*
values differ without the manifest retaining what they used to be, so a manifest
missing a path explains less and proves exactly as much. A verifier that finds a
subject differing from `root` **MUST** report the difference at the most specific
path the manifest carries, and **MUST NOT** report a path it does not. [APR-DIGEST-005]

> Rationale: ordering is required because two
> manifests over one form should be one manifest. Completeness is recommended
> rather than required because a whole-document manifest over a large form runs
> to thousands of entries, and buying diagnosis with size is the signer's call.

---

## 11. Profile: expressions {#expressions}

**OPTIONAL.** Core implementations skip this section entirely.

### 11.1 What expressions are {#expr-what}

Five advisory hints that let a form react to its own answers: showing a field
only when relevant, computing a total, flagging a cross-field inconsistency.
Each is an optional member of [Hints](#hints-object).

| Hint | Effect when truthy |
| --- | --- |
| `exprHidden` | Hide this prompt |
| `exprValue` | Computed value, still editable |
| `exprExpected` | Mark as expected; advisory, never blocks |
| `exprValidation` | Returns a message; empty means valid |
| `exprReadOnly` | Make read-only |

### 11.2 Invariants {#expr-invariants}

Stored responses remain authoritative.

An implementation evaluating an expression **MUST NOT** reject, rewrite, or
invalidate a response. [APR-EXPR-001]

Evaluation is pure.

An implementation **MUST NOT** expose filesystem, network, process, clock,
randomness, reflection, environment, or document-mutation access to an
expression. [APR-EXPR-002]

Failure preserves data: a failed evaluation produces a diagnostic and the
fallback ([Results and fallback](#expr-fallback)).

An implementation **MUST NOT** let a failed evaluation propagate as an error
into a filling workflow. [APR-EXPR-003]

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
and **MUST NOT** provide any extension library or custom function. [APR-EXPR-013]

An expression naming a function outside that surface is an evaluation failure,
and the per-hint fallback applies ([Results and fallback](#expr-fallback)).

**Example 11.3-1.** A function from an extension library fails, and the fallback applies.

```apr-example
id: expr-extension-function-fails
rule: expr-language
satisfies: APR-EXPR-013
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"check": ""}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "name",
          "label": "Name",
          "response": "abc"
        },
        {
          "id": "check",
          "label": "Check",
          "hints": {
            "exprValidation": "name.upperAscii()"
          }
        }
      ]
    }
  ]
}
```

> Rationale: without a pin, a function that did not exist when a form was written
> is neither clearly valid nor clearly invalid, and two conforming readers may
> evaluate the same form to different values. Pinning the specification release
> rather than a library lets implementations in different languages each track
> their own language's library, provided each conforms to the same definition.

### 11.4 Activation {#expr-activation}

An implementation **MUST** evaluate an expression against this read-only
activation and nothing else. [APR-EXPR-015]

**Example 11.4-1.** An expression naming something the activation does not hold. There
is no host environment to reach, so the evaluation fails and `exprValue`'s
fallback retains the stored response exactly.

```apr-example
id: expr-activation-is-all-there-is
rule: expr-activation
satisfies: APR-EXPR-015
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"p": "kept"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "T" },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "p", "label": "P", "response": "kept",
          "hints": { "exprValue": "env.HOME" } }
      ]
    }
  ]
}
```

Each row below is a requirement on an implementation: it supplies the name in
the activation, with the type and meaning the row gives.

| Name | Type | Meaning | Requirement | Rule |
| --- | --- | --- | --- | --- |
| a prompt's `id` | that prompt's bound type | Direct binding, where the id is a valid CEL identifier and not reserved. | **MUST** | [APR-EXPR-017] |
| `_this` | the owning prompt's bound type | The response of the prompt carrying this hint. | **MUST** | [APR-EXPR-018] |
| `_id` | `string` | The owning prompt's id. | **MUST** | [APR-EXPR-019] |
| `_now` | `timestamp` | The evaluation instant, supplied by the caller. | **MUST** | [APR-EXPR-020] |
| `_today` | `string` | The evaluation date, supplied by the caller. | **MUST** | [APR-EXPR-021] |
| `ctx` | `map` | Host-supplied context ([Context](#expr-context)). | **MUST** | [APR-EXPR-022] |

An implementation **MUST NOT** let a direct binding shadow `_this`, `_id`, `_now`,
`_today`, or `ctx`. [APR-EXPR-004]

An implementation **MUST NOT** give a prompt whose id is not a valid CEL
identifier a direct binding, nor make it reachable from an expression by any
other name. [APR-EXPR-016]

An implementation **MUST** take `_now` and `_today` from the caller and never
from the host clock during evaluation. [APR-EXPR-005]

Evaluating the same form twice with the same inputs then yields the same result.

**Example 11.4-2.** A prompt read by its id.

```apr-example
id: expr-direct-binding
rule: expr-activation
satisfies: APR-EXPR-017
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"check": "too big"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "amount",
          "label": "Amount",
          "response": "12",
          "hints": {
            "expectedDataType": "number"
          }
        },
        {
          "id": "check",
          "label": "Check",
          "hints": {
            "exprValidation": "amount > 10.0 ? 'too big' : ''"
          }
        }
      ]
    }
  ]
}
```

**Example 11.4-3.** `_this` is the owning prompt's response.

```apr-example
id: expr-this
rule: expr-activation
satisfies: APR-EXPR-018
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"code": "long"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "code",
          "label": "Code",
          "response": "abc",
          "hints": {
            "exprValidation": "size(_this) > 2 ? 'long' : ''"
          }
        }
      ]
    }
  ]
}
```

**Example 11.4-4.** `_id` is the owning prompt's id.

```apr-example
id: expr-id
rule: expr-activation
satisfies: APR-EXPR-019
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"echo": "echo"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "echo",
          "label": "Echo",
          "hints": {
            "exprValue": "_id"
          }
        }
      ]
    }
  ]
}
```

**Example 11.4-5.** `_today` comes from the caller.

```apr-example
id: expr-today
rule: expr-activation
satisfies: APR-EXPR-021, APR-EXPR-005
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"signed": "2026-09-01"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "signed",
          "label": "Signed",
          "hints": {
            "exprValue": "_today"
          }
        }
      ]
    }
  ]
}
```

**Example 11.4-6.** `ctx` carries what the host supplies.

```apr-example
id: expr-ctx
rule: expr-activation
satisfies: APR-EXPR-022
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"team": "records"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "team",
          "label": "Team",
          "hints": {
            "exprValue": "ctx.team"
          }
        }
      ]
    }
  ]
}
```

**Example 11.4-7.** A prompt whose id is a reserved name does not shadow it.

```apr-example
id: expr-reserved-name
rule: expr-activation
satisfies: APR-EXPR-004
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"signed": "2026-09-01"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "_today",
          "label": "Today",
          "response": "stored"
        },
        {
          "id": "signed",
          "label": "Signed",
          "hints": {
            "exprValue": "_today"
          }
        }
      ]
    }
  ]
}
```

**Example 11.4-8.** A prompt whose id is not a CEL identifier cannot be read, not even under a similar name.

```apr-example
id: expr-invalid-identifier
rule: expr-activation
satisfies: APR-EXPR-016
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"greeting": ""}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "first-name",
          "label": "First name",
          "response": "Ada"
        },
        {
          "id": "greeting",
          "label": "Greeting",
          "hints": {
            "exprValue": "first_name"
          }
        }
      ]
    }
  ]
}
```

### 11.5 The type environment {#expr-binding}

CEL is statically typed. `expectedDataType` supplies the types, and an
implementation binds a response to the CEL type of its row:

| `expectedDataType` | CEL type | Requirement | Rule |
| --- | --- | --- | --- |
| `number`, `currency`, `range` | `double` | **MUST** | [APR-EXPR-023] |
| `boolean` | `bool` | **MUST** | [APR-EXPR-024] |
| `date`, `time`, `datetime` | `timestamp` | **MUST** | [APR-EXPR-025] |
| `multichoice` | `list<string>` | **MUST** | [APR-EXPR-026] |
| everything else, or absent | `string` | **MUST** | [APR-EXPR-027] |

> Rationale: this is what lets an author write `quantity * unit_price` rather
> than wrapping every reference in a conversion, and what lets a type checker
> tell them an expression is wrong before a filler ever sees the form.

**Example 11.5-1.** A `currency` response binds as a `double`.

```apr-example
id: expr-bind-currency
rule: expr-binding
satisfies: APR-EXPR-023
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"check": "five"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "price",
          "label": "Price",
          "response": "2.50",
          "hints": {
            "expectedDataType": "currency"
          }
        },
        {
          "id": "check",
          "label": "Check",
          "hints": {
            "exprValidation": "price * 2.0 == 5.0 ? 'five' : 'other'"
          }
        }
      ]
    }
  ]
}
```

**Example 11.5-2.** A `date` response binds as a `timestamp`.

```apr-example
id: expr-bind-date
rule: expr-binding
satisfies: APR-EXPR-025
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"check": "ok"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "when",
          "label": "When",
          "response": "2026-03-01",
          "hints": {
            "expectedDataType": "date"
          }
        },
        {
          "id": "check",
          "label": "Check",
          "hints": {
            "exprValidation": "when < timestamp('2026-01-01T00:00:00Z') ? 'too early' : 'ok'"
          }
        }
      ]
    }
  ]
}
```

**Example 11.5-3.** A `multichoice` response binds as a `list<string>`.

```apr-example
id: expr-bind-multichoice
rule: expr-binding
satisfies: APR-EXPR-026
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"check": "two"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "colours",
          "label": "Colours",
          "response": "red\ngreen",
          "hints": {
            "expectedDataType": "multichoice"
          }
        },
        {
          "id": "check",
          "label": "Check",
          "hints": {
            "exprValidation": "size(colours) == 2 ? 'two' : 'other'"
          }
        }
      ]
    }
  ]
}
```

**Example 11.5-4.** A response with no `expectedDataType` binds as a `string`, even when it looks like a number.

```apr-example
id: expr-bind-string
rule: expr-binding
satisfies: APR-EXPR-027
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"check": "12!"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "name",
          "label": "Name",
          "response": "12"
        },
        {
          "id": "check",
          "label": "Check",
          "hints": {
            "exprValidation": "name + '!'"
          }
        }
      ]
    }
  ]
}
```

### 11.6 Values that will not bind {#expr-unbound}

An implementation **MUST** treat a response that cannot be converted to its
declared type — free text in a `number` field, an unparseable date, or an empty
one — as unbound, and never as a default. [APR-EXPR-006]

An expression reading an unbound value fails, and the fallback applies.

> Rationale: binding an empty number as zero would make a blank field silently
> total as zero — a wrong answer rather than no answer. Unbound also keeps
> short-circuiting usable: a conjunction whose first operand is false does not
> need its second operand to bind.

**Example 11.6-1.** An empty `number` does not bind as zero, so the total keeps its stored response.

```apr-example
id: expr-unbound-empty
rule: expr-unbound
satisfies: APR-EXPR-006
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"total": "5"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "quantity",
          "label": "Quantity",
          "hints": {
            "expectedDataType": "number"
          }
        },
        {
          "id": "total",
          "label": "Total",
          "response": "5",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "quantity + 1.0"
          }
        }
      ]
    }
  ]
}
```

### 11.7 Context {#expr-context}

`ctx` carries data the host application supplies — the person's own details,
their organization, their environment — so a form can offer what it already
knows.

A host **MUST NOT** place credentials, secrets, authorization decisions, or
facts private to its own systems in `ctx`. [APR-EXPR-007]

An expression is document-supplied text; what it can read, a document author can
read.

### 11.8 Results and fallback {#expr-fallback}

An implementation **MUST** write a result back to a stored string through the
canonical write forms of [Canonical value forms](#canonical-values). [APR-EXPR-028]

Each hint requires a result type. Any failure — a compile error, an evaluation
error, an unbound reference, or a result of the wrong type — applies the hint's
fallback, and an implementation applies it as its row states:

| Hint | Required result | Fallback | Requirement | Rule |
| --- | --- | --- | --- | --- |
| `exprHidden` | `bool` | false — show the prompt | **MUST** | [APR-EXPR-029] |
| `exprExpected` | `bool` | false — do not mark expected | **MUST** | [APR-EXPR-030] |
| `exprReadOnly` | `bool` | false — keep editable | **MUST** | [APR-EXPR-031] |
| `exprValidation` | `string` | empty — no advisory | **MUST** | [APR-EXPR-032] |
| `exprValue` | the prompt's bound type | Do not write; retain the stored response exactly | **MUST** | [APR-EXPR-033] |

Every fallback shows more and blocks less. A form whose expressions all fail is a
plain form.

**Example 11.8-1.** A computed `number` is written in its canonical form.

```apr-example
id: expr-canonical-result
rule: expr-fallback
satisfies: APR-EXPR-028
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"sum": "3"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "sum",
          "label": "Sum",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "2.0 + 1.0"
          }
        }
      ]
    }
  ]
}
```

**Example 11.8-2.** An `exprHidden` that fails shows the prompt.

```apr-example
id: expr-fallback-hidden
rule: expr-fallback
satisfies: APR-EXPR-029
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"hidden": {"details": false}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "details",
          "label": "Details",
          "hints": {
            "exprHidden": "nosuch == 'no'"
          }
        }
      ]
    }
  ]
}
```

**Example 11.8-3.** An `exprValidation` that returns the wrong type gives no advisory.

```apr-example
id: expr-fallback-validation
rule: expr-fallback
satisfies: APR-EXPR-032
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"validation": {"code": ""}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "code",
          "label": "Code",
          "hints": {
            "exprValidation": "42"
          }
        }
      ]
    }
  ]
}
```

**Example 11.8-4.** An `exprValue` that fails leaves the stored response alone.

```apr-example
id: expr-fallback-value
rule: expr-fallback
satisfies: APR-EXPR-033, APR-EXPR-001
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"total": "7"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "total",
          "label": "Total",
          "response": "7",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "nosuch + 1.0"
          }
        }
      ]
    }
  ]
}
```

**Example 11.8-5.** An `exprExpected` that evaluates marks its prompt expected, and
one that fails does not.

```apr-example
id: expr-fallback-expected
rule: expr-fallback
satisfies: APR-EXPR-030
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"expected": {"signature": true, "details": false}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "consent", "label": "Consent", "response": "yes" },
        {
          "id": "signature",
          "label": "Signature",
          "hints": { "exprExpected": "consent == 'yes'" }
        },
        {
          "id": "details",
          "label": "Details",
          "hints": { "exprExpected": "nosuch == 'no'" }
        }
      ]
    }
  ]
}
```

**Example 11.8-6.** An `exprReadOnly` that evaluates marks its prompt read-only, and
one that fails keeps its prompt editable.

```apr-example
id: expr-fallback-read-only
rule: expr-fallback
satisfies: APR-EXPR-031
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"readOnly": {"reference": true, "details": false}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        { "id": "consent", "label": "Consent", "response": "yes" },
        {
          "id": "reference",
          "label": "Reference",
          "hints": { "exprReadOnly": "consent == 'yes'" }
        },
        {
          "id": "details",
          "label": "Details",
          "hints": { "exprReadOnly": "nosuch == 'no'" }
        }
      ]
    }
  ]
}
```

### 11.9 A computed value is a suggestion, not a lock {#expr-computed}

A renderer **MUST** keep a computed prompt editable. [APR-EXPR-014]

Any string is a valid response, and a renderer that refuses typing into a
computed field has stopped implementing the format. A total that is wrong —
because the form's arithmetic does not match what was actually agreed — is for
the person filling it in to correct.

Being computed does not make a prompt read-only. `exprReadOnly` asks for that
*presentation*, and even then it is an affordance rather than a wall.

**A correction survives recomputation.** Every non-empty response in a document
as it was read is **authored**, whatever produced it.

An implementation **MUST NOT** overwrite an authored response when it recomputes. [APR-EXPR-008]

A reader presents the recomputed value as a suggestion instead.

A reader **MAY** replace a response it computed itself in the same session. [APR-EXPR-034]

That knowledge is the reader's own state, and the document records nothing about
it.

> Rationale: a document cannot tell a stale computed value from a correction
> someone typed. Treating every response already in the file as
> authored needs no marker and errs toward keeping an answer, which is the one
> thing this format exists to prevent losing. The cost is that a stale value does
> not silently refresh across a save and reopen, and that is the correct cost: a
> computed value is a suggestion, not a lock.

**Example 11.9-1.** A response already in the document is not overwritten, while an empty one is computed.

```apr-example
id: expr-authored-response
rule: expr-computed
satisfies: APR-EXPR-008
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"corrected": "99", "computed": "4"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "base",
          "label": "Base",
          "response": "2",
          "hints": {
            "expectedDataType": "number"
          }
        },
        {
          "id": "corrected",
          "label": "Corrected",
          "response": "99",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "base * 2.0"
          }
        },
        {
          "id": "computed",
          "label": "Computed",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "base * 2.0"
          }
        }
      ]
    }
  ]
}
```

An implementation **MUST** order computed prompts by their direct references so
that a subtotal feeds a tax feeds a total in one pass. [APR-EXPR-009]

A self-reference or a dependency cycle is an authoring error, and evaluating one
is an evaluation failure that applies the fallback.

**Example 11.9-2.** A total that depends on a tax that depends on a subtotal settles in one pass.

```apr-example
id: expr-dependency-order
rule: expr-computed
satisfies: APR-EXPR-009
representation: jsonc
expect: valid
evaluate: {"_today": "2026-09-01", "_now": "2026-09-01T12:00:00Z", "ctx": {"team": "records"}}
expects: {"responses": {"subtotal": "10", "tax": "5", "total": "15"}}
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": {
    "title": "T"
  },
  "sections": [
    {
      "id": "s",
      "title": "S",
      "prompts": [
        {
          "id": "qty",
          "label": "Quantity",
          "response": "2",
          "hints": {
            "expectedDataType": "number"
          }
        },
        {
          "id": "price",
          "label": "Price",
          "response": "5",
          "hints": {
            "expectedDataType": "number"
          }
        },
        {
          "id": "total",
          "label": "Total",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "subtotal + tax"
          }
        },
        {
          "id": "tax",
          "label": "Tax",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "subtotal * 0.5"
          }
        },
        {
          "id": "subtotal",
          "label": "Subtotal",
          "hints": {
            "expectedDataType": "number",
            "exprValue": "qty * price"
          }
        }
      ]
    }
  ]
}
```

### 11.10 Authoring-time checking {#expr-authoring}

An implementation **SHOULD** type-check expressions against the document's type
environment when a template is authored, and report failures to the author with
position information. [APR-EXPR-010]

This is exactly where [Authoring data](#authoring-strictness) says strictness
belongs. The **author** is stopped and asked to fix something before publication;
the **filler** is never blocked, because at fill time the same expression
degrades.

### 11.11 Bounds {#expr-limits}

An implementation **MUST** bound expression size, complexity, and evaluation
cost, so that evaluation terminates. [APR-EXPR-011]

An implementation **MUST** report reaching a bound as a failure that applies the
fallback, never as partial mutation. [APR-EXPR-035]

Exact bounds are implementation-defined, for the reason given in
[Security considerations](#security).

---

## 12. Profile: attestations {#attestations}

**OPTIONAL.** Core implementations preserve attestation records and report them
as unchecked.

### 12.1 Model {#attestation-model}

An APR form is ordinary form
data; cryptographic assertions live in **independent attestation records** that
travel in the same stream.

> Rationale: keeping the assertion outside the form means a reader that ignores attestations simply reads forms,
> and an attestation identifies its subject by content rather than by position or
> filename — so it does not matter what order records arrive in, or whether the
> subject is even present.

Verification is a pure computation over bytes already in hand: **a form carrying
attestations is exactly as safe to open as one without.** APR never executes
anything.

### 12.2 Attestation record {#attestation-catalogue}

An attestation is a JSON object. Each row below is a requirement on an
attestation record, and its Requirement column says whether the member is
present. A validator reports a member its row requires that is missing, and a
member outside what its row allows, by the codes
[Errors](#structural-validation) names.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `recordType` | string | **REQUIRED** | [APR-ATTEST-001] | Exactly `attestation`. |
| `aprVersion` | string | **REQUIRED** | [APR-ATTEST-002] | `1.0-beta.7`, or `1.0-beta.6` ([Version compatibility](#version-compatibility)). |
| `subject` | object | **REQUIRED** | [APR-ATTEST-021] | `digest` and `canonicalization`, and no other member. |
| `subject.digest` | string | **REQUIRED** | [APR-ATTEST-022] | The digest ([Digests and manifests](#digests)) of the subject form's complete semantic model. |
| `subject.canonicalization` | string | **REQUIRED** | [APR-ATTEST-003] | Exactly `jcs-sha256`. |
| `scope` | object | **REQUIRED** | [APR-ATTEST-023] | `kind` and `fields` ([Scope](#attestation-scope)), and no other member. |
| `scope.kind` | string | **REQUIRED** | [APR-ATTEST-024] | `document` or `fields`. |
| `scope.fields` | array | **REQUIRED** when `kind` is `fields` | [APR-ATTEST-025] | One or more prompt ids, none blank. |
| `manifest` | object | **REQUIRED** | [APR-ATTEST-026] | `root` and `entries`, and no other member. |
| `manifest.root` | string | **REQUIRED** | [APR-ATTEST-027] | The digest of the subject form. |
| `manifest.entries` | array | **REQUIRED** | [APR-ATTEST-028] | Entries as [Digests and manifests](#digests) defines them, each with no other member. |
| `proofs` | array | **REQUIRED** | [APR-ATTEST-029] | Zero or more proofs ([Proofs](#proofs)). |
| `witnesses` | array | **REQUIRED** | [APR-ATTEST-030] | Zero or more witnesses ([Witnesses](#witnesses)). |

An attestation record **MAY** carry extension members. [APR-ATTEST-004]

An implementation **MUST** preserve an attestation record's extension members
across a round trip. [APR-ATTEST-031]

A verifier **MUST** resolve a subject by `subject.digest` alone, and never by
stream position, filename, or document id. [APR-ATTEST-017]

**Example 12.2-1.** An attestation over a whole form.

```apr-example
id: attestation-document-scope
rule: attestation-catalogue
satisfies: APR-ATTEST-001, APR-ATTEST-002, APR-ATTEST-003, APR-ATTEST-021, APR-ATTEST-022, APR-ATTEST-023, APR-ATTEST-024, APR-ATTEST-026, APR-ATTEST-027, APR-ATTEST-028, APR-ATTEST-029, APR-ATTEST-030
representation: jsonc
expect: valid
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-2.** A `recordType` that is not `attestation`.

```apr-example
id: attestation-record-type-wrong
rule: attestation-catalogue
violates: APR-ATTEST-001
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestations",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-3.** An attestation written to another version.

```apr-example
id: attestation-version-wrong
rule: attestation-catalogue
violates: APR-ATTEST-002
representation: jsonc
expect: reject
diagnostic: UNSUPPORTED_VERSION
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.5",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-4.** A canonicalization other than `jcs-sha256`.

```apr-example
id: attestation-canonicalization-wrong
rule: attestation-catalogue
violates: APR-ATTEST-003
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-5.** A `subject` with a member it does not define.

```apr-example
id: attestation-subject-extra-member
rule: attestation-catalogue
violates: APR-ATTEST-021
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256",
    "note": "signed at the counter"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-6.** A `subject.digest` that is not a digest.

```apr-example
id: attestation-subject-digest-malformed
rule: attestation-catalogue
violates: APR-ATTEST-022
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:C525",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-7.** An attestation without a `scope`.

```apr-example
id: attestation-scope-missing
rule: attestation-catalogue
violates: APR-ATTEST-023
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-8.** A `scope.kind` that is neither `document` nor `fields`.

```apr-example
id: attestation-scope-kind-unknown
rule: attestation-catalogue
violates: APR-ATTEST-024
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "section"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-9.** A `fields` scope that names no prompt.

```apr-example
id: attestation-fields-empty
rule: attestation-catalogue
violates: APR-ATTEST-025
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "fields",
    "fields": []
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-10.** A `manifest` with a member it does not define.

```apr-example
id: attestation-manifest-extra-member
rule: attestation-catalogue
violates: APR-ATTEST-026
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ],
    "note": "complete"
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-11.** A `manifest.root` that is not a digest.

```apr-example
id: attestation-manifest-root-malformed
rule: attestation-catalogue
violates: APR-ATTEST-027
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "c525780361ebf5ef",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-12.** A manifest entry with a member it does not define.

```apr-example
id: attestation-entry-extra-member
rule: attestation-catalogue
violates: APR-ATTEST-028
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
        "value": "Ada"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**Example 12.2-13.** `proofs` that is not an array.

```apr-example
id: attestation-proofs-not-array
rule: attestation-catalogue
violates: APR-ATTEST-029
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": {},
  "witnesses": []
}
```

**Example 12.2-14.** `witnesses` that is not an array.

```apr-example
id: attestation-witnesses-not-array
rule: attestation-catalogue
violates: APR-ATTEST-030
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": "none"
}
```

**Example 12.2-15.** An extension member on an attestation record survives a round trip.

```apr-example
id: attestation-extension-member
rule: attestation-catalogue
satisfies: APR-ATTEST-004, APR-ATTEST-031
representation: jsonc
expect: valid
round-trip: true
preserves: /com.example.counter
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": [],
  "com.example.counter": "front desk"
}
```

### 12.3 Scope {#attestation-scope}

A `document` scope covers the complete form, including its extension members.

A `fields` scope covers the prompts its `fields` member names.

The manifest of a `fields` attestation **MUST** include each selected prompt,
its response and hints, and every ancestor section's id, title, description,
kind, and role. [APR-ATTEST-005]

**Example 12.3-1.** A `fields` attestation over one prompt and the section it sits in.

```apr-example
id: attestation-fields-scope
rule: attestation-scope
satisfies: APR-ATTEST-005, APR-ATTEST-024, APR-ATTEST-025
representation: jsonc
expect: valid
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "fields",
    "fields": [
      "name"
    ]
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      },
      {
        "path": "/sections/0/id",
        "digest": "sha256:ddb020662a633640dd1d4d0dd981e920629b458a79fc1a1f083ed4a83a2b8a6e"
      },
      {
        "path": "/sections/0/prompts/0",
        "digest": "sha256:ecb874b2ecb9667ab8ff21f6479c62ed5db164db6f059a87ec6fc3a100f5a94a"
      },
      {
        "path": "/sections/0/prompts/0/response",
        "digest": "sha256:a39afeed7d3319213be7a235840b3c6d3f09f2810b9c91779f34572b6b36832a"
      },
      {
        "path": "/sections/0/title",
        "digest": "sha256:290ba830f4a1f9820d0bfe1e494a084252c7dcb52b61d406f09d2fa51bed9f9a"
      }
    ]
  },
  "proofs": [],
  "witnesses": []
}
```

**A filler attests to the question, not only the answer.** Anything less is not
an attestation on a form.

> Rationale: a scope covering the response alone fails like this. Sign "No" to *"Have
> you ever been convicted of a felony?"*, let someone afterwards change the label
> to *"Do you enjoy long walks?"*, and the signature would still verify — putting a
> person on record as having answered a question they never saw. Covering the
> question, its type, and its offered options is what closes that.

A fields scope is deliberately *not* the whole document: a filler attests to
their part.

A verifier **MUST NOT** report a `fields` attestation as `invalid` because someone
edited a section it does not cover. [APR-ATTEST-006]

**What that protection is, exactly.** `subject.digest` names the complete form,
so an edit anywhere produces a changed form, and against *that* form the
attestation is `unresolved` rather than invalid
([Changed forms](#changed-forms), [Verification vocabulary](#verification)). It
stays `valid` against the form it was made over, which is why a workflow retains
the original record rather than replacing it.

A verifier **MUST NOT** report `invalid` merely because the form it holds is a
later one. [APR-ATTEST-015]

A verifier **MAY** compare a `fields` manifest's entries against a changed form
and report which attested paths still match. [APR-ATTEST-016]

A verifier **MUST NOT** report that comparison as a verification result. [APR-ATTEST-032]

The comparison is a diagnostic, and the attestation remains an assertion about
its original subject.

> Rationale: the promise above is worth making and was worth stating precisely.
> Read loosely it suggests a fields attestation keeps verifying across edits,
> which nothing in the format delivers, because a digest over the whole form
> moves when any part of it does. What the format does deliver is that a filler
> is never reported as having signed something false — and, for a reader that
> wants it, a per-path answer to what actually changed.

### 12.4 Proofs {#proofs}

A proof is a JSON object. Each row below is a requirement on a proof.

| Member | Type | Requirement | Rule | Notes |
| --- | --- | --- | --- | --- |
| `type` | string | **REQUIRED** | [APR-ATTEST-033] | The proof type. |
| `value` | string | **REQUIRED** | [APR-ATTEST-034] | The proof, encoded as its type defines. |

A proof **MUST NOT** carry a copy of the subject digest or the scope. [APR-ATTEST-007]

> Rationale: two copies of one fact is a correctness bug everywhere in this
> format, and here it is a security hole: a verifier that checks a second copy
> reports a signature valid after the real value has changed.

**Example 12.4-1.** A proof carrying its own copy of the subject.

```apr-example
id: attestation-proof-copies-subject
rule: proofs
violates: APR-ATTEST-007
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [
    {
      "type": "example/opaque-v1",
      "value": "b3BhcXVl",
      "subject": {
        "digest": "sha256:abababababababababababababababababababababababababababababababab"
      }
    }
  ],
  "witnesses": []
}
```

An implementation producing a proof **MUST** compute it over the JCS
serialization (RFC 8785) of the attestation's envelope: the record without its
`proofs` member. [APR-ATTEST-035]

A verifier **MUST** verify a proof over that same serialization. [APR-ATTEST-036]

> Rationale: two implementations that sign different bytes can each pass their
> own tests and never verify each other.

This specification defines one proof type. Each row below is a requirement on a
proof of that type: its `value` is what the row states.

| Type | Value | Requirement | Rule |
| --- | --- | --- | --- |
| `cms/ecdsa-p256-sha256` | ECDSA over the P-256 curve with SHA-256 (FIPS 186-5), carried as CMS SignedData (RFC 5652) with the X.509 certificate chain (RFC 5280) included, encoded as base64 (RFC 4648). | **MUST** | [APR-ATTEST-037] |

A verifier that does not recognize a proof type **MUST** report the proof as
`unverifiable`, never as `invalid`. [APR-ATTEST-008]

An implementation **MUST** preserve a proof whose type it does not
recognize. [APR-ATTEST-038]

**Example 12.4-2.** A proof of a type the reader does not recognize survives a round trip.

```apr-example
id: attestation-unknown-proof-preserved
rule: proofs
satisfies: APR-ATTEST-007, APR-ATTEST-033, APR-ATTEST-034, APR-ATTEST-038
representation: jsonc
expect: valid
round-trip: true
preserves: /proofs/0/type, /proofs/0/value
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [
    {
      "type": "example/opaque-v1",
      "value": "b3BhcXVl"
    }
  ],
  "witnesses": []
}
```

**Example 12.4-3.** A proof without `type`.

```apr-example
id: attestation-proof-without-type
rule: proofs
violates: APR-ATTEST-033
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"}]},"proofs":[{"value":"b3BhcXVl"}],"witnesses":[]}
```

**Example 12.4-4.** A proof without `value`.

```apr-example
id: attestation-proof-without-value
rule: proofs
violates: APR-ATTEST-034
representation: jsonc
expect: reject
diagnostic: REQUIRED_FIELD
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"}]},"proofs":[{"type":"example/opaque-v1"}],"witnesses":[]}
```

"I cannot check this" and "this is forged" are different statements.

A renderer **MUST NOT** present an `unverifiable` proof as `invalid`. [APR-ATTEST-039]

A proof **MAY** carry a claimed signing time. [APR-ATTEST-014]

In `cms/ecdsa-p256-sha256` that is
the CMS signing-time signed attribute (RFC 5652), which sits inside the signature
and therefore cannot be altered without breaking it. What nothing vouches for is
the clock: the value is the signer's assertion that they signed then, and no
more.

A renderer that shows a claimed signing time **MUST** show it as claimed rather
than proven. [APR-ATTEST-040]

An implementation **MUST NOT** conclude from a claimed signing time that one
record precedes another. [APR-ATTEST-041]

> Rationale: every signature format works this way, and pretending otherwise is
> how a plausible timestamp becomes evidence it was never entitled to be.
> Trusted time needs a time authority, which this specification does not define.
> A claimed time is still worth carrying, because it is what the signer said.

### 12.5 Witnesses {#witnesses}

`witnesses` records that this attestation's signer explicitly witnessed earlier
assertions.

Each entry in `witnesses` **MUST** be the digest of an earlier attestation's
envelope. [APR-ATTEST-042]

`witnesses` **MUST NOT** repeat a digest. [APR-ATTEST-043]

**Example 12.5-1.** An attestation witnessing an earlier one in the same stream.

```apr-example
id: attestation-witness
rule: witnesses
satisfies: APR-ATTEST-042, APR-ATTEST-043
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","documentType":"template","metadata":{"title":"Beta 6 permit"},"sections":[{"id":"applicant","title":"Applicant","prompts":[{"id":"name","label":"Name","response":"Ada"}]}]}
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"}]},"proofs":[],"witnesses":[]}
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"fields","fields":["name"]},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"},{"path":"/sections/0/id","digest":"sha256:ddb020662a633640dd1d4d0dd981e920629b458a79fc1a1f083ed4a83a2b8a6e"},{"path":"/sections/0/prompts/0","digest":"sha256:ecb874b2ecb9667ab8ff21f6479c62ed5db164db6f059a87ec6fc3a100f5a94a"},{"path":"/sections/0/prompts/0/response","digest":"sha256:a39afeed7d3319213be7a235840b3c6d3f09f2810b9c91779f34572b6b36832a"},{"path":"/sections/0/title","digest":"sha256:290ba830f4a1f9820d0bfe1e494a084252c7dcb52b61d406f09d2fa51bed9f9a"}]},"proofs":[],"witnesses":["sha256:48f60a7124d41f89159e9d38003441a22755f09bc8744227dabde81a5bf8f1d0"]}
```

**Example 12.5-2.** A witness that is not a digest.

```apr-example
id: attestation-witness-malformed
rule: witnesses
violates: APR-ATTEST-042
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{
  "recordType": "attestation",
  "aprVersion": "1.0-beta.6",
  "subject": {
    "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "canonicalization": "jcs-sha256"
  },
  "scope": {
    "kind": "document"
  },
  "manifest": {
    "root": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675",
    "entries": [
      {
        "path": "",
        "digest": "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
      }
    ]
  },
  "proofs": [],
  "witnesses": [
    "the counter attestation"
  ]
}
```

**Example 12.5-3.** Witnesses repeating one digest.

```apr-example
id: attestation-witness-repeated
rule: witnesses
violates: APR-ATTEST-043
representation: jsonc
expect: reject
diagnostic: WRONG_TYPE
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"}]},"proofs":[],"witnesses":["sha256:48f60a7124d41f89159e9d38003441a22755f09bc8744227dabde81a5bf8f1d0","sha256:48f60a7124d41f89159e9d38003441a22755f09bc8744227dabde81a5bf8f1d0"]}
```

Witnessing neither authorizes a change nor proves a clock order, workflow
acceptance, real-world identity, or trusted time.

### 12.6 Changed forms {#changed-forms}

A changed form is another complete form occurrence with a different subject
digest.

A verifier **MUST NOT** transfer an attestation to a changed form. [APR-ATTEST-009]

The attestation remains an assertion about its original subject.

A reader **MUST** accept a stream holding several attestations of one
form. [APR-ATTEST-044]

A reader **MUST** accept an attestation that comes before its subject in a
stream. [APR-ATTEST-045]

**Example 12.6-1.** A form and two attestations of it.

```apr-example
id: attestations-several-of-one-form
rule: changed-forms
satisfies: APR-ATTEST-044
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","documentType":"template","metadata":{"title":"Beta 6 permit"},"sections":[{"id":"applicant","title":"Applicant","prompts":[{"id":"name","label":"Name","response":"Ada"}]}]}
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"}]},"proofs":[],"witnesses":[]}
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"fields","fields":["name"]},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"},{"path":"/sections/0/id","digest":"sha256:ddb020662a633640dd1d4d0dd981e920629b458a79fc1a1f083ed4a83a2b8a6e"},{"path":"/sections/0/prompts/0","digest":"sha256:ecb874b2ecb9667ab8ff21f6479c62ed5db164db6f059a87ec6fc3a100f5a94a"},{"path":"/sections/0/prompts/0/response","digest":"sha256:a39afeed7d3319213be7a235840b3c6d3f09f2810b9c91779f34572b6b36832a"},{"path":"/sections/0/title","digest":"sha256:290ba830f4a1f9820d0bfe1e494a084252c7dcb52b61d406f09d2fa51bed9f9a"}]},"proofs":[],"witnesses":[]}
```

**Example 12.6-2.** An attestation that comes before its subject.

```apr-example
id: attestation-before-subject
rule: changed-forms
satisfies: APR-ATTEST-045
representation: jsonc-stream
expect: valid
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675","entries":[{"path":"","digest":"sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"}]},"proofs":[],"witnesses":[]}
---
{"aprVersion":"1.0-beta.6","documentType":"template","metadata":{"title":"Beta 6 permit"},"sections":[{"id":"applicant","title":"Applicant","prompts":[{"id":"name","label":"Name","response":"Ada"}]}]}
```

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

Each row below is a requirement on a verifier: it reports the result when the
row's condition holds.

| Result | Condition | Requirement | Rule |
| --- | --- | --- | --- |
| `valid` | The subject resolved, digest and manifest match, and a recognized proof verifies. | **MUST** | [APR-ATTEST-046] |
| `invalid` | A recognized proof fails, or a resolved subject differs from the attested digest or manifest. | **MUST** | [APR-ATTEST-047] |
| `unresolved` | No matching form occurrence is available. | **MUST** | [APR-ATTEST-048] |
| `unverifiable` | Required representation, extension, digest, or proof support is unavailable. | **MUST** | [APR-ATTEST-049] |
| `witnessed` | One or more referenced envelopes resolve and match. | **MUST** | [APR-ATTEST-050] |

A verifier **MUST** report each result independently of the others, so an
attestation can be both `unverifiable` and `witnessed`. [APR-ATTEST-018]

A verifier **MUST NOT** report `unresolved` as a failure of the assertion. [APR-ATTEST-051]

**Validity is independent of trust.** A self-signed certificate can produce a
perfectly valid proof that proves nothing about identity.

A verifier **MUST** report whether a proof verifies separately from whether its
certificate is trusted. [APR-ATTEST-010]

> Rationale: collapsing content validity and certificate trust into one green
> checkmark teaches people to trust a checkmark that does not mean what they
> think.

### 12.8 Attestations never gate the data {#never-gate}

**An attestation is an assertion about a document, never a permission to read
it.** An implementation **MUST NOT** treat the presence, absence, or state of an
attestation as authorization to read, or to withhold, the data a form
carries. [APR-ATTEST-019]

**A form needs no attestation.** A form with no attestation is a complete,
ordinary, fully valid APR document.

An implementation **MUST NOT** require an attestation in order to save, send,
accept, or process a form. [APR-ATTEST-011]

A renderer **MUST NOT** present an unattested document as deficient. [APR-ATTEST-052]

**Nothing waits on an attestation.**

An implementation **MUST NOT** refuse to parse, validate, render, print, export,
or extract data from a document because its attestations are absent,
unrecognized, expired, untrusted, or outright invalid. [APR-ATTEST-012]

A validator **MUST NOT** report attestation state as an error. [APR-ATTEST-053]

**Example 12.8-1.** A form beside an attestation of a different form is valid.

```apr-example
id: attestation-state-no-error
rule: never-gate
satisfies: APR-ATTEST-053
representation: jsonc-stream
expect: valid
---
{"aprVersion":"1.0-beta.6","documentType":"template","metadata":{"title":"Beta 6 permit"},"sections":[{"id":"applicant","title":"Applicant","prompts":[{"id":"name","label":"Name","response":"Ada"}]}]}
---
{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:abababababababababababababababababababababababababababababababab","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:abababababababababababababababababababababababababababababababab","entries":[{"path":"","digest":"sha256:abababababababababababababababababababababababababababababababab"}]},"proofs":[],"witnesses":[]}
```

An implementation **MAY** warn, badge, or refuse to *act* on a document by its
own policy — a receiving workflow is entitled to reject an unattested permit
request. [APR-ATTEST-013]

That is the workflow's decision. It is not the file format's, and a
reader that enforces it on the workflow's behalf has taken a choice away from
every other consumer of the same document.

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

Each row below is a requirement on a renderer.

| Obligation | Requirement | Rule |
| --- | --- | --- |
| Present section titles and prompt labels as the accessible name. | **MUST** | [APR-RENDER-001] |
| Use a placeholder as the only label. | **MUST NOT** | [APR-RENDER-002] |
| Associate `helpText` programmatically with its prompt, not merely place it adjacent to it. | **MUST** | [APR-RENDER-003] |
| Convey section nesting structurally — heading levels, groups, landmarks — and not by indentation alone. | **MUST** | [APR-RENDER-004] |
| Make every prompt reachable by keyboard. | **MUST** | [APR-RENDER-005] |
| Accept a response to every prompt from the keyboard. | **MUST** | [APR-RENDER-014] |
| Block saving because of a hint mismatch. | **MUST NOT** | [APR-RENDER-006] |
| Present a table section with header association, not as a purely visual grid. | **SHOULD** | [APR-RENDER-007] |

These are format-level requirements, not house style. APR's structure is what
makes an accessible rendering possible; a renderer that discards it discards the
reason to use APR.

### 13.2 Ordering {#ordering}

Presentation is otherwise free, but **order is data**. A form asks its questions
in a sequence its author chose, and two renderers that disagree about that
sequence are showing two different forms.

Each row below is a requirement on a renderer.

| Order | Requirement | Rule |
| --- | --- | --- |
| Present sections in array order. | **MUST** | [APR-RENDER-010] |
| Present the prompts within a section in array order. | **MUST** | [APR-RENDER-011] |
| Present a section's own prompts before its child sections. | **MUST** | [APR-RENDER-012] |

A section's own prompts come first by document convention: a heading's own content
precedes its subheadings.

A renderer **MAY** paginate, group, or lazily load a form. [APR-RENDER-013]

A wizard that shows one section at a time still visits them in array order.

### 13.3 Export {#export}

A renderer **MAY** introduce layout — page size, margins, footers — into an
export to PDF, HTML, or print. [APR-RENDER-015]

That layout belongs to the renderer's options.

A renderer **MUST NOT** write export layout back into the APR document. [APR-RENDER-009]

The document stays presentation-free no matter how many ways it has been rendered.

---

## 14. Security considerations {#security}

**No executable content.** APR contains no scripts, macros, formulas with host
access, or external references. Opening an APR document from an untrusted sender
executes nothing, and this is the format's most important security property.

An implementation **MUST NOT** execute anything a document carries. [APR-SEC-009]

Expressions are pure, bounded, and non-Turing-complete; they are not an exception.

**No network access on open.** `submissionUrls` is data, and so is a certificate
chain.

A reader **MUST NOT** fetch anything when it reads a document. [APR-SEC-010]

An implementation **MUST NOT** contact a `submissionUrls` entry without an explicit
user action. [APR-SEC-016]

An implementation **MUST NOT** contact a certificate endpoint without an explicit
user action. [APR-SEC-017]

**Resource bounds.**

A reader **MUST** bound nesting depth. [APR-SEC-011]

A reader **SHOULD** bound document size and stream length. [APR-SEC-018]

A reader **MUST** refuse a document cleanly on reaching a bound it applies, rather
than exhausting memory or crashing. [APR-SEC-019]

A reader **MUST** terminate on every input. [APR-SEC-020]

Evaluation cost is bounded as [Bounds](#expr-limits) states.

Two of these bounds have floors this document states: sixteen levels of
nesting ([Nesting depth](#nesting)) and a one-mebibyte response
([Any string is a valid response](#any-string)). The others are deliberately the
implementation's to choose, because the right limit for a phone and for a batch
importer are not the same number, and a number here would be wrong for one of
them.

Concrete limits above those two floors are **implementation-defined**. No
numeric ceiling is specified because no test enforces one, and a limit that
nothing verifies is a limit implementations will disagree about.

**Deceptive text.** A filler's response is preserved and rendered defensively
instead of silently cleaned. Author-supplied members a machine acts on — above
all `metadata.submissionUrls` — are checked and refused at authoring time.
Spending strictness on the answer rather than on the submission target protects
nothing and destroys data.

**Attestations are not authorization.** A valid proof shows bytes are unaltered.
It does not show that the signer is who they claim, that they were entitled
to sign, or that the form is to be acted on. Conversely, an absent or failing
attestation is not a reason to withhold data from a reader — it is information
the reader is entitled to have alongside the data, not instead of it.

**What an attestation reveals.** A manifest reveals the *shape* of a form: its
pointers name every attested path. A `fields` attestation additionally reveals
which prompts were selected. Neither reveals response values. A certificate chain
in a proof carries the signer's identity in cleartext.

**Responses can be sensitive.** APR documents routinely hold personal data in
plain text. The format provides no encryption, no access control, and no
redaction; protection at rest and in transit is the surrounding system's
responsibility.

---

## 15. Open questions {#open-questions}

This section is informative. It lists what this specification does not settle.

1. **Media types not yet registered.** `application/vnd.apr+json`,
   `application/vnd.apr+yaml` and `application/vnd.apr+json-seq` are defined
   ([Document type](#media-types)) but the IANA vendor-tree registration has
   not been filed.
2. **No governance.** A format used by public institutions eventually needs
   stewardship that is not a single repository.

---

## 16. Normative references {#normative-references}

Compliance with this specification requires the editions below.

| Designation | Title |
| --- | --- |
| BCP 14 | Key words for use in RFCs (RFC 2119 and RFC 8174) |
| BCP 47 | Tags for Identifying Languages (RFC 5646) |
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

## 17. Informative references {#informative-references}

This section is informative. Nothing below is needed to implement this
specification.

| Designation | Title |
| --- | --- |
| CommonMark | A strongly defined, highly compatible specification of Markdown |
| W3C QA SpecGL | QA Framework: Specification Guidelines, W3C Recommendation, 17 August 2005 |

---

## 18. Appendix A: Minimal valid document {#appendix-minimal}

This appendix is informative.

**Example 18-1.** The smallest valid form: the three members a document
requires, one section and one prompt.

```apr-example
id: minimal-form
rule: root-object
satisfies: APR-MODEL-005, APR-MODEL-053, APR-MODEL-055
representation: jsonc
expect: valid
---
{
  "aprVersion": "1.0-beta.6",
  "metadata": { "title": "Contact" },
  "sections": [
    {
      "id": "contact",
      "title": "Contact",
      "prompts": [
        { "id": "full_name", "label": "Full name" }
      ]
    }
  ]
}
```

## 19. Appendix B: The rule to remember {#appendix-rule}

This appendix is informative.

If you implement nothing else correctly, implement this:

> **Any string is a valid response, and a hint never says otherwise.**

Everything else in APR is structure. That rule is the point, and
[Any string is a valid response](#any-string) states it.

## 20. Appendix C: What earns a primitive {#primitives}

This appendix is informative.

This appendix states the test a concept meets to earn its own record kind or
structural element, rather than being expressed with the ones this document
already defines.

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
structure that already exists rather than a new thing. Tables are common, and
commonness is not the test.

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

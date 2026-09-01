# APR File Format Specification {#apr-specification}

**Specification document version:** 1.0.0-beta.6-draft
**Describes format version:** `1.0-beta.6`
**Status:** BETA — breaking changes are intentional until the first public release
**Normative schema:** `schemas/apr-1.0-beta.6.schema.json`
**Conformance corpus:** `tests/Conformance/beta6/`

This document has not been ratified. Nothing in it designates APR 1.0, and a
beta baseline remains revisable until an explicit human decision says otherwise.

## 1. Scope {#scope}

APR is a local-first, semantic form format. It stores what a form asks and the
string responses it receives. It stores no page layout and executes no
document-supplied code.

This document specifies the `1.0-beta.6` format: its abstract model, its JSONC
and YAML representations, record streams, semantic digests, attestations,
verification reporting, and conformance profiles.

Out of scope: page layout and pagination, hosted collaboration, submission
transport beyond the record contract defined here, and any behavior an
application layers on top of an APR document.

## 2. Normative language {#normative-language}

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**,
**SHOULD**, **SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** are to be
interpreted as described in BCP 14 (RFC 2119, RFC 8174) when, and only when,
they appear in all capitals.

Lowercase uses of these words carry their ordinary English meaning and impose no
requirement.

## 3. Document conventions {#conventions}

Four kinds of text appear in this document and are distinguished deliberately.

- **Normative text** states requirements. It is ordinary prose using the
  keywords of [§2](#normative-language).
- **Examples** appear in fenced code blocks introduced as examples. An example
  is illustrative. Where an example and normative text disagree, the normative
  text governs.
- **Rationale** appears in blockquotes beginning `Rationale:`. Rationale
  explains why a rule exists and is non-normative. Removing every rationale
  block would not change the format.
- **Decisions** appear in blockquotes beginning `Decision (beta.6):`. A decision
  block marks a rule that this document originates rather than transcribes from
  the schema or the conformance corpus. Decision blocks are normative, and they
  are marked so that every non-transcribed rule can be found and reviewed.

Each heading carries an explicit anchor, written `{#anchor-name}`.

> Rationale: **anchors, not section numbers, are the stable identifiers.**
> Section numbers renumber whenever material is inserted. A coverage manifest,
> a drift test, or an external citation that referenced `§6.2` would silently
> come to mean something else; one that references `#form-model` either
> resolves or fails loudly. Cite anchors.

## 4. Authority and precedence {#authority}

APR authority is, in descending order:

1. the beta.6 conformance corpus, `tests/Conformance/beta6/`;
2. the beta.6 JSON Schema, `schemas/apr-1.0-beta.6.schema.json`; and
3. this specification.

Where they disagree, the higher authority governs and the disagreement is a
defect to be reported rather than resolved by a reader's judgment.

This document is the declared prose authority for the `1.0-beta.6` baseline.

> Rationale: the corpus outranks the prose because a conformance claim is
> settled by executing vectors, not by reading. An implementation that passes
> the corpus and contradicts a sentence here has found a specification bug.

### 4.1 Relationship to the schema's own prose {#schema-prose}

The beta.6 schema layers its constraints over a shared structural base,
`schemas/apr-1.0.schema.json`. That base carries `description` text written for
an earlier beta. Its **structure** is correctly constrained for beta.6 by the
overriding layer; several of its **descriptions** are stale and describe retired
behavior — an accepted version of `1.0-beta`, a `core+signatures` profile, and
the retired `signature` and `signer` definitions.

Where a base-schema description and this document disagree, this document
governs for beta.6 semantics, and the stale description is a defect to be
corrected in the schema rather than a competing rule.

## 5. The beta.6 boundary {#beta6-boundary}

Beta.6 replaces beta.3. There is no public compatibility commitment: a beta.6
reader **MUST** reject a beta.3 form rather than silently treating it as
beta.6.

- `version` **MUST** be exactly `"1.0-beta.6"`.
- `signatures` and `apr-sig-v3` are retired. An APR form is ordinary form data;
  cryptographic assertions live in independent attestation records
  ([§11](#attestations)).
- A form has no `signatures` member. A reader **MUST** report it as
  `RETIRED_EMBEDDED_SIGNATURES`. It is not an extension member.

## 6. Abstract model and processing {#abstract-model}

### 6.1 Model, serialization, presentation {#model-layers}

APR separates three layers. A rule stated at one layer does not constrain
another.

1. **Semantic model** — the information APR defines: a form's identity,
   metadata, section tree, prompts, advisory hints, and string responses; or an
   attestation's subject, scope, manifest, proofs, and witnesses. All APR
   semantics are properties of this layer.
2. **Serialization** — the JSON data model the semantic model maps onto:
   objects, arrays, strings, numbers, booleans, and null.
3. **Presentation** — the bytes actually written: APR-JSONC or APR-YAML spelling,
   comments, whitespace, indentation, quoting style, and mapping order.

Two documents with the same semantic model are the same form, whatever their
presentation. This is what makes a semantic digest ([§10](#digests)) meaningful:
it is computed over the model, never over the bytes.

Information is discarded deliberately when moving from presentation to model.
Comments, whitespace, key order, scalar spelling, and framing are **source
trivia**: they **MUST NOT** carry APR meaning, and a writer is under no
obligation to reproduce them. Everything else — including members APR does not
define ([§7.14](#extensions)) — **MUST** survive a round trip.

### 6.2 Processing roles {#processing-roles}

An implementation may take any of these roles. Each is defined by its
obligations, not by an API shape.

| Role | Obligation |
| --- | --- |
| **Reader** | Decode one document from a representation to the semantic model, or reject it. **MUST** reject a document whose `version` is not `1.0-beta.6` ([§5](#beta6-boundary)). |
| **Writer** | Encode a semantic model to a representation. **MUST NOT** emit `null` for a response ([§7.10](#responses)), and **MUST** preserve extension members. |
| **Stream reader** | Yield every record of a stream in order ([§9](#streams)). **MUST NOT** deduplicate, reorder, or select by position. |
| **Validator** | Report whether a document satisfies this specification, including the constraints of [§7.9](#unexpressible) that no schema can state. |
| **Renderer** | Present a form to a person under the obligations of [§7.17](#renderers). |
| **Attestation producer** | Compute a subject digest and manifest and emit an attestation record ([§11](#attestations)). |
| **Verifier** | Resolve an attestation against observed forms and report the vocabulary of [§12](#verification). **MUST NOT** conflate cryptographic validity with trust. |
| **Application** | Anything layered above: workflow, storage, transport. Outside this specification. |

A single implementation commonly takes several roles. A conformance profile
([§13](#conformance)) names which roles it exercises.

### 6.3 Failure points {#failure-points}

A document can fail at distinct stages, and the stages are reported
distinguishably:

- **presentation failure** — the bytes are not well-formed in the declared
  representation ([§8](#representations));
- **serialization failure** — a forbidden construct resolved to something
  outside the JSON data model ([§8.2](#apr-yaml));
- **model failure** — the JSON is well-formed but violates a rule of
  [§7](#form-model); and
- **assertion failure** — the model is valid but an attestation over it does not
  verify ([§12](#verification)). An assertion failure **MUST NOT** prevent
  access to the form.

## 7. Form model {#form-model}

A form is a JSON object. This section catalogues every member APR defines.

Throughout: *required* means the member **MUST** be present; *optional* means it
**MAY** be absent, and absence carries the stated default. Unless a member's
entry says otherwise, its value is a JSON string.

### 7.1 Root object {#root-object}

| Member | Required | Type | Domain and default |
| --- | --- | --- | --- |
| `version` | required | string | **MUST** be exactly `"1.0-beta.6"`. |
| `documentType` | optional | string | `template` or `filledForm`. Absent means `template`. |
| `metadata` | required | object | [§7.2](#metadata) |
| `sections` | required | array | At least one [section](#section-object). |
| `roles` | optional | array | Zero or more [role definitions](#role-object). |

`documentType` — **not** the filename extension — determines how a document is
treated. A `filledForm` **MUST** declare `metadata.templateId`.

A form **MUST NOT** carry `signatures` ([§5](#beta6-boundary)).

### 7.2 `metadata` {#metadata}

| Member | Required | Type | Notes |
| --- | --- | --- | --- |
| `title` | required | non-blank string | The form's name. |
| `description` | optional | string | Prose about the form as a whole. |
| `created` | optional | date-time | RFC 3339 / ISO 8601. |
| `modified` | optional | date-time | |
| `author` | optional | string | A person. |
| `publisher` | optional | string | The organization standing behind the template. |
| `templateId` | optional | string | Required on a `filledForm`; identifies the template it answers. |
| `templateVersion` | optional | string | The template revision answered. |
| `filledBy` | optional | string | Who supplied the responses. |
| `filledDate` | optional | date-time | |
| `submissionUrls` | optional | array of string | Ordered explicit delivery choices for a completed form. |

A *non-blank string* contains at least one non-whitespace character.
Whitespace-only is treated as absent.

`submissionUrls` is data an application **MAY** offer as a destination. Reading a
form **MUST NOT** contact one ([§7.16](#offline)). APR defines no submission
protocol; a delivery action is an application concern.

### 7.3 `section` {#section-object}

| Member | Required | Type | Domain and default |
| --- | --- | --- | --- |
| `id` | required | non-blank string | Unique document-wide among sections. |
| `title` | required | non-blank string | Carries the document outline. |
| `description` | optional | string | Prose shown with the section. |
| `sections` | optional | array | Child sections; recursive. |
| `prompts` | optional | array | Child [prompts](#prompt-object). |
| `kind` | optional | string | `section` or `table`. Absent means `section`. |
| `canAddRows` | optional | string | Truthy if a filler may add or remove table instances. Absent means instances are fixed. |
| `maxRows` | optional | string | Advisory upper bound on instance count. |
| `role` | optional | string | Open vocabulary; see [§7.13](#roles). |

A section **MUST** have at least one prompt or at least one child section. This
holds for `table` sections as well: a table declares its shape with at least one
instance, even where `canAddRows` permits a filler to add more.

> Rationale: prose in an earlier baseline stated an exception admitting an empty
> dynamic table. The schema admits no such exception, and no shipped example or
> corpus fixture exercises one — every `kind: table` section carries at least one
> child section defining the shape. [§4](#authority) makes the schema govern, so
> the exception is removed rather than the constraint relaxed.

A section `title` is required and never optional.

> Rationale: titles carry the document outline that assistive technology
> navigates by. An untitled section is invisible to a screen-reader user moving
> by heading.

### 7.4 `prompt` {#prompt-object}

| Member | Required | Type | Domain and default |
| --- | --- | --- | --- |
| `id` | required | non-blank string | Unique document-wide among prompts. |
| `label` | required | non-blank string | The accessible name. |
| `response` | optional | string | See [§7.10](#responses). Absent means the empty string. |
| `hints` | optional | object | [§7.5](#hints-object). Every member advisory. |
| `responseMetadata` | optional | object | [§7.6](#response-metadata). |
| `role` | optional | string | Overrides the containing section's role. |

A prompt `label` is required, never optional, and **MUST NOT** be substituted by
placeholder text.

### 7.5 `hints` {#hints-object}

Every member is **ADVISORY** ([§7.11](#hints-advisory)).

| Member | Type | Meaning |
| --- | --- | --- |
| `placeholder` | string | Text shown in an empty control. Never a substitute for `label`. |
| `expectedDataType` | string | Suggested input affordance. Open registry; see below. |
| `suggestedValues` | array of string | Offered as autocomplete or menu options. A response outside the list is still valid. |
| `helpText` | string | Explanatory text for the prompt. |
| `validationPattern` | string | Advisory regular expression. A non-matching response is still a valid document. |
| `min` | string | Suggested lower bound for an ordered field. |
| `max` | string | Suggested upper bound for an ordered field. |
| `step` | string | Suggested increment for an ordered field. |
| `exprHidden` | string | CEL. Truthy hides this prompt. |
| `exprValue` | string | CEL. Computed read-only value. |
| `exprExpected` | string | CEL. Truthy marks the prompt as expected. |
| `exprValidation` | string | CEL. Returns a message; empty string means valid. |
| `exprReadOnly` | string | CEL. Truthy makes this prompt read-only in a renderer. |

`expectedDataType` values are a **registry, not a closed set**. These are the
registered values:

`text`, `multiline`, `email`, `phone`, `url`, `date`, `time`, `datetime`,
`number`, `currency`, `boolean`, `signature`, `file`, `select`, `multichoice`,
`password`, `range`, `color`.

An unrecognized value **MUST** degrade to `text` rather than raise an error.

The five `expr*` members belong to the `core+expressions` profile. Their
language is CEL, restricted to pure, bounded, side-effect-free evaluation; the
binding is specified separately in `docs/drafts/APR_CEL_PROFILE_1.md` and is a
deliberate draft, not part of this baseline's normative core. An implementation
without expression support **MUST** preserve these members and ignore them.

### 7.6 `responseMetadata` {#response-metadata}

| Member | Type | Meaning |
| --- | --- | --- |
| `inferredDataType` | string | What a reader detected in the response. Never authoritative, never a constraint. |
| `lastModified` | date-time | When the response last changed. |
| `source` | string | `computed` is the only defined value. Present when an `exprValue` produced the response; absent when a person or an API wrote it. |

Every member is advisory. A reader that ignores `responseMetadata` entirely
still holds a valid document.

> Rationale: `source` exists so recomputation can tell a stale computed value
> from an answer someone typed. Without it, correcting a computed field would
> silently revert on the next recompute.

### 7.7 `roleDefinition` {#role-object}

| Member | Required | Type | Meaning |
| --- | --- | --- | --- |
| `id` | required | string | The identifier that section and prompt `role` members reference. |
| `name` | optional | string | The name to show a person. Falls back to `id`. |
| `description` | optional | string | Prose about the party. |

Declaring a role is optional and the vocabulary is open: a section or prompt
**MAY** reference a role that is not declared here, and a reader shows the
identifier.

### 7.8 Attestation record members {#attestation-members}

An attestation is a stream record, not a form member. Its catalogue is in
[§11.1](#attestation-catalogue).

### 7.9 Constraints no schema expresses {#unexpressible}

A conforming validator **MUST** enforce these rules, which JSON Schema cannot
state:

1. **Section ids are unique document-wide.**
2. **Prompt ids are unique document-wide.** A section id and a prompt id **MAY**
   coincide; they are separate namespaces.
3. **Attestation assertions verify as specified** ([§12](#verification)). In
   beta.3 this rule concerned embedded canonical signature payloads; those are
   retired, and the beta.6 obligation is digest and manifest agreement plus
   proof verification.
4. **Within a `table` section, prompts at the same position across instances
   correspond.** A ragged or mislabelled table is **advisory, never invalid**: a
   validator reports it as a warning and the document remains conformant.

The schema also deliberately declines to express the advisory rules of
[§7.11](#hints-advisory). No hint — `validationPattern` and the `expr*` family
included — ever constrains `response`. **Any JSON string is a valid response.**

### 7.10 Responses are strings {#responses}

A response **MUST** be a JSON string, in templates and filled forms alike. It is
never a JSON number, boolean, array, or object; those **MUST** be rejected at
parse time.

An absent response is read as the empty string. JSON `null` is tolerated on read
and coerced to the empty string, but a conforming writer **MUST NOT** emit it.

> Rationale: `null` is permitted on read only so that lenient inbound documents
> validate rather than failing at the door. The schema admits
> `["string","null"]` for exactly this reason and for no other.

### 7.11 Hints are advisory {#hints-advisory}

A hint **MUST NOT** cause a response to be rejected, altered, or blocked from
being saved. This holds for every member of [§7.5](#hints-object) without
exception.

A response outside `suggestedValues`, outside `min`/`max`, or not matching
`validationPattern` is a valid document. A reader surfaces the divergence as a
warning, never as a block.

### 7.12 Structural tables {#tables}

A section with `kind` of `table` declares that its child sections are repeating
instances of one shape: prompts at the same position correspond across
instances, each instance's title identifies it, and a prompt's label names the
corresponding field.

This is a claim about **structure, not appearance**. A renderer may present it
as a grid, as stacked cards, as a flat sequence of prompts, or as speech, and
all are conformant. It licenses no layout data of any kind.

A table **MUST** carry at least one instance, which establishes the shape its
other instances repeat. `canAddRows` states whether a filler may add or remove
instances; absent, instances are fixed. `maxRows` is advisory in the sense of
[§7.11](#hints-advisory): a table carrying more is still valid and is reported
as a warning.

> Rationale: fixed-by-default is the safe direction. A table that silently
> gained a row is a worse failure than one that needed an explicit property.

### 7.13 Roles {#roles}

A role states who is meant to fill something in — `patient`, `nurse`, `office`.
The vocabulary is open and a role is a statement of intent, never enforcement: a
reader marks the field and still accepts any input. A prompt's role overrides
the role of the section containing it.

### 7.14 Extension members {#extensions}

Unknown members are semantic extension data. They **MUST** round-trip and are
included in whole-document beta.6 digests ([§10](#digests)). Retired
presentation members remain forbidden, and `signatures` is retired rather than
unknown ([§5](#beta6-boundary)).

### 7.15 Reserved for future use {#reserved}

No member name is reserved. A future baseline may define a member that a
document is already carrying as an extension; that collision is resolved by the
version boundary ([§14](#compatibility)), not by reservation.

### 7.16 Offline reading {#offline}

Core form reading is offline and safe. Opening a form or an attestation
**MUST NOT** contact a `metadata.submissionUrls` entry, a certificate endpoint,
or any other network location.

### 7.17 Renderer obligations {#renderers}

A renderer **MUST** preserve semantic order, **MUST** use labels as accessible
names, and **MUST** allow complete keyboard operation.

A renderer **MUST NOT** use `placeholder` as a prompt's accessible name, and
**MUST NOT** treat any hint as a constraint on input
([§7.11](#hints-advisory)).

## 8. Representations {#representations}

The semantic model is representation-neutral. The same form or attestation may
be written as APR-JSONC or APR-YAML.

A document **MUST** be encoded as UTF-8. A reader **SHOULD** tolerate a leading
byte-order mark and **MUST NOT** silently mis-decode a document that is not
UTF-8; such input is a presentation failure ([§6.3](#failure-points)).

### 8.1 APR-JSONC {#apr-jsonc}

APR-JSONC is JSON (RFC 8259) extended with exactly three constructs:

- line comments introduced by `//`, running to end of line;
- block comments delimited by `/*` and `*/`; and
- a trailing comma after the final member of an object or element of an array.

Once comments and trailing commas are removed the text **MUST** decode as JSON
to the semantic model. Comments are source trivia
([§6.1](#model-layers)) and cannot carry APR meaning.

A JSONC parser **MUST** reject duplicate object keys rather than applying a
last-key-wins rule.

*Negative case:* `malformed/duplicate-member.apr.jsonc`.

> Rationale: last-key-wins makes the meaning of a document depend on which
> parser reads it, which is precisely what a semantic digest cannot tolerate.

### 8.2 APR-YAML {#apr-yaml}

APR-YAML is a restricted YAML 1.2 representation of the same JSON model.

Keys **MUST** be strings. These constructs are forbidden and **MUST** be
rejected:

| Forbidden | Corpus case |
| --- | --- |
| Anchors and aliases (`&`, `*`) | `malformed/yaml-anchor.apr.yaml` |
| Tags (`!!str`, `!Custom`) | none — corpus gap |
| Merge keys (`<<`) | none — corpus gap |
| Non-finite numbers (`.inf`, `.nan`) | none — corpus gap |
| Binary values | none — corpus gap |
| Dates resolved by implicit typing | none — corpus gap |
| Arbitrary-language constructors | none — corpus gap |

Implementations **MUST** use a safe YAML loader and **MUST** resolve scalars only
to JSON null, boolean, number, string, array, or object before APR validation.

Responses remain strings even where a YAML scalar could otherwise resolve as a
number or boolean: `response: 42` and `response: true` carry the string values
`"42"` and `"true"`.

Rows above marked *corpus gap* state a rule the corpus does not yet exercise.
The rule is normative; the missing vector is a corpus defect.

## 9. Streams {#streams}

A stream is an ordered transport of independent records. Physical order is
presentation only: it creates no subject, revision, chronology, or trust
relationship.

Each record is exactly one of:

- a complete standalone APR form; or
- an APR attestation (`recordType: "attestation"`).

A stream **MUST NOT** mix representations. It **MUST NOT** deduplicate repeated
form occurrences, even when their semantic digests are identical. A single-form
API given a stream **MUST** return `APR_STREAM_REQUIRES_ITERATION` and
**MUST NOT** select a record by position. A streaming API yields every record
and may hold an unresolved attestation until its subject form has been
observed.

### 9.1 JSONC framing {#jsonc-framing}

APR-JSONC streams use RFC 7464 framing: every record is prefixed by ASCII Record
Separator (`0x1E`) and terminated by LF (`0x0A`). A comment is confined to its
one JSONC record.

A record not preceded by `0x1E` is a framing failure.

*Negative case:* `malformed/missing-record-separator.apr.jsonc`.

### 9.2 YAML framing {#yaml-framing}

APR-YAML streams use YAML document markers: every document introduced by `---`
is exactly one record.

### 9.3 Equivalence {#stream-equivalence}

The corpus supplies paired streams whose records have equal semantic models
across the two representations. A stream reader **MUST** produce the same
sequence of semantic records from either member of such a pair.

## 10. Semantic digests and manifests {#digests}

`jcs-sha256` is the beta.6 semantic digest algorithm. Its input is RFC 8785 JCS
serialization of the fully parsed JSON semantic model, encoded as UTF-8; its
value is lowercase hexadecimal SHA-256 prefixed with `sha256:`. Source syntax is
never hashed.

A digest value **MUST** match `^sha256:[0-9a-f]{64}$`.

A form digest includes every APR-defined member and every unknown extension
member that survived parsing. It excludes only representation trivia. A verifier
that cannot preserve or digest an extension member **MUST** report the assertion
as `unverifiable`, not valid.

> Rationale: including extensions prevents a whole-form attestation from
> silently omitting a meaningful member.

An integrity manifest does not duplicate plaintext. It contains `root`, the form
digest, and sorted `entries`; each entry has a JSON Pointer `path` and a digest
of the JCS encoding of that path's value. Pointers are RFC 6901 pointers into
the form semantic model. `entries` includes `""`, the root pointer, and every
defined semantic leaf; whole-document manifests also include extension members.

A verifier can compare entries to explain which values differ without the
manifest retaining their old values.

## 11. Attestations {#attestations}

An attestation is a stream record.

```jsonc
// Example — illustrative, not normative.
{
  "recordType": "attestation",
  "version": "1.0-beta.6",
  "subject": { "digest": "sha256:...", "canonicalization": "jcs-sha256" },
  "scope": { "kind": "document" },
  "manifest": { "root": "sha256:...", "entries": [] },
  "proofs": [],
  "witnesses": []
}
```

### 11.1 Attestation catalogue {#attestation-catalogue}

| Member | Required | Type | Domain |
| --- | --- | --- | --- |
| `recordType` | required | string | **MUST** be `"attestation"`. |
| `version` | required | string | **MUST** be `"1.0-beta.6"`. |
| `subject` | required | object | `digest` and `canonicalization`, no other members. |
| `subject.digest` | required | string | `sha256:` + 64 lowercase hex. |
| `subject.canonicalization` | required | string | **MUST** be `"jcs-sha256"`. |
| `scope` | required | object | `document` or `fields` form; see below. |
| `manifest` | required | object | `root` and `entries`, no other members. |
| `manifest.root` | required | string | Digest of the subject form. |
| `manifest.entries` | required | array | Entries of `path` and `digest`, no other members. |
| `proofs` | required | array | Entries of `type` and `value`, no other members. May be empty. |
| `witnesses` | required | array | Unique digests of earlier attestation envelopes. May be empty. |

Unlike `subject`, `scope`, `manifest`, and their entries — which admit no
additional members — an attestation record itself **MAY** carry extension
members, which round-trip under [§7.14](#extensions).

A `document` scope object carries only `kind`. A `fields` scope carries `kind`
and `fields`, a non-empty array of unique non-blank prompt ids.

`subject.digest` identifies the complete form semantic model, never a stream
position, filename, or document id.

A `fields` manifest **MUST** include each selected prompt, its response and
hints, and every ancestor section's id, title, description, kind, and role. A
fields assertion therefore attests to both what was answered and the question
and context presented.

### 11.2 Proofs {#proofs}

`proofs` are assertions over the JCS serialization of the attestation envelope
after omitting `proofs` themselves.

Beta.6 defines one proof type: `cms/ecdsa-p256-sha256`, whose `value` is base64
CMS SignedData with the certificate chain included.

A proof **MUST NOT** invent a second copy of the subject digest or scope.
Unsupported proof types are `unverifiable`, not invalid; a reader without
support for a proof type **MUST** preserve it.

> Rationale: a second copy of the subject is what once let a redirected
> destination still verify.

### 11.3 Witnesses {#witnesses}

`witnesses` is an ordered, duplicate-free list of semantic digests of earlier
attestation envelopes, again excluding `proofs`. It says only that this
attestation's signer explicitly witnessed those assertions.

Witnessing neither authorizes a change nor proves a clock order, workflow
acceptance, real-world identity, or trusted time.

### 11.4 Changed forms {#changed-forms}

A changed form is another complete form occurrence with a different subject
digest. Earlier attestations remain assertions about their original subject and
do not transfer to the changed form.

Multiple attestations may target one unchanged form, and an attestation may be
encountered before its subject.

## 12. Verification and safety {#verification}

Verification reports these independent facts:

| Result | Meaning |
| --- | --- |
| `valid` | The subject resolved, semantic digest and manifest match, and a recognized proof verifies. |
| `invalid` | A recognized proof fails, or a resolved subject differs from the attested digest or manifest. |
| `unresolved` | No matching form occurrence is available. |
| `unverifiable` | Required representation, extension, digest, or proof support is unavailable. |
| `witnessed` | One or more referenced attestation envelopes resolve and match. |

These are independent: an attestation may be both `unverifiable` and
`witnessed`, and `unresolved` is not a failure of the assertion.

Trust is separate from cryptographic validity. A valid self-signed proof is not
a trusted identity; trust policy is supplied by the caller and is not part of
this format.

Attestation status **MUST NOT** gate parsing, validation, rendering, export, or
data extraction. An unsigned form is complete APR data.

Verification is a side-effect-free computation over data already in hand. A form
carrying attestations is no less safe to open than one without.

## 13. Conformance {#conformance}

| Profile | Requirement |
| --- | --- |
| `core` | Parse and write one beta.6 form in both representations. |
| `core+streams` | `core`, plus stream iteration ([§9](#streams)). |
| `core+attestations` | `core+streams`, plus semantic digests, manifests, attestation resolution, witness lookup, and the vocabulary of [§12](#verification). |
| `core+expressions` | `core`, plus evaluation of the `expr*` hints ([§7.5](#hints-object)). Independent of the stream and attestation profiles. |

An implementation may claim a profile only with the exact beta.6 corpus revision
it passes.

The beta.6 corpus covers paired JSONC/YAML forms and streams, duplicate and
out-of-order records, malformed framing, single-form API rejection, digest and
manifest vectors, document and fields scopes, CMS proof inputs, unsupported
proofs, witness chains, and changed copied forms.

## 14. Compatibility and extensibility {#compatibility}

### 14.1 Within beta.6 {#forward-compatibility}

Forward compatibility rests on [§7.14](#extensions): unknown members are
extension data, **MUST** round-trip, and participate in whole-document digests.

An implementation encountering an unknown `expectedDataType`
([§7.5](#hints-object)) or an unknown `role` ([§7.13](#roles)) **MUST** degrade
gracefully rather than reject the document. An unknown proof type is
`unverifiable` rather than invalid ([§11.2](#proofs)).

### 14.2 Across versions {#version-compatibility}

Beta.6 makes no compatibility commitment. `version` **MUST** be exactly
`"1.0-beta.6"`, and a document declaring any other version — including
`1.0-beta.3` and any later beta — **MUST** be rejected.

> Decision (beta.6): version handling is **exact-match rejection**, not
> MAJOR.MINOR negotiation. The shared base schema's `version` description still
> describes a MAJOR.MINOR compatibility rule under which a newer MINOR is
> readable; that rule does not apply to beta.6 and the description is stale
> ([§4.1](#schema-prose)). Negotiation is deferred until the first public
> release, when there is something to negotiate with.

### 14.3 Extension governance {#extension-governance}

> Decision (beta.6): extensions are **unregistered and uncoordinated** during
> beta. Anyone may add an extension member; nothing reserves a name, and no
> registry mediates collisions ([§7.15](#reserved)). Two vendors choosing the
> same member name produce documents that round-trip correctly and mean
> different things, and this baseline accepts that risk rather than standing up
> governance for a format with no public release.
>
> A producer **SHOULD** therefore name extension members distinctively — a
> reverse-DNS or vendor prefix — to make an accidental collision unlikely.
> This is a recommendation, not a constraint a validator enforces.

## 15. Security and privacy {#security}

### 15.1 Properties beta.6 guarantees {#security-guarantees}

- Opening a document executes no document-supplied code ([§1](#scope)).
- Opening a document performs no network access ([§7.16](#offline)).
- Verification is side-effect-free ([§12](#verification)).
- Attestation status never gates access to form data ([§12](#verification)).
- A valid proof is not a trusted identity ([§12](#verification)).
- A manifest does not retain the plaintext it attests to ([§10](#digests)).

### 15.2 Properties beta.6 does not provide {#security-non-guarantees}

APR defines no encryption, no access control, and no redaction. A form carries
whatever a person typed into it and is exactly as sensitive as its responses; it
is protected by the storage and transport around it, not by the format.

An attestation's manifest reveals the *shape* of a form — its pointers name
every attested path — and a `fields` attestation additionally reveals which
prompts were selected. It reveals no response values.

A certificate chain in a CMS proof carries the signer's identity in cleartext.

### 15.3 Resource limits {#resource-limits}

A malformed or hostile document **MUST NOT** be able to hang a reader. Parsing
**MUST** terminate.

> Decision (beta.6): concrete limits are **implementation-defined**, with one
> floor. A reader **MUST** support at least 16 levels of section nesting; the
> corpus pins this. Beyond that floor an implementation chooses its own ceilings
> for document size, nesting depth, stream length, and evaluation budget, and
> **MUST** report reaching one as a clean refusal rather than a crash, a hang, or
> a silent truncation.
>
> No numeric ceiling is specified because no test enforces one, and a limit that
> nothing verifies is a limit implementations will disagree about.

## 16. Media types and file extensions {#media-types}

> Decision (beta.6): APR registers **no media type**. Implementations
> **MUST NOT** rely on a media type to determine that a document is APR, nor to
> select a representation or profile. Registration is deferred to the first public
> release; minting a provisional type during beta would leave a name in
> circulation that the ratified format may not honor.

Representation is determined by content, not by name: a document beginning with
`0x1E` framing or parsing as JSONC is APR-JSONC; one parsing as restricted YAML
is APR-YAML. `documentType` ([§7.1](#root-object)) is authoritative over any
filename.

These extensions are **conventional**, not normative:

| Extension | Convention |
| --- | --- |
| `.aprt` | A template in JSON spelling. |
| `.aprf` | A filled form in JSON spelling. |
| `.apr.jsonc` | An APR-JSONC form or stream. |
| `.apr.yaml` | An APR-YAML form or stream. |

A reader **MUST NOT** reject a document because its extension disagrees with its
content, and **MUST NOT** infer `documentType` from an extension.

## 17. Change history {#history}

| Format version | Change |
| --- | --- |
| `1.0-beta.6` | Retired embedded `signatures` and `apr-sig-v3` in favor of independent attestation records. Added APR-JSONC and APR-YAML representations, representation-neutral record streams, `jcs-sha256` semantic digests, integrity manifests, and the verification vocabulary. |
| `1.0-beta.3` | Superseded. Not a compatibility target. |

## 18. Corpus gaps {#corpus-gaps}

Non-normative editor's note.

Every rule in this document is normative. These rules are not yet exercised by a
conformance vector, which makes them a corpus defect rather than a
specification gap:

- the forbidden APR-YAML constructs other than anchors ([§8.2](#apr-yaml)):
  tags, merge keys, non-finite numbers, binary values, implicit dates, and
  language constructors;
- mixed-representation stream rejection ([§9](#streams)); and
- manifest vectors across the full range of changed member kinds
  ([§10](#digests)).

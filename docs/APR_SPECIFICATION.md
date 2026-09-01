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

Three kinds of text appear in this document and are distinguished deliberately.

- **Normative text** states requirements. It is ordinary prose using the
  keywords of [§2](#normative-language).
- **Examples** appear in fenced code blocks introduced as examples. An example
  is illustrative. Where an example and normative text disagree, the normative
  text governs.
- **Rationale** appears in blockquotes beginning `Rationale:`. Rationale
  explains why a rule exists and is non-normative. Removing every rationale
  block would not change the format.

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
It does not restate the schema's structural constraints member by member; it
states the semantics those constraints exist to serve.

> Rationale: the corpus outranks the prose because a conformance claim is
> settled by executing vectors, not by reading. An implementation that passes
> the corpus and contradicts a sentence here has found a specification bug.

### 4.1 Relationship to the schema's own prose {#schema-prose}

The beta.6 schema layers its constraints over a shared structural base,
`schemas/apr-1.0.schema.json`. That base carries `description` text written for
an earlier beta. Its **structure** is correctly constrained for beta.6 by the
overriding layer; several of its **descriptions** are stale and describe
retired behavior.

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
  ([§10](#attestations)).
- A form has no `signatures` member. A reader **MUST** report it as
  `RETIRED_EMBEDDED_SIGNATURES`. It is not an extension member.

## 6. Form model {#form-model}

A form is a JSON object. Its members are `version`, `documentType`, `metadata`,
`sections`, and `roles`.

- `sections` holds an ordered list of sections. A document **MUST** have at
  least one section.
- A section carries `id`, `title`, and optionally `description`, `role`, `kind`,
  `canAddRows`, `maxRows`, and its own child `sections` and `prompts`. A section
  has at least one prompt or child section, except an explicitly dynamic table.
- A prompt carries `id`, `label`, and optionally `response`, `hints`,
  `responseMetadata`, and `role`.
- `hints` is an object whose every member is advisory ([§6.3](#hints)).
- `roles` declares the parties expected to fill parts of the form. Declaring a
  role is optional and the vocabulary is open: a section or prompt may reference
  a role that is not declared, and a reader shows the identifier.

The member-by-member catalogue — cardinality, requiredness, defaulting, allowed
value domains, and cross-field constraints for every member above — is pinned
today by `schemas/apr-1.0-beta.6.schema.json` and the beta.6 corpus. Its
normative prose is pending; see [§17](#pending).

### 6.1 Identity and required text {#identity}

- `documentType` is `template` or `filledForm`. This member, **not** the
  filename extension, determines how a document is treated. Absent means
  `template`.
- A filled form **MUST** declare `metadata.templateId`.
- `metadata.title`, every section `id` and `title`, and every prompt `id` and
  `label` are required non-blank strings. A non-blank string contains at least
  one non-whitespace character; whitespace-only is treated as absent.
- Section ids and prompt ids are each document-wide unique namespaces. A section
  id and a prompt id may be identical.

A section `title` is required and never optional: titles carry the document
outline that assistive technology navigates by. A prompt `label` is required,
never optional, and never substituted by placeholder text: it is the accessible
name.

### 6.2 Responses are strings {#responses}

A response **MUST** be a JSON string, in templates and filled forms alike. It is
never a JSON number, boolean, array, or object; those **MUST** be rejected at
parse time.

An absent response is read as the empty string. JSON `null` is tolerated on read
and coerced to the empty string, but a conforming writer **MUST NOT** emit it.

> Rationale: `null` is permitted on read only so that lenient inbound documents
> validate rather than failing at the door.

### 6.3 Hints are advisory {#hints}

Every member of a `hints` object is **ADVISORY**. A hint **MUST NOT** cause a
response to be rejected, altered, or blocked from being saved.

This applies without exception to every hint the schema defines, including the
suggested input affordance, offered values, advisory patterns, and suggested
bounds. A response outside a suggested list or bound, or not matching an
advisory pattern, is still a valid document; a reader surfaces it as a warning,
never as a block.

An affordance hint names a suggested input control from an open registry. An
unrecognized value **MUST** degrade to plain text rather than raise an error.

Expression hints are optional, pure, bounded CEL. They belong to the
`core+expressions` profile and remain advisory: an expression may compute a
value, hide a prompt, mark it read-only, or return a message, and none of those
outcomes blocks a response.

### 6.4 Structural tables {#tables}

A section may declare that its child sections are repeating instances of one
shape: prompts at the same position correspond across instances, each instance's
title identifies it, and a prompt's label names the corresponding field.

This is a claim about **structure, not appearance**. A renderer may present it
as a grid, as stacked cards, as a flat sequence of prompts, or as speech, and
all are conformant. It licenses no layout data of any kind.

Whether a filler may add or remove instances is explicit; absent, instances are
fixed. Any advisory upper bound on instance count is advisory in the sense of
[§6.3](#hints): a document carrying more is still valid and is reported as a
warning.

> Rationale: fixed-by-default is the safe direction. A table that silently
> gained a row is a worse failure than one that needed an explicit property.

### 6.5 Roles {#roles}

A role states who is meant to fill something in — `patient`, `nurse`, `office`.
The vocabulary is open and a role is a statement of intent, never enforcement: a
reader marks the field and still accepts any input. A prompt's role overrides the
role of the section containing it.

### 6.6 Extension members {#extensions}

Unknown members are semantic extension data. They **MUST** round-trip and are
included in whole-document beta.6 digests ([§9](#digests)). Retired presentation
members remain forbidden.

### 6.7 Offline reading {#offline}

Core form reading is offline and safe. Opening a form or an attestation **MUST
NOT** contact a `metadata.submissionUrls` entry, a certificate endpoint, or any
other network location.

### 6.8 Renderer obligations {#renderers}

A renderer **MUST** preserve semantic order, **MUST** use labels as accessible
names, and **MUST** allow complete keyboard operation.

## 7. Representations {#representations}

The semantic model is representation-neutral. The same form or attestation may
be written as APR-JSONC or APR-YAML; comments, whitespace, indentation, scalar
spelling, and mapping order have no semantic effect.

A complete grammar for each binding — character and encoding assumptions, the
full set of legal and illegal productions, and boundary inputs — is pinned today
by the corpus. Its normative prose is pending; see [§17](#pending).

### 7.1 APR-JSONC {#apr-jsonc}

APR-JSONC is JSON with line comments, block comments, and trailing commas. Once
comments and trailing commas are removed it **MUST** decode to the JSON semantic
model in this specification. Comments are source trivia and cannot carry APR
meaning. A JSONC parser **MUST** reject duplicate object keys.

### 7.2 APR-YAML {#apr-yaml}

APR-YAML is a restricted YAML 1.2 representation of the same JSON model. Keys
**MUST** be strings. Anchors, aliases, tags, merge keys, non-finite numbers,
binary values, dates with implicit typing, and arbitrary-language constructors
are forbidden.

Implementations **MUST** use a safe YAML loader and resolve scalars only to JSON
null, boolean, number, string, array, or object before APR validation. Responses
remain strings even where a YAML scalar could otherwise resolve as a number or
boolean.

## 8. Streams {#streams}

A stream is an ordered transport of independent records. Physical order is
presentation only: it creates no subject, revision, chronology, or trust
relationship.

Each record is exactly one of:

- a complete standalone APR form; or
- an APR attestation (`recordType: "attestation"`).

A stream **MUST NOT** mix representations. It **MUST NOT** deduplicate repeated
form occurrences, even when their semantic digests are identical. A single-form
API given a stream **MUST** return `APR_STREAM_REQUIRES_ITERATION` and **MUST
NOT** select a record by position. A streaming API yields every record and may
hold an unresolved attestation until its subject form has been observed.

APR-JSONC streams use RFC 7464 framing: every record is prefixed by ASCII Record
Separator (`0x1e`) and terminated by LF. A comment is confined to its one JSONC
record. APR-YAML streams use YAML document markers (`---`); every document is one
record. The corpus supplies paired streams with equal semantic records.

## 9. Semantic digests and manifests {#digests}

`jcs-sha256` is the beta.6 semantic digest algorithm. Its input is RFC 8785 JCS
serialization of the fully parsed JSON semantic model, encoded as UTF-8; its
value is lowercase hexadecimal SHA-256 prefixed with `sha256:`. Source syntax is
never hashed.

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

## 10. Attestations {#attestations}

An attestation is a stream record with this shape.

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

`subject.digest` identifies the complete form semantic model, never a stream
position, filename, or document id.

`scope.kind` is `document` or `fields`. `document` covers the complete form. A
`fields` scope lists prompt ids, and the manifest **MUST** include each selected
prompt, its response and hints, and every ancestor section's id, title,
description, kind, and role. A fields assertion therefore attests to both what
was answered and the question and context presented.

`proofs` are assertions over the JCS serialization of the attestation envelope
after omitting `proofs` themselves. Beta.6 initially defines
`cms/ecdsa-p256-sha256`: its value is base64 CMS SignedData with the certificate
chain included. A proof **MUST NOT** invent a second copy of the subject digest
or scope. Unsupported proof types are `unverifiable`, not invalid.

> Rationale: a second copy of the subject is what once let a redirected
> destination still verify.

`witnesses` is an ordered, duplicate-free list of semantic digests of earlier
attestation envelopes, again excluding `proofs`. It says only that this
attestation's signer explicitly witnessed those assertions. Witnessing neither
authorizes a change nor proves a clock order, workflow acceptance, real-world
identity, or trusted time.

A changed form is another complete form occurrence with a different subject
digest. Earlier attestations remain assertions about their original subject and
do not transfer to the changed form. Multiple attestations may target one
unchanged form, and an attestation may be encountered before its subject.

## 11. Verification and safety {#verification}

Verification reports these independent facts:

- `valid` — the subject resolved, semantic digest and manifest match, and a
  recognized proof verifies;
- `invalid` — a recognized proof fails, or a resolved subject differs from the
  attested digest or manifest;
- `unresolved` — no matching form occurrence is available;
- `unverifiable` — required representation, extension, digest, or proof support
  is unavailable; and
- `witnessed` — one or more referenced attestation envelopes resolve and match.

Trust is separate from cryptographic validity. A valid self-signed proof is not
a trusted identity; trust policy is supplied by the caller and is not part of
this format.

Attestation status **MUST NOT** gate parsing, validation, rendering, export, or
data extraction. An unsigned form is complete APR data.

Verification is a side-effect-free computation over data already in hand. A form
carrying attestations is no less safe to open than one without.

## 12. Conformance {#conformance}

- `core` parses and writes one beta.6 form in both representations.
- `core+streams` adds stream iteration.
- `core+attestations` adds semantic digests, manifests, attestation resolution,
  witness lookup, and the verification vocabulary of [§11](#verification).

An implementation may claim a profile only with the exact beta.6 corpus revision
it passes.

The beta.6 corpus covers paired JSONC/YAML forms and streams, duplicate and
out-of-order records, malformed framing, single-form API rejection, digest and
manifest vectors, document and fields scopes, CMS proof inputs, unsupported
proofs, witness chains, and changed copied forms.

## 13. Extensibility and compatibility {#compatibility}

Within beta.6, forward compatibility rests on [§6.6](#extensions): unknown
members are extension data, **MUST** round-trip, and participate in
whole-document digests.

Across versions, beta.6 makes no compatibility commitment. `version` **MUST** be
exactly `"1.0-beta.6"` and a beta.3 document **MUST** be rejected
([§5](#beta6-boundary)).

Version and profile negotiation, the governance process for registered
extensions, and the compatibility rules that will apply after the first public
release are not yet specified. Their prose is pending; see [§17](#pending).

## 14. Security and privacy {#security}

The security properties beta.6 states normatively are:

- opening a document executes no document-supplied code ([§1](#scope));
- opening a document performs no network access ([§6.7](#offline));
- verification is side-effect-free ([§11](#verification));
- attestation status never gates access to form data ([§11](#verification));
- a valid proof is not a trusted identity ([§11](#verification)); and
- a manifest does not retain the plaintext it attests to ([§9](#digests)).

A form carries whatever a person typed into it and is as sensitive as its
responses. This specification defines no encryption, no access control, and no
redaction; a document is protected by the storage and transport around it.

Resource limits — maximum document size, nesting depth, stream length, and the
behavior of a reader that reaches one — are not yet specified normatively.
Implementations impose their own; see [§17](#pending).

## 15. Media types and file extensions {#media-types}

APR defines no registered media type. Implementations **MUST NOT** rely on a
media type to determine that a document is APR, nor to select a representation
or profile.

The extensions `.aprt` and `.aprf` are in use by convention, and `documentType`
is authoritative over any filename ([§6.1](#identity)). Extensions for the JSONC
and YAML representations follow the corpus and examples by convention only.

A registered media type, a normative extension registry, and representation
detection rules are not specified. See [§17](#pending).

## 16. Change history {#history}

| Format version | Change |
| --- | --- |
| `1.0-beta.6` | Retired embedded `signatures` and `apr-sig-v3` in favor of independent attestation records. Added APR-JSONC and APR-YAML representations, representation-neutral record streams, `jcs-sha256` semantic digests, integrity manifests, and the verification vocabulary. |
| `1.0-beta.3` | Superseded. Not a compatibility target. |

## 17. Pending normative sections {#pending}

Non-normative editor's note.

The sections below are required by the implementation contract and are not yet
written as prose. In each case the behavior is pinned today by the schema and
the conformance corpus, which outrank this document ([§4](#authority)). Listing
them here is a statement of what this document does not yet say — not a licence
to infer the missing rules.

| Area | Anchor | Pinned today by |
| --- | --- | --- |
| Abstract model and processing roles | — | corpus; SDK surfaces |
| Representation grammars | [#representations](#representations) | corpus `forms/`, `malformed/` |
| Member-by-member catalogue | [#form-model](#form-model) | `apr-1.0-beta.6.schema.json` |
| Version and profile negotiation, extension governance | [#compatibility](#compatibility) | — |
| Resource limits | [#security](#security) | implementation-defined |
| Media type and extension registration | [#media-types](#media-types) | — |

Two rows have no pinning authority at all: extension governance and media-type
registration are new normative content that neither the schema nor the corpus
determines. They require a decision, not transcription.

Tracking for this work lives in the repository's specification milestone.

# APR File Format Specification

**Specification document version:** 1.0.0-beta.6-draft
**Status:** BETA — breaking changes are intentional until the first public release
**Describes format version:** `1.0-beta.6`
**Schema:** `schemas/apr-1.0-beta.6.schema.json`
**Conformance corpus:** `tests/Conformance/beta6/`

## 1. Authority and beta.6 boundary

APR is a local-first, semantic form format. It stores what a form asks and the
string responses it receives; it stores no page layout and executes no
document-supplied code. Authority is, in descending order:

1. the beta.6 conformance corpus;
2. the beta.6 JSON Schema; and
3. this specification.

Beta.6 replaces beta.3. There is no public compatibility commitment: a beta.6
reader **MUST** reject a beta.3 form rather than silently treating it as beta.6.
In particular, `signatures` and `apr-sig-v3` are retired. An APR form is ordinary
form data; cryptographic assertions live in independent attestation records.

## 2. Core form profile

A form is an object with `version`, `metadata`, and `sections`. It has the same
semantic structure as the preceding beta except for the beta.6 changes stated
here.

- `version` **MUST** be exactly `"1.0-beta.6"`.
- `documentType` is `template` or `filledForm`; it is authoritative over a file
  name. A filled form **MUST** declare `metadata.templateId`.
- `metadata.title`, every section `id` and `title`, and every prompt `id` and
  `label` are required non-blank strings.
- A response **MUST** be a JSON string. `null` or an absent response is read as
  the empty string; writers **MUST NOT** emit null. Hints are advisory and
  **MUST NOT** reject, alter, or block a response.
- Section ids and prompt ids are each document-wide unique namespaces. A section
  id and prompt id may be identical. A section has at least one prompt or child
  section, except an explicitly dynamic table.
- Unknown members are semantic extension data. They **MUST** round-trip and are
  included in whole-document beta.6 digests. Retired presentation members remain
  forbidden.
- Forms have no `signatures` member. A reader **MUST** report it as
  `RETIRED_EMBEDDED_SIGNATURES`; it is not an extension member.

Core form reading remains offline and safe: opening a form or an attestation
**MUST NOT** contact a `submissionUrls` entry, certificate endpoint, or any other
network location. Expressions remain optional, pure, bounded CEL hints. Renderers
must preserve semantic order, use labels as accessible names, and allow complete
keyboard operation.

## 3. Representations

The semantic model is representation-neutral. The same form or attestation may
be written as APR-JSONC or APR-YAML; comments, whitespace, indentation, scalar
spelling, and mapping order have no semantic effect.

### 3.1 APR-JSONC

APR-JSONC is JSON with line comments, block comments, and trailing commas. Once
comments and trailing commas are removed it **MUST** decode to the JSON semantic
model in this specification. Comments are source trivia and cannot carry APR
meaning. A JSONC parser **MUST** reject duplicate object keys.

### 3.2 APR-YAML

APR-YAML is a restricted YAML 1.2 representation of the same JSON model. Keys
**MUST** be strings. Anchors, aliases, tags, merge keys, non-finite numbers,
binary values, dates with implicit typing, and arbitrary-language constructors
are forbidden. Implementations **MUST** use a safe YAML loader and resolve
scalars only to JSON null, boolean, number, string, array, or object before APR
validation. Responses remain strings even where a YAML scalar could otherwise be
resolved as a number or boolean.

## 4. Streams

A stream is an ordered transport of independent records. Physical order is
presentation only: it creates no subject, revision, chronology, or trust
relationship.

Each record is exactly one of:

- a complete standalone APR form; or
- an APR attestation (`recordType: "attestation"`).

A stream **MUST NOT** mix representations. It **MUST NOT** deduplicate repeated
form occurrences, even when their semantic digests are identical. A single-form
API given a stream **MUST** return `APR_STREAM_REQUIRES_ITERATION`, never select a
record by position. A streaming API yields every record and may hold an
unresolved attestation until its subject form has been observed.

APR-JSONC streams use RFC 7464 framing: every record is prefixed by ASCII Record
Separator (`0x1e`) and terminated by LF. A comment is confined to its one JSONC
record. APR-YAML streams use YAML document markers (`---`); every document is one
record. The corpus supplies paired streams with equal semantic records.

## 5. Semantic digests and manifests

`jcs-sha256` is the beta.6 semantic digest algorithm. Its input is RFC 8785 JCS
serialization of the fully parsed JSON semantic model, encoded as UTF-8; its
value is lowercase hexadecimal SHA-256 prefixed with `sha256:`. Source syntax is
never hashed.

A form digest includes every APR-defined field and every unknown extension member
that survived parsing. It excludes only representation trivia. This prevents a
whole-form attestation from silently omitting a meaningful extension. A verifier
that cannot preserve or digest an extension member **MUST** report the assertion
as `unverifiable`, not valid.

An integrity manifest does not duplicate plaintext. It contains `root`, the form
digest, and sorted `entries`; each entry has a JSON Pointer `path` and a digest of
the JCS encoding of that path's value. A verifier can compare entries to explain
which values differ without the manifest retaining their old values. Pointers are
RFC 6901 pointers into the form semantic model. `entries` includes `""` (the root
pointer) and every
defined semantic leaf; whole-document manifests also include extension members.

## 6. Attestations

An attestation is a stream record with this shape:

```jsonc
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
position, filename, or document id. `scope.kind` is `document` or `fields`.
`document` covers the complete form. A `fields` scope lists prompt ids and the
manifest **MUST** include each selected prompt, its response and hints, and every
ancestor section's id, title, description, kind, and role. A fields assertion
therefore attests to both what was answered and the question/context presented.

`proofs` are assertions over the JCS serialization of the attestation envelope
after omitting `proofs` themselves. Beta.6 initially defines `cms/ecdsa-p256-sha256`:
its value is base64 CMS SignedData with the certificate chain included. A proof
must not invent a second copy of the subject digest or scope. Unsupported proof
types are `unverifiable`, not invalid.

`witnesses` is an ordered, duplicate-free list of semantic digests of earlier
attestation envelopes (again excluding `proofs`). It says only that this
attestation's signer explicitly witnessed those assertions. Witnessing neither
authorizes a change nor proves a clock order, workflow acceptance, real-world
identity, or trusted time.

A changed form is another complete form occurrence with a different subject
digest. Earlier attestations remain assertions about their original subject;
they do not transfer to the changed form. Multiple attestations may target one
unchanged form, and an attestation may be encountered before its subject.

## 7. Verification vocabulary and safety

Verification reports these independent facts:

- `valid`: the subject resolved, semantic digest and manifest match, and a
  recognized proof verifies;
- `invalid`: a recognized proof fails or a resolved subject differs from the
  attested digest or manifest;
- `unresolved`: no matching form occurrence is available;
- `unverifiable`: required representation, extension, digest, or proof support
  is unavailable; and
- `witnessed`: one or more referenced attestation envelopes resolve and match.

Trust is separate from cryptographic validity. A valid self-signed proof is not a
trusted identity. Attestation status **MUST NOT** gate parsing, validation,
rendering, export, or data extraction. An unsigned form is complete APR data.

## 8. Conformance claims

`core` parses and writes one beta.6 form in both representations. `core+streams`
adds stream iteration. `core+attestations` adds semantic digests, manifests,
attestation resolution, witness lookup, and the verification vocabulary. An
implementation may claim a profile only with the exact beta.6 corpus revision it
passes.

The beta.6 corpus must cover paired JSONC/YAML forms and streams, duplicate and
out-of-order records, malformed framing, single-form API rejection, digest and
manifest vectors, document and fields scopes, CMS proof inputs, unsupported
proofs, witness chains, and changed copied forms.

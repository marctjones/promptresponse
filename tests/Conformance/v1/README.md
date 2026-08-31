# APR Conformance Corpus — format generation 1

**Format version covered:** `1.0-beta` · **Corpus tag:** `corpus/v1` · **Status:** BETA

`v1/` covers format generation 1. It does *not* get a new folder per release — only
a new folder when the format version changes incompatibly. Pin the git tag when you
declare conformance: `APR core, corpus/v1 @ <sha>`.

Shared fixtures that every APR implementation, in any language, is held to. The
corpus is the executable half of the specification: where prose and fixtures
disagree, **the fixtures win** and the prose is a bug.

Two independent gates run over this directory:

| Gate | What it proves | How to run |
|---|---|---|
| `.NET runner` | The reference implementation behaves correctly | `dotnet test tests/PromptResponse.Core.Tests --filter Conformance` |
| `schema gate` | The published JSON Schema agrees with the fixtures | `python3 scripts/check-schema.py` |

A new SDK is conformant when it reproduces the four behaviours below.

## Categories

### `valid/` — MUST parse, MUST validate clean, MUST round-trip

Deserialize, validate with zero errors, re-serialize, deserialize again, and
validate again. All response strings MUST survive the round-trip unchanged.

> **Scope of the preservation check.** The runner compares responses *after the
> first parse* against responses after the round-trip. Both sides are therefore
> post-sanitizer, so this assertion catches a write-time mutation but **not** a
> read-time one that alters a fixture on the way in. Closing that gap needs a
> raw-bytes-to-parsed comparison. Until then, §7 of the specification is the
> authority on what read-time text handling is permitted to change.

| Fixture | Pins |
|---|---|
| `minimal-template.aprt` | The smallest useful template. |
| `filled-unicode.aprf` | Unicode responses survive a round-trip. |
| `table-and-expressions.aprt` | A fixed table plus the `expr*` hint family. |
| `hints-contradicted.aprf` | **The format's central rule.** Every response contradicts its own type hint — `"about twelve"` for a `number`, `"call me instead"` for an `email` — and the document is still completely valid. An implementation that rejects this file has misunderstood APR. |
| `response-edge-cases.aprf` | Strings that break form tooling: empty, whitespace-only, `"null"` and `"true"` as text, `"007"` (leading zeros preserved), an integer beyond double precision, embedded quotes, Windows paths, CRLF, tabs, emoji ZWJ sequences, Persian ZWNJ, RTL, CJK, combining accents. |
| `null-response-coercion.aprf` | `"response": null` and an absent `response` key both read as `""`. Lenient on read; a conforming writer MUST NOT emit null. |
| `unknown-fields.aprt` | Forward compatibility. Unknown members at document, section, prompt, and hint level MUST be ignored, never rejected. An unrecognized `expectedDataType` MUST degrade to a text field. |
| `deep-nesting.aprt` | 16 levels of section nesting — the interop floor (see *Nesting depth*). |
| `dynamic-table.aprt` | The one section permitted to be empty: a dynamic table whose rows arrive at fill time. |
| `all-hint-types.aprt` | Every registered `expectedDataType`, plus a prompt with no hint at all. |
| `section-ordering.aprt` | Presentation order is normative: a section's own prompts render **before** its child sections. A fixture cannot pin order by itself, so the runner asserts the block sequence. |
| `canonical-values.aprt` | Canonical write forms for `date`/`time`/`datetime`/`boolean`/`number`/`multichoice`/`select`, alongside non-canonical equivalents that are equally valid — including a multichoice option containing a comma, which comma-separation would corrupt. |
| `table-fixed-filled.aprf` / `table-dynamic-filled.aprf` / `dynamic-table.aprt` | The table matrix. Mutability (`canAddRows`) and population (do the cells hold values) are **independent axes**, so all four combinations are expressible — and none of them needs a table-specific representation, because population falls out of the sections and responses that are already there. |
| `hidden-characters-preserved.aprf` | A hidden character survives in a response hinted `url`, `email`, and `text` alike, alongside a legitimate Persian ZWNJ. The strictness that once cleaned url/email answers now lives on the submission URL instead, where it **refuses** rather than rewrites. |
| `unicode-security-advisories.aprf` | Bidi override/isolate and legitimate Persian ZWNJ/emoji ZWJ responses remain valid and byte-preserved. Shared advisors expose stable findings without rejecting or changing them. |
| `newer-minor-accepted.aprt` | Declares version `1.7`. A `1.0` reader **MUST** read it, warn `NEWER_MINOR_VERSION`, and preserve its unrecognised members. This plus `unknown-fields.aprt` is what makes the format extensible. |
| `signed-template.aprt` | A real publisher signature that MUST verify over its own unmodified content. Not a placeholder blob — it was produced by `apr sign` and is checked cryptographically on every test run. |

### `invalid/` — MUST parse, MUST FAIL validation

These are well-formed JSON with correct types. A reader MUST load them without
throwing and then report at least one structural error. Parsing and validation
are separate stages, and this category is what keeps them separate.

Covers: unsupported **major** version (`2.0` and `99.0` — a different major is
incompatible, while a newer *minor* is fine and lives in `valid/`), duplicate prompt
id, duplicate section id, blank ids, missing metadata title, missing section title,
missing prompt label (placeholder text is not a label), a section with no content, no
sections at all, and a filled form that does not name its template.

### `malformed/` — MUST be REJECTED at parse time

The strings-only guarantee is only worth something if violations are refused
rather than quietly coerced. A response given as a JSON number or boolean MUST
NOT be silently stringified into `"42"` or `"true"`. In the reference
implementation these raise `SerializationException`.

Covers: `"response": 42`, `"response": true`, `sections` as an object instead of
an array, and truncated bytes.

### `canonicalization/` — the apr-sig-v3 byte contract

`input.aprt` plus `vectors.json`: the exact canonical payload text, byte length, and
SHA-256 for the publisher, filler, and form-definition payloads. An implementation
with flawless CMS code still produces signatures nobody can verify if it assembles
the payload differently by one byte, and round-tripping against your own signer
cannot detect that. See that folder's README.

### `signatures/` — structurally valid, verification MUST fail

Both fixtures are `signed-template.aprt` with the submission URL redirected to
another host. They still validate structurally, because tampering is a verification
result and not a schema error — but the publisher signature MUST fail.

`tampered-metadata-url.aprt` is the one that matters. It changes **only**
`metadata.submissionUrls`, the ordered field a submitting client actually reads. An earlier
revision stored the URL a second time on the signature object and verified against
that private copy, so this exact edit left the signature reporting **valid** while the
form submitted somewhere else. Two copies of one fact is a correctness bug everywhere
in this format; here it was a security hole. Any implementation that recomputes the
payload from anything other than the document will pass `tampered-submission-url.aprt`
and fail this one.

## Tables carry no column definitions

A table is a section marked `kind: "table"`. Its child sections are instances and
their prompts are the cells, so a column header is simply the corresponding prompt's
`label` and a column's type hint is that prompt's `expectedDataType`. Correspondence
between instances is **positional**.

Nothing about a table is stated twice, which is why no fixture needs to pin what
happens when a column definition and its cells disagree: that state cannot be
represented. `kind: "table"` is a claim about structure only — a renderer may present
it as a grid, as cards, as a flat run of prompts, or as speech.

Every table carries at least one instance. An "empty" table was never empty: a UI
offering to add the first row is already presenting a row, and the instance is what
describes the table's own fields.

## Unknown members are preserved, retired members are not

Unrecognised members survive a round-trip unchanged — that is what lets the format
grow additively without older readers destroying newer files. The one exception is
members the specification has **retired** (`width`, `alignment`, and the rest of the
table-column presentation set), which are dropped so that removing them actually
means something. `TableDefinitionTests` pins the drop; the corpus pins the
preservation.

## Rules the schema cannot express

`schemas/apr-1.0.schema.json` catches 8 of the 10 `invalid/` fixtures. It cannot
express **document-wide id uniqueness**, so `duplicate-prompt-id.aprt` and
`duplicate-section-id.aprt` pass the schema by design and are caught by the
validator instead. Any implementation that validates with the schema alone is
incomplete and MUST add its own uniqueness pass.

Section ids and prompt ids occupy separate namespaces: a section and a prompt
MAY share an id.

## Nesting depth

The specification requires every implementation to support **at least 16 levels**
of section nesting. That is the interop floor, and `deep-nesting.aprt` pins it.

Implementations MAY support more. The reference implementation currently fails
above **30 levels**, because each section level costs two JSON depth levels
against `System.Text.Json`'s default `MaxDepth` of 64. That ceiling is an
implementation limit, not a conformance rule — do not encode it in a fixture.

## Adding fixtures

- Never rewrite an existing fixture in a way that changes its meaning. Add a new
  one instead; downstream SDKs pin against these bytes.
- Create a new corpus folder (`v2/`) only when the APR format version changes.
- A fixture must earn its place by pinning a rule that some implementation could
  plausibly get wrong.
- Signed fixtures must be generated with real keys via `apr keygen` / `apr sign`,
  never hand-written. The corpus certificate is self-signed with a 100-year
  validity so the fixture does not rot; its private key is deliberately not
  committed, because the embedded certificate is all a verifier needs.

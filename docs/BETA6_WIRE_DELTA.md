# The beta.6 wire delta

<!-- AI-ASSISTANT-README -->
Read this before aligning any SDK to APR `1.0-beta.6`. It lists every change an
implementation has to make, and — where one exists — the defect the .NET
alignment actually found there. It is derived from
[the specification](APR_SPECIFICATION.md); where the two disagree, the
specification is right.
<!-- END-AI-ASSISTANT-README -->

## Why this exists

The .NET SDK went from 95 to 171 of 171 conformance cases by making the changes
below. Three more SDKs have to make the same ones, and the point of writing them
down once is that each does not rediscover them.

Every row has a **found** note where the .NET alignment turned up something the
specification implies but nobody had noticed. Those are the expensive ones: they
are not in the changelog, and an implementer reading only the specification would
have to derive each of them.

## 1. Members

| Change | Rule | Notes |
| --- | --- | --- |
| `version` is now `aprVersion` | `APR-MODEL-005` | Both records, form and attestation. Nothing else spells the format version. |
| `filledBy` and `filledDate` are gone | changelog | Workflow state. An unsigned claim about who completed a form and when is not evidence of either. A workflow that needs to record receipt writes an ordinary form naming this one under `metadata.regarding`. |
| `responseMetadata` is gone, including `source` | changelog | It rested a prohibition on a member every reader was free to ignore. What survives is reader state: a reader knows which responses it computed **this session** and may replace those; every non-empty response in the document as it was read is authored. **Found:** .NET tracked the marker in the document and had to move it to a `[JsonIgnore]` field. TypeScript did the same, and so did the browser demo, which read `responseMetadata.source` to decide which fields to write back after a recompute — a reader that dropped the marker would have silently stopped updating computed fields on screen. Python was the same again, and its `web-demo.py` was still stamping the retired `filledDate` on every save. Java, which keeps the raw JSON tree, dropped no retired member at all and wrote `responseMetadata` and `filledDate` straight back out. |
| `metadata.regarding` is new | `APR-MODEL-043`, `APR-MODEL-044` | An ordered, duplicate-free array of digests. It asserts context and nothing else — no revision, no chronology, no authority. |
| `metadata.templateId` is a URI | `APR-MODEL-036` | A slug is not one. **Found:** this rippled through thirty-nine fixtures and assertions, and through the PDF importer, which minted a slug. |
| Root `signatures` is retired | `APR-MODEL-022` | Report `RETIRED_EMBEDDED_SIGNATURES` rather than dropping it silently: a document carrying it was making a cryptographic claim beta.6 cannot honour. |
| Extension members carry a reverse-DNS prefix | `APR-MODEL-029`, `APR-MODEL-031` | Unprefixed names are reserved to the specification. A validator **may** warn `UNPREFIXED_MEMBER`; a producer **must not** write one. |

## 2. Types

Only a response is always a string. Everything structural carries the JSON type
it means.

| Member | Type | Notes |
| --- | --- | --- |
| `canAddRows` | boolean | **Found:** TypeScript typed it as a string, so it rejected the specification's own table example and accepted `"true"` — a document the format requires be refused with `WRONG_TYPE`. One mistyped member failed the format in both directions at once. |
| `maxRows` | integer, at least 1 | **Found:** a cap below one describes a table that cannot exist, and nothing checked it. |
| `step` | number | **Found:** Java types nothing structurally — it reads the raw tree — so `"canAddRows": "true"` was accepted until the member tables were mirrored across from .NET. |
| `min`, `max` | number **or** string | A number on `number`, `currency`, `range`; a canonical-form string on `date`, `time`, `datetime`. **Found:** this conditional type is in the member table and no schema can express it, so a number on a date field passed every check. Neither spelling may be rewritten into the other — writing `5` back as `"5"` changes the digest. |
| `signature`, `file` | removed | Signing is an attestation; attachments have no representation. |

**Found:** a typed deserializer conflates two conditions the specification
separates. A wrongly typed *response* is a parse failure (§7.3); a wrongly typed
*structural member* is the validation error `WRONG_TYPE` (§7.1). In .NET both
surfaced identically, and twenty-six cases were refused under the wrong code.
Check the parsed value against the member tables **before** building the typed
model.

## 3. Diagnostics

Section 7.1 names eight errors, 7.2 names fifteen advisories, 7.3 governs the
parse stage. `APR-VAL-009` makes the spellings binding: whether to warn is your
choice, the name is not.

**Found:** .NET carried its own vocabulary — `TYPE_MISMATCH` for
`RESPONSE_CONTRADICTS_TYPE`, `ROW_COUNT_OUT_OF_HINT_RANGE` for
`TABLE_OVER_CAPACITY` — and did not report eight of the fifteen at all. Expect
the same: an existing validator almost certainly has its own names.

**Found:** do not derive a diagnostic from an exception message. .NET matched
substrings and reported a YAML *directive* as `YAML_TAG_FORBIDDEN`, because the
directive message mentions `%TAG`. Carry the code as data.

## 4. Digests

`jcs-sha256` over the RFC 8785 canonical form of the fully parsed semantic model.

**Found, and this is the one most likely to bite:** no standard JSON writer
produces JCS. .NET's default encoder escapes for HTML safety, and even its
relaxed encoder escapes a character outside the Basic Multilingual Plane as a
surrogate pair — so a document with an emoji in a title produced a digest no
other implementation could reproduce. Write the string escaping yourself: escape
the quote, the backslash and the C0 controls, and leave every other character as
itself.

**Found:** number serialization is ECMAScript's `Number::toString`, and two
shortcuts that look right are not. Rendering an integral double with your
language's integer conversion prints the exact value where ECMAScript prints the
shortest round-trip padded with zeros. And most languages switch to exponential
notation at a different threshold. `scripts/check-oracle.py` holds this to
RFC 8785's own published vectors; run the equivalent.

Manifest entries are ordered by path and never repeat one (`APR-DIGEST-003`), and
a manifest carrying entries carries the root pointer (`APR-DIGEST-004`).

## 5. Attestations

Independent stream records, resolved by digest, never by position or filename.
`subject`, `scope`, `manifest` and their entries admit no additional members,
while the record itself carries extension members that round-trip
(`APR-ATTEST-004`). A proof must not restate the subject digest or the scope
(`APR-ATTEST-007`).

**Found:** absent and present-but-wrong are different conditions. A missing
required member is `REQUIRED_FIELD`; `WRONG_TYPE` tells a reader to look at the
type of a member that is not there.

## 6. Text

Human-facing text is held to NFC and the UTS #39 floor (`APR-TEXT-011`). A
validator reports a violation; **a reader never rewrites it.**

**Found:** .NET stripped abusive code points out of every title and label as it
read them. That changed the semantic model, so a document carrying an invisible
character produced a digest nobody else could reproduce and every attestation
over it failed — and it hid the attempt, removing in silence a zero-width space
inserted to make one label look like another.

## 7. Expressions

CEL, pinned to cel-spec `v0.25.3`: standard library and standard macros, no
extension library and no custom function. Reserved bindings are `_this`, `_id`,
`_now`, `_today` and `ctx`, and `_now`/`_today` come from the caller rather than
the host clock.

**Found:** `exprValidation` returns a message, and a non-string result takes the
fallback. .NET coerced any result into a string, so `2 + 2` showed a person "4"
where the author meant nothing at all.

**Found:** computed values settle in reference order, not document order. A total
that depends on a tax that depends on a subtotal comes back empty if each prompt
is computed once against the initial context.

## 8. Representations and streams

APR-JSONC and APR-YAML spell one semantic model. A record-separated stream
carries one representation throughout; a JSONC stream carrying a YAML record is
`APR_STREAM_MIXED_REPRESENTATIONS`, not a generic parse failure.

**Found:** do not leak your parser's exception type to callers. .NET surfaced
`System.Text.Json.JsonException` and `YamlDotNet.Core.YamlException`, which
couples every caller to a dependency choice they did not make — and crashed the
conformance driver twice before it could report anything.

## Order of work

The .NET alignment went 95 → 116 → 134 → 144 → 151 → 158 → 162 → 171. The
sequence that produced it, and the one to repeat:

1. **The driver's own assumptions first.** Twenty-one of the initial failures
   were in the driver, not the library: it assumed a lone document is a form
   (an attestation is not), and it wrote from the typed model rather than the
   parsed value, so extension members did not survive a round trip.
2. **The parse/validate split**, which is the only change here needing a design
   decision.
3. **The diagnostic vocabulary**, which is mechanical once the split exists.
4. **The value-shape and text rules**, each independently testable.
5. **Expressions last**, because they depend on everything else being right.

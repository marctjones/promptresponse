# SDK Conformance

<!-- AI-ASSISTANT-README -->
Use this when changing the APR format, serializers, validators, or non-.NET SDKs.
It defines the shared fixture corpus and schema gate all SDKs are held to.
<!-- END-AI-ASSISTANT-README -->

APR 1.0 is defined by three artifacts. Where they disagree, authority runs in
this order:

1. **The conformance corpus** — `tests/Conformance/v1/`, executable and always right
2. **The JSON Schema** — `schemas/apr-1.0.schema.json`, machine-checkable structure
3. **The specification** — [`docs/APR_SPECIFICATION.md`](APR_SPECIFICATION.md)

An SDK is conformant when it reproduces the corpus behaviours below. See
[`tests/Conformance/v1/README.md`](../tests/Conformance/v1/README.md) for what each
fixture pins.

## The two gates

```bash
# Reference implementation behaves correctly
dotnet test tests/PromptResponse.Core.Tests --filter Conformance

# Published schema agrees with the fixtures (any language, no .NET required)
pip install jsonschema && python3 scripts/check-schema.py
```

## Required behaviours

| Corpus folder | Requirement |
|---|---|
| `valid/` | Deserialize → validate with zero errors → serialize → deserialize → validate again. Every response **MUST** survive byte-for-byte. |
| `invalid/` | Deserialize successfully, then report **at least one structural error**. Parsing and validation are separate stages. |
| `malformed/` | **Reject at parse time.** A response given as a JSON number or boolean **MUST NOT** be coerced to a string. |
| `signatures/` | Validate structurally, but **fail signature verification**. Tampering is a verification result, not a schema error. |
| `canonicalization/` | Reproduce the published `apr-sig-v3` payloads **byte-for-byte**. Port this before writing any CMS code — it is the only part of signing that fails silently. |

An SDK **MUST** also preserve unrecognised members across a round-trip
(`valid/newer-minor-accepted.aprt`), accept a newer *minor* version while rejecting a
different *major* (§1.3.1), and present a section's own prompts before its child
sections (`valid/section-ordering.aprt`).

Additionally, `valid/signed-template.aprt` **MUST** verify over its own
unmodified content — proving the `apr-sig-v3` canonical payload is reproducible
rather than merely well-formed.

## Rules no schema can express

`schemas/apr-1.0.schema.json` catches 8 of the 10 `invalid/` fixtures. It cannot
express **document-wide id uniqueness**, so `duplicate-prompt-id.aprt` and
`duplicate-section-id.aprt` pass the schema by design. An SDK that validates with
the schema alone is incomplete and **MUST** add its own uniqueness pass.

## Conformance profiles

Only `core` is required. An SDK implementing core alone is fully conformant, not
degraded — but it **MUST** preserve `expr*` hint strings and the `signatures`
array on round-trip rather than dropping features it does not implement.

| Profile | Adds | Corpus coverage |
|---|---|---|
| `core` | Parse, validate, fill, write | `valid/`, `invalid/`, `malformed/` |
| `core+expressions` | `expr*` evaluation | `valid/table-and-expressions.aprt` |
| `core+signatures` | CMS verification | `valid/signed-template.aprt`, `signatures/` |

Declare conformance as e.g. `APR 1.0 core, corpus v1 @ <sha>`.

## Versioning

`v1/` maps to APR document `version: "1.0"`. Do not rewrite existing fixtures in
ways that change their meaning — downstream SDKs pin against these bytes. Add new
fixtures for new cases; create a new corpus folder only when the format version
changes.

## Current SDK status

| SDK | Status |
|---|---|
| .NET | **Conformant** — corpus, schema, desktop, and release gates run in CI |
| Python | **Core + expressions** — shared corpus, APR CEL binding, and locked `uv` environment run in CI; signatures are preserved but unchecked |
| TypeScript | **Core + expressions (partial)** — shared corpus, HTML renderer, and CEL numeric/boolean/string bindings run in CI; timestamp declarations remain `dyn` pending a runtime with typed timestamp support |
| Java | **Core + expressions** — shared corpus and APR CEL binding run in CI through the project-local Maven wrapper; signatures are preserved but unchecked |
| Java / Rust / C++ | Not implemented; no conformance claim |

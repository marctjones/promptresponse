# Concept registry

This registry maps enduring product concepts to their normative definition,
implementation owner, and regression evidence. It is a navigation aid; it does not
override the corpus, schema, specification, source, or tests.

The authority column cites specification **anchors**, not section numbers.
Anchors are stable across edits; section numbers are not.

| Concept | Intended behavior | Authority | Implementation | Regression evidence | Maturity |
| --- | --- | --- | --- | --- | --- |
| APR document | Semantic template or filled form with ordered sections and prompts | spec [#form-model](APR_SPECIFICATION.md#form-model), [#root-object](APR_SPECIFICATION.md#root-object) | `PromptResponse.Core.Models` | corpus + schema gate | Shipped beta |
| Responses | Always strings; preserved byte-for-byte; any string is valid | spec [#responses](APR_SPECIFICATION.md#responses), [#any-string](APR_SPECIFICATION.md#any-string) | serializer, validators | Core, SDK, corpus tests | Shipped beta |
| Hints | Advisory in full; never reject, alter, or block a response | spec [#hints-advisory](APR_SPECIFICATION.md#hints-advisory), [#hints-object](APR_SPECIFICATION.md#hints-object) | validators, renderers | Core and desktop tests | Shipped beta |
| Representations | One semantic model spelled as APR-JSONC or APR-YAML | spec [#representations](APR_SPECIFICATION.md#representations), [#apr-jsonc](APR_SPECIFICATION.md#apr-jsonc), [#apr-yaml](APR_SPECIFICATION.md#apr-yaml) | `Core.Beta6` reader/writer | paired corpus fixtures | Shipped beta |
| Streams | Ordered transport of independent records; order carries no meaning | spec [#streams](APR_SPECIFICATION.md#streams) | `AprBeta6Reader` stream APIs | stream fixtures, SDK tests | Shipped beta |
| Digests and manifests | `jcs-sha256` over the semantic model; manifests hold no plaintext | spec [#digests](APR_SPECIFICATION.md#digests) | `Core.Beta6.AprSemanticDigest` | digest vectors | Shipped beta |
| Attestations | Independent records; resolve by digest; never gate the data | spec [#attestations](APR_SPECIFICATION.md#attestations), [#never-gate](APR_SPECIFICATION.md#never-gate) | `Core.Beta6` attestation types | attestation and witness fixtures | Shipped beta |
| Expressions | CEL-based advisory and computed behavior, never code execution | spec [#expressions](APR_SPECIFICATION.md#expressions) | `Core.Expressions` | expression tests | Shipped beta, gaps recorded |
| Tables | A structural claim about repeating instances; licenses no layout | spec [#tables](APR_SPECIFICATION.md#tables), [#table-no-layout](APR_SPECIFICATION.md#table-no-layout) | section tree, renderers | schema gate | Shipped, no beta.6 fixture |
| Roles | Who a part is for; never who may type into it | spec [#roles](APR_SPECIFICATION.md#roles) | desktop role affordances | roles fixture, SDK tests | Shipped beta |
| Text handling | Filled data never rewritten; authoring data may be refused | spec [#text-handling](APR_SPECIFICATION.md#text-handling), [#filled-never-rewritten](APR_SPECIFICATION.md#filled-never-rewritten) | Unicode inspector | Unicode security tests | Shipped beta |
| Validation | Errors are structural only; warnings never affect validity | spec [#validation](APR_SPECIFICATION.md#validation), [#semantic-validation](APR_SPECIFICATION.md#semantic-validation) | validators | Core and SDK tests | Shipped beta |
| Rendering | Render semantic content without storing layout; order is data | spec [#renderers](APR_SPECIFICATION.md#renderer-requirements), [#ordering](APR_SPECIFICATION.md#ordering) | Core render model; PDF/HTML renderers | renderer and PDF tests | Shipped |
| Accessibility | Labels are accessible names; complete keyboard operation | spec [#renderer-requirements](APR_SPECIFICATION.md#renderer-requirements); [UX](UX_ACCESSIBILITY.md) | Desktop profiles and views | accessibility and GUI tests | Partial runtime evidence |
| Import/export | Transform without making external layout part of APR | spec [#export](APR_SPECIFICATION.md#export); [Implementation registry](IMPLEMENTATION_REGISTRY.md) | PDF renderer/importer, CLI | PDF, CLI, release smoke tests | Shipped with limits |
| Submission | Explicit user action only; document opening stays network-free | spec [#security](APR_SPECIFICATION.md#security), [#metadata](APR_SPECIFICATION.md#metadata) | CLI/Desktop handoff services | service and CLI tests | Limited profile |

## Retired concepts

| Concept | Status |
| --- | --- |
| Embedded signatures (`signatures`, `apr-sig-v3`) | **Retired in beta.6.** Replaced by independent attestations. A document carrying `signatures` is reported as `RETIRED_EMBEDDED_SIGNATURES` — see spec [#retired-members](APR_SPECIFICATION.md#retired-members). |
| Table column presentation members | **Retired.** Dropped on read; see spec [#retired-members](APR_SPECIFICATION.md#retired-members). |

`tests/registry.json` is the machine-readable mapping from normative requirements to
fixtures and tests. `scripts/check-test-registry.py` verifies it in CI.

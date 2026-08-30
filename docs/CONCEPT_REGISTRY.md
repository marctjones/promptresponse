# Concept registry

This registry maps enduring product concepts to their normative definition,
implementation owner, and regression evidence. It is a navigation aid; it does not
override the corpus, schema, specification, source, or tests.

| Concept | Intended behavior | Authority | Implementation | Regression evidence | Maturity |
| --- | --- | --- | --- | --- | --- |
| APR document | Semantic template or filled form with ordered sections/prompts | APR spec §§3-5 | `PromptResponse.Core.Models` | corpus + schema gate | Shipped beta |
| Responses and hints | String responses; hints are advisory and non-destructive | APR spec §§3, 6 | serializer, validators, profiles | Core, SDK, corpus tests | Shipped beta |
| Rendering | Render semantic content without storing layout | APR spec §10 | Core render model; PDF/HTML renderers | renderer/PDF tests | Shipped |
| Expressions | CEL-based advisory and computed behavior, never code execution | APR spec §8 | `Core.Expressions` | expression vectors/tests | Shipped beta |
| Signatures | CMS integrity/provenance; v3 is not complete attestation history | APR spec §9; [Signing](SIGNING.md) | `Core.Signing` | canonical vectors + signing tests | Shipped beta |
| Accessibility | Universal interaction base plus optional profiles | [UX](UX_ACCESSIBILITY.md) | Desktop profiles/views | accessibility and GUI tests | Partial runtime evidence |
| Import/export | Transform without making external layout part of APR | [Implementation registry](IMPLEMENTATION_REGISTRY.md) | PDF renderer/importer, CLI | PDF/CLI/release smoke tests | Shipped with limits |
| Submission | Explicit user action only; document opening stays network-free | Product + APR spec | CLI/Desktop handoff services | service/CLI tests | Limited profile |

`tests/registry.json` is the machine-readable mapping from normative requirements to
fixtures and tests. `scripts/check-test-registry.py` verifies it in CI.

# Architecture

APR is the shared semantic document format. The conformance corpus, JSON Schema, and APR specification define it; applications and SDKs implement it rather than competing with it.

```
Corpus + schema + specification
             │
     PromptResponse.Core
  model · parse/write · validation
  expressions · attestation records · rendering model
      │             │             │
 Desktop (Avalonia) CLI       PDF renderer/importer
      │             │             │
     local files, explicit export and handoff
```

Python, TypeScript, and Java SDKs independently exercise the shared corpus. Local demos consume SDKs and do not parse APR independently.

| Layer | Owns | Must not own |
| --- | --- | --- |
| Format artifacts | APR meaning and compatibility | UI policy or hosted workflows |
| Core | model, serialization, validation, expressions, attestations, render model | Avalonia, file dialogs, network UI |
| Renderers/importers | derived PDF/HTML and controlled import | APR layout fields |
| Desktop | interactive author/fill workflow and local services | new format semantics |
| CLI | deterministic automation and explicit operations | hidden background submission |
| SDKs | profile-appropriate APR read/write behavior | loss of unknown members |

Readers parse UTF-8 JSON, reject malformed wire types, then validate structure. Responses remain strings; unknown forward-compatible members survive round trips. Renderers consume the semantic tree but never write layout instructions into APR. Network handoff occurs only after an explicit user command.

Constraints: no executable document content or implicit network access on open; no layout/styling fields in APR; preserve unknown members unless the specification explicitly retires them; keep feature-specific presentation outside the Core model.

See the [concept registry](CONCEPT_REGISTRY.md) for code and test ownership.

## Repository ownership

The tree mirrors the architectural layers. Source and durable evidence are tracked;
build output, package-manager caches, test results, and locally generated release
artifacts are not source of truth.

| Location | Ownership | Tracked role | Not a source of truth |
| --- | --- | --- | --- |
| `src/PromptResponse.Core` | APR model and profiles | reference .NET implementation | desktop or transport policy |
| `src/PromptResponse.Desktop` | Avalonia client | interactive author/fill host | APR semantics or renderer internals |
| `src/PromptResponse.Cli` | deterministic automation | explicit command host | hidden background delivery |
| `src/PromptResponse.Rendering.Pdf` | PDF export and AcroForm import | derived document renderer/importer | APR layout model |
| `tests/` and `tests/Conformance/` | regression and format evidence | executable behavior contract | generated test results |
| `schemas/` | structural APR contract | machine-readable format authority | application defaults |
| `python/`, `typescript/`, `java/` | independent SDKs and local demos | cross-language corpus evidence | a second format definition |
| `docs/` | canonical product, design, operations, and registry records | human-facing authority map | historical plans |
| `examples/` and `tests/Conformance/beta6/` | user and regression inputs | supported beta.6 documents | mutable test output |
| `packaging/`, `docker/`, `scripts/`, `.github/` | build, release, and verification machinery | reproducible operational sources | release binaries |
| `local-nuget/` | vendored PDF-engine package feed | intentionally tracked reproducibility input | a general package cache |

Ignored directories such as `bin/`, `obj/`, `TestResults/`, `dist/`,
`dist-smoke/`, Python/Node/Java dependency caches, and temporary evidence are
regenerated. A change belongs in the source owner above, never in a generated
copy.

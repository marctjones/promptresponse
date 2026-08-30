# Architecture

APR is the shared semantic document format. The conformance corpus, JSON Schema, and APR specification define it; applications and SDKs implement it rather than competing with it.

```
Corpus + schema + specification
             │
     PromptResponse.Core
  model · parse/write · validation
  expressions · signing · rendering model
      │             │             │
 Desktop (Avalonia) CLI       PDF renderer/importer
      │             │             │
     local files, explicit export and handoff
```

Python, TypeScript, and Java SDKs independently exercise the shared corpus. Local demos consume SDKs and do not parse APR independently.

| Layer | Owns | Must not own |
| --- | --- | --- |
| Format artifacts | APR meaning and compatibility | UI policy or hosted workflows |
| Core | model, serialization, validation, expressions, signatures, render model | Avalonia, file dialogs, network UI |
| Renderers/importers | derived PDF/HTML and controlled import | APR layout fields |
| Desktop | interactive author/fill workflow and local services | new format semantics |
| CLI | deterministic automation and explicit operations | hidden background submission |
| SDKs | profile-appropriate APR read/write behavior | loss of unknown members |

Readers parse UTF-8 JSON, reject malformed wire types, then validate structure. Responses remain strings; unknown forward-compatible members survive round trips. Renderers consume the semantic tree but never write layout instructions into APR. Network handoff occurs only after an explicit user command.

Constraints: no executable document content or implicit network access on open; no layout/styling fields in APR; preserve unknown members unless the specification explicitly retires them; keep feature-specific presentation outside the Core model.

See the [concept registry](CONCEPT_REGISTRY.md) for code and test ownership.

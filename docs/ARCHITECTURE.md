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

## Host ports

Decided for #142. Core owns APR; the operating system is reached through a named
port or not at all.

**The problem.** Six capabilities are reached for directly today, each from
whichever surface happened to need it first:

| Capability | Reached today by | How |
| --- | --- | --- |
| Choosing a file | `Desktop/Services/FileService` | Avalonia `StorageProvider` |
| Writing a file | `Cli/Api/Filling/FilledFormWriter`, `Desktop/Services/AprDocumentPersistence` | `File.WriteAllText` |
| Handing off to mail | `Desktop/Services/MailHandoffService` | `Process.Start` |
| Reading a signing key | `Cli/Commands/AttestCommand` | `X509CertificateLoader` from a path |
| Host preferences | `Desktop/Profiles/OsAccessibilityProbe` | environment variables, `Process.Start` |
| Submitting over HTTPS | nowhere yet | — |

The CLI cannot open a file dialog, the desktop cannot attest from a key file, and
neither can submit. The sixth row is the one that matters now: the CLI submission
work would add a seventh direct reach, in the surface that has the least reason
to own one.

**The seam.** `PromptResponse.Host.Abstractions` — interfaces and result types,
no implementation, no dependency on Avalonia, a platform, or Core's internals.
Each surface registers adapters at its composition root. There is no runtime
plugin discovery: an adapter that is not registered is a capability that is
absent, and absent is a first-class answer.

**Ports.** `IDocumentStore` (choose, read, write), `IDelivery` (submit by a
defined transport; hand off to mail), `ISigningKeys` (produce proof material over
canonical beta.6 proof input, without exposing private-key bytes),
`IHostPreferences` (reduced motion, contrast, scale), `INotifications`.

**Rules.**

- Core, the language SDKs and the conformance tooling depend on none of this.
  Core takes bytes and returns bytes; a port is how bytes reach it.
- A port returns a result, never a dialog. Whether to ask a person is the
  surface's decision, and a port that opens a window cannot be tested with a
  fake.
- A capability may be absent. Every port answers "not available here" without
  throwing, and a surface says so in words a person can act on, without naming a
  platform API or leaking a path.
- Nothing crosses a port that is not already a defined APR artifact: bytes, a
  digest, a canonical proof input. A port never takes an `AprDocument`, because a
  host adapter that understands the model is a second implementation of it.
- Signing is opaque. The host returns proof material; the key never crosses the
  seam. This is what lets a platform key store, a smart card and a test key be
  the same port.

**What this decision does not do.** It moves nothing between Desktop and CLI —
they keep their own workflows and their own composition roots. It moves
host-touching code *out of both* into adapters behind one seam. The migration is
#144 and is deliberately not part of this decision: the ADR exists now so that
work written before the migration lands in the shape the migration expects,
rather than being rewritten by it.

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

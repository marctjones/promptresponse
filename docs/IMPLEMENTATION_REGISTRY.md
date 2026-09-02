# Implementation registry

This is the human inventory of shipped and planned PromptResponse surfaces. For concept-level code and test ownership see [Concept registry](CONCEPT_REGISTRY.md); for format requirements see `tests/registry.json`.

| Surface | Responsibility | Status | Gate or evidence |
| --- | --- | --- | --- |
| APR specification | Normative format definition | Single file, 1,900 lines, formal grammar, stable rule identifiers, executable examples | `check-spec-shape.py`, `check-spec-completeness.py` |
| Schema and type registry | Derived projections of the specification | Agreement-checked in both directions; a disagreement is the derived artifact's defect | `check-schema-agrees.py`, `check-schema.py` |
| Conformance corpus | Cross-implementation behavior, derived from the specification's examples | Paired JSONC/YAML forms and streams, digests/manifests, CMS and unsupported proofs, fields scope, witness chains, changed forms, and malformed representations | beta.6 corpus + SDK tests |
| .NET Core | Reference core, CEL, representations, streams, attestations | Beta.6 core+attestations, manifest resolution, CMS verification, and independent attestation creation | focused beta.6 core tests |
| Desktop | Author, fill, review, export, import, attestation display | Beta.6 JSONC/YAML form I/O plus stream-occurrence browser; attestation states are non-gating and trust remains external by design | GUI/accessibility/desktop tests |
| CLI | Validate, fill, review, eval, import/export, stream and attestation inspection | Every runtime command uses beta.6-only form I/O; validate/info/normalize/attest use stream APIs, and retired embedded-signature commands are rejected | CLI tests + release smoke |
| PDF renderer/importer | Flat/fillable/PDF-A export and AcroForm import | Shipped with limits | PDF tests |
| Python SDK and local web demo | Core reading/writing and local demo | Beta.6 core+attestations with digest/manifest/witness resolution and detached CMS content verification; trust is supplied by the caller | Python beta.6 corpus tests + schema gate |
| TypeScript SDK/HTML projection | Browser-capable core and renderer | Beta.6 core+attestations with async detached CMS content verification and resolution; trust is supplied by the caller | shared corpus CI |
| Java SDK and local demo | Core processing and local JDK demo | Beta.6 core+attestations with digest/manifest/witness resolution and detached CMS content verification; trust is supplied by the caller | 41 Java conformance cases |
| Browser extension and mobile | New clients | Planned | roadmap decision required |
| Browser demo | Local beta.6 JSONC/YAML stream viewer | Selects every form occurrence; shows non-gating resolution plus async CMS content verification where available; trust policy remains external | `demos/web` test |
| Hosted collaboration, RBAC, analytics, SSO | Enterprise platform features | Deferred | outside current scope |

## Desktop composition boundaries

This map records the current ownership boundaries inside the desktop client. It is
updated with refactors so file size is never the only indicator of architectural
health.

| Component | Owns | Must not own | Evidence |
| --- | --- | --- | --- |
| `MainShellViewModel` | composition, document/tree lifecycle, command compatibility, screen-level derived state | renderer implementation, file-format semantics, trust policy | desktop shell, GUI, and accessibility tests |
| `SectionViewModel` | section tree projection and structural editing | a second application shell or unrelated document I/O | section/table and undo/redo tests |

Document I/O/export/delivery and table-shape editing are separate services.
`MainShellViewModel` remains their composition boundary; a new
feature must extend the owning service rather than reintroduce those concerns into
the shell.

| Gate | What it prevents |
| --- | --- |
| Three-OS .NET build/test | platform-specific drift |
| Corpus, schema, SDK runners | APR interoperability drift |
| Test registry check | unowned format requirements |
| Accessibility and GUI tests | interaction and palette regressions |
| Coverage thresholds | untested regressions |
| Release smoke checklist | source-versus-artifact gap |

Roadmap priority belongs in [ROADMAP.md](../ROADMAP.md), not in a competing feature tracker.

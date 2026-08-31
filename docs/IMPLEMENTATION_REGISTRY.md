# Implementation registry

This is the human inventory of shipped and planned PromptResponse surfaces. For concept-level code and test ownership see [Concept registry](CONCEPT_REGISTRY.md); for format requirements see `tests/registry.json`.

| Surface | Responsibility | Status | Gate or evidence |
| --- | --- | --- | --- |
| APR specification and schema | Format definition | Beta | corpus/schema CI gates |
| Conformance corpus | Cross-implementation behavior | Shipped | .NET, Python, TypeScript, Java CI |
| .NET Core | Reference core, CEL, CMS signatures | Shipped beta | Core + corpus tests |
| Desktop | Author, fill, review, export, import, signing | Shipped beta | GUI/accessibility/desktop tests |
| CLI | Validate, fill, review, eval, import/export, sign/verify, submit | Shipped beta | CLI tests + release smoke |
| PDF renderer/importer | Flat/fillable/PDF-A export and AcroForm import | Shipped with limits | PDF tests |
| Python SDK and local web demo | Core reading/writing and local demo | Shipped | shared corpus CI |
| TypeScript SDK/HTML projection | Browser-capable core and renderer | Shipped, package split pending | shared corpus CI |
| Java SDK and local demo | Core processing and local JDK demo | Shipped | shared corpus CI |
| Browser extension and mobile | New clients | Planned | roadmap decision required |
| Hosted collaboration, RBAC, analytics, SSO | Enterprise platform features | Deferred | outside current scope |

## Desktop composition boundaries

This map records the current ownership boundaries inside the desktop client. It is
updated with refactors so file size is never the only indicator of architectural
health.

| Component | Owns | Must not own | Evidence |
| --- | --- | --- | --- |
| `MainShellViewModel` | composition, document/tree lifecycle, command compatibility, screen-level derived state | certificate/signature workflow details, renderer implementation, file-format semantics | desktop shell, GUI, and accessibility tests |
| `ViewModels/Signing/SignatureWorkflow` | signature verification/status, field coverage, breakage notice, deliberate removal, certificate-backed signing | XAML ownership, document-tree construction, trust/key-store product expansion | signature, coverage, and breakage tests |
| `SectionViewModel` | section tree projection and structural editing | a second application shell or unrelated document I/O | section/table and undo/redo tests |

Document I/O/export/delivery, table-shape editing, and signature workflow are
separate services. `MainShellViewModel` remains their composition boundary; a new
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

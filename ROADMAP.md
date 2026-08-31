# PromptResponse roadmap

**Current release:** [v1.0.0-beta.3](CHANGELOG.md)

**Format status:** APR `1.0-beta`
**Planning authority:** GitHub milestones and issues; this document states product
direction, not an alternate delivery tracker.

## Direction

PromptResponse replaces page-bound PDF and Word forms with APR: a local-first,
semantic JSON format for reusable templates and filled responses. The core loop is
complete: **create or open a template → fill it with accessible assistance → save
a response → export, hand off, or process structured data**.

The product remains deliberately outside the page-layout, hosted-form-SaaS, and
workflow-engine markets. APR is safe to open, has no executable document content,
keeps responses as strings, and does not store presentation layout.

For the full product boundary, see [Product](docs/PRODUCT.md). For present shipped
surfaces and their evidence, see the [implementation registry](docs/IMPLEMENTATION_REGISTRY.md).

## Current focus: public-beta stabilization

New feature delivery is frozen while the following work improves the reliability
and maintainability of what already ships:

1. **Code architecture** — keep the Core, desktop shell, renderer/importer, CLI,
   and SDK boundaries cohesive; split oversized responsibilities without changing
   APR meaning or observable behavior.
2. **Regression confidence** — preserve corpus compatibility and improve focused
   unit, integration, GUI, package-smoke, and accessibility evidence. Coverage is
   a guardrail, not a substitute for risk-based testing.
3. **Documentation and registries** — maintain one truthful map of product,
   architecture, development workflow, concepts, implementations, and release
   evidence. Superseded plans belong in Git history.
4. **Release provenance and local reproducibility** — make it possible to connect
   release artifacts to validated source and to run the supported development path
   predictably on macOS, Linux, and Windows.

The active work is tracked in GitHub:

- [Code refactoring](https://github.com/marctjones/promptresponse/milestone/13)
- [Test and CI refactoring](https://github.com/marctjones/promptresponse/milestone/14)
- [Documentation and registry refactoring](https://github.com/marctjones/promptresponse/milestone/15)
- [Host architecture refactoring](https://github.com/marctjones/promptresponse/milestone/8)

## Deferred, explicit decisions

These remain outside the stabilization freeze unless a future roadmap decision
changes their priority:

- hosted collaboration, RBAC, analytics, SSO, and cloud-by-default workflows;
- browser extension and mobile clients;
- explicit submission transports and APR streams/attestations;
- native print, trust-store, encryption, notarization, and richer document import;
- Word/Excel export.

The format's optional CEL profile is documented separately as a deliberate draft;
it is not a substitute for the core-format authority.

## What must remain true

- Corpus, schema, and specification stay compatible for the declared beta format.
- Core document opening remains offline and never executes document-supplied code.
- Unknown compatible members survive round trips where the specification requires it.
- Accessibility and keyboard behavior are maintained as product work, with the
  limits of live assistive-technology evidence recorded rather than overstated.
- A release is supported only by reproducible source, tests, and artifact evidence.

See [docs/README.md](docs/README.md) for the canonical documentation map.

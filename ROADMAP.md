# PromptResponse roadmap

**Current format target:** APR `1.0-beta.6`
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

## Current focus: APR `1.0-beta.6` migration and stabilization

APR has not been publicly released. The beta.3 wire contract is therefore not a
compatibility target: beta.6 may make the required breaking changes once, before
we stabilize the implementation around it. In particular, beta.6 replaces the
embedded `signatures` / `apr-sig-v3` model with independent attestation records,
adds JSONC and YAML representations, and introduces representation-neutral
streams.

Stabilization is part of this migration, not work that precedes it:

1. **Contract first** — settle the beta.6 specification, schema, semantic-digest
   and attestation vectors, and language-neutral conformance corpus.
2. **Reference core and SDKs** — implement the settled core contract, then bring
   Python, TypeScript, and Java through the same executable corpus.
3. **Clients** — upgrade CLI before desktop and the web demo; clients consume
   stream and attestation APIs and must not invent their own semantics.
4. **Evidence while migrating** — add focused core, SDK, CLI, GUI, accessibility,
   and package-smoke coverage as each layer moves. Only after every surface is on
   beta.6 do cross-platform release and maintainability gates become the final
   stabilization pass.

The active work is [APR 1.0 beta.6 upgrade — streams and
attestations](https://github.com/marctjones/promptresponse/milestone/18). Existing
refactoring and dependency milestones support this work; they must not stabilize
the retired beta.3 contract.

## Deferred, explicit decisions

These remain outside the stabilization freeze unless a future roadmap decision
changes their priority:

- hosted collaboration, RBAC, analytics, SSO, and cloud-by-default workflows;
- browser extension and mobile clients;
- explicit submission transports beyond the beta.6 stream and attestation
  contract;
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

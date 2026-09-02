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

## Current focus: APR `1.0-beta.6`

APR has not been publicly released. The beta.3 wire contract is therefore not a
compatibility target: beta.6 makes the required breaking changes once, before we
stabilize around it. Beta.6 replaces the embedded `signatures` / `apr-sig-v3`
model with independent attestation records, adds the APR-JSONC and APR-YAML
representations, and introduces representation-neutral streams.

### Where the contract stands

The specification is normative, and the schema, the type registry, and the
conformance corpus are derived from it. A derived artifact that disagrees with
the specification has the defect, as does an implementation. That ordering is
enforced rather than declared:

- the corpus is **generated** from executable examples embedded in the
  specification, and CI fails if the two diverge;
- the schema is **agreement-checked** in both directions against the
  specification's member tables;
- the type registry is checked against the registry section;
- every normative clause carries a stable rule identifier, and coverage is
  counted per rule rather than per section.

Core, the Python, TypeScript and Java SDKs, the CLI, the desktop client, and the
web demo all read and write both representations, iterate streams, and resolve
attestations without gating data on them. The same executable examples run in all
four implementations.

### What remains

1. **Evidence** — Core line coverage is below its gate, and the manifest vectors
   for changed member kinds have no corpus entry.
2. **Specification apparatus** — a review and release checklist, and a build that
   produces tagged baselines identifying the exact specification, schema,
   registry and corpus set.
3. **Stabilization** — only after those do cross-platform release and
   maintainability gates become the final pass.

Planning lives in [the specification
milestone](https://github.com/marctjones/promptresponse/milestone/19) and [the
beta.6 upgrade milestone](https://github.com/marctjones/promptresponse/milestone/18).
Existing refactoring and dependency milestones support this work; they must not
stabilize the retired beta.3 contract.

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

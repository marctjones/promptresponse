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

1. **Scoring the other three SDKs.** Conformance is asked two ways, and only one
   of them is hard. A suite that *exercises* an implementation checks it reads the
   corpus without failing. A suite that *scores* it withholds the answers, demands
   a named diagnostic for every rejection and compares a computed digest per case.
   The .NET library, the command line, the desktop client and the PDF exporter are
   scored. Python, TypeScript and Java are exercised, and each of the three passed
   its own suite for weeks while unable to read a beta.6 document at all.
2. **Reaching every rule.** 84 of 166 rules are fully evidenced — enforced by a
   check, shown satisfied, shown violated, and the violation caught. The rest are
   reached partly or not at all, and each gap is recorded rather than rounded up.
3. **Gates that decide what they claim to.** A skipped step, an unapplied fixture
   and a threshold set below what already passes all report the same green as a
   working check. Several were found this way and the class is not exhausted.
4. **Stabilization** — only after those do cross-platform release and
   maintainability gates become the final pass.

Planning lives in GitHub milestones and issues, which this document does not
restate. Existing refactoring and dependency milestones support this work; they
must not stabilize the retired beta.3 contract.

## Deferred, explicit decisions

These remain outside the stabilization freeze unless a future roadmap decision
changes their priority:

- hosted collaboration, RBAC, analytics, SSO, and cloud-by-default workflows;
- browser extension and mobile clients;
- explicit submission transports beyond the beta.6 stream and attestation
  contract;
- native print, trust-store, encryption, notarization, and richer document import;
- Word/Excel export.

The expressions profile is normative in the specification's section 11, pinned to
a CEL release, with no extension library and no custom function.

One consequence of that pin is a known limitation of 1.0, recorded rather than
worked around: **a dynamic table cannot have a computed total.** A fixed table
totals by naming its cells; a table whose rows a filler may add has no expressible
fold, because the pinned CEL surface has no reduce. Every way out reopens a
decision this baseline made on purpose, so 1.0 ships without it and a later
baseline decides. The question and its options are #337.

## What must remain true

- Corpus, schema, and specification stay compatible for the declared beta format.
- Core document opening remains offline and never executes document-supplied code.
- Unknown compatible members survive round trips where the specification requires it.
- Accessibility and keyboard behavior are maintained as product work, with the
  limits of live assistive-technology evidence recorded rather than overstated.
- A release is supported only by reproducible source, tests, and artifact evidence.

See [docs/README.md](docs/README.md) for the canonical documentation map.

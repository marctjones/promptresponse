# PromptResponse roadmap

**Current format target:** APR `1.0-beta.7`, which a reader accepts alongside
`1.0-beta.6`
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

## Current focus: APR `1.0-beta.7`

APR has not been publicly released. The beta.3 wire contract is therefore not a
compatibility target: the beta line makes the required breaking changes once,
before we stabilize around it. Beta.6 replaces the embedded `signatures` /
`apr-sig-v3` model with independent attestation records, adds the APR-JSONC and
APR-YAML representations, and introduces representation-neutral streams. Beta.7
adds the object form of a `submissionUrls` entry, so a presigned browser POST can
carry its policy; it moves no other member, which is why a reader accepts both
versions.

The readers are at beta.6. Aligning them with what beta.7 states — the version
pair, the hidden-character warning codes outside human-facing text, the object
submission entry, and the diagnostic for an absent `aprVersion` — is the code
pass that follows the specification change, and the precondition for any of it
being scored.

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

Scoring is complete: the .NET library, the command line, the desktop client, the
PDF exporter, and the Python, TypeScript and Java SDKs all answer the suite with
zero failures. Conformance is asked two ways. A suite that *exercises* an
implementation checks it reads the corpus without failing; a suite that *scores*
it withholds the answers and compares a computed digest and a named diagnostic
per case. A passing score has one limit: the SDKs share a design with the
reference oracle, so it shows they agree, not that they are right. Items 1 and 2
below are bugs that agreement hid.

In order:

1. **Fix what agreement hid.** The JSONC trailing-comma stripper rewrites string
   contents in the oracle and all three SDKs (a response `"1,]"` reads back as
   `1]`), so a response is no longer byte-for-byte what was written (#550). The
   corpus needs a case with that shape, then the fix everywhere. With it:
   Python boolean binding (#512), naive datetimes binding through the host time
   zone (Python, TypeScript; #551), the Java driver aborting on deep nesting, a JSON
   array record reported as a mixed stream (#510), multichoice binding (#515),
   `string(duration)` (#544), and blank submission members (#545).
2. **Readers accept `1.0-beta.7`** (#549). The specification describes beta.7 and no
   example, reader or validator carries it; every reader accepts beta.6 only, and
   the Desktop app sniffs the literal string. Collapse each reader's version
   check onto one constant, then add the version pair, the hidden-character
   warning codes, and the diagnostic for an absent `aprVersion`, with an example
   at the current version. This is the precondition for scoring beta.7.
3. **Reach every rule.** The specification carries 356 rules and the suite 327
   cases. 207 rules are named outright by a case; crediting each case with every
   rule in the section it cites reaches 319. 37 are reached by nothing, 3 of those
   with no recorded gap (`scripts/check-suite-coverage.py --gaps`).
4. **Gates that decide what they claim to.** The example-coverage gate accepts
   258 of 356 rules as excepted and checks only that a reason was typed, and a
   bare file path as `coveredBy` (#546). Stream cases withhold their digests, so a
   reader that accepts everything and reads nothing passes 18 valid cases. The
   driver-teeth check tries one mutation per SDK. The GUI activation floor counts
   by label (#391). The ROADMAP and conformance figures are stated by hand and
   checked by nothing. Each is the same defect: a green check that measures
   nothing.
5. **Attestations: claim what is implemented.** The Python, TypeScript, Java and .NET
   libraries all verify CMS proofs, but none claims the profile (#439). No driver
   reports `verified` (#538), so chapter 12 is scored against the oracle alone. Python requires a P-256 key and SHA-256; Java and
   TypeScript accept any signer or algorithm, and the .NET verifier does not bind
   the proof type to the algorithm.
6. **Safe-to-open evidence.** The parser fuzz tests exercise the typed serializer,
   not the hand-written JSONC and YAML scanner every real file passes through.
   No SDK sets an expression-evaluation budget (#542, #358).
7. **Specification defects found in review**, in the specification and then in its
   derived artifacts: the JSON Schema rejects a `kind` and `documentType` the
   specification accepts; `1.0-beta.6` is stated as normative in one line and
   derived in another; expressions cannot name table cells, so a fixed table
   cannot be totalled either (the dynamic-table case is #337); date and time
   binding and double formatting in the expressions profile are underspecified;
   the comment grammar has no derivation for `/***/`; the key-attachment rule for
   submission transports does not fit a pre-signed request.
8. **Stabilization** — the accessibility proof, host-port rules, supply-chain
   controls and desktop workflows. These are the final pass before a release
   candidate, and they follow items 1 to 6 rather than run beside them.

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

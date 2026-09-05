# SDK Conformance

<!-- AI-ASSISTANT-README -->
Use this when changing the APR format, serializers, validators, or any SDK.
It defines what an implementation must do to be called conformant, and how that
claim is measured. The conformance suite and the driver contract live here.
<!-- END-AI-ASSISTANT-README -->

## What decides

The specification decides. [`docs/APR_SPECIFICATION.md`](APR_SPECIFICATION.md) is
normative and everything below is derived from it: where a derived artifact
disagrees with the document, the artifact has the defect, and so does an
implementation.

| Artifact | Role |
| --- | --- |
| `docs/APR_SPECIFICATION.md` | **Normative.** Defines the format completely. |
| `tests/Conformance/beta6/suite.json` | Every conformance vector, self-contained. Generated. |
| `schemas/apr-1.0-beta.6.schema.json` | A machine-checkable projection of the structural subset. |
| `schemas/apr-types-1.0.json` | The type registry, projected. |
| `docs/release/apr-oscal-catalog.json` | Every rule identifier as an OSCAL control. |
| `docs/release/specification-manifest.json` | The exact bytes a baseline publishes. |

## Validating an implementation

An implementation is measured by answering questions, not by being written in a
particular language or wired into this repository's test projects. It supplies a
**driver**: a program that reads the suite on standard input and writes what it
did on standard output.

```bash
python3 scripts/run-conformance.py --driver "./my-sdk-driver"
python3 scripts/run-conformance.py --driver "./my-sdk-driver" --json
python3 scripts/run-conformance.py --driver "./my-sdk-driver" --oscal results.json
```

`scripts/reference-driver.py` is a worked example in about sixty lines. A real
driver is the same shape in whatever language the implementation is written in.

### The driver contract

**Input** on stdin is `tests/Conformance/beta6/suite.json` verbatim. Each case
carries `id`, `representation` (`jsonc`, `yaml`, `jsonc-stream`, `yaml-stream`)
and `document`, the source text. Stream cases carry real record separators.

**Output** on stdout is one JSON object:

```json
{
  "implementation": { "name": "…", "version": "…", "profiles": ["core"] },
  "results": [
    { "id": "spec:table-section", "outcome": "valid" },
    { "id": "spec:yaml-tag", "outcome": "reject", "diagnostic": "YAML_TAG_FORBIDDEN" },
    { "id": "corpus:forms/permit.apr.jsonc", "outcome": "valid", "digest": "sha256:…" }
  ]
}
```

- `outcome` is `valid` when the document was read and no error was found, and
  `reject` when it was refused. A case expecting rejection accepts either a parse
  failure or a validation failure.
- `diagnostic` is the code reported. Where the suite names one, a different code
  is recorded as a discrepancy rather than a failure: a document can be refused
  for the right reason under another name.
- `digest` is the `jcs-sha256` semantic digest, and reporting it is how a case
  proves more than acceptance. Most valid cases state the digest the document
  must produce, and reporting a different one fails the case even though you
  accepted the document. That is where a reader whose scalar resolution is wrong
  is caught. Omitting it is allowed and skips the check, which weakens your score
  rather than improving it.
- A case a driver omits is reported as unanswered, never as failed.

**What this cannot check** is whether a profile you claim is a profile you
implement. Declaring conformance stays a statement a person makes, as the
specification's conformance section says.

## Profiles

`core` is required of every implementation. `core+streams`, `core+attestations`
and `core+expressions` are optional and independent, and each submission
transport (`https`, `mailto`) is claimed separately. State what you implement and
the corpus commit you pass:

> APR 1.0-beta.6 core+streams, submits https, corpus beta6 @ `<sha>`

Each profile has a checklist in the specification. Work through it; the suite
does not cover everything a checklist states, and the gap is named per rule in
`tests/registry.json`.

## Gates that hold the artifacts themselves honest

These run in CI and are part of the specification review checklist. They check
the artifacts, not an SDK.

```bash
python3 scripts/check-schema.py         # corpus and examples validate against the schema
python3 scripts/check-corpus.py         # every digest in the corpus is the digest it claims
python3 scripts/build-corpus.py         # the corpus is what regenerating it would produce
python3 scripts/build-suite.py          # the suite is current
python3 scripts/validate-apr.py --spec-examples   # the examples agree with a validator
python3 scripts/run-conformance.py --driver "python3 scripts/reference-driver.py"
python3 scripts/check-rule-evidence.py  # per-rule: enforced, satisfied, violated, caught
```

`check-rule-evidence.py` is the one that says whether a rule is genuinely
covered. A rule needs all four: a check that enforces it, a case showing a
document that satisfies it, a case showing one that violates it, and that
violation actually being caught traceably. It gates two invariants — a case may
not cite a rule the catalogue lacks, and a case expecting rejection must be
refused for the rule it names — and ratchets the four counts so coverage cannot
fall.

`scripts/validate-apr.py FILE…` validates any APR document against the
specification's error and warning tables, by the codes the specification names.
Use it before adding an example. It reads its member vocabulary from the
specification's own member tables, so it follows the document rather than the
schema.

## Regenerating the corpus

Attestations are derived: subject digests, manifest roots, entry digests,
witnesses and the CMS proof are all computed over other records. Never edit one
by hand.

```bash
python3 scripts/build-corpus.py --write
python3 scripts/build-suite.py --write
```

What is derived, and what each attestation asserts, is declared in
`tests/Conformance/beta6/corpus.map.json`. Signing uses the committed test key in
`tests/Conformance/beta6/keys/`, which secures nothing and exists so the signed
fixture can be rebuilt. Signatures are deterministic, so regenerating an
unchanged corpus reproduces it byte for byte.

## Implementation status

The specification moved substantially during the beta.6 baseline work: native
JSON types for structural members, the `signature` and `file` types removed, a
reverse-DNS prefix required on extension members, submission transports defined,
`templateId` as a URI, `metadata.regarding`, and the format-version member
renamed to `aprVersion`.

**No SDK is currently aligned to it.** The Python suite fails 26 of 109 tests
against the regenerated corpus; the .NET, TypeScript and Java suites have not
been re-run since those changes and are expected to fail the same way. The
alignment pass is deliberate outstanding work, and until it lands no SDK should
claim any profile.

## Required behaviours

Read both representations, preserve independent stream records, hold the digest
and manifest relations, reject a root `signatures` member, reject any
`aprVersion` other than `1.0-beta.6` at parse and write boundaries, and preserve
every response byte for byte across a round trip.

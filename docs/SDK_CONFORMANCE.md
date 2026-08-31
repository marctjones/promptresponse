# SDK Conformance

<!-- AI-ASSISTANT-README -->
Use this when changing the APR format, serializers, validators, or non-.NET SDKs.
It defines the shared fixture corpus and schema gate all SDKs are held to.
<!-- END-AI-ASSISTANT-README -->

## Beta.6 conformance status

The active contract is `tests/Conformance/beta6/`,
`schemas/apr-1.0-beta.6.schema.json`, and the beta.6 specification. The schema
gate validates JSONC/YAML forms, independent attestation records, and streams.

| SDK | Verified beta.6 profile | Known gap |
|---|---|---|
| .NET | `core+attestations`, including detached CMS verification and attestation creation | certificate trust policy is intentionally caller-configured |
| Python | `core+attestations`, including detached CMS content verification | certificate trust is a caller policy, separate from content validity |
| TypeScript | `core+attestations`, including async detached CMS content verification and resolution | certificate trust is a caller policy, separate from content validity |
| Java | `core+attestations`, including detached CMS content verification | certificate trust is a caller policy, separate from content validity |

No SDK may claim `core+attestations` until it passes the complete beta.6 corpus,
including CMS, changed-form, and fields-scope vectors. Beta.6 is the only supported
wire format; older APR documents must be rejected.

## The two gates

```bash
# Reference implementation behaves correctly
dotnet test tests/PromptResponse.Core.Tests --filter FullyQualifiedName~Beta6

# Published schema agrees with the fixtures (any language, no .NET required)
pip install jsonschema && python3 scripts/check-schema.py
```

## Repeatable compliance benchmark

Run `scripts/benchmark-beta6-compliance.sh` from a normal local terminal to
measure and execute the beta.6 schema, SDK, CMS, and desktop stream gates in
one command. It is intentionally beta.6-only: an older corpus or wire version
is not a fallback test target.

## Required behaviours

SDKs must accept JSONC and YAML beta.6 forms, preserve independent stream records,
validate canonical digest and manifest relations, reject root `signatures`, and reject
every version other than `1.0-beta.6` at parse and write boundaries.

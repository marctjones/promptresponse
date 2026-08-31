# APR beta.6 attestations

APR beta.6 does not embed signatures in a form. A form remains complete semantic
data on its own; a separate attestation stream record can make a CMS assertion
about that form. This lets clients carry multiple attestations without changing
the form they verify.

An attestation contains the subject form digest, an integrity manifest, optional
field scope, CMS proofs, and witness envelope digests. Its CMS proof signs the
JCS serialization of the attestation after omitting `proofs`. The supported
proof type is `cms/ecdsa-p256-sha256`: detached CMS SignedData, ECDSA P-256, and
SHA-256 with the signer certificate chain included.

Cryptographic validity and certificate trust are deliberately separate. A valid
self-signed proof says its content was signed by that certificate; it does not
establish a trusted identity. Attestation results never prevent parsing,
rendering, export, or data extraction.

## CLI

Use an existing ECDSA P-256 PKCS#12 certificate to append an independent
attestation. The input stream must contain exactly one subject form and the
output path is required so the original data remains available unchanged.

```bash
apr attest permit.apr --cert=signer.pfx --password=secret --output=permit.attested.apr
apr attest permit.apr --cert=signer.pfx --password=secret --fields=name --output=permit.attested.apr
apr info permit.attested.apr --json
```

The `--fields` form records selected prompt ids. Its manifest still contains the
required prompt and ancestor-section context; callers can inspect the result
with `info` without selecting a form occurrence by position.

## Desktop and web

The desktop client reads beta.6 JSONC/YAML streams, lets the user select a form
occurrence, and displays non-gating attestation status. The local web demo does
the same and verifies the content of supported CMS proofs asynchronously in the
browser. Certificate trust policy is intentionally external to both clients.

## Historical beta.3 signing

The previous `apr-sig-v3` model and the `apr keygen`, `apr sign`, and `apr
verify` commands are retired. They are not compatible with beta.6 forms and are
not instructions for creating or verifying beta.6 data.

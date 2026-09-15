# APR beta.6 conformance corpus

This directory is the executable authority for APR beta.6.

The vectors pin the new, cross-language seams before host code moves:

- `forms/` supplies the same semantic form in JSONC and YAML;
- `digests/` supplies its RFC 8785 JCS SHA-256 values; and
- `attestations/` supplies independent records that resolve their subject by
  semantic digest rather than by stream position or filename.
- `streams/` supplies out-of-order attestations and duplicate complete forms;
- `malformed/` supplies invalid framing or representation inputs.

Every implementation must parse the two form representations to the same model,
produce the published document and leaf digests, preserve an independently
encountered form occurrence, and keep an attestation separate from the form.
`proofs: []` deliberately has no validity claim: it tests record resolution and
manifest shape only.

A stream fixture's expectation in `corpus.map.json` may carry `verify`: what a
verifier must report for each attestation in it. A document is valid whatever its
attestations say, so these are the vectors that separate a verifier resolving a
subject by digest from one resolving it by position.

`streams/cms-proof.apr.jsonc` holds the permit beside a CMS-signed attestation of
it. The proof verifies, so the attestation is `valid`, and its self-signed
certificate is not trusted: the two facts are reported apart.

`streams/unsupported-proof.apr.jsonc` holds the permit beside an attestation whose
only proof is of a type no verifier recognizes. It is `unverifiable`, never
`invalid`.

`streams/fields-changed-form.apr.jsonc` holds a fields-scope attestation beside a
form edited outside the attested field. That form is not the subject, so the
attestation is `unresolved`, not `invalid`.

`streams/witnessed.apr.jsonc` pins a witness reference to an exact proof-free
attestation envelope. Its stream order is still non-semantic: the form follows
both attestations deliberately.

`streams/changed-form.apr.jsonc` pins the opposite case: a fully copied form
with one changed response has a new subject digest, so an earlier attestation
must report `unresolved` rather than transferring to it.

`malformed/duplicate-member.apr.jsonc` requires JSONC readers to reject duplicate
object members instead of applying a parser's silent last-key-wins behavior.

`attestations/permit.cms.attestation.jsonc` is a detached ECDSA P-256 CMS proof
over the exact proof-free permit envelope. A CMS-capable reader must verify it;
a reader without CMS support must preserve it and report `unverifiable`.

`attestations/permit.fields.attestation.jsonc` is the positive fields-scope
case. Its manifest binds the selected prompt and response plus the required
ancestor-section context; without a recognized proof it resolves as
`unverifiable`, not invalid.

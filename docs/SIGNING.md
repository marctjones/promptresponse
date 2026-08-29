# Signing APR forms

APR documents can carry **verifiable digital signatures** using industry-standard
**CMS / PKCS#7** over X.509 certificates (the same cryptographic standards PDF and
government PKI use). Unlike PDF signing, APR signs the form's **content/meaning**,
not the rendered bytes — so a signature survives re-serialization and re-export,
is **field-scoped**, and supports **multiple signers** who each sign the part they
filled.

> **Beta boundary.** `apr-sig-v3` verifies that covered content has not changed,
> but it does not record a complete manifest of fields visible when a signer
> acted. Until the `apr-sig-v4` witnessed-manifest profile is specified and
> implemented ([#88](https://github.com/marctjones/promptresponse/issues/88)),
> use APR signatures for integrity and provenance experiments, not as the sole
> evidence for an external high-stakes workflow.

Two signature roles:
- **Publisher** signs the *template* (the form definition) and **binds the
  submission URL**, so it can't be redirected without invalidating the signature.
- **Filler** signs the *responses* in a chosen scope. Multiple fillers can each
  sign their own fields; editing a covered field invalidates only the signatures
  that cover it.

Trust works two ways, matching real deployments:
- **CA-issued certificate** (Federal PKI / PIV-CAC / org CA / eIDAS) — trusted by
  chaining to a configured root.
- **Self-signed certificate** — trusted by **pinning** its public cert.

> Crypto: ECDSA P-256 + SHA-256 (FIPS-approved), built-in .NET. This is the
> CAdES-BES tier; trusted timestamps (RFC 3161) and long-term validation are
> planned additive layers. See [issue #73](https://github.com/marctjones/promptresponse/issues/73).

## End-to-end (CLI)

```bash
# 1) Publisher: make a signing certificate (or use a CA-issued .pfx / PIV card)
apr keygen --name="Town of Bloomfield" --output=publisher.pfx --cert-out=publisher.cer

# 2) Publisher signs the template and binds the submission URL
apr sign permit.aprt --publisher --cert=publisher.pfx \
    --url="https://bloomfieldct.gov/permit/submit"

# 3) A person fills the form, then signs the fields they completed
apr keygen --name="Ada Lovelace" --output=ada.pfx
apr sign permit.aprf --fields=applicant_name,dob --cert=ada.pfx --id=ada

# 4) Verify — pin the publisher's public cert as a trust anchor
apr verify permit.aprf --trust=publisher.cer
```

`verify` prints, per signature, whether the covered content is intact and how far
the certificate is trusted (`trusted` / `self-signed` / `untrusted` / `INVALID`),
and exits non-zero if any signed content was altered.

## Desktop

With a form open, **File → Sign**:
- **Sign as publisher…** — pick your `.pfx`, enter the submission URL; signs the
  form definition and binds the URL.
- **Sign my responses…** — signs the fields you've filled in.

The right-rail **Signatures** panel shows each signature's role, signer, scope,
and trust status (with a **Re-verify** button). (In-GUI trust pinning / CA
configuration is a follow-up; self-signed certs show as such.)

## Commands

### `keygen`
```
apr keygen --name="<signer>" --output=<file.pfx> [--password=<pw>] [--cert-out=<file.cer>] [--years=<n>]
```
Generates a self-signed ECDSA P-256 signing certificate. Share the `.cer` so
others can pin it. (For real PKI, skip this and use a CA-issued `.pfx` or a smart
card.)

### `sign`
```
apr sign <file> --publisher --cert=<file.pfx> [--password=<pw>] --url=<submitUrl[,submitUrl...]> [--id=<id>] [--output=<file>]
apr sign <file> --fields=<id1,id2,...> --cert=<file.pfx> [--password=<pw>] [--id=<id>] [--output=<file>]
```
Appends a signature to the document. Publisher signing also records
`metadata.submissionUrls` (and binds every entry into the signature). Writes in place unless
`--output` is given.

### `verify`
```
apr verify <file> [--trust=<anchor1.cer,anchor2.cer>] [--check-revocation]
```
Verifies all signatures against the document's current content. `--trust` supplies
CA roots and/or pinned self-signed certs; `--check-revocation` enables OCSP/CRL
(needs network). Exit code is non-zero if any signature is invalid.

## Library

```csharp
using PromptResponse.Core.Signing;
doc.Metadata.SubmissionUrls = ["https://example.org/submit"];
var sig = AprSigner.SignTemplate(doc, cert, DateTime.UtcNow);
doc.Signatures = [sig];
var results = AprVerifier.VerifyAll(doc, new AprTrustOptions { TrustAnchors = [publisherCert] });
```

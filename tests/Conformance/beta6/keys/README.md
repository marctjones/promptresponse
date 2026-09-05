# Corpus signing key

**This private key is published on purpose and secures nothing.**

`corpus-test-key.pem` and `corpus-test-cert.pem` sign the one conformance
fixture that carries a real CMS proof. They are committed so that
`scripts/build-corpus.py` can regenerate that fixture whenever the records it
covers change. Before they existed, any specification change touching a member
name broke the corpus permanently: every digest moved, and the signed fixture
could not be re-signed by anyone.

A proof made with this key demonstrates cryptographic content validity and
nothing else, which is exactly what the specification says a proof establishes.
The certificate is self-signed, so it also demonstrates the separation the
specification insists on: a perfectly valid proof whose signer is nobody.

Never use it for anything but this corpus. Never add it to a trust store.

Signatures are deterministic (RFC 6979) and the certificate's serial number and
validity window are fixed, so regenerating an unchanged corpus reproduces it
byte for byte.

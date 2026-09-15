#!/usr/bin/env python3
"""Verify attestations as the specification's verification vocabulary states it.

The reference driver answers what verification found with this, and like the rest of
`scripts/` it imports no SDK: it is what an SDK's verifier is measured against, so it
cannot be one of them. It follows the vocabulary table row by row. A subject is found
by its digest alone, a proof type nothing here recognizes is `unverifiable`, and
whether a proof verifies is reported beside whether its certificate is trusted,
never folded into it.

Nothing here holds a trust store, so no certificate is ever reported as trusted. That
is the honest answer for a verifier with no trust policy, and it is what makes the
separation between the two facts decidable: a self-signed proof that verifies must
come back verifying and untrusted.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

CMS = "cms/ecdsa-p256-sha256"


def verify(records: list) -> list[dict]:
    """One report per attestation record, in the order the records occur."""
    forms: dict[str, object] = {}
    for record in records:
        if not aprlib.is_attestation(record):
            forms.setdefault(aprlib.digest(record), record)
    attestations = [record for record in records if aprlib.is_attestation(record)]
    envelopes = {aprlib.envelope_digest(record) for record in attestations}
    return [report(attestation, forms, envelopes) for attestation in attestations]


def report(attestation: dict, forms: dict, envelopes: set[str]) -> dict:
    # Each result is reported independently of the others, so an attestation can be
    # unverifiable and witnessed at once. [APR-ATTEST-018]
    proofs = [proof_report(attestation, proof) for proof in attestation.get("proofs") or []]
    # By digest alone, never by stream position, filename or document id. [APR-ATTEST-017]
    subject = attestation.get("subject") if isinstance(attestation.get("subject"), dict) else {}
    form = forms.get(subject.get("digest"))
    if form is None:
        # Not a failure of the assertion: its subject is not here. A changed form is
        # not its subject either, so nothing transfers to it. [APR-ATTEST-051] [APR-ATTEST-009]
        state = "unresolved"
    elif differs(form, attestation.get("manifest")):
        state = "invalid"
    elif any(proof["verifies"] for proof in proofs):
        state = "valid"
    elif any(proof["type"] == CMS for proof in proofs):
        state = "invalid"
    else:
        state = "unverifiable"
    witnesses = attestation.get("witnesses") or []
    return {"state": state,
            "witnessed": any(isinstance(w, str) and w in envelopes for w in witnesses),
            "proofs": proofs}


def differs(form, manifest) -> bool:
    """Does the resolved subject differ from what the manifest attests?"""
    if not isinstance(manifest, dict) or manifest.get("root") != aprlib.digest(form):
        return True
    for entry in manifest.get("entries") or []:
        path = entry.get("path") if isinstance(entry, dict) else None
        value = aprlib.resolve_pointer(form, path) if isinstance(path, str) else aprlib.MISSING
        if value is aprlib.MISSING or aprlib.digest(value) != entry.get("digest"):
            return True
    return False


def proof_report(attestation: dict, proof) -> dict:
    kind = proof.get("type") if isinstance(proof, dict) else None
    # A type nothing here recognizes does not verify, and the attestation it belongs
    # to is unverifiable rather than invalid. [APR-ATTEST-008]
    verifies = kind == CMS and cms_verifies(attestation, proof)
    # No trust store, so nothing is trusted, whatever the signature says. [APR-ATTEST-010]
    return {"type": kind, "verifies": verifies, "trusted": False}


def cms_verifies(attestation: dict, proof: dict) -> bool:
    """Does a detached CMS ECDSA P-256 SHA-256 proof verify over this envelope?"""
    try:
        from asn1crypto import cms
        from cryptography import x509
        from cryptography.exceptions import InvalidSignature
        from cryptography.hazmat.primitives import hashes
        from cryptography.hazmat.primitives.asymmetric import ec
    except ImportError as exc:
        raise aprlib.MissingDependency(
            "asn1crypto and cryptography are required to verify a CMS proof: "
            "pip install -r scripts/requirements.txt") from exc

    if not isinstance(proof.get("value"), str):
        return False
    try:
        container = cms.ContentInfo.load(base64.b64decode(proof["value"], validate=True))
        if container["content_type"].native != "signed_data":
            return False
        signed = container["content"]
        # Detached: the payload travels beside the container, never inside it.
        if signed["encap_content_info"]["content"].native is not None:
            return False
        if len(signed["signer_infos"]) != 1:
            return False
        signer = signed["signer_infos"][0]
        if (signer["digest_algorithm"]["algorithm"].native != "sha256"
                or signer["signature_algorithm"]["algorithm"].native != "sha256_ecdsa"):
            return False
        attributes = signer["signed_attrs"]
        if attributes.native is None:
            return False
        # Over the JCS serialization of the record without its proofs. [APR-ATTEST-036]
        payload = aprlib.canonicalize(
            {k: v for k, v in attestation.items() if k != "proofs"}).encode("utf-8")
        declared = next((a["values"][0].native for a in attributes
                         if a["type"].native == "message_digest" and len(a["values"])), None)
        if not isinstance(declared, bytes) or not hmac.compare_digest(
                declared, hashlib.sha256(payload).digest()):
            return False
        certificate = signer_certificate(signed, signer)
        if certificate is None:
            return False
        key = x509.load_der_x509_certificate(certificate.dump()).public_key()
        if not isinstance(key, ec.EllipticCurvePublicKey) or key.curve.name != "secp256r1":
            return False
        # CMS signs the DER SET OF signed attributes, not the IMPLICIT [0] they travel in.
        key.verify(signer["signature"].native, attributes.untag().dump(),
                   ec.ECDSA(hashes.SHA256()))
        return True
    except (ValueError, TypeError, KeyError, InvalidSignature):
        return False


def signer_certificate(signed, signer):
    identifier = signer["sid"]
    if identifier.name != "issuer_and_serial_number" or signed["certificates"].native is None:
        return None
    wanted = identifier.chosen
    for choice in signed["certificates"]:
        if choice.name != "certificate":
            continue
        tbs = choice.chosen["tbs_certificate"]
        if (tbs["serial_number"].native == wanted["serial_number"].native
                and tbs["issuer"].dump() == wanted["issuer"].dump()):
            return choice.chosen
    return None

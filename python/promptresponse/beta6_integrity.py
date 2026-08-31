"""Semantic JCS-style digests, manifests, and non-gating beta.6 resolution."""

import base64
import hashlib
import json
from hmac import compare_digest
from typing import Any, Iterable

from asn1crypto import cms
from cryptography import x509
from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec

from .beta6 import Beta6AttestationRecord, Beta6FormRecord, Beta6Record
from .errors import AprParseError
from .serialization import dumps

CANONICALIZATION = "jcs-sha256"
CMS_ECDSA_P256_SHA256 = "cms/ecdsa-p256-sha256"


def form_value(document) -> dict[str, Any]:
    """Return the complete semantic form object, including extension data."""
    return json.loads(dumps(document))


def canonicalize(value: Any) -> str:
    """Canonical JSON for APR's JSON subset (sorted member names, no source trivia)."""
    if value is None or isinstance(value, (bool, int, float, str)):
        if isinstance(value, float) and (value != value or value in (float("inf"), float("-inf"))):
            raise AprParseError("APR semantic digests require finite JSON numbers")
        return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    if isinstance(value, list):
        return "[" + ",".join(canonicalize(item) for item in value) + "]"
    if isinstance(value, dict):
        return "{" + ",".join(json.dumps(str(key), ensure_ascii=False, separators=(",", ":")) + ":" + canonicalize(value[key]) for key in sorted(value)) + "}"
    raise AprParseError("APR semantic digests require JSON values")


def digest(value: Any) -> str:
    return "sha256:" + hashlib.sha256(canonicalize(value).encode("utf-8")).hexdigest()


def create_manifest(value: Any) -> dict[str, Any]:
    entries: list[dict[str, str]] = []
    def visit(current: Any, path: str) -> None:
        entries.append({"path": path, "digest": digest(current)})
        if isinstance(current, list):
            for index, child in enumerate(current): visit(child, path + "/" + str(index))
        elif isinstance(current, dict):
            for key in sorted(current): visit(current[key], path + "/" + str(key).replace("~", "~0").replace("/", "~1"))
    visit(value, "")
    return {"root": digest(value), "entries": entries}


def attestation_envelope_digest(value: dict[str, Any]) -> str:
    return digest({key: item for key, item in value.items() if key != "proofs"})


def resolve_attestations(records: Iterable[Beta6Record]) -> list[dict[str, Any]]:
    """Resolve subject/manifest/witness facts and recognized detached CMS content proofs."""
    items = list(records)
    forms = {digest(_record_form_value(item)): _record_form_value(item) for item in items if isinstance(item, Beta6FormRecord)}
    envelopes = {attestation_envelope_digest(item.value) for item in items if isinstance(item, Beta6AttestationRecord)}
    results = []
    for item in items:
        if not isinstance(item, Beta6AttestationRecord): continue
        subject = item.value.get("subject")
        if not isinstance(subject, dict) or not isinstance(subject.get("digest"), str): raise AprParseError("beta.6 attestation subject.digest is required")
        witnesses = sum(1 for witness in item.value.get("witnesses", []) if isinstance(witness, str) and witness in envelopes)
        form = forms.get(subject["digest"])
        if form is None:
            results.append({"value": item.value, "state": "unresolved", "differingPaths": [], "witnessesResolved": witnesses}); continue
        actual, asserted = create_manifest(form), item.value.get("manifest", {})
        differing = [] if isinstance(asserted, dict) and asserted.get("root") == actual["root"] else [""]
        actual_by_path = {entry["path"]: entry["digest"] for entry in actual["entries"]}
        if isinstance(asserted, dict) and isinstance(asserted.get("entries"), list):
            for entry in asserted["entries"]:
                path = entry.get("path") if isinstance(entry, dict) else "?"
                if not isinstance(entry, dict) or actual_by_path.get(path) != entry.get("digest"): differing.append(path if isinstance(path, str) else "?")
        _validate_fields_scope(form, item.value, {entry.get("path") for entry in asserted.get("entries", []) if isinstance(entry, dict) and isinstance(entry.get("path"), str)} if isinstance(asserted, dict) else set(), differing)
        if differing:
            results.append({"value": item.value, "state": "invalid", "differingPaths": list(dict.fromkeys(differing)), "witnessesResolved": witnesses})
            continue
        cms_proofs = [proof for proof in item.value.get("proofs", []) if isinstance(proof, dict) and proof.get("type") == CMS_ECDSA_P256_SHA256]
        if cms_proofs:
            state = "valid" if any(verify_cms_proof(item.value, proof) for proof in cms_proofs) else "invalid"
        else:
            state = "unverifiable"
        results.append({"value": item.value, "state": state, "differingPaths": [], "witnessesResolved": witnesses})
    return results


def verify_cms_proof(attestation: dict[str, Any], proof: dict[str, Any] | None = None) -> bool:
    """Verify a detached CMS ECDSA-P256/SHA-256 proof over its beta.6 envelope.

    This establishes cryptographic content validity only. Certificate-chain and
    application trust policy remain deliberately separate from this operation.
    """
    if proof is None:
        proofs = attestation.get("proofs")
        proof = next((item for item in proofs if isinstance(item, dict) and item.get("type") == CMS_ECDSA_P256_SHA256), None) if isinstance(proofs, list) else None
    if not isinstance(proof, dict) or proof.get("type") != CMS_ECDSA_P256_SHA256 or not isinstance(proof.get("value"), str):
        return False
    try:
        content_info = cms.ContentInfo.load(base64.b64decode(proof["value"], validate=True))
        if content_info["content_type"].native != "signed_data":
            return False
        signed_data = content_info["content"]
        # APR carries its payload beside the CMS container, not inside it.
        if signed_data["encap_content_info"]["content"].native is not None:
            return False
        signer_infos = signed_data["signer_infos"]
        if len(signer_infos) != 1:
            return False
        signer = signer_infos[0]
        if signer["digest_algorithm"]["algorithm"].native != "sha256" or signer["signature_algorithm"]["algorithm"].native != "sha256_ecdsa":
            return False
        signed_attrs = signer["signed_attrs"]
        if signed_attrs.native is None:
            return False
        payload = canonicalize({key: item for key, item in attestation.items() if key != "proofs"}).encode("utf-8")
        message_digest = next((attribute["values"][0].native for attribute in signed_attrs if attribute["type"].native == "message_digest" and len(attribute["values"])), None)
        if not isinstance(message_digest, bytes) or not compare_digest(message_digest, hashlib.sha256(payload).digest()):
            return False
        certificate = _cms_signer_certificate(signed_data, signer)
        if certificate is None:
            return False
        public_key = x509.load_der_x509_certificate(certificate.dump()).public_key()
        if not isinstance(public_key, ec.EllipticCurvePublicKey) or public_key.curve.name != "secp256r1":
            return False
        # CMS signs the DER SET OF signed attributes, rather than its IMPLICIT [0] wrapper.
        public_key.verify(signer["signature"].native, signed_attrs.untag().dump(), ec.ECDSA(hashes.SHA256()))
        return True
    except (ValueError, TypeError, KeyError, InvalidSignature):
        return False


def _cms_signer_certificate(signed_data: cms.SignedData, signer: cms.SignerInfo):
    sid = signer["sid"]
    if sid.name != "issuer_and_serial_number" or signed_data["certificates"].native is None:
        return None
    issuer_and_serial = sid.chosen
    for choice in signed_data["certificates"]:
        if choice.name != "certificate":
            continue
        certificate = choice.chosen
        tbs_certificate = certificate["tbs_certificate"]
        if tbs_certificate["serial_number"].native == issuer_and_serial["serial_number"].native and tbs_certificate["issuer"].dump() == issuer_and_serial["issuer"].dump():
            return certificate
    return None


def _validate_fields_scope(form: dict[str, Any], attestation: dict[str, Any], paths: set[str], differing: list[str]) -> None:
    scope = attestation.get("scope")
    if not isinstance(scope, dict) or scope.get("kind") != "fields":
        return
    fields = scope.get("fields")
    if not isinstance(fields, list) or not fields:
        differing.append("/scope/fields")
        return
    for field in fields:
        found = _find_prompt(form.get("sections"), field, "/sections", []) if isinstance(field, str) else None
        if found is None:
            differing.append("/scope/fields")
            continue
        prompt_path, sections = found
        _require(paths, prompt_path, differing)
        _require(paths, prompt_path + "/response", differing)
        if _at_pointer(form, prompt_path + "/hints") is not None: _require(paths, prompt_path + "/hints", differing)
        for section in sections:
            for member in ("id", "title", "description", "kind", "role"):
                if _at_pointer(form, section + "/" + member) is not None: _require(paths, section + "/" + member, differing)


def _find_prompt(sections: Any, identifier: str, base: str, ancestors: list[str]):
    if not isinstance(sections, list): return None
    for index, section in enumerate(sections):
        if not isinstance(section, dict): continue
        path, next_ancestors = f"{base}/{index}", ancestors + [f"{base}/{index}"]
        for prompt_index, prompt in enumerate(section.get("prompts", [])):
            if isinstance(prompt, dict) and prompt.get("id") == identifier: return f"{path}/prompts/{prompt_index}", next_ancestors
        nested = _find_prompt(section.get("sections"), identifier, path + "/sections", next_ancestors)
        if nested is not None: return nested
    return None


def _at_pointer(value: Any, pointer: str) -> Any:
    current = value
    for part in pointer.split("/")[1:]:
        part = part.replace("~1", "/").replace("~0", "~")
        if isinstance(current, dict): current = current.get(part)
        elif isinstance(current, list) and part.isdigit() and int(part) < len(current): current = current[int(part)]
        else: return None
    return current


def _require(paths: set[str], path: str, differing: list[str]) -> None:
    if path not in paths: differing.append(path)


def _record_form_value(record: Beta6FormRecord) -> dict[str, Any]:
    return record.value if record.value is not None else form_value(record.document)

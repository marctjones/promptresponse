#!/usr/bin/env python3
"""Recompute every derived value in the conformance corpus.

A form in the corpus is authored: somebody wrote it, and its JSONC comments and
trailing commas are themselves under test. An attestation is not. Its subject
digest, manifest root, per-path entry digests, witness digests and CMS proof are
all computed over other records, so they cannot be maintained by hand: change a
form and every one of them is wrong, with no way back short of recomputing them.

Before this existed, a specification change touching any member name broke the
corpus irrecoverably, because the signed fixture could not be re-signed. That is
why the rename of the format-version member stalled.

    python3 scripts/build-corpus.py            # report drift, change nothing
    python3 scripts/build-corpus.py --write    # recompute and rewrite

What is derived and what is authored is declared in
`tests/Conformance/beta6/corpus.map.json`. This script never invents a record:
it reads each attestation as it stands, replaces only the values that are
computed, and leaves every other member, and every form, exactly as written.

Signing uses the committed corpus test key. Signatures are deterministic
(RFC 6979), so regenerating an unchanged corpus reproduces it byte for byte and
`--write` is a no-op. The key is a fixture, published on purpose, and proves
nothing about anybody: see the note beside it.
"""
from __future__ import annotations

import base64
import datetime
import hashlib
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
CORPUS = ROOT / "tests" / "Conformance" / "beta6"
MAP = CORPUS / "corpus.map.json"
CMS_TYPE = "cms/ecdsa-p256-sha256"
# Fixed so a regenerated corpus is byte-identical. A claimed signing time is the
# signer's assertion, never trusted time.
SIGNING_TIME = datetime.datetime(2026, 1, 2, tzinfo=datetime.timezone.utc)


def address(path: pathlib.Path, index: int) -> str:
    return f"{path.relative_to(CORPUS)}[{index}]"


def load_corpus() -> dict[str, tuple[pathlib.Path, int, object]]:
    records: dict[str, tuple[pathlib.Path, int, object]] = {}
    for path in sorted(CORPUS.rglob("*")):
        if not path.is_file() or path.suffix not in {".jsonc", ".yaml", ".yml"}:
            continue
        if "malformed" in path.relative_to(CORPUS).parts:
            continue
        for index, record in enumerate(aprlib.read_file(path)):
            records[address(path, index)] = (path, index, record)
    return records


def sign_cms(payload: bytes, key_pem: bytes, cert_pem: bytes) -> str:
    """A detached CMS SignedData over `payload`, as the specification's one proof type."""
    from asn1crypto import cms, algos, core, x509 as asn1x509
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import ec

    private_key = serialization.load_pem_private_key(key_pem, password=None)
    certificate = asn1x509.Certificate.load(
        base64.b64decode(b"".join(
            line for line in cert_pem.splitlines() if not line.startswith(b"-----")))
    )

    signed_attrs = cms.CMSAttributes([
        cms.CMSAttribute({"type": "content_type", "values": ["data"]}),
        cms.CMSAttribute({"type": "signing_time",
                          "values": [cms.Time({"utc_time": SIGNING_TIME})]}),
        cms.CMSAttribute({"type": "message_digest",
                          "values": [hashlib.sha256(payload).digest()]}),
    ])
    # CMS signs the DER SET OF attributes, not the IMPLICIT [0] wrapper it travels in.
    signature = private_key.sign(
        signed_attrs.untag().dump(),
        ec.ECDSA(hashes.SHA256(), deterministic_signing=True),
    )

    signer = cms.SignerInfo({
        "version": "v1",
        "sid": cms.SignerIdentifier({"issuer_and_serial_number": cms.IssuerAndSerialNumber({
            "issuer": certificate["tbs_certificate"]["issuer"],
            "serial_number": certificate["tbs_certificate"]["serial_number"],
        })}),
        "digest_algorithm": algos.DigestAlgorithm({"algorithm": "sha256"}),
        "signed_attrs": signed_attrs,
        "signature_algorithm": algos.SignedDigestAlgorithm({"algorithm": "sha256_ecdsa"}),
        "signature": signature,
    })
    signed_data = cms.SignedData({
        "version": "v1",
        "digest_algorithms": [algos.DigestAlgorithm({"algorithm": "sha256"})],
        # Detached: APR carries the payload beside the container, never inside it.
        "encap_content_info": cms.ContentInfo({"content_type": "data"}),
        "certificates": cms.CertificateSet([cms.CertificateChoices({"certificate": certificate})]),
        "signer_infos": cms.SignerInfos([signer]),
    })
    container = cms.ContentInfo({"content_type": "signed_data", "content": signed_data})
    return base64.b64encode(container.dump()).decode("ascii")


def rebuild(record: dict, spec: dict, records, resolved, key_pem, cert_pem) -> dict:
    """The same record with every computed value recomputed. Nothing else moves."""
    out = json.loads(json.dumps(record))  # deep copy, preserving member order
    _, _, subject_form = records[spec["subject"]]
    subject_digest = aprlib.digest(subject_form)

    out.setdefault("subject", {})["digest"] = subject_digest
    out["subject"]["canonicalization"] = "jcs-sha256"
    out["scope"] = spec["scope"]

    entries = []
    for pointer in sorted(spec["entries"]):
        value = aprlib.resolve_pointer(subject_form, pointer)
        if value is aprlib.MISSING:
            raise SystemExit(
                f"corpus.map.json: entry {pointer!r} does not exist in {spec['subject']}")
        entries.append({"path": pointer, "digest": aprlib.digest(value)})
    out["manifest"] = {"root": subject_digest, "entries": entries}

    out["witnesses"] = [resolved[w] for w in spec["witnesses"]]

    proofs = []
    for declared in spec["proofs"]:
        if declared["type"] == CMS_TYPE:
            envelope = {k: v for k, v in out.items() if k != "proofs"}
            payload = aprlib.canonicalize(envelope).encode("utf-8")
            proofs.append({"type": CMS_TYPE, "value": sign_cms(payload, key_pem, cert_pem)})
        else:
            proofs.append({"type": declared["type"], "value": declared["value"]})
    out["proofs"] = proofs
    return out


def serialize(record: dict, path: pathlib.Path) -> str:
    if path.suffix in {".yaml", ".yml"}:
        import yaml
        return yaml.safe_dump(record, sort_keys=False, default_flow_style=False,
                              width=100, allow_unicode=True).rstrip("\n")
    return json.dumps(record, indent=2, ensure_ascii=False)


def main() -> int:
    write = "--write" in sys.argv
    mapping = json.loads(MAP.read_text(encoding="utf-8"))
    records = load_corpus()
    key_pem = (CORPUS / mapping["signingKey"]).read_bytes()
    cert_pem = (CORPUS / mapping["signingCertificate"]).read_bytes()

    specs = mapping["attestations"]
    for addr in specs:
        if addr not in records:
            raise SystemExit(f"corpus.map.json names {addr}, which is not in the corpus")

    # Witnesses name other envelopes, so resolve in dependency order.
    resolved: dict[str, str] = {}
    rebuilt: dict[str, dict] = {}
    pending = dict(specs)
    while pending:
        ready = [a for a, s in pending.items() if all(w in resolved for w in s["witnesses"])]
        if not ready:
            raise SystemExit("corpus.map.json: witness references form a cycle")
        for addr in ready:
            _, _, record = records[addr]
            new = rebuild(record, pending.pop(addr), records, resolved, key_pem, cert_pem)
            rebuilt[addr] = new
            resolved[addr] = aprlib.envelope_digest(new)

    # The published digest vectors state the same facts a third time, so they are
    # derived too. A vector nobody regenerates is a vector that goes quietly stale.
    changed: list[str] = []
    for name, spec in (mapping.get("digestVectors") or {}).items():
        path = CORPUS / name
        _, _, form = records[spec["form"]]
        vector = {
            "canonicalization": "jcs-sha256",
            "canonicalJson": aprlib.canonicalize(form),
            "documentDigest": aprlib.digest(form),
            "entries": [
                {"path": pointer, "digest": aprlib.digest(aprlib.resolve_pointer(form, pointer))}
                for pointer in sorted(spec["entries"])
            ],
        }
        rendered = json.dumps(vector, indent=2, ensure_ascii=False) + "\n"
        if path.read_text(encoding="utf-8") != rendered:
            changed.append(str(path.relative_to(ROOT)))
            if write:
                path.write_text(rendered, encoding="utf-8")

    # Rewrite each file, replacing only the attestation records the map declares.
    for path in sorted({records[a][0] for a in specs}):
        text = path.read_text(encoding="utf-8")
        if path.suffix in {".yaml", ".yml"}:
            leading = "---\n" if text.lstrip().startswith("---") else ""
            parts = [p for p in text.split("\n---\n")]
            parts[0] = parts[0].removeprefix("---\n")
            joiner, prefix = "\n---\n", leading
        else:
            parts = [p for p in text.split(aprlib.RS) if p.strip()]
            joiner, prefix = "", ""
        rendered = []
        for index, part in enumerate(parts):
            addr = address(path, index)
            if addr in rebuilt:
                body = serialize(rebuilt[addr], path)
                rendered.append(body + "\n" if path.suffix == ".jsonc" else body + "\n")
            else:
                rendered.append(part if path.suffix != ".jsonc" else part.lstrip("\n"))
        if path.suffix == ".jsonc":
            updated = "".join(aprlib.RS + p.rstrip("\n") + "\n" for p in rendered) \
                if aprlib.RS in text else rendered[0]
        else:
            updated = prefix + joiner.join(p.rstrip("\n") for p in rendered) + "\n"
        if updated != text:
            changed.append(str(path.relative_to(ROOT)))
            if write:
                path.write_text(updated, encoding="utf-8")

    if write:
        if changed:
            print(f"Rewrote {len(changed)} file(s):")
            for name in changed:
                print(f"  {name}")
        else:
            print("Corpus already agrees with the records it is derived from.")
        return 0
    if changed:
        print(f"{len(changed)} corpus file(s) carry values that are not what the records "
              f"they cover digest to:")
        for name in changed:
            print(f"  {name}")
        print("\nRun scripts/build-corpus.py --write")
        return 1
    print(f"Corpus is current: {len(specs)} attestation records, every derived value recomputed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

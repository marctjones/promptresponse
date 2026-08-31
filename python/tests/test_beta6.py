import pytest
from pathlib import Path

import promptresponse as pr


FORM = '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}'
CORPUS = Path(__file__).parents[2] / "tests" / "Conformance" / "beta6" / "forms"


def test_beta6_shared_jsonc_and_yaml_corpus_are_semantically_equal():
    jsonc = pr.read_beta6_form((CORPUS / "permit.apr.jsonc").read_text(), "jsonc")
    yaml = pr.read_beta6_form((CORPUS / "permit.apr.yaml").read_text(), "yaml")
    assert jsonc.metadata.title == yaml.metadata.title
    assert jsonc.document_type == yaml.document_type
    assert jsonc.sections[0].prompts[0].response == yaml.sections[0].prompts[0].response


def test_beta6_jsonc_and_yaml_have_same_semantics():
    parsed_jsonc = pr.read_beta6_form("// comment\n" + FORM[:-1] + ",}", "jsonc")
    yaml = pr.write_beta6_form(parsed_jsonc, "yaml")
    parsed_yaml = pr.read_beta6_form(yaml, "yaml")
    assert parsed_yaml.sections[0].prompts[0].response == "Ada"


def test_beta6_stream_keeps_duplicate_forms_and_requires_iteration():
    attestation = '{"recordType":"attestation","version":"1.0-beta.6","subject":{"digest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:0000000000000000000000000000000000000000000000000000000000000000","entries":[]},"proofs":[],"witnesses":[]}'
    source = "\x1e" + attestation + "\n\x1e" + FORM + "\n\x1e" + FORM
    records = pr.read_beta6_stream(source, "jsonc")
    assert len(records) == 3
    assert sum(isinstance(record, pr.Beta6FormRecord) for record in records) == 2
    with pytest.raises(pr.AprParseError, match="APR_STREAM_REQUIRES_ITERATION"):
        pr.read_beta6_form(source, "jsonc")
    assert len(pr.read_beta6_stream(pr.write_beta6_stream(records, "yaml"), "yaml")) == 3


def test_beta6_shared_out_of_order_stream_resolves_by_digest_not_position():
    source = (CORPUS.parent / "streams" / "out-of-order.apr.jsonc").read_text()
    records = pr.read_beta6_stream(source, "jsonc")
    assert len(records) == 3
    assert sum(isinstance(record, pr.Beta6FormRecord) for record in records) == 2
    assert pr.resolve_attestations(records)[0]["state"] == "unverifiable"
    yaml_records = pr.read_beta6_stream((CORPUS.parent / "streams" / "out-of-order.apr.yaml").read_text(), "yaml")
    assert sum(isinstance(record, pr.Beta6FormRecord) for record in yaml_records) == 2
    assert pr.resolve_attestations(yaml_records)[0]["state"] == "unverifiable"


def test_beta6_rejects_retired_embedded_signatures():
    with pytest.raises(pr.AprParseError, match="RETIRED_EMBEDDED_SIGNATURES"):
        pr.read_beta6_form(FORM[:-1] + ',"signatures":[]}', "jsonc")


def test_beta6_rejects_duplicate_jsonc_members():
    with pytest.raises(pr.AprParseError, match="duplicate member"):
        pr.read_beta6_form(FORM.replace('"metadata":', '"metadata":{},"metadata":'), "jsonc")


def test_beta6_shared_malformed_corpus_is_rejected():
    for path in (CORPUS.parent / "malformed").iterdir():
        with pytest.raises(pr.AprParseError):
            pr.read_beta6_stream(path.read_text(), "yaml" if path.suffix in {".yaml", ".yml"} else "jsonc")


def test_beta6_digest_and_unsigned_attestation_resolve_from_shared_corpus():
    document = pr.read_beta6_form((CORPUS / "permit.apr.jsonc").read_text(), "jsonc")
    value = pr.form_value(document)
    manifest = pr.create_manifest(value)
    assert pr.digest(value) == "sha256:d06b9720c44d64b368e93bd6765cad81bfa1e8ea9b767b4acd1ffc57c26b0253"
    attestation = {"recordType": "attestation", "version": "1.0-beta.6", "subject": {"digest": manifest["root"], "canonicalization": "jcs-sha256"}, "scope": {"kind": "document"}, "manifest": manifest, "proofs": [], "witnesses": []}
    records = [pr.Beta6FormRecord(document), pr.Beta6AttestationRecord(attestation)]
    assert pr.resolve_attestations(records)[0]["state"] == "unverifiable"


def test_beta6_fields_scope_requires_the_selected_response_in_manifest():
    document = pr.read_beta6_form(FORM, "jsonc")
    value, complete = pr.form_value(document), pr.create_manifest(pr.form_value(document))
    manifest = {**complete, "entries": [entry for entry in complete["entries"] if entry["path"] != "/sections/0/prompts/0/response"]}
    attestation = {"recordType": "attestation", "version": "1.0-beta.6", "subject": {"digest": pr.digest(value), "canonicalization": "jcs-sha256"}, "scope": {"kind": "fields", "fields": ["p"]}, "manifest": manifest, "proofs": [], "witnesses": []}
    result = pr.resolve_attestations([pr.Beta6FormRecord(document), pr.Beta6AttestationRecord(attestation)])[0]
    assert result["state"] == "invalid"
    assert "/sections/0/prompts/0/response" in result["differingPaths"]


def test_beta6_shared_witness_vector_resolves_exact_envelope():
    records = pr.read_beta6_stream((CORPUS.parent / "streams" / "witnessed.apr.jsonc").read_text(), "jsonc")
    assert pr.resolve_attestations(records)[1]["witnessesResolved"] == 1
    chain = pr.read_beta6_stream((CORPUS.parent / "streams" / "witness-chain.apr.jsonc").read_text(), "jsonc")
    assert pr.resolve_attestations(chain)[1]["witnessesResolved"] == 1
    assert pr.resolve_attestations(chain)[2]["witnessesResolved"] == 1


def test_beta6_changed_copied_form_does_not_inherit_an_attestation():
    records = pr.read_beta6_stream((CORPUS.parent / "streams" / "changed-form.apr.jsonc").read_text(), "jsonc")
    assert pr.resolve_attestations(records)[0]["state"] == "unresolved"


def test_beta6_cms_corpus_proof_verifies_over_the_exact_detached_envelope():
    form = pr.read_beta6_form((CORPUS / "permit.apr.jsonc").read_text(), "jsonc")
    proof = pr.read_beta6_stream((CORPUS.parent / "attestations" / "permit.cms.attestation.jsonc").read_text(), "jsonc")[0]
    assert pr.resolve_attestations([pr.Beta6FormRecord(form), proof])[0]["state"] == "valid"
    assert pr.verify_cms_proof(proof.value)
    proof.value["scope"] = {"kind": "fields", "fields": ["applicant-name"]}
    assert not pr.verify_cms_proof(proof.value)
    assert pr.resolve_attestations([pr.Beta6FormRecord(form), proof])[0]["state"] == "invalid"


def test_beta6_stream_rewrite_preserves_semantic_extensions_and_cms_subjects():
    source = '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[]}],"x-vendor":{"enabled":true}}'
    rewritten = pr.write_beta6_stream(pr.read_beta6_stream(source, "jsonc"), "jsonc")
    assert __import__("json").loads(rewritten.lstrip("\x1e").strip())["x-vendor"] == {"enabled": True}

    form = pr.read_beta6_stream((CORPUS / "permit.apr.jsonc").read_text(), "jsonc")[0]
    proof = pr.read_beta6_stream((CORPUS.parent / "attestations" / "permit.cms.attestation.jsonc").read_text(), "jsonc")[0]
    round_tripped = pr.read_beta6_stream(pr.write_beta6_stream([form, proof], "jsonc"), "jsonc")
    assert pr.resolve_attestations(round_tripped)[0]["state"] == "valid"


def test_beta6_fields_scope_corpus_vector_binds_context_before_proof_verification():
    form = pr.read_beta6_form((CORPUS / "permit.apr.jsonc").read_text(), "jsonc")
    proof = pr.read_beta6_stream((CORPUS.parent / "attestations" / "permit.fields.attestation.jsonc").read_text(), "jsonc")[0]
    assert pr.resolve_attestations([pr.Beta6FormRecord(form), proof])[0]["state"] == "unverifiable"


def test_beta6_unsupported_proof_is_unverifiable_not_invalid():
    form = pr.read_beta6_form((CORPUS / "permit.apr.jsonc").read_text(), "jsonc")
    proof = pr.read_beta6_stream((CORPUS.parent / "attestations" / "permit.unsupported.attestation.jsonc").read_text(), "jsonc")[0]
    assert pr.resolve_attestations([pr.Beta6FormRecord(form), proof])[0]["state"] == "unverifiable"

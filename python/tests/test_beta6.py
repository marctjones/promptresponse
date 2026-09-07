import pytest
from pathlib import Path

import promptresponse as pr


FORM = '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}'
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
    attestation = '{"recordType":"attestation","aprVersion":"1.0-beta.6","subject":{"digest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:0000000000000000000000000000000000000000000000000000000000000000","entries":[]},"proofs":[],"witnesses":[]}'
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
    assert pr.digest(value) == "sha256:c525780361ebf5ef97b1c6ffb6db963c12281bce62a0bdc150a2d2c7f1a14675"
    attestation = {"recordType": "attestation", "aprVersion": "1.0-beta.6", "subject": {"digest": manifest["root"], "canonicalization": "jcs-sha256"}, "scope": {"kind": "document"}, "manifest": manifest, "proofs": [], "witnesses": []}
    records = [pr.Beta6FormRecord(document), pr.Beta6AttestationRecord(attestation)]
    assert pr.resolve_attestations(records)[0]["state"] == "unverifiable"


def test_beta6_fields_scope_requires_the_selected_response_in_manifest():
    document = pr.read_beta6_form(FORM, "jsonc")
    value, complete = pr.form_value(document), pr.create_manifest(pr.form_value(document))
    manifest = {**complete, "entries": [entry for entry in complete["entries"] if entry["path"] != "/sections/0/prompts/0/response"]}
    attestation = {"recordType": "attestation", "aprVersion": "1.0-beta.6", "subject": {"digest": pr.digest(value), "canonicalization": "jcs-sha256"}, "scope": {"kind": "fields", "fields": ["p"]}, "manifest": manifest, "proofs": [], "witnesses": []}
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
    source = '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[]}],"x-vendor":{"enabled":true}}'
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


# RFC 8785 Appendix B: IEEE 754 bit patterns and their required JCS spellings.
JCS_NUMBER_VECTORS = [
    ("0000000000000000", "0"), ("8000000000000000", "0"), ("0000000000000001", "5e-324"), ("0000000000000002", "1e-323"),
    ("8000000000000001", "-5e-324"), ("7fefffffffffffff", "1.7976931348623157e+308"),
    ("ffefffffffffffff", "-1.7976931348623157e+308"), ("4340000000000000", "9007199254740992"),
    ("c340000000000000", "-9007199254740992"), ("4430000000000000", "295147905179352830000"),
    ("44b52d02c7e14af5", "9.999999999999997e+22"), ("44b52d02c7e14af6", "1e+23"),
    ("44b52d02c7e14af7", "1.0000000000000001e+23"), ("444b1ae4d6e2ef4e", "999999999999999700000"),
    ("444b1ae4d6e2ef4f", "999999999999999900000"), ("444b1ae4d6e2ef50", "1e+21"),
    ("3eb0c6f7a0b5ed8c", "9.999999999999997e-7"), ("3eb0c6f7a0b5ed8d", "0.000001"),
    ("41b3de4355555553", "333333333.3333332"), ("41b3de4355555554", "333333333.33333325"),
    ("41b3de4355555555", "333333333.3333333"), ("41b3de4355555556", "333333333.3333334"),
    ("41b3de4355555557", "333333333.33333343"), ("becbf647612f3696", "-0.0000033333333333333333"),
    ("43143ff3c1cb0959", "1424953923781206.2"),
]

# One form whose numbers live in extension members, so every SDK's stream reader
# preserves them. Its digest is pinned across the Python, .NET, TypeScript and
# Java canonicalizers.
NUMERIC_EXTENSIONS_JSONC = '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"","com.example.canAddRows":true,"com.example.maxRows":5,"com.example.min":1996,"com.example.step":0.5,"com.example.scale":1e21,"com.example.epsilon":1e-7}]}]}'
NUMERIC_EXTENSIONS_YAML = """aprVersion: "1.0-beta.6"
metadata: { title: T }
sections:
  - id: s
    title: S
    prompts:
      - id: p
        label: P
        response: ""
        com.example.canAddRows: true
        com.example.maxRows: 5
        com.example.min: 1996.0
        com.example.step: 0.5
        com.example.scale: 1000000000000000000000
        com.example.epsilon: 0.0000001
"""
NUMERIC_EXTENSIONS_DIGEST = "sha256:df7259065a4e63df08be70e66bfe7c85412e42a3af6d477ccab2d285a62c9fa8"


def test_jcs_number_serialization_matches_rfc_8785_appendix_b():
    import struct
    for pattern, expected in JCS_NUMBER_VECTORS:
        value = struct.unpack(">d", bytes.fromhex(pattern))[0]
        assert pr.canonicalize(value) == expected, pattern
    assert pr.canonicalize(1996) == "1996"
    assert pr.canonicalize(1996.0) == "1996"
    assert pr.canonicalize(1e20) == "100000000000000000000"
    assert pr.canonicalize(2**53 + 2) == "9007199254740994"
    assert pr.canonicalize(True) == "true"
    for bad in (float("inf"), float("-inf"), float("nan"), 10**400):
        with pytest.raises(pr.AprParseError, match="finite JSON numbers"):
            pr.canonicalize(bad)


def test_jsonc_and_yaml_spellings_of_native_structural_numbers_digest_identically():
    import json
    import yaml
    from promptresponse.beta6 import AprYamlLoader, _strip_jsonc

    jsonc = """{
      // structural members use native JSON types
      "aprVersion": "1.0-beta.6", "metadata": { "title": "T" },
      "sections": [{ "id": "s", "title": "S", "canAddRows": true, "maxRows": 5,
        "prompts": [{ "id": "n", "label": "N", "hints": { "min": 1996 }, "response": "" }] }]
    }"""
    yaml_source = """aprVersion: "1.0-beta.6"
metadata: { title: T }
sections:
  - id: s
    title: S
    canAddRows: true
    maxRows: 5
    prompts:
      - id: n
        label: N
        hints: { min: 1996 }
        response: ""
"""
    from_jsonc = json.loads(_strip_jsonc(jsonc))
    from_yaml = yaml.load(yaml_source, AprYamlLoader)
    assert from_jsonc == from_yaml
    assert isinstance(from_yaml["sections"][0]["maxRows"], int)
    assert isinstance(from_yaml["sections"][0]["prompts"][0]["hints"]["min"], int)
    assert '"maxRows":5,' in pr.canonicalize(from_jsonc)
    assert '"min":1996}' in pr.canonicalize(from_yaml)
    assert pr.digest(from_jsonc) == pr.digest(from_yaml)
    # A float spelling of an integral value is the same JCS number.
    assert pr.digest(yaml.load(yaml_source.replace("min: 1996", "min: 1996.0"), AprYamlLoader)) == pr.digest(from_jsonc)


def test_apr_yaml_resolves_integer_and_float_scalars_to_the_json_value_space():
    import yaml
    from promptresponse.beta6 import AprYamlLoader

    loaded = yaml.load("i: 5\nz: -0\nf: 1.5\ne: 1e3\nlead: 012\nq: '5'\nsex: 1:30\n", AprYamlLoader)
    assert loaded == {"i": 5, "z": 0, "f": 1.5, "e": 1000.0, "lead": "012", "q": "5", "sex": "1:30"}
    assert isinstance(loaded["i"], int) and isinstance(loaded["f"], float)


def test_numeric_extension_members_digest_identically_from_jsonc_and_yaml_streams():
    jsonc_record = pr.read_beta6_stream(NUMERIC_EXTENSIONS_JSONC, "jsonc")[0]
    yaml_record = pr.read_beta6_stream(NUMERIC_EXTENSIONS_YAML, "yaml")[0]
    assert pr.digest(jsonc_record.value) == NUMERIC_EXTENSIONS_DIGEST
    assert pr.digest(yaml_record.value) == NUMERIC_EXTENSIONS_DIGEST

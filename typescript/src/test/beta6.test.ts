import assert from "node:assert/strict";
import test from "node:test";
import { buildExpressionContext, computeValue, AprParseError, beta6FormValue, createBeta6Manifest, digestBeta6, readBeta6Form, readBeta6Stream, resolveBeta6Attestations, resolveBeta6AttestationsAsync, verifyBeta6CmsProof, writeBeta6Form, writeBeta6Stream } from "../index.js";
import { readFile } from "node:fs/promises";

const form = `{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}`;

test("beta.6 shared JSONC and YAML corpus forms have equal semantics", async () => {
  const corpus = "../../../tests/Conformance/beta6/forms/";
  const jsonc = readBeta6Form(await readFile(new URL(corpus + "permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  const yaml = readBeta6Form(await readFile(new URL(corpus + "permit.apr.yaml", import.meta.url), "utf8"), "yaml");
  assert.deepEqual(jsonc, yaml);
});

test("beta.6 JSONC and YAML decode to the same form", () => {
  const jsonc = `// comment\n${form.replace("}]}", "},],}")}`;
  const parsedJsonc = readBeta6Form(jsonc, "jsonc");
  const yaml = writeBeta6Form(parsedJsonc, "yaml");
  const parsedYaml = readBeta6Form(yaml, "yaml");
  assert.equal(parsedYaml.sections[0].prompts[0].response, "Ada");
});

test("beta.6 streams preserve duplicate forms and reject implicit selection", () => {
  const attestation = `{"recordType":"attestation","version":"1.0-beta.6","subject":{"digest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:0000000000000000000000000000000000000000000000000000000000000000","entries":[]},"proofs":[],"witnesses":[]}`;
  const source = `\u001e${attestation}\n\u001e${form}\n\u001e${form}\n`;
  const records = readBeta6Stream(source, "jsonc");
  assert.equal(records.length, 3);
  assert.equal(records.filter(record => record.type === "form").length, 2);
  assert.throws(() => readBeta6Form(source, "jsonc"), AprParseError);
  assert.equal(readBeta6Stream(writeBeta6Stream(records, "yaml"), "yaml").length, 3);
});

test("beta.6 shared out-of-order stream stays unresolved before the form and preserves duplicates", async () => {
  const source = await readFile(new URL("../../../tests/Conformance/beta6/streams/out-of-order.apr.jsonc", import.meta.url), "utf8");
  const records = readBeta6Stream(source, "jsonc");
  assert.equal(records.length, 3);
  assert.equal(records.filter(record => record.type === "form").length, 2);
  assert.equal(resolveBeta6Attestations(records)[0].state, "unverifiable");
  const yaml = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/streams/out-of-order.apr.yaml", import.meta.url), "utf8"), "yaml");
  assert.equal(yaml.filter(record => record.type === "form").length, 2);
  assert.equal(resolveBeta6Attestations(yaml)[0].state, "unverifiable");
});

test("beta.6 rejects the retired root signatures field", () => {
  const retired = `${form.slice(0, -1)},"signatures":[]}`;
  assert.throws(() => readBeta6Form(retired, "jsonc"), /RETIRED_EMBEDDED_SIGNATURES/);
});

test("beta.6 rejects duplicate JSONC members", () => {
  assert.throws(() => readBeta6Form(form.replace('"metadata":', '"metadata":{},"metadata":'), "jsonc"), /duplicate member/);
});

test("beta.6 shared malformed corpus is rejected", async () => {
  for (const name of ["missing-record-separator.apr.jsonc", "duplicate-member.apr.jsonc", "yaml-anchor.apr.yaml"]) {
    const source = await readFile(new URL(`../../../tests/Conformance/beta6/malformed/${name}`, import.meta.url), "utf8");
    assert.throws(() => readBeta6Stream(source, name.endsWith(".yaml") ? "yaml" : "jsonc"), AprParseError);
  }
});

test("beta.6 shared digest and an unsigned attestation resolve without a validity claim", async () => {
  const document = readBeta6Form(await readFile(new URL("../../../tests/Conformance/beta6/forms/permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  const value = beta6FormValue(document), manifest = createBeta6Manifest(value);
  assert.equal(digestBeta6(value), "sha256:d06b9720c44d64b368e93bd6765cad81bfa1e8ea9b767b4acd1ffc57c26b0253");
  const attestation = { recordType: "attestation", version: "1.0-beta.6", subject: { digest: manifest.root, canonicalization: "jcs-sha256" }, scope: { kind: "document" }, manifest, proofs: [], witnesses: [] };
  assert.equal(resolveBeta6Attestations([{ type: "form", document }, { type: "attestation", value: attestation }])[0].state, "unverifiable");
});

test("beta.6 fields scope is invalid when its selected response is absent from the manifest", () => {
  const document = readBeta6Form(form, "jsonc"), value = beta6FormValue(document), complete = createBeta6Manifest(value);
  const manifest = { ...complete, entries: complete.entries.filter(entry => entry.path !== "/sections/0/prompts/0/response") };
  const attestation = { recordType: "attestation", version: "1.0-beta.6", subject: { digest: complete.root, canonicalization: "jcs-sha256" }, scope: { kind: "fields", fields: ["p"] }, manifest, proofs: [], witnesses: [] };
  const result = resolveBeta6Attestations([{ type: "form", document }, { type: "attestation", value: attestation }])[0];
  assert.equal(result.state, "invalid");
  assert.ok(result.differingPaths.includes("/sections/0/prompts/0/response"));
});

test("beta.6 shared witness vector resolves an exact earlier envelope", async () => {
  const records = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/streams/witnessed.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(resolveBeta6Attestations(records)[1].witnessesResolved, 1);
  const chain = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/streams/witness-chain.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(resolveBeta6Attestations(chain)[1].witnessesResolved, 1);
  assert.equal(resolveBeta6Attestations(chain)[2].witnessesResolved, 1);
});

test("beta.6 changed copied form does not inherit an earlier attestation", async () => {
  const records = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/streams/changed-form.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(resolveBeta6Attestations(records)[0].state, "unresolved");
});

test("beta.6 CMS corpus vector remains explicitly unverifiable without CMS support", async () => {
  const document = readBeta6Form(await readFile(new URL("../../../tests/Conformance/beta6/forms/permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  const [proof] = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/attestations/permit.cms.attestation.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(resolveBeta6Attestations([{ type: "form", document }, proof!])[0].state, "unverifiable");
});

test("beta.6 CMS corpus proof verifies the exact detached envelope", async () => {
  const [proof] = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/attestations/permit.cms.attestation.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(proof!.type, "attestation");
  if (proof!.type !== "attestation") throw new Error("expected attestation");
  assert.equal(await verifyBeta6CmsProof(proof.value), true);
  assert.equal(await verifyBeta6CmsProof({ ...proof.value, scope: { kind: "changed" } }), false);
  const form = readBeta6Form(await readFile(new URL("../../../tests/Conformance/beta6/forms/permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal((await resolveBeta6AttestationsAsync([{ type: "form", document: form }, proof]))[0]?.state, "valid");
  assert.equal((await resolveBeta6AttestationsAsync([{ type: "form", document: form }, { type: "attestation", value: { ...proof.value, scope: { kind: "changed" } } }]))[0]?.state, "invalid");
});

test("beta.6 stream rewrite preserves semantic extensions and CMS subjects", async () => {
  const source = '{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[]}],"x-vendor":{"enabled":true}}';
  assert.match(writeBeta6Stream(readBeta6Stream(source, "jsonc"), "jsonc"), /"x-vendor":\{"enabled":true\}/);

  const form = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/forms/permit.apr.jsonc", import.meta.url), "utf8"), "jsonc")[0]!;
  const proof = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/attestations/permit.cms.attestation.jsonc", import.meta.url), "utf8"), "jsonc")[0]!;
  assert.equal(resolveBeta6Attestations(readBeta6Stream(writeBeta6Stream([form, proof], "jsonc"), "jsonc"))[0]?.state, "unverifiable");
  if (proof.type !== "attestation") throw new Error("expected attestation");
  assert.equal(await verifyBeta6CmsProof((readBeta6Stream(writeBeta6Stream([form, proof], "jsonc"), "jsonc")[1] as Extract<typeof proof, { type: "attestation" }>).value), true);
});

test("beta.6 fields-scope corpus vector binds prompt context before proof verification", async () => {
  const document = readBeta6Form(await readFile(new URL("../../../tests/Conformance/beta6/forms/permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  const [proof] = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/attestations/permit.fields.attestation.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(resolveBeta6Attestations([{ type: "form", document }, proof!])[0].state, "unverifiable");
});

test("beta.6 unsupported proof remains explicitly unverifiable", async () => {
  const document = readBeta6Form(await readFile(new URL("../../../tests/Conformance/beta6/forms/permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  const [proof] = readBeta6Stream(await readFile(new URL("../../../tests/Conformance/beta6/attestations/permit.unsupported.attestation.jsonc", import.meta.url), "utf8"), "jsonc");
  assert.equal(resolveBeta6Attestations([{ type: "form", document }, proof!])[0].state, "unverifiable");
});

test("the expression activation binds every name the specification defines", () => {
  const document = readBeta6Form(JSON.stringify({
    version: "1.0-beta.6", metadata: { title: "T" },
    sections: [{ id: "s", title: "S", prompts: [
      { id: "echo_id", label: "E", response: "", hints: { exprValue: "_id" } },
      { id: "echo_today", label: "T", response: "", hints: { exprValue: "_today" } },
      { id: "echo_ctx", label: "C", response: "", hints: { exprValue: "ctx['team']" } },
      { id: "echo_this", label: "S", response: "seed", hints: { exprValue: "_this" } },
    ] }],
  }), "jsonc");
  const context = buildExpressionContext(document, "2026-09-01T12:00:00Z", { team: "records" });
  const value = (id: string) =>
    computeValue(document.sections[0].prompts.find(p => p.id === id)!, context);

  assert.equal(value("echo_id"), "echo_id");
  assert.equal(value("echo_today"), "2026-09-01");
  assert.equal(value("echo_ctx"), "records");
  assert.equal(value("echo_this"), "seed");
});

test("temporal names are unbound when the caller supplies nothing", () => {
  // Reading the host clock would make the same inputs evaluate differently twice.
  const document = readBeta6Form(JSON.stringify({
    version: "1.0-beta.6", metadata: { title: "T" },
    sections: [{ id: "s", title: "S", prompts: [
      { id: "t", label: "T", response: "kept", hints: { exprValue: "_today" } },
    ] }],
  }), "jsonc");
  const context = buildExpressionContext(document);
  assert.equal(computeValue(document.sections[0].prompts[0], context), undefined);
});

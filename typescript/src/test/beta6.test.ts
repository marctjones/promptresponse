import assert from "node:assert/strict";
import test from "node:test";
import { buildExpressionContext, computeValue, AprParseError, beta6FormValue, canonicalizeBeta6, createBeta6Manifest, digestBeta6, readBeta6Form, readBeta6Stream, resolveBeta6Attestations, resolveBeta6AttestationsAsync, verifyBeta6CmsProof, writeBeta6Form, writeBeta6Stream } from "../index.js";
import { readFile } from "node:fs/promises";

const form = `{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}`;

test("beta.6 shared JSONC and YAML corpus forms have equal semantics", async () => {
  const corpus = "../../../tests/Conformance/beta6/forms/";
  const jsonc = readBeta6Form(await readFile(new URL(corpus + "permit.apr.jsonc", import.meta.url), "utf8"), "jsonc");
  const yaml = readBeta6Form(await readFile(new URL(corpus + "permit.apr.yaml", import.meta.url), "utf8"), "yaml");
  assert.deepEqual(jsonc, yaml);
});

// RFC 8785 Appendix B: IEEE 754 bit patterns and their required JCS spellings.
const JCS_NUMBER_VECTORS: [string, string][] = [
  ["0000000000000000", "0"], ["8000000000000000", "0"], ["0000000000000001", "5e-324"], ["0000000000000002", "1e-323"], ["8000000000000001", "-5e-324"],
  ["7fefffffffffffff", "1.7976931348623157e+308"], ["ffefffffffffffff", "-1.7976931348623157e+308"],
  ["4340000000000000", "9007199254740992"], ["c340000000000000", "-9007199254740992"], ["4430000000000000", "295147905179352830000"],
  ["44b52d02c7e14af5", "9.999999999999997e+22"], ["44b52d02c7e14af6", "1e+23"], ["44b52d02c7e14af7", "1.0000000000000001e+23"],
  ["444b1ae4d6e2ef4e", "999999999999999700000"], ["444b1ae4d6e2ef4f", "999999999999999900000"], ["444b1ae4d6e2ef50", "1e+21"],
  ["3eb0c6f7a0b5ed8c", "9.999999999999997e-7"], ["3eb0c6f7a0b5ed8d", "0.000001"],
  ["41b3de4355555553", "333333333.3333332"], ["41b3de4355555554", "333333333.33333325"], ["41b3de4355555555", "333333333.3333333"],
  ["41b3de4355555556", "333333333.3333334"], ["41b3de4355555557", "333333333.33333343"],
  ["becbf647612f3696", "-0.0000033333333333333333"], ["43143ff3c1cb0959", "1424953923781206.2"],
];

test("beta.6 canonical numbers match RFC 8785 Appendix B", () => {
  for (const [bits, expected] of JCS_NUMBER_VECTORS) {
    const view = new DataView(new ArrayBuffer(8));
    view.setBigUint64(0, BigInt(`0x${bits}`));
    assert.equal(canonicalizeBeta6(view.getFloat64(0)), expected, bits);
  }
  assert.equal(canonicalizeBeta6(JSON.parse(`{"maxRows":5.0,"min":1.996e3,"canAddRows":true}`)), `{"canAddRows":true,"maxRows":5,"min":1996}`);
  assert.throws(() => canonicalizeBeta6(Infinity), AprParseError);
  assert.throws(() => canonicalizeBeta6(NaN), AprParseError);
});

test("beta.6 numeric extension members digest identically from JSONC and YAML", () => {
  const expected = "sha256:b2d48b3e183f16894e16b4c94f99f340d2c2fc5dcc32e68938f61bebcc404d0a";
  const jsonc = `{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"","com.example.canAddRows":true,"com.example.maxRows":5,"com.example.min":1996,"com.example.step":0.5,"com.example.scale":1e21,"com.example.epsilon":1e-7}]}]}`;
  const yaml = [
    `version: "1.0-beta.6"`, "metadata: { title: T }", "sections:", "  - id: s", "    title: S", "    prompts:",
    "      - id: p", "        label: P", `        response: ""`, "        com.example.canAddRows: true", "        com.example.maxRows: 5",
    "        com.example.min: 1996.0", "        com.example.step: 0.5", "        com.example.scale: 1000000000000000000000", "        com.example.epsilon: 0.0000001", "",
  ].join("\n");
  for (const [source, representation] of [[jsonc, "jsonc"], [yaml, "yaml"]] as const) {
    const record = readBeta6Stream(source, representation)[0]!;
    assert.equal(record.type, "form");
    assert.equal(digestBeta6(record.value!), expected, representation);
  }
  assert.throws(() => readBeta6Stream(`version: "1.0-beta.6"\nmetadata: { title: T, com.example.big: 1e999 }\nsections: []\n`, "yaml"), /non-finite/);
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

test("beta.6 YAML indicator characters inside a plain scalar are ordinary content", () => {
  // An anchor, alias or tag is a node property (specification 4.5); "&", "*" and
  // "!" inside a scalar's content are just characters of a string, and a quoted
  // "<<" is a string key rather than a merge key.
  const form = (response: string) => `version: "1.0-beta.6"\nmetadata:\n  title: T\n  "<<": not a merge key\nsections:\n  - id: s\n    title: S\n    prompts:\n      - id: p\n        label: P\n        hints:\n          exprValue: string(fee_count * 8.0)\n        response: ${response}\n`;
  const record = readBeta6Stream(form("a * b & c! d"), "yaml")[0];
  assert.equal(record.type, "form");
  const prompt = (record.value as { sections: { prompts: { hints: Record<string, unknown>; response: unknown }[] }[] }).sections[0].prompts[0];
  assert.equal(prompt.hints.exprValue, "string(fee_count * 8.0)");
  assert.equal(prompt.response, "a * b & c! d");
  const cases: [string, RegExp][] = [
    [form("&r 1"), /anchors/], [form("*r"), /aliases/], [form("!!str 1"), /tags/], [form("! 1"), /tags/],
    [form("1").replace("    title: S\n", "    title: S\n    <<: {description: merged}\n"), /merge keys/],
    [form("{<<: {b: 1}}"), /merge keys/],
    ["%YAML 1.2\n---\n" + form("1"), /directives/], ["%TAG !e! tag:example.com,2000:\n---\n" + form("1"), /directives/],
    // A directive belongs to the document that follows it, wherever that is in the stream.
    [form("1") + "...\n%TAG !e! tag:example.com,2000:\n---\n" + form("2"), /directives/],
  ];
  for (const [source, message] of cases) assert.throws(() => readBeta6Stream(source, "yaml"), message);
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

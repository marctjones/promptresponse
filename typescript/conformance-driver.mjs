#!/usr/bin/env node
// The TypeScript SDK's conformance driver.
//
// Answers `tests/Conformance/beta6/suite.json` (with the answers withheld)
// using the built `dist/` output -- the actual package a caller installs, not
// a hand-rolled parser. See `docs/SDK_CONFORMANCE.md` for the driver contract
// this implements, and `python/conformance_driver.py` for the sibling driver
// this is modeled on.
//
//   node typescript/conformance-driver.mjs < suite.json
//   python3 scripts/run-conformance.py --driver "node typescript/conformance-driver.mjs"
//
// Not part of the published package (package.json's "files" ships only
// dist/ and README.md): a driver over the SDK, not a module the SDK ships,
// the same relationship python/conformance_driver.py has to the
// promptresponse package.

import { readFileSync } from "node:fs";
import {
  readBeta6Form, readBeta6Stream, writeBeta6Form, writeBeta6Stream,
  digestBeta6, beta6FormValue,
  validate, buildExpressionContext, condition, validationMessage, recomputeComputedValues,
} from "./dist/index.js";

const RS = "";

function allPrompts(sections) {
  return sections.flatMap(section => [...section.prompts, ...allPrompts(section.sections)]);
}

function diagnostic(error) {
  return error.code || error.name || "Error";
}

function digestOf(record) {
  // record.value is the document exactly as parsed -- an absent optional member
  // stays absent. beta6FormValue(record.document) goes through the typed model
  // instead, whose interface fields don't distinguish "absent" from "empty
  // string" for every member, so it can digest a document that was never the
  // one on the wire. Both are "correct" readings of the same bytes; the
  // suite's reference digests are computed from the parsed value, so that's
  // the comparable one.
  return digestBeta6(record.value ?? beta6FormValue(record.document));
}

function evaluate(document, inputs) {
  // Order matters: hidden/validation see only the responses as read, then
  // computed values (exprValue) fill in -- mirroring scripts/aprexpr.py's
  // evaluate(), which is the contract's own worked example.
  const today = inputs._today || inputs._now;
  const ctx = inputs.ctx;
  const context = buildExpressionContext(document, today, ctx);

  const hidden = {};
  const expected = {};
  const readOnly = {};
  const result = {};
  for (const prompt of allPrompts(document.sections)) {
    if (prompt.hints.exprHidden) hidden[prompt.id] = condition(prompt, prompt.hints.exprHidden, context);
    if (prompt.hints.exprExpected) expected[prompt.id] = condition(prompt, prompt.hints.exprExpected, context);
    if (prompt.hints.exprReadOnly) readOnly[prompt.id] = condition(prompt, prompt.hints.exprReadOnly, context);
    if (prompt.hints.exprValidation) result[prompt.id] = validationMessage(prompt, context) ?? "";
  }

  recomputeComputedValues(document, today, ctx);
  const responses = {};
  for (const prompt of allPrompts(document.sections)) if (prompt.hints.exprValue) responses[prompt.id] = prompt.response;
  return { responses, hidden, expected, readOnly, validation: result };
}

function written(records) {
  // writeBeta6Stream, given a form record that still carries the raw `value`
  // readBeta6Stream attached, echoes that raw text back rather than
  // re-serializing the typed document -- a legitimate preservation path, but
  // one that would let this driver claim a round trip without exercising the
  // SDK's own writer at all. Clearing it forces writeBeta6Form's typed-model
  // path, which is the one part of this contract that tests writing.
  const typed = records.map(record => record.type === "form" ? { type: "form", document: record.document } : record);
  if (typed.length === 1 && typed[0].type === "form") return writeBeta6Form(typed[0].document, "jsonc");
  return writeBeta6Stream(typed, "jsonc");
}

function answer(testCase, profiles) {
  const id = testCase.id;
  const representation = testCase.representation.startsWith("yaml") ? "yaml" : "jsonc";

  if (testCase.representation === "jsonc-stream" && !testCase.document.includes(RS)) {
    // Two JSON texts with no record separator between them parse as one
    // document, silently losing the second -- refused before parsing gets
    // the chance to merge them.
    return { id, outcome: "reject", diagnostic: "APR_STREAM_MISSING_RECORD_SEPARATOR" };
  }

  let records;
  try {
    // Without core+streams a caller asks for one form, and the SDK refuses a stream
    // rather than choose a record from it. [APR-CONF-001]
    if (!profiles.includes("core+streams")) readBeta6Form(testCase.document, representation);
    records = readBeta6Stream(testCase.document, representation);
  } catch (error) {
    return { id, outcome: "reject", diagnostic: diagnostic(error) };
  }

  if (!records.length) return { id, outcome: "reject", diagnostic: "NULL_DOCUMENT" };

  // Attestation records are structurally validated during parsing itself
  // (beta6.ts's validateAttestation); only form records need the separate
  // validate() pass.
  const allErrors = [];
  for (const record of records) if (record.type === "form") allErrors.push(...validate(record.document).errors);
  if (allErrors.length) return { id, outcome: "reject", diagnostic: allErrors[0].code };

  const first = records[0];
  const warnings = first.type === "form" ? [...new Set(validate(first.document).warnings.map(w => w.code))].sort() : [];

  const result = { id, outcome: "valid", digest: digestOf(first), warnings };

  if ((testCase.evaluates || testCase.expects) && first.type === "form") {
    try {
      result.evaluated = evaluate(first.document, testCase.evaluate || {});
    } catch (error) {
      result.evaluated = { error: error.name || "Error" };
    }
  }

  if (testCase.roundTrip) result.written = written(records);

  return result;
}

// core+attestations is deliberately unclaimed, matching the .NET/Python
// drivers: those cases ask a verifier to resolve a manifest and report what it
// found, which this reads and structurally accepts but does not yet verify. A
// case outside every claimed profile is left unanswered rather than guessed at.
const PROFILES = ["core", "core+streams", "core+expressions"];

function main() {
  const suite = JSON.parse(readFileSync(0, "utf8"));
  // A run that names fewer profiles gets an implementation claiming only those.
  const profiles = PROFILES.filter(profile => (suite.profiles ?? PROFILES).includes(profile));
  const claimed = suite.cases.filter(testCase => profiles.includes(testCase.profile));
  process.stdout.write(JSON.stringify({
    implementation: { name: "PromptResponse (TypeScript)", version: suite.formatVersion, profiles },
    results: claimed.map(testCase => answer(testCase, profiles)),
  }, null, 2));
}

main();

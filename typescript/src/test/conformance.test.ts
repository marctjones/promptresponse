import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";
import { AprParseError, dumps, inspectText, loads, recomputeComputedValues, renderHtml, validate } from "../index.js";

const corpus = join(process.cwd(), "..", "tests", "Conformance", "v1");
const files = (directory: string) => readdirSync(join(corpus, directory)).filter(file => file.endsWith(".apr") || file.endsWith(".aprt") || file.endsWith(".aprf"));
const text = (directory: string, file: string) => readFileSync(join(corpus, directory, file), "utf8");
const responses = (document: ReturnType<typeof loads>): Record<string, string> => {
  const result: Record<string, string> = {};
  const visit = (sections: typeof document.sections): void => sections.forEach(section => {
    section.prompts.forEach(prompt => { result[prompt.id] = prompt.response; });
    visit(section.sections);
  });
  visit(document.sections);
  return result;
};

test("valid fixtures parse, validate, and round-trip", () => {
  for (const file of files("valid")) {
    const document = loads(text("valid", file));
    assert.equal(validate(document).isValid, true, file);
    const roundTripped = loads(dumps(document));
    assert.equal(validate(roundTripped).isValid, true, `${file} after round-trip`);
    assert.deepEqual(responses(roundTripped), responses(document), `${file} responses`);
  }
});
test("core SDK preserves unimplemented profiles and newer members", () => {
  const signed = loads(text("valid", "signed-template.aprt"));
  assert.deepEqual(loads(dumps(signed)).signatures, signed.signatures);
  const newer = loads(text("valid", "newer-minor-accepted.aprt"));
  assert.deepEqual(loads(dumps(newer)).extra, newer.extra);
});
test("invalid fixtures parse then report structural errors", () => {
  for (const file of files("invalid")) {
    assert.equal(validate(loads(text("invalid", file))).isValid, false, file);
  }
});
test("malformed fixtures are rejected rather than coerced", () => {
  for (const file of files("malformed")) {
    assert.throws(() => loads(text("malformed", file)), AprParseError, file);
  }
});
test("Unicode safety inspection and rendering warn without rewriting a response", () => {
  const document = loads('{"version":"1.0-beta","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"L","response":"safe\\u202etxt.exe"}]}]}');
  assert.deepEqual(inspectText("safe\u202etxt.exe").map(finding => finding.code), ["BIDI_OVERRIDE"]);
  assert.equal(validate(document).isValid, true);
  assert.match(renderHtml(document), /apr-security-warning/);
  assert.match(renderHtml(document, { editable: false }), /<bdi>safe/);
  assert.equal(responses(loads(dumps(document))).p, "safe\u202etxt.exe");
});
test("shared Unicode safety fixture preserves text and reports every advisory category", () => {
  const document = loads(text("valid", "unicode-security-advisories.aprf"));
  const responseMap = responses(document);
  const warningCodes = new Set(validate(document).warnings.map(warning => warning.code));
  assert.equal(responseMap.bidi_override, "safe\u202etxt.exe");
  assert.equal(responseMap.persian_zwnj, "می‌روم");
  assert.equal(responseMap.emoji_zwj, "👨‍👩‍👧");
  for (const code of ["BIDI_OVERRIDE", "BIDI_ISOLATE", "HIDDEN_ZWNJ", "HIDDEN_ZWJ"]) assert.ok(warningCodes.has(code), code);
});
test("CEL binding computes typed values without overwriting a human correction", () => {
  const document = loads('{"version":"1.0-beta","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"qty","label":"Quantity","response":"3","hints":{"expectedDataType":"number"}},{"id":"price","label":"Price","response":"12.5","hints":{"expectedDataType":"currency"}},{"id":"total","label":"Total","response":"","hints":{"expectedDataType":"currency","exprValue":"qty * price"}}]}]}');
  assert.equal(recomputeComputedValues(document), true);
  const total = document.sections[0].prompts[2];
  assert.equal(total.response, "37.5");
  total.response = "40"; total.responseMetadata.source = undefined;
  assert.equal(recomputeComputedValues(document), false);
  assert.equal(total.response, "40");
});

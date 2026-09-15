import assert from "node:assert/strict";
import test from "node:test";
import { loads, dumps, validate } from "../index.js";

function codes(issues: { code: string }[]): string[] {
  return issues.map(issue => issue.code).sort();
}

test("a response the source never carried round-trips without an added response member", () => {
  const source = '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P"}]}]}';
  const written = dumps(loads(source));
  assert.doesNotMatch(written, /"response"/, "a prompt whose source never declared response gets none written back");
});

test("an explicit empty response is preserved, not dropped", () => {
  const source = '{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":""}]}]}';
  const written = JSON.parse(dumps(loads(source)));
  assert.equal(written.sections[0].prompts[0].response, "");
});

test("a non-NFC title is preserved exactly and reported, not silently cleaned", () => {
  const decomposed = "Café"; // "Café" spelled with a combining acute accent
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: decomposed },
    sections: [{ id: "s", title: "S", prompts: [{ id: "p", label: "P" }] }],
  }));
  assert.equal(document.metadata.title, decomposed, "the reader never rewrites human-facing text");
  assert.deepEqual(codes(validate(document).warnings), ["NON_NFC_TEXT"]);
});

test("a response or submission URL carrying an excluded code point warns; a carriage return in a response does not", () => {
  const form = (response: string, urls?: string[]) => loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "T", ...(urls ? { submissionUrls: urls } : {}) },
    sections: [{ id: "s", title: "S", prompts: [{ id: "p", label: "P", response }] }],
  }));
  assert.ok(codes(validate(form("Ad​a")).warnings).includes("RESPONSE_FORBIDDEN_CODE_POINT"));
  assert.ok(!codes(validate(form("one\ttwo\r\nthree")).warnings).includes("RESPONSE_FORBIDDEN_CODE_POINT"));
  assert.ok(codes(validate(form("", ["https://uploads.exa​mple.gov/permits"])).warnings).includes("SUBMISSION_URL_FORBIDDEN_CODE_POINT"));
});

test("an id outside the machine-key characters warns, never refuses (APR-TEXT-010)", () => {
  for (const [id, warns] of [["first name", true], ["café", true], ["q:1", true], ["first_name.v2-A", false]] as const) {
    const result = validate(loads(JSON.stringify({
      aprVersion: "1.0-beta.6", metadata: { title: "T" },
      sections: [{ id: "s", title: "S", prompts: [{ id, label: "P" }] }],
    })));
    assert.equal(result.errors.length, 0, id);
    assert.equal(codes(result.warnings).includes("ID_FORBIDDEN_CHARACTER"), warns, id);
  }
  const named = validate(loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "T" }, roles: [{ id: "the notary", name: "Notary" }],
    sections: [{ id: "the applicant", title: "S", prompts: [{ id: "p", label: "P" }] }],
  })));
  assert.equal(codes(named.warnings).filter(code => code === "ID_FORBIDDEN_CHARACTER").length, 2);
});

test("a hidden zero-width space in a title is reported as a forbidden code point", () => {
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "Permit​application" },
    sections: [{ id: "s", title: "S", prompts: [{ id: "p", label: "P" }] }],
  }));
  assert.deepEqual(codes(validate(document).warnings), ["FORBIDDEN_CODE_POINT"]);
});

test("a Cyrillic letter hidden in a Latin title is reported as a confusable script mix", () => {
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "Pаypal Permit" }, // U+0430 CYRILLIC SMALL LETTER A
    sections: [{ id: "s", title: "S", prompts: [{ id: "p", label: "P" }] }],
  }));
  assert.deepEqual(codes(validate(document).warnings), ["CONFUSABLE_SCRIPT_MIX"]);
});

test("a legitimate multi-script title is not flagged as confusable", () => {
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "Toyota パーツ Order Form" }, // "パーツ" (parts) in Katakana
    sections: [{ id: "s", title: "S", prompts: [{ id: "p", label: "P" }] }],
  }));
  assert.deepEqual(codes(validate(document).warnings), []);
});

test("an unregistered expectedDataType is a warning, not a rejection", () => {
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "T" },
    sections: [{ id: "s", title: "S", prompts: [{ id: "p", label: "P", hints: { expectedDataType: "carrier-pigeon" } }] }],
  }));
  const result = validate(document);
  assert.equal(result.isValid, true);
  assert.deepEqual(codes(result.warnings), ["UNREGISTERED_DATA_TYPE"]);
});

test("a table row that does not match the first row's field count is ragged", () => {
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "T" },
    sections: [{
      id: "t", title: "T", kind: "table",
      sections: [
        { id: "r1", title: "R1", prompts: [{ id: "r1.a", label: "A" }, { id: "r1.b", label: "B" }] },
        { id: "r2", title: "R2", prompts: [{ id: "r2.a", label: "A" }] },
      ],
    }],
  }));
  // A ragged row also can't have its labels compared meaningfully against the
  // first row, so it is reported as a label mismatch too (matching
  // python/promptresponse/validation.py's _validate_tables).
  assert.deepEqual(codes(validate(document).warnings), ["TABLE_LABEL_MISMATCH", "TABLE_RAGGED"]);
});

test("an empty table section is a structural error", () => {
  const document = loads(JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "T" },
    sections: [{ id: "t", title: "T", kind: "table", sections: [] }],
  }));
  const result = validate(document);
  assert.equal(result.isValid, false);
  assert.deepEqual(codes(result.errors), ["EMPTY_TABLE"]);
});

test("maxRows must be an integer, and at least 1", () => {
  // docs/BETA6_WIRE_DELTA.md types maxRows as "integer, at least 1". .NET's
  // model declares it C# int?, so a fractional JSON number fails to
  // deserialize at all; JSON has no separate integer type, so loads() must
  // check explicitly (issue #378).
  const section = (maxRows: number) => JSON.stringify({
    aprVersion: "1.0-beta.6", metadata: { title: "T" },
    sections: [{ id: "t", title: "T", kind: "table", maxRows, sections: [{ id: "r", title: "R", prompts: [{ id: "p", label: "P" }] }] }],
  });
  assert.throws(() => loads(section(2.5)), (error: unknown) => error instanceof Error && (error as { code?: string }).code === "WRONG_TYPE");
  for (const maxRows of [0, -3]) {
    const document = loads(section(maxRows));
    assert.deepEqual(codes(validate(document).errors), ["WRONG_TYPE"]);
  }
  assert.equal(validate(loads(section(5))).isValid, true);
});

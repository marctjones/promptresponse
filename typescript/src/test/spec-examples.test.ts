import assert from "node:assert/strict";
import test from "node:test";
import { readFile } from "node:fs/promises";
import { readBeta6Form, readBeta6Stream } from "../index.js";
import { validate } from "../validation.js";

/**
 * Runs the executable examples embedded in the APR specification.
 *
 * The vectors are generated from docs/APR_SPECIFICATION.md by
 * scripts/extract-spec-examples.py, so these are the specification's own claims
 * rather than a separately authored suite. Where an example and this reader
 * disagree, the specification is normative and the reader has the defect:
 * such cases belong in knownDivergences with the issue tracking them.
 */

interface Example {
  id: string;
  rule: string;
  representation: string;
  expect: string;
  document: string;
  diagnostic?: string;
}

/** Examples this reader does not yet satisfy. Each entry is a reader defect. */
const knownDivergences = new Map<string, string>();

const vectors = new URL(
  "../../../tests/Conformance/beta6/spec-examples.json",
  import.meta.url,
);

async function loadExamples(): Promise<Example[]> {
  const raw = await readFile(vectors, "utf8");
  return (JSON.parse(raw) as { examples: Example[] }).examples;
}

/**
 * Restores RFC 7464 framing to an APR-JSONC stream the specification prints with `---`.
 *
 * A record separator is invisible on a page, so the specification stands one in with a
 * `---` line. Handing that prose form straight to the reader tests the reader against a
 * document the format never defines. APR-YAML streams are untouched: there `---` is
 * genuinely the separator, not a stand-in for one.
 *
 * Mirrors `Framed()` in tests/PromptResponse.Core.Tests/Beta6/SpecExampleTests.cs.
 */
function framed(document: string): string {
  return document
    .split(/^---$/m)
    .filter(part => part.trim().length > 0)
    .map(part => `\u001e${part.replace(/^\n+|\n+$/g, "")}\n`)
    .join("");
}

function read(example: Example): any {
  const representation = example.representation.startsWith("yaml") ? "yaml" : "jsonc";
  if (example.representation.endsWith("-stream")) {
    return readBeta6Stream(
      representation === "jsonc" ? framed(example.document) : example.document,
      representation,
    );
  }
  return readBeta6Form(example.document, representation);
}

test("every specification example behaves as the specification says", async () => {
  const examples = await loadExamples();
  assert.ok(examples.length > 0, "no examples were extracted from the specification");

  const failures: string[] = [];
  for (const example of examples) {
    const divergence = knownDivergences.get(example.id);
    if (divergence) {
      failures.push(`${example.id}: known reader defect — ${divergence}`);
      continue;
    }

    if (
      !example.representation.endsWith("-stream") &&
      (example.document.includes('"recordType"') || example.document.includes("recordType:"))
    ) {
      // An attestation record belongs to core+attestations, which this SDK does not
      // claim. The conformance runner leaves such a case unanswered, and so does this.
      continue;
    }

    if (example.expect === "valid") {
      try {
        read(example);
      } catch (error) {
        failures.push(
          `${example.id} (#${example.rule}): the specification says this is valid, ` +
            `the reader rejected it — ${(error as Error).message}`,
        );
      }
      continue;
    }

    if (example.expect === "reject") {
      // Rejection is a refused read or a form that fails validation: a missing label
      // parses and is an error, as the conformance driver reports it.
      let accepted = false;
      try {
        const result = read(example);
        const documents = Array.isArray(result)
          ? result.filter(record => record.type === "form").map(record => record.document)
          : [result];
        // A read that yields no form holds no document, which is refused too (NULL_DOCUMENT).
        accepted = documents.length > 0 && documents.every(document => validate(document).errors.length === 0);
      } catch {
        // Rejected, as the specification requires.
      }
      if (accepted) {
        failures.push(
          `${example.id} (#${example.rule}): the specification requires rejection ` +
            `(${example.diagnostic ?? "no diagnostic named"}), the reader accepted it`,
        );
      }
      continue;
    }

    failures.push(`${example.id}: unrecognised expectation '${example.expect}'`);
  }

  assert.deepEqual(failures, [], `\n${failures.join("\n")}`);
});

test("every specification example cites a rule and carries a document", async () => {
  for (const example of await loadExamples()) {
    assert.ok(example.rule, `${example.id} must cite the specification anchor it demonstrates`);
    assert.ok(example.document.trim(), `${example.id} must carry a document`);
    if (example.expect === "reject") {
      assert.ok(example.diagnostic, `${example.id} must name a diagnostic`);
    }
  }
});

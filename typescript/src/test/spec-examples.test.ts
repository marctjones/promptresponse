import assert from "node:assert/strict";
import test from "node:test";
import { readFile } from "node:fs/promises";
import { readBeta6Form, readBeta6Stream } from "../index.js";

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

function read(example: Example): unknown {
  const representation = example.representation.startsWith("yaml") ? "yaml" : "jsonc";
  if (example.representation.endsWith("-stream")) {
    return readBeta6Stream(example.document, representation);
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
      let accepted = false;
      try {
        read(example);
        accepted = true;
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

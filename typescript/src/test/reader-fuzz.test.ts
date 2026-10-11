import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { AprParseError } from "../errors.js";
import { readBeta6Stream } from "../beta6.js";

// For any input the reader either returns records or throws AprParseError, and terminates
// promptly. An exception of any other type reaching the caller is a defect: a caller told the
// format is safe to open cannot defend against what it was never told about. Seeded from the
// specification's executable examples in every representation, with a fixed generator so a
// failure reproduces; the same corpus and mutations the other SDKs' suites use.
const EXAMPLES = fileURLToPath(new URL("../../../tests/Conformance/beta6/spec-examples.json", import.meta.url));
const POISON = "\"{}[],:\\/*\u001e\u0000\ud800￿&!%@`#|>-?<'\n\t ";
const BUDGET_MS = 5000;

test("hostile input is refused cleanly and promptly", () => {
  const examples = JSON.parse(readFileSync(EXAMPLES, "utf8")).examples as Array<{ id: string; representation: string; document: string }>;
  let state = 20261010;
  const next = (n: number): number => { state = (state * 1103515245 + 12345) & 0x7fffffff; return state % n; };
  const defects = new Map<string, string>();
  for (const example of examples) {
    const representation = example.representation.startsWith("yaml") ? "yaml" : "jsonc";
    const document = example.document;
    const mutations: Array<[string, string]> = [];
    for (let n = 0; n < document.length; n += Math.max(1, Math.floor(document.length / 80))) mutations.push([`truncated to ${n}`, document.slice(0, n)]);
    for (let i = 0; i < 80; i++) { const at = next(document.length); mutations.push([`char ${at}`, document.slice(0, at) + POISON[next(POISON.length)] + document.slice(at + 1)]); }
    for (let i = 0; i < 40; i++) {
      const from = next(document.length), span = 1 + next(Math.max(1, document.length / 4));
      mutations.push([`${span} chars deleted at ${from}`, document.slice(0, from) + document.slice(from + span)]);
      mutations.push(["a stray record separator", document.slice(0, next(document.length)) + "\u001e" + document.slice(next(document.length))]);
    }
    for (const [how, mutated] of mutations) {
      const started = Date.now();
      try { readBeta6Stream(mutated, representation); }
      catch (leaked) {
        if (!(leaked instanceof AprParseError)) {
          const kind = `${(leaked as Error)?.constructor?.name}: ${String((leaked as Error)?.message).split("\n")[0]}`;
          if (!defects.has(kind)) defects.set(kind, `${example.id} (${how})`);
        }
      }
      if (Date.now() - started > BUDGET_MS && !defects.has("took too long")) defects.set("took too long", example.id);
    }
  }
  assert.deepEqual([...defects], [], `the reader must refuse bad input with AprParseError and nothing else; kinds found: ${JSON.stringify([...defects])}`);
});

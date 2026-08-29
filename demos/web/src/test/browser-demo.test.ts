import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

test("browser demo advertises local-only behavior and loads the SDK", async () => {
  const source = await readFile(new URL("../main.js", import.meta.url), "utf8");
  assert.match(source, /renderHtml/);
  assert.match(source, /dumps\(documentModel\)/);
  assert.doesNotMatch(source, /fetch\s*\(/);
});

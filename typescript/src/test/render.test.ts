import assert from "node:assert/strict";
import test from "node:test";
import { loads, renderHtml } from "../index.js";

function htmlRendererPreservesAccessibleStructureAndEscapesContent(): void {
  const document = loads('{"version":"1.0-beta","metadata":{"title":"<Unsafe>"},"sections":[{"id":"first","title":"First","prompts":[{"id":"email","label":"Email","response":"<answer>","hints":{"expectedDataType":"email","helpText":"We will not <share> this"}},{"id":"notes","label":"Notes","response":"line one","hints":{"expectedDataType":"multiline"}}]}]}');
  const html = renderHtml(document);

  assert.match(html, /<h1>&lt;Unsafe&gt;<\/h1>/, "document title is escaped");
  assert.match(html, /<fieldset[^>]*data-apr-section="first"/, "section has a stable semantic wrapper");
  assert.match(html, /<label for="apr-email">Email\s*<\/label>/, "input has an accessible name");
  assert.match(html, /type="email"/, "email hint maps to the browser email widget");
  assert.match(html, /<textarea[^>]*id="apr-notes"[^>]*dir="auto"/, "multiline hint maps to an isolated textarea");
  assert.match(html, /aria-describedby="apr-email-help"/, "help text is associated with its field");
  assert.match(html, /We will not &lt;share&gt; this/, "help text is escaped");
  assert.match(html, /value="&lt;answer&gt;"/, "response is escaped");
  assert.ok(html.indexOf('data-apr-prompt="email"') < html.indexOf('data-apr-prompt="notes"'), "prompt order is preserved");
}

function htmlRendererFallsBackSafelyAndNeverContactsNetwork(): void {
  const document = loads('{"version":"1.0-beta","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"future","label":"Future","response":"x","hints":{"expectedDataType":"made-up-type"}}]}]}');
  const originalFetch = globalThis.fetch;
  let contactedNetwork = false;
  globalThis.fetch = (async () => {
    contactedNetwork = true;
    throw new Error("rendering must not fetch");
  }) as typeof fetch;
  try {
    const html = renderHtml(document);
    assert.match(html, /<input[^>]*type="text"/, "unknown types degrade to a text input");
  } finally {
    globalThis.fetch = originalFetch;
  }
  assert.equal(contactedNetwork, false);
}

test("HTML renderer preserves accessible structure and escapes content", htmlRendererPreservesAccessibleStructureAndEscapesContent);
test("HTML renderer has safe fallback and no network access", htmlRendererFallsBackSafelyAndNeverContactsNetwork);

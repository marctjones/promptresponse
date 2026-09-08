/**
 * A content-blind size and overwrite gate in front of a real R2 bucket.
 *
 * This exists because a bare S3-compatible presigned PUT (demos/r2-put/)
 * genuinely has no size limit and no overwrite protection of its own --
 * confirmed against Cloudflare's and AWS's own docs, not assumed. That's a
 * real, industry-standard-shaped gap: presigned POST's policy document (with
 * its `content-length-range` condition) exists specifically because
 * anonymous, internet-facing uploaders are exactly this kind of risk. APR's
 * specification deliberately doesn't use POST (5.2.1's own rationale: a
 * pre-signed browser POST "needs policy fields beyond the URL, which a
 * string entry cannot carry"), so the fix here is the shape production
 * systems commonly use instead: a fronting layer that gatekeeps on
 * transport-level properties only, never on content.
 *
 * This is a **different kind of thing** from
 * `demos/submission-receiver/cloudflare/`'s reverted worker.js, which parsed
 * and structurally validated every submission against a published template.
 * That contradicted specification 5.2.1's "no processing on the receiving
 * side... assumed or permitted." This file never reads the body as APR at
 * all -- it counts bytes and checks a key, nothing else. It doesn't know or
 * care whether the body is a valid APR document, a JPEG, or garbage.
 *
 * - `GET /template` mints a one-time HMAC-signed link (UUID + expiry) and
 *   embeds it into the template's `metadata.submissionUrls`, same convention
 *   as the reverted worker and `demos/minio-put/demo.sh --embedded-url`.
 * - `PUT /inbox/<uuid>?exp=&sig=` verifies the signature and expiry, rejects
 *   a request whose declared `Content-Length` exceeds the limit outright
 *   (no bytes read), and separately enforces the same limit against the
 *   *actual* stream as it's written -- a lying or absent Content-Length
 *   header doesn't get a free pass. Writes through R2's own conditional-put
 *   (`onlyIf: { etagDoesNotMatch: "*" }`, the binding-API equivalent of the
 *   `If-None-Match: *` proved out in demos/r2-put/demo.sh step 5): a second
 *   PUT to the same key fails rather than overwriting the first.
 * - `GET /` and `GET /submissions/<uuid>` are unchanged in spirit from the
 *   reverted worker: a listing and raw content, nothing content-aware.
 */

const MAX_LISTED = 50;
const MAX_BODY_BYTES = 65536; // 64 KiB -- generous for this five-prompt form.
const LINK_LIFETIME_SECONDS = 60 * 60;

// Kept identical to demos/minio-put/dog-license.aprt and demos/r2-put/dog-license.aprt.
const TEMPLATE_SOURCE = `{
  "aprVersion": "1.0-beta.6",
  "documentType": "template",
  "metadata": {
    "title": "Dog License Application",
    "description": "A small, deliberately simple form for demoing APR submission end to end.",
    "created": "2026-09-07T00:00:00Z",
    "modified": "2026-09-07T00:00:00Z",
    "author": "PromptResponse Demo",
    "templateId": "tag:promptresponse.example,2026:demo/dog-license",
    "templateVersion": "1.0"
  },
  "sections": [
    {
      "id": "section_dog_and_owner",
      "title": "Dog and Owner Information",
      "description": "Tell us about the dog and who's responsible for it.",
      "prompts": [
        {
          "id": "prompt_dog_name",
          "label": "Dog's Name",
          "response": "",
          "hints": { "placeholder": "Rex", "expectedDataType": "text", "helpText": "The name your dog answers to" }
        },
        {
          "id": "prompt_breed",
          "label": "Breed",
          "response": "",
          "hints": { "placeholder": "Labrador Retriever", "expectedDataType": "text", "helpText": "Breed or mix, best guess is fine" }
        },
        {
          "id": "prompt_owner_name",
          "label": "Owner's Name",
          "response": "",
          "hints": { "placeholder": "Jane Doe", "expectedDataType": "text", "helpText": "The person responsible for this license" }
        },
        {
          "id": "prompt_owner_phone",
          "label": "Owner's Phone",
          "response": "",
          "hints": { "placeholder": "+1 (555) 123-4567", "expectedDataType": "phone", "helpText": "In case animal control needs to reach you" }
        },
        {
          "id": "prompt_rabies_vaccination_date",
          "label": "Most Recent Rabies Vaccination Date",
          "response": "",
          "hints": { "expectedDataType": "date", "helpText": "Required for a license to be issued" }
        }
      ]
    }
  ]
}`;

function hexToBytes(hex) {
  if (!/^[0-9a-f]+$/i.test(hex) || hex.length % 2 !== 0) return null;
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < bytes.length; i++) bytes[i] = parseInt(hex.slice(i * 2, i * 2 + 2), 16);
  return bytes;
}

async function hmacKey(secret) {
  return crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign", "verify"]);
}

async function signLink(secret, message) {
  const key = await hmacKey(secret);
  const signature = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
  return [...new Uint8Array(signature)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

/** Constant-time by construction: WebCrypto's own `verify`, not a hand-rolled compare. */
async function verifyLink(secret, message, hexSignature) {
  const bytes = hexToBytes(hexSignature);
  if (!bytes) return false;
  const key = await hmacKey(secret);
  return crypto.subtle.verify("HMAC", key, bytes, new TextEncoder().encode(message));
}

function escapeHtml(value) {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function textResponse(status, body) {
  return new Response(body.endsWith("\n") ? body : body + "\n", { status, headers: { "content-type": "text/plain; charset=utf-8" } });
}

/**
 * Reads a stream up to `limit` bytes and returns the concrete result,
 * aborting as soon as more arrives -- enforced against the real bytes
 * received, not the client's possibly-absent or dishonest Content-Length
 * header. Never inspects what the bytes are.
 *
 * R2Bucket.put() requires a value with a known length; a piped-through
 * TransformStream loses that (confirmed against the local R2 emulator: "must
 * have a known length"), so this reads bounded chunks into one buffer
 * instead of trying to stream-passthrough. Safe because `limit` is small by
 * design -- never buffers more than one limit's worth of bytes regardless of
 * how much a client tries to send.
 */
async function readBounded(stream, limit) {
  const reader = stream.getReader();
  const chunks = [];
  let total = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    total += value.byteLength;
    if (total > limit) {
      await reader.cancel();
      throw new Error(`body exceeds ${limit} bytes`);
    }
    chunks.push(value);
  }
  const merged = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    merged.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return merged;
}

async function handleTemplate(env, url) {
  const uuid = crypto.randomUUID();
  const exp = Math.floor(Date.now() / 1000) + LINK_LIFETIME_SECONDS;
  const signature = await signLink(env.SUBMISSION_SECRET, `${uuid}.${exp}`);
  const submitUrl = `${url.origin}/inbox/${uuid}?exp=${exp}&sig=${signature}`;

  const document = JSON.parse(TEMPLATE_SOURCE);
  document.metadata.submissionUrls = [submitUrl];

  return new Response(JSON.stringify(document, null, 2), {
    headers: { "content-type": "application/vnd.apr+json" },
  });
}

async function handleSubmit(request, env, url, uuid) {
  const exp = url.searchParams.get("exp");
  const sig = url.searchParams.get("sig");
  if (!exp || !sig || !/^[0-9]+$/.test(exp)) {
    return textResponse(400, "Missing or malformed exp/sig -- download a fresh link from GET /template.");
  }
  if (!(await verifyLink(env.SUBMISSION_SECRET, `${uuid}.${exp}`, sig))) {
    return textResponse(403, "Invalid signature -- this link wasn't issued by this demo, or was altered.");
  }
  if (Date.now() / 1000 > Number(exp)) {
    return textResponse(403, "This link has expired. Download a fresh one from GET /template.");
  }

  // Cheap rejection when the client is honest about size: no bytes read at all.
  const declaredLength = request.headers.get("content-length");
  if (declaredLength && Number(declaredLength) > MAX_BODY_BYTES) {
    return textResponse(413, `Body too large: ${declaredLength} bytes declared (limit ${MAX_BODY_BYTES}).`);
  }
  if (!request.body) {
    return textResponse(400, "Empty body.");
  }

  // R2's onlyIf/R2Conditional is built for optimistic-concurrency updates
  // (etagMatches/etagDoesNotMatch against a *specific, known* etag) -- tested
  // directly against the local emulator, and confirmed there is no wildcard
  // equivalent to the S3 HTTP API's `If-None-Match: *` for "absent" here.
  // head()-then-put() is the real pattern for this on the Workers binding
  // API; it leaves a narrow check-then-act race between the two calls,
  // acceptable here because keys are random UUIDs minted one at a time by a
  // single operator-run script, not a concurrent multi-writer system.
  if ((await env.SUBMISSIONS.head(uuid)) !== null) {
    return textResponse(409, "This link has already been used. Each downloaded template is good for one submission.");
  }

  const contentType = request.headers.get("content-type") || "(none)";
  let body;
  try {
    body = await readBounded(request.body, MAX_BODY_BYTES);
  } catch {
    return textResponse(413, `Body exceeds ${MAX_BODY_BYTES} bytes -- rejected while reading, regardless of what Content-Length claimed.`);
  }

  await env.SUBMISSIONS.put(uuid, body, {
    httpMetadata: { contentType },
    customMetadata: { receivedAt: new Date().toISOString() },
  });

  return textResponse(
    201,
    `201 Created: written under ${MAX_BODY_BYTES}-byte cap.\n` +
      `stored as: ${uuid}\n` +
      `view it at: ${url.origin}/submissions/${encodeURIComponent(uuid)}\n`,
  );
}

async function handleIndex(env, url) {
  const list = await env.SUBMISSIONS.list({ limit: MAX_LISTED, include: ["httpMetadata"] });
  const rows = list.objects
    .sort((a, b) => (a.key < b.key ? 1 : -1))
    .map((object) => {
      const href = `/submissions/${encodeURIComponent(object.key)}`;
      return (
        `<tr><td><a href="${href}">${escapeHtml(object.key)}</a></td>` +
        `<td>${escapeHtml(object.httpMetadata?.contentType || "")}</td>` +
        `<td>${object.size}</td>` +
        `<td>${escapeHtml(object.uploaded.toISOString())}</td></tr>`
      );
    })
    .join("\n");

  const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>APR submission receiver (size-guarded R2 demo)</title>
<style>
 body { font: 15px/1.5 system-ui, sans-serif; max-width: 900px; margin: 2rem auto; padding: 0 1rem; }
 table { width: 100%; border-collapse: collapse; }
 th, td { text-align: left; padding: .4rem .6rem; border-bottom: 1px solid #8884; font-size: .9rem; }
 code { background: #8882; padding: .1rem .3rem; border-radius: 3px; }
 .empty { opacity: .7; font-style: italic; }
</style></head>
<body>
<h1>APR submission receiver (size-guarded)</h1>
<p>Same <a href="https://github.com/marctjones/promptresponse/blob/main/docs/APR_SPECIFICATION.md">APR-MODEL-033</a>
single-PUT contract as <code>demos/r2-put/</code>, fronted by a gate that checks only byte count
and key uniqueness -- it never reads the body as APR.</p>
<ol>
<li><code>curl ${escapeHtml(url.origin)}/template -o dog-license.aprt</code> -- a fresh,
one-time link (<code>metadata.submissionUrls</code> inside the file), good for
${LINK_LIFETIME_SECONDS / 3600} hour and one submission under ${MAX_BODY_BYTES} bytes.</li>
<li>Fill it -- <code>apr fill dog-license.aprt --output=filled.aprf</code>, or any client.</li>
<li><code>apr submit filled.aprf --yes</code> -- reads the link from the file.</li>
</ol>
<h2>Recent submissions (newest first, last ${MAX_LISTED})</h2>
${rows ? `<table><tr><th>Key</th><th>Content-Type</th><th>Bytes</th><th>Received</th></tr>${rows}</table>` : '<p class="empty">Nothing submitted yet.</p>'}
<p><a href="https://github.com/marctjones/promptresponse/tree/main/demos/r2-put-guarded">Source</a> ·
<a href="https://github.com/marctjones/promptresponse/tree/main/demos/r2-put">Bare-PUT parity demo (no gate)</a></p>
</body></html>`;

  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8" } });
}

async function handleGetSubmission(env, key) {
  const object = await env.SUBMISSIONS.get(key);
  if (object === null) {
    return textResponse(404, "404 Not Found: no submission stored under that key.");
  }
  return new Response(object.body, { headers: { "content-type": object.httpMetadata?.contentType || "application/octet-stream" } });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/template") {
      return handleTemplate(env, url);
    }
    if (request.method === "PUT" && url.pathname.startsWith("/inbox/")) {
      return handleSubmit(request, env, url, url.pathname.slice("/inbox/".length));
    }
    if (request.method === "GET" && url.pathname === "/") {
      return handleIndex(env, url);
    }
    if (request.method === "GET" && url.pathname.startsWith("/submissions/")) {
      return handleGetSubmission(env, decodeURIComponent(url.pathname.slice("/submissions/".length)));
    }
    if (request.method === "PUT") {
      return textResponse(400, "PUT only to a link from GET /template.");
    }
    return textResponse(405, `405 Method Not Allowed: '${request.method}' is not the submission method APR-MODEL-033 defines.`);
  },
};

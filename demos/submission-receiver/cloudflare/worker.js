/**
 * A live, publicly-reachable HTTPS submission receiver for the transport
 * APR-MODEL-033 defines (docs/APR_SPECIFICATION.md, section 5.2.1): a client
 * sends a single HTTP PUT of the complete document to a pre-signed-style URL.
 *
 * This is the hosted counterpart to podman/submission-receiver/ -- the same
 * PUT-not-POST contract, running on Cloudflare Workers instead of a local
 * container. Unlike the podman one, this receiver only accepts a completion
 * of the one template it publishes:
 *
 * - `GET /template` mints a one-time signed link (an HMAC over a fresh UUID
 *   and an expiry, verified with WebCrypto so the comparison is constant-time
 *   without this file hand-rolling one) and embeds it into the template's own
 *   `metadata.submissionUrls`, the same way `demos/minio-put/demo.sh`'s
 *   `--embedded-url` mode does locally. Fill the template with `apr fill` (or
 *   any client) and `apr submit` reads that URL with no `--url` needed.
 * - `PUT /inbox/<uuid>?exp=&sig=` verifies the signature and expiry, rejects a
 *   second PUT to an already-used link, and -- using the real
 *   `@promptresponse/core` beta.6 reader, not a hand-rolled parser -- rejects
 *   anything that isn't `documentType: "filledForm"` of this exact
 *   `templateId`/`templateVersion` whose structure (every id, label, hint,
 *   and section -- everything except `response` and `metadata.modified`)
 *   matches the published template exactly.
 *
 *   That structural match is a **demo policy this file invents, not an APR
 *   rule**: the specification requires `templateId` on a filled form
 *   (APR-SEC-008) but never says a receiver must verify structural fidelity,
 *   and is explicit that `templateId` is a URI a reader must never fetch. A
 *   core-only reader elsewhere is free to accept a completion of any
 *   template; this is what a demo that must reject tampering looks like on
 *   top of that, not a claim about what APR itself requires.
 * - Every other method except GET: 405 Method Not Allowed.
 * - `GET /`: a small HTML page listing recent submissions (newest first).
 * - `GET /submissions/<uuid>`: the raw stored content of one submission.
 *
 * Stored submissions expire from KV after `SUBMISSION_TTL_SECONDS` on their
 * own (KV's own `expirationTtl`, not a cleanup job). No authentication beyond
 * the signed link -- anyone who completes the download-fill-submit flow can
 * submit once per link, which is the point of the exercise.
 */

import { readBeta6Form } from "../../../typescript/dist/beta6.js";

const MAX_LISTED = 50;
const MAX_BODY_BYTES = 65536; // 64 KiB -- generous for this five-prompt form.
const MAX_RESPONSE_CHARS = 500;
const SUBMISSION_TTL_SECONDS = 4 * 60 * 60; // Stored objects self-delete after 4 hours.
const LINK_LIFETIME_SECONDS = 60 * 60; // A downloaded link is good for 1 hour.
const TEMPLATE_ID = "tag:promptresponse.example,2026:demo/dog-license";
const TEMPLATE_VERSION = "1.0";

// Kept identical to demos/minio-put/dog-license.aprt on purpose -- the same
// form, two receivers.
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

function noExtra(extra, what) {
  if (extra && Object.keys(extra).length > 0) {
    throw new Error(`unexpected member on ${what}: ${Object.keys(extra).join(", ")}`);
  }
}

function fingerprintHints(hints) {
  if (!hints) return null;
  noExtra(hints.extra, "hints");
  const { expectedDataType, placeholder, helpText, validationPattern, suggestedValues, min, max, step, exprHidden, exprValue, exprExpected, exprValidation, exprReadOnly } = hints;
  return { expectedDataType: expectedDataType ?? null, placeholder: placeholder ?? null, helpText: helpText ?? null, validationPattern: validationPattern ?? null, suggestedValues: suggestedValues ?? [], min: min ?? null, max: max ?? null, step: step ?? null, exprHidden: exprHidden ?? null, exprValue: exprValue ?? null, exprExpected: exprExpected ?? null, exprValidation: exprValidation ?? null, exprReadOnly: exprReadOnly ?? null };
}

function fingerprintPrompt(prompt) {
  noExtra(prompt.extra, `prompt "${prompt.id}"`);
  // `response` is deliberately excluded -- it's the one thing a filler may change.
  return { id: prompt.id, label: prompt.label, role: prompt.role ?? null, hints: fingerprintHints(prompt.hints) };
}

function fingerprintSection(section) {
  noExtra(section.extra, `section "${section.id}"`);
  return {
    id: section.id, title: section.title, description: section.description ?? null,
    kind: section.kind ?? null, canAddRows: section.canAddRows ?? null, maxRows: section.maxRows ?? null,
    role: section.role ?? null,
    prompts: section.prompts.map(fingerprintPrompt),
    sections: section.sections.map(fingerprintSection),
  };
}

function fingerprintRole(role) {
  noExtra(role.extra, `role "${role.id}"`);
  return { id: role.id, name: role.name ?? null, description: role.description ?? null };
}

/**
 * Everything a filler may not change, as one comparable string. Excludes
 * `response` throughout and `metadata.created`/`modified`/`submissionUrls` --
 * the first two legitimately change on fill, and `submissionUrls` carries
 * this download's own one-time signed link.
 */
function fingerprintDocument(document) {
  noExtra(document.extra, "the document");
  noExtra(document.metadata.extra, "metadata");
  return JSON.stringify({
    title: document.metadata.title, description: document.metadata.description ?? null,
    author: document.metadata.author ?? null, publisher: document.metadata.publisher ?? null,
    templateId: document.metadata.templateId ?? null, templateVersion: document.metadata.templateVersion ?? null,
    roles: (document.roles ?? []).map(fingerprintRole),
    sections: document.sections.map(fingerprintSection),
  });
}

const REFERENCE_FINGERPRINT = fingerprintDocument(readBeta6Form(TEMPLATE_SOURCE, "jsonc"));

function* allPrompts(sections) {
  for (const section of sections) {
    yield* section.prompts;
    yield* allPrompts(section.sections);
  }
}

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

async function handleTemplate(env, url) {
  const uuid = crypto.randomUUID();
  const exp = Math.floor(Date.now() / 1000) + LINK_LIFETIME_SECONDS;
  const message = `${uuid}.${exp}`;
  const signature = await signLink(env.SUBMISSION_SECRET, message);
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
  if ((await env.SUBMISSIONS.get(uuid)) !== null) {
    return textResponse(409, "This link has already been used. Each downloaded template is good for one submission.");
  }

  const bodyBuffer = await request.arrayBuffer();
  if (bodyBuffer.byteLength > MAX_BODY_BYTES) {
    return textResponse(413, `Body too large: ${bodyBuffer.byteLength} bytes (limit ${MAX_BODY_BYTES}).`);
  }

  let text;
  try {
    text = new TextDecoder("utf-8", { fatal: true }).decode(bodyBuffer);
  } catch {
    return textResponse(422, "Body is not valid UTF-8.");
  }

  let document;
  const errors = [];
  for (const representation of ["jsonc", "yaml"]) {
    try {
      document = readBeta6Form(text, representation);
      break;
    } catch (error) {
      errors.push(`${representation}: ${error.message}`);
    }
  }
  if (!document) {
    return textResponse(422, `Not a valid APR beta.6 form, as either representation:\n${errors.join("\n")}`);
  }

  if (document.version !== "1.0-beta.6") {
    return textResponse(422, `aprVersion must be "1.0-beta.6", got "${document.version}".`);
  }
  if (document.documentType !== "filledForm") {
    return textResponse(422, 'documentType must be "filledForm" -- fill the template before submitting it.');
  }
  if (document.metadata.templateId !== TEMPLATE_ID || document.metadata.templateVersion !== TEMPLATE_VERSION) {
    return textResponse(422, "This isn't a completion of the published template (templateId/templateVersion mismatch).");
  }
  for (const prompt of allPrompts(document.sections)) {
    if (prompt.response.length > MAX_RESPONSE_CHARS) {
      return textResponse(413, `Response for "${prompt.id}" is too long (limit ${MAX_RESPONSE_CHARS} characters).`);
    }
  }

  let fingerprint;
  try {
    fingerprint = fingerprintDocument(document);
  } catch (error) {
    return textResponse(422, `Rejected: ${error.message}. This demo only accepts an unmodified fill of the published template -- an answer may change, nothing else.`);
  }
  if (fingerprint !== REFERENCE_FINGERPRINT) {
    return textResponse(422, "Rejected: this document's structure doesn't match the published template. Only response values (and metadata.modified) may differ from what GET /template returned.");
  }

  const contentType = request.headers.get("content-type") || "(none)";
  await env.SUBMISSIONS.put(uuid, bodyBuffer, {
    expirationTtl: SUBMISSION_TTL_SECONDS,
    metadata: { path: url.pathname, contentType, bytes: bodyBuffer.byteLength, receivedAt: new Date().toISOString() },
  });

  return textResponse(
    201,
    `201 Created: ${bodyBuffer.byteLength} bytes written.\n` +
      `stored as: ${uuid}\n` +
      `view it at: ${url.origin}/submissions/${encodeURIComponent(uuid)}\n` +
      `deletes itself in ${SUBMISSION_TTL_SECONDS / 3600} hours.\n`,
  );
}

async function handleIndex(env, url) {
  const list = await env.SUBMISSIONS.list({ limit: MAX_LISTED });
  const rows = list.keys
    .sort((a, b) => (a.name < b.name ? 1 : -1))
    .map((entry) => {
      const meta = entry.metadata || {};
      const href = `/submissions/${encodeURIComponent(entry.name)}`;
      return (
        `<tr><td><a href="${href}">${escapeHtml(entry.name)}</a></td>` +
        `<td>${escapeHtml(meta.contentType || "")}</td>` +
        `<td>${meta.bytes ?? ""}</td>` +
        `<td>${escapeHtml(meta.receivedAt || "")}</td></tr>`
      );
    })
    .join("\n");

  const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>APR submission receiver (demo)</title>
<style>
 body { font: 15px/1.5 system-ui, sans-serif; max-width: 900px; margin: 2rem auto; padding: 0 1rem; }
 table { width: 100%; border-collapse: collapse; }
 th, td { text-align: left; padding: .4rem .6rem; border-bottom: 1px solid #8884; font-size: .9rem; }
 code { background: #8882; padding: .1rem .3rem; border-radius: 3px; }
 .empty { opacity: .7; font-style: italic; }
</style></head>
<body>
<h1>APR submission receiver</h1>
<p>A live demo of the HTTPS submission transport
<a href="https://github.com/marctjones/promptresponse/blob/main/docs/APR_SPECIFICATION.md">APR-MODEL-033</a>
defines: a single HTTP <code>PUT</code> to a pre-signed-style URL. This one only
accepts a completion of the one template it publishes:</p>
<ol>
<li><code>curl ${escapeHtml(url.origin)}/template -o dog-license.aprt</code> --
each download is a fresh, one-time link (<code>metadata.submissionUrls</code>
inside the file), good for ${LINK_LIFETIME_SECONDS / 3600} hour and one submission.</li>
<li>Fill it -- <code>apr fill dog-license.aprt --output=filled.aprf</code>, or any
client.</li>
<li><code>apr submit filled.aprf --yes</code> -- reads the link from the file, no
<code>--url</code> needed.</li>
</ol>
<p>Editing anything but the answers (or resubmitting to an expired or already-used
link) gets refused, not silently accepted.</p>
<h2>Recent submissions (newest first, last ${MAX_LISTED})</h2>
${rows ? `<table><tr><th>Key</th><th>Content-Type</th><th>Bytes</th><th>Received</th></tr>${rows}</table>` : '<p class="empty">Nothing submitted yet.</p>'}
<p><a href="https://github.com/marctjones/promptresponse/tree/main/demos/submission-receiver/cloudflare">Source</a> ·
<a href="https://github.com/marctjones/promptresponse/tree/main/demos/submission-receiver/podman">Local (podman) equivalent</a></p>
</body></html>`;

  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8" } });
}

async function handleGetSubmission(env, key) {
  const object = await env.SUBMISSIONS.getWithMetadata(key, { type: "arrayBuffer" });
  if (!object || object.value === null) {
    return textResponse(404, "404 Not Found: no submission stored under that key.");
  }
  const contentType = object.metadata?.contentType || "application/octet-stream";
  return new Response(object.value, { headers: { "content-type": contentType } });
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
      return textResponse(400, "PUT only to a link from GET /template -- this receiver no longer accepts an arbitrary path.");
    }

    return textResponse(
      405,
      "405 Method Not Allowed: this receiver accepts only PUT to a signed /inbox/ link (and GET for the demo page).\n" +
        `'${request.method}' is not the submission method APR-MODEL-033 defines.`,
    );
  },
};

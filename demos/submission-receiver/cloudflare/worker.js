/**
 * A live, publicly-reachable HTTPS submission receiver for the transport
 * APR-MODEL-033 defines (docs/APR_SPECIFICATION.md, section 5.2.1): a client
 * sends a single HTTP PUT of the complete document to a pre-signed-style URL.
 *
 * This is the hosted counterpart to podman/submission-receiver/ -- the same
 * contract, running on Cloudflare Workers instead of a local container, so a
 * demo doesn't require anyone to run anything locally. Both exist because they
 * serve different audiences: podman/ for someone with this repo checked out,
 * this one for a link anyone can PUT to.
 *
 * - PUT to any path: stores the raw body in Workers KV, unmodified, keyed by a
 *   timestamp and the request path, and answers 201. Content-Type is recorded,
 *   never validated or rejected -- specification 5.2.1 says "no processing on
 *   the receiving side is assumed or permitted to be needed."
 * - Every other method except GET: 405 Method Not Allowed with an
 *   "Allow: PUT" header. This is the point of the exercise, same as the
 *   podman receiver: it is exactly what would have caught a client sending
 *   POST where the spec requires PUT.
 * - GET /: a small HTML page listing recent submissions (newest first) so the
 *   demo has something to look at, not just a bare API.
 * - GET /submissions/<key>: the raw stored content of one submission.
 *
 * No authentication, no authorization -- this stands in for a pre-signed URL,
 * whose whole point is that "the grant travels in the query string, so the
 * client never holds a credential" (specification 5.2.1's rationale). Anyone
 * with the URL can PUT to it during the life of this demo.
 */

const MAX_LISTED = 50;

function safeName(pathname) {
  const trimmed = pathname.replace(/^\/+/, "").replace(/\/+$/, "");
  const safe = (trimmed || "root").replace(/[^A-Za-z0-9._-]+/g, "_");
  return safe.replace(/^[._]+|[._]+$/g, "") || "root";
}

function isoStamp(date) {
  return date.toISOString().replace(/[:.]/g, "").replace("Z", "Z");
}

function escapeHtml(value) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

async function handlePut(request, env, url) {
  const body = await request.arrayBuffer();
  const contentType = request.headers.get("content-type") || "(none)";
  const timestamp = isoStamp(new Date());
  const name = safeName(url.pathname);
  const key = `${timestamp}__${name}`;

  await env.SUBMISSIONS.put(key, body, {
    metadata: {
      path: url.pathname,
      contentType,
      bytes: body.byteLength,
      receivedAt: new Date().toISOString(),
    },
  });

  const summary =
    `201 Created: ${body.byteLength} bytes written.\n` +
    `path: ${url.pathname}\n` +
    `content-type seen (not validated): ${contentType}\n` +
    `stored as: ${key}\n` +
    `view it at: ${url.origin}/submissions/${encodeURIComponent(key)}\n`;

  return new Response(summary, {
    status: 201,
    headers: { "content-type": "text/plain; charset=utf-8" },
  });
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
        `<td>${escapeHtml(meta.path || "")}</td>` +
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
defines: a single HTTP <code>PUT</code> to a pre-signed-style URL. <code>PUT</code> to
any path here to see it work; anything else (including <code>POST</code>, the
mechanism this spec deliberately does not use) gets <code>405</code>.</p>
<p>Try it: <code>curl -X PUT --data-binary @your-form.apr.json -H "Content-Type: application/vnd.apr+json" ${escapeHtml(url.origin)}/inbox/demo</code></p>
<h2>Recent submissions (newest first, last ${MAX_LISTED})</h2>
${rows ? `<table><tr><th>Key</th><th>Path</th><th>Content-Type</th><th>Bytes</th><th>Received</th></tr>${rows}</table>` : '<p class="empty">Nothing submitted yet.</p>'}
<p><a href="https://github.com/marctjones/promptresponse/tree/main/cloudflare/submission-receiver">Source</a> ·
<a href="https://github.com/marctjones/promptresponse/tree/main/podman/submission-receiver">Local (podman) equivalent</a></p>
</body></html>`;

  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8" } });
}

async function handleGetSubmission(env, key) {
  const object = await env.SUBMISSIONS.getWithMetadata(key, { type: "arrayBuffer" });
  if (!object || object.value === null) {
    return new Response("404 Not Found: no submission stored under that key.\n", { status: 404 });
  }
  const contentType = object.metadata?.contentType || "application/octet-stream";
  return new Response(object.value, { headers: { "content-type": contentType } });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "PUT") {
      return handlePut(request, env, url);
    }

    if (request.method === "GET" && url.pathname === "/") {
      return handleIndex(env, url);
    }

    if (request.method === "GET" && url.pathname.startsWith("/submissions/")) {
      const key = decodeURIComponent(url.pathname.slice("/submissions/".length));
      return handleGetSubmission(env, key);
    }

    return new Response(
      `405 Method Not Allowed: this receiver accepts only PUT (and GET for the demo page).\n` +
      `'${request.method}' is not the submission method APR-MODEL-033 defines.\n`,
      { status: 405, headers: { Allow: "PUT, GET" } },
    );
  },
};

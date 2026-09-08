# APR submission receiver (hosted demo)

The hosted counterpart to [`demos/submission-receiver/podman/`](../podman/):
the same PUT-not-POST contract per specification 5.2.1 (`APR-MODEL-033`),
running on Cloudflare Workers instead of a local container, so trying it
doesn't require anyone to check out this repo or run podman.

Unlike the podman receiver (and unlike this file's own earlier version),
this one only accepts a completion of the one template it publishes: it
mints a one-time signed download link, and rejects anything that isn't an
unmodified fill of that exact template arriving on that exact link.

**Status: built and tested locally, not deployed.** Nothing here is live.
Deploying it makes it a public, unauthenticated endpoint anyone with a
downloaded link can `PUT` a completed form to, under your Cloudflare account
— that's a deliberate decision for you to make, not something to happen as a
side effect of writing the code. See **Deploy** below when you're ready.

## The flow

1. `GET /template` — mints a fresh UUID and a 1-hour expiry, signs
   `<uuid>.<exp>` with `SUBMISSION_SECRET` (HMAC-SHA256, verified later with
   WebCrypto's own constant-time `verify`, not a hand-rolled comparison), and
   returns the template with that link written into its own
   `metadata.submissionUrls` — the same convention
   [`demos/minio-put/demo.sh`](../../minio-put/demo.sh)'s `--embedded-url`
   mode uses locally. No KV write happens here; the link is stateless until
   it's used.
2. Fill it offline with `apr fill dog-license.aprt --output=filled.aprf` (or
   any client) and `apr submit filled.aprf --yes` — it reads the link from
   the file, no `--url` needed.
3. `PUT /inbox/<uuid>?exp=&sig=` verifies the signature and expiry, then
   parses the body with the real `@promptresponse/core` beta.6 reader
   (`typescript/dist/beta6.js`, imported directly — not a sixth hand-rolled
   APR parser) and rejects the submission unless **all** of:
   - the signature and expiry check out, and this link hasn't been used yet
     (one KV `get` before the `put` — a second `PUT` to the same link gets
     `409`);
   - it parses as a valid beta.6 form, JSONC or YAML;
   - `documentType` is `"filledForm"` and `templateId`/`templateVersion`
     match the published template;
   - every prompt's `response` is under 500 characters, and the whole body is
     under 64 KiB;
   - **the rest of the document — every section, prompt, id, label, hint, and
     role, everything except `response` and `metadata.modified` — is
     byte-for-byte the same shape as the published template.** Add a section,
     relabel a prompt, widen a bound, and it's refused, not silently accepted.

   That last check is a **demo policy this file invents, not an APR rule**:
   the specification requires `templateId` on a filled form (`APR-SEC-008`)
   but never asks a reader to verify structural fidelity against it, and is
   explicit that `templateId` is a URI a reader must never fetch
   (specification 5.2 checklist). A core-only reader elsewhere is free to
   accept a completion of any template; this is what a demo that must reject
   tampering looks like layered on top of that, not a claim about what APR
   itself requires.
4. Accepted submissions are stored in Workers KV with `expirationTtl` set to
   4 hours — KV deletes them itself, no cleanup job.
5. `GET /`: an HTML page listing recent submissions (newest first, escaped
   before it's ever put in HTML). `GET /submissions/<uuid>`: one submission's
   raw stored content, served back with the `Content-Type` it arrived with.
6. Every method except `PUT`/`GET`: `405`. A `PUT` to anything other than a
   valid `/inbox/<uuid>` link: `400` — this receiver no longer accepts an
   arbitrary path the way the podman one still does.

## What's here

- `worker.js` — the whole receiver. Imports `readBeta6Form` directly from
  `typescript/dist/beta6.js` (bypassing the package's own barrel export, and
  therefore `pkijs` and `cel-js`, which that reader doesn't need) — confirmed
  to bundle (~55 KiB gzipped) and run correctly under both
  `wrangler deploy --dry-run` and `wrangler dev --local` in this session.
  Requires `typescript/` to be built (`npm run build` there) before
  `wrangler dev`/`deploy`, since it imports the compiled `dist/`, not the
  TypeScript source.
- `wrangler.toml` — config, including the KV namespace binding. The
  namespace (`APR_SUBMISSIONS`, id in `wrangler.toml`) already exists in your
  account — `wrangler kv namespace create` was run to produce it, since
  `wrangler.toml` needs a real id to reference. It is empty and inert until
  the Worker is deployed and something is `PUT` to it.
- `SUBMISSION_SECRET` — **not in this directory.** For local dev, put it in a
  git-ignored `.dev.vars` file (`SUBMISSION_SECRET=<anything>`, one line).
  For the real deployment, `wrangler secret put SUBMISSION_SECRET` — it never
  touches a file or this repo.

## Local testing (already done, repeatable)

```bash
cd typescript && npm run build && cd ../demos/submission-receiver/cloudflare
echo "SUBMISSION_SECRET=dev-only-local-secret" > .dev.vars
wrangler dev --local --port 8790
```

`--local` runs entirely on your machine against a local KV emulation —
nothing reaches your real Cloudflare account or the namespace above. In a
second terminal:

```bash
curl http://localhost:8790/template -o dog-license.aprt
# the file now has a signed link in metadata.submissionUrls; fill it and
# PUT it to that link (apr submit, or curl -X PUT directly) to see 201.

# POST fails -- 405. This is still the point.
curl -X POST --data-binary @dog-license.aprt -D - http://localhost:8790/inbox/whatever

open http://localhost:8790/   # or curl it
```

Verified in this session: a genuine fill-and-submit round trip gets `201`; a
relabeled prompt gets `422`; a forged or altered signature gets `403`; a
validly-signed but expired link gets `403`; a second `PUT` to an
already-used link gets `409`; a body over 64 KiB gets `413`; `POST` still
gets `405`; the index page and `/submissions/<uuid>` still work as before.

## Deploy

When you decide to make this live:

```bash
cd demos/submission-receiver/cloudflare
wrangler secret put SUBMISSION_SECRET   # prompts for the value; not stored in any file
wrangler deploy
```

This publishes to a `*.workers.dev` URL under your account (or a custom
route/domain if you configure one — not set up here). From then on it is a
real, public endpoint — unauthenticated in the sense that anyone can request
a link and submit once with it, same as the pre-signed-URL model it
demonstrates — until you remove it:

```bash
wrangler delete apr-submission-receiver   # tears down the Worker
wrangler kv namespace delete --namespace-id 8fc50ee8c60942ca91c0a784b6666724  # and the data
```

## What this receiver deliberately doesn't do

- No authentication beyond the signed link itself — same posture as the
  podman receiver and the same reason: this stands in for a pre-signed URL,
  whose whole point is that "the grant travels in the query string, so the
  client never holds a credential" (specification 5.2.1's rationale).
- No validation or rejection based on the `Content-Type` header — logged,
  never gated on, matching specification 5.2.1's "no processing on the
  receiving side is assumed or permitted to be needed" and APR-SEC-014.
- No retention beyond the 4-hour KV TTL. `GET /` still lists every
  submission and links straight to its full content, same as before this
  change — a deliberate choice to keep the demo's behavior unchanged rather
  than a new privacy guarantee. Don't `PUT` anything to a deployed instance
  you wouldn't want a stranger to read back or see listed.
- No rate limiting beyond "one link, one submission" — a determined visitor
  can still request many links in a row. Add a Cloudflare rate-limiting rule
  in front of `GET /template` if that becomes a problem in practice; nothing
  here does it yet.

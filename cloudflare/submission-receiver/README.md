# APR submission receiver (hosted demo)

The hosted counterpart to [`podman/submission-receiver/`](../../podman/submission-receiver/):
the same contract — a single HTTP `PUT` per specification 5.2.1
(`APR-MODEL-033`), `POST` rejected with `405` — running on Cloudflare Workers
instead of a local container, so trying it doesn't require anyone to check
out this repo or run podman.

**Status: built and tested locally, not deployed.** Nothing here is live.
Deploying it makes it a public, unauthenticated endpoint anyone with the URL
can `PUT` arbitrary data to, under your Cloudflare account — that's a
deliberate decision for you to make, not something to happen as a side
effect of writing the code. See **Deploy** below when you're ready.

## What's here

- `worker.js` — the whole receiver, one file, no build step, no dependencies
  beyond the Workers runtime itself.
- `wrangler.toml` — config, including the KV namespace binding. The
  namespace (`APR_SUBMISSIONS`, id in `wrangler.toml`) already exists in your
  account — `wrangler kv namespace create` was run to produce it, since
  `wrangler.toml` needs a real id to reference. It is empty and inert until
  the Worker is deployed and something is `PUT` to it.

## What it does

- `PUT` to any path: stores the raw body in Workers KV, unmodified, and
  answers `201`. The `Content-Type` header is recorded, never validated or
  rejected — same reasoning as the podman receiver: specification 5.2.1 says
  "no processing on the receiving side is assumed or permitted to be
  needed."
- Every method except `PUT`/`GET`: `405 Method Not Allowed` with
  `Allow: PUT, GET`. This is the point of the exercise: it's exactly what
  would have caught the desktop client's now-fixed bug of sending `POST`
  instead of `PUT`.
- `GET /`: an HTML page listing recent submissions (newest first), so the
  demo has something to look at rather than being a bare API. Each entry
  links to the stored content.
- `GET /submissions/<key>`: the raw stored content of one submission, served
  back with the `Content-Type` it arrived with.

## Local testing (already done, repeatable)

```bash
cd cloudflare/submission-receiver
wrangler dev --local --port 8790
```

`--local` runs entirely on your machine against a local KV emulation —
nothing reaches your real Cloudflare account or the namespace above. In a
second terminal:

```bash
echo '{"aprVersion":"1.0-beta.6","hello":"world"}' > /tmp/test.json

# PUT succeeds — 201, byte-identical storage.
curl -X PUT --data-binary @/tmp/test.json -H "Content-Type: application/vnd.apr+json" \
  -D - http://localhost:8790/inbox/demo

# POST fails — 405. This is the point.
curl -X POST --data-binary @/tmp/test.json -D - http://localhost:8790/inbox/demo

# See it listed and served back.
open http://localhost:8790/   # or curl it
```

Verified in this session: `PUT` returns `201` and the stored content is
byte-for-byte identical to what was sent (`diff` clean); `POST` returns
`405`; the index page lists the submission with correct path, content-type,
and byte count; fetching `/submissions/<key>` returns the original bytes
with the original `Content-Type`.

## Deploy

When you decide to make this live:

```bash
cd cloudflare/submission-receiver
wrangler deploy
```

This publishes to a `*.workers.dev` URL under your account (or a custom
route/domain if you configure one — not set up here). From then on it is a
real, public, unauthenticated write endpoint until you remove it:

```bash
wrangler delete apr-submission-receiver   # tears down the Worker
wrangler kv namespace delete --namespace-id 8fc50ee8c60942ca91c0a784b6666724  # and the data
```

## What this receiver deliberately doesn't do

Same posture as the podman one, for the same reason — this stands in for a
pre-signed URL, whose whole point is that "the grant travels in the query
string, so the client never holds a credential" (specification 5.2.1's
rationale):

- No authentication, no authorization, no credentials.
- No validation or rejection based on `Content-Type` — logged, not gated on.
- No retention policy beyond "whatever's in KV until you delete it" — this
  is a demo, not a service with a data-lifecycle commitment. Don't `PUT`
  anything to a deployed instance you wouldn't want a stranger to read back
  from `/submissions/<key>` or see listed on `/`.

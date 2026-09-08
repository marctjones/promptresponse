# APR submission demo (size- and overwrite-guarded, real R2)

A companion to [`demos/r2-put/`](../r2-put/), not a replacement for it.
`demos/r2-put/` proves the bare claim -- specification 5.2.1's presigned PUT
works unmediated against a real S3-compatible bucket, with genuinely zero
server-side processing. This demo answers the question that came up while
building that one: a bare presigned PUT has no size limit and no overwrite
protection of its own (confirmed against Cloudflare's and AWS's own docs,
not assumed), and for a link handed to an anonymous, untrusted filler on the
open internet, that's a real gap -- the same reason the wider industry often
reaches for presigned *POST* for exactly this kind of upload. APR's
specification deliberately doesn't use POST (5.2.1's rationale: it "needs
policy fields beyond the URL, which a string entry cannot carry"), so this
is the shape production systems commonly use instead: a thin fronting layer
that gatekeeps on transport-level properties only, never on content.

**This is a different kind of thing from
[`demos/submission-receiver/cloudflare/`](https://github.com/marctjones/promptresponse/tree/main)'s
reverted Worker**, which parsed and structurally validated every submission
against a published template -- exactly the "processing on the receiving
side" specification 5.2.1 says isn't needed, and it was removed for that
reason. `worker.js` here never reads the body as APR at all. It counts
bytes and checks whether a key already exists. That's it. It doesn't know
or care whether the body is a valid APR document, a JPEG, or garbage.

## What it does

- `GET /template` mints a one-time HMAC-signed link (UUID + expiry) and
  embeds it into the template's own `metadata.submissionUrls`, same
  convention as `demos/minio-put/demo.sh --embedded-url`.
- `PUT /inbox/<uuid>?exp=&sig=` verifies the signature and expiry (WebCrypto's
  own constant-time `verify`), then:
  - Rejects outright (`413`) if the declared `Content-Length` exceeds 64 KiB
    -- no bytes read.
  - Separately enforces the same 64 KiB limit against the *actual* stream as
    it arrives, so a lying or absent `Content-Length` (chunked transfer)
    doesn't get a free pass. Verified locally: a real oversized body sent
    with no `Content-Length` header still gets rejected while reading.
  - Rejects (`409`) if a submission already exists under this link's key --
    checked with `head()` before writing. **Not a single atomic operation**:
    R2's Workers-binding `onlyIf` conditional (tested directly against the
    local emulator) is built for optimistic-concurrency updates against a
    *known* etag, not an "absent" wildcard the way the S3 HTTP API's
    `If-None-Match: *` is -- there is a narrow check-then-act race between
    the `head()` and the `put()`. Acceptable here because keys are random
    UUIDs minted one at a time by an operator-run script, not a
    high-concurrency multi-writer system.
- `GET /` and `GET /submissions/<uuid>` are unchanged in spirit from the
  reverted worker: a listing and raw content, nothing content-aware.

## Setup and running it

Same as [`demos/r2-put/README.md`](../r2-put/README.md#setup) for creating a
bucket, except this one binds to R2 natively from the Worker
(`wrangler.toml`'s `r2_buckets`) rather than presigning S3 API calls, so you
don't need a separate R2 API token for it -- just a bucket. Update
`wrangler.toml`'s `bucket_name` to a real bucket you've created, then:

```bash
cd demos/r2-put-guarded
echo "SUBMISSION_SECRET=$(openssl rand -hex 32)" > .dev.vars   # local dev only
wrangler dev --local   # or `wrangler deploy` once you're ready to go live,
                        # after `wrangler secret put SUBMISSION_SECRET`
```

Verified locally in this session (`wrangler dev --local`, R2 emulated):
a genuine fill-and-submit round trip gets `201`; a second PUT to the same
link gets `409`; a declared-oversized body gets `413` immediately; a
chunked body with no `Content-Length` that's actually oversized gets `413`
while being read, before ever reaching R2; a nonexistent key gets `404`.

## What this still doesn't solve

- **Bucket-wide storage/cost abuse from many separate links.** Each link is
  capped at one 64 KiB write, but nothing here limits *how many* links get
  issued. Neither R2 nor real AWS S3 offers a native total-bucket-size quota
  that blocks further writes -- this is confirmed, not a gap in this demo
  specifically. The actual lever is controlling how `GET /template` gets
  called (rate limiting, requiring some form of accountability before
  minting a link) if this is ever exposed more broadly than an
  operator-run script.
- **The `head()`-then-`put()` race noted above.** Vanishingly unlikely to
  matter here, but it is not the atomic guarantee a true `If-None-Match: *`
  would be.

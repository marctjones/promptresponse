# APR submission demo (real Cloudflare R2 bucket)

The R2 counterpart to [`demos/minio-put/`](../minio-put/): the same proof --
a real S3-compatible pre-signed PUT URL, the real unmodified `apr` CLI, no
server-side processing of the submitted content at all -- against a real
hosted bucket instead of a throwaway local container.

This replaced an earlier version of this demo that ran a custom Cloudflare
Worker parsing and validating each submission's structure server-side.
That was a mistake: specification 5.2.1 says a submission target needs "no
processing on the receiving side... assumed or permitted," and the whole
point of this exercise is proving a *genuine, unmodified* S3-compatible PUT
works, the same claim `demos/minio-put/` already proves locally. A receiver
that inspects the body isn't testing that claim; it's testing something
else. See git history if you want the old worker.js.

## Setup

You need one R2 bucket and two separate R2 API tokens -- **do not reuse one
token for both roles**, since they have very different blast radii if
either ever leaks.

1. **Create the bucket** (Cloudflare dashboard → R2, or `wrangler r2 bucket
   create <name>`).
2. **Create a write token for this script** (dashboard → R2 → Manage R2 API
   Tokens → Create API Token): permission **Object Read & Write**, scoped to
   **this bucket only**. This is what signs presigned PUT (and this script's
   own verification download) -- it never leaves your machine, and is never
   shared with anyone testing the demo. Set the four values below from it:

   ```bash
   export R2_ACCOUNT_ID=...
   export R2_ACCESS_KEY_ID=...
   export R2_SECRET_ACCESS_KEY=...
   export R2_BUCKET=...
   ```

3. **If you want someone else to download a submitted file**, create a
   *second*, independent token: permission **Object Read only**, scoped to
   the same bucket, with an **expiration date** set. Share that token's
   access key/secret pair with them directly (not through this repo, not
   through any script) -- it's what you hand out, precisely because it can
   only read this one bucket, can't write or delete anything, and expires on
   its own. They use it exactly like any S3-compatible read credential, e.g.:

   ```bash
   aws s3 --endpoint-url https://<account-id>.r2.cloudflarestorage.com \
     cp s3://<bucket>/submissions/<key> ./downloaded.aprf \
     --profile <a profile configured with the read-only token>
   ```

   Revoke it from the dashboard the moment you're done sharing it -- an
   expiration date is a backstop, not a reason to skip revoking early.

## Run it

```bash
cd demos/r2-put
./demo.sh
```

Fills the dog license template with the real CLI, presigns a real PUT URL
against your bucket, submits with the real CLI (no container, no
certificate workaround -- R2's certificate is already trusted by your
system), downloads the object back with your own write token, and verifies
it byte-for-byte and with `apr diff`.

## Security notes

- **A presigned PUT URL is safe to hand to someone**, by design: it's scoped
  to one bucket, one key, one content type, and expires (this demo signs for
  1 hour). The signature travels in the query string; no credential does.
  Specification 5.2.1's own rationale is exactly this: "the grant travels in
  the query string, so the client never holds a credential."
- **The one real gap**: unlike a presigned *POST*, a presigned PUT has no
  built-in size cap -- S3's `content-length-range` condition is a POST
  policy feature, and specification 5.2.1 deliberately excludes POST as a
  submission mechanism at all ("a pre-signed browser POST is deliberately
  absent"). So nothing here stops someone holding a PUT link from uploading
  a large object to that one key before it expires. Fine for a small number
  of trusted testers; something to know before handing PUT links out more
  broadly.
- **Never share the write token.** It can write (and, depending on exact
  permission chosen, delete) inside the bucket; a presigned link it signs
  can only touch the one object it was signed for.
- **R2 lifecycle rules are day-granularity, not hour-granularity** (this is
  also true of real AWS S3 -- not an R2 gap). Nothing here auto-deletes a
  submitted object; `demo.sh` prints the exact command to remove one when
  you're done.

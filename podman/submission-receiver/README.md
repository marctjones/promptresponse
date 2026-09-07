# APR submission receiver (demo)

A minimal, self-contained receiver for the HTTPS submission transport that
APR-MODEL-033 defines (`docs/APR_SPECIFICATION.md`, section 5.2.1,
"Submission targets"): a client sends a single HTTP `PUT` of the complete
document to a URL the document itself names in `metadata.submissionUrls`,
using the URL verbatim. The spec says directly that "what the receiver
holds after a PUT is the request body, byte for byte: the stream as the
client wrote it, already a valid APR file," and its rationale for the
section states that "a pre-signed browser **POST** is deliberately
absent" from the format — this is `PUT`, not `POST`, and that distinction
is load-bearing.

This receiver exists to make that contract concrete: it accepts `PUT` to
any path and writes the body to disk unmodified, and it rejects every
other method — including `POST` — with `405 Method Not Allowed`. That
405 is the whole point of the exercise: it is exactly what would have
caught the desktop client's now-fixed bug of sending `POST` instead of
`PUT`.

It is a demo/operational tool, not product code — it lives outside
`src/`, `tests/`, and `scripts/` on purpose. It lives under a new
top-level `podman/` rather than the existing `docker/` because the only
thing in `docker/` today is the legacy, unrelated `docker-compose.s3-test.yml`
pair (see the bottom of this README) — folding a podman-first receiver
into that directory would wrongly imply the two are related, so this gets
its own root.

## What's here

- `server.py` — the receiver. Python standard library only
  (`http.server` + `ssl`): no `pip install`, no framework, one file. This
  repo already has a `python/` toolchain and CPython's stdlib needs
  nothing extra inside the container, so this is the simplest option that
  is genuinely simplest, not just the default one.
- `Containerfile` — builds the receiver into a minimal image and bakes in
  a throwaway self-signed TLS certificate at build time (see **Why
  HTTPS, and why baked-in TLS** below).

## Why HTTPS, and why baked-in TLS

This isn't cosmetic. The CLI's delivery adapter
(`src/PromptResponse.Cli/Host/CliDelivery.cs`) hard-codes
`target.Scheme == Uri.UriSchemeHttps` as the only web transport it
supports — a plain `http://` URL is refused before any request is even
attempted. So a demo that wants to exercise the real `apr submit` command
needs a real TLS listener, not just a URL that starts with `https`.

Rather than adding a reverse proxy in front of the container, the
`Containerfile` generates a self-signed certificate with `openssl` at
**build time** and bakes it into the image, and `server.py` terminates
TLS itself with the stdlib `ssl` module. That's a few extra lines in a
Containerfile, not a second moving part. The certificate's subject
alternative names cover `DNS:localhost` and `IP:127.0.0.1` (a bare `CN`
is not enough — .NET's TLS stack checks SANs, not the subject CN).

This is a **throwaway demo certificate that nothing on your host machine
is asked to trust**. Because it's self-signed and untrusted by default:

- `curl` needs `-k` (or `--cacert` pointed at the exported cert) to talk
  to it.
- The real `apr submit` CLI, run directly on macOS, will fail with an
  SSL trust error — .NET on macOS validates TLS through the system
  Keychain, and this repo's automation does not add anything to your
  Keychain or system trust store on its own initiative. If you want to
  run `apr submit` from your host shell against this receiver, that step
  is yours to take, e.g. export the container's cert
  (`podman cp <container>:/certs/receiver.pem .`) and add it to your own
  trust store, or use `dotnet dev-certs https --trust` with a cert you
  control. This README's worked example instead runs the CLI itself
  inside a container where only *that* container's trust store is
  extended — see **Worked example, step 3**.

## Build and run

```bash
cd podman/submission-receiver
podman build -t apr-submission-receiver:demo .

mkdir -p "$HOME/apr-receiver-data"   # or any host directory you want the PUTs written to
podman run -d --name apr-receiver-demo \
  -p 8443:8443 \
  -v "$HOME/apr-receiver-data:/data:Z" \
  apr-submission-receiver:demo

podman logs apr-receiver-demo
```

Note for macOS: podman machine's VM only bind-mounts host paths under
`$HOME` (and `/private/tmp`) — a plain `/tmp/...` bind mount fails with
`statfs: no such file or directory` even though the path exists on the
Mac. Every `-v` below therefore uses a path under `$HOME`; host-only
paths that aren't bind-mounted into a container (a `curl` input file, the
`--output=` target of `apr fill`) can go anywhere.

You should see:

```
apr submission receiver: listening on https://0.0.0.0:8443
apr submission receiver: writing accepted PUTs under /data
apr submission receiver: PUT succeeds (201); every other method fails (405) -- that distinction is the point.
```

Every accepted `PUT` is written under the mounted directory as
`<UTC-timestamp>__<path-with-separators-replaced>`, e.g.
`20260907T182157362703Z__inbox_contact-intake` for a `PUT` to
`/inbox/contact-intake`. The timestamp keeps repeated demo runs from
overwriting each other; the path is folded in only for traceability —
the receiver does not otherwise interpret it, matching a pre-signed
URL's path being opaque.

## The demonstration that matters: PUT works, POST doesn't

```bash
echo '{"hello":"world"}' > "$HOME/demo.json"

# PUT succeeds — 201 Created, and the body lands on disk unmodified.
curl -sk -X PUT --data-binary @"$HOME/demo.json" \
  -H "Content-Type: application/vnd.apr+json" \
  -D - https://localhost:8443/some/opaque/path

# POST fails — 405 Method Not Allowed. This is the point: a client that
# sends POST where the spec requires PUT gets caught here, not silently
# accepted.
curl -sk -X POST --data-binary @"$HOME/demo.json" \
  -H "Content-Type: application/vnd.apr+json" \
  -D - https://localhost:8443/some/opaque/path
```

Verified output from an actual run of this receiver:

```
$ curl -sk -X PUT --data-binary @test-doc.json -H "Content-Type: application/vnd.apr+json" -D - https://localhost:8443/some/opaque/path
HTTP/1.1 201 Created
...
201 Created: 72 bytes written.
path: /some/opaque/path
content-type seen (not validated): application/vnd.apr+json
stored as: 20260907T182039013338Z__some_opaque_path

$ diff test-doc.json apr-receiver-data/20260907T182039013338Z__some_opaque_path
(no output — byte-for-byte identical)

$ curl -sk -X POST --data-binary @test-doc.json -H "Content-Type: application/vnd.apr+json" -D - https://localhost:8443/some/opaque/path
HTTP/1.1 405 Method Not Allowed
Allow: PUT
...
```

## Worked example: fill a real form, submit it, and watch it land

This uses `examples/contact-intake.aprt`, one of the repo's small real
templates, and the CLI's actual current `fill` and `submit` commands
(checked against `src/PromptResponse.Cli/Commands/FillCommand.cs`,
`src/PromptResponse.Cli/Commands/Fill/FillCommandOptions.cs`, and
`src/PromptResponse.Cli/Commands/SubmitCommand.cs` — this is not guessed
syntax).

### 1. Build the CLI

The repo's documented toolchain uses the official .NET SDK, not a
Homebrew one. If `dotnet` on `PATH` isn't 10.0.400, point at the official
install explicitly:

```bash
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet build src/PromptResponse.Cli -c Release
```

### 2. Fill the example form

```bash
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet src/PromptResponse.Cli/bin/Release/net10.0/apr.dll \
  fill examples/contact-intake.aprt \
  --non-interactive \
  --set-prompt_full_name="Jane Doe" \
  --set-prompt_email="jane.doe@example.com" \
  --set-prompt_phone="+1 555 000 1234" \
  --set-prompt_organization="Acme Corp" \
  --set-prompt_address="123 Main St, Springfield" \
  --set-prompt_preferred_method="Email" \
  --output="$HOME/contact-intake.aprf"
```

`examples/contact-intake.aprt` declares no `submissionUrls`, so
`apr submit` accepts any `--url=` you give it (a document that *does*
declare targets restricts `--url` to one of them — see `SubmitCommand`'s
check).

### 3. Submit it to the running receiver

```bash
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet src/PromptResponse.Cli/bin/Release/net10.0/apr.dll \
  submit "$HOME/contact-intake.aprf" --url=https://localhost:8443/inbox/contact-intake --yes
```

Run directly on macOS this fails with a TLS trust error (see **Why
HTTPS** above) — the certificate is self-signed and this task does not
add it to your Keychain. To actually run the unmodified `apr` binary
against the receiver, do it inside a container whose *own*, disposable
trust store includes the receiver's cert (no host changes at all):

```bash
# Publish a self-contained linux build of the CLI (adjust -r for your host arch)
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet publish src/PromptResponse.Cli \
  -c Release -r linux-arm64 --self-contained true -o "$HOME/apr-cli-publish"

# Export the receiver's cert and build a tiny verification image that trusts
# only it, inside that image
podman cp apr-receiver-demo:/certs/receiver.pem "$HOME/receiver.pem"
mkdir -p "$HOME/apr-cli-verify" && cp -r "$HOME/apr-cli-publish" "$HOME/apr-cli-verify/publish"
cp "$HOME/receiver.pem" "$HOME/apr-cli-verify/receiver.pem"
cat > "$HOME/apr-cli-verify/Containerfile" <<'EOF'
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY receiver.pem /usr/local/share/ca-certificates/apr-receiver.crt
RUN update-ca-certificates
COPY publish/ /app/
WORKDIR /app
ENTRYPOINT ["./apr"]
EOF
podman build -t apr-cli-verify:demo "$HOME/apr-cli-verify"

podman run --rm --network=host \
  -v "$HOME/contact-intake.aprf:/data/contact-intake.aprf:Z" \
  apr-cli-verify:demo submit /data/contact-intake.aprf \
  --url=https://localhost:8443/inbox/contact-intake --yes
```

Verified output from an actual run:

```
contact-intake.aprf delivered to https://localhost:8443/inbox/contact-intake: HTTP 201 Created
```

### 4. What appears on disk

```
$ ls "$HOME/apr-receiver-data"
20260907T182157362703Z__inbox_contact-intake

$ diff "$HOME/contact-intake.aprf" "$HOME/apr-receiver-data/20260907T182157362703Z__inbox_contact-intake"
(no output — byte-for-byte identical, 3804 bytes both sides)
```

The delivered content matches the filled `.aprf` exactly — the CLI sends
`serializer.Serialize(document)` rather than the file's raw bytes, so an
exact match also depends on the serializer round-tripping the document
unchanged; for this example it does.

## Cleanup

```bash
podman rm -f apr-receiver-demo
podman rmi apr-submission-receiver:demo
# and, if you built the verification image from step 3:
podman rmi apr-cli-verify:demo
```

## What this receiver deliberately doesn't do

- No authentication, no authorization, no credentials. The spec's stated
  reason for defining no authentication step is that "a pre-signed URL
  *is* the authorisation: the grant travels in the query string, so the
  client never holds a credential." This toy receiver stands in for that
  pre-signed target, so it accepts anything `PUT` to it during a demo.
- No state beyond the files written for the current run.
- No validation or rejection based on `Content-Type` — it's logged, not
  gated on. The spec says "no processing on the receiving side is
  assumed or permitted to be needed." Both `application/vnd.apr+json` and
  `application/vnd.apr+yaml` (and anything else) are accepted without
  error.

## Relationship to `docker/docker-compose.s3-test.yml`

That pre-existing pair (`docker/docker-compose.s3-test.yml` and
`scripts/test-s3-upload.sh`) is unrelated and not reused here: it tests a
pre-signed **POST** against MinIO (the exact mechanism this spec
deliberately rejects), references an `apr s3-setup` command that no
longer exists, and uses pre-beta.6 wire format fields. It's legacy and
out of scope for this receiver.

# Testing against a real S3-compatible server (MinIO)

[`../submission-receiver/`](../submission-receiver/) proves the CLI and
desktop client send `PUT`, and only `PUT`, against a minimal receiver that
accepts exactly that. This directory proves the same client behavior against
a *real* S3-compatible implementation — real pre-signed URL signature
verification, real `Content-Type` binding into the signature, and real
rejection of a method the signature wasn't computed for — rather than a
toy server that would accept whatever it's handed.

Both are legitimate and test different things. Neither replaces the other.

## Why this needed its own setup, not `mc share upload`

MinIO's own client (`mc`) has a `share upload` command, but it generates a
**pre-signed POST** (a browser form with `policy`/`signature` fields) — the
exact mechanism specification 5.2.1's rationale explicitly rejects ("a
pre-signed browser POST is deliberately absent"). There is no `mc presign
put` equivalent. A real pre-signed *PUT* URL is generated the way any S3
client library does it: `boto3.generate_presigned_url("put_object", ...)`,
which is what [`presign.py`](presign.py) does.

This is also why the repo's earlier `docker/docker-compose.s3-test.yml` +
`scripts/test-s3-upload.sh` pair — removed 2026-09-07, not reused anywhere
in this demo work — tested the wrong thing: it used `mc share upload`'s
pre-signed POST against MinIO, which is realistic MinIO usage but not what
this specification defines. It also referenced an `apr s3-setup` command
that no longer exists and pre-beta.6 wire format fields, so it was doubly
unusable regardless of the transport question.

## What's here

- `Containerfile` — a real `minio/minio` image with a throwaway self-signed
  TLS certificate baked in at build time (two stages, because the official
  MinIO image is a minimal RHEL-UBI base with no package manager and no
  `openssl` to generate one directly — see the Containerfile's own
  comments). Same reasoning as `../submission-receiver/`'s Containerfile:
  the CLI's delivery adapter requires `https`, so a demo needs a real TLS
  listener, and nothing on your host machine is asked to trust this
  certificate.
- `presign.py` — generates a real pre-signed PUT URL against a running
  instance of this image.

## Build and run

```bash
cd podman/minio-put-test
podman build -t apr-minio-put-test:demo .
podman run -d --name apr-minio-put-test \
  -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin123 \
  apr-minio-put-test:demo
```

Data lives inside the container (no volume mount) — this is a throwaway
test instance, not a persistent one. Stop and remove it when done; nothing
survives.

## Create a bucket

Using MinIO's own client, containerized (no local `mc` install needed):

```bash
mkdir -p mc-config
mc() { podman run --rm --network=host -v "$(pwd)/mc-config:/root/.mc:Z" docker.io/minio/mc:latest --insecure "$@"; }

mc alias set localminio https://localhost:9000 minioadmin minioadmin123
mc mb localminio/apr-test
```

`mc-config/` persists the alias across invocations (each `podman run` is a
fresh container otherwise) — it's a local demo artifact, not something to
commit.

## Generate a real pre-signed PUT URL

```bash
uv run --with boto3 python3 presign.py apr-test inbox/demo.aprf
```

Prints a URL like:

```
https://localhost:9000/apr-test/inbox/demo.aprf?AWSAccessKeyId=minioadmin&Signature=...&content-type=application%2Fvnd.apr%2Bjson&Expires=...
```

This is a genuine S3 pre-signed URL: the signature covers the bucket, key,
content-type, and expiry, and MinIO verifies it exactly as a real AWS S3
endpoint would.

## Submit a real filled form to it, with the real CLI

Same containerized-CLI approach as `../submission-receiver/`'s worked
example, for the same reason: the certificate above is self-signed, and
`apr submit` run directly on macOS validates TLS through the system
Keychain, which this task does not touch. Publish the CLI and run it in a
container whose own, disposable trust store includes this MinIO instance's
certificate:

```bash
# From the repo root
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet publish src/PromptResponse.Cli \
  -c Release -r linux-arm64 --self-contained true -o /tmp/apr-cli-publish

mkdir -p /tmp/apr-cli-verify && cp -r /tmp/apr-cli-publish /tmp/apr-cli-verify/publish
podman cp apr-minio-put-test:/root/.minio/certs/public.crt /tmp/apr-cli-verify/minio.crt
cat > /tmp/apr-cli-verify/Containerfile <<'EOF'
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY minio.crt /usr/local/share/ca-certificates/minio-test.crt
RUN update-ca-certificates
COPY publish/ /app/
WORKDIR /app
ENTRYPOINT ["./apr"]
EOF
podman build -t apr-cli-verify-minio:demo /tmp/apr-cli-verify

# Fill a real example form (adjust prompt ids/values as you like)
DOTNET_ROOT=~/.dotnet ~/.dotnet/dotnet run --project src/PromptResponse.Cli --no-build -- \
  fill examples/contact-intake.aprt --non-interactive \
  --set-prompt_full_name="Jane Doe" --set-prompt_email="jane.doe@example.com" \
  --output=/tmp/apr-cli-verify/contact-intake.aprf

# Submit it (macOS: podman only bind-mounts paths under $HOME and /private/tmp)
podman run --rm --network=host \
  -v /private/tmp/apr-cli-verify/contact-intake.aprf:/data/contact-intake.aprf:Z \
  apr-cli-verify-minio:demo submit /data/contact-intake.aprf \
  --url="<the presigned URL from the previous step>" --yes
```

Verified output from an actual run:

```
contact-intake.aprf delivered to https://localhost:9000/apr-test/inbox/demo.aprf?AWSAccessKeyId=minioadmin&Signature=...&Expires=...: HTTP 200 OK
```

## Verify what MinIO actually holds

```bash
mc cat localminio/apr-test/inbox/demo.aprf > /tmp/from-minio.aprf
diff /tmp/apr-cli-verify/contact-intake.aprf /tmp/from-minio.aprf   # clean — byte-identical
mc stat localminio/apr-test/inbox/demo.aprf                        # Content-Type: application/vnd.apr+json
```

Verified in this session: byte-identical, correct `Content-Type` recorded
by MinIO from the actual PUT request.

## The demonstration that matters here: real S3 rejects POST too

```bash
curl -sk -X POST --data-binary @/tmp/apr-cli-verify/contact-intake.aprf \
  "<the same presigned URL>"
```

Verified output from an actual run:

```
<?xml version="1.0" encoding="UTF-8"?>
<Error><Code>BadRequest</Code><Message>An unsupported API call for method: POST at '/apr-test/inbox/demo.aprf'</Message>...</Error>
HTTP 400
```

A real S3-compatible server refuses `POST` against a URL signed for `PUT` —
not just the minimal receiver's toy `405`, but MinIO's own signature
verification saying no.

## Cleanup

```bash
podman rm -f apr-minio-put-test
podman rmi apr-minio-put-test:demo apr-cli-verify-minio:demo
rm -rf mc-config /tmp/apr-cli-publish /tmp/apr-cli-verify
```

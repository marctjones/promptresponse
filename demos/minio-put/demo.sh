#!/usr/bin/env bash
#
# End-to-end demo: a real MinIO instance, real presigned PUT URLs, and three
# independent clients -- the real unmodified `apr` CLI, the real Avalonia
# desktop GUI, and the real Python SDK -- each filling and submitting their
# own "dog license" form, in both APR-JSONC and APR-YAML, and verification
# that what MinIO holds matches what each client actually sent.
#
# Non-persistent: MinIO runs with no volume mount, so all of it -- the
# bucket, the object, everything -- disappears the moment the container is
# removed. Nothing here survives a re-run or a reboot on purpose.
#
# Usage:
#   ./demo.sh                 # pass the URL on the command line, via --url=
#   ./demo.sh --embedded-url  # the URL travels inside the document instead:
#                              # metadata.submissionUrls, and `apr submit`
#                              # reads it from there with no --url at all.
#
# What it does, in order:
#   1. Builds and starts a non-persistent MinIO container (see Containerfile).
#   2. Creates a bucket and (deliberately, for this demo only) allows
#      anonymous read/list on it, so the raw S3 listing is visible without
#      logging in -- printed to the terminal either way, in case the MinIO
#      Console step below doesn't apply to your image or setup.
#   3. Generates a real S3 presigned PUT URL (presign.py).
#   4. Fills dog-license.aprt with sample answers using the real `apr fill`.
#      With --embedded-url, the presigned URL is written into the
#      template's metadata.submissionUrls *before* filling, so the filled
#      document already names its own delivery target.
#   5. Runs the real, unmodified `apr` CLI (published fresh from this
#      checkout, run inside a throwaway container that trusts this MinIO
#      instance's certificate -- see ../submission-receiver/podman/README.md for
#      why this dance exists: your Mac's Keychain is never touched) to
#      submit the filled form. Without --embedded-url this passes --url=
#      explicitly; with it, apr submit is given no --url at all and reads
#      metadata.submissionUrls from the file, then a second submit attempt
#      with a *different* --url is shown being refused, since the document
#      already names its one target.
#   6. Drives the real, unmodified Desktop app -- the shipped App, views,
#      MainShellViewModel, and HttpsSubmissionService -- headlessly: types
#      into its actual rendered form fields and clicks the real "Submit via
#      HTTPS" command, which PUTs to a second presigned URL. No container is
#      needed for this one (unlike step 5): HttpsSubmissionService already
#      takes an HttpMessageHandler through its constructor, so the demo
#      pins trust to this MinIO instance's exact certificate bytes for this
#      one HttpClient instance, rather than asking anything to trust it more
#      broadly. A screenshot of the filled form is saved so you can see what
#      the GUI actually rendered, and a real "Save" (the same FileService a
#      Save menu action uses) writes a local copy beside it, both filled
#      with different test data than the CLI's own (Buddy/Beagle/Sam Rivera
#      vs. Rex/Labrador/Jane Doe) so the two objects are visibly distinct.
#   7. Submits a second representation of the *same* CLI-filled answers --
#      APR-YAML this time, not APR-JSONC -- with the same real, unmodified
#      `apr` CLI, to a third presigned URL whose signature is bound to
#      `application/vnd.apr+yaml` (specification 5.2.1 / APR-SEC-012-014).
#      `SubmitCommand.MediaTypeFor` picks that content type from the
#      `.apr.yaml` extension, the same convention `FilledFormWriter` and the
#      desktop's `AprDocumentPersistence` use to choose a representation on
#      write.
#   8. Submits a third, independent client's representation: the real Python
#      SDK (`python/.venv`, not a container) fills the same template with its
#      own answers and writes APR-YAML using the writer landed for issue
#      #402 -- before that fix Python could read APR-YAML but had no writer
#      for it at all -- then PUTs it with `urllib`, pinned to this MinIO
#      instance's certificate, to a fourth presigned URL.
#   9. Downloads all four objects back from MinIO and checks each two ways: a
#      byte diff against the local file it came from (for the GUI, that's a
#      three-way check -- local save, the exact bytes its HTTP client put on
#      the wire, and the download all have to be identical, since nothing
#      in between re-serializes the document), and `apr diff` -- the real
#      CLI's semantic, form-level comparison (documentType, section/prompt
#      structure, every response by prompt id) rather than a byte diff.
#      `apr diff` reads APR-JSONC and APR-YAML alike and compares the parsed
#      forms, so it agrees two files are the same form even if one is JSON
#      and the other is YAML: the CLI's own YAML object is diffed against its
#      JSON one here to prove exactly that, since both carry identical
#      answers. The GUI's and Python's objects are also validated with the
#      real CLI, and each YAML object's stored `Content-Type` is confirmed
#      from MinIO's own response header, not assumed.
#  10. Prints the raw S3 ListObjects listing for the bucket, so you can see
#      all four submitted files without opening anything -- plus the MinIO
#      Console URL and login, if you want to look yourself.
#
# What this does not cover: a mail handoff leg. The desktop has no mail
# compose integration yet -- issue #103 is open -- so there is nothing here
# for this script to drive; it isn't a gap in the script.
#
# Leaves the MinIO container running so you can look around. Cleanup
# instructions print at the end.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$SCRIPT_DIR"

EMBEDDED_URL=false
for arg in "$@"; do
  case "$arg" in
    --embedded-url) EMBEDDED_URL=true ;;
    *) echo "Unknown argument: $arg (expected: --embedded-url)" >&2; exit 1 ;;
  esac
done

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_header() { echo -e "\n${BLUE}═══ $1 ═══${NC}"; }
print_info()   { echo -e "${YELLOW}ℹ $1${NC}"; }
print_ok()     { echo -e "${GREEN}✓ $1${NC}"; }
print_err()    { echo -e "${RED}✗ $1${NC}"; }
print_cmd()    { echo -e "${BLUE}\$ $1${NC}"; }

CONTAINER_NAME="apr-minio-demo"
IMAGE_NAME="apr-minio-put-test:demo"
BUCKET="dog-licenses"
OBJECT_KEY="submissions/dog-license-$(date +%Y%m%dT%H%M%S).aprf"
GUI_OBJECT_KEY="submissions/dog-license-gui-$(date +%Y%m%dT%H%M%S).aprf"
YAML_OBJECT_KEY="submissions/dog-license-yaml-$(date +%Y%m%dT%H%M%S).apr.yaml"
PYTHON_OBJECT_KEY="submissions/dog-license-python-$(date +%Y%m%dT%H%M%S).apr.yaml"
GUI_SCREENSHOT="$SCRIPT_DIR/gui-submission-screenshot.png"
# MinIO has no built-in default credential: MINIO_ROOT_USER (>=3 chars) and
# MINIO_ROOT_PASSWORD (>=8 chars) must be set explicitly for it to start at
# all. These are the shortest memorable pair that satisfies both minimums.
ROOT_USER="root"
ROOT_PASSWORD="password"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

mc() {
  podman run --rm --network=host -v "$SCRIPT_DIR/mc-config:/root/.mc:Z" \
    docker.io/minio/mc:latest --insecure "$@"
}

for tool in podman uv curl diff jq; do
  command -v "$tool" >/dev/null 2>&1 || { print_err "'$tool' is required and not on PATH."; exit 1; }
done

PYTHON_VENV="$REPO_ROOT/python/.venv/bin/python3"
[ -x "$PYTHON_VENV" ] || { print_err "'$PYTHON_VENV' not found -- set up the Python SDK's venv first (see python/README.md)."; exit 1; }

if [ -z "${DOTNET_ROOT:-}" ] && [ -d "$HOME/.dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi
command -v dotnet >/dev/null 2>&1 || { print_err "dotnet is required and not on PATH."; exit 1; }

print_header "1. Start a non-persistent MinIO instance"
podman rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
print_info "Building the MinIO image (cached after the first run)..."
podman build -q -t "$IMAGE_NAME" "$SCRIPT_DIR" >/dev/null
print_info "Starting it with no volume mount -- nothing it holds survives removing this container."
podman run -d --name "$CONTAINER_NAME" \
  -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER="$ROOT_USER" \
  -e MINIO_ROOT_PASSWORD="$ROOT_PASSWORD" \
  "$IMAGE_NAME" >/dev/null

print_info "Waiting for it to become healthy..."
for _ in $(seq 1 30); do
  if curl -sk -o /dev/null -w '%{http_code}' https://localhost:9000/minio/health/live 2>/dev/null | grep -q 200; then
    print_ok "MinIO is up at https://localhost:9000 (console at https://localhost:9001)"
    break
  fi
  sleep 1
done

print_header "2. Create the bucket (anonymous read/list, for this demo only)"
rm -rf "$SCRIPT_DIR/mc-config"
mkdir -p "$SCRIPT_DIR/mc-config"
mc alias set localminio "https://localhost:9000" "$ROOT_USER" "$ROOT_PASSWORD" >/dev/null
mc mb "localminio/$BUCKET" >/dev/null
mc anonymous set download "localminio/$BUCKET" >/dev/null
print_ok "Bucket '$BUCKET' created; anonymous download/list enabled."

print_header "3. Generate a real S3 presigned PUT URL"
PRESIGNED_URL="$(cd "$SCRIPT_DIR" && uv run --with boto3 python3 presign.py "$BUCKET" "$OBJECT_KEY" \
  --access-key "$ROOT_USER" --secret-key "$ROOT_PASSWORD" 2>/dev/null | tail -1)"
print_ok "Presigned URL (expires in 1 hour):"
echo "  $PRESIGNED_URL"

print_header "4. Fill the dog license form"
TEMPLATE="$SCRIPT_DIR/dog-license.aprt"
if [ "$EMBEDDED_URL" = true ]; then
  TEMPLATE="$WORK_DIR/dog-license-with-url.aprt"
  jq --arg url "$PRESIGNED_URL" '.metadata.submissionUrls = [$url]' \
    "$SCRIPT_DIR/dog-license.aprt" > "$TEMPLATE"
  print_info "Wrote the presigned URL into metadata.submissionUrls before filling --"
  print_info "the filled document below already names its own delivery target."
fi
dotnet build "$REPO_ROOT/src/PromptResponse.Cli" -c Release --nologo -v q
FILLED="$WORK_DIR/dog-license.aprf"
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  fill "$TEMPLATE" --non-interactive \
  --set-prompt_dog_name="Rex" \
  --set-prompt_breed="Labrador Retriever" \
  --set-prompt_owner_name="Jane Doe" \
  --set-prompt_owner_phone="+1 (555) 123-4567" \
  --set-prompt_rabies_vaccination_date="$(date +%Y-%m-%d)" \
  --output="$FILLED"
print_ok "Filled form saved to $FILLED"
echo
cat "$FILLED"
echo
if [ "$EMBEDDED_URL" = true ] && ! jq -e '.metadata.submissionUrls == [$url]' --arg url "$PRESIGNED_URL" "$FILLED" >/dev/null 2>&1; then
  print_err "apr fill did not carry metadata.submissionUrls through from the template. That's a bug to file, not something for this script to patch around."
  exit 1
fi

print_header "5. Submit it with the real, unmodified apr CLI"
print_info "Publishing a self-contained linux-arm64 build of the CLI from this checkout..."
dotnet publish "$REPO_ROOT/src/PromptResponse.Cli" -c Release -r linux-arm64 --self-contained true \
  -o "$WORK_DIR/publish" --nologo -v q
# dotnet publish -r <rid> touches the RID-specific section of each project's
# packages.lock.json. Revert that; it's build noise, not a real change.
git -C "$REPO_ROOT" checkout -- \
  src/PromptResponse.Cli/packages.lock.json \
  src/PromptResponse.Core/packages.lock.json \
  src/PromptResponse.Host.Abstractions/packages.lock.json \
  src/PromptResponse.Rendering.Pdf/packages.lock.json 2>/dev/null || true

print_info "Building a throwaway container that trusts only this MinIO instance's certificate..."
podman cp "$CONTAINER_NAME:/root/.minio/certs/public.crt" "$WORK_DIR/minio.crt"
cat > "$WORK_DIR/Containerfile" <<'EOF'
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY minio.crt /usr/local/share/ca-certificates/minio-demo.crt
RUN update-ca-certificates
COPY publish/ /app/
WORKDIR /app
ENTRYPOINT ["./apr"]
EOF
podman build -q -t apr-cli-verify-minio:demo "$WORK_DIR" >/dev/null
print_info "Nothing on your Mac's own trust store is touched -- only this throwaway container's."

apr_submit() {
  podman run --rm --network=host \
    -v "$FILLED:/data/dog-license.aprf:Z" \
    apr-cli-verify-minio:demo submit /data/dog-license.aprf "$@"
}

echo
if [ "$EMBEDDED_URL" = true ]; then
  print_cmd "apr submit dog-license.aprf --yes   # no --url: reads metadata.submissionUrls"
  echo
  SUBMIT_OUTPUT="$(apr_submit --yes)"
  echo "$SUBMIT_OUTPUT"
  if echo "$SUBMIT_OUTPUT" | grep -q "delivered to"; then
    print_ok "Submitted using only the URL the document names itself."
  else
    print_err "Submission did not report success -- see output above."
    exit 1
  fi

  echo
  print_info "And a --url that doesn't match what the document names is refused:"
  print_cmd 'apr submit dog-license.aprf --url="https://example.invalid/wrong" --yes'
  REJECT_OUTPUT="$(apr_submit --url="https://example.invalid/wrong" --yes 2>&1 || true)"
  echo "$REJECT_OUTPUT"
  if echo "$REJECT_OUTPUT" | grep -q "must be one of the targets the document names"; then
    print_ok "Correctly refused: the document's own submissionUrls is authoritative."
  else
    print_err "Expected a refusal citing the document's declared targets -- see output above."
    exit 1
  fi
else
  print_cmd "apr submit dog-license.aprf --url=\"$PRESIGNED_URL\" --yes"
  echo
  SUBMIT_OUTPUT="$(apr_submit --url="$PRESIGNED_URL" --yes)"
  echo "$SUBMIT_OUTPUT"
  if echo "$SUBMIT_OUTPUT" | grep -q "delivered to"; then
    print_ok "Submitted."
  else
    print_err "Submission did not report success -- see output above."
    exit 1
  fi
fi

print_header "6. Submit a second copy with the real desktop GUI, driven headlessly"
GUI_PRESIGNED_URL="$(cd "$SCRIPT_DIR" && uv run --with boto3 python3 presign.py "$BUCKET" "$GUI_OBJECT_KEY" \
  --access-key "$ROOT_USER" --secret-key "$ROOT_PASSWORD" 2>/dev/null | tail -1)"
print_info "A second presigned URL, for the GUI's own object:"
echo "  $GUI_PRESIGNED_URL"
dotnet build "$REPO_ROOT/tools/PromptResponse.GuiSubmitDemo.Avalonia" -c Release --nologo -v q
# A plain (non-RID-specific) build shouldn't touch packages.lock.json, but revert
# defensively anyway -- see the comment on the same pattern in step 5.
git -C "$REPO_ROOT" checkout -- \
  src/PromptResponse.Core/packages.lock.json \
  src/PromptResponse.Desktop/packages.lock.json \
  src/PromptResponse.Rendering.Pdf/packages.lock.json 2>/dev/null || true
GUI_CAPTURED_BODY="$WORK_DIR/gui-captured-body.aprf"
GUI_LOCAL_SAVE="$WORK_DIR/gui-local-save.aprf"
dotnet run --project "$REPO_ROOT/tools/PromptResponse.GuiSubmitDemo.Avalonia" -c Release --no-build -- \
  "$SCRIPT_DIR/dog-license.aprt" "$GUI_PRESIGNED_URL" "$WORK_DIR/minio.crt" \
  "$GUI_SCREENSHOT" "$GUI_CAPTURED_BODY" "$GUI_LOCAL_SAVE"
print_ok "GUI submission complete -- screenshot of the filled form: $GUI_SCREENSHOT"

print_header "7. Submit the CLI's same answers again, as APR-YAML this time"
print_info "Same template, same --set-prompt_* values as step 5 -- only the"
print_info "representation differs, so step 9's apr diff has something to prove."
YAML_PRESIGNED_URL="$(cd "$SCRIPT_DIR" && uv run --with boto3 python3 presign.py "$BUCKET" "$YAML_OBJECT_KEY" \
  --content-type "application/vnd.apr+yaml" \
  --access-key "$ROOT_USER" --secret-key "$ROOT_PASSWORD" 2>/dev/null | tail -1)"
print_ok "A third presigned URL, its signature bound to application/vnd.apr+yaml:"
echo "  $YAML_PRESIGNED_URL"
YAML_FILLED="$WORK_DIR/dog-license.apr.yaml"
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  fill "$TEMPLATE" --non-interactive \
  --set-prompt_dog_name="Rex" \
  --set-prompt_breed="Labrador Retriever" \
  --set-prompt_owner_name="Jane Doe" \
  --set-prompt_owner_phone="+1 (555) 123-4567" \
  --set-prompt_rabies_vaccination_date="$(date +%Y-%m-%d)" \
  --output="$YAML_FILLED"
print_ok "Filled form saved to $YAML_FILLED"
echo
cat "$YAML_FILLED"
echo
print_cmd "apr submit dog-license.apr.yaml --url=\"\$YAML_PRESIGNED_URL\" --yes"
echo
YAML_SUBMIT_OUTPUT="$(podman run --rm --network=host \
  -v "$YAML_FILLED:/data/dog-license.apr.yaml:Z" \
  apr-cli-verify-minio:demo submit /data/dog-license.apr.yaml --url="$YAML_PRESIGNED_URL" --yes)"
echo "$YAML_SUBMIT_OUTPUT"
if echo "$YAML_SUBMIT_OUTPUT" | grep -q "delivered to"; then
  print_ok "Submitted as APR-YAML."
else
  print_err "YAML submission did not report success -- see output above."
  exit 1
fi

print_header "8. Submit a third client's own answers: the real Python SDK, writing APR-YAML"
print_info "python/.venv, not a container -- the same interpreter the conformance"
print_info "driver uses. Exercises the YAML writer landed for issue #402."
PYTHON_PRESIGNED_URL="$(cd "$SCRIPT_DIR" && uv run --with boto3 python3 presign.py "$BUCKET" "$PYTHON_OBJECT_KEY" \
  --content-type "application/vnd.apr+yaml" \
  --access-key "$ROOT_USER" --secret-key "$ROOT_PASSWORD" 2>/dev/null | tail -1)"
print_ok "A fourth presigned URL, also bound to application/vnd.apr+yaml:"
echo "  $PYTHON_PRESIGNED_URL"
PYTHON_FILLED="$WORK_DIR/dog-license-python.apr.yaml"
"$PYTHON_VENV" "$SCRIPT_DIR/python_submit.py" "$TEMPLATE" "$PYTHON_FILLED" "$PYTHON_PRESIGNED_URL" "$WORK_DIR/minio.crt"
echo
cat "$PYTHON_FILLED"
echo
print_ok "Python client submitted its own filled form as APR-YAML."

print_header "9. Download all four objects back from MinIO and verify each"
print_info "Byte level first (diff), then form level (apr diff -- see below for what"
print_info "that checks that a byte diff can't: it parses both sides and compares"
print_info "documentType, section/prompt structure, and every response by prompt id,"
print_info "so it's the same check whether both files are JSON, both are APR-YAML, or"
print_info "one is each -- steps 7 and 8 below are the \"one is each\" case, for real.)"
echo

DOWNLOADED="$WORK_DIR/downloaded.aprf"
mc cat "localminio/$BUCKET/$OBJECT_KEY" > "$DOWNLOADED"
if diff -q "$FILLED" "$DOWNLOADED" >/dev/null; then
  print_ok "CLI: downloaded object is byte-for-byte identical to the local file it submitted."
else
  print_err "CLI MISMATCH -- the downloaded file differs from what was submitted:"
  diff "$FILLED" "$DOWNLOADED" || true
  exit 1
fi
print_cmd "apr diff dog-license.aprf downloaded.aprf   # local file vs. what MinIO holds"
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  diff "$FILLED" "$DOWNLOADED"

GUI_DOWNLOADED="$WORK_DIR/gui-downloaded.aprf"
mc cat "localminio/$BUCKET/$GUI_OBJECT_KEY" > "$GUI_DOWNLOADED"
# Three-way: the file the GUI's own Save action wrote, the exact bytes its HTTP client
# put on the wire, and what MinIO ends up holding -- nothing in between re-serializes
# the document, so all three are expected to be byte-identical, not just equivalent.
if diff -q "$GUI_LOCAL_SAVE" "$GUI_CAPTURED_BODY" >/dev/null && diff -q "$GUI_CAPTURED_BODY" "$GUI_DOWNLOADED" >/dev/null; then
  print_ok "GUI: local save, captured wire bytes, and the downloaded object are all byte-for-byte identical."
else
  print_err "GUI MISMATCH -- local save, wire capture, and download don't all agree:"
  diff "$GUI_LOCAL_SAVE" "$GUI_CAPTURED_BODY" || true
  diff "$GUI_CAPTURED_BODY" "$GUI_DOWNLOADED" || true
  exit 1
fi
print_cmd "apr diff gui-local-save.aprf gui-downloaded.aprf   # local save vs. what MinIO holds"
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  diff "$GUI_LOCAL_SAVE" "$GUI_DOWNLOADED"

GUI_VALIDATE="$(podman run --rm --network=host \
  -v "$GUI_DOWNLOADED:/data/gui-downloaded.aprf:Z" \
  apr-cli-verify-minio:demo validate /data/gui-downloaded.aprf)"
echo "$GUI_VALIDATE"
if echo "$GUI_VALIDATE" | grep -q '"valid": true'; then
  print_ok "GUI submission validates cleanly with the real CLI."
else
  print_err "GUI submission failed validation -- see output above."
  exit 1
fi

YAML_DOWNLOADED="$WORK_DIR/downloaded.apr.yaml"
mc cat "localminio/$BUCKET/$YAML_OBJECT_KEY" > "$YAML_DOWNLOADED"
if diff -q "$YAML_FILLED" "$YAML_DOWNLOADED" >/dev/null; then
  print_ok "CLI/YAML: downloaded object is byte-for-byte identical to the local file it submitted."
else
  print_err "CLI/YAML MISMATCH -- the downloaded file differs from what was submitted:"
  diff "$YAML_FILLED" "$YAML_DOWNLOADED" || true
  exit 1
fi
YAML_CONTENT_TYPE="$(curl -sk -D - -o /dev/null "https://localhost:9000/$BUCKET/$YAML_OBJECT_KEY" | tr -d '\r' | grep -i '^Content-Type:')"
if echo "$YAML_CONTENT_TYPE" | grep -qi "application/vnd.apr+yaml"; then
  print_ok "MinIO stored it with $YAML_CONTENT_TYPE -- the presigned URL's bound content type actually made it onto the wire."
else
  print_err "Expected application/vnd.apr+yaml on the stored object, got: $YAML_CONTENT_TYPE"
  exit 1
fi
print_cmd "apr diff downloaded.aprf downloaded.apr.yaml   # same CLI answers, JSON vs. YAML"
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  diff "$DOWNLOADED" "$YAML_DOWNLOADED"
print_ok "Identical answers, two representations -- apr diff agrees they're the same form."

PYTHON_DOWNLOADED="$WORK_DIR/downloaded-python.apr.yaml"
mc cat "localminio/$BUCKET/$PYTHON_OBJECT_KEY" > "$PYTHON_DOWNLOADED"
if diff -q "$PYTHON_FILLED" "$PYTHON_DOWNLOADED" >/dev/null; then
  print_ok "Python: downloaded object is byte-for-byte identical to the local file it submitted."
else
  print_err "Python MISMATCH -- the downloaded file differs from what was submitted:"
  diff "$PYTHON_FILLED" "$PYTHON_DOWNLOADED" || true
  exit 1
fi
PYTHON_VALIDATE="$(podman run --rm --network=host \
  -v "$PYTHON_DOWNLOADED:/data/downloaded-python.apr.yaml:Z" \
  apr-cli-verify-minio:demo validate /data/downloaded-python.apr.yaml)"
echo "$PYTHON_VALIDATE"
if echo "$PYTHON_VALIDATE" | grep -q '"valid": true'; then
  print_ok "Python's submission validates cleanly with the real CLI."
else
  print_err "Python's submission failed validation -- see output above."
  exit 1
fi

print_header "10. Directory listing of everything PUT into the bucket"
BUCKET_URL="https://localhost:9000/$BUCKET/"
CONSOLE_URL="https://localhost:9001/browser/$BUCKET"
print_info "Raw S3 ListObjects response for '$BUCKET':"
echo
curl -sk "$BUCKET_URL" | sed 's/></>\n</g'
echo
print_info "To browse it yourself: $CONSOLE_URL (login: $ROOT_USER / $ROOT_PASSWORD)"

print_header "Done"
echo "MinIO is still running (non-persistent -- no data survives removing it)."
echo "The GUI's filled-form screenshot is at: $GUI_SCREENSHOT"
echo "When you're done looking around:"
echo
print_cmd "podman rm -f $CONTAINER_NAME"
print_cmd "rm -f \"$GUI_SCREENSHOT\""
echo

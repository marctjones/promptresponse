#!/usr/bin/env bash
#
# End-to-end demo: a real MinIO instance, a real presigned PUT URL, the real
# unmodified `apr` CLI *and* the real Avalonia desktop GUI each filling and
# submitting their own "dog license" form, verification that what MinIO holds
# matches what each client actually sent, and a Chrome window pointed at the
# bucket so you can see both submitted files listed there yourself.
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
#      the GUI actually rendered.
#   7. Downloads both objects back from MinIO: the CLI's is diffed
#      byte-for-byte against the file it submitted; the GUI's is diffed
#      against the exact bytes its own HTTP client sent (captured before the
#      request left the process, since the GUI never writes its completed
#      copy to disk) and validated with the real CLI.
#   8. Opens a fresh, disposable Chrome window on the MinIO Console's file
#      browser for the bucket, so you can see both files listed yourself --
#      launched with a throwaway profile and --ignore-certificate-errors so
#      it doesn't stop at a certificate warning first. Nothing on your main
#      Chrome profile or your Mac's own trust store is touched.
#
# Leaves the MinIO container (and that Chrome window) running so you can
# look around. Cleanup instructions print at the end.

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
GUI_SCREENSHOT="$SCRIPT_DIR/gui-submission-screenshot.png"
# MinIO has no built-in default credential: MINIO_ROOT_USER (>=3 chars) and
# MINIO_ROOT_PASSWORD (>=8 chars) must be set explicitly for it to start at
# all. These are the shortest memorable pair that satisfies both minimums.
ROOT_USER="root"
ROOT_PASSWORD="password"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

# The Chrome profile is deliberately outside WORK_DIR: WORK_DIR is deleted at
# script exit, but Chrome itself keeps running afterward (see step 7), so its
# profile has to survive that cleanup. Wiped at the *start* of each run instead.
CHROME_PROFILE="$SCRIPT_DIR/.chrome-profile"
rm -rf "$CHROME_PROFILE"

mc() {
  podman run --rm --network=host -v "$SCRIPT_DIR/mc-config:/root/.mc:Z" \
    docker.io/minio/mc:latest --insecure "$@"
}

for tool in podman uv curl diff jq; do
  command -v "$tool" >/dev/null 2>&1 || { print_err "'$tool' is required and not on PATH."; exit 1; }
done

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
dotnet run --project "$REPO_ROOT/tools/PromptResponse.GuiSubmitDemo.Avalonia" -c Release --no-build -- \
  "$SCRIPT_DIR/dog-license.aprt" "$GUI_PRESIGNED_URL" "$WORK_DIR/minio.crt" \
  "$GUI_SCREENSHOT" "$GUI_CAPTURED_BODY"
print_ok "GUI submission complete -- screenshot of the filled form: $GUI_SCREENSHOT"

print_header "7. Download both objects back from MinIO and verify each"
DOWNLOADED="$WORK_DIR/downloaded.aprf"
mc cat "localminio/$BUCKET/$OBJECT_KEY" > "$DOWNLOADED"
if diff -q "$FILLED" "$DOWNLOADED" >/dev/null; then
  print_ok "CLI: byte-for-byte identical to what was submitted."
else
  print_err "CLI MISMATCH -- the downloaded file differs from what was submitted:"
  diff "$FILLED" "$DOWNLOADED" || true
  exit 1
fi

GUI_DOWNLOADED="$WORK_DIR/gui-downloaded.aprf"
mc cat "localminio/$BUCKET/$GUI_OBJECT_KEY" > "$GUI_DOWNLOADED"
if diff -q "$GUI_CAPTURED_BODY" "$GUI_DOWNLOADED" >/dev/null; then
  print_ok "GUI: byte-for-byte identical to what its HTTP client actually sent."
else
  print_err "GUI MISMATCH -- the downloaded file differs from what was captured on the wire:"
  diff "$GUI_CAPTURED_BODY" "$GUI_DOWNLOADED" || true
  exit 1
fi
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

print_header "8. Open a directory listing of everything PUT into the bucket"
BUCKET_URL="https://localhost:9000/$BUCKET/"
CONSOLE_URL="https://localhost:9001/browser/$BUCKET"
print_info "Raw S3 ListObjects response for '$BUCKET' (this is the actual, unfiltered"
print_info "directory listing -- printed here so it's visible even if the browser"
print_info "step below doesn't apply to your setup):"
echo
curl -sk "$BUCKET_URL" | sed 's/></>\n</g'
echo
print_info "Opening the same listing as a file browser in a fresh, disposable Chrome"
print_info "window (its own throwaway profile -- your main Chrome profile, and your"
print_info "Mac's own certificate trust store, are both left untouched). It's launched"
print_info "with --ignore-certificate-errors so it lands on the MinIO Console's login"
print_info "screen directly, with no certificate warning first."
print_info "Console login: $ROOT_USER / $ROOT_PASSWORD"
if [ "$(uname)" = "Darwin" ] && [ -x "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" ]; then
  mkdir -p "$CHROME_PROFILE"
  nohup "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" \
    --user-data-dir="$CHROME_PROFILE" \
    --no-first-run --no-default-browser-check \
    --ignore-certificate-errors \
    "$CONSOLE_URL" >/dev/null 2>&1 &
  disown
  print_ok "Chrome opened on $CONSOLE_URL"
elif command -v xdg-open >/dev/null 2>&1; then
  print_info "No Chrome-flag workaround applied on this platform -- your browser may"
  print_info "still show its own certificate warning; click through it once."
  xdg-open "$CONSOLE_URL"
else
  print_info "Could not auto-launch a browser. Open this yourself: $CONSOLE_URL"
fi

print_header "Done"
echo "MinIO is still running (non-persistent -- no data survives removing it)."
echo "The GUI's filled-form screenshot is at: $GUI_SCREENSHOT"
echo "When you're done looking around:"
echo
print_cmd "podman rm -f $CONTAINER_NAME"
print_cmd "rm -rf \"$CHROME_PROFILE\" \"$GUI_SCREENSHOT\""
echo

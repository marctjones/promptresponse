#!/usr/bin/env bash
#
# End-to-end demo: a real Cloudflare R2 bucket, a real S3 pre-signed PUT URL,
# and the real, unmodified `apr` CLI filling and submitting a "dog license"
# form -- proving specification 5.2.1's (APR-MODEL-033) claim that submission
# is an ordinary S3-compatible pre-signed PUT, against an actual hosted
# S3-compatible service rather than a local MinIO container
# (see ../minio-put/demo.sh, which proves the same thing locally, plus the
# desktop GUI and Python SDK as second and third clients -- add those legs
# here the same way once this simpler R2 path is verified against a real
# account).
#
# Unlike demos/minio-put/demo.sh, there is no container to build, no
# self-signed certificate to trust, and no anonymous bucket read to enable:
# R2 has a publicly trusted TLS certificate, and this script downloads the
# object back with the same real R2 credentials it used to presign the PUT,
# rather than emulating a client that has to work around not holding any.
#
# Also proves the fix for a real concern raised while building this: many
# fillers could be handed a submission link, and a bare presigned PUT has no
# way to stop one from overwriting another's already-submitted object at the
# same key. Step 5 shows the actual R2/S3 mechanism for that -- binding
# `If-None-Match: *` into the presigned URL's own signature, so a second PUT
# to the same key gets 412, not a silent overwrite, and the header can't be
# left off since it's part of what's signed.
#
# Requires (see README.md for how to get each):
#   R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET
#
# Nothing here is deleted automatically: the two objects this writes stay in
# your R2 bucket (R2's own lifecycle rules are day-granularity, not something
# a demo run should wait on) until you remove them yourself -- printed at the
# end.

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

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

for var in R2_ACCOUNT_ID R2_ACCESS_KEY_ID R2_SECRET_ACCESS_KEY R2_BUCKET; do
  [ -n "${!var:-}" ] || { print_err "\$$var is not set -- see README.md."; exit 1; }
done

for tool in uv curl diff jq dotnet; do
  command -v "$tool" >/dev/null 2>&1 || { print_err "'$tool' is required and not on PATH."; exit 1; }
done
if [ -z "${DOTNET_ROOT:-}" ] && [ -d "$HOME/.dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
OBJECT_KEY="submissions/dog-license-$(date +%Y%m%dT%H%M%S).aprf"
ENDPOINT="https://${R2_ACCOUNT_ID}.r2.cloudflarestorage.com"

print_header "1. Generate a real S3 pre-signed PUT URL against R2"
PRESIGNED_URL="$(cd "$SCRIPT_DIR" && uv run --with boto3 python3 presign.py "$R2_BUCKET" "$OBJECT_KEY" \
  --account-id "$R2_ACCOUNT_ID" --access-key "$R2_ACCESS_KEY_ID" --secret-key "$R2_SECRET_ACCESS_KEY" 2>/dev/null | tail -1)"
print_ok "Presigned URL (expires in 1 hour), signed for application/vnd.apr+json:"
echo "  $PRESIGNED_URL"

print_header "2. Fill the dog license form"
FILLED="$WORK_DIR/dog-license.aprf"
dotnet build "$REPO_ROOT/src/PromptResponse.Cli" -c Release --nologo -v q
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  fill "$SCRIPT_DIR/dog-license.aprt" --non-interactive \
  --set-prompt_dog_name="Rex" \
  --set-prompt_breed="Labrador Retriever" \
  --set-prompt_owner_name="Jane Doe" \
  --set-prompt_owner_phone="+1 (555) 123-4567" \
  --set-prompt_rabies_vaccination_date="$(date +%Y-%m-%d)" \
  --output="$FILLED"
print_ok "Filled form saved to $FILLED"

print_header "3. Submit it with the real, unmodified apr CLI"
print_info "No container, no certificate trust workaround -- R2's certificate is"
print_info "already trusted by your system, same as any real HTTPS endpoint."
print_cmd "apr submit dog-license.aprf --url=\"\$PRESIGNED_URL\" --yes"
echo
SUBMIT_OUTPUT="$(dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  submit "$FILLED" --url="$PRESIGNED_URL" --yes)"
echo "$SUBMIT_OUTPUT"
if echo "$SUBMIT_OUTPUT" | grep -q "delivered to"; then
  print_ok "Submitted to a real Cloudflare R2 bucket."
else
  print_err "Submission did not report success -- see output above."
  exit 1
fi

print_header "4. Download it back from R2 and verify"
print_info "Using the same held R2 credentials directly (boto3 get_object) rather"
print_info "than a second presigned URL -- this script already has real access."
DOWNLOADED="$WORK_DIR/downloaded.aprf"
uv run --with boto3 python3 - "$R2_BUCKET" "$OBJECT_KEY" "$DOWNLOADED" <<'EOF'
import sys, boto3, os
bucket, key, out = sys.argv[1:4]
client = boto3.client("s3", endpoint_url=f"https://{os.environ['R2_ACCOUNT_ID']}.r2.cloudflarestorage.com",
                       aws_access_key_id=os.environ["R2_ACCESS_KEY_ID"], aws_secret_access_key=os.environ["R2_SECRET_ACCESS_KEY"],
                       region_name="auto")
client.download_file(bucket, key, out)
EOF

if diff -q "$FILLED" "$DOWNLOADED" >/dev/null; then
  print_ok "Downloaded object is byte-for-byte identical to the local file it submitted."
else
  print_err "MISMATCH -- the downloaded file differs from what was submitted:"
  diff "$FILLED" "$DOWNLOADED" || true
  exit 1
fi
print_cmd "apr diff dog-license.aprf downloaded.aprf   # local file vs. what R2 holds"
dotnet run --project "$REPO_ROOT/src/PromptResponse.Cli" -c Release --no-build -- \
  diff "$FILLED" "$DOWNLOADED"

print_header "5. Prove If-None-Match: * stops a second submission from overwriting the first"
print_info "This is not routed through the real apr CLI -- SubmitCommand doesn't send"
print_info "this header today (that's a separate, open question: see the linked issue"
print_info "in README.md on whether APR's https submission clients should). This step"
print_info "proves the underlying R2/S3 mechanism itself, with curl, independent of"
print_info "whether any client has adopted it yet."
IFNM_KEY="submissions/dog-license-ifnm-$(date +%Y%m%dT%H%M%S).aprf"
IFNM_URL="$(cd "$SCRIPT_DIR" && uv run --with boto3 python3 presign.py "$R2_BUCKET" "$IFNM_KEY" --if-none-match \
  --account-id "$R2_ACCOUNT_ID" --access-key "$R2_ACCESS_KEY_ID" --secret-key "$R2_SECRET_ACCESS_KEY" 2>/dev/null | tail -1)"
print_ok "Presigned URL with If-None-Match: * bound into its own signature:"
echo "  $IFNM_URL"

echo
print_cmd "curl -X PUT --data-binary @dog-license.aprf -H 'Content-Type: application/vnd.apr+json' -H 'If-None-Match: *' \"\$IFNM_URL\""
FIRST_STATUS="$(curl -s -o /dev/null -w '%{http_code}' -X PUT --data-binary @"$FILLED" \
  -H "Content-Type: application/vnd.apr+json" -H "If-None-Match: *" "$IFNM_URL")"
if [ "$FIRST_STATUS" = "200" ]; then
  print_ok "First PUT: HTTP $FIRST_STATUS -- accepted, nothing existed at this key yet."
else
  print_err "First PUT expected 200, got HTTP $FIRST_STATUS."
  exit 1
fi

echo
print_info "Same URL, same header, second attempt -- an object now exists at this key:"
SECOND_STATUS="$(curl -s -o /dev/null -w '%{http_code}' -X PUT --data-binary @"$FILLED" \
  -H "Content-Type: application/vnd.apr+json" -H "If-None-Match: *" "$IFNM_URL")"
if [ "$SECOND_STATUS" = "412" ]; then
  print_ok "Second PUT: HTTP $SECOND_STATUS Precondition Failed -- rejected, not silently overwritten."
else
  print_err "Second PUT expected 412, got HTTP $SECOND_STATUS."
  exit 1
fi

echo
print_info "Same URL, header omitted -- it's bound into the signature, not optional:"
THIRD_STATUS="$(curl -s -o /dev/null -w '%{http_code}' -X PUT --data-binary @"$FILLED" \
  -H "Content-Type: application/vnd.apr+json" "$IFNM_URL")"
if [ "$THIRD_STATUS" = "403" ]; then
  print_ok "PUT without the header: HTTP $THIRD_STATUS -- signature doesn't validate without it."
else
  print_err "PUT without the header expected 403 (signature mismatch), got HTTP $THIRD_STATUS."
  exit 1
fi

print_header "Done"
echo "Two objects left in your bucket (R2 has no sub-day lifecycle rule to expire"
echo "them automatically -- see README.md if you want a scheduled cleanup):"
echo "  s3://$R2_BUCKET/$OBJECT_KEY"
echo "  s3://$R2_BUCKET/$IFNM_KEY"
echo
echo "To remove them yourself:"
print_cmd "uv run --with boto3 python3 -c \"import boto3,os; c=boto3.client('s3', endpoint_url='$ENDPOINT', aws_access_key_id=os.environ['R2_ACCESS_KEY_ID'], aws_secret_access_key=os.environ['R2_SECRET_ACCESS_KEY'], region_name='auto'); [c.delete_object(Bucket='$R2_BUCKET', Key=k) for k in ('$OBJECT_KEY', '$IFNM_KEY')]\""

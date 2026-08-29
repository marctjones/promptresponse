#!/usr/bin/env bash
#
# Smoke-test a release-style PromptResponse build on the current machine.
#
# By default this publishes a self-contained artifact for the host runtime into
# dist-smoke/, then exercises the shipped CLI binary from the staged folder. It
# intentionally avoids the developer build output so it catches packaging and
# publish mistakes.
#
# Usage:
#   scripts/release-smoke.sh [--rid <rid>] [--version <v>] [--stage <dir>] [--skip-publish]
#
set -euo pipefail

cd "$(dirname "$0")/.."

rid_from_host() {
  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"
  case "$os" in
    Darwin)
      case "$arch" in
        arm64|aarch64) echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
        *) echo "Unsupported macOS architecture: $arch" >&2; return 2 ;;
      esac
      ;;
    Linux)
      case "$arch" in
        x86_64) echo "linux-x64" ;;
        arm64|aarch64) echo "linux-arm64" ;;
        *) echo "Unsupported Linux architecture: $arch" >&2; return 2 ;;
      esac
      ;;
    MINGW*|MSYS*|CYGWIN*)
      echo "win-x64"
      ;;
    *)
      echo "Unsupported OS: $os" >&2
      return 2
      ;;
  esac
}

RID="$(rid_from_host)"
VERSION="0.6.0-smoke"
OUTPUT="dist-smoke"
SKIP_PUBLISH=0
STAGE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid) RID="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --output) OUTPUT="$2"; shift 2 ;;
    --stage) STAGE="$2"; SKIP_PUBLISH=1; shift 2 ;;
    --skip-publish) SKIP_PUBLISH=1; shift ;;
    -h|--help) grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$STAGE" ]]; then
  STAGE="$OUTPUT/promptresponse-${VERSION}-${RID}"
fi

if [[ "$SKIP_PUBLISH" -eq 0 ]]; then
  scripts/publish.sh --rid "$RID" --version "$VERSION" --output "$OUTPUT"
fi

EXE=""
[[ "$RID" == win-* ]] && EXE=".exe"
APR="$STAGE/apr$EXE"
APP="$STAGE/promptresponse$EXE"

[[ -x "$APR" ]] || { echo "Missing executable CLI: $APR" >&2; exit 1; }
[[ -x "$APP" ]] || { echo "Missing executable desktop app: $APP" >&2; exit 1; }
[[ -f "$STAGE/LICENSE" ]] || { echo "Missing bundled LICENSE" >&2; exit 1; }
[[ -f "$STAGE/README.md" ]] || { echo "Missing bundled README.md" >&2; exit 1; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

TEMPLATE="examples/contact-intake.aprt"
SIGNED="$WORK/contact-intake.signed.aprt"
KEY="$WORK/smoke.pfx"
CERT="$WORK/smoke.cer"
PDF="$WORK/contact-intake.pdf"
PDFA="$WORK/contact-intake.pdfa.pdf"
FILLABLE_PDF="$WORK/contact-intake-fillable.pdf"
HTML="$WORK/contact-intake.html"
FILLABLE_HTML="$WORK/contact-intake-fillable.html"
JSON="$WORK/contact-intake.json"
TXT="$WORK/contact-intake.txt"
IMPORTED="$WORK/imported.aprt"

echo "▶ Smoke-testing staged artifact: $STAGE"

# The GUI itself needs a desktop session, but --help is handled before Avalonia
# starts. This proves the staged desktop executable is runnable and retained its
# documented form-filling command-line contract without opening a window in CI.
"$APP" --help | grep -q -- '--open <file>'

"$APR" validate "$TEMPLATE" >/dev/null
"$APR" info "$TEMPLATE" >/dev/null
"$APR" stats "$TEMPLATE" >/dev/null

"$APR" export "$TEMPLATE" --format=json --output="$JSON" >/dev/null
"$APR" export "$TEMPLATE" --format=txt --output="$TXT" >/dev/null
"$APR" export "$TEMPLATE" --format=html --output="$HTML" >/dev/null
"$APR" export "$TEMPLATE" --format=html --fillable --output="$FILLABLE_HTML" >/dev/null
"$APR" export "$TEMPLATE" --format=pdf --output="$PDF" >/dev/null
"$APR" export "$TEMPLATE" --format=pdf --pdfa --output="$PDFA" >/dev/null
"$APR" export "$TEMPLATE" --format=pdf --fillable --output="$FILLABLE_PDF" >/dev/null

[[ -s "$JSON" ]] || { echo "JSON export is empty" >&2; exit 1; }
[[ -s "$TXT" ]] || { echo "TXT export is empty" >&2; exit 1; }
[[ -s "$HTML" ]] || { echo "HTML export is empty" >&2; exit 1; }
[[ -s "$FILLABLE_HTML" ]] || { echo "Fillable HTML export is empty" >&2; exit 1; }
[[ -s "$PDF" ]] || { echo "PDF export is empty" >&2; exit 1; }
[[ -s "$PDFA" ]] || { echo "PDF/A export is empty" >&2; exit 1; }
[[ -s "$FILLABLE_PDF" ]] || { echo "Fillable PDF export is empty" >&2; exit 1; }

grep -q "Contact Intake" "$JSON"
grep -q "Contact Intake" "$TXT"
grep -q "Contact Intake" "$HTML"
grep -q "Download filled form" "$FILLABLE_HTML"

"$APR" import "$FILLABLE_PDF" --output="$IMPORTED" --title="Imported Contact Intake" >/dev/null
"$APR" validate "$IMPORTED" >/dev/null

"$APR" keygen --name="PromptResponse Smoke Test" --output="$KEY" --cert-out="$CERT" --password=smoke >/dev/null
"$APR" sign "$TEMPLATE" --publisher --cert="$KEY" --password=smoke --url=https://example.invalid/submit --output="$SIGNED" >/dev/null
"$APR" verify "$SIGNED" --trust="$CERT" >/dev/null

echo "✓ Release artifact smoke test passed for $RID"

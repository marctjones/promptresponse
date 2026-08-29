#!/usr/bin/env bash
# Package a signed, testable macOS .app. Release automation supplies signing and notarization.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="osx-arm64"; VERSION="1.0.0-beta.1"; OUTPUT="$ROOT/dist/PromptResponse.app"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid) RID="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --output) OUTPUT="$2"; shift 2 ;;
    -h|--help) echo "Usage: $0 [--rid osx-arm64|osx-x64] [--version VERSION] [--output PATH/PromptResponse.app]"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ "$(uname -s)" == "Darwin" ]] || { echo "macOS is required" >&2; exit 2; }
[[ ! -e "$OUTPUT" ]] || { echo "Refusing to overwrite existing bundle: $OUTPUT" >&2; exit 2; }
mkdir -p "$(dirname "$OUTPUT")" "$OUTPUT/Contents/MacOS" "$OUTPUT/Contents/Resources"
PUBLISH="$(mktemp -d)"; trap 'rm -rf "$PUBLISH"' EXIT
dotnet publish "$ROOT/src/PromptResponse.Desktop/PromptResponse.Desktop.csproj" -c Release -r "$RID" --self-contained true -o "$PUBLISH" /p:Version="$VERSION"
ditto "$PUBLISH" "$OUTPUT/Contents/MacOS"
cp "$ROOT/packaging/macos/Info.plist" "$OUTPUT/Contents/Info.plist"
plutil -replace CFBundleShortVersionString -string "$VERSION" "$OUTPUT/Contents/Info.plist"
plutil -replace CFBundleVersion -string "$VERSION" "$OUTPUT/Contents/Info.plist"
if [[ -n "${CODESIGN_IDENTITY:-}" ]]; then
  codesign --force --deep --options runtime --entitlements "$ROOT/packaging/macos/PromptResponse.entitlements" --sign "$CODESIGN_IDENTITY" "$OUTPUT"
else
  echo "CODESIGN_IDENTITY is not set; creating an ad-hoc bundle for local AX testing." >&2
  codesign --force --deep --sign - "$OUTPUT"
fi
codesign --verify --deep --strict --verbose=2 "$OUTPUT"
echo "Created $OUTPUT"

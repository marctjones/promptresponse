#!/usr/bin/env bash
# Launch a packaged app and retain live NSAccessibility-tree release evidence.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="${1:?Usage: scripts/verify-macos-accessibility.sh /path/PromptResponse.app [evidence-dir]}"
EVIDENCE="${2:-$ROOT/artifacts/macos-accessibility/$(date +%Y%m%d-%H%M%S)}"
[[ "$(uname -s)" == "Darwin" ]] || { echo "macOS is required" >&2; exit 2; }
EXECUTABLE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$APP/Contents/Info.plist")"
[[ -d "$APP" && -x "$APP/Contents/MacOS/$EXECUTABLE" ]] || { echo "Not a runnable app bundle: $APP" >&2; exit 2; }
mkdir -p "$EVIDENCE"
sw_vers > "$EVIDENCE/macos-version.txt"
codesign -dvv "$APP" > "$EVIDENCE/codesign.txt" 2>&1 || true
open -n "$APP" --args --open "$ROOT/examples/sf-86-background-check.aprt"
OUTPUT="$EVIDENCE/nsaccessibility-tree.json"
for _ in {1..15}; do
  /usr/bin/osascript -l JavaScript "$ROOT/scripts/macos-ax-capture.js" "$EXECUTABLE" "$OUTPUT" &
  CAPTURE_PID=$!
  for _ in {1..3}; do
    if ! kill -0 "$CAPTURE_PID" 2>/dev/null; then break; fi
    sleep 1
  done
  if kill -0 "$CAPTURE_PID" 2>/dev/null; then
    kill "$CAPTURE_PID" 2>/dev/null || true
  fi
  if wait "$CAPTURE_PID" 2>/dev/null && [[ -s "$OUTPUT" ]]; then
    echo "✓ Live AX evidence captured in $EVIDENCE"
    echo "Now complete and sign off $ROOT/docs/release/MACOS_ACCESSIBILITY.md"
    exit 0
  fi
  sleep 1
done
echo "Could not capture the live accessibility tree. Grant Terminal (or the CI runner) Accessibility permission in System Settings → Privacy & Security → Accessibility, then retry." >&2
exit 1

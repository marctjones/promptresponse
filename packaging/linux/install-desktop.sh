#!/usr/bin/env bash
#
# Per-user desktop integration for PromptResponse on Linux.
#
# Run this from inside an extracted release tarball (the folder containing the
# `promptresponse` and `apr` binaries). It installs the binaries under
# ~/.local, registers the .apr/.aprt/.aprf MIME types and a desktop entry, and
# sets PromptResponse as the default handler — so double-clicking those files
# opens the app. No root required; no .NET runtime required.
#
# Uninstall: pass --uninstall.
#
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DATA="${XDG_DATA_HOME:-$HOME/.local/share}"
LIBDIR="$HOME/.local/lib/promptresponse"
BINDIR="$HOME/.local/bin"
DESKTOP="$DATA/applications/promptresponse.desktop"
MIME="$DATA/mime/packages/promptresponse.xml"
ICON="$DATA/icons/hicolor/256x256/apps/promptresponse.png"

refresh() {
  update-mime-database "$DATA/mime" >/dev/null 2>&1 || true
  update-desktop-database "$DATA/applications" >/dev/null 2>&1 || true
}

if [[ "${1:-}" == "--uninstall" ]]; then
  rm -rf "$LIBDIR"
  rm -f "$BINDIR/apr" "$DESKTOP" "$MIME" "$ICON"
  refresh
  echo "PromptResponse desktop integration removed."
  exit 0
fi

echo "▶ Installing PromptResponse for the current user…"

# Binaries
mkdir -p "$LIBDIR" "$BINDIR"
install -m755 "$HERE/promptresponse" "$LIBDIR/promptresponse"
install -m755 "$HERE/apr" "$LIBDIR/apr"
ln -sf "$LIBDIR/apr" "$BINDIR/apr"

# Icon + MIME types
install -Dm644 "$HERE/packaging/promptresponse.png" "$ICON"
install -Dm644 "$HERE/packaging/promptresponse.xml" "$MIME"

# Desktop entry (resolve the Exec placeholder to the installed binary)
mkdir -p "$DATA/applications"
sed "s|APP_EXEC|$LIBDIR/promptresponse|" "$HERE/packaging/promptresponse.desktop" > "$DESKTOP"
chmod 644 "$DESKTOP"

refresh

# Register as default handler for each APR type
for t in application/x-apr application/x-aprt application/x-aprf; do
  xdg-mime default promptresponse.desktop "$t" >/dev/null 2>&1 || true
done

echo "✓ Installed."
echo "  • Double-click .apr / .aprt / .aprf to open PromptResponse."
echo "  • CLI available as 'apr' (ensure $BINDIR is on your PATH)."
echo "  • Uninstall with: $HERE/install-desktop.sh --uninstall"

#!/usr/bin/env bash
#
# Publish self-contained, single-file PromptResponse binaries (Desktop + CLI)
# for a target runtime. No .NET runtime is required on the end-user machine.
#
# Usage:
#   scripts/publish.sh [--rid <rid>] [--version <v>] [--output <dir>] [--no-archive]
#
#   --rid       Target runtime identifier (default: linux-x64). e.g. win-x64, osx-x64.
#   --version   Version stamped into the binaries and archive name (default: from git).
#   --output    Output directory (default: dist).
#   --no-archive  Skip building the .tar.gz / .zip; leave the staged folder only.
#
# Produces, under <output>/:
#   promptresponse-<version>-<rid>/        staged folder with both binaries + docs
#   promptresponse-<version>-<rid>.tar.gz  (linux/osx) or .zip (windows)
#
set -euo pipefail

cd "$(dirname "$0")/.."

RID="linux-x64"
VERSION=""
OUTPUT="dist"
ARCHIVE=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)        RID="$2"; shift 2 ;;
    --version)    VERSION="$2"; shift 2 ;;
    --output)     OUTPUT="$2"; shift 2 ;;
    --no-archive) ARCHIVE=0; shift ;;
    -h|--help)    grep '^#' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

# Derive version from the latest tag when not provided (strip a leading 'v').
if [[ -z "$VERSION" ]]; then
  VERSION="$(git describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || true)"
  VERSION="${VERSION:-0.0.0}"
fi

# Windows binaries get a .exe suffix.
EXE=""
[[ "$RID" == win-* ]] && EXE=".exe"

STAGE_NAME="promptresponse-${VERSION}-${RID}"
STAGE="${OUTPUT}/${STAGE_NAME}"

echo "▶ Publishing PromptResponse ${VERSION} for ${RID}"
rm -rf "$STAGE"
mkdir -p "$STAGE"

COMMON=(
  -c Release
  -r "$RID"
  --self-contained true
  -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:DebugType=none
  -p:DebugSymbols=false
  -p:Version="$VERSION"
  --nologo
)

publish_one() {
  local project="$1" srcname="$2" destname="$3" tmp
  tmp="$(mktemp -d)"
  echo "  • $project → $destname$EXE"
  # A NuGet lock file records one runtime graph.  Create the target graph in
  # this disposable release workspace, then publish without another restore.
  # Package versions remain constrained by the committed lock and global.json.
  dotnet restore "$project" -r "$RID" -p:RestoreLockedMode=false --nologo >/dev/null
  dotnet publish "$project" "${COMMON[@]}" --no-restore -o "$tmp" >/dev/null
  cp "$tmp/${srcname}${EXE}" "$STAGE/${destname}${EXE}"
  rm -rf "$tmp"
}

# srcname is the published assembly name (Desktop = project name; CLI sets <AssemblyName>apr</AssemblyName>).
publish_one src/PromptResponse.Desktop PromptResponse.Desktop promptresponse
publish_one src/PromptResponse.Cli     apr                    apr

# Bundle user-facing docs + license alongside the binaries.
cp LICENSE "$STAGE/" 2>/dev/null || true
cp README.md "$STAGE/" 2>/dev/null || true

# For Linux, bundle the desktop-integration installer so the tarball is
# self-installing (double-click .apr/.aprt/.aprf after running install-desktop.sh).
if [[ "$RID" == linux-* ]]; then
  mkdir -p "$STAGE/packaging"
  cp packaging/linux/install-desktop.sh "$STAGE/install-desktop.sh"
  cp packaging/linux/promptresponse.desktop "$STAGE/packaging/"
  cp packaging/linux/promptresponse.xml "$STAGE/packaging/"
  cp src/PromptResponse.Desktop/Assets/app-icon-256.png "$STAGE/packaging/promptresponse.png"
  chmod +x "$STAGE/install-desktop.sh"
fi

if [[ "$ARCHIVE" -eq 1 ]]; then
  if [[ "$RID" == win-* ]]; then
    ( cd "$OUTPUT" && zip -qr "${STAGE_NAME}.zip" "$STAGE_NAME" )
    echo "✓ ${OUTPUT}/${STAGE_NAME}.zip"
  else
    tar -czf "${OUTPUT}/${STAGE_NAME}.tar.gz" -C "$OUTPUT" "$STAGE_NAME"
    echo "✓ ${OUTPUT}/${STAGE_NAME}.tar.gz"
  fi
fi

echo "✓ Staged: $STAGE"

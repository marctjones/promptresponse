#!/usr/bin/env bash
# Repair local build-state problems that can prevent `dotnet test` on macOS.
# Run this from a normal Terminal, not a filesystem-sandboxed agent session.
set -euo pipefail

PROJECT_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
USER_HOME="${HOME:?HOME must be set}"
NUGET_CACHE_ROOT="${USER_HOME}/.local/share/NuGet"
NUGET_HTTP_CACHE="${NUGET_CACHE_ROOT}/http-cache"
AVALONIA_LOG_DIRECTORY="${USER_HOME}/Library/Application Support/AvaloniaUI/BuildServices"
STAMP="$(date +%Y%m%d-%H%M%S)"

echo "Stopping shared .NET build services..."
dotnet build-server shutdown || true

mkdir -p "${NUGET_CACHE_ROOT}"
if [[ -d "${NUGET_HTTP_CACHE}" ]]; then
  BACKUP="${NUGET_CACHE_ROOT}/http-cache.backup-${STAMP}"
  echo "Moving stale NuGet HTTP cache to ${BACKUP}"
  mv "${NUGET_HTTP_CACHE}" "${BACKUP}"
fi
mkdir -p "${NUGET_HTTP_CACHE}"
mkdir -p "${AVALONIA_LOG_DIRECTORY}"

echo "Running the full test suite..."
cd "${PROJECT_ROOT}"
dotnet test

echo "Done. If a CMS test asks for Keychain access, unlock your login keychain and rerun this script."

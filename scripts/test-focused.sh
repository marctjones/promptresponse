#!/usr/bin/env bash

# Run a focused .NET test command with intermediate and result paths isolated
# from other local builds. This avoids CS2012/CS0016 races when several
# refactoring lanes run `dotnet test` at once in the same checkout.
set -euo pipefail

if [[ $# -eq 0 ]]; then
  echo "Usage: scripts/test-focused.sh <dotnet test arguments>" >&2
  echo "Example: scripts/test-focused.sh tests/PromptResponse.Core.Tests --filter 'FullyQualifiedName~Conformance'" >&2
  exit 64
fi

repo_root="$(git rev-parse --show-toplevel)"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/promptresponse-test.XXXXXX")"
results_root="$test_root/TestResults"
ln -s "$repo_root" "$test_root/repo"

echo "Using isolated local build outputs: $test_root" >&2

cd "$repo_root"
dotnet test "$@" \
  -m:1 \
  --results-directory "$results_root" \
  -p:PromptResponseBuildIsolationRoot="$test_root" \
  -p:UseSharedCompilation=false \
  -p:GenerateDocumentationFile=false

#!/usr/bin/env bash
# Runs the APR beta.6 contract gates and prints elapsed time per implementation.
set -euo pipefail

ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
run() {
  local label="$1"
  shift
  local started=$SECONDS
  "$@"
  printf 'PASS  %-24s %ss\n' "$label" "$((SECONDS - started))"
}

cd "$ROOT"
PYTHON_BIN="python3"
[[ -x python/.venv/bin/python ]] && PYTHON_BIN="python/.venv/bin/python"
run 'schema and examples' "$PYTHON_BIN" scripts/check-schema.py
if [[ -x python/.venv/bin/python ]]; then
  run 'python beta6' bash -lc 'cd python && .venv/bin/python -m pytest tests/test_beta6.py tests/test_wire_boundaries.py -q'
else
  run 'python beta6' bash -lc 'cd python && python3 -m pytest tests/test_beta6.py tests/test_wire_boundaries.py -q'
fi
run 'typescript beta6' bash -lc 'cd typescript && npm test'
run 'java beta6' ./java/run-tests.sh
run 'dotnet core beta6' dotnet test tests/PromptResponse.Core.Tests --no-restore --filter 'FullyQualifiedName~Beta6'
run 'desktop beta6 stream' dotnet test tests/PromptResponse.Desktop.Tests --no-restore --filter 'FullyQualifiedName~Beta6'
echo 'APR beta.6 compliance benchmark completed.'

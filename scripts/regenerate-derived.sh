#!/usr/bin/env bash
# Regenerate every artifact derived from the specification, then run the deterministic
# gates that CI runs over them.
#
# The specification is normative; the corpus, the suites, the OSCAL catalog, the
# conformance statement, the review rubric and the specification manifest are derived
# from it. Editing the specification (or tests/registry.json, which the manifest
# digests) makes each of them stale, and CI fails on any one that is. Doing this by hand
# is how one gets forgotten, so this is the one place the list lives.
#
#   scripts/regenerate-derived.sh            # write, then check
#   scripts/regenerate-derived.sh --check    # check only; writes nothing
#
# Run it after the LAST edit to the specification or registry, and run the SDK drivers
# afterwards (scripts/run-conformance.py); this script does not score an implementation.
# Exits non-zero if any gate fails.
set -u
cd "$(dirname "$0")/.."
PY="${PYTHON:-.venv/bin/python}"
[ -x "$PY" ] || PY=python3

write=1
[ "${1:-}" = "--check" ] && write=0

if [ "$write" = 1 ]; then
  # Order matters: the manifest digests the suites and the registry, so it is last.
  for generator in extract-spec-examples build-suite build-renderer-suite build-oscal \
                   build-conformance-statement build-review-rubric build-spec; do
    echo "write  $generator: $("$PY" -I "scripts/$generator.py" --write 2>&1 </dev/null | tail -1)"
  done
fi

failed=0
gate() {
  local out rc
  out=$(timeout 600 "$PY" -I "$@" 2>&1 </dev/null); rc=$?
  if [ "$rc" -ne 0 ]; then failed=1; echo "FAIL   $*"; echo "$out" | tail -4 | sed 's/^/         /'; fi
}

gate scripts/extract-spec-examples.py
gate scripts/check-corpus.py
gate scripts/build-corpus.py
gate scripts/validate-apr.py --spec-examples
gate scripts/build-suite.py
gate scripts/build-renderer-suite.py
gate scripts/build-oscal.py
gate scripts/build-conformance-statement.py
gate scripts/build-review-rubric.py
gate scripts/build-spec.py
gate scripts/check-test-registry.py
gate scripts/check-spec-shape.py
gate scripts/check-spec-completeness.py
gate scripts/check-schema-agrees.py
gate scripts/check-schema.py
gate scripts/check-spec-approach.py --gate
gate scripts/check-example-coverage.py --gate
gate scripts/check-docs.py
gate scripts/check-rule-evidence.py
gate scripts/check-suite-coverage.py
gate scripts/check-harness-teeth.py
gate scripts/check-renderer-teeth.py
gate scripts/check-oscal-teeth.py
gate scripts/check-oracle.py
gate scripts/check-expression-oracle.py

if [ "$failed" = 0 ]; then echo "All derived artifacts are current and every gate passes."; fi
exit "$failed"

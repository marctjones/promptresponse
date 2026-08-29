# Small stable MVP release checklist

The MVP supports local APR files only: author/open a template, fill it, save or
reopen a `.aprf`, validate it with the CLI, and export it. It deliberately does
not automatically send data anywhere.

Before a release tag:

1. Run `dotnet test --configuration Release`, `python3 scripts/check-schema.py`,
   `python3 scripts/check-test-registry.py`, and `python3 scripts/check-docs.py`.
2. In `python/`, run `pip install -e ".[test]" && pytest -q`; in `typescript/`,
   run `npm ci && npm test`; in `demos/web/`, run `npm ci && npm test`; and run
   `java/run-tests.sh`.
3. Run `scripts/release-smoke.sh` for the host artifact. It exercises the staged
   CLI, exports, PDF import, and signing round trip; the desktop binary must also
   answer `--help` from that artifact.
4. The stable-beta gate is automated. Human accessibility certification, macOS
   AX-tree evidence collection, and release-candidate screen-reader testing are
   valuable follow-up work, but are expressly outside this beta scope.

`apr-sig-v3` is a beta integrity/provenance feature, not complete external
workflow attestation. Do not market it as the sole evidence trail until the
witnessed-manifest profile in issue #88 exists.

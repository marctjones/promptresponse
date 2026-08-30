# Release smoke test

Validate a downloaded or staged artifact, not only the source checkout.

- [ ] Install or unpack without relying on a local build.
- [ ] Launch, open a starter template, fill, save, close, and reopen.
- [ ] Export flat PDF, fillable PDF, PDF/A, read-only HTML, and fillable HTML.
- [ ] Import good and poor fillable PDFs; verify quality review.
- [ ] Generate, sign, verify, and tamper with a test signature.
- [ ] Exercise keyboard navigation and the relevant assistive-technology evidence.
- [ ] Run `scripts/release-smoke.sh` and the core conformance suite.

Record platform, artifact name, path, and smallest reproducer for every failure.

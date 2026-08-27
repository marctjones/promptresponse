# Release Smoke Test

<!-- AI-ASSISTANT-README -->
Use this checklist before tagging a release or after downloading release
artifacts. It validates the shipped app, not just the source tree.
<!-- END-AI-ASSISTANT-README -->

Run this checklist on every platform artifact produced by `.github/workflows/release.yml`:

- Windows x64 installer
- Windows x64 portable zip
- Linux x64 tarball
- macOS arm64 tarball
- macOS x64 tarball

For the automated CLI/export/import/signature subset on the current platform,
run:

```bash
scripts/release-smoke.sh
```

The script publishes a release-style artifact into `dist-smoke/` and tests the
staged `apr` binary rather than developer build output.

## Install And Launch

- [ ] Install or unpack the artifact on a machine without relying on a local build output.
- [ ] Launch the desktop app.
- [ ] Confirm the About dialog reports the expected release version.
- [ ] Confirm the home screen appears on first launch.
- [ ] Confirm starter templates are visible.

## Core User Flow

- [ ] Create a new form from a starter template.
- [ ] Fill at least three different field types.
- [ ] Confirm progress updates while typing.
- [ ] Confirm advisory warnings appear for an intentionally invalid typed value.
- [ ] Save as `.aprf`.
- [ ] Close and reopen the saved file from Recent files.

## Export Flow

- [ ] Export flat PDF.
- [ ] Open Print Preview and confirm the title, sections, fields, tables, and signature summary match the document.
- [ ] Export fillable PDF.
- [ ] Export PDF/A archival PDF with `--pdfa` from the CLI.
- [ ] Export read-only HTML.
- [ ] Export fillable HTML, open it in a browser, fill a field, and download `.aprf`.
- [ ] Confirm exported files contain the document title and filled responses.

## Import Flow

- [ ] Import a known-good fillable PDF.
- [ ] Confirm generated `.aprt` opens in the editor.
- [ ] Confirm import quality summary is visible in CLI output or desktop warning flow.
- [ ] Import a low-quality fillable PDF and confirm the desktop review dialog shows score, recommendation, flag summary, and sample fields before opening.
- [ ] Import a flat/scanned PDF and confirm the app explains that the document-to-APR skill is the path.

## Signing Flow

- [ ] Generate a test signing certificate with `apr keygen`.
- [ ] Sign a template as publisher with a submission URL.
- [ ] Verify the signed file with `apr verify`.
- [ ] Open the signed file in the desktop app and confirm the Signatures panel appears.
- [ ] Sign filled responses from the desktop app.
- [ ] Tamper with one signed response in a copy and confirm verification reports invalid content.

## Accessibility And Keyboard

- [ ] Navigate the home screen, editor, fill view, export menus, and signature panel by keyboard.
- [ ] Confirm every actionable control has a useful accessible name.
- [ ] On Linux, run `./tests/at-spi/run_at_spi_smoke.sh` against the release build when available.

## CLI

- [ ] Run `apr validate examples/contact-intake.aprt`.
- [ ] Run `apr info examples/contact-intake.aprt`.
- [ ] Run `apr stats examples/contact-intake.aprt`.
- [ ] Run `apr export examples/contact-intake.aprt --format=json`.
- [ ] Run `apr export examples/contact-intake.aprt --format=pdf --output=/tmp/contact-intake.pdf`.

## SDK Conformance

- [ ] Run `dotnet test tests/PromptResponse.Core.Tests --filter FullyQualifiedName~ConformanceCorpusTests`.
- [ ] Confirm every file under `tests/Conformance/v1/valid/` validates and round-trips.
- [ ] Confirm every file under `tests/Conformance/v1/invalid/` is rejected by validation.

Record failures with platform, artifact name, exact command or UI path, and the
smallest repro file that demonstrates the issue.

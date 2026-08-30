# User guide

Use the desktop home screen to open a document, create a blank template, or start from a bundled template. Templates use `.aprt`; filled forms use `.aprf`; legacy `.apr` is auto-detected.

Authors add sections and prompts, choose advisory hints, provide useful labels/help, and save a template. Fillers use text or optional assisted widgets, can switch to wizard mode for long forms, review advisories, and save a filled form. Responses remain editable strings even when a hint suggests a type.

`./run.sh --usage` and `./run.ps1 usage` describe launcher options. `apr help` lists CLI commands. Common operations are `apr validate`, `apr info`, `apr fill`, `apr review`, `apr export`, `apr import`, `apr sign`, and `apr verify`.

Export creates CSV, JSON, text, HTML, flat PDF, fillable PDF, or PDF/A without adding layout to APR. `apr import` converts fillable PDFs and reports quality limits; use the document-to-APR skill for flat or scanned source documents. Signing and explicit submission are described in [Signing](SIGNING.md).

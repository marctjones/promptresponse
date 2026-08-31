# PromptResponse browser demo

This is the browser-facing reference demo. It is deliberately separate from
the Python/Flask reference demo at the repository root:

- this demo runs the TypeScript `@promptresponse/core` SDK in the browser to
  parse beta.6 JSONC/YAML streams, select every form occurrence, explain
  non-gating attestation resolution, render, validate, fill, and download an APR document;
- `web-demo.py` uses the Python SDK to demonstrate server-side/local-host
  processing, including server-side live advisories and saving outputs.

Neither demo uploads documents or follows `metadata.submissionUrls`. Submission
is a separate explicit user action and transport policy.

## Run locally

```bash
npm --prefix demos/web install
npm --prefix demos/web run build
python3 -m http.server 8000
```

Then open `http://127.0.0.1:8000/demos/web/` and choose an `.apr`, `.aprt`, or
`.aprf` file. The server only serves static files; the browser reads the chosen
file locally and downloads the completed `.aprf` locally.

## Why Flask does not import the TypeScript renderer

The TypeScript renderer is browser/Node JavaScript, while the Flask demo is a
Python server. Calling it through a subprocess, a bundler, or an embedded JS
runtime would introduce two rendering authorities and make the Python reference
demo less reliable. Keep the boundary explicit: use the TypeScript SDK for
browser rendering and interaction; use the Python SDK for server-side parsing,
validation, and storage. Shared conformance fixtures keep their core behavior
aligned.

# Product

## Purpose

PromptResponse replaces page-bound PDF and Word forms with APR: a local-first,
semantic JSON format for reusable templates and filled responses. People can author,
fill, save, review, export, and process forms without turning layout into data.

## Users and core loop

The primary users are people who create or complete ordinary office, public-sector,
and privacy-sensitive forms, plus developers who process the resulting structured
data. The essential loop is:

**create or open a template → fill it with accessible assistance → save a filled
form → export, hand off, or process the structured result.**

## Product principles

- **Semantic, not page-based.** APR stores questions, responses, sections, roles,
  and advisory hints; renderers choose presentation.
- **Local-first and safe to open.** Core document operations are offline and opening
  a file never performs network activity or executes embedded code.
- **Responses stay human.** Responses are strings. Hints improve rendering and
  advisory feedback but never make an otherwise visible answer invalid.
- **Accessible by design.** Keyboard operation, useful accessible names, visible
  focus, adaptable presentation, and assistive-technology evidence are product work,
  not a later compliance layer.
- **Open and interoperable.** The specification, with the schema and conformance
  corpus derived from it, enables readers and writers across languages without
  vendor lock-in.

## Deliberate boundaries

APR is not a word processor, layout language, hosted form SaaS, workflow engine, or
database. Pixel-perfect layout, cloud-by-default behavior, rich embedded media, and
script execution are outside the format. Optional transport, collaboration, mobile,
and enterprise capabilities require explicit product decisions on the roadmap.

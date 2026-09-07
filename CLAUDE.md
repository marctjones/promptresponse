# PromptResponse agent guidance

Read [docs/README.md](docs/README.md) before making product, format, architecture, UX, or documentation changes.

- The APR specification is normative; the JSON Schema, type registry, and
  conformance corpus are derived from it. A derived artifact that disagrees with
  the specification has the defect, as does an implementation.
- Preserve local-first, semantic, advisory-hint, string-response, no-layout, and safe-to-open constraints.
- Use focused tests first, then full validation.
- Treat keyboard paths, meaningful accessible names, contrast, and profile behavior as functional requirements.
- Update the designated authority document; do not create duplicate plans, visions, architectures, or feature trackers.
- When code or documentation is confirmed dead (verified unreferenced, superseded by
  a later decision, or describing something that no longer exists), delete it
  outright rather than commenting it out, hedging with a "kept just in case" remark,
  or leaving it half-removed. This repository commits often, so restoring from git
  history is cheap; confirm the deletion is safe (grep the whole repo, check test
  coverage, rebuild and retest), then delete with confidence.
- Commit regularly and in sensible, single-purpose batches rather than one large
  change — do not wait to be asked. A small logical unit (a cleanup, a new gate, a
  doc fix) is one commit; unrelated changes from the same session go in separate
  commits. Frequent commits are what make the confident-deletion guidance above
  safe in practice.

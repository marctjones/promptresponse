# Development guide

Install the SDK pinned by `global.json`, then run:

```bash
dotnet test --configuration Release
python3 scripts/check-test-registry.py
python3 scripts/check-docs.py
```

Use corpus/schema/specification for format behavior and product, architecture, and UX documents for product behavior.

1. Add or update the focused test before changing behavior.
2. Preserve Core/UI and format/presentation boundaries.
3. For UI work, verify keyboard operation, accessible names, contrast, and profile behavior.
4. Run the smallest relevant suite, then full validation.
5. Update the document that owns a changed claim; never create a second status, plan, vision, or architecture document.

Nullable references, warnings as errors, deterministic builds, and package locks are mandatory. Do not weaken a threshold, skip a test, or rewrite a corpus fixture merely to hide a regression.

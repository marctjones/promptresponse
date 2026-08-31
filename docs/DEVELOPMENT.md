# Development guide

Install the SDK pinned by `global.json`, then run:

```bash
dotnet test --configuration Release
python3 scripts/check-test-registry.py
python3 scripts/check-docs.py
```

For a focused .NET suite while another local build or test may be running, use
the output-isolated launcher instead of directing two `dotnet test` commands at
the checkout's shared `obj/` folders:

```bash
scripts/test-focused.sh tests/PromptResponse.Core.Tests --filter 'FullyQualifiedName~Conformance'
```

Pass any normal `dotnet test` project, solution, filter, or configuration
arguments after the script name. It creates a unique temporary intermediate
build, output, test-results, and NuGet HTTP-cache root for that invocation,
disables the shared compiler server, uses one MSBuild worker per invocation,
and omits XML documentation only for this local focused-check mode. The private
NuGet cache preserves vulnerability-audit checks while preventing parallel
focused restores from racing on the atomic advisory-cache update. The temporary
output root mirrors the normal repository-relative layout, so corpus tests
continue to find their fixtures. Do not use it for release verification: CI
retains normal output paths and generates XML documentation during its release
build.

Use corpus/schema/specification for format behavior and product, architecture, and UX documents for product behavior.

1. Add or update the focused test before changing behavior.
2. Preserve Core/UI and format/presentation boundaries.
3. For UI work, verify keyboard operation, accessible names, contrast, and profile behavior.
4. Run the smallest relevant suite, then full validation.
5. Update the document that owns a changed claim; never create a second status, plan, vision, or architecture document.

Nullable references, warnings as errors, deterministic builds, and package locks are mandatory. Do not weaken a threshold, skip a test, or rewrite a corpus fixture merely to hide a regression.

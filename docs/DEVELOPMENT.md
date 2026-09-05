# Development guide

Install the SDK pinned by `global.json`, then run:

```bash
dotnet test --configuration Release
python3 scripts/check-test-registry.py
python3 scripts/check-docs.py
```

The specification and conformance scripts under `scripts/` are plain Python 3 and
import no SDK, but several need packages the standard library does not carry.
They are declared in `scripts/requirements.txt` and belong in a virtual
environment — a Homebrew or system Python refuses to install into itself, and
overriding that is not the answer:

```bash
python3 -m venv .venv && .venv/bin/pip install -r scripts/requirements.txt
```

Then run the gates with `.venv/bin/python` in place of `python3`, or activate the
environment first. `python/.venv`, created by `uv sync`, carries the same
packages and works too; the requirements file exists so the tooling states its
own dependencies rather than borrowing the SDK's.

A gate whose package is missing exits non-zero saying which one, and never
reports the absence as a defect in a document — but a gate that cannot run has
not passed, so check that the environment is the one you think it is before
trusting a green run.

The specification has a completeness review with a deterministic half and a
judgement half. `scripts/check-spec-completeness.py` is the deterministic half and
runs in CI: every concept the registry cites resolves to a substantive section,
every schema member has a normative sentence, every profile has a checklist, and
no recorded gap still describes a rule that is now fully evidenced. The judgement
half is opt-in, local, and never a gate:

```bash
python3 scripts/build-review-rubric.py --write     # one item per ungated rule
python3 scripts/run-spec-semantic-review.py --dry-run
python3 scripts/run-spec-semantic-review.py --model-path ~/models/Qwen3-8B-4bit-mlx
```

The rubric carries each rule's own section as its excerpt and is asked in bounded
batches, so the reviewer sees one rule's text rather than three thousand lines.
The model never ratifies the specification; its output is review leads for a
person, and it is written to a gitignored artifact because it is specific to one
machine and one specification digest.

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

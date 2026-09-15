# Fillable import snapshots

What `apr import` produces today for every AcroForm-carrying form in
`scripts/pdf-form-benchmark/corpus/`, serialized through `AprJsonSerializer` —
the same path the CLI writes through.

These exist for milestone #49, which unifies AcroForm import and visual
conversion into one pipeline. That refactor moves code every fillable PDF
already depends on, and its stated done-criterion is that a fillable PDF's
import output is unchanged by it. These files are that criterion, executable.

## When the test fails

That is the tool working. A diff means import output moved, and the only
question is whether the move was intended.

To accept a deliberate change:

```bash
UPDATE_IMPORT_SNAPSHOTS=1 dotnet test tests/PromptResponse.Rendering.Pdf.Tests
```

then commit the regenerated files **as their own reviewable change**, so the
diff is visible in history rather than buried in the commit that caused it.

Never hand-edit a snapshot to make a test pass. A snapshot edited to match the
code pins nothing and is worse than no snapshot, because it looks like
coverage.

## Why the source PDFs are not here

They are already committed once, in `scripts/pdf-form-benchmark/corpus/`.
`tests/Fixtures/import-corpus/README.md` records the decision not to duplicate
them under `tests/`.

## Coverage

11 forms, 444 prompts. A companion test asserts the set of files here exactly
matches the list of forms the suite claims to pin — a stale snapshot for a
removed form, or a form with no snapshot, is coverage that looks complete and
is not.

# promptresponse (Python)

A reader, writer, and CEL expression evaluator for APR beta.6. It also handles
independent attestation records; trust remains caller policy.

```python
import promptresponse as pr

document = pr.load("intake.aprt")
for prompt in document.all_prompts():
    print(prompt.label, "=", prompt.response)

result = pr.validate(document)
if not result.is_valid:
    for error in result.errors:
        print(error.code, error.path, error.message)

pr.dump(document, "intake-answered.aprf")
```

## Expression profile

Expression hints (`exprValue`, `exprValidation`, …) are evaluated with CEL using
APR's typed binding. A failed expression is advisory and leaves a stored response
unchanged.

## Rules it holds to

- **Responses are strings.** A response given as a JSON number or boolean is a
  parse failure, never coerced. `null` is tolerated on read and becomes `""`,
  and is never written back.
- **Any text is a valid response.** A hint suggests an affordance; it never
  restricts what may be written.
- **Unknown members survive.** A member from a newer minor version is preserved
  through a round trip; without that, every additive change to the format would
  be destructive.
- **Parse failures are not validation errors.** A flawed document still opens,
  so a reader can show somebody what is wrong with it.

## Isolated development environment and tests

```bash
uv sync --all-extras
uv run pytest
```

The suite runs the shared beta.6 conformance corpus at
`../tests/Conformance/beta6` directly.

# APR semantic specification review

This is an **opt-in, local-only review aid**. It is not a conformance test,
does not alter APR behavior, and cannot certify that the specification is
correct. Deterministic schema, corpus, and structural drift checks retain that
role.

It gives a pinned local MLX model the normative APR Markdown and this rubric,
then records structured, evidence-citing review leads for a human to inspect.
It never downloads a model: `--model-path` must already exist locally.

On Apple silicon, install the optional dependency and invoke it with a pinned
model directory and source revision:

```sh
uv sync --directory python --extra semantic-review
python3 scripts/run-spec-semantic-review.py \
  --model-path /absolute/path/to/local-mlx-model \
  --model-id mlx-community/Qwen3-4B-Instruct-2507-4bit \
  --model-revision <pinned-model-revision>
```

The runner fixes temperature to zero and records the model identity/revision,
seed, MLX package version, input hashes, and rubric version in its JSON output.
Model findings use only `addressed` or `needs_human_review`; there is no model
"pass" verdict. The default output under `artifacts/` is intentionally not a
committed specification artifact.

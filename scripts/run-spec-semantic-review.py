#!/usr/bin/env python3
"""Run an opt-in, local-only MLX review of the APR specification.

This tool emits review leads for a human. It is deliberately not a conformance
test and is not part of the fast CI path.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import sys
from datetime import UTC, datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "python"))

from promptresponse.spec_semantic import (  # noqa: E402
    SemanticReviewError,
    build_prompt,
    load_rubric,
    parse_model_report,
)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--model-path",
        type=Path,
        required=True,
        help="Existing local MLX model directory. Network model downloads are not allowed.",
    )
    # Pinned so two runs are comparable. Override for a different reviewer.
    parser.add_argument(
        "--model-id",
        default="Qwen/Qwen3-8B",
        help="Pinned source model identifier.",
    )
    parser.add_argument(
        "--model-revision",
        default="b968826d9c46dd6066d109eabc6255188de91218-mlx-q4",
        help="Pinned model revision, including any local quantisation.",
    )
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--max-tokens", type=int, default=4096)
    parser.add_argument("--spec", type=Path, default=ROOT / "docs" / "APR_SPECIFICATION.md")
    parser.add_argument(
        "--rubric", type=Path, default=ROOT / "tests" / "spec-semantic" / "rubric.json"
    )
    parser.add_argument(
        "--output", type=Path, default=ROOT / "artifacts" / "spec-semantic-review.json"
    )
    return parser.parse_args()


def main() -> int:
    args = arguments()
    if not args.model_path.is_dir():
        raise SystemExit(f"--model-path must be an existing local directory: {args.model_path}")
    for path in (args.spec, args.rubric):
        if not path.is_file():
            raise SystemExit(f"required input does not exist: {path}")

    try:
        from mlx_lm import generate, load
        from mlx_lm.sample_utils import make_sampler
        import mlx.core as mx
    except ImportError as exc:
        raise SystemExit(
            "MLX support is not installed. On Apple silicon run: "
            "uv sync --directory python --extra semantic-review"
        ) from exc

    rubric_version, items = load_rubric(json.loads(args.rubric.read_text(encoding="utf-8")))
    specification = args.spec.read_text(encoding="utf-8")
    prompt = build_prompt(
        specification=specification, rubric_version=rubric_version, items=items
    )
    mx.random.seed(args.seed)
    model, tokenizer = load(str(args.model_path))

    # Present the prompt the way the model was trained to receive it. A reasoning
    # model asked for JSON through a raw completion prompt answers with its chain
    # of thought first; enable_thinking=False turns that off where the template
    # supports it, and the parser strips it where it does not.
    if getattr(tokenizer, "chat_template", None):
        try:
            prompt = tokenizer.apply_chat_template(
                [{"role": "user", "content": prompt}],
                tokenize=False,
                add_generation_prompt=True,
                enable_thinking=False,
            )
        except TypeError:
            prompt = tokenizer.apply_chat_template(
                [{"role": "user", "content": prompt}],
                tokenize=False,
                add_generation_prompt=True,
            )
    # Greedy decoding, expressed the way current mlx-lm expects it. The older
    # "temp=0.0" keyword was removed upstream, and passing it raised a TypeError
    # before any review could run.
    raw_report = generate(
        model,
        tokenizer,
        prompt=prompt,
        max_tokens=args.max_tokens,
        sampler=make_sampler(temp=0.0),
        verbose=False,
    )
    report = parse_model_report(raw_report, rubric_version=rubric_version, items=items)
    output = {
        "kind": "non-authoritative-apr-specification-semantic-review",
        "warning": "This is local-model review evidence, not an APR conformance result.",
        "created_at": datetime.now(UTC).isoformat(),
        "model": {
            "path": str(args.model_path.resolve()),
            "id": args.model_id,
            "revision": args.model_revision,
            "mlx_lm_version": importlib.metadata.version("mlx-lm"),
            "seed": args.seed,
            "temperature": 0.0,
            "max_tokens": args.max_tokens,
        },
        "inputs": {
            "specification": str(args.spec.resolve()),
            "specification_sha256": sha256(args.spec),
            "rubric": str(args.rubric.resolve()),
            "rubric_sha256": sha256(args.rubric),
        },
        "report": report,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote non-authoritative review evidence to {args.output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except SemanticReviewError as exc:
        raise SystemExit(f"Rejected model report: {exc}") from exc

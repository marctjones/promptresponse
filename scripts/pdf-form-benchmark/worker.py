#!/usr/bin/env python3
"""Runs ONE model against a set of forms. This is meant to be launched as a
subprocess by run_benchmark.py (the supervisor), never invoked directly for
a real benchmark run -- the supervisor is what enforces the per-form
timeout and memory watchdog. Running this file by itself has no safety net.

Loads the model once, then loops the given forms/pages -- same "load once"
efficiency as before, just moved into its own process so the supervisor can
kill it cleanly (SIGKILL releases all its memory back to the OS instantly)
without taking down anything else.
"""
import argparse
import json
import sys
import time
import traceback
from pathlib import Path

import resource_guard
resource_guard.set_mlx_safety_limits()

import mlx.core as mx  # noqa: E402
from mlx_vlm import load, generate  # noqa: E402
from mlx_vlm.prompt_utils import apply_chat_template  # noqa: E402
from mlx_vlm.utils import load_config  # noqa: E402

import apr_schema  # noqa: E402
import parsers  # noqa: E402
import prompts  # noqa: E402
from render_pdf import render  # noqa: E402
from validate_apr import validate  # noqa: E402

HERE = Path(__file__).parent
RESULTS = HERE / "results"
RAW_DIR = RESULTS / "raw"
APRT_DIR = RESULTS / "aprt"
LOG_PATH = RESULTS / "run_log.jsonl"

MAX_TOKENS = {
    "doctags": 4096,
    "dots-ocr-native": 6000,
    "florence-detection": 2048,
    "chat-json": 8000,
    "paligemma-detection": 3000,
}


def log_event(event: dict) -> None:
    with LOG_PATH.open("a") as f:
        f.write(json.dumps(event) + "\n")


def run_model_on_form(model, processor, config, strategy: str, model_id: str,
                       form_id: str, form_name: str, pages: list[Path],
                       generate_kwargs: dict | None = None) -> None:
    prompt_text = prompts.build_prompt(strategy)
    max_tokens = MAX_TOKENS[strategy]
    raw_pages, page_timings = [], []

    out_dir = RAW_DIR / model_id / form_id
    out_dir.mkdir(parents=True, exist_ok=True)

    for i, page_path in enumerate(pages, start=1):
        formatted = apply_chat_template(processor, config, prompt_text, num_images=1)
        t0 = time.time()
        result = generate(
            model, processor, formatted, [str(page_path)],
            max_tokens=max_tokens, temperature=0.0, verbose=False,
            **(generate_kwargs or {}),
        )
        dt = time.time() - t0
        text = result.text if hasattr(result, "text") else str(result)
        raw_pages.append(text)
        page_timings.append(dt)
        (out_dir / f"page-{i}.txt").write_text(text)

    ir = parsers.parse(strategy, raw_pages)
    doc = apr_schema.build_aprt(form_name, ir)

    aprt_dir = APRT_DIR / model_id
    aprt_dir.mkdir(parents=True, exist_ok=True)
    aprt_path = aprt_dir / f"{form_id}.aprt"
    aprt_path.write_text(json.dumps(doc, indent=2))

    validation = validate(aprt_path)

    log_event({
        "model_id": model_id, "form_id": form_id,
        "pages": len(pages), "page_seconds": page_timings,
        "total_seconds": sum(page_timings),
        "peak_mlx_memory_gb": round(mx.get_peak_memory() / 1024**3, 3),
        "field_count": len(ir["fields"]), "section_count": len(doc["sections"]),
        "table_count": len(ir["tables"]), "parse_errors": ir["parse_errors"],
        "valid": validation.get("valid"), "validation_errors": validation.get("errors"),
    })
    print(f"    {form_id}: {len(pages)} page(s), {sum(page_timings):.1f}s, "
          f"{len(ir['fields'])} fields, valid={validation.get('valid')}, "
          f"peak_mlx_mem={mx.get_peak_memory() / 1024**3:.2f}GB", flush=True)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--forms", nargs="+", required=True)
    args = ap.parse_args()

    models = json.loads((HERE / "models.json").read_text())["models"]
    model_cfg = next((m for m in models if m["id"] == args.model), None)
    if model_cfg is None:
        print(f"unknown model id {args.model!r}", file=sys.stderr)
        return 2
    all_forms = {f["id"]: f for f in json.loads((HERE / "corpus_manifest.json").read_text())["forms"]}

    print(f"=== Loading {model_cfg['id']} ({model_cfg['repo']}) "
          f"[MLX cap {resource_guard.MLX_MEMORY_LIMIT_GB:.1f}GB] ===", flush=True)
    t0 = time.time()
    model, processor = load(model_cfg["repo"], trust_remote_code=model_cfg.get("trust_remote_code", False))
    config = load_config(model_cfg["repo"], trust_remote_code=model_cfg.get("trust_remote_code", False))
    print(f"  loaded in {time.time() - t0:.1f}s", flush=True)

    for form_id in args.forms:
        form = all_forms[form_id]
        pages_dir = RESULTS / "pages" / form_id
        pages = sorted(pages_dir.glob("page-*.png")) or sorted(pages_dir.glob("page*.png"))
        if not pages:
            pages = render(form_id)
        try:
            run_model_on_form(model, processor, config, model_cfg["strategy"],
                               model_cfg["id"], form_id, form["name"], pages,
                               generate_kwargs=model_cfg.get("generate_kwargs"))
        except Exception:
            err = traceback.format_exc()
            print(f"    {form_id}: FAILED\n{err}", flush=True)
            log_event({"model_id": model_cfg["id"], "form_id": form_id, "run_error": err})
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

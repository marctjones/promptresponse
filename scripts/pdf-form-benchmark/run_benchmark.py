#!/usr/bin/env python3
"""Run every model in models.json against every form in corpus_manifest.json.

For each model: load weights ONCE, then loop over every form's rendered
pages, generate, save the raw output, and (once all of a form's pages are
in) parse -> build .aprt -> validate. Progress is appended to
results/run_log.jsonl as it happens so a crash partway through doesn't lose
completed work; rerun and already-done (model, form) pairs are skipped.
"""
import argparse
import json
import time
import traceback
from pathlib import Path

import mlx.core as mx
from mlx_vlm import load, generate
from mlx_vlm.prompt_utils import apply_chat_template
from mlx_vlm.utils import load_config

import apr_schema
import parsers
import prompts
from render_pdf import render
from validate_apr import validate

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
}


def load_json(path: Path) -> dict:
    return json.loads(path.read_text())


def already_done(model_id: str, form_id: str) -> bool:
    return (APRT_DIR / model_id / f"{form_id}.aprt").exists()


def log_event(event: dict) -> None:
    with LOG_PATH.open("a") as f:
        f.write(json.dumps(event) + "\n")


def run_model_on_form(model, processor, config, strategy: str, model_id: str,
                       form_id: str, form_name: str, pages: list[Path]) -> None:
    prompt_text = prompts.build_prompt(strategy)
    max_tokens = MAX_TOKENS[strategy]
    raw_pages = []
    page_timings = []

    out_dir = RAW_DIR / model_id / form_id
    out_dir.mkdir(parents=True, exist_ok=True)

    for i, page_path in enumerate(pages, start=1):
        formatted = apply_chat_template(processor, config, prompt_text, num_images=1)
        t0 = time.time()
        result = generate(
            model, processor, formatted, [str(page_path)],
            max_tokens=max_tokens, temperature=0.0, verbose=False,
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
        "field_count": len(ir["fields"]), "section_count": len(doc["sections"]),
        "table_count": len(ir["tables"]), "parse_errors": ir["parse_errors"],
        "valid": validation.get("valid"), "validation_errors": validation.get("errors"),
    })
    print(f"    {form_id}: {len(pages)} page(s), {sum(page_timings):.1f}s, "
          f"{len(ir['fields'])} fields, valid={validation.get('valid')}")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--models", nargs="*", help="subset of model ids to run")
    ap.add_argument("--forms", nargs="*", help="subset of form ids to run")
    ap.add_argument("--force", action="store_true", help="rerun even if output exists")
    args = ap.parse_args()

    models = load_json(HERE / "models.json")["models"]
    forms = load_json(HERE / "corpus_manifest.json")["forms"]
    if args.models:
        models = [m for m in models if m["id"] in args.models]
    if args.forms:
        forms = [f for f in forms if f["id"] in args.forms]

    for model_cfg in models:
        model_id = model_cfg["id"]
        print(f"\n=== Loading {model_id} ({model_cfg['repo']}) ===")
        t0 = time.time()
        try:
            model, processor = load(model_cfg["repo"], trust_remote_code=model_cfg.get("trust_remote_code", False))
            config = load_config(model_cfg["repo"], trust_remote_code=model_cfg.get("trust_remote_code", False))
        except Exception:
            print(f"  FAILED to load {model_id}:\n{traceback.format_exc()}")
            log_event({"model_id": model_id, "form_id": None, "load_error": traceback.format_exc()})
            continue
        print(f"  loaded in {time.time() - t0:.1f}s")

        for form in forms:
            form_id, form_name = form["id"], form["name"]
            if not args.force and already_done(model_id, form_id):
                print(f"    {form_id}: skip (already done)")
                continue
            pages_dir = RESULTS / "pages" / form_id
            pages = sorted(pages_dir.glob("page-*.png")) or sorted(pages_dir.glob("page*.png"))
            if not pages:
                pages = render(form_id)
            try:
                run_model_on_form(model, processor, config, model_cfg["strategy"],
                                   model_id, form_id, form_name, pages)
            except Exception:
                err = traceback.format_exc()
                print(f"    {form_id}: FAILED\n{err}")
                log_event({"model_id": model_id, "form_id": form_id, "run_error": err})

        del model, processor
        mx.clear_cache()

    print("\nDone.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Pre-download every model in models.json into the local HF cache.

Run once before run_benchmark.py so model download time isn't attributed to
any single model's timing, and so a flaky network only needs retrying here.
"""
import json
import sys
import time
from pathlib import Path

from huggingface_hub import snapshot_download

HERE = Path(__file__).parent
MODELS = json.loads((HERE / "models.json").read_text())["models"]


def main() -> int:
    failures = []
    for model in MODELS:
        print(f"\n=== {model['id']}  ({model['repo']}) ===")
        start = time.time()
        try:
            path = snapshot_download(repo_id=model["repo"])
            print(f"  ok  {path}  ({time.time() - start:.0f}s)")
        except Exception as exc:  # noqa: BLE001
            failures.append((model["id"], str(exc)))
            print(f"  FAILED: {exc}", file=sys.stderr)
    if failures:
        print(f"\n{len(failures)} model(s) failed to download:", file=sys.stderr)
        for mid, err in failures:
            print(f"  - {mid}: {err}", file=sys.stderr)
        return 1
    print(f"\nAll {len(MODELS)} models cached locally.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

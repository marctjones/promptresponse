#!/usr/bin/env python3
"""Supervisor: runs worker.py as a subprocess once per (model, form) pair.

Why a subprocess per pair, reloading the model each time, instead of one
long-lived process looping every form (which is what this script used to
do): a subprocess is the only thing this supervisor can reliably KILL and
have its memory actually released back to the OS. Running everything
in-process, this benchmark once took down the machine -- see
resource_guard.py's docstring. The model-reload cost (a few seconds per
the load times observed so far) is cheap next to generation time (30-500+s
per page) and cheap next to "the machine needs a hard reboot."

Every (model, form) pair is bounded by:
  - a hard wall-clock timeout (resource_guard.PER_FORM_TIMEOUT_SECONDS)
  - a live memory watchdog that kills the worker if system free memory
    drops below resource_guard.MIN_FREE_MEMORY_GB
  - a lock file so a second invocation of this script (or an ad hoc
    mlx_vlm smoke test run by hand) can't run concurrently and contend
    for the same memory -- concurrent loads are exactly what caused the
    crash this replaces.

Progress is still resumable exactly as before: a (model, form) pair with an
existing results/aprt/<model>/<form>.aprt is skipped unless --force.
"""
import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path

import resource_guard

HERE = Path(__file__).parent
RESULTS = HERE / "results"
APRT_DIR = RESULTS / "aprt"
LOG_PATH = RESULTS / "run_log.jsonl"
LOCK_PATH = HERE / ".benchmark.lock"
VENV_PYTHON = HERE / ".venv" / "bin" / "python3"


def load_json(path: Path) -> dict:
    return json.loads(path.read_text())


def already_done(model_id: str, form_id: str) -> bool:
    return (APRT_DIR / model_id / f"{form_id}.aprt").exists()


def log_event(event: dict) -> None:
    with LOG_PATH.open("a") as f:
        f.write(json.dumps(event) + "\n")


def acquire_lock() -> None:
    if LOCK_PATH.exists():
        try:
            pid = int(LOCK_PATH.read_text().strip())
            os.kill(pid, 0)  # raises if not alive
            print(f"ERROR: another benchmark run (pid {pid}) appears to be active "
                  f"({LOCK_PATH}). Refusing to start a second one concurrently -- "
                  f"that's exactly what caused the earlier crash.", file=sys.stderr)
            sys.exit(1)
        except (ValueError, ProcessLookupError, PermissionError):
            pass  # stale lock from a killed/crashed run; safe to take over
    LOCK_PATH.write_text(str(os.getpid()))


def release_lock() -> None:
    try:
        if LOCK_PATH.exists() and LOCK_PATH.read_text().strip() == str(os.getpid()):
            LOCK_PATH.unlink()
    except OSError:
        pass


def run_pair(model_id: str, form_id: str) -> None:
    free_gb = resource_guard.free_memory_gb()
    if free_gb < resource_guard.MIN_FREE_MEMORY_GB:
        print(f"    {form_id}: SKIPPED -- only {free_gb:.2f}GB free "
              f"(floor {resource_guard.MIN_FREE_MEMORY_GB}GB); waiting 30s and retrying once")
        time.sleep(30)
        free_gb = resource_guard.free_memory_gb()
        if free_gb < resource_guard.MIN_FREE_MEMORY_GB:
            log_event({"model_id": model_id, "form_id": form_id,
                       "skipped_low_memory": True, "free_gb": round(free_gb, 2)})
            print(f"    {form_id}: still low on memory ({free_gb:.2f}GB free) -- skipping this pair")
            return

    proc = subprocess.Popen(
        [str(VENV_PYTHON), "worker.py", "--model", model_id, "--forms", form_id],
        cwd=HERE,
    )
    watchdog = resource_guard.MemoryWatchdog(proc)
    watchdog.start()
    t0 = time.time()
    try:
        proc.wait(timeout=resource_guard.PER_FORM_TIMEOUT_SECONDS)
        timed_out = False
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()
        timed_out = True
    finally:
        watchdog.stop()
    elapsed = time.time() - t0

    if timed_out:
        log_event({"model_id": model_id, "form_id": form_id, "timed_out": True,
                   "timeout_seconds": resource_guard.PER_FORM_TIMEOUT_SECONDS})
        print(f"    {form_id}: KILLED -- exceeded {resource_guard.PER_FORM_TIMEOUT_SECONDS}s timeout")
    elif watchdog.event.triggered:
        log_event({"model_id": model_id, "form_id": form_id, "killed_low_memory": True,
                   "reason": watchdog.event.reason})
        print(f"    {form_id}: KILLED by memory watchdog -- {watchdog.event.reason}")
    elif proc.returncode != 0:
        log_event({"model_id": model_id, "form_id": form_id,
                   "worker_exit_code": proc.returncode})
        print(f"    {form_id}: worker exited with code {proc.returncode} after {elapsed:.1f}s")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--models", nargs="*", help="subset of model ids to run")
    ap.add_argument("--forms", nargs="*", help="subset of form ids to run")
    ap.add_argument("--force", action="store_true", help="rerun even if output exists")
    args = ap.parse_args()

    acquire_lock()
    try:
        models = load_json(HERE / "models.json")["models"]
        forms = load_json(HERE / "corpus_manifest.json")["forms"]
        if args.models:
            models = [m for m in models if m["id"] in args.models]
        if args.forms:
            forms = [f for f in forms if f["id"] in args.forms]

        print(f"MLX memory cap: {resource_guard.MLX_MEMORY_LIMIT_GB:.1f}GB | "
              f"free-memory floor: {resource_guard.MIN_FREE_MEMORY_GB}GB | "
              f"per-form timeout: {resource_guard.PER_FORM_TIMEOUT_SECONDS}s | "
              f"total RAM: {resource_guard.TOTAL_RAM_GB:.1f}GB")

        for model_cfg in models:
            model_id = model_cfg["id"]
            print(f"\n=== {model_id} ===")
            for form in forms:
                form_id = form["id"]
                if not args.force and already_done(model_id, form_id):
                    print(f"    {form_id}: skip (already done)")
                    continue
                run_pair(model_id, form_id)
        print("\nDone.")
        return 0
    finally:
        release_lock()


if __name__ == "__main__":
    raise SystemExit(main())

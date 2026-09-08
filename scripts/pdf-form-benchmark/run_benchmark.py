#!/usr/bin/env python3
"""Supervisor: runs worker.py as a subprocess once PER MODEL, covering
every remaining form in one load -- not once per (model, form) pair.

An earlier version of this spawned a fresh worker per form specifically so
it could be killed safely. That measurably made things worse: reloading an
8B model 9 times under memory pressure took 1.7s -> 6.3s per successive
load (repeated large alloc/dealloc cycles), on top of being pure overhead
in the normal case -- load-once-per-model is both faster and gentler on an
already memory-constrained machine. Safety doesn't require a subprocess per
form, just a subprocess per model that can still be killed and restarted:

  - a live memory watchdog thread kills the worker outright if system free
    memory drops below resource_guard.MIN_FREE_MEMORY_GB, regardless of
    which form is in flight,
  - a stall detector (part of the same watchdog) kills the worker if no
    form has completed in resource_guard.STALL_TIMEOUT_SECONDS -- a hang
    degrades to "restart this model," not "the run never finishes,"
  - if a worker is killed with forms still remaining, the supervisor
    retries with a fresh subprocess covering only what's left, up to
    MAX_RESTARTS_PER_MODEL times, then gives up on that model for this run
    (its already-completed forms are kept; rerun with --force to redo them).

Progress is resumable exactly as before: a (model, form) pair with an
existing results/aprt/<model>/<form>.aprt is skipped unless --force. A lock
file (PID-checked) refuses a second concurrent invocation -- concurrent
model loads are what caused the original crash this whole module exists to
prevent; don't run an ad hoc mlx_vlm smoke test while this is active.
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

MAX_RESTARTS_PER_MODEL = 2


def load_json(path: Path) -> dict:
    return json.loads(path.read_text())


def done_forms(model_id: str, form_ids: list[str]) -> set[str]:
    return {fid for fid in form_ids if (APRT_DIR / model_id / f"{fid}.aprt").exists()}


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


def run_model(model_id: str, form_ids: list[str]) -> None:
    remaining = [f for f in form_ids if f not in done_forms(model_id, form_ids)]
    attempt = 0
    while remaining:
        free_gb = resource_guard.free_memory_gb()
        if free_gb < resource_guard.MIN_FREE_MEMORY_GB:
            print(f"    only {free_gb:.2f}GB free (floor {resource_guard.MIN_FREE_MEMORY_GB}GB); "
                  f"waiting 30s before starting {model_id}")
            time.sleep(30)
            free_gb = resource_guard.free_memory_gb()
            if free_gb < resource_guard.MIN_FREE_MEMORY_GB:
                log_event({"model_id": model_id, "skipped_low_memory": True, "free_gb": round(free_gb, 2)})
                print(f"    still low on memory ({free_gb:.2f}GB free) -- skipping {model_id} entirely this run")
                return

        print(f"    launching worker for {len(remaining)} remaining form(s): {remaining}")
        proc = subprocess.Popen(
            [str(VENV_PYTHON), "worker.py", "--model", model_id, "--forms", *remaining],
            cwd=HERE,
        )

        def progress() -> int:
            return len(done_forms(model_id, form_ids))

        watchdog = resource_guard.MemoryWatchdog(proc, progress_fn=progress)
        watchdog.start()
        proc.wait()
        watchdog.stop()

        if watchdog.event.triggered:
            log_event({"model_id": model_id, "killed": True, "reason": watchdog.event.reason,
                       "remaining_before_kill": remaining})
            print(f"    KILLED -- {watchdog.event.reason}")
        elif proc.returncode != 0:
            log_event({"model_id": model_id, "worker_exit_code": proc.returncode})
            print(f"    worker exited with code {proc.returncode}")

        still_remaining = [f for f in form_ids if f not in done_forms(model_id, form_ids)]
        if not still_remaining:
            return
        if still_remaining == remaining and proc.returncode == 0 and not watchdog.event.triggered:
            # Worker exited cleanly but made no progress at all (shouldn't
            # normally happen -- worker.py catches per-form errors itself)
            print(f"    no progress made and no crash detected; not retrying {model_id} to avoid looping")
            log_event({"model_id": model_id, "gave_up": True, "remaining": still_remaining,
                       "reason": "clean exit with zero progress"})
            return
        remaining = still_remaining
        attempt += 1
        if attempt > MAX_RESTARTS_PER_MODEL:
            log_event({"model_id": model_id, "gave_up": True, "remaining": remaining,
                       "reason": f"exceeded {MAX_RESTARTS_PER_MODEL} restarts"})
            print(f"    giving up on {model_id} for this run after {MAX_RESTARTS_PER_MODEL} restarts; "
                  f"{len(remaining)} form(s) left undone: {remaining}")
            return


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--models", nargs="*", help="subset of model ids to run")
    ap.add_argument("--forms", nargs="*", help="subset of form ids to run")
    ap.add_argument("--force", action="store_true", help="rerun even if output exists (deletes existing .aprt first)")
    args = ap.parse_args()

    acquire_lock()
    try:
        models = load_json(HERE / "models.json")["models"]
        forms = load_json(HERE / "corpus_manifest.json")["forms"]
        if args.models:
            models = [m for m in models if m["id"] in args.models]
        if args.forms:
            forms = [f for f in forms if f["id"] in args.forms]
        form_ids = [f["id"] for f in forms]

        if args.force:
            for m in models:
                for fid in form_ids:
                    p = APRT_DIR / m["id"] / f"{fid}.aprt"
                    if p.exists():
                        p.unlink()

        print(f"MLX memory cap: {resource_guard.MLX_MEMORY_LIMIT_GB:.1f}GB | "
              f"free-memory floor: {resource_guard.MIN_FREE_MEMORY_GB}GB | "
              f"stall timeout: {resource_guard.STALL_TIMEOUT_SECONDS}s | "
              f"total RAM: {resource_guard.TOTAL_RAM_GB:.1f}GB")

        for model_cfg in models:
            model_id = model_cfg["id"]
            pending = [f for f in form_ids if f not in done_forms(model_id, form_ids)]
            print(f"\n=== {model_id}: {len(form_ids) - len(pending)}/{len(form_ids)} already done ===")
            if pending:
                run_model(model_id, form_ids)
        print("\nDone.")
        return 0
    finally:
        release_lock()


if __name__ == "__main__":
    raise SystemExit(main())

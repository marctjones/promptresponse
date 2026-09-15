"""Resource safety for running local VLMs on a memory-constrained Mac.

This exists because running this benchmark WITHOUT these guards took down
the machine: two models loaded concurrently (an 8B chat model plus a second
probe process) on a 24GB-RAM MacBook Air, alongside several other active
Claude Code sessions, produced a shutdown stall and forced reboot. Nothing
in the benchmark's own results was lost (run_log.jsonl and each .aprt are
written as they complete), but the machine itself became unusable. This
module is the fix: never let a model run unsupervised or unbounded again.

Three independent layers, since any one alone is not enough:
  1. An MLX-level hard memory cap (mx.set_memory_limit) so the allocator
     itself refuses to grow past a safe ceiling -- raises in-process
     instead of pressuring the OS.
  2. A live system-memory watchdog thread that polls actual free memory
     (not just what MLX thinks it's using -- torch/transformers
     preprocessing, other processes, etc. all count) and can force-kill
     a runaway subprocess before the OS has to.
  3. A stall detector: the parent watches for forward progress (a new
     completed form) on the worker subprocess and kills it if none appears
     for too long, so a hang degrades to "restart this model" instead of
     "the run never finishes." This is a stall timeout, not a per-form one
     -- one subprocess loads a model ONCE and runs every form it's given,
     because reloading per form was tried first and made things WORSE
     under memory pressure (repeated large alloc/dealloc cycles slowed
     each successive load), on top of being needless overhead normally.
"""
import subprocess
import threading
import time
from dataclasses import dataclass

TOTAL_RAM_GB = int(subprocess.run(
    ["sysctl", "-n", "hw.memsize"], capture_output=True, text=True, check=True
).stdout.strip()) / (1024 ** 3)

# Conservative on purpose: this machine also runs Claude Desktop and
# multiple concurrent Claude Code sessions outside this benchmark's control.
# Leave at least half of physical RAM, and never claim more than 10GB for
# MLX's own allocator regardless of how much RAM the box has.
MLX_MEMORY_LIMIT_GB = min(10.0, TOTAL_RAM_GB * 0.4)
MLX_CACHE_LIMIT_GB = min(4.0, MLX_MEMORY_LIMIT_GB * 0.5)

# Below this much free system memory, kill the worker outright rather than
# hope it recovers -- macOS starts compressing/swapping well before this,
# and swapping a multi-GB model's working set is what actually stalls a
# machine, not a clean OOM.
MIN_FREE_MEMORY_GB = 3.0
WATCHDOG_POLL_SECONDS = 3

# A model's worker subprocess runs every remaining form in one load (see
# worker.py) rather than being relaunched per form -- reloading a model
# per document was tried and measured to make things WORSE under memory
# pressure (repeated large alloc/dealloc cycles slowed each successive
# load: 1.7s -> 6.3s over 9 reloads of the same 8B model), on top of being
# needless overhead in the normal case. So the timeout here isn't "this one
# form is slow," it's "no form has finished in this long" -- a stall
# detector on the whole worker, not a per-document budget. Generous but
# bounded: the slowest observed page so far was ~500s (Qwen3-VL-8B on a
# dense 4-page form), so 20 minutes with no completed form is a genuine
# stall, not a model that's just legitimately slow.
STALL_TIMEOUT_SECONDS = 1200


def set_mlx_safety_limits() -> None:
    import mlx.core as mx
    mx.set_memory_limit(int(MLX_MEMORY_LIMIT_GB * 1024 ** 3))
    mx.set_cache_limit(int(MLX_CACHE_LIMIT_GB * 1024 ** 3))


def free_memory_gb() -> float:
    """Actual system-wide available memory, not process RSS -- this is
    what predicts whether the MACHINE stalls, which is the failure mode
    that actually happened."""
    out = subprocess.run(["vm_stat"], capture_output=True, text=True, check=True).stdout
    page_size = 16384  # Apple Silicon default; vm_stat's header confirms it but this is stable
    stats = {}
    for line in out.splitlines():
        if ":" in line:
            key, _, val = line.partition(":")
            val = val.strip().rstrip(".")
            if val.isdigit():
                stats[key.strip()] = int(val)
    free_pages = (stats.get("Pages free", 0)
                  + stats.get("Pages purgeable", 0)
                  + stats.get("Pages inactive", 0))
    return free_pages * page_size / (1024 ** 3)


@dataclass
class WatchdogEvent:
    triggered: bool = False
    reason: str = ""


class MemoryWatchdog:
    """Background thread that kills `proc` if EITHER system free memory
    drops below MIN_FREE_MEMORY_GB, OR `progress_fn()` (e.g. a count of
    completed forms) hasn't changed in STALL_TIMEOUT_SECONDS -- a hang, not
    memory pressure, is the other real failure mode for a long-running
    worker. Call .start() after launching the subprocess, .stop() once it's
    done (success or otherwise)."""

    def __init__(self, proc: subprocess.Popen, progress_fn=None, on_kill=None):
        self.proc = proc
        self.progress_fn = progress_fn
        self.on_kill = on_kill
        self.event = WatchdogEvent()
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, daemon=True)

    def start(self) -> None:
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self._thread.join(timeout=WATCHDOG_POLL_SECONDS + 1)

    def _kill(self, reason: str) -> None:
        self.event.triggered = True
        self.event.reason = reason
        try:
            self.proc.kill()
        except ProcessLookupError:
            pass
        if self.on_kill:
            self.on_kill(reason)

    def _run(self) -> None:
        last_progress = self.progress_fn() if self.progress_fn else None
        last_progress_time = time.time()
        while not self._stop.is_set():
            if self.proc.poll() is not None:
                return  # process already exited on its own
            free_gb = free_memory_gb()
            if free_gb < MIN_FREE_MEMORY_GB:
                return self._kill(f"system free memory {free_gb:.2f}GB < {MIN_FREE_MEMORY_GB}GB floor")
            if self.progress_fn:
                current = self.progress_fn()
                now = time.time()
                if current != last_progress:
                    last_progress, last_progress_time = current, now
                elif now - last_progress_time > STALL_TIMEOUT_SECONDS:
                    return self._kill(f"no progress in {STALL_TIMEOUT_SECONDS}s (stuck since {current!r})")
            self._stop.wait(WATCHDOG_POLL_SECONDS)

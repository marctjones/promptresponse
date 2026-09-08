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
  3. A hard wall-clock timeout per (model, form) pair, enforced by the
     parent process on the worker subprocess, so a hang degrades to "one
     failed form" instead of "the run never finishes."
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

# Generous but bounded: the slowest observed page in the initial run was
# ~500s (Qwen3-VL-8B on a dense 4-page form). This bounds a genuine hang
# without false-triggering on a model that's just legitimately slow.
PER_FORM_TIMEOUT_SECONDS = 1200


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
    """Background thread that kills `proc` if system free memory drops
    below MIN_FREE_MEMORY_GB. Call .start() after launching the subprocess,
    .stop() once it's done (success or otherwise)."""

    def __init__(self, proc: subprocess.Popen, on_kill=None):
        self.proc = proc
        self.on_kill = on_kill
        self.event = WatchdogEvent()
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._run, daemon=True)

    def start(self) -> None:
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self._thread.join(timeout=WATCHDOG_POLL_SECONDS + 1)

    def _run(self) -> None:
        while not self._stop.is_set():
            if self.proc.poll() is not None:
                return  # process already exited on its own
            free_gb = free_memory_gb()
            if free_gb < MIN_FREE_MEMORY_GB:
                self.event.triggered = True
                self.event.reason = f"system free memory {free_gb:.2f}GB < {MIN_FREE_MEMORY_GB}GB floor"
                try:
                    self.proc.kill()
                except ProcessLookupError:
                    pass
                if self.on_kill:
                    self.on_kill(self.event.reason)
                return
            self._stop.wait(WATCHDOG_POLL_SECONDS)

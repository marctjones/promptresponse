# Layer 3: AT-SPI integration smoke tests

End-to-end accessibility tests that drive the running desktop app via the same
**AT-SPI** bus that **Orca** (Linux), **NVDA** (Windows via UIA), and
**VoiceOver** (macOS via NSAccessibility) consume. Mirrors the in-process
Layer 1 / Layer 2 invariants against the *live* screen-reader-facing surface,
catching regressions where Avalonia's automation-peer tree is fine in-process
but the AT-SPI bridge fails to expose it externally.

## What this layer adds

`AutomationTreeTests.cs` (Layer 1) verifies the in-process `AutomationPeer`
tree. That tree is bridged to AT-SPI via Avalonia's at-spi2 backend — but
only when the AT-SPI services and toolkit modules are running. This script
verifies the bridge is intact and the live tree carries the same invariants:

* Every focusable interactive node has a non-empty Name (so screen readers
  can announce it).
* Every interactive role is a known AT-SPI role.

## Requirements (Debian / Ubuntu)

```sh
sudo apt install xvfb dbus-x11 at-spi2-core python3-gi gir1.2-atspi-2.0
```

## Running

```sh
./tests/at-spi/run_at_spi_smoke.sh
```

The script:

1. Builds `PromptResponse.Desktop` if needed.
2. Starts Xvfb on display `:99` and a private dbus session.
3. Launches `at-spi-bus-launcher` so the AT-SPI registry is reachable.
4. Launches the desktop app with `--open examples/sf-86-background-check.aprt`.
5. Runs `dump_tree.py --check` to walk the live AT-SPI tree and assert the
   Layer 1 invariants.

## Manual inspection

Without `--check`, `dump_tree.py` prints the full AT-SPI tree:

```sh
/usr/bin/python3 tests/at-spi/dump_tree.py --application PromptResponse
```

## Why opt-in (not part of `dotnet test`)

This layer needs `at-spi2-core` running in the test session, plus `Xvfb`
and a private dbus bus. CI runners typically don't have all of that. We
keep Layer 1/2 in the default `dotnet test` for fast deterministic coverage
and run Layer 3 manually before releases.

## Known issue exposed by this layer

Running this script today **exits non-zero with "no AT-SPI application
matching 'PromptResponse' is registered"**. That's a real bug, not a
script bug — the Avalonia desktop app isn't registering itself on the
AT-SPI bus at all (other apps on the same session DO register). Until
the Avalonia-side at-spi backend wiring is fixed, screen readers cannot
see PromptResponse. Tracked separately as an idlergear bug task. Layer 3
will start passing once that's resolved; the framework here is the
guard against future regressions.

## Not covered

* macOS NSAccessibility — needs a different driver (`AXUIElement`).
* Windows UIA — needs `pywinauto` on a Windows runner; Layer 1 covers the
  same invariants in-process there.
* The "feel" of narration. Still needs manual passes with real Orca /
  NVDA / VoiceOver.

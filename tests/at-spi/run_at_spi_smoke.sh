#!/usr/bin/env bash
# Layer 3 smoke test: launches the desktop app under a virtual display +
# at-spi bus, runs the Python AT-SPI checker against it, and shuts down.
#
# Invariants checked: every focusable interactive node in the live AT-SPI
# tree has a non-empty Name. Mirrors the in-process Layer 1 test against
# the actual screen-reader-facing surface.
#
# Usage:
#     ./tests/at-spi/run_at_spi_smoke.sh
#
# Requirements (Debian/Ubuntu):
#     sudo apt install xvfb dbus-x11 at-spi2-core python3-gi gir1.2-atspi-2.0
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo_root"

echo ">>> Building desktop project..."
DOTNET_ROLL_FORWARD=Major dotnet build src/PromptResponse.Desktop -nologo -v quiet >/dev/null

display_num=99
echo ">>> Starting Xvfb on :$display_num"
Xvfb ":$display_num" -screen 0 1280x900x24 >/dev/null 2>&1 &
xvfb_pid=$!
trap 'kill $xvfb_pid 2>/dev/null || true; kill $app_pid 2>/dev/null || true' EXIT

export DISPLAY=":$display_num"
sleep 1

# Use dbus-run-session to spawn a private bus + run everything inside it.
# (`dbus-launch` is part of `dbus-x11` which isn't always installed.)

export GTK_MODULES="${GTK_MODULES:-gail:atk-bridge}"
export QT_ACCESSIBILITY=1

dbus-run-session -- bash -c '
    set +u  # app_pid may stay unset if the launch fails — guard with -u off
    /usr/libexec/at-spi-bus-launcher --launch-immediately >/dev/null 2>&1 &
    sleep 1

    echo ">>> Launching PromptResponse.Desktop..."
    DOTNET_ROLL_FORWARD=Major dotnet run --project '"$repo_root"'/src/PromptResponse.Desktop --no-build -- \
        --open '"$repo_root"'/examples/sf-86-background-check.aprt \
        >/tmp/promptresponse-at-spi.log 2>&1 &
    app_pid=$!
    sleep 6

    echo ">>> Running AT-SPI checks..."
    /usr/bin/python3 '"$repo_root"'/tests/at-spi/dump_tree.py --check
    result=$?

    [ -n "${app_pid:-}" ] && kill "$app_pid" 2>/dev/null || true
    [ -n "${app_pid:-}" ] && wait "$app_pid" 2>/dev/null || true
    exit $result
'
result=$?

if [ "$result" -eq 0 ]; then
    echo ">>> Layer 3 AT-SPI smoke test PASSED"
else
    echo ">>> Layer 3 AT-SPI smoke test FAILED — see /tmp/promptresponse-at-spi.log for app output"
fi
exit $result

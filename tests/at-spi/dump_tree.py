#!/usr/bin/env python3
"""
Layer 3 of the blind-user accessibility test stack.

Walks the AT-SPI accessibility tree of the running PromptResponse desktop app
and prints / asserts properties that screen readers (Orca on Linux, NVDA on
Windows, VoiceOver on macOS) consume in production. This is the end-to-end
companion to the in-process Layer 1/2 tests — it verifies the app actually
exposes itself to the AT bus, which the in-process tests can't.

Run this against an instance launched under Xvfb (or your desktop) — see
run_at_spi_smoke.sh in the same directory.

Usage:
    /usr/bin/python3 dump_tree.py [--check] [--application PromptResponse.Desktop]

    --check  Exit non-zero if any of the same invariants the C# Layer 1 tests
             enforce are violated against the running app.

Dependencies (Debian/Ubuntu):
    sudo apt install python3-gi gir1.2-atspi-2.0 at-spi2-core
"""

import argparse
import sys
import gi  # type: ignore

gi.require_version("Atspi", "2.0")
from gi.repository import Atspi  # type: ignore


# Roles that are valid focusable interactive controls — match the Layer 1
# allow-list in AutomationTreeTests.cs (translated from Avalonia's
# AutomationControlType to AT-SPI Role).
INTERACTIVE_ROLES = {
    Atspi.Role.PUSH_BUTTON,
    Atspi.Role.TOGGLE_BUTTON,
    Atspi.Role.RADIO_BUTTON,
    Atspi.Role.CHECK_BOX,
    Atspi.Role.MENU,
    Atspi.Role.MENU_BAR,
    Atspi.Role.MENU_ITEM,
    Atspi.Role.CHECK_MENU_ITEM,
    Atspi.Role.RADIO_MENU_ITEM,
    Atspi.Role.TEXT,
    Atspi.Role.ENTRY,
    Atspi.Role.PASSWORD_TEXT,
    Atspi.Role.COMBO_BOX,
    Atspi.Role.LIST,
    Atspi.Role.LIST_ITEM,
    Atspi.Role.TABLE_CELL,
    Atspi.Role.LINK,
    Atspi.Role.SLIDER,
    Atspi.Role.SPIN_BUTTON,
    Atspi.Role.PAGE_TAB,
    Atspi.Role.PAGE_TAB_LIST,
}


def find_application(name_substring: str):
    """Find a running app exposed via AT-SPI whose name contains the given substring."""
    desktop = Atspi.get_desktop(0)
    for i in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(i)
        if app and name_substring.lower() in (app.get_name() or "").lower():
            return app
    return None


def walk(node, depth=0):
    """Depth-first iterator over an AT-SPI accessibility subtree."""
    yield node, depth
    try:
        count = node.get_child_count()
    except Exception:
        return
    for i in range(count):
        try:
            child = node.get_child_at_index(i)
        except Exception:
            continue
        if child is None:
            continue
        yield from walk(child, depth + 1)


def is_focusable(node):
    try:
        states = node.get_state_set()
        return states.contains(Atspi.StateType.FOCUSABLE) and states.contains(Atspi.StateType.SHOWING)
    except Exception:
        return False


def is_interactive_control(node):
    try:
        return node.get_role() in INTERACTIVE_ROLES
    except Exception:
        return False


def dump(app):
    print(f"=== AT-SPI tree for {app.get_name()} ===")
    for node, depth in walk(app):
        indent = "  " * depth
        try:
            role = node.get_role_name()
            name = node.get_name()
            states = node.get_state_set()
            focusable = states.contains(Atspi.StateType.FOCUSABLE)
            print(f"{indent}{role!s:14}  name={name!r}{'  [focusable]' if focusable else ''}")
        except Exception as ex:
            print(f"{indent}<error: {ex}>")


def check(app):
    """Same invariants as the C# Layer 1 tests, but against the live AT-SPI tree."""
    failures = []
    for node, _ in walk(app):
        if is_focusable(node) and is_interactive_control(node):
            name = node.get_name() or ""
            if not name.strip():
                role = node.get_role_name()
                failures.append(f"  - focusable {role} has empty AT-SPI Name")
    return failures


def main():
    parser = argparse.ArgumentParser(description="AT-SPI smoke test for PromptResponse.Desktop")
    parser.add_argument("--application", default="PromptResponse",
                        help="Substring of the app's AT-SPI name to look up")
    parser.add_argument("--check", action="store_true",
                        help="Run Layer 1-equivalent checks against the live tree and exit non-zero on failure")
    args = parser.parse_args()

    app = find_application(args.application)
    if app is None:
        print(f"ERROR: no AT-SPI application matching '{args.application}' is registered.", file=sys.stderr)
        print("Is the app running and is at-spi2-core started? Try `dbus-launch --exit-with-session`.", file=sys.stderr)
        return 2

    if args.check:
        failures = check(app)
        if failures:
            print("Layer 3 AT-SPI check FAILED:", file=sys.stderr)
            for f in failures:
                print(f, file=sys.stderr)
            return 1
        print(f"Layer 3 AT-SPI check OK — every focusable interactive node has a non-empty Name.")
        return 0
    else:
        dump(app)
        return 0


if __name__ == "__main__":
    sys.exit(main())

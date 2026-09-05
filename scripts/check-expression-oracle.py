#!/usr/bin/env python3
"""Test the expression oracle every evaluation case is scored against.

`scripts/aprexpr.py` computes the expected result of every case carrying
`expects`, so a defect in it is a defect in what four SDKs will be measured
against. Nothing checked it.

CEL language conformance is not this project's to test — section 11.3 says so,
and cel-spec's own suite is where that is settled. What is ours, and what is
checked here, is the surface APR pins and the way APR uses it:

  * the standard library and standard macros are present, since APR-EXPR-013
    requires them;
  * the extension libraries are absent, since the same rule forbids them and the
    strings extension was withdrawn from this baseline on exactly that evidence;
  * activation composition — a bound prompt id, a reserved name, and the rule
    that a prompt named `ctx` cannot shadow the host context;
  * an unbound name is omitted from the activation rather than declared empty,
    which is what makes a reference to a missing prompt a failure instead of a
    silent false;
  * failure degrades per hint rather than failing the document;
  * a value crossing back into a response is marshalled to its declared type.

    python3 scripts/check-expression-oracle.py
    python3 scripts/check-expression-oracle.py --verbose
"""
from __future__ import annotations

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprexpr  # noqa: E402

PIN = "v0.25.3"

# The standard library surface APR-EXPR-013 requires. Each entry is an expression
# and the string it must evaluate to through the same path a form takes.
STANDARD = [
    ("1 + 2", "3"),
    ("7 / 2", "3"),                      # integer division, not 3.5
    ("7.0 / 2.0", "3.5"),
    ("'a' + 'b'", "ab"),
    ("size('hello')", "5"),
    ("'hello'.startsWith('he')", "true"),
    ("'hello'.endsWith('lo')", "true"),
    ("'hello'.contains('ell')", "true"),
    ("'hello'.matches('^h.*o$')", "true"),
    ("[1, 2, 3].size()", "3"),
    ("3 in [1, 2, 3]", "true"),
    ("{'a': 1}['a']", "1"),
    ("'a' in {'a': 1}", "true"),
    ("true ? 'yes' : 'no'", "yes"),
    ("!false", "true"),
    ("1 < 2 && 2 < 3", "true"),
    ("string(42)", "42"),
    ("int('42') + 1", "43"),
    ("double('1.5') + 0.5", "2"),
    ("timestamp('2026-01-02T03:04:05Z').getFullYear()", "2026"),
    ("duration('60s') > duration('30s')", "true"),
    # Standard macros, which the same rule requires.
    ("[1, 2, 3].all(x, x > 0)", "true"),
    ("[1, 2, 3].exists(x, x > 2)", "true"),
    ("[1, 2, 3].filter(x, x > 1).size()", "2"),
    ("[1, 2, 3].map(x, x * 2)[0]", "2"),
    ("has({'a': 1}.a)", "true"),
]

# Extension functions APR-EXPR-013 forbids. Each must be an evaluation failure,
# not a value: an implementation that happens to carry an extension must not
# quietly answer with it, because a form relying on that answer would then
# evaluate differently somewhere else.
FORBIDDEN = [
    "'  padded  '.trim()",
    "'a,b'.split(',')[0]",
    "'abc'.upperAscii()",
    "'ABC'.lowerAscii()",
    "['a', 'b'].join('-')",
]


def form(prompts: list[dict]) -> dict:
    """The smallest document carrying the given prompts."""
    return {
        "aprVersion": "1.0-beta.6",
        "metadata": {"title": "Oracle"},
        "sections": [{"id": "s", "title": "S", "prompts": prompts}],
    }


def value_of(expression: str, *, prompts: list[dict] | None = None, **activation) -> str:
    """Evaluate `expression` as a computed prompt and return the response it writes."""
    document = form((prompts or []) + [
        {"id": "_probe", "label": "Probe", "hints": {"exprValue": expression}}])
    return aprexpr.evaluate(document, **activation)["responses"]["_probe"]


def main() -> int:
    verbose = "--verbose" in sys.argv
    problems: list[str] = []
    checked = 0

    for expression, expected in STANDARD:
        produced = value_of(expression)
        checked += 1
        if produced != expected:
            problems.append(
                f"standard surface: {expression!r} gave {produced!r}, expected {expected!r}")
        elif verbose:
            print(f"  {expression} -> {produced}")

    for expression in FORBIDDEN:
        # The fallback for a failed exprValue is the stored response, and there is
        # none, so a refusal shows up as the empty string. A value means the
        # extension is present.
        produced = value_of(expression)
        checked += 1
        if produced != "":
            problems.append(
                f"forbidden extension: {expression!r} evaluated to {produced!r}; "
                f"section 11.3 requires the standard library only")
        elif verbose:
            print(f"  {expression} -> refused, as required")

    # Activation. A prompt id binds; a reserved name is supplied by the caller;
    # and a prompt named `ctx` must not be able to take the host context's place.
    answered = [{"id": "qty", "label": "Qty", "response": "3",
                 "hints": {"expectedDataType": "number"}}]
    cases = [
        ("qty * 2.0", {}, answered, "6"),
        ("_today", {"_today": "1970-01-01"}, [], "1970-01-01"),
        ("ctx.role", {"ctx": {"role": "nurse"}}, [], "nurse"),
        # A prompt called ctx does not shadow the reserved name.
        ("ctx.role", {"ctx": {"role": "nurse"}},
         [{"id": "ctx", "label": "Ctx", "response": "attacker"}], "nurse"),
        # An unbound name is a failure, not a false: nothing is declared empty.
        ("nosuch + 1", {}, [], ""),
    ]
    for expression, activation, prompts, expected in cases:
        produced = value_of(expression, prompts=prompts, **activation)
        checked += 1
        if produced != expected:
            problems.append(
                f"activation: {expression!r} with {activation} gave {produced!r}, "
                f"expected {expected!r}")
        elif verbose:
            print(f"  {expression} with {activation} -> {produced!r}")

    # Degradation is per hint. A broken exprValue must not stop a sound
    # exprHidden on another prompt from answering.
    document = form([
        {"id": "broken", "label": "B", "hints": {"exprValue": "nosuch.thing()"}},
        {"id": "sound", "label": "S", "hints": {"exprHidden": "1 < 2"}},
    ])
    result = aprexpr.evaluate(document)
    checked += 1
    if result["responses"].get("broken") != "" or result["hidden"].get("sound") is not True:
        problems.append(
            "degradation is not per hint: a failed exprValue disturbed another "
            f"prompt's exprHidden ({result})")
    elif verbose:
        print("  a failed hint degrades alone")

    # Marshalling back into a response, which is always a string.
    for declared, expression, expected in [
        ("number", "1 + 1", "2"), ("number", "0.5 + 0.25", "0.75"),
        ("text", "'x'", "x"), ("boolean", "1 < 2", "true"),
    ]:
        document = form([{"id": "_probe", "label": "P", "hints": {
            "expectedDataType": declared, "exprValue": expression}}])
        produced = aprexpr.evaluate(document)["responses"]["_probe"]
        checked += 1
        if produced != expected:
            problems.append(
                f"marshalling {declared}: {expression!r} gave {produced!r}, "
                f"expected {expected!r}")
        elif verbose:
            print(f"  {declared}: {expression} -> {produced!r}")

    print(f"Expression oracle checked against the {PIN} surface: {checked} assertions")
    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nEvery evaluation case in the suite is scored against this oracle, so a "
              "defect here\nis a defect in what four implementations will be measured "
              "against.")
        return 1
    print("The standard library and macros are present, the extension libraries are "
          "absent,\nand activation, degradation and marshalling behave as section 11 "
          "states.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

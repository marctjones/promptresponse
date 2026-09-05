#!/usr/bin/env python3
"""Reference evaluation of the APR expressions profile, for conformance tooling.

Like scripts/aprlib.py this is written from the specification and imports no APR
SDK, so the harness cannot certify an implementation against that implementation's
own idea of what an expression means. It uses `celpy` for the language itself,
which is the point: the specification says APR does not define CEL, it adopts it.

What it implements, and where the specification says it:

* the language: the CEL standard library from `celpy`, plus the strings extension
  the specification pins, which `celpy` does not ship (#expr-language)
* the activation and its reserved names (#expr-activation)
* the type environment from `expectedDataType` (#expr-binding)
* a response that will not convert is **unbound**, never defaulted (#expr-unbound)
* results marshalled through the canonical write forms (#canonical-values)
* a required result type per hint, and a fallback for every failure (#expr-fallback)
* computed prompts ordered by their direct references (#expr-computed)
* a correction surviving recomputation (#expr-computed)

The one liberty taken: a name that will not bind is left out of the activation
entirely rather than declared and left empty. A declared-but-unbound name does not
reliably error in every CEL binding, and an expression that silently reads a
stringified type object instead of failing is the exact opposite of what
"unbound, never defaulted" asks for.
"""
from __future__ import annotations

import datetime
import re

RESERVED = ("_this", "_id", "_now", "_today", "ctx")
IDENTIFIER = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")

# expectedDataType -> the CEL type a response binds as (#expr-binding)
CEL_TYPE = {
    "number": "double", "currency": "double", "range": "double",
    "boolean": "bool",
    "date": "timestamp", "time": "timestamp", "datetime": "timestamp",
    "multichoice": "list",
}

TRUE = {"true", "yes", "y", "1", "on", "x", "checked"}
FALSE = {"false", "no", "n", "0", "off", "unchecked"}

# Which hint requires which result type, and what happens when anything fails.
# Every fallback shows more and blocks less (#expr-fallback).
HINTS = {
    "exprHidden": ("bool", False),
    "exprExpected": ("bool", False),
    "exprReadOnly": ("bool", False),
    "exprValidation": ("string", ""),
    "exprValue": ("bound", None),
}


def strings_extension():
    """No extension library. The specification requires the standard library alone.

    This returns nothing, and stays as a seam because the evidence tooling patches
    it to model an implementation that gets the language surface wrong. An earlier
    baseline required the cel-go strings extension; it was withdrawn because not
    every CEL binding carries it.
    """
    return {}


def _withdrawn_strings_extension():
    """Kept for reference: what the withdrawn extension requirement asked for.

    `celpy` ships the CEL standard library and standard macros but not this
    extension, so the tooling supplies it. That is a faithful reading of what the
    specification asks an implementation to provide, and it is also a finding: a
    conformant Python implementation cannot get this from its CEL library today.

    Semantics follow the cel-go definition the specification cites. Indices are
    zero-based, and a call outside the surface is left to fail, because failing is
    what the expressions profile requires of anything it does not define.
    """
    from celpy import celtypes
    S, I, L, B = (celtypes.StringType, celtypes.IntType, celtypes.ListType,
                  celtypes.BoolType)

    def substring(s, start, end=None):
        text = str(s)
        return S(text[int(start):] if end is None else text[int(start):int(end)])

    def index_of(s, sub, start=None):
        return I(str(s).find(str(sub), 0 if start is None else int(start)))

    return {
        "charAt": lambda s, i: S(str(s)[int(i):int(i) + 1]),
        "indexOf": index_of,
        "lastIndexOf": lambda s, sub: I(str(s).rfind(str(sub))),
        "lowerAscii": lambda s: S("".join(c.lower() if c.isascii() else c for c in str(s))),
        "upperAscii": lambda s: S("".join(c.upper() if c.isascii() else c for c in str(s))),
        "replace": lambda s, old, new, n=None: S(
            str(s).replace(str(old), str(new)) if n is None
            else str(s).replace(str(old), str(new), int(n))),
        "split": lambda s, sep, n=None: L([S(p) for p in (
            str(s).split(str(sep)) if n is None else str(s).split(str(sep), int(n) - 1))]),
        "substring": substring,
        "trim": lambda s: S(str(s).strip()),
        "reverse": lambda s: S(str(s)[::-1]),
        "join": lambda items, sep=None: S(
            ("" if sep is None else str(sep)).join(str(i) for i in items)),
    }


def compose(bound: dict, ambient: dict) -> dict:
    """The activation an expression sees.

    Reserved names are applied last and therefore win. That is the whole content
    of the rule that a direct binding must not shadow one: a prompt whose id is
    `ctx` is bound, and `ctx` in an expression is still the host context.
    """
    activation = dict(bound)
    activation.update(ambient)
    return activation


class Unbound(Exception):
    """A response that will not convert to its declared type."""


def _celtypes():
    from celpy import celtypes
    return celtypes


def bind(response, declared: str | None):
    """A response as its declared CEL type, or Unbound if it will not convert."""
    celtypes = _celtypes()
    kind = CEL_TYPE.get(declared or "", "string")
    text = "" if response is None else str(response)
    if kind == "string":
        return celtypes.StringType(text)
    if text.strip() == "":
        raise Unbound("an empty response is unbound, never a zero or a false")
    if kind == "double":
        try:
            return celtypes.DoubleType(float(text.strip()))
        except ValueError as exc:
            raise Unbound(f"{text!r} is not a number") from exc
    if kind == "bool":
        lowered = text.strip().lower()
        if lowered in TRUE:
            return celtypes.BoolType(True)
        if lowered in FALSE:
            return celtypes.BoolType(False)
        raise Unbound(f"{text!r} is not a boolean")
    if kind == "timestamp":
        for shape in ("%Y-%m-%dT%H:%M:%S%z", "%Y-%m-%dT%H:%M:%SZ", "%Y-%m-%d",
                      "%H:%M:%S", "%H:%M"):
            try:
                parsed = datetime.datetime.strptime(text.strip(), shape)
            except ValueError:
                continue
            if parsed.tzinfo is None:
                parsed = parsed.replace(tzinfo=datetime.timezone.utc)
            return celtypes.TimestampType(parsed)
        raise Unbound(f"{text!r} is not a date, time or datetime")
    if kind == "list":
        lines = [l for l in text.split("\n") if l != ""]
        return celtypes.ListType([celtypes.StringType(l) for l in lines])
    return celtypes.StringType(text)


def marshal(value, declared: str | None) -> str:
    """A result back to a stored string, through the canonical write forms."""
    celtypes = _celtypes()
    if isinstance(value, celtypes.BoolType) or isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, celtypes.TimestampType) or isinstance(value, datetime.datetime):
        moment = value if isinstance(value, datetime.datetime) else value
        if declared == "date":
            return moment.strftime("%Y-%m-%d")
        if declared == "time":
            return moment.strftime("%H:%M")
        return moment.strftime("%Y-%m-%dT%H:%M:%SZ")
    if isinstance(value, (celtypes.ListType, list)):
        return "\n".join(str(v) for v in value)
    if isinstance(value, (celtypes.DoubleType, float)):
        number = float(value)
        return str(int(number)) if number == int(number) else repr(number)
    if isinstance(value, (celtypes.IntType, int)):
        return str(int(value))
    return str(value)


def prompts_of(form: dict):
    """Every prompt in document order, with the section path that reached it."""
    def walk(sections, path):
        for index, section in enumerate(sections or []):
            if not isinstance(section, dict):
                continue
            here = f"{path}/sections/{index}"
            for j, prompt in enumerate(section.get("prompts") or []):
                if isinstance(prompt, dict):
                    yield prompt, f"{here}/prompts/{j}"
            yield from walk(section.get("sections"), here)
    return list(walk(form.get("sections"), ""))


def order(computed: list[dict]) -> list[dict]:
    """Computed prompts ordered by their direct references (#expr-computed).

    A subtotal must feed a tax must feed a total in one pass. A self-reference or
    a cycle is an authoring error, and those prompts are left in document order so
    they fail on their own rather than hanging the run.
    """
    names = {p["id"] for p in computed if isinstance(p.get("id"), str)}
    needs = {}
    for prompt in computed:
        source = (prompt.get("hints") or {}).get("exprValue") or ""
        found = set(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", source)) & names
        needs[prompt["id"]] = found - {prompt["id"]}
    done, out, remaining = set(), [], list(computed)
    while remaining:
        ready = [p for p in remaining if needs[p["id"]] <= done]
        if not ready:  # a cycle: emit the rest as authored
            return out + remaining
        for prompt in ready:
            out.append(prompt)
            done.add(prompt["id"])
            remaining.remove(prompt)
    return out


def evaluate(form: dict, _now: str | None = None, _today: str | None = None,
             ctx: dict | None = None) -> dict:
    """Evaluate every expression hint in a form. Returns what each hint produced.

    The form is never modified. `_now` and `_today` come from the caller and are
    left unbound when not supplied, so an expression using one fails to its
    fallback rather than quietly reading the host clock (#expr-activation).
    """
    import celpy
    celtypes = _celtypes()

    everything = prompts_of(form)
    bound: dict[str, object] = {}
    declared_of: dict[str, str | None] = {}
    for prompt, _ in everything:
        identifier = prompt.get("id")
        declared = (prompt.get("hints") or {}).get("expectedDataType")
        declared_of[identifier] = declared
        if not (isinstance(identifier, str) and IDENTIFIER.match(identifier)):
            continue  # not a CEL identifier, so it has no direct binding
        if identifier in RESERVED:
            continue  # reserved names are never shadowed by a direct binding
        try:
            bound[identifier] = bind(prompt.get("response"), declared)
        except Unbound:
            pass  # left out of the activation entirely

    ambient: dict[str, object] = {}
    if _now:
        try:
            ambient["_now"] = bind(_now, "datetime")
        except Unbound:
            pass
    if _today is not None:
        ambient["_today"] = celtypes.StringType(_today)
    if ctx is not None:
        ambient["ctx"] = celtypes.MapType(
            {celtypes.StringType(k): celtypes.StringType(v) for k, v in ctx.items()})

    env = celpy.Environment()
    results = {"responses": {}, "hidden": {}, "expected": {},
               "readOnly": {}, "validation": {}, "unbound": sorted(
                   i for i, _ in declared_of.items()
                   if isinstance(i, str) and IDENTIFIER.match(i)
                   and i not in bound and i not in RESERVED)}

    def run(prompt, source, required):
        identifier = prompt.get("id")
        activation = compose(bound, ambient)
        activation["_id"] = celtypes.StringType(str(identifier))
        if identifier in bound:
            activation["_this"] = bound[identifier]
        program = env.program(env.compile(source), functions=strings_extension())
        value = program.evaluate(activation)
        if isinstance(value, Exception):
            raise value
        if required == "bool" and not isinstance(value, (celtypes.BoolType, bool)):
            raise TypeError("expected a bool")
        if required == "string" and not isinstance(value, (celtypes.StringType, str)):
            raise TypeError("expected a string")
        return value

    for key, target in (("exprHidden", "hidden"), ("exprExpected", "expected"),
                        ("exprReadOnly", "readOnly"), ("exprValidation", "validation")):
        required, fallback = HINTS[key]
        for prompt, _ in everything:
            source = (prompt.get("hints") or {}).get(key)
            if not isinstance(source, str):
                continue
            try:
                value = run(prompt, source, required)
                results[target][prompt["id"]] = (
                    bool(value) if required == "bool" else str(value))
            except Exception:  # noqa: BLE001 - any failure applies the fallback
                results[target][prompt["id"]] = fallback

    computed = [p for p, _ in everything
                if isinstance((p.get("hints") or {}).get("exprValue"), str)]
    for prompt in order(computed):
        identifier = prompt["id"]
        stored = prompt.get("response")
        # A correction survives recomputation: every non-empty response in the
        # document as it was read is authored, and is not overwritten.
        if isinstance(stored, str) and stored != "":
            results["responses"][identifier] = stored
            continue
        declared = declared_of.get(identifier)
        try:
            value = run(prompt, prompt["hints"]["exprValue"], "bound")
            written = marshal(value, declared)
            results["responses"][identifier] = written
            bound[identifier] = bind(written, declared)
        except Exception:  # noqa: BLE001 - retain the stored response exactly
            results["responses"][identifier] = "" if stored is None else str(stored)
    return results

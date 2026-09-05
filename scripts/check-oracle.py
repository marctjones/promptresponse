#!/usr/bin/env python3
"""Test the oracle the whole conformance suite is scored against.

`scripts/aprlib.py` computes every digest in the corpus and `scripts/aprexpr.py`
computes every expected evaluation. Nothing checked either against anything
outside this repository, so a subtly wrong canonicalizer would be baked into the
corpus and four SDKs would then be judged wrong for being right.

The vectors here are not ours. They are RFC 8785's own published test data: the
worked example from section 3.2, the property-ordering set from 3.2.3, and the
IEEE 754 serialization pairs from Appendix B. Number formatting is where a JCS
implementation usually goes wrong, because ECMAScript's shortest-round-trip rule
is not what most languages print by default.

    python3 scripts/check-oracle.py
    python3 scripts/check-oracle.py --verbose
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import struct
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

# RFC 8785 section 3.2, written with escapes so this file carries no literal
# control characters of its own.
EXAMPLE_INPUT = (
    '{\n'
    '  "numbers": [333333333.33333329, 1E30, 4.50, 2e-3, '
    '0.000000000000000000000000001],\n'
    '  "string": "\\u20ac$\\u000F\\u000aA\'\\u0042\\u0022\\u005c\\\\\\"\\/",\n'
    '  "literals": [null, true, false]\n'
    '}'
)
EXAMPLE_CANONICAL = (
    '{"literals":[null,true,false],'
    '"numbers":[333333333.3333333,1e+30,4.5,0.002,1e-27],'
    '"string":"€$\\u000f\\nA\'B\\"\\\\\\\\\\"/"}'
)

# RFC 8785 section 3.2.3. Members sort by UTF-16 code unit, not code point, which
# shows up only outside the Basic Multilingual Plane: the emoji is a surrogate
# pair and sorts before U+FB33 even though its code point is far higher. An
# implementation sorting by code point passes every other vector and fails this.
ORDERING_INPUT = {
    "€": "Euro Sign",
    "\r": "Carriage Return",
    "דּ": "Hebrew Letter Dalet With Dagesh",
    "1": "One",
    "\U0001f600": "Emoji: Grinning Face",
    "": "Control",
    "ö": "Latin Small Letter O With Diaeresis",
}
ORDERING_EXPECTED = ["\r", "1", "", "ö", "€", "\U0001f600", "דּ"]

# RFC 8785 Appendix B: IEEE 754 bit pattern -> the string JCS must produce.
NUMBERS = [
    ("0000000000000000", "0"),
    ("8000000000000000", "0"),
    ("0000000000000001", "5e-324"),
    ("8000000000000001", "-5e-324"),
    ("7fefffffffffffff", "1.7976931348623157e+308"),
    ("ffefffffffffffff", "-1.7976931348623157e+308"),
    ("4340000000000000", "9007199254740992"),
    ("c340000000000000", "-9007199254740992"),
    ("4430000000000000", "295147905179352830000"),
    ("44b52d02c7e14af5", "9.999999999999997e+22"),
    ("44b52d02c7e14af6", "1e+23"),
    ("44b52d02c7e14af7", "1.0000000000000001e+23"),
    ("444b1ae4d6e2ef4e", "999999999999999700000"),
    ("444b1ae4d6e2ef4f", "999999999999999900000"),
    ("444b1ae4d6e2ef50", "1e+21"),
    ("3eb0c6f7a0b5ed8c", "9.999999999999997e-7"),
    ("3eb0c6f7a0b5ed8d", "0.000001"),
    ("41b3de4355555553", "333333333.3333332"),
    ("41b3de4355555554", "333333333.33333325"),
    ("41b3de4355555555", "333333333.3333333"),
    ("41b3de4355555556", "333333333.3333334"),
    ("41b3de4355555557", "333333333.33333343"),
    ("becbf647612f3696", "-0.0000033333333333333333"),
    ("43143ff3c1cb0959", "1424953923781206.2"),
]


def main() -> int:
    verbose = "--verbose" in sys.argv
    problems: list[str] = []
    checked = 0

    produced = aprlib.canonicalize(json.loads(EXAMPLE_INPUT))
    checked += 1
    if produced != EXAMPLE_CANONICAL:
        problems.append(
            "the section 3.2 example does not canonicalize to the published form:\n"
            f"      expected {EXAMPLE_CANONICAL}\n"
            f"      produced {produced}")
    elif verbose:
        print(f"  section 3.2 example: {produced}")

    order = json.loads(aprlib.canonicalize(ORDERING_INPUT),
                       object_pairs_hook=lambda pairs: [key for key, _ in pairs])
    checked += 1
    if order != ORDERING_EXPECTED:
        problems.append(
            "member ordering is not UTF-16 code-unit order:\n"
            f"      expected {[hex(ord(key[0])) for key in ORDERING_EXPECTED]}\n"
            f"      produced {[hex(ord(key[0])) for key in order]}")
    elif verbose:
        print(f"  ordering: {[hex(ord(key[0])) for key in order]}")

    for bits, expected in NUMBERS:
        value = struct.unpack(">d", bytes.fromhex(bits))[0]
        produced = aprlib.canonicalize(value)
        checked += 1
        if produced != expected:
            problems.append(f"IEEE 754 {bits}: expected {expected}, produced {produced}")
        elif verbose:
            print(f"  {bits} -> {produced}")

    # A digest is SHA-256 over those bytes, so proving the bytes nearly proves the
    # digest. Assert the composition anyway: it is the one line every attestation
    # in the corpus rests on, and it is cheap to state.
    expected_digest = "sha256:" + hashlib.sha256(
        EXAMPLE_CANONICAL.encode("utf-8")).hexdigest()
    produced_digest = aprlib.digest(json.loads(EXAMPLE_INPUT))
    checked += 1
    if produced_digest != expected_digest:
        problems.append(
            "a digest is not sha256: over the canonical UTF-8 bytes:\n"
            f"      expected {expected_digest}\n"
            f"      produced {produced_digest}")

    print(f"Oracle checked against RFC 8785's own vectors: {checked} assertions")
    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        print("\nThe corpus is derived from this canonicalizer, so a defect here is in "
              "every digest\nthe suite states. Fix it before regenerating anything.")
        return 1
    print("Canonicalization, member ordering, number serialization and digest "
          "composition\nagree with the published vectors.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except aprlib.MissingDependency as missing:
        print(f"cannot run: {missing}", file=sys.stderr)
        sys.exit(2)

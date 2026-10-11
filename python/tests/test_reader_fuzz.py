"""The reader refuses hostile input with AprParseError and nothing else.

For any input the reader either returns records or raises AprParseError, and it terminates
promptly. An exception of any other type reaching the caller is a defect: a caller told the
format is safe to open cannot defend against what it was never told about. Seeded from the
specification's executable examples in every representation, with fixed seeds so a failure
reproduces; the same corpus and mutations the other SDKs' suites use.
"""

import json
import pathlib
import random
import time

import pytest

from promptresponse.beta6 import read_beta6_stream
from promptresponse.errors import AprParseError

EXAMPLES = pathlib.Path(__file__).resolve().parents[2] / "tests" / "Conformance" / "beta6" / "spec-examples.json"
POISON = "\"{}[],:\\/*\x1e\x00\ud800￿&!%@`#|>-?<'\n\t "
BUDGET_SECONDS = 5


def _seeds():
    return json.loads(EXAMPLES.read_text(encoding="utf-8"))["examples"]


def _mutations(document: str, rng: random.Random):
    for length in range(0, len(document), max(1, len(document) // 80)):
        yield f"truncated to {length}", document[:length]
    for _ in range(80):
        at = rng.randrange(len(document))
        char = rng.choice(POISON)
        yield f"char {at} replaced by U+{ord(char):04X}", document[:at] + char + document[at + 1:]
    for _ in range(40):
        start = rng.randrange(len(document))
        span = rng.randrange(1, max(2, len(document) // 4))
        yield f"{span} chars deleted at {start}", document[:start] + document[start + span:]
        yield "a stray record separator", document[:rng.randrange(len(document))] + "\x1e" + document[rng.randrange(len(document)):]


def test_hostile_input_is_refused_cleanly_and_promptly():
    rng = random.Random(20261010)
    defects: dict[str, str] = {}
    for example in _seeds():
        representation = "yaml" if example["representation"].startswith("yaml") else "jsonc"
        for how, mutated in _mutations(example["document"], rng):
            started = time.monotonic()
            try:
                read_beta6_stream(mutated, representation)
            except AprParseError:
                pass  # the designed outcome for bad input
            except BaseException as leaked:  # noqa: BLE001 - any other type is the defect
                defects.setdefault(f"{type(leaked).__name__}: {leaked}", f"{example['id']} ({how})")
            if time.monotonic() - started > BUDGET_SECONDS:
                defects.setdefault("took too long: a document that hangs a reader is a denial of service", example["id"])
    assert not defects, f"the reader must refuse bad input with AprParseError and nothing else: {defects}"

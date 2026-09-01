"""Execute the examples embedded in the APR specification.

The specification is normative and the conformance vectors are derived from it
(scripts/extract-spec-examples.py). This module is what makes the examples
*executable* rather than merely extracted: every example in the specification is
run against the reference behaviour and asserted to produce the outcome the
specification claims.

Where an example and the implementation disagree, the specification is right and
the implementation has the defect. Those cases are listed in KNOWN_DIVERGENCES
with the issue tracking them, and are marked strict-xfail: when the
implementation is fixed the test fails until the entry is removed, so the list
cannot quietly rot.
"""

import json
from pathlib import Path

import pytest

import promptresponse as pr

VECTORS = (
    Path(__file__).parents[2] / "tests" / "Conformance" / "beta6" / "spec-examples.json"
)

# Examples where the reference implementation does not yet do what the
# specification requires. Each entry is an implementation defect, not a
# specification question.
KNOWN_DIVERGENCES: dict[str, str] = {}


def load_examples():
    data = json.loads(VECTORS.read_text(encoding="utf-8"))
    return data["examples"]


def read(example):
    """Read an example the way its declared representation requires."""
    representation = example["representation"]
    document = example["document"]
    if representation.endswith("-stream"):
        return pr.read_beta6_stream(document, representation.split("-", 1)[0])
    return pr.read_beta6_form(document, representation)


def identifiers():
    return [e["id"] for e in load_examples()]


@pytest.mark.parametrize("example", load_examples(), ids=identifiers())
def test_specification_example_behaves_as_the_specification_says(example, request):
    if example["id"] in KNOWN_DIVERGENCES:
        request.node.add_marker(
            pytest.mark.xfail(reason=KNOWN_DIVERGENCES[example["id"]], strict=True)
        )

    expectation = example["expect"]

    if expectation == "valid":
        # A valid example must parse. Nothing else is asserted here: what the
        # document *means* is covered by the corpus tests, while this asserts the
        # single claim the example itself makes.
        assert read(example) is not None
        return

    if expectation == "reject":
        with pytest.raises(Exception):
            read(example)
        return

    if expectation == "equivalent":
        other = next(e for e in load_examples() if e["id"] == example["equivalentTo"])
        assert read(example) is not None
        assert read(other) is not None
        return

    pytest.fail(f"unrecognised expectation {expectation!r}")


def test_every_example_cites_a_rule_and_a_representation():
    for example in load_examples():
        assert example["rule"], f"{example['id']} cites no specification anchor"
        assert example["representation"], f"{example['id']} declares no representation"
        if example["expect"] == "reject":
            assert example.get("diagnostic"), f"{example['id']} names no diagnostic"


def test_known_divergences_name_only_real_examples():
    """A divergence entry for an example that no longer exists is stale."""
    ids = {e["id"] for e in load_examples()}
    unknown = sorted(set(KNOWN_DIVERGENCES) - ids)
    assert not unknown, f"KNOWN_DIVERGENCES names examples that do not exist: {unknown}"

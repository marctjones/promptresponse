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
import re
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


def framed(document: str) -> str:
    """Restore RFC 7464 framing to an APR-JSONC stream printed with ``---``.

    A record separator is invisible on a page, so the specification stands one in
    with a ``---`` line. Handing that prose form straight to the reader tests the
    reader against a document the format never defines. APR-YAML streams are
    untouched: there ``---`` is genuinely the separator, not a stand-in for one.

    Mirrors ``Framed()`` in
    tests/PromptResponse.Core.Tests/Beta6/SpecExampleTests.cs.
    """
    parts = [p for p in re.split(r"(?m)^---$", document) if p.strip()]
    return "".join("\x1e" + p.strip("\n") + "\n" for p in parts)


def read(example):
    """Read an example the way its declared representation requires."""
    representation = example["representation"]
    document = example["document"]
    if representation.endswith("-stream"):
        kind = representation.split("-", 1)[0]
        return pr.read_beta6_stream(framed(document) if kind == "jsonc" else document, kind)
    return pr.read_beta6_form(document, representation)


def rejection_codes(example) -> tuple[bool, list[str]]:
    """Whether the example is refused, and the codes the refusal reported.

    A prompt without a label parses and is a validation error, a malformed document
    fails to parse, and the specification asks for rejection either way, as the
    conformance driver reports it. The code has to be the one the example names, or a
    reader refusing for an unrelated reason would pass.
    """
    try:
        result = read(example)
    except Exception as error:
        code = getattr(error, "code", None)
        return True, [code] if code else []
    records = result if isinstance(result, list) else [result]
    documents = [getattr(r, "document", r) for r in records
                 if not isinstance(r, pr.beta6.Beta6Record) or isinstance(r, pr.beta6.Beta6FormRecord)]
    # A read that yields no form holds no document, which is refused too (NULL_DOCUMENT).
    if not documents:
        return True, ["NULL_DOCUMENT"]
    errors = [error for document in documents for error in pr.validate(document).errors]
    return bool(errors), [error.code for error in errors]


def identifiers():
    return [e["id"] for e in load_examples()]


@pytest.mark.parametrize("example", load_examples(), ids=identifiers())
def test_specification_example_behaves_as_the_specification_says(example, request):
    if example["id"] in KNOWN_DIVERGENCES:
        request.node.add_marker(
            pytest.mark.xfail(reason=KNOWN_DIVERGENCES[example["id"]], strict=True)
        )

    document = example["document"]
    if not example["representation"].endswith("-stream") and ('"recordType"' in document or "recordType:" in document):
        # An attestation record belongs to core+attestations, which this SDK does not
        # claim. The conformance runner leaves such a case unanswered, and so does this.
        pytest.skip("core+attestations is not claimed")

    expectation = example["expect"]

    if expectation == "valid":
        # A valid example must parse. Nothing else is asserted here: what the
        # document *means* is covered by the corpus tests, while this asserts the
        # single claim the example itself makes.
        assert read(example) is not None
        return

    if expectation == "reject":
        refused, codes = rejection_codes(example)
        assert refused, f"{example['id']} was accepted; the specification requires rejection"
        assert example["diagnostic"] in codes, (
            f"{example['id']} was refused with {codes or 'no code'}; "
            f"the specification names {example['diagnostic']}")
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


def test_the_rejection_examples_name_more_than_one_diagnostic():
    """A reader refusing every rejection example with one code passes only if every example names it."""
    named = {e["diagnostic"] for e in load_examples() if e["expect"] == "reject"}
    assert len(named) > 1, f"every rejection example names {named}"

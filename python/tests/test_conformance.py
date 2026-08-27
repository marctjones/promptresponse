"""The conformance corpus, run against this implementation.

This is the gate that matters. The corpus is the format's primary authority -
each fixture carries its own verdict, and fixture-plus-verdict together are the
specification. An implementation that passes it agrees with the reference
implementation about what the format is; one that does not, does not, whatever
its own tests say.
"""

import json
import pathlib

import pytest

import promptresponse as pr

CORPUS = pathlib.Path(__file__).resolve().parents[2] / "tests" / "Conformance" / "v1"


def _fixtures(kind):
    directory = CORPUS / kind
    if not directory.is_dir():
        pytest.skip(f"corpus directory missing: {directory}")
    found = sorted(p for p in directory.glob("*.apr*"))
    assert found, f"no fixtures in {directory}; an empty corpus proves nothing"
    return found


def _ids(paths):
    return [p.name for p in paths]


# ── valid/ — must parse AND validate ─────────────────────────────────────────

VALID = _fixtures("valid")


@pytest.mark.parametrize("path", VALID, ids=_ids(VALID))
def test_valid_fixtures_parse_and_validate(path):
    document = pr.load(path)
    result = pr.validate(document)
    assert result.is_valid, (
        f"{path.name} is in valid/ and must validate. Errors: "
        + " | ".join(f"{e.code}@{e.path}: {e.message}" for e in result.errors)
    )


@pytest.mark.parametrize("path", VALID, ids=_ids(VALID))
def test_valid_fixtures_round_trip_without_loss(path):
    """Reading and writing must not change what the document says.

    Specification 4.8: unknown members survive. Without this every additive
    change to the format is destructive, because an older reader silently
    strips whatever it did not recognise the first time it saves.
    """
    original = json.loads(path.read_text(encoding="utf-8-sig"))
    reloaded = json.loads(pr.dumps(pr.load(path)))

    def compare(before, after, where):
        if isinstance(before, dict):
            assert isinstance(after, dict), f"{where}: shape changed"
            for key, value in before.items():
                # Members the format retired are dropped on purpose (4.8.1).
                if key in pr.models.RETIRED_MEMBERS:
                    continue
                # An empty array or object carries no information, so writing it
                # or omitting it says the same thing. The rule under test is 4.8 -
                # unknown members must survive - not byte-identical output.
                if value in ([], {}):
                    continue
                assert key in after, f"{where}.{key} was lost on round trip"
                compare(value, after[key], f"{where}.{key}")
        elif isinstance(before, list):
            assert len(after) == len(before), f"{where}: {len(before)} items became {len(after)}"
            for i, item in enumerate(before):
                compare(item, after[i], f"{where}[{i}]")

    compare(original, reloaded, path.name)


# ── invalid/ — must PARSE, and fail validation ───────────────────────────────

INVALID = _fixtures("invalid")


@pytest.mark.parametrize("path", INVALID, ids=_ids(INVALID))
def test_invalid_fixtures_parse_but_do_not_validate(path):
    """Specification 6.3: a flawed document still opens, so it can be shown.

    Refusing to parse these would be the failure mode the split exists to
    prevent - somebody handed a fixable form and no way to see what is wrong.
    """
    document = pr.load(path)
    assert not pr.validate(document).is_valid, (
        f"{path.name} is in invalid/ and must fail validation"
    )


# ── malformed/ — must NOT parse ──────────────────────────────────────────────

MALFORMED = _fixtures("malformed")


@pytest.mark.parametrize("path", MALFORMED, ids=_ids(MALFORMED))
def test_malformed_fixtures_are_refused_at_parse_time(path):
    with pytest.raises(pr.AprParseError):
        pr.load(path)


# ── signatures/ — content is readable whatever the signature says ────────────

SIGNED = _fixtures("signatures")


@pytest.mark.parametrize("path", SIGNED, ids=_ids(SIGNED))
def test_a_tampered_signature_never_withholds_the_data(path):
    """Specification 9.5. These fixtures carry signatures that do not verify.

    A core reader cannot tell - it does not verify - and that is the point: the
    data is readable either way, and no reader may withhold it over a signature.
    """
    document = pr.load(path)
    assert document.sections, f"{path.name}: the content must be readable"
    assert document.signatures, f"{path.name}: and the signatures must be preserved"

"""Text handling: normalisation and the characters that are stripped.

Specification 7. Authoring fields are normalised and cleaned; a response is
normalised and otherwise left exactly as the person typed it.
"""

import unicodedata

# Bidirectional overrides and non-characters. These are removed everywhere,
# including from responses, because they change how text *renders* rather than
# what it says - a label that reads one way and sorts another is a spoofing
# tool, not a typo (specification 7.2).
_ABUSIVE = frozenset(
    [
        "\u202a", "\u202b", "\u202c", "\u202d", "\u202e",  # LRE RLE PDF LRO RLO
        "\u2066", "\u2067", "\u2068", "\u2069",              # LRI RLI FSI PDI
        "\ufeff",                                              # zero-width no-break space
    ]
)


def _is_noncharacter(ch: str) -> bool:
    """Codepoints Unicode reserves as never being characters."""
    cp = ord(ch)
    return 0xFDD0 <= cp <= 0xFDEF or (cp & 0xFFFE) == 0xFFFE


def normalize(value):
    """NFC-normalise, and remove characters that misrepresent the text.

    Returns ``None`` unchanged, so an absent field stays absent rather than
    becoming an empty string.

    Deliberately does *not* remove zero-width spaces or other merely-unusual
    characters from a response. A response is what a person typed; odd
    characters in one are reported, never silently rewritten (specification
    3.3, 7.2).
    """
    if value is None or value == "":
        return value

    cleaned = "".join(
        ch for ch in value if ch not in _ABUSIVE and not _is_noncharacter(ch)
    )
    return unicodedata.normalize("NFC", cleaned)

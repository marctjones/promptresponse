"""Non-destructive Unicode safety inspection for APR response text.

APR stores responses exactly as supplied. This module reports characters that
can be invisible or alter visual ordering; it never changes or rejects text.
"""

from dataclasses import dataclass
from typing import List


@dataclass(frozen=True)
class UnicodeFinding:
    """One suspicious code point, at a Python string offset."""

    offset: int
    codepoint: int
    code: str
    description: str


def inspect_text(value: str) -> List[UnicodeFinding]:
    """Return advisory findings without modifying *value*."""
    findings: List[UnicodeFinding] = []
    for offset, char in enumerate(value or ""):
        point = ord(char)
        code = description = None
        if point == 0x200B:
            code, description = "HIDDEN_ZWSP", "zero-width space U+200B"
        elif point == 0x200C:
            code, description = "HIDDEN_ZWNJ", "zero-width non-joiner U+200C"
        elif point == 0x200D:
            code, description = "HIDDEN_ZWJ", "zero-width joiner U+200D"
        elif point in (0x200E, 0x200F):
            code, description = "HIDDEN_BIDI_MARK", f"bidirectional mark U+{point:04X}"
        elif point == 0x00AD:
            code, description = "HIDDEN_SOFT_HYPHEN", "soft hyphen U+00AD"
        elif point == 0x2060:
            code, description = "HIDDEN_WORD_JOINER", "word joiner U+2060"
        elif 0x202A <= point <= 0x202E:
            code, description = "BIDI_OVERRIDE", f"bidirectional override U+{point:04X}"
        elif 0x2066 <= point <= 0x2069:
            code, description = "BIDI_ISOLATE", f"bidirectional isolate U+{point:04X}"
        elif point == 0xFEFF:
            code, description = "TEXT_BOM", "byte-order mark U+FEFF inside text"
        elif point in (0xFFFE, 0xFFFF):
            code, description = "NONCHARACTER", f"Unicode noncharacter U+{point:04X}"
        elif point <= 0x08 or point in (0x0B, 0x0C) or 0x0E <= point <= 0x1F or 0x7F <= point <= 0x9F:
            code, description = "CONTROL_CHARACTER", f"control character U+{point:04X}"
        elif 0x2061 <= point <= 0x2064:
            code, description = "HIDDEN_INVISIBLE_OPERATOR", f"invisible math operator U+{point:04X}"
        elif 0xFE00 <= point <= 0xFE0F or 0xE0100 <= point <= 0xE01EF:
            code, description = "HIDDEN_VARIATION_SELECTOR", f"variation selector U+{point:04X}"
        if code:
            findings.append(UnicodeFinding(offset, point, code, description))
    return findings

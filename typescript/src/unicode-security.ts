/** Non-destructive Unicode safety inspection for exact APR response strings. */
export interface UnicodeFinding {
  /** JavaScript UTF-16 string offset of the code point. */
  offset: number;
  codepoint: number;
  code: string;
  description: string;
}

/** Reports visually deceptive or invisible characters; it never changes text. */
export function inspectText(value: string): UnicodeFinding[] {
  const findings: UnicodeFinding[] = [];
  for (let offset = 0; offset < value.length;) {
    const codepoint = value.codePointAt(offset)!;
    const hex = `U+${codepoint.toString(16).toUpperCase().padStart(4, "0")}`;
    let code: string | undefined;
    let description: string | undefined;
    if (codepoint === 0x200B) [code, description] = ["HIDDEN_ZWSP", "zero-width space U+200B"];
    else if (codepoint === 0x200C) [code, description] = ["HIDDEN_ZWNJ", "zero-width non-joiner U+200C"];
    else if (codepoint === 0x200D) [code, description] = ["HIDDEN_ZWJ", "zero-width joiner U+200D"];
    else if (codepoint === 0x200E || codepoint === 0x200F) [code, description] = ["HIDDEN_BIDI_MARK", `bidirectional mark ${hex}`];
    else if (codepoint === 0x00AD) [code, description] = ["HIDDEN_SOFT_HYPHEN", "soft hyphen U+00AD"];
    else if (codepoint === 0x2060) [code, description] = ["HIDDEN_WORD_JOINER", "word joiner U+2060"];
    else if (codepoint >= 0x202A && codepoint <= 0x202E) [code, description] = ["BIDI_OVERRIDE", `bidirectional override ${hex}`];
    else if (codepoint >= 0x2066 && codepoint <= 0x2069) [code, description] = ["BIDI_ISOLATE", `bidirectional isolate ${hex}`];
    else if (codepoint === 0xFEFF) [code, description] = ["TEXT_BOM", "byte-order mark U+FEFF inside text"];
    else if (codepoint === 0xFFFE || codepoint === 0xFFFF) [code, description] = ["NONCHARACTER", `Unicode noncharacter ${hex}`];
    else if (codepoint <= 0x08 || codepoint === 0x0B || codepoint === 0x0C || (codepoint >= 0x0E && codepoint <= 0x1F) || (codepoint >= 0x7F && codepoint <= 0x9F)) [code, description] = ["CONTROL_CHARACTER", `control character ${hex}`];
    else if (codepoint >= 0x2061 && codepoint <= 0x2064) [code, description] = ["HIDDEN_INVISIBLE_OPERATOR", `invisible math operator ${hex}`];
    else if ((codepoint >= 0xFE00 && codepoint <= 0xFE0F) || (codepoint >= 0xE0100 && codepoint <= 0xE01EF)) [code, description] = ["HIDDEN_VARIATION_SELECTOR", `variation selector ${hex}`];
    if (code && description) findings.push({ offset, codepoint, code, description });
    offset += codepoint > 0xFFFF ? 2 : 1;
  }
  return findings;
}

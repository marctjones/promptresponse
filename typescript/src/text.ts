const ABUSIVE = new Set(["\u202a", "\u202b", "\u202c", "\u202d", "\u202e", "\u2066", "\u2067", "\u2068", "\u2069", "\ufeff"]);

function isNoncharacter(char: string): boolean {
  const code = char.codePointAt(0)!;
  return (code >= 0xfdd0 && code <= 0xfdef) || (code & 0xfffe) === 0xfffe;
}

/** NFC-normalise visible text and remove bidi controls/noncharacters. */
export function normalize(value: string | undefined): string | undefined {
  if (!value) return value;
  return [...value].filter(char => !ABUSIVE.has(char) && !isNoncharacter(char)).join("").normalize("NFC");
}

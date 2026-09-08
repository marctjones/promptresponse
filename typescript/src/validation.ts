import { AprDocument, Prompt, Section } from "./model.js";
import { isSupportedVersion } from "./serialization.js";
import { inspectText } from "./unicode-security.js";

export interface ValidationIssue { code: string; message: string; path: string; }
export interface ValidationResult { errors: ValidationIssue[]; warnings: ValidationIssue[]; isValid: boolean; }

const NUMERIC_TYPES = new Set(["number", "currency", "range"]);
const TEMPORAL_TYPES = new Set(["date", "time", "datetime"]);

// Mirrors schemas/apr-types-1.0.json. A copy, not a read of that file at run
// time -- python/promptresponse/advisories.py and
// src/PromptResponse.Core/Validation/AdvisoryVocabulary.cs keep the same copy
// for the same reason.
const REGISTERED_TYPES = new Set([
  "boolean", "color", "currency", "date", "datetime", "email", "multichoice",
  "multiline", "number", "password", "phone", "range", "select", "text", "time",
  "url",
]);
const SUBMISSION_SCHEMES = new Set(["https", "mailto"]);

function* walkSections(sections: Section[], path: string): Generator<[Section, string]> {
  for (let index = 0; index < sections.length; index++) {
    const here = `${path}[${index}]`;
    yield [sections[index], here];
    yield* walkSections(sections[index].sections, `${here}.sections`);
  }
}

function isUri(value: string): boolean {
  try { return Boolean(new URL(value).protocol); } catch { return false; }
}
function isDigest(value: unknown): value is string {
  return typeof value === "string" && /^sha256:[0-9a-f]{64}$/.test(value);
}

/** Members whose value has a shape the format states, not just a type (specification 7.1). */
function validateShape(document: AprDocument, errors: ValidationIssue[]): void {
  const template = document.metadata.templateId;
  if (template && !isUri(template)) errors.push({ code: "WRONG_TYPE", message: `templateId ${JSON.stringify(template)} is not a URI.`, path: "metadata.templateId" });
  const regarding = document.metadata.extra.regarding;
  if (Array.isArray(regarding)) regarding.forEach((entry, index) => {
    if (!isDigest(entry)) errors.push({ code: "WRONG_TYPE", message: `regarding entry ${index} is not a digest.`, path: `metadata.regarding[${index}]` });
  });
  for (const [section, path] of walkSections(document.sections, "sections")) {
    if (section.maxRows !== undefined && section.maxRows < 1) errors.push({
      code: "WRONG_TYPE",
      message: `maxRows is ${section.maxRows}. A table always holds at least one instance, so a cap below one describes a table that cannot exist.`,
      path: `${path}.maxRows`,
    });
  }
}

// Below the human-facing text floor (specification 7.2): unassigned, a surrogate,
// private-use, a control other than tab and newline, or a code point UTS #39
// classifies as default-ignorable, deprecated or not-a-character.
const BELOW_FLOOR_CATEGORY = /\p{Cc}|\p{Cf}|\p{Cs}|\p{Co}|\p{Cn}/u;
const BELOW_FLOOR_RANGES: [number, number][] = [
  [0x00AD, 0x00AD], [0x061C, 0x061C], [0x180B, 0x180F], [0x200B, 0x200F],
  [0x202A, 0x202E], [0x2060, 0x206F], [0xFEFF, 0xFEFF], [0xFFF0, 0xFFF8],
  [0xFFFE, 0xFFFF], [0x1D173, 0x1D17A], [0xE0000, 0xE0FFF],
];

function belowFloor(character: string): boolean {
  const value = character.codePointAt(0)!;
  if (value === 0x09 || value === 0x0A) return false;
  if (BELOW_FLOOR_CATEGORY.test(character)) return true;
  if (BELOW_FLOOR_RANGES.some(([low, high]) => value >= low && value <= high)) return true;
  return (value & 0xFFFE) === 0xFFFE;
}

// Specification 8.2.3 places NON_NFC_TEXT and FORBIDDEN_CODE_POINT in the
// warnings table (7.2), not the errors table (7.1, stated exhaustive by
// APR-VAL-008): a validator MUST report them, but a warning MUST NOT affect
// validity or block saving (APR-VAL-006, APR-VAL-007). Reporting them as
// errors would reject a document the format requires to stay valid.
//
// 8.2.3 also requires a reader to preserve this text exactly ("a reader that
// meets one in a published form... never rewrites it") -- text.ts's
// normalize() no longer runs on these fields at parse time, so a non-NFC
// spelling or an excluded code point in the source survives to be reported
// here instead of being silently cleaned away before anyone sees it.
function holdToTheFloor(value: string | undefined, path: string, warnings: ValidationIssue[]): void {
  if (!value) return;
  if (value.normalize("NFC") !== value) warnings.push({
    code: "NON_NFC_TEXT",
    message: "Human-facing text must be in Normalization Form C; two spellings of one word are two different strings to everything that compares them.",
    path,
  });
  for (const character of value) {
    if (belowFloor(character)) {
      // Not uniformly "renders as nothing": this category also holds ZWJ and
      // ZWNJ, load-bearing for correct glyph shaping in Persian, Hindi and
      // other scripts. Say what the rule is, not a rendering claim that's
      // false for part of the set it covers.
      warnings.push({ code: "FORBIDDEN_CODE_POINT", message: `Human-facing text carries U+${character.codePointAt(0)!.toString(16).toUpperCase().padStart(4, "0")}, which the human-facing text floor excludes.`, path });
      return; // One report names the member; listing every offender adds noise.
    }
  }
}

/** A response is not human-facing text in this sense; only titles and labels are. */
function validateTextFloor(document: AprDocument, warnings: ValidationIssue[]): void {
  holdToTheFloor(document.metadata.title, "metadata.title", warnings);
  holdToTheFloor(document.metadata.description, "metadata.description", warnings);
  holdToTheFloor(document.metadata.author, "metadata.author", warnings);
  holdToTheFloor(document.metadata.publisher, "metadata.publisher", warnings);
  for (const [section, path] of walkSections(document.sections, "sections")) {
    holdToTheFloor(section.title, `${path}.title`, warnings);
    holdToTheFloor(section.description, `${path}.description`, warnings);
    section.prompts.forEach((prompt, index) => holdToTheFloor(prompt.label, `${path}.prompts[${index}].label`, warnings));
  }
}

// Specification 8.2.3/APR-TEXT-012: "SHOULD apply the confusable and
// mixed-script detection of UTS #39... report what it finds." Full UTS #39
// restriction-level analysis needs a declared document language to avoid
// flagging ordinary multi-script text (Japanese Han+Hiragana+Katakana, Korean
// Hangul+Han, Latin loanwords in Indic/Arabic/Hebrew text) -- APR has no
// metadata.language member yet, so that full analysis isn't attempted here.
//
// What doesn't need a declared language: Latin, Cyrillic and Greek have
// extensive letter-shape homoglyphs between them (Cyrillic а/Latin a, Greek
// Α/Latin A) and essentially no legitimate reason to co-occur within one
// title or label -- unlike CJK/Hangul/Indic scripts, which routinely mix with
// Latin for brand names, loanwords and numerals. Flagging only these three
// scripts mixing with each other is a narrow, script-agnostic slice of UTS #39
// that produces zero known false positives on real multi-script text.
//
// Not in specification 7.2's warnings table -- CONFUSABLE_SCRIPT_MIX is this
// implementation's own spelling of an APR-VAL-002 "MAY surface any warning,
// including conditions this table does not name" extension, not a code every
// implementation must use.
const CONFUSABLE_SCRIPTS: [string, RegExp][] = [
  ["Cyrillic", /\p{Script=Cyrillic}/u], ["Greek", /\p{Script=Greek}/u], ["Latin", /\p{Script=Latin}/u],
];

function confusableScriptOf(character: string): string | undefined {
  if (!/\p{L}/u.test(character)) return undefined; // Not a letter: digits, punctuation and spaces are script-neutral.
  for (const [script, pattern] of CONFUSABLE_SCRIPTS) if (pattern.test(character)) return script;
  return undefined;
}

function checkFieldForConfusableMix(value: string | undefined, path: string, warnings: ValidationIssue[]): void {
  if (!value) return;
  const scripts = new Set<string>();
  for (const character of value) { const script = confusableScriptOf(character); if (script) scripts.add(script); }
  if (scripts.size > 1) warnings.push({
    code: "CONFUSABLE_SCRIPT_MIX",
    message: `Mixes ${[...scripts].sort().join(", ")} letters in one field; Latin, Cyrillic and Greek share look-alike letters, and a mix within one title or label is rarely intentional.`,
    path,
  });
}

/** A response is not human-facing text in this sense; only titles and labels are. */
function checkConfusableScriptMix(document: AprDocument, warnings: ValidationIssue[]): void {
  checkFieldForConfusableMix(document.metadata.title, "metadata.title", warnings);
  checkFieldForConfusableMix(document.metadata.description, "metadata.description", warnings);
  checkFieldForConfusableMix(document.metadata.author, "metadata.author", warnings);
  checkFieldForConfusableMix(document.metadata.publisher, "metadata.publisher", warnings);
  for (const [section, path] of walkSections(document.sections, "sections")) {
    checkFieldForConfusableMix(section.title, `${path}.title`, warnings);
    checkFieldForConfusableMix(section.description, `${path}.description`, warnings);
    section.prompts.forEach((prompt, index) => checkFieldForConfusableMix(prompt.label, `${path}.prompts[${index}].label`, warnings));
  }
}

/** A bound must be comparable in the space its field lives in (specification 4.7). */
function validateHintBounds(document: AprDocument, errors: ValidationIssue[]): void {
  for (const [section, path] of walkSections(document.sections, "sections")) {
    section.prompts.forEach((prompt, index) => {
      const declared = prompt.hints.expectedDataType ?? "";
      let wanted: "number" | "string" | undefined;
      if (NUMERIC_TYPES.has(declared)) wanted = "number";
      else if (TEMPORAL_TYPES.has(declared)) wanted = "string";
      else return;
      for (const boundName of ["min", "max"] as const) {
        const value = prompt.hints[boundName];
        if (value === undefined || typeof value === wanted) continue;
        errors.push({
          code: "WRONG_TYPE",
          message: `hints.${boundName} is not ${wanted === "number" ? "a number" : "a string"} on a ${JSON.stringify(declared)} field, where the format declares a value comparable in that field's space.`,
          path: `${path}.prompts[${index}].hints.${boundName}`,
        });
      }
    });
  }
}

function validateTables(document: AprDocument, errors: ValidationIssue[], warnings: ValidationIssue[]): void {
  for (const [section, path] of walkSections(document.sections, "sections")) {
    if (section.kind !== "table") continue;
    const rows = section.sections;
    if (!rows.length) { errors.push({ code: "EMPTY_TABLE", message: "A table section has no instances. A table always has at least one row; an empty one cannot describe its own fields.", path }); continue; }
    const first = rows[0].prompts;
    for (const row of rows.slice(1)) {
      const prompts = row.prompts;
      let mismatch = prompts.length !== first.length;
      if (mismatch) warnings.push({ code: "TABLE_RAGGED", message: `Table instance ${JSON.stringify(row.id)} has ${prompts.length} prompts but the first has ${first.length}; corresponding fields cannot be aligned by position.`, path: `${path}.sections` });
      for (let index = 0; index < prompts.length && !mismatch; index++) mismatch = prompts[index].label !== first[index].label;
      if (mismatch) warnings.push({ code: "TABLE_LABEL_MISMATCH", message: `Table instance ${JSON.stringify(row.id)} does not name its fields as the first instance does; corresponding fields should share a label.`, path: `${path}.sections` });
    }
    if (section.maxRows && section.maxRows > 0 && rows.length > section.maxRows) warnings.push({ code: "TABLE_OVER_CAPACITY", message: `Table has ${rows.length} instances, above the advisory maximum of ${section.maxRows}.`, path });
  }
}

function inspectExtensions(extra: Record<string, unknown>, path: string): ValidationIssue[] {
  return Object.keys(extra)
    .filter(name => !name.includes("."))
    .map(name => ({ code: "UNPREFIXED_MEMBER", message: `unknown member ${JSON.stringify(name)} carries no reverse-DNS prefix; unprefixed names are reserved to the specification.`, path: `${path}.${name}` }));
}

function inspectSubmission(urls: readonly string[]): ValidationIssue[] {
  return urls.flatMap((url, index) => {
    const scheme = url.includes(":") ? url.slice(0, url.indexOf(":")) : "";
    if (SUBMISSION_SCHEMES.has(scheme.toLowerCase())) return [];
    return [{ code: "SUBMISSION_URL_UNSUPPORTED", message: `submission entry ${index} names the scheme ${JSON.stringify(scheme)}, which this document does not define; a reader offers the entries it understands.`, path: `metadata.submissionUrls[${index}]` }];
  });
}

function compiles(pattern: string): boolean {
  try { new RegExp(pattern); return true; } catch { return false; }
}
function isNumber(value: string): boolean { return value.trim() !== "" && Number.isFinite(Number(value)); }
function looksLike(value: string, expected: string): boolean {
  const checks: Record<string, (v: string) => boolean> = {
    email: v => v.includes("@") && v.split("@").at(-1)!.includes("."),
    number: isNumber, range: isNumber,
    currency: v => isNumber(v.replace(/[^0-9.eE+-]/g, "") || "x"),
    date: v => /^\d{4}-\d{2}-\d{2}/.test(v),
    url: v => v.startsWith("http://") || v.startsWith("https://"),
    boolean: v => ["true", "false", "yes", "no", "1", "0"].includes(v.trim().toLowerCase()),
  };
  return checks[expected]?.(value) ?? true;
}

function outOfBounds(prompt: Prompt): ValidationIssue[] {
  const hints = prompt.hints;
  if (!hints.min && !hints.max) return [];
  const value = Number(prompt.response);
  if (!Number.isFinite(value)) return [];
  const issues: ValidationIssue[] = [];
  for (const [bound, name, worse] of [[hints.min, "minimum", (a: number, b: number) => a < b], [hints.max, "maximum", (a: number, b: number) => a > b]] as const) {
    if (!bound) continue;
    const limit = Number(bound);
    if (!Number.isFinite(limit)) continue;
    if (worse(value, limit)) issues.push({ code: "RESPONSE_OUTSIDE_BOUNDS", message: `Outside the suggested ${name} of ${bound}. Bounds describe the control offered, not a limit on the answer.`, path: prompt.id });
  }
  return issues;
}

export function advisoriesFor(prompt: Prompt, roles: ReadonlySet<string>): ValidationIssue[] {
  const warnings: ValidationIssue[] = [];
  if (prompt.role && !roles.has(prompt.role)) warnings.push({ code: "UNDECLARED_ROLE", message: `role ${JSON.stringify(prompt.role)} is not declared in metadata.roles.`, path: prompt.id });
  warnings.push(...inspectExtensions(prompt.extra, prompt.id));

  const hints = prompt.hints;
  const declared = hints.expectedDataType;
  if (declared && !REGISTERED_TYPES.has(declared)) warnings.push({ code: "UNREGISTERED_DATA_TYPE", message: `expectedDataType ${JSON.stringify(declared)} is not in the type registry; an unrecognised type degrades to text and never rejects a response.`, path: prompt.id });
  if (hints.validationPattern && !compiles(hints.validationPattern)) warnings.push({ code: "HINT_UNUSABLE", message: "validationPattern is not a valid regular expression, so nothing can apply it.", path: prompt.id });

  warnings.push(...inspectText(prompt.response).map(finding => ({ code: finding.code, message: `Response contains a hidden or visually deceptive character (${finding.description}) at offset ${finding.offset}. It was preserved; verify it was intentional.`, path: prompt.id })));
  if (!prompt.response) return warnings;

  if (hints.validationPattern) {
    try {
      if (!new RegExp(hints.validationPattern).test(prompt.response)) warnings.push({ code: "RESPONSE_PATTERN_MISMATCH", message: `${JSON.stringify(prompt.response)} does not match the suggested pattern.`, path: prompt.id });
    } catch {
      warnings.push({ code: "RESPONSE_PATTERN_MISMATCH", message: "The suggested pattern is not a valid regex.", path: prompt.id });
    }
  }
  if (declared && !looksLike(prompt.response, declared)) warnings.push({ code: "RESPONSE_CONTRADICTS_TYPE", message: `${JSON.stringify(prompt.response)} does not look like ${JSON.stringify(declared)} (advisory).`, path: prompt.id });
  if (hints.suggestedValues.length && !hints.suggestedValues.includes(prompt.response)) warnings.push({ code: "RESPONSE_OUTSIDE_SUGGESTED_VALUES", message: "Not one of the suggested options, which the format allows.", path: prompt.id });
  warnings.push(...outOfBounds(prompt));
  return warnings;
}

function documentAdvisories(document: AprDocument): ValidationIssue[] {
  const roles = new Set((document.roles ?? []).map(role => role.id).filter(Boolean));
  const warnings: ValidationIssue[] = [...inspectExtensions(document.metadata.extra, "metadata"), ...inspectSubmission(document.metadata.submissionUrls ?? [])];
  for (const [section, path] of walkSections(document.sections, "sections")) {
    if (section.kind !== "table" && (section.maxRows !== undefined || section.canAddRows !== undefined)) warnings.push({
      code: "TABLE_MEMBERS_ON_A_PLAIN_SECTION",
      message: "maxRows or canAddRows on a section that is not a table; a table is a table only by carrying kind: \"table\".",
      path,
    });
    if (section.role && !roles.has(section.role)) warnings.push({ code: "UNDECLARED_ROLE", message: `role ${JSON.stringify(section.role)} is not declared in metadata.roles.`, path });
    warnings.push(...inspectExtensions(section.extra, path));
    for (const prompt of section.prompts) warnings.push(...advisoriesFor(prompt, roles));
  }
  return warnings;
}

export function validate(document: AprDocument): ValidationResult {
  const errors: ValidationIssue[] = []; const warnings: ValidationIssue[] = [];
  const required = (value: string | undefined, path: string, label: string) => { if (!value?.trim()) errors.push({ code: "REQUIRED_FIELD", message: `${label} is required.`, path }); };
  required(document.version, "aprVersion", "aprVersion"); if (document.version && !isSupportedVersion(document.version)) errors.push({ code: "UNSUPPORTED_VERSION", message: `Unsupported APR version ${document.version}.`, path: "aprVersion" });
  required(document.metadata.title, "metadata.title", "metadata.title"); if (!document.sections.length) errors.push({ code: "REQUIRED_FIELD", message: "A document must have at least one section.", path: "sections" });
  if (document.documentType === "filledForm") required(document.metadata.templateId, "metadata.templateId", "A filled form templateId");
  validateShape(document, errors);
  validateTextFloor(document, warnings);
  checkConfusableScriptMix(document, warnings);
  validateHintBounds(document, errors);
  const sectionIds: string[] = []; const promptIds: string[] = [];
  const walk = (section: Section, path: string): void => {
    const here = `${path}[${section.id || "?"}]`;
    required(section.id, here, "Section id"); required(section.title, `${here}.title`, "Section title"); sectionIds.push(section.id);
    if (!section.prompts.length && !section.sections.length && section.kind !== "table") errors.push({ code: "EMPTY_SECTION", message: "A section must contain prompts or child sections.", path: here });
    for (const prompt of section.prompts) { const promptPath = `${here}.${prompt.id || "?"}`; required(prompt.id, promptPath, "Prompt id"); required(prompt.label, `${promptPath}.label`, "Prompt label"); promptIds.push(prompt.id); }
    for (const child of section.sections) walk(child, here);
  };
  for (const section of document.sections) walk(section, "sections");
  for (const [kind, ids] of [["section", sectionIds], ["prompt", promptIds]] as const) { const seen = new Set<string>(); for (const id of ids) { if (id && seen.has(id)) errors.push({ code: "DUPLICATE_ID", message: `Duplicate ${kind} id: ${id}`, path: id }); seen.add(id); } }
  validateTables(document, errors, warnings);
  warnings.push(...documentAdvisories(document));
  return { errors, warnings, isValid: errors.length === 0 };
}

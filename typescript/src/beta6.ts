import { parseAllDocuments, stringify } from "yaml";
import { AprParseError } from "./errors.js";
import { AprDocument, JsonObject, JsonValue } from "./model.js";
import { dumps, loads } from "./serialization.js";

/** Source representations defined by APR beta.6. */
export type Beta6Representation = "jsonc" | "yaml";
/** An independent beta.6 stream occurrence. */
export type Beta6Record = { type: "form"; document: AprDocument; /** Full parsed semantic object, including extensions. */ value?: JsonObject } | { type: "attestation"; value: JsonObject };
const VERSION = "1.0-beta.6";

/** Reads all stream occurrences without assigning meaning from their order. */
export function readBeta6Stream(source: string, representation: Beta6Representation): Beta6Record[] {
  const raw = representation === "jsonc" ? splitJsonc(source).map(stripJsonc) : splitYaml(source);
  return raw.map(parseRecord);
}

/** Reads exactly one form; a stream must be handled through readBeta6Stream. */
export function readBeta6Form(source: string, representation: Beta6Representation): AprDocument {
  const records = readBeta6Stream(source, representation);
  if (records.length !== 1 || records[0].type !== "form") throw new AprParseError("APR_STREAM_REQUIRES_ITERATION");
  return records[0].document;
}

/** Writes one beta.6 form in the requested representation. */
export function writeBeta6Form(document: AprDocument, representation: Beta6Representation): string {
  if (document.version !== VERSION) throw new AprParseError(`APR beta.6 writers require version ${VERSION}`);
  const json = dumps(document);
  return representation === "jsonc" ? json : stringify(JSON.parse(json));
}

/** Writes every independent record, preserving occurrence order without linking records. */
export function writeBeta6Stream(records: Iterable<Beta6Record>, representation: Beta6Representation): string {
  const encoded = [...records].map(record => {
    const json = record.type === "form" ? JSON.stringify(record.value ?? JSON.parse(writeBeta6Form(record.document, "jsonc"))) : JSON.stringify(record.value, null, 2);
    return representation === "jsonc" ? json : stringify(JSON.parse(json));
  });
  return representation === "jsonc" ? encoded.map(value => `\u001e${value}\n`).join("") : encoded.join("---\n");
}

function parseRecord(raw: string): Beta6Record {
  let value: unknown;
  try { rejectDuplicateObjectMembers(raw); value = JSON.parse(raw); } catch (error) { throw new AprParseError(`not valid beta.6 representation: ${(error as Error).message}`); }
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new AprParseError("an APR beta.6 record must be an object");
  const object = value as JsonObject;
  if (object.version !== VERSION) throw new AprParseError(`APR beta.6 records must declare version ${VERSION}`);
  if (object.recordType !== undefined) {
    if (object.recordType !== "attestation") throw new AprParseError("unknown APR beta.6 stream record type");
    validateAttestation(object);
    return { type: "attestation", value: object };
  }
  if (object.signatures !== undefined) throw new AprParseError("RETIRED_EMBEDDED_SIGNATURES");
  return { type: "form", document: loads(JSON.stringify(object)), value: object };
}

/** Lightweight JSON member scanner used before JSON.parse's otherwise silent last-key-wins behavior. */
function rejectDuplicateObjectMembers(source: string): void {
  const stack: Array<Set<string> | null> = [];
  let i = 0;
  const whitespace = /\s/;
  const string = (): string => {
    const start = i++; let escaped = false;
    while (i < source.length) { const c = source[i++]; if (escaped) escaped = false; else if (c === "\\") escaped = true; else if (c === '"') return JSON.parse(source.slice(start, i)); }
    throw new Error("unterminated JSON string");
  };
  while (i < source.length) {
    const c = source[i]; if (whitespace.test(c) || c === "," || c === ":") { i++; continue; }
    if (c === "{") { stack.push(new Set()); i++; continue; }
    if (c === "[") { stack.push(null); i++; continue; }
    if (c === "}" || c === "]") { stack.pop(); i++; continue; }
    if (c === '"') {
      const value = string(); let next = i; while (next < source.length && whitespace.test(source[next])) next++;
      const object = stack.at(-1);
      if (source[next] === ":" && object) { if (object.has(value)) throw new Error(`duplicate member ${value}`); object.add(value); }
      continue;
    }
    i++;
  }
}

function validateAttestation(value: JsonObject): void {
  const subject = isObject(value.subject) ? value.subject : undefined;
  if (!subject || !isDigest(subject.digest) || subject.canonicalization !== "jcs-sha256") throw new AprParseError("beta.6 attestation requires subject.digest and jcs-sha256 canonicalization");
  const scope = isObject(value.scope) ? value.scope : undefined;
  if (!scope || (scope.kind !== "document" && scope.kind !== "fields")) throw new AprParseError("beta.6 attestation scope.kind must be document or fields");
  if (scope.kind === "fields" && (!Array.isArray(scope.fields) || !scope.fields.length || scope.fields.some(field => typeof field !== "string" || !field.trim()))) throw new AprParseError("beta.6 fields attestations require non-blank scope.fields");
  const manifest = isObject(value.manifest) ? value.manifest : undefined;
  if (!manifest || !isDigest(manifest.root) || !Array.isArray(manifest.entries)) throw new AprParseError("beta.6 attestation requires manifest.root and manifest.entries");
  for (const entry of manifest.entries) if (!isObject(entry) || typeof entry.path !== "string" || !isDigest(entry.digest)) throw new AprParseError("beta.6 manifest entries require path and digest");
  if (!Array.isArray(value.proofs) || !Array.isArray(value.witnesses) || value.witnesses.some(witness => !isDigest(witness))) throw new AprParseError("beta.6 attestations require proofs and digest witnesses arrays");
}

function isDigest(value: JsonValue | undefined): value is string {
  return typeof value === "string" && /^sha256:[0-9a-f]{64}$/.test(value);
}

function isObject(value: JsonValue | undefined): value is JsonObject {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function splitJsonc(source: string): string[] {
  const records = source.includes("\u001e") ? source.split("\u001e").filter(Boolean) : [source];
  if (!records.length) throw new AprParseError("an APR JSONC stream has no records");
  return records;
}

function splitYaml(source: string): string[] {
  if (/(^|[\s\[{,])(?:[&*!]|<<\s*:)/m.test(source)) throw new AprParseError("APR YAML forbids anchors, aliases, tags, and merge keys");
  const documents = parseAllDocuments(source, { schema: "core" });
  return documents.map(document => {
    if (document.errors.length) throw new AprParseError(`invalid APR YAML: ${document.errors[0].message}`);
    return JSON.stringify(document.toJSON() as JsonValue);
  });
}

function stripJsonc(source: string): string {
  let result = "", quote = false, escaped = false;
  for (let i = 0; i < source.length; i++) {
    const c = source[i];
    if (quote) { result += c; if (escaped) escaped = false; else if (c === "\\") escaped = true; else if (c === '"') quote = false; continue; }
    if (c === '"') { quote = true; result += c; continue; }
    if (c === "/" && source[i + 1] === "/") { while (i < source.length && source[i] !== "\n") i++; result += "\n"; continue; }
    if (c === "/" && source[i + 1] === "*") { i += 2; while (i + 1 < source.length && !(source[i] === "*" && source[i + 1] === "/")) i++; if (i + 1 >= source.length) throw new AprParseError("unterminated JSONC comment"); i++; continue; }
    result += c;
  }
  return result.replace(/,(\s*[}\]])/g, "$1");
}

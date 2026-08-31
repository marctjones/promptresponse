import { AprParseError } from "./errors.js";
import { AprDocument, JsonObject, JsonValue } from "./model.js";
import { dumps } from "./serialization.js";
import { Beta6Record } from "./beta6.js";
import * as pkijs from "pkijs";

/** The beta.6 representation-neutral digest algorithm. */
export const BETA6_CANONICALIZATION = "jcs-sha256";
export type Beta6ManifestEntry = { path: string; digest: string };
export type Beta6Manifest = { root: string; entries: Beta6ManifestEntry[] };
export type Beta6AttestationState = "valid" | "invalid" | "unresolved" | "unverifiable";
export type Beta6Resolution = { value: JsonObject; state: Beta6AttestationState; differingPaths: string[]; witnessesResolved: number };

/** Canonical JSON for the JSON subset APR permits. ECMAScript serialization supplies JCS number spelling. */
export function canonicalizeBeta6(value: JsonValue): string { return canonical(value); }
export function digestBeta6(value: JsonValue): string {
  return `sha256:${sha256(canonical(value))}`;
}
export function beta6FormValue(document: AprDocument): JsonObject { return JSON.parse(dumps(document)) as JsonObject; }
export function createBeta6Manifest(value: JsonValue): Beta6Manifest {
  const entries: Beta6ManifestEntry[] = [];
  const visit = (current: JsonValue, path: string) => {
    entries.push({ path, digest: digestBeta6(current) });
    if (Array.isArray(current)) current.forEach((child, index) => visit(child, `${path}/${index}`));
    else if (current !== null && typeof current === "object") Object.keys(current).sort().forEach(key => visit((current as JsonObject)[key]!, `${path}/${pointer(key)}`));
  };
  visit(value, "");
  return { root: digestBeta6(value), entries };
}

/** Resolves only digest/manifest/witness facts; unsupported proofs remain unverifiable. */
export function resolveBeta6Attestations(records: readonly Beta6Record[]): Beta6Resolution[] {
  const forms = new Map<string, JsonObject>();
  const envelopes = new Set<string>();
  for (const record of records) if (record.type === "form") { const value = record.value ?? beta6FormValue(record.document); forms.set(digestBeta6(value), value); }
  for (const record of records) if (record.type === "attestation") envelopes.add(attestationEnvelopeDigest(record.value));
  return records.filter((record): record is Extract<Beta6Record, { type: "attestation" }> => record.type === "attestation").map(record => {
    const subject = record.value.subject as JsonObject | undefined;
    const digest = subject?.digest;
    if (typeof digest !== "string") throw new AprParseError("beta.6 attestation subject.digest is required");
    const witnesses = Array.isArray(record.value.witnesses) ? record.value.witnesses.filter(item => typeof item === "string" && envelopes.has(item)).length : 0;
    const form = forms.get(digest);
    if (!form) return { value: record.value, state: "unresolved", differingPaths: [], witnessesResolved: witnesses };
    const actual = createBeta6Manifest(form);
    const manifest = record.value.manifest as JsonObject | undefined;
    const differing = manifest?.root === actual.root ? [] : [""];
    const entries = manifest?.entries;
    if (Array.isArray(entries)) for (const entry of entries) {
      if (entry === null || typeof entry !== "object" || Array.isArray(entry)) { differing.push("?"); continue; }
      const path = (entry as JsonObject).path, expected = (entry as JsonObject).digest;
      const found = actual.entries.find(current => current.path === path);
      if (typeof path !== "string" || typeof expected !== "string" || found?.digest !== expected) differing.push(typeof path === "string" ? path : "?");
    }
    validateFieldsScope(form, record.value, new Set(Array.isArray(entries) ? entries.filter((entry): entry is JsonObject => entry !== null && typeof entry === "object" && !Array.isArray(entry)).map(entry => entry.path).filter((path): path is string => typeof path === "string") : []), differing);
    if (differing.length) return { value: record.value, state: "invalid", differingPaths: [...new Set(differing)], witnessesResolved: witnesses };
    return { value: record.value, state: "unverifiable", differingPaths: [], witnessesResolved: witnesses };
  });
}

/** Resolves beta.6 attestations and asynchronously verifies supported CMS content proofs. */
export async function resolveBeta6AttestationsAsync(records: readonly Beta6Record[]): Promise<Beta6Resolution[]> {
  const resolved = resolveBeta6Attestations(records);
  return Promise.all(resolved.map(async result => {
    if (result.state !== "unverifiable") return result;
    const proofs = result.value.proofs;
    const hasCms = Array.isArray(proofs) && proofs.some(proof => isObject(proof) && proof.type === "cms/ecdsa-p256-sha256");
    if (!hasCms) return result;
    return { ...result, state: await verifyBeta6CmsProof(result.value) ? "valid" : "invalid" };
  }));
}

export function attestationEnvelopeDigest(value: JsonObject): string {
  const { proofs: _proofs, ...envelope } = value;
  return digestBeta6(envelope);
}

/** Verifies a detached CMS proof over the canonical proof-free envelope. */
export async function verifyBeta6CmsProof(value: JsonObject): Promise<boolean> {
  const proof = Array.isArray(value.proofs) ? value.proofs.find(item => isObject(item) && item.type === "cms/ecdsa-p256-sha256") as JsonObject | undefined : undefined;
  if (!proof || typeof proof.value !== "string") return false;
  try {
    const der = base64Bytes(proof.value);
    const content = pkijs.ContentInfo.fromBER(der.slice().buffer as ArrayBuffer);
    if (content.contentType !== pkijs.ContentInfo.SIGNED_DATA) return false;
    const signed = new pkijs.SignedData({ schema: content.content });
    const envelope: JsonObject = { ...value }; delete envelope.proofs;
    const data = new TextEncoder().encode(canonical(envelope));
    return await signed.verify({ signer: 0, checkChain: false, data: data.slice().buffer as ArrayBuffer });
  } catch { return false; }
}
function pointer(value: string): string { return value.replaceAll("~", "~0").replaceAll("/", "~1"); }
function validateFieldsScope(form: JsonObject, attestation: JsonObject, paths: Set<string>, differing: string[]): void {
  const scope = attestation.scope;
  if (scope === null || typeof scope !== "object" || Array.isArray(scope) || (scope as JsonObject).kind !== "fields") return;
  const fields = (scope as JsonObject).fields;
  if (!Array.isArray(fields) || !fields.length) { differing.push("/scope/fields"); return; }
  for (const id of fields) {
    if (typeof id !== "string") { differing.push("/scope/fields"); continue; }
    const found = findPrompt(form.sections, id, "/sections", []);
    if (!found) { differing.push("/scope/fields"); continue; }
    requirePath(paths, found.prompt, differing); requirePath(paths, `${found.prompt}/response`, differing);
    if (atPointer(form, `${found.prompt}/hints`) !== undefined) requirePath(paths, `${found.prompt}/hints`, differing);
    for (const section of found.sections) for (const member of ["id", "title", "description", "kind", "role"]) if (atPointer(form, `${section}/${member}`) !== undefined) requirePath(paths, `${section}/${member}`, differing);
  }
}
function findPrompt(value: JsonValue | undefined, id: string, base: string, ancestors: string[]): { prompt: string; sections: string[] } | undefined {
  if (!Array.isArray(value)) return undefined;
  for (let index = 0; index < value.length; index++) { const section = value[index]; if (section === null || typeof section !== "object" || Array.isArray(section)) continue; const path = `${base}/${index}`, next = [...ancestors, path], node = section as JsonObject, prompts = node.prompts; if (Array.isArray(prompts)) for (let prompt = 0; prompt < prompts.length; prompt++) if (prompts[prompt] !== null && typeof prompts[prompt] === "object" && !Array.isArray(prompts[prompt]) && (prompts[prompt] as JsonObject).id === id) return { prompt: `${path}/prompts/${prompt}`, sections: next }; const nested = findPrompt(node.sections, id, `${path}/sections`, next); if (nested) return nested; }
  return undefined;
}
function atPointer(value: JsonValue, path: string): JsonValue | undefined { let current: JsonValue | undefined = value; for (const part of path.split("/").slice(1)) { if (Array.isArray(current)) current = current[Number(part)]; else if (current !== null && typeof current === "object") current = (current as JsonObject)[part.replaceAll("~1", "/").replaceAll("~0", "~")]; else return undefined; } return current; }
function requirePath(paths: Set<string>, path: string, differing: string[]): void { if (!paths.has(path)) differing.push(path); }
function canonical(value: JsonValue): string {
  if (value === null || typeof value === "boolean" || typeof value === "number" || typeof value === "string") return JSON.stringify(value);
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  return `{${Object.keys(value).sort().map(key => `${JSON.stringify(key)}:${canonical(value[key]!)}`).join(",")}}`;
}
function isObject(value: JsonValue): value is JsonObject { return value !== null && typeof value === "object" && !Array.isArray(value); }
function base64Bytes(value: string): Uint8Array { const binary = atob(value); return Uint8Array.from(binary, char => char.charCodeAt(0)); }

// Small synchronous SHA-256 implementation keeps the browser-capable SDK free of
// Node-only imports. It intentionally accepts UTF-8 text only: callers first JCS
// canonicalize the JSON semantic model.
function sha256(text: string): string {
  const bytes = [...new TextEncoder().encode(text)], bitLength = bytes.length * 8;
  bytes.push(0x80); while (bytes.length % 64 !== 56) bytes.push(0);
  for (let shift = 56; shift >= 0; shift -= 8) bytes.push(Math.floor(bitLength / 2 ** shift) & 0xff);
  const hash = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];
  const constants = [0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2];
  for (let offset = 0; offset < bytes.length; offset += 64) {
    const words = Array<number>(64).fill(0);
    for (let i = 0; i < 16; i++) words[i] = (bytes[offset + i * 4] << 24) | (bytes[offset + i * 4 + 1] << 16) | (bytes[offset + i * 4 + 2] << 8) | bytes[offset + i * 4 + 3];
    for (let i = 16; i < 64; i++) { const a = words[i - 15], b = words[i - 2]; words[i] = (((a >>> 7 | a << 25) ^ (a >>> 18 | a << 14) ^ (a >>> 3)) + words[i - 16] + ((b >>> 17 | b << 15) ^ (b >>> 19 | b << 13) ^ (b >>> 10)) + words[i - 7]) | 0; }
    let [a,b,c,d,e,f,g,h] = hash;
    for (let i = 0; i < 64; i++) { const s1 = (e >>> 6 | e << 26) ^ (e >>> 11 | e << 21) ^ (e >>> 25 | e << 7), choice = (e & f) ^ (~e & g), temp1 = (h + s1 + choice + constants[i] + words[i]) | 0, s0 = (a >>> 2 | a << 30) ^ (a >>> 13 | a << 19) ^ (a >>> 22 | a << 10), majority = (a & b) ^ (a & c) ^ (b & c), temp2 = (s0 + majority) | 0; h=g; g=f; f=e; e=(d+temp1)|0; d=c; c=b; b=a; a=(temp1+temp2)|0; }
    hash[0]=(hash[0]+a)|0; hash[1]=(hash[1]+b)|0; hash[2]=(hash[2]+c)|0; hash[3]=(hash[3]+d)|0; hash[4]=(hash[4]+e)|0; hash[5]=(hash[5]+f)|0; hash[6]=(hash[6]+g)|0; hash[7]=(hash[7]+h)|0;
  }
  return hash.map(word => (word >>> 0).toString(16).padStart(8, "0")).join("");
}

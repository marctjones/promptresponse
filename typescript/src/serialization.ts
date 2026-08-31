import { AprParseError } from "./errors.js";
import { AprDocument, JsonObject, JsonValue, Metadata, Prompt, PromptHints, ResponseMetadata, RETIRED_MEMBERS, RoleDefinition, Section } from "./model.js";
import { normalize } from "./text.js";

export const CURRENT_VERSION = "1.0-beta.6";
const object = (value: unknown, what: string): JsonObject => {
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new AprParseError(`${what} must be a JSON object`);
  return value as JsonObject;
};
const string = (node: JsonObject, key: string, what: string): string | undefined => {
  const value = node[key];
  if (value === undefined || value === null) return undefined;
  if (typeof value !== "string") throw new AprParseError(`${what}.${key} must be a string; APR values are never coerced.`);
  return value;
};
const strings = (value: JsonValue | undefined, what: string): string[] => {
  if (value === undefined || value === null) return [];
  if (!Array.isArray(value) || value.some(item => typeof item !== "string")) throw new AprParseError(`${what} must be an array of strings`);
  return value as string[];
};
const rest = (node: JsonObject, known: Set<string>): JsonObject => Object.fromEntries(Object.entries(node).filter(([key]) => !known.has(key) && !RETIRED_MEMBERS.has(key)));
const optionalObject = (value: JsonValue | undefined, what: string): JsonObject | undefined => value === undefined || value === null ? undefined : object(value, what);

function parseHints(value: JsonObject): PromptHints {
  const known = new Set(["expectedDataType", "placeholder", "helpText", "validationPattern", "suggestedValues", "min", "max", "step", "exprHidden", "exprValue", "exprExpected", "exprValidation", "exprReadOnly"]);
  return { expectedDataType: string(value, "expectedDataType", "hints"), placeholder: normalize(string(value, "placeholder", "hints")), helpText: normalize(string(value, "helpText", "hints")), validationPattern: string(value, "validationPattern", "hints"), suggestedValues: strings(value.suggestedValues, "hints.suggestedValues").map(value => normalize(value)!), min: string(value, "min", "hints"), max: string(value, "max", "hints"), step: string(value, "step", "hints"), exprHidden: string(value, "exprHidden", "hints"), exprValue: string(value, "exprValue", "hints"), exprExpected: string(value, "exprExpected", "hints"), exprValidation: string(value, "exprValidation", "hints"), exprReadOnly: string(value, "exprReadOnly", "hints"), extra: rest(value, known) };
}
function parseResponseMetadata(value: JsonObject): ResponseMetadata {
  const known = new Set(["inferredDataType", "source", "lastModified"]);
  return { inferredDataType: string(value, "inferredDataType", "responseMetadata"), source: string(value, "source", "responseMetadata"), lastModified: string(value, "lastModified", "responseMetadata"), extra: rest(value, known) };
}
function parsePrompt(value: JsonValue): Prompt {
  const node = object(value, "prompt"); const known = new Set(["id", "label", "response", "role", "hints", "responseMetadata"]);
  const hints = optionalObject(node.hints, "hints"); const responseMetadata = optionalObject(node.responseMetadata, "responseMetadata");
  return { id: string(node, "id", "prompt") ?? "", label: normalize(string(node, "label", "prompt")) ?? "", response: string(node, "response", "prompt") ?? "", role: string(node, "role", "prompt"), hints: hints ? parseHints(hints) : { suggestedValues: [], extra: {} }, responseMetadata: responseMetadata ? parseResponseMetadata(responseMetadata) : { extra: {} }, extra: rest(node, known) };
}
function parseSection(value: JsonValue): Section {
  const node = object(value, "section"); const known = new Set(["id", "title", "description", "kind", "canAddRows", "maxRows", "role", "prompts", "sections"]);
  const prompts = node.prompts ?? []; const sections = node.sections ?? [];
  if (!Array.isArray(prompts) || !Array.isArray(sections)) throw new AprParseError("section.prompts and section.sections must be arrays");
  return { id: string(node, "id", "section") ?? "", title: normalize(string(node, "title", "section")) ?? "", description: normalize(string(node, "description", "section")), kind: string(node, "kind", "section"), canAddRows: string(node, "canAddRows", "section"), maxRows: string(node, "maxRows", "section"), role: string(node, "role", "section"), prompts: prompts.map(parsePrompt), sections: sections.map(parseSection), extra: rest(node, known) };
}
function parseMetadata(value: JsonValue): Metadata {
  if (object(value, "metadata").submissionUrl !== undefined) throw new AprParseError("metadata.submissionUrl is retired; use metadata.submissionUrls as an array of strings");
  const node = object(value, "metadata"); const known = new Set(["title", "description", "author", "created", "modified", "templateId", "templateVersion", "filledBy", "filledDate", "publisher", "submissionUrls"]);
  return { title: normalize(string(node, "title", "metadata")) ?? "", description: normalize(string(node, "description", "metadata")), author: normalize(string(node, "author", "metadata")), created: string(node, "created", "metadata"), modified: string(node, "modified", "metadata"), templateId: string(node, "templateId", "metadata"), templateVersion: string(node, "templateVersion", "metadata"), filledBy: normalize(string(node, "filledBy", "metadata")), filledDate: string(node, "filledDate", "metadata"), publisher: normalize(string(node, "publisher", "metadata")), submissionUrls: strings(node.submissionUrls, "metadata.submissionUrls"), extra: rest(node, known) };
}
function parseRole(value: JsonValue): RoleDefinition {
  const node = object(value, "role"); const known = new Set(["id", "name", "description"]);
  return { id: string(node, "id", "role") ?? "", name: normalize(string(node, "name", "role")), description: normalize(string(node, "description", "role")), extra: rest(node, known) };
}

export function isSupportedVersion(version: string | undefined): boolean {
  return version === CURRENT_VERSION;
}
/** Parse document bytes. Structural defects remain validation errors where APR requires that separation. */
export function loads(text: string): AprDocument {
  let parsed: unknown; try { parsed = JSON.parse(text.replace(/^\ufeff/, "")); } catch (error) { throw new AprParseError(`not valid JSON: ${(error as Error).message}`); }
  const node = object(parsed, "APR document");
  for (const member of ["version", "metadata", "sections"]) if (!(member in node)) throw new AprParseError(`${member} is required`);
  if (!Array.isArray(node.sections)) throw new AprParseError("sections must be an array");
  if (!isSupportedVersion(string(node, "version", "document"))) throw new AprParseError(`Unsupported APR version ${String(node.version)}; this build accepts only ${CURRENT_VERSION}`);
  if (node.roles !== undefined && !Array.isArray(node.roles)) throw new AprParseError("roles must be an array");
  if (node.signatures !== undefined && !Array.isArray(node.signatures)) throw new AprParseError("signatures must be an array");
  if (node.signatures !== undefined) throw new AprParseError("RETIRED_EMBEDDED_SIGNATURES");
  const known = new Set(["version", "documentType", "metadata", "sections", "roles", "signatures"]);
  return { version: string(node, "version", "document") ?? "", documentType: string(node, "documentType", "document"), metadata: parseMetadata(node.metadata), sections: (node.sections as JsonValue[]).map(parseSection), roles: node.roles === undefined ? undefined : (node.roles as JsonValue[]).map(parseRole), signatures: node.signatures as JsonValue[] | undefined, extra: rest(node, known) };
}
const compact = (node: Record<string, JsonValue | undefined>): JsonObject => Object.fromEntries(Object.entries(node).filter(([, value]) => value !== undefined && value !== null && !(Array.isArray(value) && value.length === 0) && !(typeof value === "object" && !Array.isArray(value) && Object.keys(value as object).length === 0))) as JsonObject;
function hintsJson(hints: PromptHints): JsonObject { return { ...compact({ expectedDataType: hints.expectedDataType, placeholder: hints.placeholder, helpText: hints.helpText, validationPattern: hints.validationPattern, suggestedValues: hints.suggestedValues, min: hints.min, max: hints.max, step: hints.step, exprHidden: hints.exprHidden, exprValue: hints.exprValue, exprExpected: hints.exprExpected, exprValidation: hints.exprValidation, exprReadOnly: hints.exprReadOnly }), ...hints.extra }; }
function promptJson(prompt: Prompt): JsonObject { const node: JsonObject = { id: prompt.id, label: prompt.label, response: prompt.response }; if (prompt.role) node.role = prompt.role; const hints = hintsJson(prompt.hints); if (Object.keys(hints).length) node.hints = hints; const responseMetadata = { ...compact({ inferredDataType: prompt.responseMetadata.inferredDataType, source: prompt.responseMetadata.source, lastModified: prompt.responseMetadata.lastModified }), ...prompt.responseMetadata.extra }; if (Object.keys(responseMetadata).length) node.responseMetadata = responseMetadata; return { ...node, ...prompt.extra }; }
function sectionJson(section: Section): JsonObject { const node: JsonObject = { id: section.id, title: section.title, ...compact({ description: section.description, kind: section.kind, canAddRows: section.canAddRows, maxRows: section.maxRows, role: section.role }) }; if (section.prompts.length) node.prompts = section.prompts.map(promptJson); if (section.sections.length) node.sections = section.sections.map(sectionJson); return { ...node, ...section.extra }; }
/** Serialize an APR document while preserving unknown non-retired members. */
export function dumps(document: AprDocument, indent = 2): string {
  if (!isSupportedVersion(document.version)) throw new AprParseError(`Unsupported APR version ${document.version}; this build accepts only ${CURRENT_VERSION}`);
  if (document.signatures !== undefined) throw new AprParseError("RETIRED_EMBEDDED_SIGNATURES");
  const metadata: JsonObject = { title: document.metadata.title, ...compact({ description: document.metadata.description, author: document.metadata.author, created: document.metadata.created, modified: document.metadata.modified, templateId: document.metadata.templateId, templateVersion: document.metadata.templateVersion, filledBy: document.metadata.filledBy, filledDate: document.metadata.filledDate, publisher: document.metadata.publisher, submissionUrls: document.metadata.submissionUrls }), ...document.metadata.extra };
  const node: JsonObject = { version: document.version, metadata, sections: document.sections.map(sectionJson) }; if (document.documentType) node.documentType = document.documentType; if (document.roles) node.roles = document.roles.map(role => ({ ...compact({ id: role.id, name: role.name, description: role.description }), ...role.extra })); return JSON.stringify({ ...node, ...document.extra }, null, indent);
}

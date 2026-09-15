/** APR's optional CEL expression binding. Expressions are advisory and pure. */
import { Environment, parse } from "@marcbachmann/cel-js";
import type { AprDocument, Prompt, Section } from "./model.js";

type ContextValues = Record<string, string>;

// Specification: "Provide the CEL standard library and standard macros, and no
// extension library or custom function" (the CEL strings extension is
// explicitly not required). cel-js, unlike celpy, bakes the strings and bytes
// extension member functions into every environment with no option to
// disable them, so an expression using one would evaluate here and fail
// identically in Python and .NET -- a cross-SDK divergence a conformance
// suite exists to catch. Names are exactly the cel-js built-ins absent from
// celpy's base registry (functions.js's functionOverload calls, diffed
// against celpy's activation dump).
const EXTENSION_FUNCTIONS = new Set([
  "lowerAscii", "upperAscii", "trim", "indexOf", "lastIndexOf", "substring", "split", "join",
  "json", "hex", "base64", "at",
]);
function usesExtensionFunction(node: unknown): boolean {
  if (Array.isArray(node)) return node.some(usesExtensionFunction);
  if (!node || typeof node !== "object") return false;
  const { op, args } = node as { op?: string; args?: unknown };
  const name = Array.isArray(args) ? args[0] : undefined;
  if ((op === "call" || op === "rcall") && typeof name === "string" && EXTENSION_FUNCTIONS.has(name)) return true;
  return usesExtensionFunction(args);
}

function prompts(sections: Section[]): Prompt[] {
  return sections.flatMap(section => [...section.prompts, ...prompts(section.sections)]);
}
const TIMESTAMP = "google.protobuf.Timestamp";
function typeFor(expected?: string): "double" | "bool" | typeof TIMESTAMP | "list<string>" | "string" {
  switch ((expected ?? "").toLowerCase()) {
    case "number": case "currency": case "range": return "double";
    case "boolean": return "bool";
    case "date": case "time": case "datetime": return TIMESTAMP;
    case "multichoice": return "list<string>";
    default: return "string";
  }
}
function bind(value: string, expected?: string): unknown | undefined {
  const kind = (expected ?? "").toLowerCase();
  if (["number", "currency", "range"].includes(kind)) {
    if (!value.trim() || !Number.isFinite(Number(value.trim()))) return undefined;
    return Number(value.trim());
  }
  if (kind === "boolean") {
    const v = value.trim().toLowerCase();
    if (["true", "yes", "y", "1", "on", "x", "checked"].includes(v)) return true;
    if (["false", "no", "n", "0", "off", "unchecked"].includes(v)) return false;
    return undefined;
  }
  if (["date", "time", "datetime"].includes(kind)) {
    if (!value.trim()) return undefined;
    const source = kind === "date" ? `${value}T00:00:00Z` : kind === "time" ? `1970-01-01T${value}Z` : value;
    const timestamp = new Date(source);
    return Number.isNaN(timestamp.valueOf()) ? undefined : timestamp;
  }
  if (kind === "multichoice") return (value.includes("\n") ? value.split("\n") : value.split(",")).map(x => x.trim()).filter(Boolean);
  return value;
}
function stored(value: unknown): string {
  if (typeof value === "boolean") return value ? "true" : "false";
  if (typeof value === "number") return String(value);
  if (value instanceof Date) return value.toISOString().replace(/\.\d{3}Z$/, "Z");
  if (Array.isArray(value)) return value.map(String).join("\n");
  return value == null ? "" : String(value);
}
// The standard library's string(timestamp), which cel-js lacks: RFC 3339 in UTC,
// with fractional seconds only where the instant has them.
function timestampString(value: Date): string {
  return value.toISOString().replace(/\.(\d{3})Z$/, (_, fraction: string) => fraction === "000" ? "Z" : `.${fraction.replace(/0+$/, "")}Z`);
}

const RESERVED_NAMES = new Set(["_now", "_today", "_id", "_this", "ctx"]);

export class ExpressionContext {
  private readonly fields: Map<string, Prompt>;
  private readonly bindings: Record<string, unknown> = {};
  // _now and _today are supplied by the caller and never read from the host
  // clock: defaulting them to "now" made every expression using them
  // non-deterministic, and different from the other implementations.
  // _now is the instant the caller supplied, a separate input from _today.
  constructor(document: AprDocument, today?: string, ctx: ContextValues = {}, now?: string) {
    this.fields = new Map(prompts(document.sections).filter(prompt => prompt.id).map(prompt => [prompt.id, prompt]));
    for (const prompt of this.fields.values()) {
      const value = bind(prompt.response, prompt.hints.expectedDataType);
      if (value !== undefined) this.bindings[prompt.id] = value;
    }
    if (now) {
      const instant = bind(now, "datetime");
      if (instant !== undefined) this.bindings._now = instant;
    }
    // _today is the date as YYYY-MM-DD, a string rather than a timestamp.
    if (today) this.bindings._today = today.slice(0, 10);
    this.bindings.ctx = ctx;
  }
  evaluate(prompt: Prompt, expression: string): unknown | undefined {
    try {
      if (usesExtensionFunction(parse(expression).ast)) return undefined;
      const environment = new Environment({ unlistedVariablesAreDyn: false });
      // The activation's reserved names (specification: "that prompt's bound
      // type... where the id is a valid CEL identifier and not reserved") take
      // precedence over a same-named prompt field. cel-js throws on a second
      // registerVariable call for the same name rather than letting the later
      // one win, so a document with a field literally named "ctx" would
      // otherwise fail every expression in it via the catch below.
      for (const field of this.fields.values()) if (!RESERVED_NAMES.has(field.id)) environment.registerVariable(field.id, typeFor(field.hints.expectedDataType));
      environment
        .registerVariable("_today", "string")
        .registerVariable("_now", TIMESTAMP)
        .registerVariable("_id", "string")
        .registerVariable("ctx", "map<string, string>")
        .registerVariable("_this", typeFor(prompt.hints.expectedDataType))
        .registerFunction(`string(${TIMESTAMP}): string`, timestampString);
      const bindings = { ...this.bindings };
      const current = bind(prompt.response, prompt.hints.expectedDataType);
      if (current !== undefined) bindings._this = current;
      bindings._id = prompt.id;
      return environment.evaluate(expression, bindings);
    } catch { return undefined; }
  }
}

export function buildExpressionContext(document: AprDocument, today?: string, ctx?: ContextValues, now?: string): ExpressionContext { return new ExpressionContext(document, today, ctx, now); }
export function computeValue(prompt: Prompt, context: ExpressionContext): string | undefined {
  const expression = prompt.hints.exprValue;
  if (!expression?.trim()) return undefined;
  const value = context.evaluate(prompt, expression);
  return value === undefined ? undefined : stored(value);
}
export function condition(prompt: Prompt, expression: string | undefined, context: ExpressionContext): boolean { return expression?.trim() ? context.evaluate(prompt, expression) === true : false; }
export function validationMessage(prompt: Prompt, context: ExpressionContext): string | undefined {
  const expression = prompt.hints.exprValidation;
  if (!expression?.trim()) return undefined;
  const value = context.evaluate(prompt, expression);
  // exprValidation is typed "string" (specification's expression profile): a
  // result of any other CEL type is the same failure as a compile error, not
  // a value to stringify -- 2 + 2 is not almost a validation message.
  if (typeof value !== "string") return undefined;
  return value || undefined;
}
export function recomputeComputedValues(document: AprDocument, today?: string, ctx?: ContextValues, now?: string): boolean {
  let changed = false;
  for (let pass = 0; pass < 5; pass++) {
    const context = buildExpressionContext(document, today, ctx, now); let changedThisPass = false;
    for (const prompt of prompts(document.sections)) {
      if (!prompt.hints.exprValue) continue;
      // Every non-empty response in the document as it was read is authored, whatever
      // produced it, and APR-EXPR-001 says an expression must not rewrite one. Only what
      // this session computed may be recomputed.
      if (prompt.response && !prompt.computedInThisSession) continue;
      const value = computeValue(prompt, context);
      if (value !== undefined && value !== prompt.response) { prompt.response = value; prompt.computedInThisSession = true; changed = changedThisPass = true; }
    }
    if (!changedThisPass) break;
  }
  return changed;
}

/** APR's optional CEL expression binding. Expressions are advisory and pure. */
import { Environment } from "@marcbachmann/cel-js";
import type { AprDocument, Prompt, Section } from "./model.js";

export const COMPUTED_SOURCE = "computed";
type ContextValues = Record<string, string>;

function prompts(sections: Section[]): Prompt[] {
  return sections.flatMap(section => [...section.prompts, ...prompts(section.sections)]);
}
function typeFor(expected?: string): "double" | "bool" | "dyn" | "list<string>" | "string" {
  switch ((expected ?? "").toLowerCase()) {
    case "number": case "currency": case "range": return "double";
    case "boolean": return "bool";
    // cel-js has no public timestamp declaration despite supporting Date values at
    // runtime; retain the value as dyn rather than misdeclare it as string.
    case "date": case "time": case "datetime": return "dyn";
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

export class ExpressionContext {
  private readonly fields: Map<string, Prompt>;
  private readonly bindings: Record<string, unknown> = {};
  constructor(document: AprDocument, today = new Date().toISOString(), ctx: ContextValues = {}) {
    this.fields = new Map(prompts(document.sections).filter(prompt => prompt.id).map(prompt => [prompt.id, prompt]));
    for (const prompt of this.fields.values()) {
      const value = bind(prompt.response, prompt.hints.expectedDataType);
      if (value !== undefined) this.bindings[prompt.id] = value;
    }
    const boundToday = bind(today, "datetime");
    if (boundToday !== undefined) this.bindings._today = boundToday;
    this.bindings.ctx = ctx;
  }
  evaluate(prompt: Prompt, expression: string): unknown | undefined {
    try {
      const environment = new Environment({ unlistedVariablesAreDyn: false });
      for (const field of this.fields.values()) environment.registerVariable(field.id, typeFor(field.hints.expectedDataType));
      environment.registerVariable("_today", "dyn").registerVariable("ctx", "map<string, string>").registerVariable("_this", typeFor(prompt.hints.expectedDataType));
      const bindings = { ...this.bindings };
      const current = bind(prompt.response, prompt.hints.expectedDataType);
      if (current !== undefined) bindings._this = current;
      return environment.evaluate(expression, bindings);
    } catch { return undefined; }
  }
}

export function buildExpressionContext(document: AprDocument, today?: string, ctx?: ContextValues): ExpressionContext { return new ExpressionContext(document, today, ctx); }
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
  const message = value === undefined ? "" : stored(value);
  return message || undefined;
}
export function recomputeComputedValues(document: AprDocument, today?: string, ctx?: ContextValues): boolean {
  let changed = false;
  for (let pass = 0; pass < 5; pass++) {
    const context = buildExpressionContext(document, today, ctx); let changedThisPass = false;
    for (const prompt of prompts(document.sections)) {
      if (!prompt.hints.exprValue) continue;
      if (prompt.response && prompt.responseMetadata.source !== COMPUTED_SOURCE) continue;
      const value = computeValue(prompt, context);
      if (value !== undefined && value !== prompt.response) { prompt.response = value; prompt.responseMetadata.source = COMPUTED_SOURCE; changed = changedThisPass = true; }
    }
    if (!changedThisPass) break;
  }
  return changed;
}

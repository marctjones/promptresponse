import { AprDocument, Prompt, Section } from "./model.js";
import { displayRoleName } from "./roles.js";
import { inspectText } from "./unicode-security.js";

export interface HtmlRenderOptions {
  /** Render current responses as editable fields; false produces a safe read-only projection. */
  editable?: boolean;
}

const escape = (value: string): string => value.replace(/[&<>'"]/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", "\"": "&quot;" })[char]!);
const inputType = (prompt: Prompt): string => ({ email: "email", phone: "tel", url: "url", date: "date", time: "time", datetime: "datetime-local", number: "text", currency: "text" }[prompt.hints.expectedDataType ?? ""] ?? "text");

function promptHtml(document: AprDocument, prompt: Prompt, editable: boolean): string {
  const id = `apr-${escape(prompt.id)}`;
  const role = displayRoleName(document, prompt.role);
  const roleText = role ? `<span class="apr-role">For ${escape(role)}</span>` : "";
  const help = prompt.hints.helpText ? `<small id="${id}-help">${escape(prompt.hints.helpText)}</small>` : "";
  const findings = inspectText(prompt.response);
  const warning = findings.length ? `<small id="${id}-unicode" class="apr-security-warning" role="status">Suspicious Unicode: ${escape(findings.map(f => `${f.code} (U+${f.codepoint.toString(16).toUpperCase().padStart(4, "0")})`).join(", "))}. The stored response was not changed.</small>` : "";
  const describedByIds = [prompt.hints.helpText ? `${id}-help` : "", findings.length ? `${id}-unicode` : ""].filter(Boolean).join(" ");
  const describedBy = describedByIds ? ` aria-describedby="${describedByIds}"` : "";
  if (!editable) return `<div class="apr-prompt" data-apr-prompt="${escape(prompt.id)}"><dt>${escape(prompt.label)} ${roleText}</dt><dd><bdi>${escape(prompt.response)}</bdi></dd>${help}${warning}</div>`;
  const field = prompt.hints.expectedDataType === "multiline"
    ? `<textarea id="${id}" name="${escape(prompt.id)}" dir="auto"${describedBy}>${escape(prompt.response)}</textarea>`
    : `<input id="${id}" name="${escape(prompt.id)}" type="${inputType(prompt)}" dir="auto" value="${escape(prompt.response)}"${prompt.hints.placeholder ? ` placeholder="${escape(prompt.hints.placeholder)}"` : ""}${describedBy}>`;
  return `<div class="apr-prompt" data-apr-prompt="${escape(prompt.id)}"><label for="${id}">${escape(prompt.label)} ${roleText}</label>${field}${help}${warning}</div>`;
}
function sectionHtml(document: AprDocument, section: Section, editable: boolean): string {
  const content = [...section.prompts.map(prompt => promptHtml(document, prompt, editable)), ...section.sections.map(child => sectionHtml(document, child, editable))].join("\n");
  return `<fieldset class="apr-section" data-apr-section="${escape(section.id)}"><legend>${escape(section.title)}</legend>${section.description ? `<p>${escape(section.description)}</p>` : ""}${content}</fieldset>`;
}

/**
 * Create an accessible, dependency-free HTML projection. It executes no APR
 * content and deliberately does not contact metadata.submissionUrls.
 */
export function renderHtml(document: AprDocument, options: HtmlRenderOptions = {}): string {
  const editable = options.editable ?? true;
  const body = document.sections.map(section => sectionHtml(document, section, editable)).join("\n");
  return `<form class="apr-document" data-apr-version="${escape(document.version)}" onsubmit="return false"><h1>${escape(document.metadata.title)}</h1>${document.metadata.description ? `<p>${escape(document.metadata.description)}</p>` : ""}${body}</form>`;
}

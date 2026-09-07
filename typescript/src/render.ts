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
  // Beside the label, never inside it. A role annotation folded into the <label>
  // becomes part of the field's computed accessible name, and APR-RENDER-001 asks for
  // the label: "Date received For Records office" is not what the document named it.
  const roleText = role ? `<span id="${id}-role" class="apr-role">For ${escape(role)}</span>` : "";
  const help = prompt.hints.helpText ? `<small id="${id}-help">${escape(prompt.hints.helpText)}</small>` : "";
  const findings = inspectText(prompt.response);
  const warning = findings.length ? `<small id="${id}-unicode" class="apr-security-warning" role="status">Suspicious Unicode: ${escape(findings.map(f => `${f.code} (U+${f.codepoint.toString(16).toUpperCase().padStart(4, "0")})`).join(", "))}. The stored response was not changed.</small>` : "";
  const describedByIds = [role ? `${id}-role` : "", prompt.hints.helpText ? `${id}-help` : "", findings.length ? `${id}-unicode` : ""].filter(Boolean).join(" ");
  const describedBy = describedByIds ? ` aria-describedby="${describedByIds}"` : "";
  if (!editable) return `<div class="apr-prompt" data-apr-prompt="${escape(prompt.id)}"><dt>${escape(prompt.label)}</dt>${roleText}<dd><bdi>${escape(prompt.response)}</bdi></dd>${help}${warning}</div>`;
  const field = prompt.hints.expectedDataType === "multiline"
    ? `<textarea id="${id}" name="${escape(prompt.id)}" dir="auto"${describedBy}>${escape(prompt.response)}</textarea>`
    : `<input id="${id}" name="${escape(prompt.id)}" type="${inputType(prompt)}" dir="auto" value="${escape(prompt.response)}"${prompt.hints.placeholder ? ` placeholder="${escape(prompt.hints.placeholder)}"` : ""}${describedBy}>`;
  return `<div class="apr-prompt" data-apr-prompt="${escape(prompt.id)}"><label for="${id}">${escape(prompt.label)}</label>${roleText}${field}${help}${warning}</div>`;
}
function tableHtml(document: AprDocument, section: Section, editable: boolean): string {
  // A table section's child sections are its instances, and the first instance's field
  // labels are what every column means. Rendering that as nested groups produces a
  // visual grid: nothing then tells anybody that the second field of row two is an
  // Amount, which is what APR-RENDER-007 forbids.
  const columns = section.sections[0]?.prompts ?? [];
  const columnId = (index: number) => `apr-col-${escape(section.id)}-${index}`;
  const head = `<thead><tr>${columns.map((prompt, index) => `<th scope="col" id="${columnId(index)}">${escape(prompt.label)}</th>`).join("")}</tr></thead>`;
  const body = section.sections.map(row =>
    `<tr data-apr-section="${escape(row.id)}" aria-label="${escape(row.title)}">${row.prompts.map((prompt, index) =>
      `<td${index < columns.length ? ` headers="${columnId(index)}"` : ""}>${promptHtml(document, prompt, editable)}</td>`).join("")}</tr>`).join("");
  return `<table class="apr-table">${columns.length ? head : ""}<tbody>${body}</tbody></table>`;
}

function sectionHtml(document: AprDocument, section: Section, editable: boolean): string {
  const table = section.kind === "table" && section.sections.length > 0;
  const content = [
    ...section.prompts.map(prompt => promptHtml(document, prompt, editable)),
    ...(table ? [tableHtml(document, section, editable)]
              : section.sections.map(child => sectionHtml(document, child, editable))),
  ].join("\n");
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

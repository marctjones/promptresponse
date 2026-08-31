import { readBeta6Stream, resolveBeta6AttestationsAsync, writeBeta6Form, recomputeComputedValues, renderHtml, validate, type AprDocument, type Prompt, type Section, type ValidationIssue } from "../../../typescript/dist/index.js";

const fileInput = document.querySelector<HTMLInputElement>("#apr-file")!;
const loadStatus = document.querySelector<HTMLElement>("#load-status")!;
const panel = document.querySelector<HTMLElement>("#document-panel")!;
const formHost = document.querySelector<HTMLElement>("#form-host")!;
const validationHost = document.querySelector<HTMLElement>("#validation")!;
const documentName = document.querySelector<HTMLElement>("#document-name")!;
const documentSummary = document.querySelector<HTMLElement>("#document-summary")!;
const saveButton = document.querySelector<HTMLButtonElement>("#save-button")!;
const streamPicker = document.querySelector<HTMLSelectElement>("#stream-form")!;
const streamPickerLabel = document.querySelector<HTMLLabelElement>("#stream-picker-label")!;
const streamStatus = document.querySelector<HTMLElement>("#stream-status")!;

let documentModel: AprDocument | undefined;
let streamForms: AprDocument[] = [];
let sourceStem = "completed-form";

function promptsIn(section: Section): Prompt[] {
  return [...section.prompts, ...section.sections.flatMap(promptsIn)];
}

function allPrompts(document: AprDocument): Prompt[] {
  return document.sections.flatMap(promptsIn);
}

function appendIssues(container: HTMLElement, issues: ValidationIssue[]): void {
  const list = document.createElement("ul");
  for (const issue of issues.slice(0, 12)) {
    const item = document.createElement("li");
    item.textContent = `${issue.code} at ${issue.path}: ${issue.message}`;
    list.append(item);
  }
  if (issues.length > 12) {
    const item = document.createElement("li");
    item.textContent = `… and ${issues.length - 12} more.`;
    list.append(item);
  }
  container.append(list);
}

function renderValidation(): void {
  validationHost.replaceChildren();
  if (!documentModel) return;
  const result = validate(documentModel);
  for (const [className, heading, issues] of [
    ["notice error", "This document does not validate.", result.errors],
    ["notice", "Advisories (they do not block saving).", result.warnings],
  ] as const) {
    if (!issues.length) continue;
    const notice = document.createElement("div");
    notice.className = className;
    const strong = document.createElement("strong");
    strong.textContent = heading;
    notice.append(strong);
    appendIssues(notice, issues);
    validationHost.append(notice);
  }
}

function syncResponses(): void {
  if (!documentModel) return;
  const fields = new Map<string, HTMLInputElement | HTMLTextAreaElement>();
  formHost.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>("input[name], textarea[name]").forEach(field => fields.set(field.name, field));
  for (const prompt of allPrompts(documentModel)) {
    const field = fields.get(prompt.id);
    if (field) prompt.response = field.value;
  }
  recomputeComputedValues(documentModel);
  for (const prompt of allPrompts(documentModel)) {
    if (prompt.responseMetadata.source === "computed") {
      const field = fields.get(prompt.id);
      if (field) field.value = prompt.response;
    }
  }
  renderValidation();
}

function showDocument(): void {
  if (!documentModel) return;
  documentName.textContent = documentModel.metadata.title;
  documentSummary.textContent = `APR ${documentModel.version} · ${allPrompts(documentModel).length} prompts · stays in this browser until you save it`;
  formHost.innerHTML = renderHtml(documentModel, { editable: true });
  formHost.querySelector<HTMLFormElement>("form")?.addEventListener("input", syncResponses);
  panel.hidden = false;
  renderValidation();
}

function download(): void {
  if (!documentModel) return;
  syncResponses();
  documentModel.documentType = "filledForm";
  documentModel.metadata.filledDate = new Date().toISOString();
  documentModel.metadata.templateId ||= sourceStem;
  documentModel.version = "1.0-beta.6";
  const blob = new Blob([writeBeta6Form(documentModel, "jsonc") + "\n"], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = Object.assign(document.createElement("a"), { href: url, download: `${sourceStem}.aprf` });
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
  loadStatus.textContent = `Saved ${link.download}.`;
}

fileInput.addEventListener("change", async () => {
  const file = fileInput.files?.[0];
  if (!file) return;
  try {
    const representation = /\.ya?ml$/i.test(file.name) ? "yaml" : "jsonc";
    const records = readBeta6Stream(await file.text(), representation);
    streamForms = records.filter((record): record is { type: "form"; document: AprDocument } => record.type === "form").map(record => record.document);
    if (!streamForms.length) throw new Error("This stream has no form records to display.");
    documentModel = streamForms[0];
    streamPicker.replaceChildren(...streamForms.map((_, index) => Object.assign(document.createElement("option"), { value: String(index), textContent: `Form occurrence ${index + 1}` })));
    streamPicker.hidden = streamPickerLabel.hidden = streamForms.length < 2;
    const attestations = await resolveBeta6AttestationsAsync(records);
    const states = attestations.map(result => result.state).join(", ") || "none";
    streamStatus.textContent = `${records.length} independent record(s): ${streamForms.length} form occurrence(s), ${attestations.length} attestation(s) (${states}). Attestations never block form display.`;
    streamStatus.hidden = false;
    sourceStem = file.name.replace(/\.(apr|aprt|aprf|yaml|yml)$/i, "") || sourceStem;
    loadStatus.textContent = `Opened ${file.name}.`;
    showDocument();
  } catch (error) {
    documentModel = undefined;
    panel.hidden = true;
    loadStatus.textContent = `Could not open ${file.name}: ${(error as Error).message}`;
  }
});

streamPicker.addEventListener("change", () => {
  const index = Number(streamPicker.value);
  if (!Number.isInteger(index) || !streamForms[index]) return;
  documentModel = streamForms[index];
  showDocument();
});

saveButton.addEventListener("click", download);

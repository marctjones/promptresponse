export type JsonValue = string | number | boolean | null | JsonObject | JsonValue[];
export interface JsonObject { [key: string]: JsonValue; }

export interface PromptHints {
  expectedDataType?: string; placeholder?: string; helpText?: string;
  validationPattern?: string; suggestedValues: string[];
  min?: number | string; max?: number | string; step?: number;
  exprHidden?: string; exprValue?: string; exprExpected?: string;
  exprValidation?: string; exprReadOnly?: string;
  extra: JsonObject;
}
export interface Prompt {
  id: string; label: string; response: string; role?: string;
  hints: PromptHints; extra: JsonObject;
  /**
   * Whether this filling session computed the response now in `response`.
   *
   * Not a member, and never written. beta.6 retired `responseMetadata.source`, which
   * tried to carry this between parties: it rested a prohibition on a marker every
   * reader was free to drop. Every non-empty response in a document as it was read is
   * authored, whatever produced it, so what may be recomputed is a fact about this
   * session rather than about the file.
   */
  computedInThisSession?: boolean;
}
export interface Section {
  id: string; title: string; description?: string; kind?: string;
  canAddRows?: boolean; maxRows?: number; role?: string;
  prompts: Prompt[]; sections: Section[]; extra: JsonObject;
}
export interface RoleDefinition { id: string; name?: string; description?: string; extra: JsonObject; }
export interface Metadata {
  title: string; description?: string; author?: string; created?: string; modified?: string;
  templateId?: string; templateVersion?: string;
  publisher?: string; submissionUrls?: string[]; extra: JsonObject;
}
export interface AprDocument {
  version: string; documentType?: string; metadata: Metadata; sections: Section[];
  roles?: RoleDefinition[]; extra: JsonObject;
}

export const RETIRED_MEMBERS = new Set([
  // Table column presentation, removed before 1.0.
  "tableLayout", "columns", "fixedRows",
  // Workflow state, retired in beta.6. Dropped rather than preserved into `extra`:
  // none carried a claim whose silent loss would be worse than its removal, and
  // preserving them would write them back into a document the format says has none.
  "responseMetadata", "filledBy", "filledDate",
]);

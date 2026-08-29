export type JsonValue = string | number | boolean | null | JsonObject | JsonValue[];
export interface JsonObject { [key: string]: JsonValue; }

export interface PromptHints {
  expectedDataType?: string; placeholder?: string; helpText?: string;
  validationPattern?: string; suggestedValues: string[];
  min?: string; max?: string; step?: string;
  exprHidden?: string; exprValue?: string; exprExpected?: string;
  exprValidation?: string; exprReadOnly?: string;
  extra: JsonObject;
}
export interface ResponseMetadata {
  inferredDataType?: string; source?: string; lastModified?: string; extra: JsonObject;
}
export interface Prompt {
  id: string; label: string; response: string; role?: string;
  hints: PromptHints; responseMetadata: ResponseMetadata; extra: JsonObject;
}
export interface Section {
  id: string; title: string; description?: string; kind?: string;
  canAddRows?: string; maxRows?: string; role?: string;
  prompts: Prompt[]; sections: Section[]; extra: JsonObject;
}
export interface RoleDefinition { id: string; name?: string; description?: string; extra: JsonObject; }
export interface Metadata {
  title: string; description?: string; author?: string; created?: string; modified?: string;
  templateId?: string; templateVersion?: string; filledBy?: string; filledDate?: string;
  publisher?: string; submissionUrls?: string[]; extra: JsonObject;
}
export interface AprDocument {
  version: string; documentType?: string; metadata: Metadata; sections: Section[];
  roles?: RoleDefinition[]; signatures?: JsonValue[]; extra: JsonObject;
}

export const RETIRED_MEMBERS = new Set(["tableLayout", "columns", "fixedRows"]);

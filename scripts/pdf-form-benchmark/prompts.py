"""Per-model-strategy prompt text.

Each of the 4 models has a fundamentally different output interface, so each
gets the prompt its own maintainers recommend rather than one generic prompt
forced onto all of them:
  - doctags:            IBM's own one-line Granite-Docling instruction.
  - dots-ocr-native:     dots.ocr's own documented layout-JSON prompt, verbatim.
  - florence-detection:  Florence-2's fixed task-token interface (not a chat
                         prompt at all -- the token IS the instruction).
  - chat-json:           our own instruction for the one true generalist chat
                         model, asking directly for an APR-shaped intermediate.
"""

DOCTAGS_PROMPT = "Convert this page to docling."

# Verbatim from dots.ocr's model card (dots-studio/dots.ocr) recommended
# layout-parsing prompt.
DOTS_OCR_PROMPT = """Please output the layout information from the PDF image, including each layout element's bbox, its category, and the corresponding text content within the bbox.
1. Bbox format: [x1, y1, x2, y2]
2. Layout Categories: The possible categories are ["Caption", "Footnote", "Formula", "List-item", "Page-footer", "Page-header", "Picture", "Section-header", "Table", "Text", "Title"].
3. Text Extraction & Formatting Rules:
    - Picture: For the "Picture" category, the text field should be omitted.
    - Formula: Format its text as LaTeX.
    - Table: Format its text as HTML.
    - All Others (Text, Title, etc.): Format their text as Markdown.
4. Constraints:
    - The output text must be the original text from the image, with no translation.
    - All layout elements must be sorted according to human reading order.
5. Final Output: The entire output must be a single JSON object."""

# Florence-2's task-token interface: the "prompt" is a fixed vocabulary
# token, not natural language. <OCR_WITH_REGION> gives per-span text + a
# quad-box, which is the closest Florence-2 task to what we need.
FLORENCE_TASK_TOKEN = "<OCR_WITH_REGION>"

CHAT_JSON_SCHEMA_PROMPT = """You are looking at one page of a government form. Identify every section heading and every fillable field (blank, checkbox, line, or labeled space a person is meant to write in) that is VISIBLE ON THIS PAGE ONLY. Do not invent fields that aren't there, and do not list purely instructional/explanatory text as a field.

Respond with ONLY a single JSON object, no prose before or after, matching this exact shape:

{
  "sections": [
    {"id": "kebab-case-id", "title": "Section title as printed, or a sensible name if the page has no heading"}
  ],
  "fields": [
    {
      "label": "The exact visible label/question text",
      "section_id": "id of the section above this field belongs to",
      "field_kind": "text_line | multiline | checkbox | choice | signature | date_field | table_cell",
      "expected_data_type": "text | multiline | email | phone | url | number | currency | date | time | datetime | boolean",
      "required": true,
      "choices": ["only present if field_kind is choice"]
    }
  ]
}

Rules:
- A group of mutually-exclusive checkboxes for one question (e.g. a tax-classification checkbox row) is ONE field with field_kind "choice" and a choices array -- not one field per box.
- checkbox/yes-no -> expected_data_type "boolean".
- If the page has no real heading, use one section named after what the page visibly is.
- required is your best guess from asterisks/"required"/"must" language, or true for anything clearly load-bearing (name, signature). Use false if genuinely optional or unclear.
- Output strict JSON: double-quoted keys/strings, no trailing commas, no comments."""


def build_prompt(strategy: str) -> str:
    if strategy == "doctags":
        return DOCTAGS_PROMPT
    if strategy == "dots-ocr-native":
        return DOTS_OCR_PROMPT
    if strategy == "florence-detection":
        return FLORENCE_TASK_TOKEN
    if strategy == "chat-json":
        return CHAT_JSON_SCHEMA_PROMPT
    raise ValueError(f"unknown strategy: {strategy!r}")

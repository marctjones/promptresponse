# Renderer conformance

<!-- AI-ASSISTANT-README -->
Read this before changing a renderer, the renderer harness, or chapter 13 of the
specification. It defines what a renderer reports about itself so that a rule
about rendering can pass or fail, and it is derived from the specification: where
the two disagree, the document is right.
<!-- END-AI-ASSISTANT-README -->

## Why a second contract

The document driver contract in [SDK_CONFORMANCE](SDK_CONFORMANCE.md) asks what an
implementation did with a document: did it accept it, what digest did it compute,
what did it warn about, what did it write back. Twelve rules cannot be asked that
way.

No file is valid or invalid because of how a renderer behaves. `APR-RENDER-001`
says a section title must be presented as the accessible name; every document is
equally valid whether a renderer does that or not. Nothing in `outcome`,
`diagnostic`, `digest`, `warnings`, `evaluated` or `written` can carry what a
person sees or reaches, so those twelve rules were recorded as permanently
unreachable and the milestone that closed them recorded gaps rather than evidence.

They are not unreachable. They are reachable by asking a different question: not
*what did you do with this document*, but **what did a person end up in front
of.** That is what a renderer driver reports, and this contract is its shape.

## What a renderer driver is

A program that, given a document and a viewport, renders it and writes an
**interaction snapshot** — a description of the resulting interface in terms that
mean the same thing on every platform.

    apr-render-driver < renderer-suite.json > results.json

The snapshot is not a screenshot and not a widget tree. A screenshot cannot be
compared across platforms and a widget tree is the renderer's own vocabulary. A
snapshot is what an assistive technology would be told, which is exactly the level
chapter 13 legislates at.

## The snapshot

One node per interactive or structural element, in the order a keyboard visits
them. Every field is something the accessibility layer of a real platform already
carries — nothing here is invented for the harness.

```json
{
  "nodes": [
    {
      "id": "applicant",
      "role": "group",
      "name": "Applicant",
      "headingLevel": 1,
      "documentPointer": "/sections/0",
      "keyboardOrder": 0
    },
    {
      "id": "full_name",
      "role": "textbox",
      "name": "Full name",
      "helpText": "As it appears on your passport",
      "labelledBy": "full_name_label",
      "documentPointer": "/sections/0/prompts/0",
      "keyboardOrder": 1,
      "editable": true,
      "value": "Ada Lovelace"
    }
  ],
  "saveResult": { "written": true, "blockedBy": null },
  "exportedDocument": null,
  "requests": []
}
```

| Field | Carries | Why it is here |
| --- | --- | --- |
| `role` | group, heading, textbox, checkbox, combobox, table, row, cell, columnheader, button | The platform's own control type, normalised |
| `name` | the accessible name | `APR-RENDER-001`, `APR-RENDER-002` |
| `headingLevel` | 1-6, or absent | `APR-RENDER-004` — structure conveyed, not indentation |
| `helpText` | text programmatically associated | `APR-RENDER-003` — associated, not adjacent |
| `labelledBy`, `columnHeader` | the element that names this one | `APR-RENDER-003`, `APR-RENDER-007` |
| `documentPointer` | RFC 6901 pointer into the document | Ties every node to what it renders, so `APR-RENDER-008` is checkable |
| `keyboardOrder` | position in the focus order, or absent if unreachable | `APR-RENDER-005` |
| `editable` | whether typing is accepted | `APR-EXPR-014` — a computed field stays editable |
| `saveResult` | whether a save was written, and what blocked it | `APR-RENDER-006`, `APR-MODEL-004`, `APR-VAL-006` |
| `exportedDocument` | the document as it stands after an export | `APR-RENDER-009` |
| `requests` | every network request made while rendering | `APR-SEC-010` |

### Header association is a property, not a pattern

`APR-RENDER-007` is expressed with `columnHeader`, `isRowHeader`, `isColumnHeader`
and `positionInSet` — never with a grid or table *pattern*.

This is a finding, not a preference. Avalonia carries `IsColumnHeader`,
`IsRowHeader`, `LabeledBy`, `PositionInSet` and `SizeOfSet` in
`Avalonia.Controls`, on every backend. The UIA pattern interfaces
`IGridProvider`, `IGridItemProvider`, `ITableProvider` and `ITableItemProvider`
exist **only** in `Avalonia.Win32.Automation`; the macOS and X11 backends
reference none of them. A contract written against `IGridItemProvider` would be a
Windows-only contract wearing a platform-neutral name.

## What the twelve rules become

| Rule | Checked by |
| --- | --- |
| `APR-RENDER-001` | every node whose pointer names a section or prompt has a non-empty `name` equal to its title or label |
| `APR-RENDER-002` | a prompt with a placeholder and no label has no node, because the document is invalid; a prompt with both takes its `name` from the label |
| `APR-RENDER-003` | a prompt with `helpText` has it on the node or reachable through `labelledBy`, not merely as a neighbouring text node |
| `APR-RENDER-004` | nested sections produce nodes whose `headingLevel` increases with depth, or groups that nest — indentation alone produces neither |
| `APR-RENDER-005` | every prompt's node has a `keyboardOrder` |
| `APR-RENDER-006` | after a response contradicting `expectedDataType`, `saveResult.written` is true |
| `APR-RENDER-007` | every cell node carries `columnHeader` naming a node whose `isColumnHeader` is true |
| `APR-RENDER-008` | `keyboardOrder` is non-decreasing in document-pointer order |
| `APR-RENDER-009` | `exportedDocument` is byte-identical to the input — **the PDF exporter's driver**, not Avalonia's, since Avalonia does not export |
| `APR-SEC-009` | no case produces an execution: a document carrying script-looking text renders it as text |
| `APR-SEC-010` | `requests` is empty for every case that involves no explicit user action |
| `APR-SEC-011` | a sixteen-level document renders; a deeper one refuses cleanly rather than exhausting memory |
| `APR-MODEL-004`, `APR-VAL-006` | `saveResult.written` is true even where a hint mismatch or advisory is present |

## What this contract does not decide

**Whether a renderer is any good.** Legibility, contrast against a real
background, whether a screen reader's utterance is comprehensible, whether the
tab order is *sensible* rather than merely present. Those are the human-judgement
residue, and naming them is a separate deliverable — a contract that quietly
claimed them would be worse than one that says where it stops.

**Whether a platform actually emits these.** The properties exist in Avalonia's
API on every backend; that they reach AT-SPI, NSAccessibility and UIA at runtime
is an assumption stated here and tested by the native snapshot drivers, not by
this contract.

## The harness has teeth or it has nothing

Same rule as the document harness. A renderer driver that reports a plausible
snapshot without rendering must fail, and mutant drivers prove it: one that omits
`name`, one that flattens `headingLevel`, one that drops `keyboardOrder` on a
prompt, one that returns `saveResult.written = false` after an advisory, one that
reports an empty `requests` while fetching. A mutant that passes every case is a
defect in the harness, not in the mutant.

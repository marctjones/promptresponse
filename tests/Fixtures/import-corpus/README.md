# Import corpus — real-world form fixtures

Golden `.aprt` outputs from running both import paths against two real federal
forms. Seeds [issue #64](https://github.com/marctjones/promptresponse/issues/64)
(test the importer + skill against GSA/IRS/municipal forms).

Source PDFs are U.S. federal government works (public domain): IRS Form 990
(`irs.gov/pub/irs-pdf/f990.pdf`) and SF-86 (`opm.gov`). The PDFs themselves are
**not** committed — only the derived `.aprt`. Re-fetch the PDFs to regenerate.

## What's here

| File | Path | Source path / approach |
|------|------|------------------------|
| `irs-990-skill.aprt` | skill | Header (A–M) + Part I Summary + revenue table, hand-authored by reading the printed form (the `document-to-apr` skill) |
| `sf86-skill.aprt` | skill | Certification + Sections 1–6, authored from the printed form |
| `irs-990-imported-sample.aprt` | importer | First page only, from `apr import f990.pdf` (full run = 12 sections / 1,075 prompts) |
| `sf86-imported-sample.aprt` | importer | First page only, from `apr import sf86.pdf` (full run = 132 sections / 6,197 prompts) |

## Findings (importer vs skill)

The mechanical importer's quality hinges entirely on whether the PDF's AcroForm
fields carry **tooltips** (`/TU`, the accessible name):

- **SF-86 — tooltips present.** All 6,197 fields imported with human labels
  ("First name", "Middle name", "Suffix", "Section 1. Full Name…"). The importer
  is genuinely useful here; the skill mainly improves *grouping* (real Section
  1–6 structure vs one flat section per PDF page) and field *types* (e.g. height
  as numbers, sex as a dropdown).
- **IRS 990 — no tooltips.** Every field fell back to its raw name (`f1_1[0]`,
  `c1_1[0]`) — structurally valid but semantically useless. The skill, reading
  the printed labels, recovers real questions ("Name of organization", "Employer
  identification number", Part I lines) and the Prior/Current-Year revenue grid
  as a fixed table.

### Importer follow-ups surfaced (for #64)

- Radio-button groups import as a generic `boolean` labelled "RadioButtonList" —
  should become a `Choice`/`suggestedValues` field with the option labels.
- Sectioning is per-PDF-page; the form's own Part/Section structure is richer.
- Long tooltip strings (full instruction sentences) become long labels.
- No handling of conditional/computed structure (expected — that's semantic).

### Skill limitation surfaced

- A whole *table section* can't be conditionally hidden (e.g. SF-86 Section 5's
  names table only applies if "used other names" = Yes): `exprHidden` is a
  prompt-level hint, not a section-level one. Captured for a possible
  section-level conditional feature.

All four `.aprt` here pass `apr validate` and export to fillable PDF/HTML.

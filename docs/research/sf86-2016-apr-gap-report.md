# SF-86 (November 2016) APR comparison

## Scope and method

This is a comparison of the local example
[`examples/sf-86-background-check.aprt`](../../examples/sf-86-background-check.aprt)
with the Office of Personnel Management's *Standard Form 86, Questionnaire for
National Security Positions*, revised November 2016.  The reproducible source
text, heading index, and machine-readable outline are adjacent to this report:

- [`sf86-2016-official-extracted.txt`](sf86-2016-official-extracted.txt)
- [`sf86-2016-official-structured.md`](sf86-2016-official-structured.md)
- [`sf86-2016-official-structure.json`](sf86-2016-official-structure.json)

The report records questionnaire fidelity, not an APR format change.  APR is
frozen under [`SPECIFICATION_FREEZE.md`](../SPECIFICATION_FREEZE.md).

## Result

The current APR file is a short demonstrator.  It is **not** an SF-86
transcription, and its ten numbered content groups do not correspond to the
official form's numbered sections.  In particular, its "Section 10 - Drug and
Alcohol Use" is incorrect: official section 10 is *Dual/Multiple Citizenship &
Foreign Passport Information*; drug activity is section 23 and alcohol is
section 24.

| Official section(s) | Official title | Current APR coverage |
| --- | --- | --- |
| 1–6 | Identity information | Partial and incorrectly merged into `section_1` |
| 7 | Your Contact Information | Missing |
| 8–10 | Passport and citizenship | Partial and incorrectly merged into `section_2` |
| 11 | Where You Have Lived | Partial (`section_3`); only two fixed residences |
| 12 | Where You Went to School | Partial and out of order (`section_5`) |
| 13A–13C | Employment Activities / records | Partial and out of order (`section_4`) |
| 14–15 | Selective Service / Military History | Missing |
| 16 | People Who Know You Well | Partial (`section_6`) |
| 17–18 | Marital/Relationship Status / Relatives | Partial and incorrectly merged (`section_7`) |
| 19–20C | Foreign Contacts, Activities, Business, Travel | Partial and incorrectly merged (`section_8`) |
| 21 | Psychological and Emotional Health | Missing |
| 22 | Police Record | Partial and incorrectly merged (`section_9`) |
| 23–24 | Illegal Drug Use / Use of Alcohol | Partial and incorrectly merged and numbered `section_10` |
| 25 | Investigations and Clearance Record | Missing |
| 26 | Financial Record | Partial and incorrectly merged (`section_9`) |
| 27 | Use of Information Technology Systems | Missing |
| 28 | Involvement in Non-Criminal Court Actions | Partial and incorrectly merged (`section_9`) |
| 29 | Association Record | Missing |

## Prompt-level findings

The existing prompts are useful as a lightweight demonstration but are not
officially complete or faithfully scoped:

- Identity omits, among other details, county of birth, separate feet/inches,
  identifying-mark fields, and the full structured other-name history.
- Residence supplies two fixed addresses rather than the required history and
  omits the official conditional questions and reference cases.
- Education provides one school instead of the official history and omits
  associated questions.
- Employment supplies two generic employers, but does not model the distinct
  13A, 13B, and 13C sets or the official chronology and conditional detail.
- References, family, foreign activity, legal, financial, drug, and alcohol
  prompts collapse many separate official questions into broad yes/no or free
  text placeholders.  They cannot be treated as equivalent answers to the
  official prompts.
- The front matter, page directions, and many conditional instruction blocks
  are not represented as text shown to the user.

## Why the frozen format cannot yet carry a perfect conversion

APR's current section model deliberately requires every ordinary section to
contain a prompt or a child section.  It has `description` for prose attached
to a content-bearing section, but no semantically read-only "information
block" node.  Therefore, a prose-only official direction can be faithfully
displayed today only as the `description` of the related response-bearing
section.  It must not be manufactured as an empty section, because that would
violate the frozen format and distort progress navigation.

The frozen-format route for a future human-authorized transcription is to use
the exact official section hierarchy; attach each instruction paragraph to the
related content-bearing section's `description`; and represent actual answer
fields as prompts, using repeating table sections where the official form has
repeating records.  This is a rendering compromise, not a claim that APR has a
first-class informational-content feature.

Until that transcription is independently reviewed against the official form,
the example should remain explicitly labelled as a simplified demonstration,
not as an official or complete SF-86.

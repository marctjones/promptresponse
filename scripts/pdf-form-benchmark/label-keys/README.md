# Label keys

Hand-read answer keys for "what question does this field ask", for forms whose
authors wrote no `/TU` tooltips.

## Why these exist, and what they are worth

Label recovery has one free oracle: a form author's own `/TU` text, which is the
right answer by definition. It covers 152 of the corpus's 444 fields, and they
are concentrated in three forms — `fed-i9` (128), `ct-w4` (14 meaningful) and
`ct-dmv-a25` (10, all `TextField1` and therefore useless).

`fed-w9`, `fed-8822` and `fed-ss4` have **zero** between them — 137 fields with
no oracle. `fed-w9` is also the form the label gate is built around, so label
recovery was being developed against no measurement at all on its primary
fixture. That is worse than a weak key.

## Held out

Every pairing rule in the converter was derived from `fed-w9` and `fed-i9`.
Those two therefore say only that the rules fit the forms they were fitted to.
`fed-8822` and `fed-ss4` were keyed afterwards and have never been tuned
against, which makes them the only evidence that the rules describe forms
rather than describing two documents:

| form | fields | recall | precision | chance floor |
|---|---|---|---|---|
| `fed-w9` (fitted on) | 23 | 83% | 90% | 17% |
| `fed-8822` (held out) | 25 | 80% | 95% | 12% |
| `fed-ss4` (held out) | 89 | 63% | 86% | 6% |

`fed-8822` holding up is the result worth having. `fed-ss4` dropping is the
honest cost: it is the densest form in the corpus, 89 fields with most of them
tick boxes in tight grids, and no rule here was shaped by anything like it.

## Writing a key

Record **what the form prints beside the field**, not what a good conversion
ought to say. The first draft of `fed-ss4.json` prefixed every tick box with its
group heading — "Type of entity (check only one box): Partnership" — which
marked a correct recovery of "Partnership" as wrong and understated recall by
seven points. Group context is a section concern, not a label one, and a key
that encodes it is grading the converter for something the page does not print
where the field is.

## What tier this is

**Model-authored, not definitional and not human-verified.** The labels were
transcribed by Claude from a 160 DPI render of the page. That is the same kind
of act the converter performs, judged by the same kind of judge, so it is
categorically weaker evidence than a tooltip and must never be reported as
equivalent. The `tier` field in each file says so, and a test asserts it still
does.

What keeps it from being circular:

- Read from the **rendered image**, not from the converter's text extraction and
  not from any pipeline output. The converter is not grading itself.
- Keyed to **widget rectangles**, which the PDF states outright (Tier 1). A
  field rename breaks the key rather than silently skipping fields.
- Scored with the same **chance floor** control as the tooltip oracle: each
  label is also scored against the *next* field's, and the shifted score must
  come out well below the aligned one. On `fed-w9` that is 17% against 57%.

A human review of `fed-w9.json` would promote it to a genuine Tier 3 key. Until
then, treat its numbers as indicative and treat `fed-i9`'s as evidence.

# pdf2apr

Convert a PDF form into a [PromptResponse](../README.md) **APR template**
(`.aprt`) — a small, layout-free JSON document of sections and questions.

```console
$ pdf2apr w9.pdf
Reading w9.pdf … the model runs locally and may take a minute.
  detect    23 blank(s) across 1 page(s)
  mark      1 page(s) rendered at 150 DPI, every blank numbered
  name      22 question(s) from 23 blank(s); 5 box(es) joined into shared questions
  assemble  2 section(s), 22 prompt(s)
            took 47s
Wrote w9.aprt — 22 questions from 23 fields.
```

Nothing leaves the machine. The model is open-weight and runs locally.

## Install

```console
pip install -e .            # deterministic conversion
pip install -e '.[model]'   # …and the naming model (Apple silicon)
```

You also need `pdftoppm` (`brew install poppler`) and the `convert-pdf` tool
from this repository. If `convert-pdf` is not on your `PATH`, point at it with
`PDF2APR_CONVERT_PDF=/path/to/convert-pdf`; from a checkout it is found
automatically.

## How it works, and why it is split this way

```
1. detect    the form's own fields, or the lines the page draws
2. mark      each page rendered, every blank outlined and numbered
3. name      a local vision model says what each numbered blank asks for
4. assemble  the APR template
```

**Stage 1 is geometry, and no model can do it.** A blank is the *absence* of
ink. A model that reads a page has nothing to read where the field is: asked to
transcribe a W-9, IBM's Granite-Docling returns 191 text elements and nothing at
all where the 23 blanks are.

**Stage 3 is reading, and no rule does it as well.** Deciding what a blank asks
for — and which blanks are one question the form split into three boxes — is
judgement about a page. A hand-written pipeline scored 0.43 against a 4B model's
0.71 on the same forms.

So each stage does the half it is good at, and stage 3 is shown stage 1's answer
rather than asked to work it out. The model is **never asked to find fields**,
only to name the ones already found.

## Measured

Mean F1 against hand-authored ground truth, four benchmark forms:

| approach | mean F1 |
|---|---|
| **this pipeline** | **0.79** |
| the model alone, given the page | 0.71 |
| the deterministic pipeline alone | 0.58 |

The gain is largest where a form has many boxes and few questions. USCIS Form
I-9 has 128 fillable widgets and 44 real questions; naming them from the page
collapses 128 boxes into 53 prompts and takes F1 from 0.28 to 0.78.

### Two findings worth keeping

**Guiding caps recall.** The model only names what stage 1 found, so where
detection under-reads, guiding hides fields the model would have spotted alone.
On CT-W4 the deterministic pass finds 19 blanks against 25 real questions, and
guiding takes a perfect 1.00 down to 0.77.

**Inviting the model to add fields makes it worse; accepting the ones it offers
does not.** The obvious fix — "add any field you see that has no box" — drops
the mean from 0.79 to **0.70**, below the unaided model, because permission to
add also stops it grouping: I-9 went from 53 prompts to 103.

Left alone, though, it adds a few anyway and keeps grouping. On W-9 it returns
26 entries for 23 boxes, numbering the signature and date lines 24, 25 and 26 —
questions genuinely printed on the page that the form declares no widget for.
Those are right, and discarding them as out of range cost four points of F1. So
extras are accepted when offered and never solicited, and they arrive as
questions with no blank attached: APR needs no geometry, so there is simply
nowhere on the source page to point at.

## Using it as a library

```python
from pdf2apr import convert

result = convert("w9.pdf")
result.document        # the APR template, as a dict
result.questions       # what was asked, and which blanks answer each
result.blanks          # where every field sits, in PDF user space
print(result.report)   # what each stage did
```

Each stage stands alone, and `Namer` is an interface — implement it to use a
different runtime or model:

```python
from pdf2apr import detect_blanks, mark_pages, build_document, Namer

blanks = detect_blanks("w9.pdf")
pages  = mark_pages("w9.pdf", blanks, into=Path("./pages"))

class MyNamer(Namer):
    def name_page(self, page): ...
```

`Options(use_model=False)` gives the deterministic labels alone: instant,
reproducible, and measurably weaker.

## Looking at what it did

`--show-pages DIR` keeps the images the model was shown, with every blank
outlined and numbered. When a label looks wrong, that picture usually explains
it in a second — and it is the fastest way to see whether stage 1 missed a
field or stage 3 misread one.

`--fields FILE` writes where each blank sits. APR itself is layout-free and
carries no coordinates; this is the side channel for anything that needs to
point back at the page.

## Limits

- **Apple silicon** for the model stage. `--no-model` works anywhere.
- **A scanned page yields nothing.** With no text layer *and* no vector lines,
  stage 1 finds no blanks and there is nothing to number. A pure-vision route
  would be a different tool.
- **Four forms is a small sample.** The numbers above come from the benchmark
  corpus in `scripts/pdf-form-benchmark/`, and three of the four are forms the
  deterministic stage was tuned against.
- **Check the output.** A converted form is a draft. The model reads the page
  the way a person does, including when it reads it wrong.

## Security

The form is untrusted input, and its text ends up in a document other people
open. `pdf2apr` copies what the page says into labels; it never follows it. APR
has no executable content by design and this tool adds none — no scripts, no
actions, no external references, whatever the source contains. See the
"Safety, and what to report" section of `.claude/skills/document-to-apr/SKILL.md`
for what to do when a source looks like it is trying to instruct the reader.

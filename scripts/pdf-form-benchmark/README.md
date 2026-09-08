# PDF-form-to-APR open-weight VLM benchmark

Measures how well open-weight vision-language models convert a real
government PDF form into a PromptResponse APR template, running entirely
locally via [MLX](https://github.com/ml-explore/mlx) on Apple Silicon.

## Why this exists

The `document-to-apr` skill (`.claude/skills/document-to-apr/`) does this
conversion by handing the raw PDF/image to whatever agent is running (Claude,
Gemini CLI, Codex, ...) and trusting it to read the form and reason about
structure. This benchmark asks a different question: if you wanted to do the
same conversion **offline, locally, with a small open-weight model** instead
of a large hosted agent, which model would you use, and how good would the
result actually be? See the conversation that produced this harness for the
full model survey; this directory is the empirical follow-up.

## What's here

```
corpus_manifest.json   11 real PDF forms (5 federal, 6 Connecticut), verified URLs
corpus/                the downloaded PDFs
ground_truth/          hand-verified {sections, fields} per form -- the answer key
models.json            the 4 models under test, their MLX repo, and prompting strategy
prompts.py             each model's own recommended/native prompt (not one generic prompt)
parsers.py             raw model output -> shared intermediate representation (IR)
apr_schema.py          IR -> a strictly valid .aprt (same builder for every model)
validate_apr.py        shells out to this repo's own `apr` CLI -- the one source of truth
render_pdf.py          PDF -> page PNGs (poppler)
fetch_corpus.py        downloads corpus_manifest.json's PDFs
download_models.py     pre-downloads every model in models.json
run_benchmark.py        orchestrator: load each model once, run every form, validate
score.py               compare results/aprt/*/*.aprt against ground_truth/*.json
results/               raw model output, generated .aprt files, scorecard, report
```

## Running it

```bash
python3 -m venv .venv
.venv/bin/pip install mlx mlx-vlm huggingface_hub pillow torch torchvision
.venv/bin/python3 fetch_corpus.py
.venv/bin/python3 download_models.py      # ~12GB
.venv/bin/python3 run_benchmark.py        # takes 60-90+ min; loads each model once
.venv/bin/python3 score.py                # writes results/REPORT.md and scorecard.json
```

`run_benchmark.py --models <id> --forms <id>` runs a subset. It skips
(model, form) pairs that already have a `.aprt` in `results/aprt/`, so a
crashed or interrupted run resumes where it left off; add `--force` to redo.

## The 4 models

Chosen to span the real design space, not just four sizes of one family --
see `models.json` for exact repos and notes:

- **Granite-Docling-258M** (IBM) -- tiny document-structure specialist,
  emits `DocTags` markup with a bounding box per element.
- **Qwen3-VL-8B-Instruct** (4-bit) -- the one generalist chat model, asked
  directly for an APR-shaped intermediate JSON.
- **dots.ocr** -- an OCR/layout specialist (1.2B ViT + 1.7B LLM), native JSON
  output of `{bbox, category, text}` per element. No genuine MLX build
  existed on the Hub for this one (the only "-4bit" upload found was
  bitsandbytes-quantized, incompatible with mlx-vlm), so it was converted
  in-house from `dots-studio/dots.ocr` via `mlx_vlm convert --quantize`.
- **Florence-2-base-ft** (4-bit) -- a pure grounding/detection specialist
  with a fixed task-token interface (`<OCR_WITH_REGION>`), not a chat model.
  `Florence-2-large-ft` (both the 4-bit and bf16 builds) hit a **confirmed
  mlx-vlm compatibility bug** as of mlx-vlm 0.7.0 (Sept 2026) -- it only ever
  emits the BOS token, verified empirically, not a prompting issue. `base-ft`
  is the variant that actually works, so it stands in for the family here.
  Worth re-testing once mlx-vlm fixes this upstream.

## Methodology notes (read before trusting the numbers)

- **None of these four models natively marks "this is the blank the answer
  goes in."** That's absence of ink, not content, and none of them detect it
  directly. What they give us, in varying quality, is *what text exists* and
  *some notion of structural role* (DocTags/dots.ocr tag categories; Qwen's
  own judgment since it was asked directly; Florence-2 gives text spans with
  no role at all). So for the three non-instructed models, a shared heuristic
  in `parsers.py` (`_looks_like_label`) decides which OCR'd text spans are
  field labels versus page furniture -- that heuristic is real, non-trivial
  scoring infrastructure, not a rubber stamp, and it's identical across all
  three so it can't favor one of them.
- **Every model's output goes through the same `apr_schema.py` builder.**
  No model is asked to produce valid APR JSON directly (only Qwen even
  attempts JSON, and it's an intermediate schema we invented, not real APR).
  This keeps the benchmark measuring conversion judgment -- did it find the
  right sections/fields/labels/types -- rather than which model best
  remembers APR's JSON-Schema hard rules.
- **Validation uses this repo's actual `apr` CLI**, not a reimplemented
  checker, per this repo's own principle that a derived check disagreeing
  with the authority is itself the defect.
- **Field matching is fuzzy** (difflib similarity on normalized labels,
  threshold 0.55 in `score.py`), not exact string equality, so minor label
  paraphrasing isn't scored as a false positive/negative.
- **Ground truth was built by an AI agent reading each PDF**, not a human,
  though with explicit instructions to be conservative and flag ambiguous
  calls -- see `ground_truth/README.md` for the specific judgment calls
  worth a second look (e.g. how checkbox groups were counted, whether
  worksheet-only pages count as in-scope fields).
- **Every model saw every rendered page**, including instruction/worksheet
  pages bundled into the real government PDFs (e.g. the W-9's PDF is 6 pages,
  mostly instructions). This is deliberate: a model that can't tell "this
  page is instructions, not fields" from "this page has the actual form"
  is a real failure mode worth measuring, not an artifact to filter out
  before scoring.
- **Two real bugs were found and fixed while building this harness**, not
  model-behavior findings: (1) a regex that treated the outer `<doctag>`
  wrapper as one opaque self-matching tag, silently discarding every inner
  element whenever Granite-Docling's generation completed cleanly; (2) a
  naive `max_tokens` budget that truncated Qwen3-VL mid-JSON on dense forms,
  which then failed to parse and silently zeroed out that entire page. Both
  are described in `parsers.py`/`run_benchmark.py` comments; flagging them
  here so a future reader of the results doesn't mistake harness bugs for
  model quality.

## Known limitations of this benchmark itself

- 11 forms is a real but small corpus; treat results as directional, not
  as a definitive leaderboard.
- The label-classification heuristic, while shared fairly across models,
  is still a heuristic -- it will sometimes call real instructional text a
  field, or miss a genuine field with unusual phrasing.
- Timing numbers are single-run wall-clock on one Mac; they say "roughly
  this ballpark," not "reproducible to the second."

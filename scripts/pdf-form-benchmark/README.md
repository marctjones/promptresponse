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
corpus_manifest.json   19 real PDF forms, verified URLs, each tagged with its role
                       and its measured composition (hasAcroForm / hasTextLayer)
corpus/                the downloaded PDFs
ground_truth/          hand-verified {sections, fields} per form -- the answer key
models.json            the models under test, their MLX repo, and prompting strategy
prompts.py             each model's own recommended/native prompt (not one generic prompt)
parsers.py             raw model output -> shared intermediate representation (IR)
apr_schema.py          IR -> a strictly valid .aprt (same builder for every model)
validate_apr.py        shells out to this repo's own `apr` CLI -- the one source of truth
render_pdf.py          PDF -> page PNGs (poppler)
fetch_corpus.py        downloads corpus_manifest.json's PDFs
download_models.py     pre-downloads every model in models.json
resource_guard.py      memory caps + a live watchdog -- see "Resource safety" below
run_benchmark.py       supervisor: spawns worker.py once per (model, form), enforces
                       a timeout and memory floor, is the only entry point you should run
worker.py              loads ONE model, runs it over the forms it's given -- never run
                       this directly for a real benchmark; it has no safety net of its own
score.py               compare results/aprt/*/*.aprt against ground_truth/*.json
results/               raw model output, generated .aprt files, scorecard, report
```

## The corpus has two halves, for two different jobs

`corpus_manifest.json` tags each form with a `role`:

- **`model-benchmark`** (11 forms: 5 federal, 6 Connecticut) — what the model
  comparison below was scored on. Ground truth exists for these.
- **`converter-development`** (8 forms, Town of Bloomfield CT) — added later,
  for the deterministic converter. No ground truth yet.

The split exists because measuring the original 11 with `PdfSourceDetector`
turned up something that undercut them as converter fixtures: **all 11 carry an
AcroForm.** Every document the vision models were asked to read visually could
have been imported mechanically instead. That does not invalidate the model
scores — they measure visual reading, which is what those models do — but it
means the corpus contained no example of the converter's actual target case, a
form with no form fields at all.

The eight Bloomfield forms fill that gap: municipal print-and-fill forms,
0 AcroForm fields, 577–6,459 characters of real text, 1–4 pages. Each was
vetted with the detector before being added rather than assumed non-fillable —
two further candidates were rejected for turning out to have AcroForms.

One form is worth knowing about individually: **`ct-dmv-a25` is a scan with
real AcroForm widgets laid over it** — 0 extractable characters and 10
importable fields. It is the case a binary "is this scanned?" check gets wrong,
and the only no-text-layer example available.

## Resource safety (read this before running it)

An earlier version of this harness ran every model in one long-lived process
and, once, an ad hoc second smoke-test process alongside it. On this
machine (a 24GB-RAM MacBook Air also running Claude Desktop and several
concurrent Claude Code sessions), that took the whole machine down --
`uptime`/`last reboot` and a `shutdown_stall` diagnostic report from that
timestamp confirm it, not just a guess. No benchmark *results* were lost
(every completed pair is written to disk immediately -- see below), but the
machine needed a hard restart, which is a real cost independent of the data.

`run_benchmark.py` is a supervisor, not a runner, specifically because of
that -- but it still loads each model only ONCE per model, not once per
form. (An earlier version spawned a fresh worker per form specifically so
it could be killed safely; that measurably made things worse -- reloading
an 8B model 9 times under memory pressure took successive loads from 1.7s
to 6.3s, on top of being pure overhead in the normal case. Load-once-per-
model is both faster and gentler on an already memory-constrained machine;
safety doesn't require paying that cost.) For every model it:
- spawns `worker.py --model <id> --forms <all remaining forms>` as its own
  subprocess (so it can be SIGKILLed and have its memory actually released,
  which an in-process hang can't offer) -- the worker loads the model once
  and loops every form it's given,
- runs a live watchdog thread alongside it that polls actual system free
  memory (via `vm_stat`, not just what MLX thinks it's using) every few
  seconds and kills the worker outright if free memory drops below
  `resource_guard.MIN_FREE_MEMORY_GB` (3GB), and separately kills it if no
  form has completed in `resource_guard.STALL_TIMEOUT_SECONDS` (1200s -- a
  stall detector, not a per-form timeout, since the worker legitimately
  runs long across many forms; the slowest single page seen so far was
  ~500s, so 20 minutes with zero completions is a genuine hang), and
- if killed with forms still remaining, retries with a fresh subprocess
  covering only what's left, up to `MAX_RESTARTS_PER_MODEL` (2) times, then
  gives up on that model's leftover forms for this run (already-completed
  ones are kept; rerun with `--force` to redo a model from scratch) and
  moves on to the next model rather than blocking the whole run.

Inside the worker, `resource_guard.set_mlx_safety_limits()` also caps MLX's
own allocator (`mx.set_memory_limit`) at 40% of physical RAM (capped at
10GB regardless of how much RAM the box has), so the allocator refuses to
grow past a safe ceiling on its own rather than relying only on the
watchdog to notice after the fact.

`run_benchmark.py` also takes a lock (`.benchmark.lock`, PID-checked, stale
locks from a killed run are detected and cleared automatically) so a second
invocation -- or a one-off `mlx_vlm.generate` smoke test run by hand in
another terminal -- can't run concurrently and reproduce the exact failure
mode this exists to prevent. **Don't run ad hoc model probes while
`run_benchmark.py` is active; use `worker.py --model <id> --forms <id>`
directly instead, one at a time, if you need to.**

Every safety intervention is logged to `results/run_log.jsonl` just like a
normal result (`killed`, `skipped_low_memory`, or `gave_up` keys), so the
run's own defensive behavior is visible in the same place as its results,
not silently swallowed.

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
Run only one `run_benchmark.py` at a time -- see "Resource safety" below.

## The 7 models

Chosen to span the real design space, not just different sizes of one
family -- see `models.json` for exact repos and notes:

- **Granite-Docling-258M** (IBM) -- tiny document-structure specialist,
  emits `DocTags` markup with a bounding box per element.
- **Qwen3-VL-8B-Instruct** (4-bit) -- the one generalist chat model, asked
  directly for an APR-shaped intermediate JSON.
- **dots.ocr** -- an OCR/layout specialist (1.2B ViT + 1.7B LLM), native JSON
  output of `{bbox, category, text}` per element. No genuine MLX build
  existed on the Hub for this one (the only "-4bit" upload found was
  bitsandbytes-quantized, incompatible with mlx-vlm), so it was converted
  in-house from `dots-studio/dots.ocr` via `mlx_vlm convert --quantize`.

  It's also the worst performer here (F1 0.11, 82% valid), and this looked
  at first like a benchmark-fairness problem -- it wasn't. On several forms
  (e.g. Form 8822) it classifies the ENTIRE fillable body of the form as
  one `"Picture"` element, and per its own prompt spec `"Picture"` elements
  have their text omitted -- so the whole form contributes zero fields.
  Tested at 300 DPI (up from the standard 200) to rule out a resolution
  problem: identical result, same giant Picture block. Tested at 400 DPI to
  push further: that instead exceeded the model's own documented pixel
  ceiling (11.29M px) and just hung. The real explanation is in dots.ocr's
  own known-issues list: "continuous special characters, such as ellipses
  and underscores, may cause the prediction output to repeat endlessly" --
  and U.S. government forms are built almost entirely out of underscore/
  rule-line blank-fill areas, a content type its training data (general
  document layout parsing -- papers, reports) evidently never covered. This
  is a genuine, reproducible content-type mismatch, confirmed at multiple
  resolutions, not a prompting or benchmark-design error.
- **Florence-2-base-ft** (4-bit) -- a pure grounding/detection specialist
  with a fixed task-token interface (`<OCR_WITH_REGION>`), not a chat model.
  `Florence-2-large-ft` (both the 4-bit and bf16 builds) hit a **confirmed
  mlx-vlm compatibility bug** as of mlx-vlm 0.7.0 (Sept 2026) -- it only ever
  emits the BOS token, verified empirically, not a prompting issue. `base-ft`
  is the variant that actually works, so it stands in for the family here.
  Worth re-testing once mlx-vlm fixes this upstream.
- **Qwen3-VL-4B-Instruct** (4-bit) -- same family/prompt/strategy as the 8B,
  added to answer the resource-load question directly: the 8B is very slow
  (single pages routinely 100-500+s), so does 4B actually cost real quality?
- **InternVL3-8B** (MLX 4-bit) -- a second generalist chat family, to check
  whether Qwen3-VL's results reflect the model or just the default
  assumption that Qwen is best.
- **PaliGemma 2 3B mix** (448, 4-bit) -- substituted for Moondream2, which
  turned out to be a dead end: `vikhyatk/moondream2`'s own `config.json`
  declares `model_type: "moondream1"`, which mlx-vlm's registry does not
  implement (it only has `moondream2`/`moondream3`) -- a real incompatibility,
  confirmed by trying the conversion, not a prompting workaround away.
  PaliGemma 2 fills the same "second detection/grounding specialist vs.
  Florence-2" role and has confirmed real mlx-community builds. Also worth
  noting empirically: despite Google's own documentation describing
  PaliGemma 2's OCR task as producing `{transcription, bbox}` pairs, the
  `"ocr"` prompt on this quantized MLX build returns flat text with **no**
  location tokens at all -- treated honestly here as a text-only source,
  not patched to look like something it isn't.

  A second issue surfaced after the first full run: at temperature=0
  (greedy decoding, used uniformly across all 7 models for reproducibility)
  this model fell into repetition loops on several forms -- e.g. one page
  of the W-9 correctly started listing tax-classification checkboxes
  ("Corporation, Partnership, Trust...") then got stuck repeating
  "Corporation" until the token cap, producing 2005 spurious "fields" on a
  form with 16 real ones. Confirmed via `mlx_vlm`'s own `repetition_penalty`
  support (`sample_utils.py`) that this is a standard, well-documented
  failure mode of greedy decoding on long enumerative outputs, not specific
  tuning PaliGemma2 needs beyond that. Added `generate_kwargs:
  {"repetition_penalty": 1.3, "repetition_context_size": 64}` to its
  `models.json` entry (still temperature=0, still deterministic) and
  reran: F1 went from 0.14 to 0.45, and average time per form dropped from
  245s to 78s (repetition was also why it was slow -- it was generating to
  the token cap on every affected form instead of stopping naturally). The
  pre-fix results are kept for comparison in
  `results/{aprt,raw}/paligemma2-3b-BEFORE-FIX/`.

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
- Timing numbers are single-run wall-clock on one Mac, and that Mac was under
  heavy memory contention during the run -- around 20 concurrent Claude Code
  sessions plus a 3.2GB unrelated process, which is why 36 memory-watchdog
  interventions appear in `run_log.jsonl`. Treat them as ballpark ordering,
  not measurements. The one timing conclusion robust to this is the large
  one: Qwen3-VL-4B beat its own 8B sibling on quality while running ~2.4x
  faster, a gap far wider than the noise.
- Nobody has re-run the suite to get variance. Every number here is n=1.
- **Ground truth is unverified by a human.** An AI agent read the 11 PDFs and
  produced the 313-field answer key, flagging its own ambiguous calls in
  `ground_truth/README.md` (checkbox grouping on SS-4, worksheet scope on
  W-4, repeating-grid modelling on I-9). Those flags have not been checked by
  a person. The answer key was produced by the same class of system being
  scored, which is the weakest link in every number here.
- Two harness bugs silently deflated scores during the first full run: a
  regex that swallowed every DocTags element whenever generation terminated
  cleanly, and a `max_tokens` cutoff that truncated JSON mid-object so a page
  with ~35 correctly-extracted fields parsed as zero. Both were caught by
  eyeballing raw output, not by any gate. `score.py` now flags suspect rows
  (parse failure, zero extraction, field count an order of magnitude past
  ground truth), but there is still **no canary fixture** proving each
  parser handles known-exact input -- so the next parser regression could
  again read as a model being bad. See the milestone's evaluation issues.

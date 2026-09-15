#!/usr/bin/env python3
"""Run several vision models over the same forms, one loaded model at a time.

Each model gets one long-lived worker process. It loads the model once, then
takes jobs from its queue -- a job is a prompt, a set of forms and a page mode --
until the queue is empty (or, with --idle-minutes, until nothing new arrives for
that long), and only then unloads. A new input set is a new job file, not a
reload. Two models are never resident together: this machine has 24 GB, and
resource_guard.py records what happened the one time that was allowed.

Pages go in as separate one-page PDFs, split deterministically with
`qpdf --split-pages`. Each goes through pdf2apr on its own and the pages'
questions are stitched back into one APR document. pdf2apr already shows the
model one rendered page per call; splitting the file first also makes blank
detection see one page at a time. The qwen3-vl-4b job exists to prove the split
changes nothing: same model, same prompt, temperature 0, so its documents must
match the ladder's whole-file run exactly.

Prompts: `shipped` is pdf2apr's PROMPT; `final` is what prompt_ladder.py kept.
The final prompt was tuned on Qwen3-VL-4B, so for every other model it is a
transfer test, not a prompt tuned for that model.

Scoring is the ladder's: edit_counts.py against ground truth without its
signature fields. A form whose run raised (most likely the MLX memory cap on a
dense page) is recorded as an error and never scored as a model's answer.

    .venv/bin/python model_sweep.py enqueue
    .venv/bin/python model_sweep.py check-split      # detection on page files vs whole file
    .venv/bin/python model_sweep.py run-all --wait-for-ladder
    .venv/bin/python model_sweep.py worker --model qwen3.5-4b --idle-minutes 30
    .venv/bin/python model_sweep.py report
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(REPO / "pdf2apr"))

import prompt_ladder as L  # noqa: E402  (ground truth and scoring; loads no model)
import score as S  # noqa: E402
from pdf2apr import Options, build_document, convert  # noqa: E402
from pdf2apr.name import PROMPT, MlxNamer, parse_reply  # noqa: E402

ROOT = HERE / "results" / "sweep"
QUEUE = ROOT / "queue"
PAGES = ROOT / "pages"
LOCK = ROOT / "model.lock"
GRANITE_SOURCE = "ibm-granite/granite-4.0-3b-vision"
GRANITE_FALLBACK = "mlx-community/granite-4.0-3b-vision-4bit"

# In run order. The Qwen3-VL-4B check comes first so a harness fault shows before
# any new model is judged; then smallest to largest, so the sweep can stop at the
# first model good enough to make the bigger ones unnecessary.
MODELS: dict[str, dict] = {
    "qwen3-vl-4b": {"repo": "mlx-community/Qwen3-VL-4B-Instruct-4bit", "template": {}},
    "minicpm-v-4.6": {"repo": "mlx-community/MiniCPM-V-4.6-5bit", "template": {"enable_thinking": False}},
    "granite-4.0-3b-vision": {"repo": str(HERE / "local_models" / "granite-4.0-3b-vision-mlx-4bit"),
                              "template": {}},
    "qwen3.5-4b": {"repo": "mlx-community/Qwen3.5-4B-MLX-4bit", "template": {"enable_thinking": False}},
    "qwen3-vl-8b": {"repo": "mlx-community/Qwen3-VL-8B-Instruct-4bit", "template": {}},
    "qwen3.5-9b": {"repo": "mlx-community/Qwen3.5-9B-MLX-4bit", "template": {"enable_thinking": False}},
}
THINKING = re.compile(r"<think>.*?</think>", re.S)

# pdf2apr ships 4000. SS-4's 89-blank page and a W-4 page need more: the reply is
# cut off, the page falls back to deterministic labels, and the model is never
# measured there -- the ladder's held-out run fell back on exactly those. Every
# sweep job uses this budget, so every model is compared on the same terms.
MAX_TOKENS = 8192


def job(run: str, prompt: str, forms: list[str], mode: str = "split") -> dict:
    return {"run": run, "prompt": prompt, "forms": list(forms), "mode": mode}


def default_plan() -> dict[str, list[dict]]:
    # dev-final must reproduce the ladder's whole-file documents; held-out-final is
    # Qwen3-VL-4B's own row under the same token budget as every other model.
    plan = {"qwen3-vl-4b": [job("dev-final", "final", L.DEV), job("held-out-final", "final", L.HELD_OUT)]}
    for model in list(MODELS)[1:]:
        # The final prompt on both sets first: that is what decides whether to stop.
        plan[model] = [job("dev-final", "final", L.DEV),
                       job("held-out-final", "final", L.HELD_OUT),
                       job("dev-shipped", "shipped", L.DEV)]
    return plan


def prompt_text(name: str) -> str:
    if name == "shipped":
        return PROMPT
    final = L.OUT / "final_prompt.txt"
    if not final.exists():
        raise SystemExit("no final prompt yet: prompt_ladder.py has not finished")
    return final.read_text()


def page_files(fid: str) -> list[Path]:
    folder = PAGES / fid
    if not folder.exists():
        partial = PAGES / f".{fid}.partial"
        shutil.rmtree(partial, ignore_errors=True)
        partial.mkdir(parents=True)
        subprocess.run(["qpdf", "--split-pages", str(HERE / "corpus" / f"{fid}.pdf"),
                        str(partial / "page-%d.pdf")], check=True)
        # qpdf copies the document's whole field list into every page file, so a
        # W-4 page would appear to carry all 48 fields. Keep only this page's, and
        # remember how many were left.
        manifest = {"document_fields": 0, "pages": {}}
        for pdf in partial.glob("page-*.pdf"):
            pruned = pdf.with_suffix(".pruned.pdf")
            out = subprocess.run(["mutool", "run", str(HERE / "prune_page_fields.js"), str(pdf), str(pruned)],
                                 check=True, capture_output=True, text=True).stdout
            before, after = (int(n) for n in re.search(r"(\d+) -> (\d+)", out).groups())
            # qpdf already leaves some page files without the document's fields,
            # so the document's count is the most any page file started with.
            manifest["document_fields"] = max(manifest["document_fields"], before)
            manifest["pages"][pdf.stem.split("-")[-1]] = after
            pruned.replace(pdf)
        (partial / "fields.json").write_text(json.dumps(manifest, indent=1) + "\n")
        partial.rename(folder)
    return sorted(folder.glob("page-*.pdf"), key=lambda p: int(re.search(r"(\d+)\.pdf$", p.name).group(1)))


@dataclass
class SweepNamer(MlxNamer):
    """MlxNamer that stays loaded, passes chat-template options, and keeps what it saw."""

    template: dict = field(default_factory=dict)
    raw_dir: Path = ROOT
    page_label: str = ""
    sections_seen: list = field(default_factory=list)

    def name_page(self, page):
        from mlx_vlm import generate
        from mlx_vlm.prompt_utils import apply_chat_template

        formatted = apply_chat_template(self._processor, self._model.config, self.prompt,
                                        num_images=1, **self.template)
        reply = generate(self._model, self._processor, formatted, [str(page.image)],
                         max_tokens=self.max_tokens, temperature=0.0, verbose=False)
        text = reply.text if hasattr(reply, "text") else str(reply)
        (self.raw_dir / f"page-{self.page_label or page.page}.txt").write_text(text)
        # A thinking model may reason before the JSON, and its reasoning has braces.
        questions, sections = parse_reply(THINKING.sub("", text), page.blanks)
        self.sections_seen.extend(sections)
        return questions, sections, text

    def close(self) -> None:
        """convert() closes its namer after every file; this one outlives them all."""

    def unload(self) -> None:
        MlxNamer.close(self)


def widgetless_page(fid: str, pdf: Path) -> bool:
    """A page of a fillable form that has no widgets of its own has no fields.

    Left to itself, convert-pdf treats a page file with no form fields as a form
    with no AcroForm and guesses blanks from its text and rules, which on an
    instruction page invents dozens. Whole-file detection never does that, since
    the document's fields are on other pages; this keeps the split honest to it.
    """
    manifest = json.loads((PAGES / fid / "fields.json").read_text())
    return manifest["document_fields"] > 0 and manifest["pages"][pdf.stem.split("-")[-1]] == 0


def no_fields(exc: Exception) -> bool:
    """convert-pdf refuses a file with no fields; for one page of a form that is just a blank page."""
    return isinstance(exc, RuntimeError) and "found no fields" in str(exc)


def fell_back(result) -> bool:
    return any("fell back" in message for _, message in result.report.lines)


def convert_split(namer: SweepNamer, fid: str) -> tuple[dict, list[dict]]:
    questions, sections, pages = [], [], []
    for pdf in page_files(fid):
        namer.page_label = pdf.stem.split("-")[-1]
        namer.sections_seen = []
        if widgetless_page(fid, pdf):
            pages.append({"page": int(namer.page_label), "blanks": 0, "fell_back": False,
                          "skipped": "no widgets of its own"})
            continue
        try:
            result = convert(pdf, Options(title=fid, namer=namer))
        except RuntimeError as exc:
            if not no_fields(exc):
                raise
            pages.append({"page": int(namer.page_label), "blanks": 0, "fell_back": False})
            continue
        pages.append({"page": int(namer.page_label), "blanks": len(result.blanks),
                      "fell_back": fell_back(result)})
        questions.extend(result.questions)
        for s in namer.sections_seen:
            if s["id"] not in {x["id"] for x in sections}:
                sections.append(s)
    return build_document(fid, questions, sections), pages


def convert_whole(namer: SweepNamer, fid: str) -> tuple[dict, list[dict]]:
    namer.page_label = ""
    result = convert(HERE / "corpus" / f"{fid}.pdf", Options(title=fid, namer=namer))
    return result.document, [{"page": "whole", "blanks": len(result.blanks), "fell_back": fell_back(result)}]


def run_job(namer: SweepNamer, model: str, spec: dict, repo: str) -> None:
    prompt = prompt_text(spec["prompt"])
    converter = f"sweep/{model}/{spec['run']}"
    folder = S.APRT_DIR / converter
    folder.mkdir(parents=True, exist_ok=True)
    recorded = folder / "prompt.txt"
    if recorded.exists() and recorded.read_text() != prompt:
        raise SystemExit(f"{converter} was run with a different prompt; remove it to rerun")
    recorded.write_text(prompt)
    namer.prompt = prompt
    for fid in spec["forms"]:
        meta_file = folder / f"{fid}.run.json"
        if meta_file.exists():
            continue
        namer.raw_dir = ROOT / "raw" / model / spec["run"] / fid
        namer.raw_dir.mkdir(parents=True, exist_ok=True)
        t0 = time.time()
        meta = {"model": model, "repo": repo, "mode": spec["mode"], "prompt": spec["prompt"],
                "max_tokens": namer.max_tokens}
        try:
            document, pages = (convert_split if spec["mode"] == "split" else convert_whole)(namer, fid)
        except Exception as exc:  # noqa: BLE001 -- recorded, never scored as an answer
            meta.update(error=f"{type(exc).__name__}: {exc}", seconds=round(time.time() - t0))
            print(f"    {model} {spec['run']} {fid}: ERROR {meta['error'][:120]}", flush=True)
        else:
            (folder / f"{fid}.aprt").write_text(json.dumps(document, indent=2) + "\n")
            meta.update(seconds=round(time.time() - t0), pages=pages,
                        fell_back=[p["page"] for p in pages if p["fell_back"]])
            print(f"    {model} {spec['run']} {fid}: {meta['seconds']}s"
                  + (f", fell back on page(s) {meta['fell_back']}" if meta["fell_back"] else ""), flush=True)
        meta_file.write_text(json.dumps(meta, indent=1) + "\n")


def job_done(model: str, spec: dict) -> bool:
    folder = S.APRT_DIR / "sweep" / model / spec["run"]
    return all((folder / f"{fid}.run.json").exists() for fid in spec["forms"])


def nothing_named(model: str, spec: dict) -> bool:
    """Every page with blanks fell back or errored: the model produced nothing usable."""
    folder = S.APRT_DIR / "sweep" / model / spec["run"]
    metas = [json.loads((folder / f"{fid}.run.json").read_text()) for fid in spec["forms"]]
    pages = [p for m in metas for p in m.get("pages", []) if p["blanks"]]
    return all("error" in m for m in metas) or (bool(pages) and all(p["fell_back"] for p in pages))


def busy() -> str | None:
    ladder = subprocess.run(["pgrep", "-f", "prompt_ladder.py"], capture_output=True, text=True).stdout.split()
    if ladder:
        return f"prompt_ladder.py is running (pid {' '.join(ladder)})"
    if LOCK.exists():
        pid = int(LOCK.read_text().split()[0])
        try:
            os.kill(pid, 0)
            return f"another model worker holds {LOCK.name} (pid {pid})"
        except ProcessLookupError:
            pass
    return None


def worker(model: str, idle_minutes: float, repo: str | None) -> int:
    ROOT.mkdir(parents=True, exist_ok=True)
    reason = busy()
    if reason:
        raise SystemExit(f"refusing to load {model}: {reason}")
    LOCK.write_text(f"{os.getpid()} {model}\n")
    queue = QUEUE / model
    repo = repo or MODELS[model]["repo"]
    try:
        import resource_guard

        resource_guard.set_mlx_safety_limits()
        t0 = time.time()
        namer = SweepNamer(model_id=repo, template=MODELS[model]["template"], max_tokens=MAX_TOKENS)
        print(f"{model}: loaded {repo} in {time.time() - t0:.0f}s", flush=True)
        try:
            idle_since = time.time()
            while not ((QUEUE / "STOP").exists() or (queue / "STOP").exists()):
                pending = [p for p in sorted(queue.glob("*.json")) if not job_done(model, json.loads(p.read_text()))]
                if not pending:
                    if time.time() - idle_since >= idle_minutes * 60:
                        break
                    time.sleep(10)
                    continue
                spec = json.loads(pending[0].read_text())
                print(f"  {model} job {pending[0].name}", flush=True)
                run_job(namer, model, spec, repo)
                if nothing_named(model, spec):
                    (queue / "ABORTED").write_text(f"{spec['run']}: every page fell back or errored\n")
                    print(f"  {model}: aborted, {spec['run']} produced nothing usable", flush=True)
                    break
                idle_since = time.time()
        finally:
            namer.unload()
            print(f"{model}: unloaded", flush=True)
    finally:
        LOCK.unlink(missing_ok=True)
    return 0


def convert_granite(target: Path) -> bool:
    if (target / "config.json").exists():
        return True
    cmd = [sys.executable, "-m", "mlx_vlm", "convert", "--hf-path", GRANITE_SOURCE, "--trust-remote-code",
           "-q", "--q-bits", "4", "--mlx-path", str(target)]
    print("converting Granite:", " ".join(cmd), flush=True)
    done = subprocess.run(cmd).returncode == 0 and (target / "config.json").exists()
    if not done:
        shutil.rmtree(target, ignore_errors=True)
    return done


def perfect(model: str, stop_at_edits: int) -> bool:
    """The final prompt needs no more than stop_at_edits edits on both sets, with nothing failed."""
    rows = [score_run(f"sweep/{model}/{run}", forms)
            for run, forms in [("dev-final", L.DEV), ("held-out-final", L.HELD_OUT)]]
    return all(r["forms"] == len(forms) and not r["errors"] and not r["fell_back"]
               and r["edits"] <= stop_at_edits
               for r, forms in zip(rows, [L.DEV, L.HELD_OUT]))


def run_all(wait_for_ladder: bool, stop_at_edits: int) -> int:
    while wait_for_ladder and busy():
        time.sleep(60)
    L.write_ground_truth_without_signatures(L.DEV + L.HELD_OUT)
    for model in MODELS:
        queue = QUEUE / model
        if not queue.exists() or (queue / "ABORTED").exists():
            continue
        repo = MODELS[model]["repo"]
        if model == "granite-4.0-3b-vision" and not convert_granite(Path(repo)):
            print(f"our Granite conversion failed; using {GRANITE_FALLBACK}", flush=True)
            repo = GRANITE_FALLBACK
        code = subprocess.run([sys.executable, __file__, "worker", "--model", model,
                               "--idle-minutes", "0", "--repo", repo]).returncode
        if code:
            print(f"{model}: worker exited {code}", flush=True)
            if model == "granite-4.0-3b-vision" and repo != GRANITE_FALLBACK:
                print(f"retrying Granite with {GRANITE_FALLBACK}", flush=True)
                subprocess.run([sys.executable, __file__, "worker", "--model", model,
                                "--idle-minutes", "0", "--repo", GRANITE_FALLBACK])
        if model != "qwen3-vl-4b" and perfect(model, stop_at_edits):
            print(f"{model} needs at most {stop_at_edits} edits on every form; stopping before larger models",
                  flush=True)
            break
    return 0


def enqueue() -> int:
    for model, jobs in default_plan().items():
        (QUEUE / model).mkdir(parents=True, exist_ok=True)
        for i, spec in enumerate(jobs, start=1):
            path = QUEUE / model / f"{i:02d}-{spec['run']}.json"
            if not path.exists():
                path.write_text(json.dumps(spec, indent=1) + "\n")
                print(f"queued {model} {path.name}")
    return 0


def score_run(converter: str, forms: list[str]) -> dict:
    row = {"forms": 0, "edits": 0, "raw_edits": 0, "errors": [], "fell_back": [], "seconds": 0,
           **{b: 0 for b in L.BUCKETS}}
    folder = S.APRT_DIR / converter
    for fid in forms:
        meta_file = folder / f"{fid}.run.json"
        meta = json.loads(meta_file.read_text()) if meta_file.exists() else {}
        if "error" in meta:
            row["errors"].append(fid)
        if meta.get("fell_back"):
            row["fell_back"].append(f"{fid}:{meta['fell_back']}")
        row["seconds"] += meta.get("seconds", 0)
        a = L.counts(converter, fid, True)
        if a is None:
            continue
        row["forms"] += 1
        row["edits"] += a["edits"]
        row["raw_edits"] += L.counts(converter, fid, False)["edits"]
        for b in L.BUCKETS:
            row[b] += a[b]
    return row


def report() -> int:
    L.write_ground_truth_without_signatures(L.DEV + L.HELD_OUT)
    rows = []
    ladder = json.loads((L.OUT / "ladder.json").read_text()) if (L.OUT / "ladder.json").exists() else {}
    final_run = (ladder.get("accepted") or ["r0-baseline"])[-1]
    for label, run, forms in [("dev-shipped (ladder, whole file)", "r0-baseline", L.DEV),
                              ("dev-final (ladder, whole file)", final_run, L.DEV),
                              ("held-out-final (ladder, whole file)",
                               "h1-final" if ladder.get("accepted") else "h0-baseline", L.HELD_OUT)]:
        rows.append({"model": "qwen3-vl-4b", "run": label, **score_run(f"ladder/{run}", forms)})
    for model, jobs in default_plan().items():
        for spec in jobs:
            rows.append({"model": model, "run": spec["run"],
                         **score_run(f"sweep/{model}/{spec['run']}", spec["forms"])})

    cols = ["forms", "edits", "raw_edits", "keep", "fix_label", "fix_type", "rewrite", "delete", "add"]
    print(f"{'model':24}{'run':38}" + "".join(f"{c:>10}" for c in cols) + f"{'min':>6}  fell back / errors")
    for r in rows:
        print(f"{r['model']:24}{r['run']:38}" + "".join(f"{r[c]:>10}" for c in cols)
              + f"{r['seconds'] / 60:6.0f}  {' '.join(r['fell_back'])} {' '.join('ERR:' + e for e in r['errors'])}")

    same, compared = [], 0
    for fid in L.DEV:
        a = S.APRT_DIR / "sweep" / "qwen3-vl-4b" / "dev-final" / f"{fid}.aprt"
        b = S.APRT_DIR / "ladder" / final_run / f"{fid}.aprt"
        if a.exists() and b.exists():
            compared += 1
            if json.loads(a.read_text()) == json.loads(b.read_text()):
                same.append(fid)
    print(f"\nsplit vs whole file, Qwen3-VL-4B, final prompt: {len(same)} of {compared} documents identical {same}")
    (ROOT / "report.json").write_text(json.dumps({"rows": rows, "split_equals_whole": same,
                                                  "compared": compared}, indent=1) + "\n")
    return 0


def check_split() -> int:
    """Does blank detection on each one-page file find what it finds on that page of the whole file?"""
    import dataclasses

    from pdf2apr.detect import detect_blanks

    def sig(b) -> str:
        d = dataclasses.asdict(b)
        d.pop("page")
        return repr(sorted((k, repr(v)) for k, v in d.items()))

    results = {}
    for fid in L.DEV + L.HELD_OUT:
        whole = detect_blanks(HERE / "corpus" / f"{fid}.pdf")
        for pdf in page_files(fid):
            n = int(pdf.stem.split("-")[-1])
            want = {sig(b): b for b in whole if b.page == n}
            try:
                got = {} if widgetless_page(fid, pdf) else {sig(b): b for b in detect_blanks(pdf)}
            except RuntimeError as exc:
                if not no_fields(exc):
                    raise
                got = {}
            if not want and not got:
                continue
            results[f"{fid}:{n}"] = {
                "whole": len(want), "split": len(got), "identical": set(want) == set(got),
                "only_split": [f"{b.id} | {b.label}" for k, b in got.items() if k not in want],
                "only_whole": [f"{b.id} | {b.label}" for k, b in want.items() if k not in got],
            }
            r = results[f"{fid}:{n}"]
            print(f"  {fid} p{n}: whole {r['whole']}, split {r['split']}"
                  + ("" if r["identical"] else f"  DIFF +{r['only_split']} -{r['only_whole']}"), flush=True)
    (ROOT / "split-check.json").write_text(json.dumps(results, indent=1) + "\n")
    same = sum(r["identical"] for r in results.values())
    print(f"{same} of {len(results)} pages with blanks detect identically")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="command", required=True)
    sub.add_parser("enqueue")
    w = sub.add_parser("worker")
    w.add_argument("--model", required=True, choices=list(MODELS))
    w.add_argument("--idle-minutes", type=float, default=0)
    w.add_argument("--repo")
    r = sub.add_parser("run-all")
    r.add_argument("--wait-for-ladder", action="store_true")
    r.add_argument("--stop-at-edits", type=int, default=0,
                   help="stop once a model needs no more edits than this on both form sets (default 0: perfect)")
    sub.add_parser("report")
    sub.add_parser("check-split")
    args = ap.parse_args()
    if args.command == "check-split":
        return check_split()
    if args.command == "enqueue":
        return enqueue()
    if args.command == "worker":
        return worker(args.model, args.idle_minutes, args.repo)
    if args.command == "run-all":
        return run_all(args.wait_for_ladder, args.stop_at_edits)
    return report()


if __name__ == "__main__":
    raise SystemExit(main())

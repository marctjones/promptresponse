#!/usr/bin/env python3
"""Score the `pdf2apr` converter on the model benchmark.

Runs the shipped tool -- not a private copy of its logic -- over the corpus and
scores the result through the same `score.py` as every model, so the number in
pdf2apr's README comes from the thing people actually run.

    python3 scripts/pdf-form-benchmark/score_pdf2apr.py fed-w9 ct-w4 fed-8822 fed-i9

Needs the model extra installed and takes roughly a minute a page.
"""
import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(REPO / "pdf2apr"))

import score as S  # noqa: E402
from pdf2apr import Options, convert  # noqa: E402

MODEL_ID = "pdf2apr"


def main(form_ids: list[str]) -> int:
    out = HERE / "results" / "aprt" / MODEL_ID
    out.mkdir(parents=True, exist_ok=True)

    for fid in form_ids:
        t0 = time.time()
        result = convert(HERE / "corpus" / f"{fid}.pdf", Options(title=fid))
        (out / f"{fid}.aprt").write_text(json.dumps(result.document, indent=2) + "\n")
        print(f"  {fid}: {len(result.blanks)} blanks -> {len(result.questions)} questions, "
              f"{time.time() - t0:.0f}s", flush=True)

    rows = [r for fid in form_ids if (r := S.score_form(MODEL_ID, fid, {})) is not None]
    if not rows:
        return 1

    others = {}
    for r in json.loads((HERE / "results" / "scorecard.json").read_text()):
        others.setdefault(r["model_id"], {})[r["form_id"]] = r["f1"]
    det = {r["form_id"]: r["f1"] for r in json.loads((HERE / "results" / "deterministic.json").read_text())}

    print(f"\n{'form':12}{'pred':>6}{'hit':>5}{'pdf2apr':>9}{'model':>8}{'determ':>8}")
    tot = [0.0, 0.0, 0.0]
    for r in rows:
        f = r["form_id"]
        m, d = others.get("qwen3-vl-4b", {}).get(f, 0.0), det.get(f, 0.0)
        tot[0] += r["f1"]; tot[1] += m; tot[2] += d
        print(f"{f:12}{r['pred_field_count']:6}{r['matched']:5}{r['f1']:9.2f}{m:8.2f}{d:8.2f}")
    n = len(rows)
    print(f"{'MEAN':12}{'':6}{'':5}{tot[0]/n:9.2f}{tot[1]/n:8.2f}{tot[2]/n:8.2f}")

    (HERE / "results" / f"{MODEL_ID}.json").write_text(json.dumps(rows, indent=2) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:] or ["fed-w9", "ct-w4", "fed-8822", "fed-i9"]))

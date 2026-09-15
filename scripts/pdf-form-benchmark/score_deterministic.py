#!/usr/bin/env python3
"""Score the deterministic convert-pdf pipeline on the model benchmark.

Why this exists
---------------
The converter has its own oracles -- the AcroForm widget list, and hand-read
label keys -- and against those it looks strong: 83-91% label recall on the
forms that have keys. Those oracles ask "given this widget, did you find the
right caption for it".

That is not the question a person asks of a converted form. The model benchmark
asks the other one: does the APR you produced look like the form, as a human
would write it out. Its ground truth in `ground_truth/*.json` is hand-authored
and is NOT the widget list -- I-9 has 128 widgets and 44 human questions, W-4
has 48 and 19 -- so a converter bound to the widget list is judged on how well
it groups them, not just on how well it names them.

Scored through the same `score.py` as every model, so the numbers sit on the
same axis. Emit the .aprt files first:

    for f in <form ids>; do
      dotnet run --project src/PromptResponse.Conversion.Pdf.Tool -- \
        scripts/pdf-form-benchmark/corpus/$f.pdf \
        --output=scripts/pdf-form-benchmark/results/aprt/deterministic/$f.aprt --quiet
    done
    python3 scripts/pdf-form-benchmark/score_deterministic.py
"""
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import score as S  # noqa: E402

MODEL_ID = "deterministic"


def main() -> int:
    rows = [r for form in S.FORMS
            if (r := S.score_form(MODEL_ID, form["id"], {})) is not None]
    if not rows:
        print("no scored rows -- emit the .aprt files first (see module docstring)")
        return 1

    scorecard = json.loads((HERE / "results" / "scorecard.json").read_text())
    others: dict[str, dict[str, float]] = {}
    for r in scorecard:
        others.setdefault(r["model_id"], {})[r["form_id"]] = r["f1"]

    best = max(others, key=lambda m: sum(others[m].values()) / max(1, len(others[m])))
    print(f"{'form':16}{'GT':>4}{'pred':>6}{'F1':>7}{f'  vs {best}':>22}")
    for r in sorted(rows, key=lambda x: x["f1"] - others[best].get(x["form_id"], 0.0)):
        rival = others[best].get(r["form_id"], 0.0)
        print(f"{r['form_id']:16}{r['gt_field_count']:4}{r['pred_field_count']:6}"
              f"{r['f1']:7.2f}{rival:12.2f}{r['f1'] - rival:+10.2f}")

    n = len(rows)
    mine = sum(r["f1"] for r in rows) / n
    theirs = sum(others[best].values()) / len(others[best])
    print(f"{'MEAN':16}{'':4}{'':6}{mine:7.2f}{theirs:12.2f}{mine - theirs:+10.2f}")

    (HERE / "results" / "deterministic.json").write_text(json.dumps(rows, indent=2) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Score every model's .aprt output against the hand-verified ground truth
and write results/scorecard.json + results/REPORT.md.

Field matching is a greedy bipartite match on normalized label similarity
(difflib ratio) -- not exact string equality, since a model that paraphrases
a label slightly ("Name" vs "Full Name") shouldn't be scored as if it
invented an unrelated field. MATCH_THRESHOLD controls how forgiving that is.
"""
import difflib
import json
import re
from pathlib import Path
from statistics import mean

HERE = Path(__file__).parent
GT_DIR = HERE / "ground_truth"
APRT_DIR = HERE / "results" / "aprt"
LOG_PATH = HERE / "results" / "run_log.jsonl"
MODELS = json.loads((HERE / "models.json").read_text())["models"]
FORMS = json.loads((HERE / "corpus_manifest.json").read_text())["forms"]

MATCH_THRESHOLD = 0.55


def norm(s: str) -> str:
    return re.sub(r"[^a-z0-9 ]", "", (s or "").lower()).strip()


def similarity(a: str, b: str) -> float:
    return difflib.SequenceMatcher(None, norm(a), norm(b)).ratio()


def load_run_log() -> dict:
    by_key = {}
    if LOG_PATH.exists():
        for line in LOG_PATH.read_text().splitlines():
            if not line.strip():
                continue
            rec = json.loads(line)
            if rec.get("form_id"):
                by_key[(rec["model_id"], rec["form_id"])] = rec
    return by_key


def match_fields(pred_fields: list[dict], gt_fields: list[dict]) -> dict:
    """Greedy best-first bipartite match by label similarity."""
    pairs = []
    for pi, pf in enumerate(pred_fields):
        for gi, gf in enumerate(gt_fields):
            sim = similarity(pf["label"], gf["label"])
            if sim >= MATCH_THRESHOLD:
                pairs.append((sim, pi, gi))
    pairs.sort(reverse=True)
    used_p, used_g = set(), set()
    matches = []
    for sim, pi, gi in pairs:
        if pi in used_p or gi in used_g:
            continue
        used_p.add(pi)
        used_g.add(gi)
        matches.append((pi, gi, sim))
    return {"matches": matches, "matched_pred": used_p, "matched_gt": used_g}


def score_form(model_id: str, form_id: str, run_log: dict) -> dict | None:
    gt_path = GT_DIR / f"{form_id}.json"
    aprt_path = APRT_DIR / model_id / f"{form_id}.aprt"
    log_rec = run_log.get((model_id, form_id), {})
    if not gt_path.exists():
        return None

    gt = json.loads(gt_path.read_text())
    gt_fields = gt["fields"]

    pred_fields = []
    if aprt_path.exists():
        doc = json.loads(aprt_path.read_text())
        for sec in doc.get("sections", []):
            for p in sec.get("prompts", []):
                pred_fields.append({
                    "label": p.get("label", ""),
                    "expected_data_type": (p.get("hints") or {}).get("expectedDataType", "text"),
                })

    m = match_fields(pred_fields, gt_fields)
    n_pred, n_gt, n_hit = len(pred_fields), len(gt_fields), len(m["matches"])
    precision = n_hit / n_pred if n_pred else 0.0
    recall = n_hit / n_gt if n_gt else 0.0
    f1 = (2 * precision * recall / (precision + recall)) if (precision + recall) else 0.0

    type_hits = 0
    for pi, gi, _ in m["matches"]:
        pt = pred_fields[pi]["expected_data_type"]
        gt_t = gt_fields[gi]["expected_data_type"]
        if pt == gt_t or {pt, gt_t} <= {"text", "multiline"}:
            type_hits += 1
    type_agreement = type_hits / n_hit if n_hit else 0.0

    return {
        "model_id": model_id, "form_id": form_id,
        "valid": log_rec.get("valid"),
        "gt_field_count": n_gt, "pred_field_count": n_pred, "matched": n_hit,
        "precision": round(precision, 3), "recall": round(recall, 3), "f1": round(f1, 3),
        "type_agreement": round(type_agreement, 3),
        "total_seconds": log_rec.get("total_seconds"),
        "parse_errors": log_rec.get("parse_errors", []),
        "validation_errors": log_rec.get("validation_errors"),
    }


def main() -> int:
    run_log = load_run_log()
    rows = []
    for model in MODELS:
        for form in FORMS:
            row = score_form(model["id"], form["id"], run_log)
            if row:
                rows.append(row)

    (HERE / "results" / "scorecard.json").write_text(json.dumps(rows, indent=2))

    by_model = {}
    for r in rows:
        by_model.setdefault(r["model_id"], []).append(r)

    lines = ["# PDF-form-to-APR open-weight VLM benchmark", ""]
    lines.append(f"11 real government forms (5 federal, 6 Connecticut) x 4 open-weight models, "
                 f"all running locally via MLX. Field matching is fuzzy-label similarity "
                 f"(threshold {MATCH_THRESHOLD}) against hand-verified ground truth, not exact string match.")
    lines.append("")
    lines.append("## Aggregate results")
    lines.append("")
    lines.append("| Model | Valid .aprt | Precision | Recall | F1 | Type agreement | Avg time/form |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|")
    model_display = {m["id"]: m["display_name"] for m in MODELS}
    summary = {}
    for mid, rs in by_model.items():
        valid_rate = mean(1.0 if r["valid"] else 0.0 for r in rs)
        precision = mean(r["precision"] for r in rs)
        recall = mean(r["recall"] for r in rs)
        f1 = mean(r["f1"] for r in rs)
        type_agreement = mean(r["type_agreement"] for r in rs if r["matched"] > 0) if any(r["matched"] > 0 for r in rs) else 0.0
        avg_time = mean(r["total_seconds"] for r in rs if r["total_seconds"] is not None)
        summary[mid] = dict(valid_rate=valid_rate, precision=precision, recall=recall, f1=f1,
                             type_agreement=type_agreement, avg_time=avg_time)
        lines.append(f"| {model_display.get(mid, mid)} | {valid_rate:.0%} | {precision:.2f} | "
                     f"{recall:.2f} | {f1:.2f} | {type_agreement:.2f} | {avg_time:.1f}s |")

    lines.append("")
    lines.append("## Per-form detail")
    lines.append("")
    lines.append("| Form | Model | GT fields | Pred fields | Matched | Precision | Recall | F1 | Valid |")
    lines.append("|---|---|---:|---:|---:|---:|---:|---:|---:|")
    form_display = {f["id"]: f["name"] for f in FORMS}
    for form in FORMS:
        for model in MODELS:
            r = next((x for x in rows if x["form_id"] == form["id"] and x["model_id"] == model["id"]), None)
            if not r:
                continue
            lines.append(f"| {form_display[form['id']]} | {model_display[model['id']]} | "
                         f"{r['gt_field_count']} | {r['pred_field_count']} | {r['matched']} | "
                         f"{r['precision']:.2f} | {r['recall']:.2f} | {r['f1']:.2f} | "
                         f"{'✓' if r['valid'] else '✗'} |")

    (HERE / "results" / "REPORT.md").write_text("\n".join(lines) + "\n")
    print("\n".join(lines))
    (HERE / "results" / "summary.json").write_text(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

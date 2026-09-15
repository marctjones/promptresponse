#!/usr/bin/env python3
"""How much work a reviewer has left: fields to fix, delete, or add.

F1 says how close an output is. It does not say what a person has to DO to
turn it into the right form, which is the question that matters once a
converter is good enough to use. This counts that, against the same
hand-authored ground truth and the same label matcher as score.py.

Every ground-truth field and every produced prompt lands in exactly one bucket:

  keep        found, label wording right, data type right       no edit
  fix label   found, but the wording needs editing              1 edit
  fix type    found, wording right, data type wrong             1 edit
  rewrite     a produced prompt that is recognisably this field
              but whose label is too far off to be sure          1 edit
  delete      a produced prompt matching nothing                 1 edit
  add         a ground-truth field nothing produced              1 edit

"rewrite" exists because a badly worded label fails the match and would
otherwise count twice, as one delete plus one add, when a person fixes it by
retyping one label. It is a looser second pass over the leftovers, so it is the
least certain bucket and is reported separately rather than folded into the
others.

    python3 scripts/pdf-form-benchmark/edit_counts.py [converter ...]
"""
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import score as S  # noqa: E402

LABEL_OK = 0.85     # at or above this, the wording needs no edit
REWRITE_MIN = 0.30  # leftover pairs at or above this are one field mislabelled


def types_agree(a: str, b: str) -> bool:
    return a == b or {a, b} <= {"text", "multiline"}


def greedy(preds, gts, pi_pool, gi_pool, threshold):
    pairs = sorted(
        ((S.similarity(preds[p]["label"], gts[g]["label"]), p, g)
         for p in pi_pool for g in gi_pool),
        reverse=True,
    )
    used_p, used_g, out = set(), set(), []
    for sim, p, g in pairs:
        if sim < threshold:
            break
        if p in used_p or g in used_g:
            continue
        used_p.add(p); used_g.add(g); out.append((p, g, sim))
    return out


def count(converter: str, form_id: str, detail: list | None = None) -> dict | None:
    gt_path = S.GT_DIR / f"{form_id}.json"
    aprt = S.APRT_DIR / converter / f"{form_id}.aprt"
    if not gt_path.exists() or not aprt.exists():
        return None

    gts = json.loads(gt_path.read_text())["fields"]
    doc = json.loads(aprt.read_text())
    preds = [
        {"label": p.get("label", ""),
         "type": (p.get("hints") or {}).get("expectedDataType", "text")}
        for sec in doc.get("sections", []) for p in sec.get("prompts", [])
    ]

    c = dict(gt=len(gts), produced=len(preds),
             keep=0, fix_label=0, fix_type=0, rewrite=0, delete=0, add=0)

    def note(bucket, p=None, g=None, sim=None):
        if detail is not None:
            detail.append(dict(
                form=form_id, bucket=bucket, sim=sim,
                made=preds[p]["label"] if p is not None else None,
                made_type=preds[p]["type"] if p is not None else None,
                want=gts[g]["label"] if g is not None else None,
                want_type=gts[g]["expected_data_type"] if g is not None else None,
                want_kind=gts[g].get("field_kind") if g is not None else None,
            ))

    matched = greedy(preds, gts, range(len(preds)), range(len(gts)), S.MATCH_THRESHOLD)
    for p, g, sim in matched:
        if sim < LABEL_OK:
            c["fix_label"] += 1
            note("fix_label", p, g, sim)
        elif not types_agree(preds[p]["type"], gts[g]["expected_data_type"]):
            c["fix_type"] += 1
            note("fix_type", p, g, sim)
        else:
            c["keep"] += 1

    left_p = [p for p in range(len(preds)) if p not in {m[0] for m in matched}]
    left_g = [g for g in range(len(gts)) if g not in {m[1] for m in matched}]
    rewrites = greedy(preds, gts, left_p, left_g, REWRITE_MIN)
    for p, g, sim in rewrites:
        note("rewrite", p, g, sim)
    for p in set(left_p) - {r[0] for r in rewrites}:
        note("delete", p=p)
    for g in set(left_g) - {r[1] for r in rewrites}:
        note("add", g=g)
    c["rewrite"] = len(rewrites)
    c["delete"] = len(left_p) - len(rewrites)
    c["add"] = len(left_g) - len(rewrites)
    c["edits"] = c["fix_label"] + c["fix_type"] + c["rewrite"] + c["delete"] + c["add"]
    return c


COLS = ["gt", "produced", "keep", "fix_label", "fix_type", "rewrite", "delete", "add", "edits"]
HEAD = ["GT", "made", "keep", "fixLbl", "fixTyp", "rewrite", "delete", "add", "EDITS"]


def table(converter: str, forms: list[str]) -> dict:
    print(f"\n{converter}")
    print(f"  {'form':13}" + "".join(f"{h:>8}" for h in HEAD))
    tot = dict.fromkeys(COLS, 0)
    for f in forms:
        c = count(converter, f)
        if c is None:
            continue
        for k in COLS:
            tot[k] += c[k]
        print(f"  {f:13}" + "".join(f"{c[k]:8}" for k in COLS))
    print(f"  {'TOTAL':13}" + "".join(f"{tot[k]:8}" for k in COLS))
    return tot


def main(argv: list[str]) -> int:
    if argv[:1] == ["--detail"]:
        converter = argv[1] if len(argv) > 1 else "pdf2apr"
        rows: list = []
        for f in ["fed-w9", "ct-w4", "fed-8822", "fed-i9"]:
            count(converter, f, rows)
        for bucket in ["fix_label", "fix_type", "rewrite", "delete", "add"]:
            mine = [r for r in rows if r["bucket"] == bucket]
            print(f"\n=== {bucket} ({len(mine)}) ===")
            for r in mine:
                sim = f"{r['sim']:.2f}" if r["sim"] is not None else "    "
                made = (r["made"] or "")[:58]
                want = (r["want"] or "")[:58]
                types = (f" [{r['made_type']} -> {r['want_type']}/{r['want_kind']}]"
                         if bucket == "fix_type" else
                         f" [{r['want_type']}/{r['want_kind']}]" if bucket == "add" else "")
                print(f"  {r['form']:9} {sim}  made: {made!r}")
                if r["want"] is not None:
                    print(f"  {'':9} {'':4}  want: {want!r}{types}")
        return 0

    converters = argv or ["pdf2apr", "qwen3-vl-4b", "deterministic"]
    forms = ["fed-w9", "ct-w4", "fed-8822", "fed-i9"]
    totals = {c: table(c, forms) for c in converters}

    print(f"\nSame four forms, {totals[converters[0]]['gt']} ground-truth fields:")
    print(f"  {'converter':15}{'keep':>6}{'edits':>7}   edits per 10 real fields")
    for c, t in totals.items():
        if t["gt"]:
            print(f"  {c:15}{t['keep']:6}{t['edits']:7}   {10 * t['edits'] / t['gt']:.1f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

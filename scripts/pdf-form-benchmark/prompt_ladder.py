#!/usr/bin/env python3
"""Build pdf2apr's naming prompt one rule at a time, keeping only what helps.

Pre-registered: the failure causes, their order, three phrasings of each and
the acceptance test are fixed in this file before the model runs, so a rule
cannot be reworded after seeing how it scored.

The causes come from `edit_counts.py --detail pdf2apr` on the development
forms, ordered by how many edits each could remove. Each has three phrasings
that differ in kind, not wording:

  prose    one line added to the prompt's rule list
  example  a worked example in invented form text, never text from the
           development forms
  schema   a change to the reply template's own descriptions, using only keys
           pdf2apr's parser already reads

Each phrasing runs over the development forms on top of every rule accepted
so far. The one needing fewest edits is accepted when the development forms
need at least MARGIN fewer edits in total, no form needs more than MARGIN
more, and the rule's own target buckets fell. Otherwise the cause is skipped.
Temperature is 0 and reruns reproduce exactly, so one run a phrasing is
enough; the margin is for the unrelated outputs any prompt change moves.

The objective is edit_counts.py's edit total against the benchmark ground
truth with its signature fields removed. APR has no signature type and a
signature line is not a prompt [APR-MODEL-030], so the ladder must not learn
to emit them: a produced signature prompt counts as a delete. The unadjusted
count is logged beside it. The grader is otherwise unchanged, so a check-one
group the model types boolean still costs a fix_type against ground truth that
types it text.

Held-out forms run only with the starting prompt and the final one, and their
per-field detail is never printed.

    .venv/bin/python prompt_ladder.py --dry-run   # build prompts, score what exists
    .venv/bin/python prompt_ladder.py             # resumes; finished runs are skipped
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from dataclasses import dataclass
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(REPO / "pdf2apr"))

import edit_counts as E  # noqa: E402
import score as S  # noqa: E402
from pdf2apr import Options, convert  # noqa: E402
from pdf2apr.name import PROMPT, Namer  # noqa: E402

DEV = ["fed-w9", "ct-w4", "fed-8822", "fed-i9"]
HELD_OUT = ["ct-dmv-a25", "ct-dmv-a83", "ct-dmv-b225p", "ct-dmv-b58ind",
            "ct-dmv-j23", "fed-ss4", "fed-w4"]
MARGIN = 2
OUT = HERE / "results" / "ladder"
APR_GT = OUT / "ground-truth-without-signatures"
RAW_GT = S.GT_DIR
BUCKETS = ("keep", "fix_label", "fix_type", "rewrite", "delete", "add")


@dataclass(frozen=True)
class Variant:
    kind: str
    bullet: str = ""
    example: str = ""
    schema: tuple[tuple[str, str, str], ...] = ()
    """(key, how, text): "append" to a description, "replace" it, or "add" a key."""


@dataclass(frozen=True)
class Rule:
    id: str
    cause: str
    target: tuple[str, ...]
    variants: tuple[Variant, ...]


RULES: tuple[Rule, ...] = (
    Rule(
        "check-one",
        "alternative boxes for one question emitted as one prompt each",
        ("delete", "rewrite"),
        (
            Variant("prose", bullet=(
                "- Boxes that are alternative answers to ONE question, where only one may be "
                "checked (the form says \"check one\", or the choices exclude each other), are "
                "one answer: give each of them the same \"group\" value, use the question printed "
                "before the boxes as every one's label (not the words beside a single box), set "
                "field_kind \"choice\", and list the words beside each box, in order, in "
                "\"choices\". Boxes where several may be checked stay separate fields.")),
            Variant("example", example=(
                "Example -- a page prints \"Housing status (check only one)\" followed by boxes 14, "
                "15 and 16, captioned \"Own\", \"Rent\" and \"Other\". Those three boxes are one answer:\n"
                "{\"n\": 14, \"label\": \"Housing status\", \"section_id\": \"applicant\", \"field_kind\": \"choice\", "
                "\"expected_data_type\": \"text\", \"group\": \"housing-status\", \"choices\": [\"Own\", \"Rent\", \"Other\"]}\n"
                "{\"n\": 15, \"label\": \"Housing status\", \"section_id\": \"applicant\", \"field_kind\": \"choice\", "
                "\"expected_data_type\": \"text\", \"group\": \"housing-status\", \"choices\": [\"Own\", \"Rent\", \"Other\"]}\n"
                "{\"n\": 16, \"label\": \"Housing status\", \"section_id\": \"applicant\", \"field_kind\": \"choice\", "
                "\"expected_data_type\": \"text\", \"group\": \"housing-status\", \"choices\": [\"Own\", \"Rent\", \"Other\"]}\n"
                "Had it printed \"check all that apply\", each box would be its own field, labelled "
                "with its own caption.")),
            Variant("schema", schema=(
                ("group", "append",
                 "ALSO shared by boxes that are alternative answers to one question where only one "
                 "may be checked; each such box then takes that question as its label and field_kind choice"),
                ("choices", "add",
                 "[\"only when field_kind is choice: the words beside each alternative box, in order\"]"),
            )),
        ),
    ),
    Rule(
        "repeated-labels",
        "the same printed label on several boxes, with nothing to tell them apart",
        ("fix_label", "delete", "rewrite"),
        (
            Variant("prose", bullet=(
                "- If the same words label more than one box on the page (one question repeated in "
                "different columns, lists, parts or rows), add what tells this box apart -- its "
                "column, list, part or row heading -- in parentheses after the label, so two "
                "different answers never share a label.")),
            Variant("example", example=(
                "Example -- a table has two columns headed \"Vehicle 1\" and \"Vehicle 2\", and each "
                "column has boxes captioned \"Make\" and \"Year\". The four labels are "
                "\"Make (Vehicle 1)\", \"Year (Vehicle 1)\", \"Make (Vehicle 2)\" and \"Year (Vehicle 2)\".")),
            Variant("schema", schema=(
                ("label", "append",
                 "if the same words label other boxes on this page, add the column, list, part or "
                 "row heading that tells this one apart, in parentheses"),
            )),
        ),
    ),
    Rule(
        "item-numbers",
        "the printed item number or letter dropped from the label",
        ("fix_label",),
        (
            Variant("prose", bullet=(
                "- If the form prints an item number or letter before the question (for example 4, "
                "12b or C), begin the label with it exactly as printed.")),
            Variant("example", example=(
                "Example -- the form prints \"12b  Mailing address, if different\" beside box 20. "
                "The label is \"12b Mailing address, if different\".")),
            Variant("schema", schema=(
                ("label", "append",
                 "begin with the item number or letter printed before the question, if there is one"),
            )),
        ),
    ),
    Rule(
        "specific-types",
        "a generic data type where a specific one fits",
        ("fix_type",),
        (
            Variant("prose", bullet=(
                "- Pick the most specific expected_data_type: \"phone\" for any telephone or fax "
                "number, \"currency\" for any amount of money, \"email\" for an email address, "
                "\"date\" for a date. Use \"text\" or \"number\" only when nothing more specific fits.")),
            Variant("example", example=(
                "Example -- \"Cell no.\" is expected_data_type \"phone\"; \"Fee paid $\" is \"currency\"; "
                "\"Number of dependents\" is \"number\"; \"Date of birth\" is \"date\".")),
            Variant("schema", schema=(
                ("expected_data_type", "replace",
                 "text | multiline | email | phone (any telephone or fax number) | url | "
                 "number (a count, never money) | currency (any amount of money) | date | time | "
                 "datetime | boolean"),
            )),
        ),
    ),
    Rule(
        "instructions",
        "an instruction to the person used as a label",
        ("delete",),
        (
            Variant("prose", bullet=(
                "- A sentence telling the person what to do (\"check all that apply\", \"complete if "
                "you answered yes\", \"see instructions\") is not a label. Use the words that name what "
                "the box asks for; if only the instruction names it, use the caption beside the box.")),
            Variant("example", example=(
                "Example -- the form prints \"If you checked Yes, complete the following:\" and under "
                "it a box captioned \"Date of move\". The label is \"Date of move\", not the instruction.")),
            Variant("schema", schema=(
                ("label", "append",
                 "never an instruction such as 'check all that apply' -- the words naming what is asked"),
            )),
        ),
    ),
)

BASE = {
    "label": "The exact visible label/question text for field 1",
    "field_kind": "text_line | multiline | checkbox | choice | signature | date_field | table_cell",
    "expected_data_type": ("text | multiline | email | phone | url | number | currency | date | "
                           "time | datetime | boolean"),
    "group": ("optional: a name shared by every numbered field that is really ONE question split "
              "into several boxes, e.g. a social security number in three boxes"),
}
LAST_RULE = "- Output strict JSON: double-quoted keys, no trailing commas, no comments."


def build(variants: list[Variant]) -> str:
    """The shipped prompt with these variants applied, in order."""
    desc = dict(BASE)
    added: list[str] = []
    for v in variants:
        for key, how, text in v.schema:
            if how == "append":
                desc[key] = f"{desc[key]}; {text}"
            elif how == "replace":
                desc[key] = text
            else:
                added.append(f'"{key}": {text}')
    prompt = PROMPT
    for key, base in BASE.items():
        old = f'"{key}": "{base}"'
        assert prompt.count(old) == 1, f"the shipped prompt no longer describes {key} as expected"
        new = f'"{key}": "{desc[key]}"'
        if key == "group":
            new += "".join(f",\n     {a}" for a in added)
        prompt = prompt.replace(old, new)
    bullets = "".join(f"{v.bullet}\n" for v in variants if v.bullet)
    prompt = prompt.replace(LAST_RULE, bullets + LAST_RULE)
    examples = [v.example for v in variants if v.example]
    return prompt + ("\n\n" + "\n\n".join(examples) if examples else "")


def write_ground_truth_without_signatures(forms: list[str]) -> None:
    APR_GT.mkdir(parents=True, exist_ok=True)
    for fid in forms:
        gt = json.loads((RAW_GT / f"{fid}.json").read_text())
        gt["fields"] = [f for f in gt["fields"] if f.get("field_kind") != "signature"]
        (APR_GT / f"{fid}.json").write_text(json.dumps(gt, indent=1) + "\n")


def counts(converter: str, fid: str, without_signatures: bool) -> dict:
    S.GT_DIR = APR_GT if without_signatures else RAW_GT
    try:
        return E.count(converter, fid)
    finally:
        S.GT_DIR = RAW_GT


class HeldNamer(Namer):
    """One loaded model for every conversion. convert() closes its namer after
    each form, which would unload the model twenty times; the real close is
    left to the caller, once."""

    def __init__(self, inner: Namer) -> None:
        self.inner = inner
        self.raw_dir = OUT

    def name_page(self, page):
        questions, sections, text = self.inner.name_page(page)
        (self.raw_dir / f"page-{page.page}.txt").write_text(text)
        return questions, sections, text

    def close(self) -> None:
        pass


def run(held: HeldNamer, run_id: str, prompt: str, forms: list[str]) -> dict:
    prompt_file = OUT / "prompts" / f"{run_id}.txt"
    prompt_file.parent.mkdir(parents=True, exist_ok=True)
    if prompt_file.exists() and prompt_file.read_text() != prompt:
        raise SystemExit(f"{run_id} was run with a different prompt; remove its outputs to rerun it")
    prompt_file.write_text(prompt)
    held.inner.prompt = prompt
    converter = f"ladder/{run_id}"
    out = {}
    for fid in forms:
        folder = S.APRT_DIR / converter
        aprt, meta_file = folder / f"{fid}.aprt", folder / f"{fid}.run.json"
        if not (aprt.exists() and meta_file.exists()):
            folder.mkdir(parents=True, exist_ok=True)
            held.raw_dir = OUT / "raw" / run_id / fid
            held.raw_dir.mkdir(parents=True, exist_ok=True)
            t0 = time.time()
            result = convert(HERE / "corpus" / f"{fid}.pdf", Options(title=fid, namer=held))
            aprt.write_text(json.dumps(result.document, indent=2) + "\n")
            lines = [message for _, message in result.report.lines]
            meta_file.write_text(json.dumps({
                "seconds": round(time.time() - t0),
                "fell_back": any("fell back" in m for m in lines),
                "report": lines,
            }, indent=1) + "\n")
            print(f"    {run_id} {fid}: {time.time() - t0:.0f}s", flush=True)
        meta = json.loads(meta_file.read_text())
        out[fid] = {"apr": counts(converter, fid, True), "raw": counts(converter, fid, False), **meta}
    return out


def total(res: dict, forms: list[str], buckets=("edits",), key: str = "apr") -> int:
    return sum(res[f][key][b] for f in forms for b in buckets)


def summary(res: dict, forms: list[str]) -> dict:
    return {
        "edits": total(res, forms),
        "edits_against_unadjusted_ground_truth": total(res, forms, key="raw"),
        "buckets": {b: total(res, forms, (b,)) for b in BUCKETS},
        "per_form": {f: res[f]["apr"]["edits"] for f in forms},
        "fell_back": [f for f in forms if res[f]["fell_back"]],
        "seconds": sum(res[f]["seconds"] for f in forms),
    }


def acceptable(current: dict, trial: dict, rule: Rule) -> tuple[bool, str]:
    before, after = total(current, DEV), total(trial, DEV)
    tb, ta = total(current, DEV, rule.target), total(trial, DEV, rule.target)
    said = f"edits {before} -> {after}; target {'+'.join(rule.target)} {tb} -> {ta}"
    if after > before - MARGIN:
        return False, f"{said}: needs at least {MARGIN} fewer edits"
    worse = [f for f in DEV if trial[f]["apr"]["edits"] > current[f]["apr"]["edits"] + MARGIN]
    if worse:
        return False, f"{said}: {', '.join(worse)} got more than {MARGIN} edits worse"
    if ta >= tb:
        return False, f"{said}: its own target did not fall"
    return True, said


def save(log: dict) -> None:
    (OUT / "ladder.json").write_text(json.dumps(log, indent=1) + "\n")


def climb(held: HeldNamer) -> None:
    log: dict = {"margin": MARGIN, "dev": DEV, "held_out": HELD_OUT,
                 "rounds": [], "accepted": [], "skipped": []}
    accepted: list[Variant] = []
    current = run(held, "r0-baseline", build([]), DEV)
    log["rounds"].append({"run": "r0-baseline", **summary(current, DEV)})
    save(log)
    for i, rule in enumerate(RULES, start=1):
        trials = []
        for v in rule.variants:
            run_id = f"r{i}-{rule.id}-{v.kind}"
            prompt = build(accepted + [v])
            res = run(held, run_id, prompt, DEV)
            ok, why = acceptable(current, res, rule)
            trials.append((total(res, DEV), len(prompt), run_id, v, res, ok))
            log["rounds"].append({"run": run_id, "rule": rule.id, "kind": v.kind,
                                  "passes": ok, "why": why, **summary(res, DEV)})
            print(f"  {run_id}: {'PASS' if ok else 'fail'} -- {why}", flush=True)
            save(log)
        passing = sorted((t for t in trials if t[5]), key=lambda t: (t[0], t[1]))
        if passing:
            _, _, run_id, v, res, _ = passing[0]
            accepted.append(v)
            current = res
            log["accepted"].append(run_id)
        else:
            log["skipped"].append(rule.id)
        save(log)

    final = build(accepted)
    (OUT / "final_prompt.txt").write_text(final)
    log["held_out_results"] = {"h0-baseline": summary(run(held, "h0-baseline", build([]), HELD_OUT), HELD_OUT)}
    if accepted:
        log["held_out_results"]["h1-final"] = summary(run(held, "h1-final", final, HELD_OUT), HELD_OUT)
    save(log)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--dry-run", action="store_true",
                    help="build every prompt and score existing pdf2apr outputs; load no model")
    args = ap.parse_args()

    assert build([]) == PROMPT, "an empty ladder must be exactly the shipped prompt"
    write_ground_truth_without_signatures(DEV + HELD_OUT)

    if args.dry_run:
        for i, rule in enumerate(RULES, start=1):
            for v in rule.variants:
                grew = len(build([v])) - len(PROMPT)
                print(f"  r{i}-{rule.id}-{v.kind}: +{grew} chars")
        every = build([v for r in RULES for v in r.variants])
        (OUT / "prompts").mkdir(parents=True, exist_ok=True)
        (OUT / "prompts" / "dry-run-every-variant.txt").write_text(every)
        for fid in DEV:
            a, r = counts("pdf2apr", fid, True), counts("pdf2apr", fid, False)
            print(f"  shipped pdf2apr {fid}: {a['edits']} edits without signatures, {r['edits']} with")
        return 0

    import resource_guard
    from pdf2apr.name import MlxNamer

    resource_guard.set_mlx_safety_limits()
    inner = MlxNamer()
    try:
        climb(HeldNamer(inner))
    finally:
        inner.close()
        print("model unloaded", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Build a strictly valid .aprt document from the shared intermediate
representation (see parsers.py).

This is deliberately the SAME code path for every model: none of the four
models is asked to produce valid APR JSON itself (only Qwen even attempts
JSON, and it's an intermediate schema we invented, not the real APR format).
That keeps the benchmark measuring what we actually care about -- did the
model find the right sections/fields/labels/types -- rather than which
model happens to be best at getting fiddly JSON-Schema hard-rules right.
"""
import re


def _slug(text: str, seen: set, fallback: str) -> str:
    base = re.sub(r"[^a-z0-9]+", "-", (text or "").lower()).strip("-") or fallback
    sid, n = base, 2
    while sid in seen:
        sid = f"{base}-{n}"
        n += 1
    seen.add(sid)
    return sid


def build_aprt(form_name: str, ir: dict) -> dict:
    seen_section_ids: set = set()
    seen_field_ids: set = set()

    sections_by_id = {}
    order = []
    for sec in ir["sections"]:
        title = (sec.get("title") or "Section").strip() or "Section"
        sid = _slug(sec.get("id") or title, seen_section_ids, "section")
        sections_by_id[sec["id"]] = {"id": sid, "title": title[:150], "prompts": []}
        order.append(sec["id"])

    if not sections_by_id:
        fallback_id = _slug("form", seen_section_ids, "section")
        sections_by_id["__default__"] = {"id": fallback_id, "title": form_name[:150], "prompts": []}
        order.append("__default__")
    default_key = order[0]

    for f in ir["fields"]:
        label = (f.get("label") or "").strip()
        if not label:
            continue
        key = f.get("section_id") if f.get("section_id") in sections_by_id else default_key
        fid = _slug(label, seen_field_ids, "field")
        hints = {"expectedDataType": f.get("expected_data_type") or "text"}
        if f.get("choices"):
            hints["suggestedValues"] = [str(c) for c in f["choices"]][:50]
        if f.get("field_kind") == "multiline":
            hints["expectedDataType"] = "multiline"
        prompt = {"id": fid, "label": label[:300], "response": "", "hints": hints}
        sections_by_id[key]["prompts"].append(prompt)

    sections = [sections_by_id[k] for k in order if sections_by_id[k]["prompts"]]
    if not sections:
        # No fields at all is still a structurally valid (if useless) document;
        # keep the one empty default section so the doc has >=1 section.
        sections = [sections_by_id[default_key]]

    return {
        "aprVersion": "1.0-beta.6",
        "documentType": "template",
        "metadata": {"title": form_name[:200]},
        "sections": sections,
    }

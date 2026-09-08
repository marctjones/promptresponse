"""Turn each model's native raw output into one common intermediate
representation (IR):

    {
      "sections": [{"id": str, "title": str}],
      "fields": [
        {
          "label": str, "section_id": str|None, "field_kind": str,
          "expected_data_type": str, "required": bool|None,
          "choices": list[str]|None, "bbox": [x1,y1,x2,y2]|None, "page": int,
        }
      ],
      "tables": [{"label": str, "page": int, "raw": str}],
      "parse_errors": [str],
    }

None of these four models natively marks "this is the blank the answer goes
in" (that's absence of content, not content -- see the benchmark README for
why). What they DO give us, in varying quality, is: what text exists, roughly
where, and (DocTags/dots.ocr/Qwen) some notion of structural role. So the
shared heuristic below is doing real work: deciding which OCR'd text spans
are actually field labels versus page furniture, for the three
non-instructed models (doctags, dots.ocr, florence). Qwen was simply asked
for the answer directly and is trusted to have made that judgment itself.
"""
import json
import re

LABEL_MAX_LEN = 100
NOISE_PATTERNS = [
    r"^page \d+", r"^\d+$", r"^rev\.?\s", r"^www\.", r"\.gov$",
]
SKIP_CATEGORIES = {"picture", "caption", "footnote", "page-footer", "page-header"}
SECTION_CATEGORIES = {"section-header", "title"}


def _looks_like_noise(text: str) -> bool:
    t = text.strip().lower()
    if not t or len(t) > 220:
        return True
    if len(re.findall(r"[a-z0-9]", t)) < 2:
        # Bare glyphs some models emit as their own <text> span (checkbox
        # boxes "☐", bullets, rule lines) rather than wrapping them in a
        # dedicated checkbox tag -- not a real field label on their own.
        return True
    return any(re.search(p, t) for p in NOISE_PATTERNS)


def _looks_like_label(text: str) -> bool:
    t = text.strip()
    if not t or len(t) > LABEL_MAX_LEN:
        return False
    if _looks_like_noise(t):
        return False
    # Signals that this reads like a question/label rather than a sentence
    # of instructions: ends with ':', is short and Title/ALL CAPS, or has a
    # parenthetical clarifier typical of form labels ("(Last, First, MI)").
    if t.endswith(":"):
        return True
    if re.search(r"\([^)]{2,60}\)\s*$", t) and len(t) < 90:
        return True
    words = t.split()
    if len(words) <= 8 and not t.endswith("."):
        return True
    return False


def _guess_data_type(label: str, kind: str) -> str:
    l = label.lower()
    if kind in ("checkbox",):
        return "boolean"
    if "email" in l:
        return "email"
    if "phone" in l or "telephone" in l:
        return "phone"
    if "date" in l or "d.o.b" in l or "birth" in l:
        return "date"
    if "signature" in l or kind == "signature":
        return "text"
    if any(k in l for k in ("amount", "price", "fee", "$", "salary", "wage")):
        return "currency"
    if any(k in l for k in ("number", "no.", "ssn", "ein", "zip", "qty", "quantity")):
        return "number"
    if any(k in l for k in ("address", "explain", "describe", "reason", "comments")):
        return "multiline"
    return "text"


def _new_section_id(title: str, seen: set) -> str:
    base = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-") or "section"
    sid, n = base, 2
    while sid in seen:
        sid = f"{base}-{n}"
        n += 1
    seen.add(sid)
    return sid


def _mk_ir():
    return {"sections": [], "fields": [], "tables": [], "parse_errors": []}


def _balance_close(text: str) -> str:
    """Append closing brackets/braces to balance an otherwise-valid but
    truncated JSON fragment (a model that hit max_tokens mid-object)."""
    opens = []
    in_str = False
    escape = False
    for ch in text:
        if in_str:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_str = False
            continue
        if ch == '"':
            in_str = True
        elif ch in "{[":
            opens.append(ch)
        elif ch in "}]" and opens:
            opens.pop()
    if in_str:
        text += '"'
    return text + "".join("]" if c == "[" else "}" for c in reversed(opens))


def _parse_json_lenient(candidate: str):
    """json.loads with one fallback: if the model's output was cut off by
    max_tokens mid-object, trim back to the last complete '}' and balance-
    close the rest, rather than discarding the whole (mostly good) page."""
    try:
        return json.loads(candidate)
    except json.JSONDecodeError:
        last_brace = candidate.rfind("}")
        if last_brace == -1:
            raise
        return json.loads(_balance_close(candidate[: last_brace + 1]))


def _add_section(ir, title, seen_ids):
    sid = _new_section_id(title, seen_ids)
    ir["sections"].append({"id": sid, "title": title.strip()[:150]})
    return sid


# --------------------------------------------------------------------------
# doctags (Granite-Docling)
# --------------------------------------------------------------------------
_DOCTAG_RE = re.compile(
    r"<(?P<tag>[a-z_]+\d*)>"
    r"(?:<loc_(?P<x1>\d+)><loc_(?P<y1>\d+)><loc_(?P<x2>\d+)><loc_(?P<y2>\d+)>)?"
    r"(?P<text>.*?)"
    r"</(?P=tag)>",
    re.DOTALL,
)


def _strip_outer_doctag(raw: str) -> str:
    # The model wraps the whole page in one <doctag>...</doctag>. Because
    # that's the same tag name as itself, a naive same-pattern scan matches
    # it as ONE opaque span (via the regex's own backreference) whenever
    # generation completes cleanly, silently swallowing every inner tag.
    # Strip it as plain text first so finditer only ever sees inner tags.
    t = raw.strip()
    if t.startswith("<doctag>"):
        t = t[len("<doctag>"):]
    if t.endswith("</doctag>"):
        t = t[: -len("</doctag>")]
    return t


_LIST_ITEM_RE = re.compile(
    r"<list_item>"
    r"(?:<loc_(?P<x1>\d+)><loc_(?P<y1>\d+)><loc_(?P<x2>\d+)><loc_(?P<y2>\d+)>)?"
    r"(?P<text>.*?)</list_item>",
    re.DOTALL,
)


def parse_doctags_pages(raw_pages: list[str]) -> dict:
    ir = _mk_ir()
    seen_ids = set()
    current_section = None
    for page_no, raw in enumerate(raw_pages, start=1):
        try:
            body = _strip_outer_doctag(raw)
            # list_item is nested inside <unordered_list>/<ordered_list>, so
            # the top-level tag scan below never sees it as its own tag;
            # scan for it separately (form fields occasionally render as
            # list items, e.g. a checkbox group written as a bulleted list).
            for lm in _LIST_ITEM_RE.finditer(body):
                text = re.sub(r"\s+", " ", lm.group("text") or "").strip()
                if text and _looks_like_label(text):
                    bbox = ([int(lm.group(g)) for g in ("x1", "y1", "x2", "y2")]
                            if lm.group("x1") is not None else None)
                    kind = "multiline" if len(text.split()) > 8 else "text_line"
                    ir["fields"].append({
                        "label": text, "section_id": current_section,
                        "field_kind": kind,
                        "expected_data_type": _guess_data_type(text, kind),
                        "required": None, "choices": None, "bbox": bbox, "page": page_no,
                    })
            for m in _DOCTAG_RE.finditer(body):
                tag = m.group("tag")
                text = re.sub(r"\s+", " ", m.group("text") or "").strip()
                bbox = None
                if m.group("x1") is not None:
                    bbox = [int(m.group(g)) for g in ("x1", "y1", "x2", "y2")]
                if tag in ("page_header", "page_footer", "picture", "caption", "footnote"):
                    continue
                if tag.startswith("section_header") or tag == "title":
                    if text:
                        current_section = _add_section(ir, text, seen_ids)
                    continue
                if tag in ("checkbox_unselected", "checkbox_selected"):
                    if text:
                        ir["fields"].append({
                            "label": text, "section_id": current_section,
                            "field_kind": "checkbox", "expected_data_type": "boolean",
                            "required": None, "choices": None, "bbox": bbox, "page": page_no,
                        })
                    continue
                if tag == "otsl":
                    ir["tables"].append({"label": text[:200], "page": page_no, "raw": text[:2000]})
                    continue
                if tag == "text" and text and _looks_like_label(text):
                    kind = "multiline" if len(text.split()) > 8 else "text_line"
                    ir["fields"].append({
                        "label": text, "section_id": current_section,
                        "field_kind": kind,
                        "expected_data_type": _guess_data_type(text, kind),
                        "required": None, "choices": None, "bbox": bbox, "page": page_no,
                    })
        except Exception as exc:  # noqa: BLE001
            ir["parse_errors"].append(f"page {page_no}: {exc}")
    return ir


# --------------------------------------------------------------------------
# dots.ocr native JSON: [{"bbox":[...], "category": "...", "text": "..."}]
# --------------------------------------------------------------------------
def parse_dots_ocr_pages(raw_pages: list[str]) -> dict:
    ir = _mk_ir()
    seen_ids = set()
    current_section = None
    for page_no, raw in enumerate(raw_pages, start=1):
        try:
            elements = _extract_json_array(raw)
            for el in elements:
                category = (el.get("category") or "").strip()
                text = re.sub(r"\s+", " ", (el.get("text") or "")).strip()
                bbox = el.get("bbox")
                cat_l = category.lower()
                if cat_l in SKIP_CATEGORIES:
                    continue
                if cat_l in SECTION_CATEGORIES:
                    if text:
                        current_section = _add_section(ir, text, seen_ids)
                    continue
                if cat_l == "table":
                    ir["tables"].append({"label": text[:200], "page": page_no, "raw": text[:2000]})
                    continue
                if cat_l in ("text", "list-item") and text and _looks_like_label(text):
                    kind = "multiline" if len(text.split()) > 8 else "text_line"
                    ir["fields"].append({
                        "label": text, "section_id": current_section,
                        "field_kind": kind,
                        "expected_data_type": _guess_data_type(text, kind),
                        "required": None, "choices": None, "bbox": bbox, "page": page_no,
                    })
        except Exception as exc:  # noqa: BLE001
            ir["parse_errors"].append(f"page {page_no}: {exc}")
    return ir


def _extract_json_array(raw: str) -> list:
    raw = raw.strip()
    fence = re.search(r"```(?:json)?\s*(.*?)```", raw, re.DOTALL)
    if fence:
        raw = fence.group(1).strip()
    start = raw.find("[")
    end = raw.rfind("]")
    if start != -1 and end != -1:
        try:
            return json.loads(raw[start:end + 1])
        except json.JSONDecodeError:
            pass
    if start != -1:
        # Truncated mid-array (max_tokens cutoff): trim to the last complete
        # element and balance-close, rather than discarding the whole page.
        candidate = raw[start:]
        last_obj_end = candidate.rfind("}")
        if last_obj_end != -1:
            return json.loads(_balance_close(candidate[: last_obj_end + 1]))
    # dots.ocr sometimes wraps in a single object; try to find a list value
    obj_start, obj_end = raw.find("{"), raw.rfind("}")
    if obj_start != -1 and obj_end != -1:
        obj = _parse_json_lenient(raw[obj_start:obj_end + 1])
        for v in obj.values():
            if isinstance(v, list):
                return v
    raise ValueError("no JSON array found in dots.ocr output")


# --------------------------------------------------------------------------
# Florence-2 <OCR_WITH_REGION>: "<s>TEXT1<loc_..><loc_..><loc_..><loc_..>TEXT2..."
# (base-ft emits a flat token stream: label text immediately followed by 4
# loc tokens per bounding quad's bbox, repeated. No category information.)
# --------------------------------------------------------------------------
_FLORENCE_SPAN_RE = re.compile(
    r"(?P<text>[^<>]+?)"
    r"(?:<loc_(?P<x1>\d+)><loc_(?P<y1>\d+)><loc_(?P<x2>\d+)><loc_(?P<y2>\d+)>)+",
)


def parse_florence_pages(raw_pages: list[str]) -> dict:
    ir = _mk_ir()
    seen_ids = set()
    default_section = None
    for page_no, raw in enumerate(raw_pages, start=1):
        try:
            body = raw.replace("<s>", "").replace("</s>", "")
            spans = [
                (m.group("text").strip(), [int(m.group(g)) for g in ("x1", "y1", "x2", "y2")])
                for m in _FLORENCE_SPAN_RE.finditer(body)
            ]
            if spans and default_section is None:
                default_section = _add_section(ir, "Form", seen_ids)
            for text, bbox in spans:
                if not text or _looks_like_noise(text):
                    continue
                if _looks_like_label(text):
                    kind = "multiline" if len(text.split()) > 8 else "text_line"
                    ir["fields"].append({
                        "label": text, "section_id": default_section,
                        "field_kind": kind,
                        "expected_data_type": _guess_data_type(text, kind),
                        "required": None, "choices": None, "bbox": bbox, "page": page_no,
                    })
        except Exception as exc:  # noqa: BLE001
            ir["parse_errors"].append(f"page {page_no}: {exc}")
    return ir


# --------------------------------------------------------------------------
# chat-json (Qwen3-VL): model was asked directly for our IR shape, per page.
# --------------------------------------------------------------------------
def parse_chat_json_pages(raw_pages: list[str]) -> dict:
    ir = _mk_ir()
    seen_ids = set()
    title_to_id = {}
    for page_no, raw in enumerate(raw_pages, start=1):
        try:
            text = raw.strip()
            fence = re.search(r"```(?:json)?\s*(.*?)```", text, re.DOTALL)
            if fence:
                text = fence.group(1).strip()
            start = text.find("{")
            if start == -1:
                raise ValueError("no JSON object found")
            page_obj = _parse_json_lenient(text[start:])

            local_to_global = {}
            for sec in page_obj.get("sections", []):
                title = (sec.get("title") or "Section").strip()
                if title in title_to_id:
                    gid = title_to_id[title]
                else:
                    gid = _add_section(ir, title, seen_ids)
                    title_to_id[title] = gid
                local_to_global[sec.get("id")] = gid

            for f in page_obj.get("fields", []):
                label = (f.get("label") or "").strip()
                if not label:
                    continue
                ir["fields"].append({
                    "label": label,
                    "section_id": local_to_global.get(f.get("section_id")),
                    "field_kind": f.get("field_kind") or "text_line",
                    "expected_data_type": f.get("expected_data_type") or "text",
                    "required": f.get("required"),
                    "choices": f.get("choices"),
                    "bbox": None,
                    "page": page_no,
                })
        except Exception as exc:  # noqa: BLE001
            ir["parse_errors"].append(f"page {page_no}: {exc}")
    return ir


# --------------------------------------------------------------------------
# PaliGemma 2 "ocr": plain full-page text transcript, no location tokens at
# all in this quantized MLX build -- despite the model's own documentation
# describing OCR-with-localization ({transcription, bbox} pairs), empirical
# testing showed flat text only. Real finding, not a bug: treated honestly
# as a text-only source using the same label heuristic as the other
# non-instructed models, just without bbox data.
# --------------------------------------------------------------------------
def parse_paligemma_pages(raw_pages: list[str]) -> dict:
    ir = _mk_ir()
    seen_ids = set()
    default_section = None
    for page_no, raw in enumerate(raw_pages, start=1):
        try:
            lines = [l.strip() for l in raw.splitlines() if l.strip()]
            if lines and default_section is None:
                default_section = _add_section(ir, "Form", seen_ids)
            for line in lines:
                if _looks_like_label(line):
                    kind = "multiline" if len(line.split()) > 8 else "text_line"
                    ir["fields"].append({
                        "label": line, "section_id": default_section,
                        "field_kind": kind,
                        "expected_data_type": _guess_data_type(line, kind),
                        "required": None, "choices": None, "bbox": None, "page": page_no,
                    })
        except Exception as exc:  # noqa: BLE001
            ir["parse_errors"].append(f"page {page_no}: {exc}")
    return ir


PARSERS = {
    "doctags": parse_doctags_pages,
    "dots-ocr-native": parse_dots_ocr_pages,
    "florence-detection": parse_florence_pages,
    "chat-json": parse_chat_json_pages,
    "paligemma-detection": parse_paligemma_pages,
}


def parse(strategy: str, raw_pages: list[str]) -> dict:
    return PARSERS[strategy](raw_pages)

#!/usr/bin/env python3
"""The TypeScript HTML renderer's conformance driver.

`scripts/typescript-renderer-driver.mjs` renders each case with the shipped
`renderHtml` and hands back markup. This half reads that markup the way a browser's
accessibility layer would — computing accessible names, the focus order, described-by
relations and header associations — and writes the interaction snapshot
`docs/RENDERER_CONFORMANCE.md` defines.

Nothing here consults the document except to turn an element's `data-apr-prompt` or
`data-apr-section` id into a JSON pointer, which is a join and not a judgement. Every
answer a rule is scored on comes out of the HTML. A driver that walked the document
instead would report the document back and pass chapter 13 without rendering.

    python3 scripts/run-renderer-conformance.py \\
        --driver "python3 scripts/typescript-renderer-driver.py"
"""
from __future__ import annotations

import html.parser
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
RENDERER = ROOT / "scripts" / "typescript-renderer-driver.mjs"
DIST = ROOT / "typescript" / "dist" / "index.js"

VOID = {"area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta",
        "param", "source", "track", "wbr"}
FOCUSABLE = {"input", "textarea", "select", "button"}
# What the accessibility layer would call each control. `input` is refined by type.
ROLES = {"textarea": "textbox", "select": "combobox", "button": "button",
         "fieldset": "group", "table": "table", "tr": "row", "th": "columnheader",
         "td": "cell", "a": "link", "script": "script", "iframe": "iframe",
         "embed": "embed", "object": "object"}
INPUT_ROLES = {"checkbox": "checkbox", "radio": "radio", "button": "button",
               "submit": "button", "hidden": "none"}


class Element:
    __slots__ = ("tag", "attrs", "children", "parent")

    def __init__(self, tag: str, attrs: dict, parent):
        self.tag, self.attrs, self.parent = tag, attrs, parent
        # Text and elements together, in order, so a name reads the way it is written.
        self.children: list = []

    def elements(self):
        for child in self.children:
            if isinstance(child, Element):
                yield child
                yield from child.elements()

    def content(self) -> str:
        """The element's text, as an accessible name computation concatenates it."""
        return normalise(" ".join(
            child if isinstance(child, str) else child.content()
            for child in self.children))


def normalise(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


class Tree(html.parser.HTMLParser):
    """A document tree. Deliberately generic: it knows HTML, not this renderer."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.root = Element("#document", {}, None)
        self.open = [self.root]

    def handle_starttag(self, tag, attrs):
        element = Element(tag, {k: (v or "") for k, v in attrs}, self.open[-1])
        self.open[-1].children.append(element)
        if tag not in VOID:
            self.open.append(element)

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        if tag not in VOID and self.open[-1].tag == tag:
            self.open.pop()

    def handle_endtag(self, tag):
        for index in range(len(self.open) - 1, 0, -1):
            if self.open[index].tag == tag:
                del self.open[index:]
                return

    def handle_data(self, data):
        self.open[-1].children.append(data)


def parse(markup: str) -> Element:
    tree = Tree()
    tree.feed(markup)
    tree.close()
    return tree.root


# ── the accessibility computation ───────────────────────────────────────────────

def index_by_id(root: Element) -> dict[str, Element]:
    return {e.attrs["id"]: e for e in root.elements() if e.attrs.get("id")}


def label_for(root: Element) -> dict[str, Element]:
    return {e.attrs["for"]: e for e in root.elements()
            if e.tag == "label" and e.attrs.get("for")}


def texts_of(ids: str, by_id: dict[str, Element]) -> str:
    return normalise(" ".join(by_id[ref].content() for ref in ids.split()
                              if ref in by_id))


def accessible_name(element: Element, by_id, labels) -> str:
    """HTML-AAM's order, as far as this contract needs it."""
    if element.attrs.get("aria-labelledby"):
        name = texts_of(element.attrs["aria-labelledby"], by_id)
        if name:
            return name
    if normalise(element.attrs.get("aria-label", "")):
        return normalise(element.attrs["aria-label"])
    if element.tag == "fieldset":
        legend = next((c for c in element.children if c.tag == "legend"), None)
        if legend is not None:
            return legend.content()
    identifier = element.attrs.get("id")
    if identifier and identifier in labels:
        return labels[identifier].content()
    ancestor = element.parent
    while ancestor is not None:
        if ancestor.tag == "label":
            return ancestor.content()
        ancestor = ancestor.parent
    if normalise(element.attrs.get("title", "")):
        return normalise(element.attrs["title"])
    # Last, and a finding when it is what names a field: a placeholder is not a label.
    return normalise(element.attrs.get("placeholder", ""))


def role_of(element: Element) -> str | None:
    if element.attrs.get("role"):
        return element.attrs["role"]
    if element.tag == "input":
        return INPUT_ROLES.get(element.attrs.get("type", "text"), "textbox")
    if element.tag == "a":
        return "link" if element.attrs.get("href") else None
    return ROLES.get(element.tag)


def is_focusable(element: Element) -> bool:
    tabindex = element.attrs.get("tabindex")
    if tabindex is not None:
        try:
            return int(tabindex) >= 0 and "disabled" not in element.attrs
        except ValueError:
            pass
    if "disabled" in element.attrs or element.attrs.get("type") == "hidden":
        return False
    return element.tag in FOCUSABLE or (element.tag == "a" and element.attrs.get("href"))


def owning_pointer(element: Element, pointers: dict[str, str]) -> str | None:
    node = element
    while node is not None:
        for attribute in ("data-apr-prompt", "data-apr-section"):
            pointer = pointers.get(node.attrs.get(attribute, ""))
            if pointer:
                return pointer
        node = node.parent
    return None


def column_headers(root: Element, by_id) -> dict[int, Element]:
    """Which element heads each column, by position, for a real HTML table."""
    headers: dict[int, Element] = {}
    for row in (e for e in root.elements() if e.tag == "tr"):
        cells = [c for c in row.children if c.tag in ("th", "td")]
        if all(c.tag == "th" for c in cells) and cells:
            for index, cell in enumerate(cells):
                headers.setdefault(index, cell)
    return headers


def snapshot_of(markup: str, pointers: dict[str, str], rendered: dict) -> dict:
    root = parse(markup)
    by_id, labels = index_by_id(root), label_for(root)
    headers = column_headers(root, by_id)
    nodes, order = [], 0

    for element in root.elements():
        role = role_of(element)
        if role is None or element.tag in ("legend", "label", "small", "p", "h1"):
            continue
        node = {
            "id": element.attrs.get("id") or element.attrs.get("data-apr-section")
            or element.attrs.get("data-apr-prompt"),
            "role": role,
            "name": accessible_name(element, by_id, labels),
        }
        pointer = owning_pointer(element, pointers)
        if pointer:
            node["documentPointer"] = pointer
        if is_focusable(element):
            node["keyboardOrder"] = order
            order += 1
        if element.tag in ("input", "textarea", "select"):
            node["editable"] = ("readonly" not in element.attrs
                                and "disabled" not in element.attrs)
            node["value"] = (element.attrs.get("value", "") if element.tag == "input"
                             else element.content())
        described = element.attrs.get("aria-describedby")
        if described:
            node["describedBy"] = described
            node["helpText"] = texts_of(described, by_id)
        if element.tag == "th":
            node["isColumnHeader"] = element.attrs.get("scope", "col") == "col"
        if element.tag == "td":
            explicit = element.attrs.get("headers", "").split()
            position = [c for c in element.parent.children
                        if c.tag in ("th", "td")].index(element)
            header = (explicit[0] if explicit
                      else (headers[position].attrs.get("id") if position in headers else None))
            if header:
                node["columnHeader"] = header
        nodes.append(node)

    return {
        "id": rendered["id"],
        "nodes": nodes,
        # Observed while rendering, in the renderer's own process, rather than asserted.
        "requests": rendered.get("requests") or [],
        "saveResult": rendered.get("saveResult"),
    }


def pointers_of(document: dict) -> dict[str, str]:
    """Every section and prompt id, against the pointer that reaches it.

    Ids are unique document-wide, so this join says which element renders which
    member without saying anything about how it was rendered.
    """
    found: dict[str, str] = {}

    def walk(sections, prefix):
        for index, section in enumerate(sections or []):
            pointer = f"{prefix}/sections/{index}"
            found[section.get("id", "")] = pointer
            for position, prompt in enumerate(section.get("prompts") or []):
                found[prompt.get("id", "")] = f"{pointer}/prompts/{position}"
            walk(section.get("sections"), pointer)

    walk(document.get("sections"), "")
    found.pop("", None)
    return found


def main() -> int:
    if not DIST.exists():
        print(f"{DIST.relative_to(ROOT)} is missing; run `npm run build` in typescript/",
              file=sys.stderr)
        return 2
    suite = json.load(sys.stdin)
    completed = subprocess.run(
        ["node", str(RENDERER)], input=json.dumps(suite),
        capture_output=True, text=True, cwd=ROOT / "scripts")
    if completed.returncode != 0:
        print(completed.stderr.strip()[:2000], file=sys.stderr)
        return 1
    rendered = json.loads(completed.stdout)

    results = []
    for case in suite["cases"]:
        answer = next((r for r in rendered["cases"] if r["id"] == case["id"]), None)
        if answer is None:
            continue
        if answer.get("failure"):
            # A renderer that refused the document rendered no interface, and says so
            # rather than reporting an empty one that would read as a blank page.
            results.append({"id": case["id"], "nodes": [], "requests": [],
                            "saveResult": {"written": False,
                                           "blockedBy": answer["failure"]}})
            continue
        results.append(snapshot_of(answer["html"], pointers_of(json.loads(case["document"])),
                                   answer))

    json.dump({
        "implementation": {
            "name": rendered["name"],
            "version": rendered["version"],
            # Renders; does not export. The export rule belongs to a surface that does.
            "surfaces": ["renderer"],
        },
        "results": results,
    }, sys.stdout)
    return 0


if __name__ == "__main__":
    sys.exit(main())

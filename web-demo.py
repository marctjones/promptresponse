#!/usr/bin/env python3
"""A local web demo: render an APR document as an HTML form and collect answers.

Runs entirely on the Python SDK in ``python/``. Nothing here parses APR, decides
what is valid, or has an opinion about the format - that all comes from the
library, which is gated by the shared conformance corpus. A demo that reimplements
the format is a demo that can disagree with it, and the previous version of this
file did exactly that: it hand-rolled its own validity check and had never heard
of tables or roles.

    python3 web-demo.py examples/field-types-showcase.aprt
    python3 web-demo.py filled.aprf --port 8080

Templates (.aprt) render blank, filled forms (.aprf) render with their answers.
Submitting writes a filled document beside the source and prints it.
"""

import argparse
import html
import json
import pathlib
import sys
from datetime import datetime, timezone

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent / "python"))

import promptresponse as pr
from promptresponse import roles as role_api

try:
    from flask import Flask, request
except ImportError:  # pragma: no cover - the launcher installs it
    sys.exit("Flask is required: pip install flask")

app = Flask(__name__)

DOCUMENT = None
SOURCE_PATH = None


# ── Rendering ────────────────────────────────────────────────────────────────

#: expectedDataType to HTML input type. Unregistered types fall through to text,
#: which is what the format asks for: an unrecognised hint degrades rather than
#: erroring (specification 4.7).
INPUT_TYPES = {
    "email": "email",
    "phone": "tel",
    "url": "url",
    "date": "date",
    "time": "time",
    "datetime": "datetime-local",
    "number": "number",
    "currency": "number",
    "range": "range",
    "password": "password",
    "color": "color",
    "file": "file",
}


def esc(value):
    return html.escape(str(value or ""), quote=True)


def render_field(prompt, role_label):
    """One prompt: its label, its control, and whatever the author said about it."""
    hints = prompt.hints
    declared = (hints.expected_data_type or "text").lower()
    field_id = esc(prompt.id)

    badge = (
        f'<span class="role" title="This part is for {esc(role_label)}">'
        f"For {esc(role_label)}</span>"
        if role_label
        else ""
    )

    # A response is always a string, so every control below is a way of typing
    # one - never a constraint on what may be typed (specification 3.3).
    if hints.suggested_values:
        options = "".join(
            f'<option value="{esc(v)}"{" selected" if v == prompt.response else ""}>{esc(v)}</option>'
            for v in hints.suggested_values
        )
        chosen_is_offered = prompt.response in hints.suggested_values
        # An answer outside the list is allowed and often right, so it is kept
        # as an option rather than silently dropped on the floor.
        if prompt.response and not chosen_is_offered:
            options = (
                f'<option value="{esc(prompt.response)}" selected>'
                f"{esc(prompt.response)} (not offered)</option>" + options
            )
        control = f'<select id="{field_id}" name="{field_id}">{options}</select>'

    elif declared == "multiline":
        control = (
            f'<textarea id="{field_id}" name="{field_id}" rows="3" '
            f'placeholder="{esc(hints.placeholder)}">{esc(prompt.response)}</textarea>'
        )

    elif declared == "boolean":
        checked = prompt.response.strip().lower() in {"true", "yes", "1", "on"}
        control = (
            f'<input type="checkbox" id="{field_id}" name="{field_id}" value="Yes"'
            f'{" checked" if checked else ""}>'
        )

    else:
        bounds = "".join(
            f' {name}="{esc(value)}"'
            for name, value in (("min", hints.min), ("max", hints.max), ("step", hints.step))
            if value
        )
        control = (
            f'<input type="{INPUT_TYPES.get(declared, "text")}" id="{field_id}" '
            f'name="{field_id}" value="{esc(prompt.response)}" '
            f'placeholder="{esc(hints.placeholder)}"{bounds}>'
        )

    help_text = (
        f'<p class="help">{esc(hints.help_text)}</p>' if hints.help_text else ""
    )
    return (
        f'<div class="field">{badge}'
        f'<label for="{field_id}">{esc(prompt.label)}</label>'
        f"{control}{help_text}</div>"
    )


def render_table(section, role_of):
    """A table section: rows are child sections, cells are their prompts.

    Column headers come from the prompts' own labels. There is no separate
    column declaration to read, because a table adds no new primitive - that is
    the whole point of the design (specification 4.5).
    """
    rows = section.sections
    if not rows:
        return '<p class="empty">No rows yet.</p>'

    headers = "".join(f"<th>{esc(cell.label)}</th>" for cell in rows[0].prompts)
    body = ""
    for row in rows:
        cells = "".join(
            f'<td>{render_field(cell, role_of.get(id(cell)))}</td>' for cell in row.prompts
        )
        body += f"<tr>{cells}</tr>"

    return f"<table><thead><tr>{headers}</tr></thead><tbody>{body}</tbody></table>"


def render_section(section, role_of, depth=0):
    heading = min(depth + 2, 6)
    parts = [f"<fieldset><legend><h{heading}>{esc(section.title)}</h{heading}></legend>"]
    if section.description:
        parts.append(f'<p class="description">{esc(section.description)}</p>')

    if section.kind == "table":
        parts.append(render_table(section, role_of))
    else:
        parts.extend(render_field(p, role_of.get(id(p))) for p in section.prompts)
        parts.extend(render_section(s, role_of, depth + 1) for s in section.sections)

    parts.append("</fieldset>")
    return "".join(parts)


def render_notices(document):
    """What the library says about this document, shown rather than hidden.

    A document that fails validation still renders. Refusing to show it would be
    the failure the parse/validate split exists to prevent (specification 6.3).
    """
    result = pr.validate(document)
    blocks = []

    if result.errors:
        items = "".join(
            f"<li><code>{esc(e.code)}</code> at <code>{esc(e.path)}</code>: {esc(e.message)}</li>"
            for e in result.errors
        )
        blocks.append(
            f'<div class="notice error"><strong>This document does not validate.</strong> '
            f"It is still shown, so you can see what is wrong.<ul>{items}</ul></div>"
        )

    if result.warnings:
        items = "".join(
            f"<li><code>{esc(w.code)}</code> at <code>{esc(w.path)}</code>: {esc(w.message)}</li>"
            for w in result.warnings[:12]
        )
        more = (
            f"<li>… and {len(result.warnings) - 12} more</li>" if len(result.warnings) > 12 else ""
        )
        blocks.append(
            f'<div class="notice advisory"><strong>Advisories.</strong> Every one of these '
            f"is allowed: a hint suggests, it never restricts.<ul>{items}{more}</ul></div>"
        )

    if document.signatures:
        blocks.append(
            f'<div class="notice"><strong>{len(document.signatures)} signature(s) present, '
            "not checked.</strong> This demo implements the core profile, so it preserves "
            "signatures and has no verdict to give about them.</div>"
        )

    return "".join(blocks)


PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title} — APR web demo</title>
<style>
 :root {{ color-scheme: light dark; }}
 body {{ font: 15px/1.5 system-ui, sans-serif; max-width: 860px; margin: 2rem auto;
        padding: 0 1rem; }}
 fieldset {{ border: 1px solid #8884; border-radius: 3px; margin: 0 0 1rem; padding: 1rem; }}
 legend h2, legend h3, legend h4, legend h5, legend h6 {{ margin: 0; font-size: 1rem; }}
 .field {{ margin: 0 0 .9rem; }}
 label {{ display: block; font-weight: 600; margin-bottom: .2rem; }}
 input, select, textarea {{ width: 100%; padding: .4rem; border: 1px solid #8886;
        border-radius: 3px; background: transparent; color: inherit; font: inherit; }}
 input[type=checkbox] {{ width: auto; }}
 .help {{ font-size: .85rem; opacity: .75; margin: .25rem 0 0; }}
 .role {{ font-size: .75rem; background: #8882; padding: .1rem .4rem;
        border-radius: 3px; margin-right: .4rem; }}
 .description {{ opacity: .8; margin-top: 0; }}
 .notice {{ border-left: 3px solid #8886; padding: .6rem .9rem; margin: 0 0 1rem;
        font-size: .9rem; }}
 .notice.error {{ border-left-color: #c33; }}
 .notice.advisory {{ border-left-color: #c93; }}
 table {{ width: 100%; border-collapse: collapse; }}
 th, td {{ border: 1px solid #8884; padding: .3rem; text-align: left; vertical-align: top; }}
 td .field {{ margin: 0; }} td label {{ font-weight: 400; font-size: .8rem; opacity: .7; }}
 button {{ font: inherit; padding: .5rem 1rem; border-radius: 3px; cursor: pointer; }}
</style></head>
<body>
<h1>{title}</h1>
{description}
<p class="help">{source} · APR {version}{doctype}</p>
{notices}
<form method="post" action="/submit">{sections}
<button type="submit">Submit</button>
</form>
</body></html>
"""


@app.route("/")
def index():
    document = DOCUMENT
    role_of = {id(prompt): role_api.display_name(document, role)
               for prompt, role in role_api.resolve(document)}

    return PAGE.format(
        title=esc(document.metadata.title),
        description=(
            f"<p>{esc(document.metadata.description)}</p>"
            if document.metadata.description else ""
        ),
        source=esc(SOURCE_PATH.name),
        version=esc(document.version),
        doctype=f" · {esc(document.document_type)}" if document.document_type else "",
        notices=render_notices(document),
        sections="".join(render_section(s, role_of) for s in document.sections),
    )


@app.route("/submit", methods=["POST"])
def submit():
    """Writes the answers back into the document and saves it beside the source.

    Every prompt id is a flat form field. Table cells need no special handling
    because a cell is an ordinary prompt with its own id - the old design needed
    bespoke ``id[row][col]`` parsing, and the redesign removed the need for it.
    """
    for prompt in DOCUMENT.all_prompts():
        if prompt.id in request.form:
            prompt.response = request.form[prompt.id]
        elif (DOCUMENT.metadata and prompt.hints.expected_data_type == "boolean"):
            # An unchecked box submits nothing at all.
            prompt.response = "No"

    DOCUMENT.document_type = "filledForm"
    DOCUMENT.metadata.filled_date = datetime.now(timezone.utc).isoformat(timespec="seconds")
    if not DOCUMENT.metadata.template_id:
        # A filled form records the template it answers (specification 6.1).
        DOCUMENT.metadata.template_id = SOURCE_PATH.stem

    out = SOURCE_PATH.with_suffix(".aprf")
    pr.dump(DOCUMENT, out)

    result = pr.validate(DOCUMENT)
    print(f"\n{'=' * 60}\nSubmitted {datetime.now().isoformat(timespec='seconds')}")
    print(f"Wrote {out}")
    print(f"Valid: {result.is_valid}  Advisories: {len(result.warnings)}")
    print(json.dumps(json.loads(pr.dumps(DOCUMENT)), indent=2)[:2000])

    answered = sum(1 for p in DOCUMENT.all_prompts() if p.response)
    total = sum(1 for _ in DOCUMENT.all_prompts())
    return (
        f"<!doctype html><html><body style='font:15px system-ui;max-width:700px;"
        f"margin:3rem auto'><h1>Saved</h1><p>{answered} of {total} fields answered. "
        f"Written to <code>{esc(out.name)}</code>, and printed to the terminal.</p>"
        f"<p>Validation: <strong>{'valid' if result.is_valid else 'not valid'}</strong>, "
        f"{len(result.warnings)} advisory(s) — none of which make it invalid.</p>"
        f"<p><a href='/'>Back to the form</a></p></body></html>"
    )


def main():
    parser = argparse.ArgumentParser(
        description="Render an APR document as a web form, using the Python SDK."
    )
    parser.add_argument("file", help="an .apr, .aprt or .aprf document")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--host", default="127.0.0.1",
                        help="loopback by default; this demo has no authentication")
    args = parser.parse_args()

    global DOCUMENT, SOURCE_PATH
    SOURCE_PATH = pathlib.Path(args.file)

    try:
        DOCUMENT = pr.load(SOURCE_PATH)
    except pr.AprParseError as exc:
        sys.exit(f"Error: {args.file} is not a readable APR document: {exc}")
    except FileNotFoundError:
        sys.exit(f"Error: file not found: {args.file}")

    result = pr.validate(DOCUMENT)
    print(f"Loaded {SOURCE_PATH.name}: {DOCUMENT.metadata.title}")
    print(f"  APR {DOCUMENT.version} · {sum(1 for _ in DOCUMENT.all_prompts())} prompts")
    print(f"  valid: {result.is_valid} · advisories: {len(result.warnings)}")
    for error in result.errors:
        print(f"  invalid: {error.code} at {error.path}")
    print(f"\nOpen http://{args.host}:{args.port}/")

    app.run(host=args.host, port=args.port, debug=False)


if __name__ == "__main__":
    main()

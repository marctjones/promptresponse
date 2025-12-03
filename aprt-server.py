#!/usr/bin/env python3
"""
APRT Form Server - A minimal Flask application that renders APR templates as HTML forms.

Usage:
    python aprt-server.py <template.aprt>
    python aprt-server.py --help

Dependencies:
    pip install flask
"""

import argparse
import json
import sys
from datetime import datetime
from flask import Flask, request, render_template_string

app = Flask(__name__)

# Global to hold the loaded template
TEMPLATE_DATA = None
TEMPLATE_PATH = None

HTML_TEMPLATE = '''
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{{ title }}</title>
    <style>
        * { box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
            background: #f5f5f5;
            color: #333;
        }
        h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 10px; }
        h3 { color: #7f8c8d; margin-top: 20px; }
        .description { color: #666; font-style: italic; margin-bottom: 20px; }
        .section { background: #fff; padding: 20px; margin: 15px 0; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .subsection { margin-left: 20px; padding-left: 15px; border-left: 2px solid #ecf0f1; }
        .prompt { margin: 15px 0; }
        label { display: block; font-weight: 600; margin-bottom: 5px; color: #2c3e50; }
        .help-text { font-size: 0.85em; color: #7f8c8d; margin-bottom: 5px; }
        input[type="text"], input[type="email"], input[type="tel"], input[type="url"],
        input[type="number"], input[type="date"], input[type="time"], input[type="datetime-local"],
        input[type="password"], input[type="color"], input[type="range"], input[type="file"],
        select, textarea {
            width: 100%;
            padding: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }
        input:focus, select:focus, textarea:focus {
            outline: none;
            border-color: #3498db;
            box-shadow: 0 0 5px rgba(52,152,219,0.3);
        }
        textarea { min-height: 100px; resize: vertical; }
        .checkbox-group, .radio-group { display: flex; flex-wrap: wrap; gap: 10px; }
        .checkbox-group label, .radio-group label { font-weight: normal; display: inline-flex; align-items: center; gap: 5px; }
        input[type="checkbox"], input[type="radio"] { width: auto; }
        input[type="range"] { padding: 0; }
        .range-value { text-align: center; font-size: 0.9em; color: #666; }
        button[type="submit"] {
            background: #3498db;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            cursor: pointer;
            margin-top: 20px;
        }
        button[type="submit"]:hover { background: #2980b9; }
        .metadata { background: #ecf0f1; padding: 10px; border-radius: 4px; margin-bottom: 20px; font-size: 0.9em; }
        table.data-table { width: 100%; border-collapse: collapse; margin-top: 10px; }
        table.data-table th, table.data-table td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        table.data-table th { background: #f8f9fa; font-weight: 600; }
        table.data-table input { width: 100%; border: none; padding: 5px; }
        table.data-table input:focus { outline: 2px solid #3498db; }
        .row-header { background: #f8f9fa; font-weight: 600; }
    </style>
</head>
<body>
    <h1>{{ title }}</h1>
    {% if description %}
    <p class="description">{{ description }}</p>
    {% endif %}

    {% if metadata %}
    <div class="metadata">
        {% if metadata.author %}<strong>Author:</strong> {{ metadata.author }} | {% endif %}
        {% if metadata.templateVersion %}<strong>Version:</strong> {{ metadata.templateVersion }}{% endif %}
    </div>
    {% endif %}

    <form method="POST" action="/submit" enctype="multipart/form-data">
        {% for section in sections %}
            {{ render_section(section, 0) }}
        {% endfor %}
        <button type="submit">Submit Form</button>
    </form>

    <script>
        // Update range value displays
        document.querySelectorAll('input[type="range"]').forEach(function(range) {
            var display = document.getElementById(range.id + '_value');
            if (display) {
                range.addEventListener('input', function() {
                    display.textContent = this.value;
                });
            }
        });
    </script>
</body>
</html>
'''

def get_input_type(expected_type):
    """Map APR expectedDataType to HTML input type."""
    type_map = {
        'text': 'text',
        'email': 'email',
        'phone': 'tel',
        'url': 'url',
        'number': 'number',
        'currency': 'number',
        'date': 'date',
        'time': 'time',
        'datetime': 'datetime-local',
        'password': 'password',
        'color': 'color',
        'range': 'range',
        'file': 'file',
        'ssn': 'text',
        'ein': 'text',
        'zipcode': 'text',
        'creditcard': 'text',
    }
    return type_map.get(expected_type, 'text')

def render_prompt(prompt):
    """Render a single prompt as HTML form field."""
    prompt_id = prompt.get('id', '')
    label = prompt.get('label', 'Unlabeled Field')
    hints = prompt.get('hints', {})

    placeholder = hints.get('placeholder', '')
    expected_type = hints.get('expectedDataType', 'text')
    help_text = hints.get('helpText', '')
    suggested_values = hints.get('suggestedValues', [])
    validation_pattern = hints.get('validationPattern', '')
    table_def = hints.get('tableDefinition')

    html = f'<div class="prompt">\n'
    html += f'  <label for="{prompt_id}">{label}</label>\n'

    if help_text:
        html += f'  <div class="help-text">{help_text}</div>\n'

    # Table fields
    if expected_type == 'table' and table_def:
        html += render_table(prompt_id, table_def)
    # Multi-choice (checkboxes)
    elif expected_type == 'multichoice' and suggested_values:
        html += '  <div class="checkbox-group">\n'
        for i, val in enumerate(suggested_values):
            html += f'    <label><input type="checkbox" name="{prompt_id}" value="{val}"> {val}</label>\n'
        html += '  </div>\n'
    # Boolean (yes/no)
    elif expected_type == 'boolean':
        options = suggested_values if suggested_values else ['Yes', 'No']
        html += '  <div class="radio-group">\n'
        for val in options:
            html += f'    <label><input type="radio" name="{prompt_id}" value="{val}"> {val}</label>\n'
        html += '  </div>\n'
    # Dropdown (suggested values)
    elif suggested_values and expected_type not in ('multichoice', 'boolean'):
        html += f'  <select name="{prompt_id}" id="{prompt_id}">\n'
        html += f'    <option value="">-- Select --</option>\n'
        for val in suggested_values:
            html += f'    <option value="{val}">{val}</option>\n'
        html += f'  </select>\n'
    # Multiline text
    elif expected_type == 'multiline':
        html += f'  <textarea name="{prompt_id}" id="{prompt_id}" placeholder="{placeholder}"></textarea>\n'
    # Range slider
    elif expected_type == 'range':
        min_val = hints.get('min', '0')
        max_val = hints.get('max', '100')
        step = hints.get('step', '1')
        html += f'  <input type="range" name="{prompt_id}" id="{prompt_id}" min="{min_val}" max="{max_val}" step="{step}" value="{min_val}">\n'
        html += f'  <div class="range-value" id="{prompt_id}_value">{min_val}</div>\n'
    # Signature (styled text)
    elif expected_type == 'signature':
        html += f'  <input type="text" name="{prompt_id}" id="{prompt_id}" placeholder="{placeholder}" style="font-family: cursive; font-size: 18px;">\n'
    # Currency (number with step)
    elif expected_type == 'currency':
        html += f'  <input type="number" name="{prompt_id}" id="{prompt_id}" placeholder="{placeholder}" step="0.01">\n'
    # Standard inputs
    else:
        input_type = get_input_type(expected_type)
        pattern_attr = f' pattern="{validation_pattern}"' if validation_pattern else ''
        html += f'  <input type="{input_type}" name="{prompt_id}" id="{prompt_id}" placeholder="{placeholder}"{pattern_attr}>\n'

    html += '</div>\n'
    return html

def render_table(prompt_id, table_def):
    """Render a table field as HTML."""
    columns = table_def.get('columns', [])
    fixed_rows = table_def.get('fixedRows', [])
    dynamic_rows = table_def.get('dynamicRows')

    html = '<table class="data-table">\n'

    # Header row
    html += '  <thead><tr>\n'
    if fixed_rows:
        html += '    <th></th>\n'  # Row label column
    for col in columns:
        html += f'    <th>{col.get("label", "")}</th>\n'
    html += '  </tr></thead>\n'

    html += '  <tbody>\n'

    if fixed_rows:
        # Fixed table
        for row in fixed_rows:
            row_id = row.get('id', '')
            row_label = row.get('label', '')
            html += '  <tr>\n'
            html += f'    <td class="row-header">{row_label}</td>\n'
            for col in columns:
                col_id = col.get('id', '')
                col_type = col.get('type', 'text')
                placeholder = col.get('placeholder', '')
                field_name = f'{prompt_id}[{row_id}][{col_id}]'
                input_type = 'number' if col_type in ('number', 'currency') else 'text'
                step = ' step="0.01"' if col_type == 'currency' else ''
                html += f'    <td><input type="{input_type}" name="{field_name}" placeholder="{placeholder}"{step}></td>\n'
            html += '  </tr>\n'
    elif dynamic_rows:
        # Dynamic table - render minRows or 3 rows
        min_rows = dynamic_rows.get('minRows', 1)
        num_rows = max(min_rows, 3)
        for i in range(num_rows):
            html += '  <tr>\n'
            for col in columns:
                col_id = col.get('id', '')
                col_type = col.get('type', 'text')
                placeholder = col.get('placeholder', '')
                field_name = f'{prompt_id}[{i}][{col_id}]'
                input_type = 'number' if col_type in ('number', 'currency') else 'text'
                step = ' step="0.01"' if col_type == 'currency' else ''
                html += f'    <td><input type="{input_type}" name="{field_name}" placeholder="{placeholder}"{step}></td>\n'
            html += '  </tr>\n'

    html += '  </tbody>\n</table>\n'
    return html

def render_section(section, depth=0):
    """Render a section and its contents as HTML."""
    section_id = section.get('id', '')
    title = section.get('title', 'Untitled Section')
    description = section.get('description', '')
    subsections = section.get('sections', [])
    prompts = section.get('prompts', [])

    # Choose heading level based on depth
    h_level = min(depth + 2, 6)

    html = f'<div class="section" id="{section_id}">\n'
    html += f'  <h{h_level}>{title}</h{h_level}>\n'

    if description:
        html += f'  <p class="description">{description}</p>\n'

    # Render prompts in this section
    for prompt in prompts:
        html += render_prompt(prompt)

    # Render nested subsections
    for subsection in subsections:
        html += f'<div class="subsection">\n{render_section(subsection, depth + 1)}</div>\n'

    html += '</div>\n'
    return html

@app.route('/')
def index():
    """Render the form."""
    if TEMPLATE_DATA is None:
        return "No template loaded", 500

    metadata = TEMPLATE_DATA.get('metadata', {})
    title = metadata.get('title', 'APR Form')
    description = metadata.get('description', '')
    sections = TEMPLATE_DATA.get('sections', [])

    # Build sections HTML
    sections_html = ''
    for section in sections:
        sections_html += render_section(section)

    # Simple template rendering without Jinja macros
    html = HTML_TEMPLATE
    html = html.replace('{{ title }}', title)
    html = html.replace('{% if description %}', '')
    html = html.replace('{% endif %}', '')
    html = html.replace('{{ description }}', description if description else '')

    # Build metadata section
    metadata_html = ''
    if metadata.get('author'):
        metadata_html += f"<strong>Author:</strong> {metadata['author']}"
    if metadata.get('templateVersion'):
        if metadata_html:
            metadata_html += ' | '
        metadata_html += f"<strong>Version:</strong> {metadata['templateVersion']}"

    # Replace the sections loop with actual content
    html = html.replace('''{% if metadata %}
    <div class="metadata">
        {% if metadata.author %}<strong>Author:</strong> {{ metadata.author }} | {% endif %}
        {% if metadata.templateVersion %}<strong>Version:</strong> {{ metadata.templateVersion }}{% endif %}
    </div>
    {% endif %}''', f'<div class="metadata">{metadata_html}</div>' if metadata_html else '')

    html = html.replace('''{% for section in sections %}
            {{ render_section(section, 0) }}
        {% endfor %}''', sections_html)

    return html

@app.route('/submit', methods=['POST'])
def submit():
    """Handle form submission."""
    print("\n" + "="*60)
    print(f"FORM SUBMISSION - {datetime.now().isoformat()}")
    print(f"Template: {TEMPLATE_PATH}")
    print("="*60)

    # Collect form data
    form_data = {}
    for key in request.form:
        values = request.form.getlist(key)
        if len(values) == 1:
            form_data[key] = values[0]
        else:
            form_data[key] = values

    # Parse nested table data into structured format
    parsed_data = {}
    for key, value in form_data.items():
        if '[' in key:
            # Parse table field: prompt_id[row][col]
            parts = key.replace(']', '').split('[')
            prompt_id = parts[0]
            if prompt_id not in parsed_data:
                parsed_data[prompt_id] = {}

            if len(parts) == 3:
                row_id, col_id = parts[1], parts[2]
                if row_id not in parsed_data[prompt_id]:
                    parsed_data[prompt_id][row_id] = {}
                parsed_data[prompt_id][row_id][col_id] = value
        else:
            parsed_data[key] = value

    # Pretty print the submission
    print("\nSubmitted Data:")
    print("-"*40)
    print(json.dumps(parsed_data, indent=2))
    print("="*60 + "\n")
    sys.stdout.flush()

    # Return confirmation page
    return f'''
    <!DOCTYPE html>
    <html>
    <head>
        <title>Form Submitted</title>
        <style>
            body {{ font-family: sans-serif; max-width: 800px; margin: 40px auto; padding: 20px; }}
            .success {{ background: #d4edda; border: 1px solid #c3e6cb; padding: 20px; border-radius: 8px; }}
            pre {{ background: #f8f9fa; padding: 15px; border-radius: 4px; overflow-x: auto; }}
            a {{ color: #3498db; }}
        </style>
    </head>
    <body>
        <div class="success">
            <h1>✓ Form Submitted Successfully</h1>
            <p>Your form data has been printed to the terminal.</p>
        </div>
        <h2>Submitted Data:</h2>
        <pre>{json.dumps(parsed_data, indent=2)}</pre>
        <p><a href="/">← Fill out another form</a></p>
    </body>
    </html>
    '''

def load_template(path):
    """Load and validate an APRT file."""
    try:
        with open(path, 'r', encoding='utf-8') as f:
            data = json.load(f)

        # Basic validation
        if 'version' not in data:
            print(f"Warning: No version field in {path}", file=sys.stderr)
        if 'sections' not in data:
            print(f"Error: No sections found in {path}", file=sys.stderr)
            sys.exit(1)

        return data
    except json.JSONDecodeError as e:
        print(f"Error: Invalid JSON in {path}: {e}", file=sys.stderr)
        sys.exit(1)
    except FileNotFoundError:
        print(f"Error: File not found: {path}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error loading {path}: {e}", file=sys.stderr)
        sys.exit(1)

def main():
    parser = argparse.ArgumentParser(
        description='APRT Form Server - Render APR templates as HTML forms',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog='''
Examples:
  python aprt-server.py form.aprt
  python aprt-server.py examples/field-types-showcase.aprt
  python aprt-server.py --port 3000 my-form.aprt

The server will start on http://127.0.0.1:8080 by default.
Form submissions are printed to stdout as JSON.
        '''
    )
    parser.add_argument('template', help='Path to the .aprt template file')
    parser.add_argument('--port', '-p', type=int, default=8080,
                        help='Port to run the server on (default: 8080)')
    parser.add_argument('--host', default='127.0.0.1',
                        help='Host to bind to (default: 127.0.0.1)')
    parser.add_argument('--debug', '-d', action='store_true',
                        help='Run in debug mode')

    args = parser.parse_args()

    global TEMPLATE_DATA, TEMPLATE_PATH
    TEMPLATE_PATH = args.template
    TEMPLATE_DATA = load_template(args.template)

    title = TEMPLATE_DATA.get('metadata', {}).get('title', 'APR Form')
    print(f"Loading template: {args.template}")
    print(f"Form title: {title}")
    print(f"Starting server at http://{args.host}:{args.port}")
    print("Press Ctrl+C to stop\n")

    app.run(host=args.host, port=args.port, debug=args.debug)

if __name__ == '__main__':
    main()

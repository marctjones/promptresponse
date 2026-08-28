#!/usr/bin/env bash
#
# Run the APRT Form Server with the field-types-showcase example
# Works on Ubuntu and macOS
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_TEMPLATE="$SCRIPT_DIR/examples/field-types-showcase.aprt"
PORT=8080

# Parse arguments
TEMPLATE="${1:-$DEFAULT_TEMPLATE}"

if [[ "$1" == "-h" || "$1" == "--help" ]]; then
    echo "Usage: $0 [template.aprt] [--port PORT]"
    echo ""
    echo "Installs Flask (if needed) and launches the APRT Form Server."
    echo ""
    echo "Arguments:"
    echo "  template.aprt    Path to an .aprt file (default: examples/field-types-showcase.aprt)"
    echo "  --port PORT      Port to run on (default: 8080)"
    echo ""
    echo "Examples:"
    echo "  $0                                    # Run with default example"
    echo "  $0 my-form.aprt                       # Run with custom template"
    echo "  $0 my-form.aprt --port 3000           # Run on port 3000"
    exit 0
fi

# Check for --port argument
for i in "${@}"; do
    if [[ "$prev" == "--port" ]]; then
        PORT="$i"
    fi
    prev="$i"
done

# Detect python command
if command -v python3 &> /dev/null; then
    PYTHON=python3
elif command -v python &> /dev/null; then
    PYTHON=python
else
    echo "Error: Python not found. Please install Python 3."
    exit 1
fi

echo "Using Python: $($PYTHON --version)"

# Install Flask if not present
if ! $PYTHON -c "import flask" 2>/dev/null; then
    echo "Installing Flask..."
    $PYTHON -m pip install --user flask
fi

# Verify template exists
if [[ ! -f "$TEMPLATE" ]]; then
    echo "Error: Template not found: $TEMPLATE"
    exit 1
fi

echo ""
echo "Starting APRT Form Server..."
echo "Template: $TEMPLATE"
echo "URL: http://localhost:$PORT"
echo ""

$PYTHON "$SCRIPT_DIR/web-demo.py" --port "$PORT" "$TEMPLATE"

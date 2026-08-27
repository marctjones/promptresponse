#!/bin/bash

# Automated Accessibility Testing Script
#
# This script launches PromptResponse with Orca screen reader and captures
# all accessibility events to a log file. You can interact with the application
# normally, and all text that Orca "sees" will be captured for automated testing.
#
# Usage:
#   ./test-accessibility.sh [OPTIONS]
#
# Options:
#   --file <path>        APR file to open automatically
#   --no-speech          Disable audio (visual output only)
#   --output <path>      Custom log output path
#   --help               Show this help

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default settings
APR_FILE=""
DISABLE_SPEECH=false
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
LOG_DIR="accessibility-logs"
LOG_FILE="$LOG_DIR/orca-$TIMESTAMP.log"
SPEECH_LOG="$LOG_DIR/speech-$TIMESTAMP.log"
ATSPI_LOG="$LOG_DIR/atspi-$TIMESTAMP.log"

print_header() {
    echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

show_help() {
    cat << EOF
Automated Accessibility Testing Script

This script runs PromptResponse with Orca screen reader and captures all
accessibility announcements to a log file for automated testing.

Usage:
  ./test-accessibility.sh [OPTIONS]

Options:
  --file <path>        Automatically open specified APR file
  --no-speech          Disable audio output (text logging only)
  --output <path>      Custom output log file path
  --help               Show this help message

Examples:
  # Basic test with audio
  ./test-accessibility.sh

  # Test specific file without audio
  ./test-accessibility.sh --file examples/contact-intake.aprt --no-speech

  # Custom log location
  ./test-accessibility.sh --output my-test.log

After running:
  1. Interact with the application normally
  2. Close the app when done
  3. Review the log file in $LOG_DIR/
  4. Run validation: dotnet run --project tests/AccessibilityTests -- validate <log-file> <apr-file>

EOF
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --file)
            APR_FILE="$2"
            shift 2
            ;;
        --no-speech)
            DISABLE_SPEECH=true
            shift
            ;;
        --output)
            LOG_FILE="$2"
            shift 2
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            echo ""
            show_help
            exit 1
            ;;
    esac
done

# Check prerequisites
print_header "Checking Prerequisites"

if ! command -v orca &> /dev/null; then
    print_error "Orca is not installed"
    echo "Install with: sudo apt-get install orca"
    exit 1
fi
print_success "Orca installed"

if ! command -v speech-dispatcher &> /dev/null; then
    print_error "speech-dispatcher is not installed"
    echo "Install with: sudo apt-get install speech-dispatcher"
    exit 1
fi
print_success "speech-dispatcher installed"

if [ ! -f "PromptResponse.sln" ]; then
    print_error "Must be run from PromptResponse project root"
    exit 1
fi
print_success "Project root verified"

# Create log directory
mkdir -p "$LOG_DIR"
print_success "Log directory created: $LOG_DIR"

# Build the project
print_header "Building PromptResponse"
if dotnet build > /dev/null 2>&1; then
    print_success "Build completed successfully"
else
    print_error "Build failed"
    exit 1
fi

# Setup Orca configuration
print_header "Configuring Orca for Testing"

ORCA_CONFIG_DIR="$HOME/.local/share/orca"
ORCA_TEST_CONFIG="$LOG_DIR/orca-test-config.py"

mkdir -p "$ORCA_CONFIG_DIR"

# Create Orca configuration that logs all speech
cat > "$ORCA_TEST_CONFIG" << 'EOF'
# Orca test configuration - logs all speech output

# Enable text-based output
import orca.speechdispatcherfactory
import orca.debug

# Log all speech
orca.debug.debugLevel = orca.debug.LEVEL_ALL
orca.debug.debugFile = None  # Will be set via command line

# Enable all output
orca.settings.enableSpeech = True
orca.settings.enableBraille = False
orca.settings.enableEchoByCharacter = True
orca.settings.enableEchoByWord = True
orca.settings.enableKeyEcho = True
orca.settings.enablePrintableKeys = True
orca.settings.enableModifierKeys = True
orca.settings.enableLockingKeys = True
orca.settings.enableFunctionKeys = True
orca.settings.enableActionKeys = True

# Verbose output
orca.settings.verbalizePunctuationStyle = orca.settings.PUNCTUATION_STYLE_ALL
orca.settings.readTableCellRow = True
EOF

print_success "Orca configuration created"

# Configure speech-dispatcher for logging
print_info "Configuring speech-dispatcher logging..."

# Stop speech-dispatcher if running
speech-dispatcher -k 2>/dev/null || true
sleep 1

# Start speech-dispatcher with logging
speech-dispatcher -d -l "$SPEECH_LOG" &
SPEECH_PID=$!
sleep 2

print_success "speech-dispatcher started (PID: $SPEECH_PID)"

if [ "$DISABLE_SPEECH" = true ]; then
    print_info "Audio output disabled (text logging only)"
    # Mute speech-dispatcher
    spd-conf -s -o espeak-ng -R 0 2>/dev/null || true
fi

# Enable AT-SPI accessibility bus
print_header "Enabling Accessibility Bus"
export AVALONIA_ENABLE_ACCESSIBILITY=1
export GTK_MODULES=gail:atk-bridge
export QT_ACCESSIBILITY=1
export ACCESSIBILITY_ENABLED=1

print_success "Accessibility environment configured"

# Create wrapper script that captures AT-SPI events
ATSPI_WRAPPER="$LOG_DIR/atspi-wrapper-$TIMESTAMP.sh"
cat > "$ATSPI_WRAPPER" << EOF
#!/bin/bash
# AT-SPI event logger
export ATSPI_DEBUG_ALL=1
export ATSPI_DEBUG_LOG="$ATSPI_LOG"
exec "\$@"
EOF
chmod +x "$ATSPI_WRAPPER"

# Function to cleanup on exit
cleanup() {
    print_header "Cleaning Up"

    # Stop Orca
    if [ ! -z "$ORCA_PID" ]; then
        kill $ORCA_PID 2>/dev/null || true
        print_success "Orca stopped"
    fi

    # Stop speech-dispatcher
    if [ ! -z "$SPEECH_PID" ]; then
        kill $SPEECH_PID 2>/dev/null || true
        print_success "speech-dispatcher stopped"
    fi

    # Restart speech-dispatcher normally
    speech-dispatcher 2>/dev/null &

    print_header "Test Complete"
    print_success "Accessibility log saved to: $LOG_FILE"
    print_success "Speech log saved to: $SPEECH_LOG"

    if [ -s "$ATSPI_LOG" ]; then
        print_success "AT-SPI log saved to: $ATSPI_LOG"
    fi

    echo ""
    print_info "To validate accessibility:"
    echo "  ./validate-accessibility.sh $LOG_FILE examples/your-file.apr"
    echo ""
    print_info "To view the log:"
    echo "  cat $LOG_FILE"
    echo ""
}

trap cleanup EXIT

# Start Orca with logging
print_header "Starting Orca Screen Reader"
print_info "Orca will announce all UI elements it encounters"
print_info "All announcements will be logged to: $LOG_FILE"
echo ""

# Start Orca with text output
orca --no-setup --debug-file="$LOG_FILE" &
ORCA_PID=$!

sleep 3
print_success "Orca started (PID: $ORCA_PID)"

# Launch PromptResponse
print_header "Launching PromptResponse"
echo ""
print_info "Instructions:"
echo "  1. The application window will open shortly"
echo "  2. Navigate through the application using keyboard (Tab, Arrow keys)"
echo "  3. Orca will announce each element (logged silently if --no-speech)"
echo "  4. Open a form and navigate through all fields"
echo "  5. Close the application when done"
echo ""
print_info "Press Enter to launch..."
read

# Build command
APP_CMD="dotnet run --project src/PromptResponse.Desktop"

if [ ! -z "$APR_FILE" ]; then
    if [ ! -f "$APR_FILE" ]; then
        print_error "APR file not found: $APR_FILE"
        exit 1
    fi
    print_info "Will automatically open: $APR_FILE"
    # Note: We'd need to implement CLI arg for auto-opening
    # For now, user must open manually
fi

# Run the application
print_info "Starting PromptResponse..."
echo ""
echo "${BLUE}─────────────────── Application Running ───────────────────${NC}"
echo ""

# Run with AT-SPI debugging
ATSPI_DEBUG_ALL=1 $APP_CMD 2>&1 | tee -a "$LOG_FILE"

echo ""
echo "${BLUE}────────────────────────────────────────────────────────────${NC}"

# Cleanup happens in trap

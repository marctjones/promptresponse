#!/bin/bash

# PromptResponse Launcher Script
#
# This script provides an easy way to run the PromptResponse application.
# By default, it opens the Field Types Demo to showcase all control types. Use options to override.

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
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

# Function to build the project
build_project() {
    print_header "Building PromptResponse"
    if dotnet build > /dev/null 2>&1; then
        print_success "Build completed successfully"
        return 0
    else
        print_error "Build failed"
        echo "Run 'dotnet build' to see detailed error messages"
        return 1
    fi
}

# Function to run tests
run_tests() {
    print_header "Running Tests"
    dotnet test --verbosity quiet
}

# Function to launch GUI
launch_gui() {
    print_header "Launching PromptResponse Desktop Application"

    if [ -n "$2" ]; then
        print_info "Opening file: $2"
    fi

    print_info "Starting GUI application with debug logging enabled..."
    print_info "All debug output will be shown below:"
    echo ""
    echo "${BLUE}─────────────────── Application Debug Log ───────────────────${NC}"
    echo ""

    # Run with full output visible
    # Arguments: $1 = mode flag (--open or --edit), $2 = file path
    if [ -n "$1" ] && [ -n "$2" ]; then
        dotnet run --project src/PromptResponse.Desktop -- "$1" "$2"
    else
        dotnet run --project src/PromptResponse.Desktop
    fi

    local exit_code=$?
    echo ""
    echo "${BLUE}──────────────────────────────────────────────────────────────${NC}"

    if [ $exit_code -eq 0 ]; then
        print_success "Application exited normally"
    else
        print_error "Application exited with code: $exit_code"
    fi

    return $exit_code
}

# Function to open a file for filling
open_file() {
    local file="$1"

    if [ ! -f "$file" ]; then
        print_error "File not found: $file"
        return 1
    fi

    launch_gui "--open" "$file"
}

# Function to open a file for editing
edit_file() {
    local file="$1"

    if [ ! -f "$file" ]; then
        print_error "File not found: $file"
        return 1
    fi

    launch_gui "--edit" "$file"
}

# Function to show CLI help
show_cli_help() {
    print_header "PromptResponse CLI Help"
    dotnet run --project src/PromptResponse.Cli -- help
}

# Function to validate example files
demo_validate() {
    print_header "Demo: Validating APR Files"

    if [ ! -d "examples" ]; then
        print_error "Examples directory not found"
        return 1
    fi

    for file in examples/*.apr; do
        if [ -f "$file" ]; then
            echo ""
            print_info "Validating: $(basename "$file")"
            dotnet run --project src/PromptResponse.Cli -- validate "$file"
        fi
    done
}

# Function to show info about example files
demo_info() {
    print_header "Demo: APR File Information"

    if [ ! -d "examples" ]; then
        print_error "Examples directory not found"
        return 1
    fi

    for file in examples/*.apr; do
        if [ -f "$file" ]; then
            echo ""
            dotnet run --project src/PromptResponse.Cli -- info "$file"
            echo ""
            read -p "Press Enter to continue to next file..."
        fi
    done
}

# Function to create a new template demo
demo_new_template() {
    print_header "Demo: Creating New Template"

    local demo_file="demo-template.apr"

    if [ -f "$demo_file" ]; then
        print_info "Removing existing demo file..."
        rm "$demo_file"
    fi

    print_info "Creating new template interactively..."
    echo ""
    dotnet run --project src/PromptResponse.Cli -- new "$demo_file"

    echo ""
    if [ -f "$demo_file" ]; then
        print_success "Template created: $demo_file"
        echo ""
        print_info "Validating the new template..."
        dotnet run --project src/PromptResponse.Cli -- validate "$demo_file"
        echo ""
        print_info "Showing template info..."
        dotnet run --project src/PromptResponse.Cli -- info "$demo_file"
    fi
}

# Function to run all CLI demos
demo_all() {
    print_header "PromptResponse CLI Demo Suite"

    print_info "This will demonstrate all CLI features"
    echo ""
    read -p "Press Enter to start..."

    # 1. Show help
    echo ""
    show_cli_help
    echo ""
    read -p "Press Enter to continue..."

    # 2. Validate examples
    echo ""
    demo_validate
    echo ""
    read -p "Press Enter to continue..."

    # 3. Show info
    echo ""
    demo_info

    # 4. Create new template
    echo ""
    print_info "Would you like to create a demo template? (y/n)"
    read -r response
    if [[ "$response" =~ ^[Yy]$ ]]; then
        demo_new_template
    fi

    echo ""
    print_header "Demo Complete"
    print_success "All CLI demos completed successfully!"
}

# Show usage information
show_usage() {
    cat << EOF
${BLUE}PromptResponse Launcher${NC}

${GREEN}Usage:${NC}
  ./run.sh [OPTION] [FILE]

${GREEN}Options:${NC}
  ${YELLOW}(none)${NC}           Open Field Types Demo to showcase all controls (default)
  ${YELLOW}--no-file${NC}        Launch GUI without opening any file
  ${YELLOW}--gui, -g${NC}        Launch GUI without opening any file (same as --no-file)
  ${YELLOW}--open <file>${NC}    Open an APR file for filling out
  ${YELLOW}--edit <file>${NC}    Open an APR template for editing
  ${YELLOW}<file>${NC}           Open an APR file for filling out (same as --open)
  ${YELLOW}--help, -h${NC}       Show CLI help information
  ${YELLOW}--demo${NC}            Run interactive CLI demo suite
  ${YELLOW}--validate${NC}        Validate all example APR files
  ${YELLOW}--info${NC}            Show information about example files
  ${YELLOW}--new${NC}             Create a new template (interactive)
  ${YELLOW}--test${NC}            Run all tests
  ${YELLOW}--build${NC}           Build the project only
  ${YELLOW}--version${NC}         Show version information
  ${YELLOW}--icon${NC}            View the application icon in browser
  ${YELLOW}--usage${NC}           Show this usage information

${GREEN}Examples:${NC}
  ./run.sh                                      # Open Field Types Demo (default - shows all controls)
  ./run.sh --no-file                            # Launch GUI without opening any file
  ./run.sh --open examples/simple-contact-form.aprt   # Open contact form for filling
  ./run.sh --edit examples/sf-86-full-template.aprt   # Edit SF-86 template
  ./run.sh examples/myform.aprf                 # Open filled form
  ./run.sh --demo                               # Run full CLI demo
  ./run.sh --validate                           # Validate example files
  ./run.sh --help                               # Show CLI help

${GREEN}Project Information:${NC}
  PromptResponse - A cross-platform form creation and filling application
  Technology: .NET 8.0, C# 12, AvaloniaUI 11
  License: GPL-3.0

EOF
}

# Show version
show_version() {
    print_header "PromptResponse Version Information"
    dotnet run --project src/PromptResponse.Cli -- version
    echo ""
    dotnet --version
}

# Main script logic
main() {
    # Check if we're in the right directory
    if [ ! -f "PromptResponse.sln" ]; then
        print_error "Error: Must be run from the PromptResponse project root directory"
        exit 1
    fi

    # Default file to open when no arguments provided
    local default_file="examples/field-types-demo.aprt"

    # If no arguments, build and open default file in form mode
    if [ $# -eq 0 ]; then
        build_project || exit 1
        echo ""
        if [ -f "$default_file" ]; then
            open_file "$default_file"
        else
            print_error "Default file not found: $default_file"
            launch_gui
        fi
        exit 0
    fi

    # Parse command line arguments
    case "$1" in
        --gui|-g)
            build_project || exit 1
            echo ""
            launch_gui
            ;;
        --no-file)
            build_project || exit 1
            echo ""
            launch_gui
            ;;
        --open)
            if [ -z "$2" ]; then
                print_error "Error: --open requires a file path"
                echo ""
                show_usage
                exit 1
            fi
            build_project || exit 1
            echo ""
            open_file "$2"
            ;;
        --edit)
            if [ -z "$2" ]; then
                print_error "Error: --edit requires a file path"
                echo ""
                show_usage
                exit 1
            fi
            build_project || exit 1
            echo ""
            edit_file "$2"
            ;;
        --help|-h)
            build_project || exit 1
            echo ""
            show_cli_help
            ;;
        --demo)
            build_project || exit 1
            echo ""
            demo_all
            ;;
        --validate)
            build_project || exit 1
            echo ""
            demo_validate
            ;;
        --info)
            build_project || exit 1
            echo ""
            demo_info
            ;;
        --new)
            build_project || exit 1
            echo ""
            demo_new_template
            ;;
        --test)
            build_project || exit 1
            echo ""
            run_tests
            ;;
        --build)
            dotnet build
            ;;
        --version|-v)
            build_project || exit 1
            echo ""
            show_version
            ;;
        --icon)
            print_header "Application Icon Preview"
            if command -v xdg-open &> /dev/null; then
                xdg-open src/PromptResponse.Desktop/Assets/icon-preview.html
            elif command -v open &> /dev/null; then
                open src/PromptResponse.Desktop/Assets/icon-preview.html
            else
                print_info "Open this file in your browser:"
                echo "  file://$(pwd)/src/PromptResponse.Desktop/Assets/icon-preview.html"
            fi
            ;;
        --usage)
            show_usage
            ;;
        -*)
            print_error "Unknown option: $1"
            echo ""
            show_usage
            exit 1
            ;;
        *)
            # If it's a file path (not starting with --), treat as --open
            if [ -f "$1" ]; then
                build_project || exit 1
                echo ""
                open_file "$1"
            else
                print_error "File not found: $1"
                echo ""
                show_usage
                exit 1
            fi
            ;;
    esac
}

# Run main function
main "$@"

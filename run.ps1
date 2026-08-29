#!/usr/bin/env pwsh

# PromptResponse Launcher Script (PowerShell)
#
# This script provides an easy way to run the PromptResponse application.
# By default, it opens the Field Types Demo to showcase all controls. Use options for CLI demos.

param(
    [Parameter(Position=0)]
    [ValidateSet('gui', 'help', 'demo', 'validate', 'info', 'new', 'test', 'build', 'version', 'usage')]
    [string]$Command = 'gui'
)

$ErrorActionPreference = "Stop"

# Function to print colored output
function Write-Header {
    param([string]$Message)
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Blue
    Write-Host "  $Message" -ForegroundColor Blue
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error-Message {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Yellow
}

# Function to build the project
function Build-Project {
    Write-Header "Building PromptResponse"

    $output = dotnet build 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Build completed successfully"
        return $true
    }
    else {
        Write-Error-Message "Build failed"
        Write-Host "Run 'dotnet build' to see detailed error messages"
        return $false
    }
}

# Function to run tests
function Invoke-Tests {
    Write-Header "Running Tests"
    dotnet test --verbosity quiet
}

# Function to launch GUI
function Start-GUI {
    Write-Header "Launching PromptResponse Desktop Application"

    $defaultFile = "examples/field-types-demo.aprt"

    if (Test-Path $defaultFile) {
        Write-Info "Opening Field Types Demo to showcase all controls..."
        Write-Host ""
        dotnet run --project src/PromptResponse.Desktop $defaultFile
    } else {
        Write-Info "Starting GUI application..."
        Write-Host ""
        dotnet run --project src/PromptResponse.Desktop
    }
}

# Function to show CLI help
function Show-CLIHelp {
    Write-Header "PromptResponse CLI Help"
    dotnet run --project src/PromptResponse.Cli -- help
}

# Function to validate example files
function Invoke-ValidateDemo {
    Write-Header "Demo: Validating APR Files"

    if (-not (Test-Path "examples")) {
        Write-Error-Message "Examples directory not found"
        return
    }

    Get-ChildItem -Path "examples" -Filter "*.apr" | ForEach-Object {
        Write-Host ""
        Write-Info "Validating: $($_.Name)"
        dotnet run --project src/PromptResponse.Cli -- validate $_.FullName
    }
}

# Function to show info about example files
function Invoke-InfoDemo {
    Write-Header "Demo: APR File Information"

    if (-not (Test-Path "examples")) {
        Write-Error-Message "Examples directory not found"
        return
    }

    Get-ChildItem -Path "examples" -Filter "*.apr" | ForEach-Object {
        Write-Host ""
        dotnet run --project src/PromptResponse.Cli -- info $_.FullName
        Write-Host ""
        Read-Host "Press Enter to continue to next file"
    }
}

# Function to create a new template demo
function Invoke-NewTemplateDemo {
    Write-Header "Demo: Creating New Template"

    $demoFile = "demo-template.apr"

    if (Test-Path $demoFile) {
        Write-Info "Removing existing demo file..."
        Remove-Item $demoFile
    }

    Write-Info "Creating new template interactively..."
    Write-Host ""
    dotnet run --project src/PromptResponse.Cli -- new $demoFile

    Write-Host ""
    if (Test-Path $demoFile) {
        Write-Success "Template created: $demoFile"
        Write-Host ""
        Write-Info "Validating the new template..."
        dotnet run --project src/PromptResponse.Cli -- validate $demoFile
        Write-Host ""
        Write-Info "Showing template info..."
        dotnet run --project src/PromptResponse.Cli -- info $demoFile
    }
}

# Function to run all CLI demos
function Invoke-AllDemos {
    Write-Header "PromptResponse CLI Demo Suite"

    Write-Info "This will demonstrate all CLI features"
    Write-Host ""
    Read-Host "Press Enter to start"

    # 1. Show help
    Write-Host ""
    Show-CLIHelp
    Write-Host ""
    Read-Host "Press Enter to continue"

    # 2. Validate examples
    Write-Host ""
    Invoke-ValidateDemo
    Write-Host ""
    Read-Host "Press Enter to continue"

    # 3. Show info
    Write-Host ""
    Invoke-InfoDemo

    # 4. Create new template
    Write-Host ""
    $response = Read-Host "Would you like to create a demo template? (y/n)"
    if ($response -match '^[Yy]$') {
        Invoke-NewTemplateDemo
    }

    Write-Host ""
    Write-Header "Demo Complete"
    Write-Success "All CLI demos completed successfully!"
}

# Show usage information
function Show-Usage {
    Write-Host "PromptResponse Launcher" -ForegroundColor Blue
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Green
    Write-Host "  .\run.ps1 [COMMAND]"
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor Green
    Write-Host "  (none)      " -ForegroundColor Yellow -NoNewline
    Write-Host "     Open Field Types Demo to showcase all controls (default)"
    Write-Host "  gui         " -ForegroundColor Yellow -NoNewline
    Write-Host "     Open Field Types Demo to showcase all controls"
    Write-Host "  help        " -ForegroundColor Yellow -NoNewline
    Write-Host "     Show CLI help information"
    Write-Host "  demo        " -ForegroundColor Yellow -NoNewline
    Write-Host "     Run interactive CLI demo suite"
    Write-Host "  validate    " -ForegroundColor Yellow -NoNewline
    Write-Host "     Validate all example APR files"
    Write-Host "  info        " -ForegroundColor Yellow -NoNewline
    Write-Host "     Show information about example files"
    Write-Host "  new         " -ForegroundColor Yellow -NoNewline
    Write-Host "     Create a new template (interactive)"
    Write-Host "  test        " -ForegroundColor Yellow -NoNewline
    Write-Host "     Run all tests"
    Write-Host "  build       " -ForegroundColor Yellow -NoNewline
    Write-Host "     Build the project only"
    Write-Host "  version     " -ForegroundColor Yellow -NoNewline
    Write-Host "     Show version information"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Green
    Write-Host "  .\run.ps1                    # Open Field Types Demo (default - shows all controls)"
    Write-Host "  .\run.ps1 demo               # Run full CLI demo"
    Write-Host "  .\run.ps1 validate           # Validate example files"
    Write-Host "  .\run.ps1 help               # Show CLI help"
    Write-Host ""
    Write-Host "Project Information:" -ForegroundColor Green
    Write-Host "  PromptResponse - A cross-platform form creation and filling application"
    Write-Host "  Technology: .NET 8.0, C# 12, AvaloniaUI 11"
    Write-Host "  License: AGPL-3.0-or-later"
    Write-Host ""
}

# Show version
function Show-Version {
    Write-Header "PromptResponse Version Information"
    dotnet run --project src/PromptResponse.Cli -- version
    Write-Host ""
    dotnet --version
}

# Main script logic
function Main {
    # Check if we're in the right directory
    if (-not (Test-Path "PromptResponse.sln")) {
        Write-Error-Message "Error: Must be run from the PromptResponse project root directory"
        exit 1
    }

    switch ($Command) {
        'gui' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Start-GUI
        }
        'help' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Show-CLIHelp
        }
        'demo' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Invoke-AllDemos
        }
        'validate' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Invoke-ValidateDemo
        }
        'info' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Invoke-InfoDemo
        }
        'new' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Invoke-NewTemplateDemo
        }
        'test' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Invoke-Tests
        }
        'build' {
            dotnet build
        }
        'version' {
            if (-not (Build-Project)) { exit 1 }
            Write-Host ""
            Show-Version
        }
        'usage' {
            Show-Usage
        }
        default {
            Write-Error-Message "Unknown command: $Command"
            Write-Host ""
            Show-Usage
            exit 1
        }
    }
}

# Run main function
Main

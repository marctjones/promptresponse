# PromptResponse Usage Guide

Complete guide to using PromptResponse for creating and filling out forms.

## Table of Contents

1. [Installation](#installation)
2. [Getting Started](#getting-started)
3. [Creating Templates](#creating-templates)
4. [Filling Out Forms](#filling-out-forms)
5. [Managing Files](#managing-files)
6. [Tips and Best Practices](#tips-and-best-practices)

## Installation

### Prerequisites

- .NET 8.0 Runtime or SDK
- Linux or Windows (macOS support planned)

### Download and Install

```bash
# Download from releases
# (Instructions will be added when releases are available)

# Or build from source
git clone https://github.com/yourusername/promptresponse.git
cd promptresponse
dotnet build -c Release
dotnet run --project src/PromptResponse.Desktop
```

## Getting Started

### Understanding APR Files

PromptResponse uses `.apr` (Adaptive Prompt Response) files, which come in two types:

1. **Templates**: Blank forms to be filled out
2. **Filled Forms**: Completed forms with responses

The application automatically detects the type when you open a file.

### Basic Concepts

- **Prompt**: A question or field (e.g., "What is your name?")
- **Response**: Your answer (always stored as text)
- **Section**: A group of related prompts (e.g., "Personal Information")
- **Subsection**: A nested group within a section (e.g., "Name and Contact")

## Creating Templates

### Starting a New Template

1. Launch PromptResponse
2. Select **File → New Template**
3. Enter template metadata:
   - Title (required)
   - Description (optional)
   - Author (optional)

### Adding Sections

1. Click **Add Section**
2. Enter section title and optional description
3. Sections help organize related prompts

### Adding Subsections

1. Select a section
2. Click **Add Subsection**
3. Enter subsection title
4. Use subsections for further organization within sections

### Adding Prompts

1. Select a section or subsection
2. Click **Add Prompt**
3. Configure the prompt:
   - **Label**: The question or field name (e.g., "Email Address")
   - **Placeholder**: Example text shown in empty field
   - **Expected Data Type**: Hint for the UI (text, email, date, etc.)
   - **Suggested Values**: List of common answers for autocomplete
   - **Help Text**: Additional guidance for users

### Example Template Structure

```
Employment Application
├── Personal Information
│   ├── Name and Contact
│   │   ├── Full Legal Name
│   │   ├── Email Address
│   │   └── Phone Number
│   └── Date of Birth
└── Employment History
    ├── Current Employer
    └── Position/Title
```

### Reordering Prompts

- Drag and drop prompts, subsections, or sections
- Order matters: forms display in the same order

### Saving Templates

1. Click **File → Save** or **Save As**
2. Choose location and filename
3. File will be saved with `.apr` extension

### Template Best Practices

- **Use descriptive labels**: "Full Legal Name" not just "Name"
- **Add help text** for complex prompts
- **Group related prompts** in sections
- **Provide suggested values** where appropriate
- **Use appropriate data type hints** to help users

## Filling Out Forms

### Opening a Template

1. Launch PromptResponse
2. Select **File → Open**
3. Choose a template (`.apr` file)
4. Select **Fill Out Form** from the dialog

### Navigating the Form

- **Sections are collapsible**: Click section title to expand/collapse
- **Scroll through the form**: All prompts in one continuous flow
- **Tab between fields**: Use Tab key to move to next prompt

### Entering Responses

1. Click in any field to enter text
2. For fields with suggestions, start typing to see autocomplete
3. All responses are stored as text (you can enter anything)

### Data Type Hints

The UI may show different input widgets based on expected data type:

- **Date**: May show date picker, but you can type any text
- **Email**: May validate format, but any text is accepted
- **Number**: May show numeric keyboard on mobile
- **Multiline**: Larger text area

**Remember**: Type hints are suggestions. You can always enter any text.

### Autocomplete

If a prompt has suggested values:
1. Start typing in the field
2. Matching suggestions appear
3. Click a suggestion or continue typing your own

### Saving Filled Forms

1. Click **File → Save** or **Save As**
2. Choose location and filename
3. File will be saved as a filled form (`.apr`)

### Continuing Later

- Filled forms can be reopened and edited
- Your progress is saved
- The Last Modified timestamp updates automatically

## Managing Files

### File Types

Both templates and filled forms use `.apr` extension. The application automatically detects which type.

### Opening Files

**Templates** (blank forms):
- Opening shows dialog: "Edit Template" or "Fill Out Form"
- Choose based on your intent

**Filled Forms**:
- Opening goes directly to form filling mode
- Continue editing your responses

### Editing Templates

To modify an existing template:
1. Open the template
2. Choose **Edit Template**
3. Make changes to structure/prompts
4. Save

**Warning**: Editing a template doesn't update filled forms based on it.

### Converting Filled Form to Template

To create a new template from a filled form:
1. Open the filled form
2. Select **File → Save As Template**
3. All responses will be cleared
4. Save as a new template

### Exporting

(Future feature)
- Export to PDF
- Export to CSV
- Export to plain text

## Tips and Best Practices

### For Template Creators

1. **Keep it simple**: Don't overwhelm with too many prompts
2. **Logical grouping**: Use sections to organize by topic
3. **Clear labels**: Be specific and unambiguous
4. **Help text**: Provide guidance for complex prompts
5. **Test it**: Fill out your own template before sharing

### For Form Fillers

1. **Read help text**: Check for additional guidance
2. **Use suggestions**: They can save time
3. **Be thorough**: Empty responses are allowed but may be incomplete
4. **Save often**: Progress is saved with each save
5. **Review before submitting**: Check all sections

### Keyboard Shortcuts

(To be implemented)
- `Ctrl+N`: New Template
- `Ctrl+O`: Open File
- `Ctrl+S`: Save
- `Ctrl+Shift+S`: Save As
- `Tab`: Next field
- `Shift+Tab`: Previous field

### Accessibility

- All prompts are keyboard accessible
- Screen reader support (in progress)
- High contrast themes supported

## Troubleshooting

### File Won't Open

- Check file extension is `.apr`
- Verify file is valid JSON
- Check for UTF-8 encoding

### Lost Data

- Check for auto-save files (feature planned)
- Look for backup files with `.apr.bak` extension

### Application Crashes

- Check logs in `~/.promptresponse/logs/`
- Report issues on GitHub

## Getting Help

- Check [FILE_FORMAT.md](FILE_FORMAT.md) for format details
- See [DEVELOPMENT.md](DEVELOPMENT.md) for technical details
- Report issues: https://github.com/yourusername/promptresponse/issues

## Examples

See the `examples/` directory for sample templates:
- `employment-application.apr` - Job application form
- `survey-template.apr` - General survey
- `contact-form.apr` - Simple contact information

## Next Steps

- Try creating a simple template
- Fill it out and save
- Share templates with others
- Contribute improvements on GitHub

# PromptResponse Application Icons

## Design Concept

The PromptResponse icon represents the core functionality of the application: transforming rigid, hard-to-fill forms into flowing, conversational interactions.

### Visual Design

**Left Bubble (Teal #00ACC1):**
- Represents a **Prompt** (question)
- Contains a question mark symbol
- Subtle horizontal lines suggest form fields

**Flow Arrow:**
- Gradient from teal to green
- Represents the transformation/flow from question to answer
- Shows the dynamic, adaptive nature of the app

**Right Bubble (Green #66BB6A):**
- Represents a **Response** (answer)
- Contains a checkmark symbol
- Filled lines suggest completed fields

### Why This Design?

✅ **Instantly communicates purpose** - Prompt → Response flow
✅ **Distinguishes from traditional forms** - No rigid document/paper metaphor
✅ **Feels modern and approachable** - Rounded, flowing shapes
✅ **Scales well** - Clear at all sizes from 16x16 to 512x512
✅ **Represents adaptability** - Dynamic flow vs static pages

## Files

### Source
- `app-icon.svg` - Master SVG file (512x512, scalable)

### Generated Icons
- `app-icon.ico` - Multi-resolution Windows icon (16, 32, 48, 256)
- `app-icon-16.png` - 16x16 PNG
- `app-icon-32.png` - 32x32 PNG
- `app-icon-48.png` - 48x48 PNG
- `app-icon-256.png` - 256x256 PNG
- `app-icon-512.png` - 512x512 PNG

### Generation Script
- `generate-icons.sh` - Bash script to regenerate all icon formats from SVG

## Regenerating Icons

If you modify `app-icon.svg`, regenerate all formats:

```bash
cd src/PromptResponse.Desktop/Assets
./generate-icons.sh
```

**Requirements:**
- Inkscape (preferred): `sudo apt install inkscape`
- Or ImageMagick: `sudo apt install imagemagick`

## Usage in Application

The icon is referenced in `Views/MainWindow.axaml`:

```xml
<Window ...
        Icon="/Assets/app-icon.ico"
        ...>
```

And included in the project via `PromptResponse.Desktop.csproj`:

```xml
<ItemGroup>
    <AvaloniaResource Include="Assets\**" />
</ItemGroup>
```

## Color Palette

### Light Mode (Default)
- **Prompt (Teal)**: `#00ACC1`
- **Response (Green)**: `#66BB6A`
- **Text**: White (`#FFFFFF`)

### Dark Mode Alternative
If you want to create a dark mode version:
- **Prompt (Light Teal)**: `#4DD0E1`
- **Response (Light Green)**: `#81C784`
- **Text**: White (`#FFFFFF`)

## Brand Identity

This icon represents PromptResponse's core value proposition:

> **From rigid PDFs/Word forms to flowing, adaptive conversations**

The teal-to-green gradient symbolizes:
- **Progress** - Moving from question to completion
- **Growth** - Transforming static forms into dynamic interactions
- **Clarity** - Simple, clear communication

## Future Enhancements

Potential icon variations to consider:
- App store icons with background (iOS requires background)
- Notification icons (simplified, monochrome)
- File type icons for `.apr` files
- Toolbar icons for common actions

## License

This icon design is part of the PromptResponse project and follows the same GPL-3.0 license.

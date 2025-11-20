# PromptResponse Design System

<!-- AI-ASSISTANT-README -->
This document defines the visual design language for PromptResponse Desktop.
AI assistants MUST follow these specifications when creating or modifying UI.
All colors, spacing, typography, and components are defined here.
Reference: Windows 11 Fluent Design System
<!-- END-AI-ASSISTANT-README -->

## Design Philosophy

PromptResponse follows the **Windows 11 Fluent Design System** principles:

1. **Light**: Use light and shadow to create depth
2. **Depth**: Layer elements with elevation
3. **Motion**: Purposeful, subtle animations
4. **Material**: Mica and Acrylic translucency
5. **Scale**: Responsive to input method

### Target Experience

> "A tool that feels native on Windows 11, works seamlessly on macOS and Linux, and is so intuitive that a town hall clerk can master it in an afternoon."

---

## Color System

### Semantic Colors

```css
/* Primary Actions */
--color-primary: #0078D4;           /* Windows blue */
--color-primary-hover: #106EBE;
--color-primary-pressed: #005A9E;

/* Status Colors */
--color-success: #107C10;           /* Green */
--color-warning: #FF8C00;           /* Amber */
--color-error: #D13438;             /* Red */
--color-info: #0078D4;              /* Blue */

/* Neutral Colors - Light Theme */
--color-background: #F3F3F3;        /* Mica base */
--color-surface: #FFFFFF;           /* Cards, panels */
--color-surface-secondary: #F9F9F9; /* Subtle backgrounds */
--color-border: #E0E0E0;
--color-border-subtle: #F0F0F0;

/* Text Colors - Light Theme */
--color-text-primary: #1A1A1A;      /* Primary text */
--color-text-secondary: #616161;    /* Secondary text */
--color-text-disabled: #A0A0A0;
--color-text-on-accent: #FFFFFF;

/* Neutral Colors - Dark Theme */
--color-background-dark: #202020;
--color-surface-dark: #2D2D2D;
--color-surface-secondary-dark: #383838;
--color-border-dark: #3D3D3D;
--color-text-primary-dark: #FFFFFF;
--color-text-secondary-dark: #C8C8C8;
```

### Accent Color Integration

Use the system accent color for:
- Primary buttons
- Selected states
- Progress indicators
- Links

```xml
<!-- Avalonia: Use system accent -->
<SolidColorBrush x:Key="SystemAccentColor" Color="{DynamicResource SystemAccentColor}"/>
```

### Contrast Requirements

| Element | Minimum Ratio | Target |
|---------|---------------|--------|
| Body text | 4.5:1 | 7:1 |
| Large text (18px+) | 3:1 | 4.5:1 |
| UI components | 3:1 | 4.5:1 |
| Focus indicators | 3:1 | 4.5:1 |

**Note**: Current design prioritizes aesthetics. Accessibility audit in Month 6 will verify and fix contrast issues.

---

## Typography

### Font Family

```css
/* Primary: Segoe UI Variable (Windows) with fallbacks */
--font-family: "Segoe UI Variable", "Segoe UI", -apple-system, BlinkMacSystemFont, sans-serif;

/* Monospace: For code and IDs */
--font-family-mono: "Cascadia Code", "Consolas", monospace;
```

### Type Scale

| Name | Size | Weight | Line Height | Usage |
|------|------|--------|-------------|-------|
| Display | 40px | 600 | 52px | Hero sections |
| Title Large | 28px | 600 | 36px | Page titles |
| Title | 20px | 600 | 28px | Section headers |
| Subtitle | 16px | 600 | 24px | Card titles |
| Body Large | 16px | 400 | 24px | Important body text |
| Body | 14px | 400 | 20px | Default body text |
| Caption | 12px | 400 | 16px | Secondary info, timestamps |

### Typography in XAML

```xml
<!-- Title -->
<TextBlock FontSize="20" FontWeight="SemiBold" Margin="0,0,0,8"/>

<!-- Body -->
<TextBlock FontSize="14" FontWeight="Normal" LineHeight="20"/>

<!-- Caption -->
<TextBlock FontSize="12" Foreground="{DynamicResource TextSecondaryBrush}"/>
```

---

## Spacing System

### Base Unit: 4px

All spacing should be multiples of 4px.

| Token | Value | Usage |
|-------|-------|-------|
| `--space-xxs` | 2px | Tight icon spacing |
| `--space-xs` | 4px | Inline element spacing |
| `--space-sm` | 8px | Related element spacing |
| `--space-md` | 12px | Component internal padding |
| `--space-lg` | 16px | Section spacing |
| `--space-xl` | 24px | Major section spacing |
| `--space-xxl` | 32px | Page-level spacing |
| `--space-xxxl` | 48px | Hero spacing |

### Common Patterns

```xml
<!-- Card padding -->
<Border Padding="16">

<!-- Button padding -->
<Button Padding="16,8">

<!-- Form field spacing -->
<StackPanel Spacing="16">

<!-- Section margin -->
<Border Margin="0,24,0,0">
```

---

## Elevation & Shadows

### Elevation Levels

| Level | Usage | Shadow |
|-------|-------|--------|
| 0 | Flat surfaces | None |
| 1 | Cards, panels | `0 2px 4px rgba(0,0,0,0.08)` |
| 2 | Dropdown menus | `0 4px 8px rgba(0,0,0,0.12)` |
| 3 | Dialogs, modals | `0 8px 16px rgba(0,0,0,0.14)` |
| 4 | Notifications | `0 16px 32px rgba(0,0,0,0.18)` |

### Shadow in XAML

```xml
<!-- Card shadow (Elevation 1) -->
<Border BoxShadow="0 2 4 0 #14000000" CornerRadius="8">
    <!-- Content -->
</Border>

<!-- Dialog shadow (Elevation 3) -->
<Border BoxShadow="0 8 16 0 #24000000" CornerRadius="12">
    <!-- Content -->
</Border>
```

---

## Corner Radius

| Element | Radius |
|---------|--------|
| Buttons | 4px |
| Input fields | 4px |
| Cards | 8px |
| Dialogs | 12px |
| Full-round (pills) | 9999px |

```xml
<!-- Button -->
<Button CornerRadius="4"/>

<!-- Card -->
<Border CornerRadius="8"/>

<!-- Dialog -->
<Border CornerRadius="12"/>
```

---

## Components

### Buttons

**Primary Button** (main actions):
```xml
<Button Classes="Primary" Content="Save Template">
    <!-- Background: SystemAccentColor -->
    <!-- Foreground: White -->
    <!-- Height: 32px (default), 40px (large) -->
    <!-- Padding: 16,8 -->
    <!-- CornerRadius: 4 -->
</Button>
```

**Secondary Button** (alternative actions):
```xml
<Button Classes="Secondary" Content="Cancel">
    <!-- Background: Transparent -->
    <!-- Border: 1px solid BorderBrush -->
    <!-- Foreground: TextPrimary -->
</Button>
```

**Subtle Button** (tertiary actions):
```xml
<Button Classes="Subtle" Content="Learn more">
    <!-- Background: Transparent -->
    <!-- Border: None -->
    <!-- Foreground: TextSecondary -->
</Button>
```

**Icon Button**:
```xml
<Button Classes="IconButton" ToolTip.Tip="Add section">
    <PathIcon Data="{StaticResource AddIcon}"/>
    <!-- Size: 32x32 -->
    <!-- Background: Transparent, hover shows subtle fill -->
</Button>
```

### Input Fields

**Text Input**:
```xml
<TextBox Watermark="Enter your name" Classes="Modern">
    <!-- Height: 32px -->
    <!-- Padding: 12,6 -->
    <!-- Border: 1px solid, 2px on focus -->
    <!-- CornerRadius: 4 -->
    <!-- Focus: AccentColor border -->
</TextBox>
```

**With Label**:
```xml
<StackPanel Spacing="4">
    <TextBlock Text="Full Name" Classes="FieldLabel"/>
    <TextBox Watermark="John Smith"/>
    <TextBlock Text="Enter your legal name" Classes="FieldHint"/>
</StackPanel>
```

### Cards

**Standard Card**:
```xml
<Border Classes="Card">
    <StackPanel Margin="16">
        <TextBlock Text="Card Title" Classes="Subtitle"/>
        <TextBlock Text="Card content goes here" Classes="Body"/>
    </StackPanel>
</Border>

<!-- Card style -->
<!-- Background: SurfaceBrush -->
<!-- CornerRadius: 8 -->
<!-- BoxShadow: Elevation 1 -->
<!-- Hover: Slight elevation increase -->
```

**Interactive Card** (clickable):
```xml
<Button Classes="CardButton">
    <Border Classes="Card">
        <!-- Content -->
    </Border>
</Button>
```

### Lists

**List Item**:
```xml
<Border Classes="ListItem" Padding="12">
    <DockPanel>
        <PathIcon DockPanel.Dock="Left" Data="{StaticResource DocumentIcon}"/>
        <StackPanel Margin="12,0,0,0">
            <TextBlock Text="Document Name" Classes="Body"/>
            <TextBlock Text="Modified 2 hours ago" Classes="Caption"/>
        </StackPanel>
    </DockPanel>
</Border>

<!-- ListItem style -->
<!-- Background: Transparent -->
<!-- Hover: SubtleFillBrush -->
<!-- Selected: AccentColor at 10% opacity -->
<!-- Height: 48px minimum (touch target) -->
```

### Progress Indicators

**Progress Bar**:
```xml
<ProgressBar Value="75" Minimum="0" Maximum="100" Height="4">
    <!-- Track: BorderSubtle -->
    <!-- Fill: AccentColor -->
    <!-- CornerRadius: 2 -->
</ProgressBar>
```

**Progress Ring** (indeterminate):
```xml
<ProgressBar IsIndeterminate="True" Classes="Ring"/>
```

### Status Indicators

**Badge**:
```xml
<Border Classes="Badge Success">
    <TextBlock Text="Verified"/>
</Border>

<!-- Variants: Success (green), Warning (amber), Error (red), Info (blue) -->
<!-- Padding: 4,8 -->
<!-- CornerRadius: 4 -->
<!-- Font: Caption weight 600 -->
```

**Status Dot**:
```xml
<Ellipse Width="8" Height="8" Classes="StatusDot Connected"/>
<!-- Connected: Green -->
<!-- Disconnected: Gray -->
<!-- Error: Red -->
<!-- Warning: Amber -->
```

---

## Layout Patterns

### Dashboard Layout

```
┌─────────────────────────────────────────────┐
│ Header (64px)                               │
├─────────────────────────────────────────────┤
│                                             │
│  ┌─── Hero Section ───────────────────┐    │
│  │ Welcome message, Quick actions     │    │
│  └────────────────────────────────────┘    │
│                                             │
│  ┌─── Recent Documents ───────────────┐    │
│  │ List of recent files               │    │
│  └────────────────────────────────────┘    │
│                                             │
│  ┌─── Quick Actions ──────────────────┐    │
│  │ Grid of action cards               │    │
│  └────────────────────────────────────┘    │
│                                             │
├─────────────────────────────────────────────┤
│ Status Bar (32px)                           │
└─────────────────────────────────────────────┘
```

### Three-Panel Editor Layout

```
┌─────────────────────────────────────────────┐
│ Toolbar (48px)                              │
├──────────┬───────────────────┬──────────────┤
│          │                   │              │
│ Navigator│   Main Content    │  Properties  │
│  (240px) │    (flexible)     │   (280px)    │
│          │                   │              │
│ - Tree   │ - Preview         │ - Settings   │
│ - Add    │ - Edit            │ - Validation │
│          │                   │              │
├──────────┴───────────────────┴──────────────┤
│ Status Bar (32px)                           │
└─────────────────────────────────────────────┘
```

### Form Filling Layout

```
┌─────────────────────────────────────────────┐
│ Progress Bar                                │
├─────────────────────────────────────────────┤
│ Section Title                     Page 1/4  │
├─────────────────────────────────────────────┤
│                                             │
│  ┌─── Form Fields ────────────────────┐    │
│  │                                    │    │
│  │  Field Label                       │    │
│  │  [ Input                        ]  │    │
│  │  Help text                         │    │
│  │                                    │    │
│  │  Field Label                       │    │
│  │  [ Input                        ]  │    │
│  │                                    │    │
│  └────────────────────────────────────┘    │
│                                             │
├─────────────────────────────────────────────┤
│ [Previous]              [Save]      [Next]  │
└─────────────────────────────────────────────┘
```

---

## Motion & Animation

### Timing

| Duration | Usage |
|----------|-------|
| 100ms | Micro-interactions (hover, press) |
| 200ms | Small transitions (expand, collapse) |
| 300ms | Medium transitions (page, dialog) |
| 500ms | Large transitions (complex animations) |

### Easing

```css
/* Standard easing for most animations */
--ease-standard: cubic-bezier(0.4, 0.0, 0.2, 1);

/* Decelerate for entering elements */
--ease-decelerate: cubic-bezier(0.0, 0.0, 0.2, 1);

/* Accelerate for exiting elements */
--ease-accelerate: cubic-bezier(0.4, 0.0, 1, 1);
```

### Common Animations

**Hover State**:
- Duration: 100ms
- Property: Background color
- Easing: Standard

**Card Hover**:
- Duration: 200ms
- Property: Transform (translateY -2px), Shadow
- Easing: Decelerate

**Dialog Open**:
- Duration: 300ms
- Property: Opacity (0→1), Scale (0.95→1)
- Easing: Decelerate

**Note**: Respect `prefers-reduced-motion` media query for accessibility.

---

## Iconography

### Icon Source

Use **Segoe Fluent Icons** or **Fluent UI System Icons**.

For Avalonia, use PathIcon with icon data:

```xml
<PathIcon Data="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10..."/>
```

### Icon Sizes

| Size | Usage |
|------|-------|
| 12px | Inline with caption text |
| 16px | Inline with body text, list items |
| 20px | Buttons, navigation |
| 24px | Card headers, primary actions |
| 32px | Empty states, feature highlights |
| 48px | Hero illustrations |

### Icon Colors

- **Primary**: Same as text color (inherits)
- **Interactive**: AccentColor on hover/active
- **Disabled**: TextDisabled color
- **Status**: Semantic colors (success, warning, error)

---

## Avalonia Implementation Notes

### Platform Limitations

| Feature | Windows | macOS | Linux |
|---------|---------|-------|-------|
| Mica material | ✅ Native | ⚠️ Simulated | ⚠️ Simulated |
| Acrylic blur | ✅ Native | ⚠️ Limited | ❌ Fallback |
| System accent | ✅ Full | ⚠️ Limited | ❌ Manual |
| Native titlebar | ✅ | ✅ | ⚠️ Varies |
| Font rendering | ✅ ClearType | ✅ Native | ⚠️ Varies |

### Fallback Strategies

**Mica/Acrylic Fallback**:
```xml
<!-- Use solid color when transparency unavailable -->
<Border Background="{DynamicResource MicaBackgroundBrush}">
    <!-- MicaBackgroundBrush: Transparent on Windows, solid on others -->
</Border>
```

**Accent Color Fallback**:
```xml
<!-- Default accent if system color unavailable -->
<Color x:Key="FallbackAccentColor">#0078D4</Color>
```

### Performance Considerations

- Avoid complex shadows on Linux (can impact performance)
- Use virtual scrolling for lists > 100 items
- Minimize blur effects on large surfaces
- Cache rendered icons

---

## Accessibility Notes

**Current Status**: Design prioritizes visual polish. Accessibility compliance planned for Month 6.

### Known Issues to Address

1. **Contrast**: Some subtle text may not meet 4.5:1
2. **Focus indicators**: Need more visible focus rings
3. **Touch targets**: Some buttons < 44px
4. **Screen readers**: AutomationProperties need audit
5. **Keyboard navigation**: Tab order needs verification
6. **Motion**: No reduced-motion support yet

### Planned Remediation

- [ ] Contrast audit with Accessibility Insights
- [ ] Add high-contrast theme support
- [ ] Implement visible focus indicators (2px ring)
- [ ] Ensure 44px minimum touch targets
- [ ] Add AutomationProperties to all interactive elements
- [ ] Implement skip navigation
- [ ] Add prefers-reduced-motion support

---

## File Organization

```
src/PromptResponse.Desktop/
├── Styles/
│   ├── DesignTokens.axaml      # Colors, spacing, typography
│   ├── ModernControls.axaml    # Button, TextBox, etc. styles
│   ├── CardStyles.axaml        # Card component styles
│   ├── ListStyles.axaml        # List and item styles
│   └── LayoutStyles.axaml      # Panel and grid styles
├── Themes/
│   ├── Light.axaml             # Light theme overrides
│   └── Dark.axaml              # Dark theme overrides
└── Assets/
    └── Icons/                   # Icon path data
```

---

## Usage Guidelines

### For Developers

1. **Always use design tokens** - Never hardcode colors or spacing
2. **Check this document first** - Before creating new components
3. **Test on all platforms** - Especially Linux for visual issues
4. **Note accessibility gaps** - Document for future fix

### For AI Assistants

1. **Follow this specification exactly** - Colors, spacing, components
2. **Use existing styles** - Check Styles/ before creating new
3. **Maintain consistency** - Match existing UI patterns
4. **Document deviations** - If specification cannot be followed

---

*Design system version 1.0 - Updated 2024-11-20*

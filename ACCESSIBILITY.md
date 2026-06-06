# Accessibility Guide

## Design philosophy: capability-profile architecture

PromptResponse rejects the framing that splits users into "normal" and
"accessibility" populations. Every user has a **capability profile** — a set of
sensory, motor, and cognitive capabilities — and the application is structured
as a universal core plus optional, composable enhancements that serve specific
profiles.

- **Core (mandatory, universal):** keyboard reachable, semantic structure,
  screen-reader coherent, raw string responses, no required color signals,
  no required animation, no mouse-only interactions. Everyone starts here.
- **Rendering profiles (composable, optional):** layered enhancements that
  specific capability profiles benefit from. Enabling visual formatting for
  a sighted user is the same kind of accommodation as enabling verbose
  screen-reader announcements for a blind user — both serve a capability profile.
  Available profiles:
  `Default`, `Light`, `Dark`, `HighContrast`,
  `VisualFormatting`, `LargeText`, `ReducedMotion`,
  `ScreenReaderTuned`, `MotorAssist`. They compose ("most accommodating wins")
  through `CompositeProfile`.
- **OS preferences are auto-detected at startup.** Windows HCM, macOS Increase
  Contrast, GNOME high-contrast, prefers-reduced-motion, and screen-reader
  presence all flip the matching profile on.
- **Display Preferences panel** (View → Display Preferences) lets the user
  toggle each profile independently and Reset to OS-detected defaults.

### Vision invariant: type hints are advisory, never enforced

Every prompt's `expectedDataType` is a hint, not a constraint. A field hinting
"number" still accepts "five", "n/a", or "approximately 5" — any visible text
is a valid response. UIs and downstream programs use the hint for affordances
and advisory feedback only.

## CI gates (vision-critical, non-negotiable)

The CI pipeline blocks merge on any of these regressions:

- **Build green** on Linux + Windows + macOS (matrix).
- **Accessibility tests** (`tests/PromptResponse.AccessibilityTests`):
  - `ColorContrastTests` validates every (foreground, background) pair on every
    theme palette against WCAG 2.1 (AA on Light/Dark, AAA on HighContrast).
  - `XamlAccessibilityValidationTests` enforces `AutomationProperties.Name`
    coverage and minimum touch-target sizes on interactive controls.
  - `KeyboardNavigationValidationTests` verifies keyboard shortcuts and
    menu mnemonics are present in the live shell.
- **Coverage gate**: `PromptResponse.Core` line coverage ≥ 95% (currently
  97.69% / 95.95% branch / 100% method). `PromptResponse.Cli` coverage
  ratchets up over time.

## Accessibility Features

### Visual Accessibility

**Theme Support:**
- Light Theme (View → Theme → Light Theme)
- Dark Theme (View → Theme → Dark Theme)
- System Default (adapts to OS theme preference)
- High contrast support through system theme integration

**Visual Design:**
- Clear visual hierarchy with consistent spacing
- Sufficient color contrast ratios for text readability
- Larger clickable areas (minimum 36px height for inputs)
- Clear focus indicators for keyboard navigation
- Maximum content width (900px) for comfortable reading

**Typography:**
- Scalable fonts that respect system font size settings
- Clear font weights for hierarchy (Bold → SemiBold → Medium → Regular)
- Generous spacing between form fields (16-24px)
- Word wrapping for long text

### Screen Reader Support

**Automation Properties:**
All interactive elements include proper automation properties for screen readers:

```xml
<!-- Form title with semantic name -->
<TextBlock Text="{Binding Title}"
           AutomationProperties.Name="{Binding Title}"/>

<!-- Input fields with labels and help text -->
<TextBox Text="{Binding Response}"
         AutomationProperties.Name="{Binding Label}"
         AutomationProperties.HelpText="{Binding HelpText}"/>

<!-- Live regions for dynamic help text -->
<TextBlock Text="{Binding HelpText}"
           AutomationProperties.LiveSetting="Polite"/>
```

**Semantic Structure:**
- Form headers clearly identified
- Sections and subsections with descriptive names
- Collapsible expanders announced with content state
- Help text associated with input fields

### Keyboard Navigation

**Full Keyboard Support:**
- All functionality accessible via keyboard
- Logical tab order through form fields
- Menu shortcuts for common actions:
  - `Ctrl+N` - New template
  - `Ctrl+O` - Open file (extension determines template vs filled form mode)
  - `Ctrl+S` - Save
  - `Ctrl+Shift+S` - Save As
  - `Ctrl+W` - Close document
  - `F1` - Keyboard shortcuts cheat sheet
  - Menu mnemonics: `Alt+F` (File), `Alt+V` (View), `Alt+H` (Help)
  - `Tab` / `Shift+Tab` - Move between focusable controls
  - `Enter` - Activate the focused button or default action
  - `Space` - Toggle the focused checkbox or radio button

**Focus Management:**
- Clear focus indicators (built into FluentTheme)
- Logical focus order (top to bottom, left to right)
- Focus restored after dialog operations
- Tab navigation through all interactive elements

### Input Accessibility

**Form Fields:**
- Watermark text provides placeholder hints
- Help text provides additional guidance
- No required fields (save forms at any completion state)
- Undo/redo support in text inputs (standard OS behavior)
- Copy/paste support
- Text selection support

## Testing Accessibility

### Linux Testing Tools

#### Orca Screen Reader

**Installation:**
```bash
# Ubuntu/Debian
sudo apt-get install orca

# Fedora
sudo dnf install orca

# Arch Linux
sudo pacman -S orca
```

**Usage:**
```bash
# Start Orca
orca

# Or press Super+Alt+S (may vary by distro)
```

**Testing with Orca:**
1. Launch PromptResponse: `./run.sh`
2. Start Orca
3. Navigate through the form using:
   - `Tab` - Move to next element
   - `Shift+Tab` - Move to previous element
   - `Up/Down arrows` - Navigate within text
   - `Ctrl+Alt+Tab` - Navigate by form elements
4. Verify Orca announces:
   - Form title and description
   - Section and subsection names
   - Field labels and current values
   - Help text when focused on inputs
   - Button and menu states

#### Accerciser (GNOME Accessibility Explorer)

**Installation:**
```bash
# Ubuntu/Debian
sudo apt-get install accerciser

# Fedora
sudo dnf install accerciser
```

**Usage:**
```bash
accerciser
```

**Testing with Accerciser:**
1. Launch PromptResponse
2. Launch Accerciser
3. In Accerciser, find "PromptResponse" in the application tree
4. Inspect each element to verify:
   - Proper role assignments (button, text field, etc.)
   - Accessible names are set correctly
   - Help text is associated with inputs
   - State changes are reflected (expanded/collapsed)
   - Focus is visible and logical

#### AT-SPI2 Inspector

**Installation:**
```bash
# Ubuntu/Debian
sudo apt-get install at-spi2-core

# Fedora
sudo dnf install at-spi2-core
```

**Usage:**
```bash
# Enable accessibility in Avalonia
export AVALONIA_ENABLE_ACCESSIBILITY=1
./run.sh
```

**Inspection:**
```bash
# Dump accessibility tree
busctl --user call org.a11y.Bus org.a11y.Bus GetAddress
```

### Windows Testing Tools

#### Narrator (Built-in Screen Reader)

**Activation:**
- Press `Win+Ctrl+Enter`
- Or: Settings → Ease of Access → Narrator → Turn on Narrator

**Testing with Narrator:**
1. Launch PromptResponse
2. Start Narrator
3. Use scan mode (`Caps Lock+Space`) to navigate
4. Verify announcements of:
   - Window title
   - Menu items and shortcuts
   - Form structure
   - Field labels and values
   - Buttons and their states

#### NVDA (Free, Open Source)

**Download:** https://www.nvaccess.org/download/

**Installation:**
```powershell
# Download and run installer from website
# Or use portable version (no installation needed)
```

**Testing with NVDA:**
1. Launch NVDA (Ctrl+Alt+N after installation)
2. Launch PromptResponse
3. Navigate with:
   - `Tab` / `Shift+Tab` - Form fields
   - `H` - Headings (sections)
   - `F` - Form fields
   - `B` - Buttons
   - `E` - Edit fields (text inputs)

#### Accessibility Insights for Windows

**Download:** https://accessibilityinsights.io/downloads/

**Features:**
- Visual inspection of accessibility tree
- Automated WCAG compliance checks
- Color contrast analyzer
- Tab order visualization

**Testing:**
1. Install Accessibility Insights
2. Launch PromptResponse
3. Run automated tests
4. Review issues and suggestions
5. Verify tab order with visualization tool

### macOS Testing Tools

#### VoiceOver (Built-in Screen Reader)

**Activation:**
- Press `Cmd+F5`
- Or: System Preferences → Accessibility → VoiceOver

**Testing with VoiceOver:**
1. Launch PromptResponse
2. Enable VoiceOver
3. Use VoiceOver commander:
   - `Ctrl+Option+Right/Left` - Navigate
   - `Ctrl+Option+Space` - Activate
   - `Ctrl+Option+Shift+Down` - Interact with groups

#### Accessibility Inspector

**Location:** Xcode → Open Developer Tool → Accessibility Inspector

**Features:**
- Inspection of accessibility properties
- Element hierarchy visualization
- Audit for common issues

### Cross-Platform Browser-Based Testing

#### axe DevTools

While primarily for web, the principles apply:
- WCAG 2.1 compliance
- Color contrast checking
- Keyboard navigation testing
- Screen reader compatibility

**Principles to Test:**
1. **Perceivable:**
   - Text alternatives for icons
   - Color contrast ratios (4.5:1 minimum for normal text)
   - Resizable text
2. **Operable:**
   - Keyboard accessible
   - Enough time to interact
   - No seizure-inducing flashing
3. **Understandable:**
   - Predictable navigation
   - Clear labels and instructions
   - Input assistance (help text)
4. **Robust:**
   - Compatible with assistive technologies

## Testing Checklist

### Visual Accessibility
- [ ] Test with Light theme
- [ ] Test with Dark theme
- [ ] Test with System Default theme
- [ ] Verify sufficient color contrast in both themes
- [ ] Test with system font size increased to 150%
- [ ] Test with system font size increased to 200%
- [ ] Verify focus indicators are visible on all interactive elements

### Keyboard Navigation
- [ ] Tab through entire form in logical order
- [ ] Test all keyboard shortcuts (Ctrl+O, Ctrl+S, etc.)
- [ ] Access all menus with keyboard (Alt+F, Alt+V, Alt+H)
- [ ] Verify Esc key closes dialogs
- [ ] Test Enter key in form fields
- [ ] Verify focus is trapped in modal dialogs
- [ ] Test Shift+Tab for reverse navigation

### Screen Reader
- [ ] Launch screen reader before opening app
- [ ] Verify window title is announced
- [ ] Navigate through menu and verify announcements
- [ ] Open a form and verify title/description announced
- [ ] Tab through form fields and verify labels announced
- [ ] Verify help text is announced when focused
- [ ] Expand/collapse sections and verify state changes
- [ ] Test with actual blind user if possible

### Input Accessibility
- [ ] Verify all inputs have labels
- [ ] Verify help text appears below inputs
- [ ] Test placeholder text visibility
- [ ] Verify inputs work with screen reader
- [ ] Test copy/paste in all text fields
- [ ] Test undo/redo in text fields
- [ ] Verify no keyboard traps

### Form Structure
- [ ] Verify form header is identifiable
- [ ] Verify sections have clear hierarchy
- [ ] Verify subsections are properly nested
- [ ] Verify expanders announce expanded/collapsed state
- [ ] Test with very long form (10+ sections)

## WCAG 2.1 Compliance

PromptResponse targets **WCAG 2.1 Level AA** compliance:

### Level A (Met)
- 1.1.1 Non-text Content - All icons have text alternatives
- 1.3.1 Info and Relationships - Semantic structure maintained
- 2.1.1 Keyboard - All functionality keyboard accessible
- 2.4.1 Bypass Blocks - Form sections allow quick navigation
- 3.2.2 On Input - No unexpected context changes

### Level AA (Met)
- 1.4.3 Contrast (Minimum) - 4.5:1 contrast ratio maintained
- 1.4.5 Images of Text - No text rendered as images
- 2.4.6 Headings and Labels - Descriptive section/field labels
- 2.4.7 Focus Visible - Clear focus indicators
- 3.3.3 Error Suggestion - Validation errors provide guidance

### Level AAA (Partial)
- 1.4.8 Visual Presentation - Text spacing can be customized via OS
- 2.5.5 Target Size - Inputs meet enhanced 44×44 CSS pixel minimum

## Common Accessibility Issues and Fixes

### Issue: Screen reader not detecting app

**Linux:**
```bash
# Enable AT-SPI in Avalonia
export AVALONIA_ENABLE_ACCESSIBILITY=1
./run.sh
```

**Windows:**
- Ensure Narrator/NVDA started before app
- Check Windows accessibility settings are enabled

### Issue: Focus not visible

**Solution:**
- Theme should provide focus indicators
- Check system high contrast settings
- Verify FluentTheme is applied correctly

### Issue: Tab order incorrect

**Solution:**
- Verify TabIndex="0" on all interactive elements
- Remove any negative TabIndex values
- Ensure logical visual layout

### Issue: Labels not announced

**Solution:**
- Verify AutomationProperties.Name is set
- Ensure labels are bound to data
- Check screen reader verbosity settings

## Accessibility Development Guidelines

### Do:
✅ Set AutomationProperties.Name on all interactive elements
✅ Provide AutomationProperties.HelpText for complex inputs
✅ Use semantic controls (Button, TextBox) not custom drawings
✅ Test with actual screen readers regularly
✅ Support system theme preferences
✅ Maintain focus visibility
✅ Provide keyboard shortcuts for common actions
✅ Use descriptive names for UI elements

### Don't:
❌ Use color alone to convey information
❌ Create keyboard traps
❌ Rely on hover-only interactions
❌ Use custom controls without accessibility support
❌ Implement time limits without extensions
❌ Use placeholder text as the only label
❌ Create inaccessible custom focus indicators

## Automated Testing

**PromptResponse includes automated accessibility tests!**

Located in: `tests/PromptResponse.AccessibilityTests/`

**Two types of automated tests:**

1. **Static APR Validation** (Fast, Always Available)
   - Validates APR files have proper accessibility metadata
   - Runs in CI/CD without special setup
   - Tests labels, titles, help text, structure
   - **8 tests currently passing**

2. **Runtime Inspection** (Integration, Platform-Specific)
   - Inspects actual accessibility tree at runtime
   - Verifies what assistive technologies see
   - Cross-platform (Linux/Windows/macOS planned)
   - Framework implemented, full integration pending

**Running tests:**
```bash
# Run all accessibility tests (fast)
dotnet test tests/PromptResponse.AccessibilityTests

# Expected: 8 passed, 2 skipped (integration tests)
```

**See:** `tests/PromptResponse.AccessibilityTests/README.md` for complete documentation.

## Resources

### Standards and Guidelines
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [ARIA Authoring Practices](https://www.w3.org/WAI/ARIA/apg/)
- [Section 508 Standards](https://www.section508.gov/)

### Avalonia Accessibility
- [Avalonia Accessibility Documentation](https://docs.avaloniaui.net/docs/concepts/accessibility)
- [AutomationProperties API](https://docs.avaloniaui.net/api/Avalonia.Automation/AutomationProperties/)

### Testing Tools
- [Orca Screen Reader](https://wiki.gnome.org/Projects/Orca)
- [NVDA Screen Reader](https://www.nvaccess.org/)
- [Accessibility Insights](https://accessibilityinsights.io/)
- [WebAIM Resources](https://webaim.org/resources/)

### Project-Specific
- [Automated Test Framework](tests/PromptResponse.AccessibilityTests/README.md)
- [test-accessibility.sh](test-accessibility.sh) - Interactive Orca testing script

### Community
- [A11y Project](https://www.a11yproject.com/)
- [Deque University](https://dequeuniversity.com/)
- [WebAIM Forums](https://webaim.org/discussion/)

## Contributing

When contributing accessibility improvements:

1. Test with at least one screen reader before submitting
2. Document new accessibility features in this guide
3. Run accessibility audits on changed areas
4. Include accessibility testing in PR description
5. Follow WCAG 2.1 AA guidelines minimum

## Support

If you encounter accessibility issues:

1. Check this guide for known solutions
2. Test with latest version of assistive technology
3. Report issues with:
   - Specific assistive technology and version
   - Operating system and version
   - Steps to reproduce
   - Expected vs actual behavior

Accessibility is a journey, not a destination. We continually work to improve accessibility for all users.

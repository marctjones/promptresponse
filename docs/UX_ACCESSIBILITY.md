# UX and accessibility design

## Experience model

PromptResponse has two first-class modes: **authoring** templates and **filling**
forms. The home screen provides recent files, starter templates, and clear new/open
actions. A form supports progressive completion, search, advisories, export, and
explicit handoff without hiding the raw response value.

The desktop client uses a native menu, keyboard shortcuts, clear empty states,
sectional navigation, and an optional wizard mode for long forms. Authoring and
filling preserve semantic order rather than page geometry.

## Universal base and capability profiles

Every user receives a usable base: keyboard reachability, semantic labels, visible
focus, raw-text entry, no color-only signal, and no hover-only action. Optional,
composable profiles add **Light Theme**, **Dark Theme**, high-contrast themes, large text, reduced motion,
screen-reader tuning, large targets, input masks, display formatting, calendar, and
boolean affordances. A specialized widget never removes the raw-text path.

The live palette and profile behavior in `src/PromptResponse.Desktop/Profiles/` are
the visual implementation source; this document defines their product intent.

## Accessibility evidence

CI checks contrast, XAML accessibility metadata, keyboard behavior, headless
automation trees, and APR accessibility structure. Linux also has an opt-in live
AT-SPI smoke test. macOS VoiceOver/AX evidence is recorded for release candidates;
Windows live UI Automation and broader live Linux coverage remain planned. Do not
describe these checks as proof of universal screen-reader or WCAG conformance.

## Keyboard Navigation testing checklist

- [ ] Tab and Shift+Tab traverse every actionable surface in a logical order.
- [ ] Ctrl+O, Ctrl+S, Ctrl+Shift+S, Alt+F, Alt+V, Alt+H, Enter, Space, and Escape
  behave conventionally where offered.
- [ ] Focus remains visible and returns sensibly after dialogs and advisories.
- [ ] Every actionable control has a useful accessible name and contextual help.
- [ ] Light, dark, high-contrast, reduced-motion, and screen-reader-tuned paths
  retain usable contrast and interaction.

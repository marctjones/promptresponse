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

## Visual design

The desktop client follows Windows 11 (Fluent) geometry and type values and Apple's
structural advice for forms, and says plainly that it renders the same on every
platform rather than imitating native chrome. A document states no layout, so each
field's presentation is derived from its type, its hints, and the active profile;
the file format is never extended to describe it.

- **Shape:** controls and in-page surfaces use a 4 px radius; straight edges that
  meet are not rounded.
- **Type:** the Windows type ramp — 12/16 caption, 14/20 body, semibold rather than
  bold for emphasis, sentence case.
- **Field width follows the expected input.** Short types (number, currency, phone,
  date, single choice) are drawn at a fixed short width; text, email, URL and
  multiline use the full measure, which is capped so a line stays near 50 to 60
  characters.
- **Colour comes from the active profile.** Views not bound to the shell read
  `Pr.*` palette resources that the profile publishes, so Dark and High Contrast
  reach every field.
- **The raw-text path stays.** The control that switches a specialised widget to a
  plain text box is a quiet icon button, not removed, and keeps its name, command
  and 36 px target.
- **Order is semantic.** Visual order, Tab order and reading order are the document
  order; no layout rule may reorder them.

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

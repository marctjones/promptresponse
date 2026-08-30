# macOS accessibility release evidence

For a packaged release candidate, record macOS version, hardware architecture, application build, VoiceOver version, AX capture, and results for default, dark, increased-contrast, and reduced-motion paths.

- [ ] Keyboard navigation reaches every form, menu, dialog, and handoff control.
- [ ] VoiceOver announces names, roles, values, help, and advisory changes without stealing focus.
- [ ] `scripts/verify-macos-accessibility.sh <packaged-app>` captures a usable tree.
- [ ] Email handoff works with Automation permission approved and has a usable fallback when denied.

Automated tests are necessary but not sufficient evidence of VoiceOver compatibility.

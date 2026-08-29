# macOS accessibility smoke checklist

Run this on a release candidate using the packaged `.app`, not only from `dotnet run`.
Record the macOS version, VoiceOver version, app build, and every pass/fail result.

## Required release matrix

For each supported macOS major version and both Apple Silicon and Intel artifacts,
record the result for every row. A failure blocks the release until it is fixed or
documented as an approved exception by the accessibility owner.

| Condition | Keyboard only | VoiceOver | AX capture | Result / tester |
|---|---:|---:|---:|---|
| Default appearance | required | required | required | |
| Dark appearance | required | required | required | |
| Increase Contrast | required | required | required | |
| Reduce Motion | required | required | required | |
| Apple Mail automation approved | required | required | required | |
| Apple Mail automation denied (fallback) | required | required | required | |

Retain the generated `nsaccessibility-tree.json`, macOS version, code-signing
record, completed table, and any VoiceOver recordings/transcripts with the release.

1. In **System Settings → Accessibility**, enable VoiceOver, Increase contrast, and
   Reduce motion one at a time. Relaunch PromptResponse after each setting and confirm
   its matching capability profile is active or select it explicitly in Display Preferences.
2. With VoiceOver, open a filled APR form. Traverse every field with Tab and VoiceOver
   navigation. Confirm each announces its label, type/role, current value, required/help
   context, and validation/advisory updates without focus being stolen.
3. Complete **File → Submit via email…**. Confirm the destination chooser announces
   each mail address as a radio choice, Cancel is reachable, and Continue describes its action.
4. Confirm the save dialog, recipient confirmation, and final handoff message are all
   announced and keyboard-operable.
5. Approve the macOS Automation permission for PromptResponse/osascript, then confirm
   Apple Mail opens a visible draft with the completed `.aprf` attached, recipient,
   subject, and body correct. Repeat with Automation denied and confirm the generic
   manual-attachment fallback remains usable.
6. Use Accessibility Inspector, or `MacAccessibilityInspector` with Accessibility
   permission granted to the test runner, to capture the live NSAccessibility tree and
   confirm named controls/roles for the form and submission flow.

Passing automated tests alone is not sufficient evidence of VoiceOver compatibility.

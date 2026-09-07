# Accessibility sign-off

<!-- AI-ASSISTANT-README -->
This is the release checklist for what no harness decides. Every item names the rule
it stands in for. Do not add an item a machine could check — put that in the renderer
suite instead — and do not remove one because a machine now checks something adjacent.
<!-- END-AI-ASSISTANT-README -->

## Why this exists

Three renderer drivers score chapter 13 against
[the renderer contract](../RENDERER_CONFORMANCE.md): a reference renderer, the
TypeScript HTML renderer, and the Avalonia desktop client. They decide, per case and
per rule, whether a section is named, whether help text reached the control, whether
a table's cells name their column, whether the keyboard reaches every field and can
fill it in, and whether anything was fetched while rendering.

**They do not decide whether the result is usable.** That is not a gap to be closed
later. It is the boundary of what a snapshot can carry, and the reason a contract
that quietly claimed it would be worse than one that says where it stops.

Naming the irreducibly human checks is what lets every other rule be asserted without
hedging.

## The residue

### R1. Whether structural conveyance is comprehensible — `APR-RENDER-004`

The harness checks that nesting is conveyed as a heading level or as a group rather
than by indentation. It cannot check that the resulting structure makes sense to
somebody navigating by headings: that the levels correspond to how the form is
organised, that a group's name says what the group is for, that a person can tell
where a section ended.

### R2. How narration actually sounds — `APR-RENDER-001`, `APR-RENDER-002`, `APR-RENDER-003`, `APR-RENDER-007`

The harness checks that an accessible name equals the label, that help text is
associated rather than adjacent, that a cell names its column. It cannot check
whether the utterance a screen reader produces is comprehensible: whether the name
is announced at a useful moment, whether the help text is announced before it is
needed or long after, whether a table's row and column context is repeated so often
it becomes noise. `tests/at-spi/README.md` records the same boundary for its layer.

### R3. Whether the tab order is sensible — `APR-RENDER-005`, `APR-RENDER-008`

The harness checks that every prompt is in the focus order, that a response can be
typed there, that Shift-Tab returns, and that the order does not contradict document
order. It cannot check whether the order is *reasonable*: whether focus lands
somewhere useful on open, whether a long form's controls are grouped so that Tab is
not the only way through, whether focus goes somewhere sane after a row is added.

### R4. Contrast and legibility against a real background — no rule

Rendered against a real theme, at a real size, with a real display. The format states
nothing about this and the harness measures nothing about it, so it is listed to say
that its absence from the automated evidence is deliberate.

### R5. Unproven driven branches — `APR-RENDER-005`

Two branches the headless input pipeline cannot drive, recorded rather than claimed:

- Space on a `ToggleButton` does not toggle it, so a checkbox's completability is
  left unclaimed by the Avalonia driver rather than claimed either way.
- `MouseDown`/`MouseUp` do not raise `Click`, so pointer activation is exercised only
  through `GuiTestExtensions.Activate`.

Both are stated at `FocusOrder.CanBeCompleted` and in `KeyboardFlowTests`' remarks.
They move out of this list when a native driver proves them, not when the harness
stops asking.

## Per-release checklist

Record for each: platform version, hardware architecture, application build, screen
reader version, and the captured accessibility tree. Run all four visual paths —
default, dark, increased contrast, reduced motion.

### macOS — VoiceOver

- [ ] `scripts/verify-macos-accessibility.sh <packaged-app>` captures a usable tree.
- [ ] **R1** Navigating by heading reaches every section, and the structure read aloud
      matches how the form is organised.
- [ ] **R2** Every field is announced with its name, role, value and help text, and
      an advisory change is announced without moving focus.
- [ ] **R3** Tab reaches every control in an order that makes sense, focus is somewhere
      useful on open, and adding a table row leaves focus somewhere sane.
- [ ] **R4** Legible at the default size in all four visual paths.
- [ ] Email handoff works with Automation permission approved, and degrades usably
      when it is denied.

### Windows — NVDA over UIA

- [ ] **R1** Heading navigation, as above.
- [ ] **R2** Narration, as above.
- [ ] **R3** Tab order, as above.
- [ ] **R4** Legibility, as above.
- [ ] A table announces row and column context without repeating it into noise.

### Linux — Orca over AT-SPI

- [ ] `tests/at-spi/run_at_spi_smoke.sh` passes against the packaged build.
- [ ] **R1** Heading navigation, as above.
- [ ] **R2** Narration, as above.
- [ ] **R3** Tab order, as above.
- [ ] **R4** Legibility, as above.

## What this is not

It is not a substitute for the drivers, and the drivers are not a substitute for it.
An item here that a machine could decide belongs in
`tests/Conformance/beta6/renderer/cases.json`; a rule there that a person has to judge
belongs here.

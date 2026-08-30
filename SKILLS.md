# Refactoring Playbook

Use this playbook when asked to refactor a project, module, file, function, or
test suite. The objective is a smaller, clearer ownership boundary with no
behavior regression—not a lower line count by itself.

## 1. Establish the boundary before editing

- Start from the current worktree and issue/milestone state. Preserve unrelated
  changes and do not assume a clean branch belongs to the refactor.
- Map the target's responsibilities, public APIs, event subscriptions, mutation
  and undo boundaries, DI/composition wiring, view bindings, and existing test
  seams. For Avalonia, include XAML bindings, generated commands, automation
  IDs, accessibility announcements, and disposal lifetime.
- Use compiler/Roslyn analysis, symbol-aware search, Git history, and tests as
  evidence. Static analysis can find hazards and dependencies; it does not
  choose an architectural boundary by itself.
- Create or update a focused issue with the observed problem, desired ownership
  boundary, preserved contracts, validation plan, and a clear stop condition.

## 2. Choose a cohesive extraction

Prefer extracting one independently understandable responsibility, such as a
workflow, presentation state, mutation coordinator, renderer stage, or test
fixture. Keep stable public façades when bindings or callers depend on them.

Do not split a class merely because it is long. Stop when the remaining code is
principally intentional composition, compatibility forwarding, or a stable
public API, unless a concrete coupling, defect, performance cost, or change
history provides a new reason to proceed.

## 3. Parallelize by file and ownership boundary

Parallel work is useful only when lanes have non-overlapping production paths.

- One lane owns the integration boundary and its immediate collaborators.
- Other lanes may work on isolated Core/CLI/rendering modules, documentation,
  or unrelated test suites.
- Do not concurrently edit a coordinator and its direct workflows, DI setup,
  XAML bindings, or shared test harnesses without an explicit handoff.
- Use independent staging: stage and commit only the files owned by the issue.
  If another lane's files are staged, unstage only those explicit paths; never
  discard their work.
- Use isolated output paths for concurrent focused tests. In this repository,
  prefer `scripts/test-focused.sh` or a serialized `dotnet test` command with
  `-m:1 -p:UseSharedCompilation=false -p:GenerateDocumentationFile=false`.

## 4. Preserve behavior while changing structure

- Keep external contracts, serialized formats, command names, binding names,
  accessibility behavior, undo/redo semantics, and session lifetime stable
  unless the issue explicitly authorizes a behavior change.
- Add direct tests for a new collaborator when it owns meaningful policy.
  Retain focused façade tests for contracts that views, automation, or callers
  consume.
- Treat a defect found during refactoring as its own issue unless fixing it is
  essential to preserve the extracted boundary. Include a reproducer and test.
- Run `git diff --check` before validation and inspect the diff for accidental
  behavior changes.

## 5. Validate in proportion to blast radius

- Always run the focused affected tests after an extraction.
- Run a major project test section after changing shared coordination, DI,
  bindings, rendering, serialization, or test infrastructure.
- Run the full suite before declaring a central refactor or milestone complete,
  and before release. A focused green suite is evidence for one seam, not for
  the entire product.
- Record the exact command and pass/fail count in the issue.

## 6. Keep progress recoverable

- Make a small, self-contained commit after each verified boundary.
- Update the issue immediately with the commit, tests, discovered defects, and
  remaining work. Close it only when its done criteria are met.
- Fast-forward or otherwise integrate a clean refactor branch regularly once
  it is verified; do not accumulate unmerged parallel work.
- Refresh the next-file priority from current evidence. High-level plans may be
  prepared early, but detailed extraction plans should be checked again when
  implementation begins.

## 7. Recommended analysis tools

For .NET, begin with built-in Roslyn analyzers (`dotnet format … analyzers`),
compiler errors, solution-aware search, and targeted tests. Add third-party
analyzers only after baselining their findings and configuring a non-overlapping,
actionable rule set. Use custom Roslyn analysis when a repository-specific
dependency, layering, or API rule needs to become repeatable.

For TypeScript and Java, use the language type checker plus project linting and
semantic migration tools appropriate to that module. Do not enable broad new
rules as CI blockers until their existing findings are triaged.

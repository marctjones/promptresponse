# Implementation Registry

**Status:** Living document · **Format version:** `1.0-beta` · **Corpus:** `corpus/v1`

Every artifact PromptResponse ships or intends to ship: what it is, what language it
is written in, what it must do, and what it is held to.

The registry exists because a format project has an unusual failure mode. Adding an
implementation is not neutral — each one can drift, and drift in a format is not a bug
in one program but a fracture in the format itself. This document is the list of things
that must not fracture, and the rules that keep them together.

---

## How to read this

### Status

| | Meaning |
|---|---|
| **Shipped** | Exists and passes its gates |
| **Partial** | Exists but does not meet its obligations yet |
| **Planned** | Agreed, not started |
| **Deferred** | Worth doing when someone needs it — not before |
| **Won't build** | Deliberately rejected. The reason is recorded so it is not relitigated |

### Conformance profiles

Declared per implementation. `core` alone is fully conformant, not degraded.

| Profile | Requires |
|---|---|
| `core` | Parse, validate, render, fill, write. A JSON parser and nothing else |
| `core+expressions` | Evaluate the `expr*` hint family |
| `core+signatures` | Verify detached CMS signatures |

An implementation **MUST** preserve what it does not implement: a `core`-only reader
keeps `expr*` strings and the `signatures` array intact on round-trip.

### Rules that bind everything here

1. **Third-party dependencies must be permissively licensed** — MIT, Apache-2.0, BSD.
   No copyleft, no commercial-licence schemes, no trust-eroding install behaviour.
2. **Every implementation is gated by the corpus.** Not "tested against" — gated, in CI.
3. **The format is versioned separately from every implementation.** See specification
   §1.3: format version, spec-document version, and corpus tag are three different numbers.

---

## 1. Format artifacts — the normative core

These are the product. Everything else is an implementation of them.

| Artifact | Path | Status |
|---|---|---|
| **Specification** | `docs/APR_SPECIFICATION.md` | **Shipped** — 1,028 lines, spec v0.6.0, describes format `1.0-beta` |
| **JSON Schema** | `schemas/apr-1.0.schema.json` | **Shipped** — Draft 2020-12 |
| **Conformance corpus** | `tests/Conformance/v1/` | **Shipped** — 35 fixtures |
| **Canonicalization vectors** | `tests/Conformance/v1/canonicalization/` | **Shipped** — `apr-sig-v2` byte contract |
| **SDK conformance contract** | `docs/SDK_CONFORMANCE.md` | **Shipped** |
| **Test registry** | `tests/registry.json` + `scripts/check-test-registry.py` | **Shipped** — CI-verified |
| **Type registry** | `schemas/apr-types-1.0.json` | **Shipped** — 15 types and 8 enumerated attributes, verified against the code, schema, and spec |
| **Expression binding vectors** | *(none)* | **Planned** — see §5 |

Authority runs **corpus → schema → prose**. Where prose disagrees with a fixture, the
prose is the bug.

### Corpus composition

| Category | Count | Obligation |
|---|---|---|
| `valid/` | 17 | Parse, validate clean, round-trip **byte-exactly** |
| `invalid/` | 11 | Parse, then fail validation |
| `malformed/` | 4 | **Rejected at parse time** — never coerced |
| `signatures/` | 2 | Validate structurally, **fail** verification |
| `canonicalization/` | 1 + vectors | Reproduce the signed payload **byte-for-byte** |

---

## 2. Applications — one of each

Exactly one desktop client and one CLI. Multiplying user-facing applications
multiplies drift without reaching anyone new.

### 2.1 Desktop client — **Shipped**

**Language: C# / .NET 10 / Avalonia 12** · Profiles: `core+expressions+signatures`

The only surface where accessibility can be proven end to end, which is why it is the
one that must not be rewritten casually.

- Accessibility across Windows (UIA), macOS (NSAccessibility), Linux (AT-SPI2),
  verified against Orca
- Capability profiles: Light/Dark/HighContrast, LargeText, ReducedMotion,
  ScreenReaderTuned, LargeHitTargets, WizardMode, plus composable display flags
- APRT structural editor with undo/redo and drag-drop reorder
- Fill, PDF/HTML export, PDF import, signing UI
- Headless GUI test harness

**Why not Electron, Tauri, or Flutter:** the accessibility work is the expensive part
and it is done. Tauri inherits the system webview's accessibility, weakest on Linux —
the wrong risk for an accessibility-first project. Flutter's desktop Linux support is
weaker still. Electron is credible on accessibility but ships a browser to do it.

### 2.2 CLI — **Shipped**

**Language: C#** · Profiles: `core+expressions+signatures`

13 commands: `validate`, `info`, `new`, `fill`, `stats`, `diff`, `export`, `import`,
`keygen`, `sign`, `verify`, `help`, `version`.

**Why one, and why C#:** a complete CLI must export PDF/PDF-A and produce CMS
signatures. Both are expensive or awkward outside .NET — Go in particular has no
strong maintained PKCS#7 library. A second CLI in another language would either
duplicate that work or ship an incomplete tool.

The value a second implementation would have provided — finding spec ambiguities by
forcing different assumptions — comes instead from the SDKs (§3), which span four type
systems and four JSON libraries.

### 2.3 Web demo — **Planned**

**Language: Python / FastAPI** · Profile: `core`

Runs locally over `http://localhost`, no install beyond Python 3, and scales to a
real deployment unchanged.

- **MUST** be built on the Python SDK (§3.3), not re-implement parsing inline
- Server-rendered HTML inherits the browser's accessibility stack — the strongest and
  most portable there is
- **Replaces** `aprt-server.py` (532 lines, legacy, parses APR inline — the exact
  pattern that produced the Python SDK's drift)

### 2.4 Browser extension — **Planned**

**Language: TypeScript** · Profile: `core`

- Render `.aprt` / `.aprf` inline when encountered
- Fill and save locally
- Fill **web forms** from an `.aprf` — the "programmatic filling" pillar, and the most
  viral capability on this list

Built on the TypeScript SDK and renderer (§3.2). Forced language: WebExtensions is a
JavaScript API.

---

## 3. SDKs — several, by audience

The only surface that needs multiple languages. Each earns its place by reaching people
the others cannot.

### 3.1 C# / .NET — **Shipped** *(reference implementation)*

`src/PromptResponse.Core` · Profiles: `core+expressions+signatures`

The conformance benchmark. Gated by `ConformanceCorpusTests` (11 test methods) in CI.

### 3.2 TypeScript — **Planned** *(highest leverage)*

Profile: `core`, later `core+signatures`

Reaches browsers, the extension, Node, Chromebooks, phones, and every locked-down
desktop where nothing installs.

**Includes extracting the renderer.** A JavaScript renderer already exists — inlined as
string concatenation inside `FillableHtmlDocumentRenderer.cs`, where it is untestable
and unreusable. Extracting it to a real package means Core, the extension, and the web
SDK share one accessible renderer instead of three.

### 3.3 Python — **Partial → rebuild**

`python/` · Currently **non-conformant**

Exports `Subsection`, `DigitalSignature`, and `SubmissionConfig` — types with no
counterpart in the format. It implements a **different schema** and must be rebuilt
against the corpus. Blocks the web demo (§2.3).

Reaches data pipelines, government scripting, and database import.

### 3.4 Java — **Planned**

Reaches enterprise and government integration, and Android. The enterprise language
that genuinely warrants an SDK.

### 3.5 Deferred

| SDK | Path | When |
|---|---|---|
| **Go** | *(none)* | Services and single-binary tooling. Official CEL implementation available |
| **Rust** | `rust/` stub | Could back Python, Node, and WASM bindings from one core — a force multiplier if SDK count becomes unsustainable |
| **C++** | `cpp/` stub | Embedding only. No identified consumer |

Ruby, PHP: **won't build** until someone asks.

---

## 4. Enterprise integration — converters, not SDKs

A converter turns APR into a shape the target system already consumes, so that system
never learns anything about APR: no library linked, no code changed on their side.
That is the only integration a mainframe or ERP shop realistically accepts.

All live in the existing C# CLI. **No new language.**

| Converter | Emits | Status |
|---|---|---|
| **COBOL copybook generator** | `.cpy` from a template | **Planned** |
| **Fixed-width records** | Copybook-aligned flat file | **Planned** |
| **Relational mapping** | Table schema + bulk loader | **Planned** — highest-value enterprise integration on the list |

**Why no COBOL SDK:** the artifact a mainframe programmer wants is the copybook, not a
JSON parser. Generating it signals domain understanding more strongly than a parser
would, and it stays correct automatically because it derives from the schema.

Worth stating publicly: **strings-only makes APR unusually mainframe-friendly.** Every
value maps to `PIC X(n)` — no `COMP-3`, no precision questions, no null handling. The
rule that costs a typing layer everywhere else is an advantage here.

---

## 5. Conformance engines

What holds every implementation above to the same format.

| Engine | Language | Status | Proves |
|---|---|---|---|
| **.NET corpus runner** | C# | **Shipped** | The reference implementation is correct |
| **Schema gate** | Python | **Shipped** | The published schema agrees with the corpus. Uses no .NET, so it fails the way a third party would |
| **CEL language conformance** | *(borrowed)* | **Planned** | Expression semantics — **cel-spec's own suite**, not ours to write |
| **Expression binding vectors** | data | **Planned** | Type environment, unbindable values, marshalling back to strings |
| **SDK contributor contract** | doc | **Planned** | How an outside SDK declares conformance |

The last item may be worth more than any single SDK: it converts every planned
implementation from *work the maintainer must do* into *work someone else can do
verifiably*.

### Expressions

Adopt **CEL** (Common Expression Language) rather than defining a language.

- Normative reference: `cel-expr/cel-spec` at a pinned tag, Apache-2.0
- Language conformance: cel-spec's own suite — 2,456 tests
- .NET implementation: **Celly** (Apache-2.0, net8/9/10, zero dependencies, 100% of the
  official conformance suite). *Young and low-adoption — the bus-factor risk is real,
  mitigated by zero dependencies and by CEL being a standard, so implementations are
  swappable*
- **Spike completed — the design is verified, not assumed.** `VariableDecl(name, CelType)`
  builds the type environment from hints; `qty * price` evaluates to `37.5` with no
  conversion wrappers; `CelEnv.Check` reports type errors at authoring time with
  positions; runtime failures arrive as error *values* rather than exceptions, matching
  the degrade-to-stored-response rule; `EvalLimits` supplies bounded evaluation
- Type environment comes from `expectedDataType`: `number`/`currency` → `double`,
  `boolean` → `bool`, `date`/`time`/`datetime` → `timestamp`, `multichoice` →
  `list<string>`, everything else → `string`
- A value that will not bind is an **error, never a default** — the expression degrades
  to the stored response, and the response stays exactly as typed

The current engine is **CEL-flavoured, not CEL**: it uses JavaScript-style truthiness
where CEL requires `bool`. That must be resolved before the claim in §8 is accurate.

---

## 6. Test coverage

Four different questions, answered by four different kinds of test. An implementation
can score well on one and badly on another, so they are tracked separately.

| | Question | Answered by |
|---|---|---|
| **Completeness** | Does it do everything it claims? | Feature coverage against the declared profile |
| **Conformance** | Does it agree with the format? | Corpus gate + schema gate |
| **Correctness** | Does it give right answers? | Unit tests + coverage gate |
| **Robustness** | Does it survive bad input and stay fixed? | Edge cases, fuzzing, mutation, regression |

**Legend:** ✅ gated in CI · ⚠️ partial · ❌ none · — not applicable

### 6.1 By component

| Component | Complete | Conform | Correct | Robust | Evidence |
|---|:--:|:--:|:--:|:--:|---|
| **Core (C# SDK)** | ⚠️ | ✅ | ✅ | ⚠️ | 560 tests, **95% line-coverage gate**, 11 conformance methods over 35 fixtures |
| **CLI** | ⚠️ | ⚠️ | ⚠️ | ❌ | 104 test methods; coverage gate is a **ratchet, not a bar**; conformance only indirect via Core |
| **Desktop** | ⚠️ | ⚠️ | ⚠️ | ⚠️ | **689 tests, GUI included, now run** — issue #30 fixed. Still **excluded from the coverage gate** |
| **Accessibility** | ⚠️ | — | ✅ | ⚠️ | 54 methods in a **mandatory CI job**; two cases cite files that do not exist and silently skip; AT-SPI smoke test is manual, outside CI |
| **PDF rendering** | ⚠️ | ✅ | ⚠️ | ⚠️ | 31 tests; **PDF/A externally validated by veraPDF (144/144)** — the only third-party conformance check in the project |
| **Schema gate** | — | ✅ | ✅ | — | Language-neutral, runs in CI without .NET, so it fails the way a third party would |
| **Python SDK** | ❌ | ❌ | ❌ | ❌ | **No tests of any kind.** Implements a different schema |
| **Rust / Java / C++ stubs** | ❌ | ❌ | ❌ | ❌ | No test runners at all |
| **Web demo** *(planned)* | ❌ | ❌ | ❌ | ❌ | Must inherit the Python SDK's gates |
| **TypeScript SDK / extension** *(planned)* | ❌ | ❌ | ❌ | ❌ | Must be corpus-gated from the first commit |
| **Converters** *(planned)* | ❌ | ❌ | ❌ | ❌ | Round-trip tests needed: APR → fixed-width → APR |

### 6.2 Test kinds absent everywhere

These gaps apply to the whole project, not to any one component.

| Kind | Status | What it would catch |
|---|:--:|---|
| **Mutation testing** | ❌ broken | Issue #29 — Stryker.NET reports `Killed: 0`. **Until it runs, the 95% coverage figure is unvalidated**: it proves lines execute, not that assertions would notice if they broke |
| **Fuzzing** | ❌ | Parser crashes and hangs on hostile input — directly relevant to "safe to open untrusted files" |
| **Property-based testing** | ❌ | Round-trip invariants over generated documents, rather than the 35 hand-written cases |
| **Performance** | ❌ | The vision targets **1000+ prompts in under a second**. Largest fixture: 20 prompts. The claim is untested |
| **Cross-implementation differential** | ❌ | Two conformant SDKs disagreeing. Impossible today — only one exists |
| **Expression semantics** | ❌ | The largest named interop risk (§8.4), with nothing testing it |

### 6.3 The machine-readable registry

`tests/registry.json` is the authoritative record; §6.1 and §6.4 are views of it.

It holds three lists: **suites** (each component's four-dimension scores), **testKinds**
(what kinds of testing exist project-wide), and **requirements** (one entry per normative
rule, with the fixtures and tests that gate it).

```bash
python3 scripts/check-test-registry.py          # human-readable
python3 scripts/check-test-registry.py --json   # machine-readable
```

The verifier runs in CI and **fails the build** on drift. It checks that:

- every fixture the registry names exists on disk
- every test method it names exists in source
- every fixture on disk is claimed by some requirement — unclaimed fixtures are
  invisible coverage
- every specification section carrying a normative **MUST** has a registry entry
- anything marked `gated` actually names a gate, and anything not gated records *why*

That last pair is what makes the registry worth keeping. A coverage document nobody
checks becomes a description of a project that no longer exists; this one cannot claim
coverage it does not have, and cannot silently omit a rule the spec added.

It found seven of its own gaps on first run.

### 6.4 How to build a suite from the spec

The method, repeatable after any spec change:

1. **Extract** every emphasised `MUST` / `MUST NOT` / `REQUIRED` from
   `docs/APR_SPECIFICATION.md`, grouped by section.
2. **Name** each as a requirement — `REQ-<section>-<slug>`.
3. **Choose the cheapest gate that would fail if the rule broke**, in this order:
   a **fixture** (data, language-neutral, runs in every SDK) → a **test** (behaviour a
   fixture cannot express, like ordering or byte-exactness) → an **external suite**
   (veraPDF, cel-spec — borrowed, not maintained) → **none**, with the gap recorded.
4. **Prefer fixtures.** A fixture gates every implementation in every language; a C#
   test gates one.
5. **Register it**, and let the verifier reject the entry if the gate does not exist.

Step 3's ordering is the load-bearing part. It is why the corpus grew from 5 fixtures to
36 while the .NET-only test count grew far less.

### 6.5 Spec alignment audit

Normative statements in the specification: **79** MUST / MUST NOT / REQUIRED clauses
across 32 sections. Spec areas with a **direct gate** (fixture or test that fails if the
rule is broken): **21 of 24**.

| Gated | Not gated |
|---|---|
| §1.3.1 version compatibility (both directions) · §3.2 strings-only · §3.3 any string valid · §3.4 / §7.3.1 hints never rewrite · §4.1–4.4 required fields · §4.5 tables · §4.6 depth floor · §4.7 unknown hint degrades · §4.8 unknown members preserved · §4.8.1 retired dropped · §4.9 canonical forms · §5 documentType precedence · §6.1 error list · §6.3 parse vs validate · §7.1–7.2 text handling · §7.3.2 submissionUrl · §9.2–9.3 canonical bytes · §9.4 tamper · §9.5 never gate data · §10.2 ordering | **§8.x expression semantics** — nothing · **§10.1 accessible name / label rules** — the accessibility suite tests the desktop app, not the rule as a format requirement · **§11 resource bounds on untrusted input** — no fuzzing, no depth-boundary test |

Method: extract every emphasised MUST/REQUIRED from the spec, group by section, and
check each area for a fixture or test that would fail if the rule were violated.
Re-run it after any spec change.

### 6.3 What this says

Three honest conclusions.

**Conformance is the strongest dimension and correctness is second** — which is the right
order for a format project. The corpus, the schema gate, and the canonicalization vectors
are real gates that a third party can run.

**Robustness is the weakest across the board.** No fuzzing, no property tests, no
performance measurement, and mutation testing broken — so the headline coverage number
means less than it appears. For a format whose selling point is *safe to open files from
untrusted senders*, the absence of fuzzing is the most conspicuous gap.

**Completeness is nowhere measured directly.** No component maps its tests to the spec
sections or profile features it claims to implement. That is why §7's corpus gaps went
unnoticed: `documentType`-vs-extension is a normative rule with no fixture, and nothing
in the build reports its absence.

---

## 7. Example files

Distinct from the corpus. The corpus pins rules for implementers; examples show humans
what forms look like. Both jobs matter; neither substitutes.

**Shipped:** 10 templates — `field-types-showcase` (109 prompts, 22 type hints),
`sf-86-background-check` (111 prompts), `irs-form-990` (78 prompts), plus 7 everyday
office forms.

**Gaps:**

| Missing | Why it matters |
|---|---|
| **Any `.aprf` at all** | 0 of 10 examples are filled. A visitor never sees half the format |
| **A signed example** | Signing shipped in v0.6.0; nothing demonstrates it |
| **Working README paths** | Quick-start cites `examples/simple-contact-form.apr` and `examples/employment-application.apr` — both live in `tests/Fixtures/`. Copy-paste fails. The same stale paths silently skip two accessibility tests |

### Corpus gaps

| Missing | Why it matters |
|---|---|
| **Expression semantics** | §8.4 names expression divergence the largest interop risk — and ships nothing to test against. Only 5 prompts repo-wide use `expr*`. **The single largest remaining gap** |
| ~~`documentType` vs extension~~ | **Closed.** `valid/documenttype-beats-extension.aprt` — a filled form named `.aprt`. The rule was spec-only until now; `FileService` still inferred type from the filename on both load and save |
| **Large document** | Vision targets 1000+ prompts under 1s; the largest fixture has 20 |
| **Separate id namespaces** | §4.4 permits a section and prompt sharing an id. No fixture does, so a shared-namespace implementation passes everything |
| **Filler signature** | `SignFields` and multi-party signing are uncovered |

---

## 8. Won't build

Recorded so they are not relitigated.

| | Why |
|---|---|
| **Second CLI** | One must do PDF and CMS; a partial second is not a CLI |
| **COBOL SDK** | Copybook generator and fixed-width converter are what that audience uses |
| **Native mobile** | The accessible TypeScript renderer reaches phones, tablets, and Chromebooks on a stronger accessibility stack |
| **Electron desktop** | Its draw is one renderer everywhere; a TS renderer served locally achieves that at zero shipping weight |
| **Second PDF engine** | One is enough |
| **Own expression language** | CEL exists, is versioned, and ships a conformance suite |

---

## 9. Priority

Ordered by how much each unblocks.

1. **TypeScript renderer + SDK** — unblocks the extension, improves the demo, and is the most accessible surface available
2. **Python SDK rebuilt** against the corpus — unblocks the web demo, and replaces an implementation that currently contradicts the format
3. **Web demo** on that SDK; retire `aprt-server.py`
4. **Browser extension**
5. **CEL adoption** + expression binding vectors — closes the largest named interop risk
6. **SDK contributor contract** — converts the remaining list into work others can do
7. **Converters** (copybook, fixed-width, relational), then **Java SDK**
8. **Corpus and example gaps** from §7 — cheap, and two of them hide real holes
9. **Test-kind gaps** from §6.2, in this order:
   - **Fix mutation testing** (issue #29) — until it runs, the 95% coverage gate is unvalidated, and every other testing decision is made on a number nobody has checked
   - **Fuzz the parser** — "safe to open untrusted files" is a security claim currently backed by 4 hand-written malformed fixtures
   - **A performance fixture** — turns the 1000-prompt claim into a measurement
   - **Raise the CLI coverage gate** from a ratchet to a bar, and bring Desktop into a gate at all

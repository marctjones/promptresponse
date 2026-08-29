# Draft: APR CEL Binding v1

**Status:** experimental design note for #112. It is not part of the APR
specification, does not change the wire format, and is not a conformance claim.

## Purpose

APR CEL Binding v1 makes the existing advisory `expr*` hints portable across the
.NET, Python, TypeScript, and Java SDKs. It lets a form react to values already
in that form without granting document-provided code authority over a person's
stored response.

The authoring experience is ordinary CEL over form values:

```cel
quantity * unitPrice
_this == confirmEmail ? '' : 'Email addresses must match'
('amount' in responses) && responses['amount'] > 1000.0
```

Kubernetes CEL is an implementation inspiration only: APR adopts its explicit
input declarations, versioned language surface, author-time checks, and bounded
evaluation. APR does **not** adopt admission decisions, API-object schemas,
`oldSelf`, Kubernetes extension libraries, or policy enforcement.

## Invariants

1. Stored responses remain authoritative. CEL never rejects, rewrites, or makes
   a response invalid.
2. Evaluation is pure. There is no filesystem, network, process, clock,
   randomness, reflection, environment, or document-mutation access.
3. A failure is data-preserving. It produces a diagnostic and the hint-specific
   fallback below; it never throws into a filling workflow.
4. All supported SDKs must agree on the experimental decision corpus before this
binding can become normative.

## Terminology

APR already uses **profile** for an optional conformance capability such as
`core+expressions` and for desktop capability-profile presets. This document is
not a third kind of profile. A **binding** defines how a `core+expressions`
implementation supplies APR values to CEL, which CEL surface it enables, and how
it turns CEL outcomes back into advisory APR behavior.

## Activation

Every expression receives only this read-only activation.

| Name | CEL type | Meaning |
|---|---|---|
| direct prompt ID | prompt's declared APR CEL type | Convenience binding for a prompt ID that is a valid, non-reserved CEL identifier. |
| `_this` | current prompt's declared APR CEL type | The response of the prompt that owns this hint. |
| `responses` | `map<string, dyn>` | Every successfully bound prompt response, keyed by its exact APR ID. |
| `_today` | `timestamp` | A single host-supplied UTC midnight timestamp for the evaluation pass. |

Direct bindings are the normal authoring path. `responses` is the escape hatch
for IDs such as `unit-price`, reserved names, and data-driven lookups. Reserved
names are `_this`, `_today`, `responses`, and `ctx`.

The base profile is form-only. It has no host context dictionary, user identity,
organization data, device data, environment variables, or remote data. `_today`
is the one standard evaluation input: it is supplied once by the caller rather
than read from the operating system by CEL. A future, separately named host-context
extension may add explicitly documented read-only data, but it must never place
credentials, secrets, authorization decisions, private server facts, or changing
ambient state in an APR expression.

## APR-to-CEL binding

| `expectedDataType` | CEL type | Binding rule |
|---|---|---|
| `number`, `currency`, `range` | `double` | Non-blank finite number. |
| `boolean` | `bool` | APR's accepted boolean spellings. |
| `date`, `time`, `datetime` | `timestamp` | Canonical APR temporal value; date is UTC midnight and time uses `1970-01-01`. |
| `multichoice` | `list<string>` | Canonical newline-separated values. |
| other or absent | `string` | The stored response, including `""`. |

A blank or unparseable typed response is **unbound**, not a default value and not
`null`. It is absent from `responses` and has no runtime direct binding. A direct
reference therefore fails only if it is evaluated; CEL short-circuiting remains
usable. This is intentional:

```cel
rush && amount > 1000.0
```

does not need a valid `amount` when `rush` is false.

## Hint contracts and safe fallback

| Hint | Required successful result | Fallback on any failure |
|---|---|---|
| `exprHidden` | `bool` | `false` — show the prompt. |
| `exprExpected` | `bool` | `false` — do not mark expected. |
| `exprReadOnly` | `bool` | `false` — keep editable. |
| `exprValidation` | `string` | `""` — no expression advisory. |
| `exprValue` | target prompt's APR CEL type | Do not write; retain stored response exactly. |

`exprValue` writes the normal APR canonical string form only after a successful
result. It marks `responseMetadata.source` as `"computed"`. A human or API write
clears that marker, and recomputation MUST NOT overwrite a non-empty response
without the marker.

## Evaluation statuses and diagnostics

The experimental SDK API returns one of:

`success`, `notConfigured`, `compileError`, `runtimeError`, or
`resourceExhausted`.

Authoring tools treat `compileError`, impossible result type, dependency cycle,
and statically excessive complexity as publication errors. Filling tools surface
the diagnostic non-blockingly and apply the safe fallback. SDK diagnostics should
include status, message, source span when available, prompt ID, and expression
hint name; messages are informational and not stable API identifiers.

## Library surface and limits

The eventual profile must name an exact CEL language/library version and test it
in all SDKs. The experiment begins with CEL core operators, literals, conversions,
timestamps/durations, collection operations, and standard macros (`has`, `all`,
`exists`, `exists_one`, `map`, `filter`). There are no custom functions.

`matches()` is intentionally outside the first portable profile until all SDKs
can implement one bounded, compatible regex dialect. Existing `validationPattern`
remains advisory and separate.

The decision corpus will test the following proposed guardrails:

- source at most 8 KiB UTF-8, AST depth at most 32, and at most 256 AST nodes;
- at most 64 direct prompt references;
- bounded evaluation per expression and per recomputation pass;
- bounded collection and string work before `all`, `exists`, `map`, or `filter`;
- abort yields `resourceExhausted`, never partial mutation.

The exact cost accounting is not yet normative. Kubernetes' deterministic static
cost estimate plus runtime budget is the model; APR will adopt exact limits only
after every target runtime can conform to them.

## Computed dependencies

An authoring tool extracts direct prompt references from `exprValue` expressions
and topologically orders computed prompts. A self-reference or dependency cycle
is an authoring error. Dynamic `responses[...]` reads see the pre-pass snapshot
and do not create inferred dependencies. This supports predictable subtotal → tax
→ total chains without retry-until-stable behavior.

## Open design: tables

The base activation deliberately does not yet expose table rows as a CEL value.
Direct prompt bindings can calculate a known fixed row, but they cannot naturally
aggregate dynamic rows. Issue #129 owns a separate prototype for a form-only table
view. It must provide stable, typed row/column access without making row labels,
row positions, or the optional `rowId.columnId` naming convention silently
normative. If aggregation requires helpers beyond standard CEL, they must be a
small, pure, versioned APR library and tested in every SDK.

## Prototype exit criteria

The decision corpus must establish identical observable outcomes for typed
binding, missing values, short-circuiting, arbitrary IDs, result-type failures,
all hint fallbacks, context, temporal values, provenance, dependency cycles, and
resource exhaustion in Python, TypeScript, Java, and .NET. Only then may #118
move this document's decisions into the normative specification and conformance
corpus.

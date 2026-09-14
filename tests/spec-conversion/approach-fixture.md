# Approach Fixture

**Status:** a fixture for scripts/check-spec-approach.py, not a specification. It
satisfies every approach check; the self-test mutates it one check at a time.

## 1. Conventions {#conventions}

A requirement names the class of product it binds, and an example shows it.
Informative text here may use ordinary words such as must and should.

## 2. Terminology {#terminology}

**reader** — software that parses a document.

**writer** — software that produces a document.

## 3. Documents {#documents}

A reader **MUST** keep member order. [APR-TEST-001]

A writer **SHOULD** emit members in a stable order. [APR-TEST-002]

A reader **MAY** report a warning for an empty title. [APR-TEST-003]

### 3.1 Members {#members}

| Member | Requirement | Rule |
| --- | --- | --- |
| `title` | REQUIRED | APR-TEST-004 |
| `note` | OPTIONAL | APR-TEST-005 |

> Rationale: order is kept because a person reads the form top to bottom, and
> a writer that must reorder would surprise them.

**Example 3.1-1.** Member order is kept.

```apr-example
id: fixture-order
rules: APR-TEST-001
expect: valid
---
{"aprVersion": "1.0-beta.6"}
```

## 4. Normative references {#normative-references}

YAML 1.1, the legacy YAML specification, is cited only by name.

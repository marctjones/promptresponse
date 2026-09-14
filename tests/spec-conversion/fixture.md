# Fixture Specification

**Status:** a fixture for scripts/spec-units.py, not a specification.

## 1. First chapter {#first}

Alpha paragraph opens the chapter. It has two sentences.

Beta paragraph follows, e.g. with an abbreviation. Inline `a. B` code stays whole. See [the link. Text](#first) for more.

A reader **MUST** keep this. A writer must not do that, and one word changes here. [APR-TEST-001] A trailing sentence follows the tag.

An earlier draft said something else.

### 1.1 Lists and tables {#lists}

An implementation:

- **MUST** do the first thing; and
  continues on a second line.
- [ ] a checklist item.
1. A numbered item.

| Member | Requirement | Rule |
| --- | --- | --- |
| `title` | REQUIRED | APR-TEST-002 |
| `note` | OPTIONAL | APR-TEST-003 |

> Rationale: explanation that carries no keyword.

> A plain quotation.

```apr-example
id: fixture-example
rules: APR-TEST-001
expect: valid
---
{"aprVersion": "1.0-beta.6"}
```

```jsonc
{ "just": "code" }
```

Repeated sentence.

Repeated sentence.

# APR specification review and release checklist

Every published APR specification baseline is reviewed against this checklist,
and the completed review is committed with the release. A baseline released
without one has no auditable record that anybody read it.

The checklist has two halves. The **automated** half is run, not judged: if a
gate fails the review stops. The **human** half is the part no script can do.

A failed or incomplete review is not a conformance result, and **MUST NOT** be
presented as one.

---

## 1. Classify the change first

Before reviewing anything, say which kind of change this is. The rest of the
checklist depends on the answer.

| Class | Meaning | Consequence |
| --- | --- | --- |
| **Editorial** | Wording, structure, examples, rationale. A conforming implementation stays conforming. | No format version change. |
| **Behavioural** | A rule changes, is added, or is removed. An implementation may stop conforming. | Format version changes, and every implementation is re-verified. |

State the class in the release record. If a reviewer disagrees with the
classification, that disagreement is resolved before release, not after.

> A change is behavioural if you cannot describe it without naming a rule
> identifier. That is a useful test, not a definition.

---

## 2. Automated gates — all must pass

Run these and record the output. They are ordered so the cheapest failure
surfaces first.

- [ ] `python3 scripts/check-spec-shape.py` — the document has its declared shape
- [ ] `python3 scripts/check-spec-completeness.py` — concepts, schema members and profiles are covered
- [ ] `python3 scripts/check-schema-agrees.py` — schema and type registry match the member tables
- [ ] `python3 scripts/extract-spec-examples.py` — the corpus still matches the specification
- [ ] `python3 scripts/check-test-registry.py` — per-rule coverage, fixtures, tests and suites resolve
- [ ] `python3 scripts/check-docs.py` — references and the authority ordering hold
- [ ] `python3 scripts/check-schema.py` — corpus and examples validate against the schema
- [ ] Every implementation suite passes: .NET, Python, TypeScript, Java, and the web demo
- [ ] The executable examples pass in **all four** implementations, not only the reference one

## 3. Language and structure

- [ ] Every normative clause uses a BCP 14 keyword in all capitals, and nothing else reads as a requirement
- [ ] No requirement sits inside a `Rationale:` block — deleting every rationale block must leave the requirements unchanged
- [ ] Every new or changed rule carries a rule identifier, and no identifier was reused or renumbered
- [ ] Every retired rule's identifier is retired, not recycled
- [ ] New terms are defined in the terminology section, once, and not redefined inline
- [ ] Cross-references cite anchors, never section numbers

## 4. Rules and their evidence

- [ ] Every new rule is either gated by an executable example or a test, or recorded in the registry as `partial` with a named gap
- [ ] No rule is claimed to be gated by a test that does not exercise it
- [ ] Every new example names the anchor it demonstrates and, when it expects rejection, the diagnostic
- [ ] The corpus-gaps appendix lists what is genuinely still unexercised, and nothing that now is

## 5. What the specification says about itself

- [ ] Legal and illegal behaviour are both stated — a rule that says what is allowed and not what happens otherwise is incomplete
- [ ] Member tables carry requiredness, type and domain for every member
- [ ] Conformance profiles each have a checklist an implementer can work through
- [ ] Compatibility behaviour is stated for unknown members, retired members and an unrecognised version
- [ ] Security considerations cover what the format guarantees **and what it does not**

## 6. Judgement — the part no gate covers

- [ ] Could an implementer build a conforming reader from this text alone, without reading the code?
- [ ] Does any section contradict another? Pay particular attention to rules stated in two places
- [ ] Is anything stated twice? Two copies of one fact will eventually disagree
- [ ] Does every `Decision (beta.6):` block still reflect a decision someone made deliberately, rather than one inherited by accident?
- [ ] Are the open questions honest — is anything listed as settled that is not, or as open that now is?

The opt-in local review (`scripts/run-spec-semantic-review.py`) assists here. It
is **evidence for a reviewer, never a reviewer**: its output is recorded with the
review and never substitutes for a human reading the text.

## 7. Release record

- [ ] Change class from §1 recorded, with reasoning
- [ ] Output of every gate in §2 recorded
- [ ] Reviewer named, and the date
- [ ] `python3 scripts/build-spec.py --write` run, and the release manifest committed
- [ ] Tag identifies the exact specification, schema, type registry, registry and corpus set

---

## What this checklist deliberately does not do

**It cannot ratify.** Completing it produces a reviewed beta baseline, nothing
more. A beta remains revisable, and **only an explicit human decision** changes
freeze status or applies a 1.0 designation. No checklist item, no passing gate,
and no model review authorises either.

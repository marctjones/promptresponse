# PromptResponse documentation

Read documents by the question you need answered:

| Need | Authoritative document |
| --- | --- |
| What product are we building and why? | [Product](PRODUCT.md) |
| What is planned now? | [Roadmap](../ROADMAP.md) |
| How do the parts fit together? | [Architecture](ARCHITECTURE.md) |
| How should the experience look and work? | [UX and accessibility](UX_ACCESSIBILITY.md) |
| What does an APR document mean? | [APR specification](APR_SPECIFICATION.md) |
| Can the APR format change? | [Specification freeze](SPECIFICATION_FREEZE.md) |
| How does another implementation conform? | [SDK conformance](SDK_CONFORMANCE.md) |
| Where is a concept implemented and tested? | [Concept registry](CONCEPT_REGISTRY.md) |
| Which applications and SDKs ship? | [Implementation registry](IMPLEMENTATION_REGISTRY.md) |
| How do I use it? | [User guide](USER_GUIDE.md) |
| How do I contribute? | [Development guide](DEVELOPMENT.md) |
| How are releases built and verified? | [Release documentation](release/) |

## Authority

For APR semantics, the **[APR specification](APR_SPECIFICATION.md) is
normative**. The JSON Schema, the type registry, and the conformance corpus are
**derived projections** of it: where one disagrees with the specification, the
derived artifact has the defect.

This is how CommonMark and YAML are organised — one normative document, with
machine-readable artifacts generated from or subordinate to it. It replaces an
earlier ordering that put the corpus first, which suited a specification that
merely described what had been built.

Source code and tests establish what a shipped implementation does, and an
implementation that departs from the specification has a defect. The registries
connect those facts without redefining them. Guides explain use and must not
invent product behavior.

Historical plans and superseded specifications live in Git history, not the active
documentation tree.

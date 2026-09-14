# APR 1.0-beta.6 Implementation Conformance Statement

Generated from [the OSCAL rule catalog](apr-oscal-catalog.json) by
`scripts/build-conformance-statement.py`. Do not edit; regenerate it.

The [specification](../APR_SPECIFICATION.md) is normative, and every row here
links to the rule it lists. Rules are grouped by the conformance profile that
defines them and by the class of product each is a requirement on. A claim of a
profile covers every row under it, and a claim of an optional profile also covers
`core`. Documents lists requirements on a form or record, which a validator or
reader checks.

To state conformance, copy this file and fill in the last column of each row a
claim covers: **Yes**, **No**, or **N/A** where the implementation is not that
class of product.

## `core`

232 rules.

### Implementation

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-CONF-004](../APR_SPECIFICATION.md#declaring-conformance) | MUST NOT | An implementation **MUST NOT** claim a profile without passing the corpus revision it names. | |
| [APR-CONF-005](../APR_SPECIFICATION.md#profile-core) | MUST NOT | An implementation that does not claim `core+attestations` **MUST NOT** report a document as verified. | |
| [APR-CONF-006](../APR_SPECIFICATION.md#conformance) | REQUIRED | `core` — [About this document](../APR_SPECIFICATION.md#scope), [Conformance profiles](../APR_SPECIFICATION.md#conformance), [Representations](../APR_SPECIFICATION.md#representations) through [Text handling](../APR_SPECIFICATION.md#text-handling), [Rendering](../APR_SPECIFICATION.md#renderers), and [Security considerations](../APR_SPECIFICATION.md#security) — **REQUIRED** | |
| [APR-CONF-007](../APR_SPECIFICATION.md#conformance) | OPTIONAL | `core+streams` — [`core+streams`](../APR_SPECIFICATION.md#profile-streams), [Streams](../APR_SPECIFICATION.md#streams), and [Semantic digests](../APR_SPECIFICATION.md#digests) — **OPTIONAL** | |
| [APR-CONF-008](../APR_SPECIFICATION.md#conformance) | OPTIONAL | `core+attestations` — [`core+attestations`](../APR_SPECIFICATION.md#profile-attestations) and [Attestations](../APR_SPECIFICATION.md#attestations) — **OPTIONAL** | |
| [APR-CONF-009](../APR_SPECIFICATION.md#conformance) | OPTIONAL | `core+expressions` — [`core+expressions`](../APR_SPECIFICATION.md#profile-expressions) and [Expressions](../APR_SPECIFICATION.md#expressions) — **OPTIONAL** | |
| [APR-CONF-010](../APR_SPECIFICATION.md#conformance) | MUST | An implementation that claims `core+attestations` **MUST** also claim `core+streams`. | |
| [APR-CONF-012](../APR_SPECIFICATION.md#profile-core) | SHOULD | An implementation that does not claim `core+attestations` **SHOULD** indicate that attestations are present but unchecked. | |
| [APR-CONF-014](../APR_SPECIFICATION.md#declaring-conformance) | MUST | An implementation **MUST** state, in its conformance claim, the profiles it claims, the submission transports it implements (`https`, `mailto`, both, or none — [Submission targets](../APR_SPECIFICATION.md#submission)), and the corpus revision it passes. | |
| [APR-MODEL-003](../APR_SPECIFICATION.md#hints-advisory) | MUST NOT | An implementation **MUST NOT** reject, alter, truncate, or refuse to save a response because of a hint, including `validationPattern` and every member of the `expr*` family. | |
| [APR-MODEL-015](../APR_SPECIFICATION.md#nesting) | MUST | An implementation **MUST** support at least **16 levels** of section nesting. | |
| [APR-MODEL-033](../APR_SPECIFICATION.md#submission) | MUST | Send the document as the body of one `PUT`, with the `vnd.apr` media type of its representation as `Content-Type` ([Document type](../APR_SPECIFICATION.md#media-types)) — **MUST** | |
| [APR-MODEL-034](../APR_SPECIFICATION.md#submission) | SHOULD | An implementation submitting to a `mailto` entry **SHOULD** hand the composition to the user's mail program. | |
| [APR-MODEL-035](../APR_SPECIFICATION.md#submission) | MUST NOT | An implementation **MUST NOT** act on an entry whose scheme it does not recognise or does not implement. | |
| [APR-MODEL-086](../APR_SPECIFICATION.md#submission) | MUST NOT | Send credentials, cookies, or headers derived from the document — **MUST NOT** | |
| [APR-MODEL-087](../APR_SPECIFICATION.md#submission) | MUST | Treat any status other than 2xx as failure — **MUST** | |
| [APR-MODEL-088](../APR_SPECIFICATION.md#submission) | MUST NOT | Follow a redirect — **MUST NOT** | |
| [APR-MODEL-089](../APR_SPECIFICATION.md#submission) | MUST NOT | Retry without a fresh user action — **MUST NOT** | |
| [APR-MODEL-090](../APR_SPECIFICATION.md#submission) | MAY | An implementation submitting to a `mailto` entry **MAY** send the message itself instead. | |
| [APR-MODEL-127](../APR_SPECIFICATION.md#any-string) | MUST | An implementation **MUST** support a `prompt.response` of at least **1 MiB** (1,048,576 bytes) encoded as UTF-8. | |
| [APR-REP-001](../APR_SPECIFICATION.md#model-layers) | MUST | An implementation that reads a document and writes it back **MUST** preserve every part of its semantic model, including members APR does not define. | |
| [APR-SEC-009](../APR_SPECIFICATION.md#security) | MUST NOT | An implementation **MUST NOT** execute anything a document carries. | |
| [APR-SEC-016](../APR_SPECIFICATION.md#security) | MUST NOT | An implementation **MUST NOT** contact a `submissionUrls` entry without an explicit user action. | |
| [APR-SEC-017](../APR_SPECIFICATION.md#security) | MUST NOT | An implementation **MUST NOT** contact a certificate endpoint without an explicit user action. | |
| [APR-TEXT-001](../APR_SPECIFICATION.md#text-responses) | MUST NOT | An implementation **MUST NOT** normalize, strip, or otherwise rewrite a response when it reads or writes one. | |
| [APR-TEXT-005](../APR_SPECIFICATION.md#authoring-strictness) | MAY | An implementation **MAY** hold authoring members to rules stricter than this document states. | |
| [APR-TEXT-006](../APR_SPECIFICATION.md#authoring-strictness) | MUST NOT | An implementation **MUST NOT** Unicode-normalize authoring data or remove a code point from it. | |
| [APR-TEXT-008](../APR_SPECIFICATION.md#authoring-strictness) | MUST NOT | An implementation **MUST NOT** produce an attestation over a form whose `submissionUrls` has such an entry. | |
| [APR-VAL-002](../APR_SPECIFICATION.md#warnings) | MUST NOT | An implementation **MUST NOT** report a warning as the document being invalid. | |
| [APR-VAL-034](../APR_SPECIFICATION.md#warnings) | MAY | An implementation **MAY** report a warning for a condition the table does not name, such as a blank response a workflow treats as required. | |

### Reader

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-CONF-001](../APR_SPECIFICATION.md#profile-core) | MUST | A reader that does not claim `core+streams`, given a stream, **MUST** report `APR_STREAM_REQUIRES_ITERATION` rather than select a record by position. | |
| [APR-CONF-003](../APR_SPECIFICATION.md#profile-core) | MUST NOT | A reader that does not claim `core+expressions` **MUST NOT** reject a document because it uses expressions. | |
| [APR-MODEL-001](../APR_SPECIFICATION.md#responses) | MUST | A reader **MUST** reject, at parse time, a `prompt.response` that is a JSON number, boolean, array, or object. | |
| [APR-MODEL-020](../APR_SPECIFICATION.md#extensions) | MUST | A reader **MUST** ignore members it does not recognise, at every level, and **MUST NOT** reject a document for carrying them. | |
| [APR-MODEL-021](../APR_SPECIFICATION.md#extensions) | MUST | A reader **MUST** also **preserve** them: an unrecognised member present on read **MUST** still be present, unchanged, on write. | |
| [APR-MODEL-023](../APR_SPECIFICATION.md#canonical-values) | MUST | A reader **MUST** read every form the table lists, canonical or accepted on read, as the value it spells. | |
| [APR-MODEL-025](../APR_SPECIFICATION.md#roles) | MUST | A reader **MUST** present a field whose role it does not recognise as it presents any other field, without an error. | |
| [APR-MODEL-027](../APR_SPECIFICATION.md#roles) | MUST NOT | A reader **MUST NOT** refuse input to a field because of its role. | |
| [APR-MODEL-028](../APR_SPECIFICATION.md#roles) | SHOULD | Where a form declares roles, a reader **SHOULD** let the person say which role they are filling. | |
| [APR-MODEL-038](../APR_SPECIFICATION.md#tables) | MUST NOT | A reader **MUST NOT** treat a section as a table unless it carries `kind: "table"`, whatever its `maxRows`, `canAddRows`, or child sections. | |
| [APR-MODEL-039](../APR_SPECIFICATION.md#hints-object) | MUST | A reader **MUST** preserve a hint that is unrecognised, unsupported, or malformed. | |
| [APR-MODEL-044](../APR_SPECIFICATION.md#regarding) | MUST NOT | A reader **MUST NOT** present a reference as a revision, a supersession, a chronology, an authority, or a trust relationship. | |
| [APR-MODEL-045](../APR_SPECIFICATION.md#regarding) | MUST NOT | A reader **MUST NOT** reject a document for an unresolved reference, report it as damaged, or withhold its data. | |
| [APR-MODEL-049](../APR_SPECIFICATION.md#responses) | MUST NOT | A reader **MUST NOT** coerce such a response into a string: `42` does not become `"42"`, nor `true` `"true"`. | |
| [APR-MODEL-050](../APR_SPECIFICATION.md#roles) | MUST | A reader **MUST** show a role's `id` where the role has no `name`. | |
| [APR-MODEL-082](../APR_SPECIFICATION.md#metadata) | MUST NOT | A reader **MUST NOT** fetch a `templateId`. | |
| [APR-MODEL-083](../APR_SPECIFICATION.md#metadata) | MUST | A reader **MUST** take the language of a section or prompt that declares none from the nearest enclosing section that declares one, and otherwise from `metadata.language`. | |
| [APR-MODEL-097](../APR_SPECIFICATION.md#tables) | MUST | A reader **MUST** preserve a `maxRows` or `canAddRows` member on a section that is not a table. | |
| [APR-MODEL-098](../APR_SPECIFICATION.md#table-rows) | MUST | A reader **MUST** treat a table without `canAddRows` as fixed. | |
| [APR-MODEL-118](../APR_SPECIFICATION.md#extensions) | MUST | A reader **MUST** compare member names case-sensitively, so a member whose name matches a defined member only when case is ignored is an unknown member. | |
| [APR-MODEL-120](../APR_SPECIFICATION.md#canonical-values) | MUST | A reader **MUST** read an empty response to a prompt of any type above as no selection. | |
| [APR-MODEL-121](../APR_SPECIFICATION.md#roles) | MUST | A reader **MUST** take a prompt's role from the prompt where it carries one, and otherwise from the section containing it. | |
| [APR-MODEL-122](../APR_SPECIFICATION.md#roles) | MUST | A reader **MUST** show the identifier of a role no entry declares, without an error. | |
| [APR-MODEL-125](../APR_SPECIFICATION.md#roles) | SHOULD | A reader **SHOULD** mark which fields belong to the person's role, leaving the fields of other roles visible and editable. | |
| [APR-MODEL-126](../APR_SPECIFICATION.md#roles) | SHOULD | A reader **SHOULD** make a field's role available to assistive technology. | |
| [APR-REP-003](../APR_SPECIFICATION.md#encoding) | MUST | A reader **MUST** reject ill-formed UTF-8 rather than substituting replacement characters. | |
| [APR-REP-006](../APR_SPECIFICATION.md#apr-jsonc) | MUST | A reader **MUST** reject an APR-JSONC object that has two members of the same name, reporting `DUPLICATE_MEMBER`. | |
| [APR-REP-008](../APR_SPECIFICATION.md#yaml-resolution) | MUST | Any other plain scalar — a string — **MUST** | |
| [APR-REP-011](../APR_SPECIFICATION.md#yaml-resolution) | MUST | A reader **MUST** reject a document containing a plain scalar that denotes a non-finite float, reporting `YAML_NON_FINITE_NUMBER`. | |
| [APR-REP-012](../APR_SPECIFICATION.md#yaml-resolution) | MUST NOT | A reader **MUST NOT** resolve a scalar by a YAML library's default schema. | |
| [APR-REP-013](../APR_SPECIFICATION.md#apr-yaml) | MUST | A reader **MUST** use a safe loader: one that constructs only JSON values and never instantiates a host-language object from document content. | |
| [APR-REP-014](../APR_SPECIFICATION.md#json-subset) | MUST | A reader **MUST** reject a document in which a member other than `response` is `null`. | |
| [APR-REP-015](../APR_SPECIFICATION.md#json-subset) | MUST | A reader **MUST** reject a structural member of any other JSON type, reporting `WRONG_TYPE` ([Errors](../APR_SPECIFICATION.md#structural-validation)). | |
| [APR-REP-019](../APR_SPECIFICATION.md#encoding) | SHOULD | A reader **SHOULD** accept a document that begins with a byte-order mark. | |
| [APR-REP-024](../APR_SPECIFICATION.md#json-subset) | MUST | A reader **MUST** read a `null` or absent `response` as the empty string. | |
| [APR-REP-026](../APR_SPECIFICATION.md#yaml-resolution) | MUST | Any quoted scalar — a string, verbatim — **MUST** | |
| [APR-REP-027](../APR_SPECIFICATION.md#yaml-resolution) | MUST | Plain `null`, `Null`, `NULL`, `~`, or empty — null — **MUST** | |
| [APR-REP-028](../APR_SPECIFICATION.md#yaml-resolution) | MUST | Plain `true`, `True`, `TRUE`, `false`, `False`, `FALSE` — a boolean — **MUST** | |
| [APR-REP-029](../APR_SPECIFICATION.md#yaml-resolution) | MUST | A plain scalar matching JSON's `number` production (RFC 8259 §6) — a number — **MUST** | |
| [APR-SEC-002](../APR_SPECIFICATION.md#version-compatibility) | MUST | A reader **MUST** reject a record whose `aprVersion` is not exactly `"1.0-beta.6"`, reporting `UNSUPPORTED_VERSION`. | |
| [APR-SEC-005](../APR_SPECIFICATION.md#media-types) | MUST | A reader **MUST** determine whether a document is a template or a filled form from its `documentType` member alone. | |
| [APR-SEC-007](../APR_SPECIFICATION.md#media-types) | MUST NOT | A reader **MUST NOT** reject a document because its extension disagrees with its content. | |
| [APR-SEC-010](../APR_SPECIFICATION.md#security) | MUST NOT | A reader **MUST NOT** fetch anything when it reads a document. | |
| [APR-SEC-011](../APR_SPECIFICATION.md#security) | MUST | A reader **MUST** bound nesting depth. | |
| [APR-SEC-013](../APR_SPECIFICATION.md#media-types) | MUST NOT | A reader **MUST NOT** select APR behaviour from the generic `application/json`, `application/yaml`, or `application/json-seq` types. | |
| [APR-SEC-014](../APR_SPECIFICATION.md#media-types) | MUST NOT | A reader **MUST NOT** reject a document because its media type and its content disagree. | |
| [APR-SEC-015](../APR_SPECIFICATION.md#media-types) | SHOULD | A reader **SHOULD** warn when a file's extension and its `documentType` disagree, rather than silently honouring either. | |
| [APR-SEC-018](../APR_SPECIFICATION.md#security) | SHOULD | A reader **SHOULD** bound document size and stream length. | |
| [APR-SEC-019](../APR_SPECIFICATION.md#security) | MUST | A reader **MUST** refuse a document cleanly on reaching a bound it applies, rather than exhausting memory or crashing. | |
| [APR-SEC-020](../APR_SPECIFICATION.md#security) | MUST | A reader **MUST** terminate on every input. | |
| [APR-VAL-003](../APR_SPECIFICATION.md#parse-errors) | MUST NOT | A reader **MUST NOT** validate input it could not parse. | |
| [APR-VAL-004](../APR_SPECIFICATION.md#parse-errors) | MUST | A reader **MUST** read a document that parses, even when that document fails validation. | |
| [APR-VAL-010](../APR_SPECIFICATION.md#parse-errors) | MUST | A reader that reports a parse failure **MUST** report it under a parse-stage code: the code this document names for that condition where it names one — `DUPLICATE_MEMBER`, the `YAML_*` refusals, `APR_STREAM_MIXED_REPRESENTATIONS` — and `PARSE_ERROR` where it does not. | |

### Writer

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-CONF-013](../APR_SPECIFICATION.md#profile-core) | MUST | A writer that does not claim `core+expressions` **MUST** preserve expression strings across a round trip. | |
| [APR-MODEL-013](../APR_SPECIFICATION.md#table-no-layout) | MUST NOT | A writer **MUST NOT** add a member to a table that states width, alignment, colour, or font. | |
| [APR-MODEL-017](../APR_SPECIFICATION.md#nesting) | SHOULD NOT | A writer **SHOULD NOT** nest sections more than five levels deep. | |
| [APR-MODEL-031](../APR_SPECIFICATION.md#extensions) | MUST NOT | A writer **MUST NOT** add a member whose name carries no prefix. | |
| [APR-MODEL-037](../APR_SPECIFICATION.md#metadata) | MUST NOT | A writer **MUST NOT** add a member to a form to record when it was received, by whom, or what happened to it next. | |
| [APR-MODEL-040](../APR_SPECIFICATION.md#generated-ids) | MUST | A writer **MUST** preserve every valid id it read. | |
| [APR-MODEL-041](../APR_SPECIFICATION.md#generated-ids) | MUST | A writer repairing a document **MUST** give a blank or whitespace-only id an id generated by the steps above. | |
| [APR-MODEL-042](../APR_SPECIFICATION.md#generated-ids) | MUST NOT | A writer **MUST NOT** generate an id that encodes position, such as `q1`, `q2`, or `row_3`. | |
| [APR-MODEL-048](../APR_SPECIFICATION.md#table-assertion) | SHOULD | A writer **SHOULD** give a cell the id `{instanceId}.{columnId}`, which helps addressing and database import. | |
| [APR-MODEL-084](../APR_SPECIFICATION.md#metadata) | MUST NOT | A writer filling a form **MUST NOT** add or change a `language` member. | |
| [APR-MODEL-094](../APR_SPECIFICATION.md#prompt-object) | MUST NOT | A writer **MUST NOT** change an id when it reorders prompts. | |
| [APR-MODEL-095](../APR_SPECIFICATION.md#generated-ids) | MUST NOT | A writer **MUST NOT** invent or replace an id unless the caller explicitly asks it to repair the document. | |
| [APR-MODEL-096](../APR_SPECIFICATION.md#generated-ids) | MUST | A writer repairing a document **MUST** give both members sharing an id an id generated by the steps above, since neither has a better claim to the name. | |
| [APR-MODEL-119](../APR_SPECIFICATION.md#canonical-values) | SHOULD | A writer choosing a response **SHOULD** write it in the canonical write form the table below gives for the prompt's `expectedDataType`. | |
| [APR-REP-018](../APR_SPECIFICATION.md#encoding) | SHOULD NOT | A writer **SHOULD NOT** write a byte-order mark. | |
| [APR-REP-023](../APR_SPECIFICATION.md#json-subset) | MUST | A writer **MUST** emit each structural member in the JSON type its member table declares. | |
| [APR-REP-025](../APR_SPECIFICATION.md#json-subset) | MUST NOT | A writer **MUST NOT** emit `null`. | |
| [APR-SEC-006](../APR_SPECIFICATION.md#media-types) | SHOULD | A writer **SHOULD** give a file the extension that matches its `documentType`. | |
| [APR-SEC-012](../APR_SPECIFICATION.md#media-types) | MUST | A writer that labels APR content **MUST** use the type above for its representation. | |

### Validator

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-MODEL-011](../APR_SPECIFICATION.md#prompt-object) | MUST | A validator **MUST** compare ids by exact code-point equality, applying no normalization, case folding, or trimming. | |
| [APR-MODEL-018](../APR_SPECIFICATION.md#hints-object) | MUST NOT | A validator **MUST NOT** reject a form because its `expectedDataType` is not in the registry. | |
| [APR-MODEL-051](../APR_SPECIFICATION.md#roles) | MUST NOT | A validator **MUST NOT** reject a form whose `role` names a role that `roles` does not declare. | |
| [APR-MODEL-085](../APR_SPECIFICATION.md#metadata) | MUST NOT | A validator **MUST NOT** relax a check on human-facing text because of a `language` member. | |
| [APR-MODEL-091](../APR_SPECIFICATION.md#submission) | SHOULD | A validator **SHOULD** report an entry whose scheme this document does not define as `SUBMISSION_URL_UNSUPPORTED` ([Warnings](../APR_SPECIFICATION.md#warnings)). | |
| [APR-MODEL-099](../APR_SPECIFICATION.md#table-rows) | MUST NOT | A validator **MUST NOT** reject a table that carries more instances than its `maxRows`. | |
| [APR-MODEL-100](../APR_SPECIFICATION.md#table-ragged) | MUST NOT | A validator **MUST NOT** reject a table whose instances disagree. | |
| [APR-MODEL-116](../APR_SPECIFICATION.md#hints-object) | MUST NOT | A validator **MUST NOT** reject a form because a hint is unusable, such as a `validationPattern` that is not a regular expression, a bound of the wrong type, or an expression that does not compile. | |
| [APR-TEXT-004](../APR_SPECIFICATION.md#filled-never-rewritten) | MUST | A validator **MUST** report a warning for a response that contains a code point [Human-facing text](../APR_SPECIFICATION.md#human-text) excludes. | |
| [APR-TEXT-007](../APR_SPECIFICATION.md#authoring-strictness) | SHOULD | A validator **SHOULD** report a warning for a `submissionUrls` entry that contains a code point [Human-facing text](../APR_SPECIFICATION.md#human-text) excludes. | |
| [APR-TEXT-010](../APR_SPECIFICATION.md#authoring-strictness) | SHOULD | A validator **SHOULD** report a warning for an id that contains a character outside `[A-Za-z0-9_.-]`. | |
| [APR-TEXT-012](../APR_SPECIFICATION.md#human-text) | SHOULD | A validator **SHOULD** apply the confusable and mixed-script detection of UTS #39 to human-facing text and report what it finds. | |
| [APR-TEXT-014](../APR_SPECIFICATION.md#human-text) | MUST | A validator **MUST** report a warning for human-facing text that is not in Normalization Form C or that contains a code point the rule above excludes. | |
| [APR-VAL-005](../APR_SPECIFICATION.md#semantic-validation) | MUST NOT | A validator **MUST NOT** reject a document because of what a response means. | |
| [APR-VAL-007](../APR_SPECIFICATION.md#validation) | MUST NOT | A validator **MUST NOT** report a document as invalid for any reason the [Errors](../APR_SPECIFICATION.md#structural-validation) table does not list. | |
| [APR-VAL-011](../APR_SPECIFICATION.md#structural-validation) | MUST | `NULL_DOCUMENT` — No document. — **MUST** | |
| [APR-VAL-012](../APR_SPECIFICATION.md#structural-validation) | MUST | `REQUIRED_FIELD` — `aprVersion`, `metadata.title`, section `id` or `title`, prompt `id` or `label` blank; `metadata` or `sections` absent; `sections` empty; `templateId` absent on a filled form; a role entry without `id`; a member the attestation record table requires, absent. — **MUST** | |
| [APR-VAL-013](../APR_SPECIFICATION.md#structural-validation) | MUST | `UNSUPPORTED_VERSION` — `aprVersion` is not exactly `1.0-beta.6` ([Version compatibility](../APR_SPECIFICATION.md#version-compatibility)). — **MUST** | |
| [APR-VAL-014](../APR_SPECIFICATION.md#structural-validation) | MUST | `DUPLICATE_ID` — A section or prompt id repeats within its namespace. — **MUST** | |
| [APR-VAL-015](../APR_SPECIFICATION.md#structural-validation) | MUST | `EMPTY_SECTION` — A section has no prompts and no child sections. — **MUST** | |
| [APR-VAL-016](../APR_SPECIFICATION.md#structural-validation) | MUST | `EMPTY_TABLE` — A `kind: "table"` section has no child sections, so it has no instances ([Rows and instances](../APR_SPECIFICATION.md#table-rows)). — **MUST** | |
| [APR-VAL-017](../APR_SPECIFICATION.md#structural-validation) | MUST | `WRONG_TYPE` — A structural member is not the JSON type its member table declares ([Value types](../APR_SPECIFICATION.md#json-subset)); an attestation member outside what its row allows. — **MUST** | |
| [APR-VAL-018](../APR_SPECIFICATION.md#warnings) | MUST | `RESPONSE_CONTRADICTS_TYPE` — A response contradicts `expectedDataType`. — **MUST** | |
| [APR-VAL-019](../APR_SPECIFICATION.md#warnings) | MUST | `RESPONSE_PATTERN_MISMATCH` — A response does not match `validationPattern`. — **MUST** | |
| [APR-VAL-020](../APR_SPECIFICATION.md#warnings) | MUST | `RESPONSE_OUTSIDE_BOUNDS` — A response falls outside the bounds family ([Hints](../APR_SPECIFICATION.md#hints-object)). — **MUST** | |
| [APR-VAL-021](../APR_SPECIFICATION.md#warnings) | MUST | `RESPONSE_OUTSIDE_SUGGESTED_VALUES` — A response is not one of `suggestedValues`. — **MUST** | |
| [APR-VAL-022](../APR_SPECIFICATION.md#warnings) | MUST | `HINT_UNUSABLE` — A hint cannot be applied at all — a `validationPattern` that is not a valid regular expression, a bound that will not parse. — **MUST** | |
| [APR-VAL-023](../APR_SPECIFICATION.md#warnings) | MUST | `UNREGISTERED_DATA_TYPE` — `expectedDataType` names a type the registry does not carry ([Types are affordances](../APR_SPECIFICATION.md#data-types)). — **MUST** | |
| [APR-VAL-024](../APR_SPECIFICATION.md#warnings) | MUST | `UNDECLARED_ROLE` — A `role` names a role `roles` does not declare ([Roles](../APR_SPECIFICATION.md#roles)). — **MUST** | |
| [APR-VAL-025](../APR_SPECIFICATION.md#warnings) | MUST | `TABLE_RAGGED` — Instances of a table do not carry the same number of prompts ([Ragged tables](../APR_SPECIFICATION.md#table-ragged)). — **MUST** | |
| [APR-VAL-026](../APR_SPECIFICATION.md#warnings) | MUST | `TABLE_LABEL_MISMATCH` — A cell's label differs across instances of the same column. — **MUST** | |
| [APR-VAL-027](../APR_SPECIFICATION.md#warnings) | MUST | `TABLE_OVER_CAPACITY` — A table carries more instances than `maxRows`. — **MUST** | |
| [APR-VAL-028](../APR_SPECIFICATION.md#warnings) | MUST | `TABLE_MEMBERS_ON_A_PLAIN_SECTION` — `maxRows` or `canAddRows` on a section that is not a table ([Tables](../APR_SPECIFICATION.md#tables)). — **MUST** | |
| [APR-VAL-029](../APR_SPECIFICATION.md#warnings) | MUST | `UNPREFIXED_MEMBER` — An unrecognised member with no reverse-DNS prefix ([Unknown members](../APR_SPECIFICATION.md#extensions)). — **MUST** | |
| [APR-VAL-030](../APR_SPECIFICATION.md#warnings) | MUST | `SUBMISSION_URL_UNSUPPORTED` — A submission entry of a scheme this document does not define ([Submission targets](../APR_SPECIFICATION.md#submission)). — **MUST** | |
| [APR-VAL-031](../APR_SPECIFICATION.md#warnings) | MUST | `NON_NFC_TEXT` — Human-facing text is not in Normalization Form C ([Human-facing text](../APR_SPECIFICATION.md#human-text)). — **MUST** | |
| [APR-VAL-032](../APR_SPECIFICATION.md#warnings) | MUST | `FORBIDDEN_CODE_POINT` — Human-facing text carries a code point the floor excludes ([Human-facing text](../APR_SPECIFICATION.md#human-text)). — **MUST** | |
| [APR-VAL-035](../APR_SPECIFICATION.md#warnings) | MUST | `CONFUSABLE_SCRIPT_MIX` — One member of human-facing text mixes letters of two or more of the Latin, Cyrillic and Greek scripts ([Human-facing text](../APR_SPECIFICATION.md#human-text)). — **MUST** | |

### Renderer

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-MODEL-012](../APR_SPECIFICATION.md#table-no-layout) | MAY | A renderer **MAY** present a table as a grid, as stacked cards, as a flat sequence of prompts, or as speech. | |
| [APR-MODEL-101](../APR_SPECIFICATION.md#table-ragged) | MUST | A renderer **MUST** present every prompt an instance carries, whether or not the other instances carry one at that position. | |
| [APR-MODEL-115](../APR_SPECIFICATION.md#hints-object) | MUST | A renderer **MUST** present a prompt whose `expectedDataType` it does not recognise as a `text` prompt. | |
| [APR-MODEL-117](../APR_SPECIFICATION.md#hints-object) | MUST | A renderer **MUST** apply a prompt's hints in the order the table above lists them, and where two conflict, apply the earlier and skip the later. | |
| [APR-RENDER-001](../APR_SPECIFICATION.md#renderer-requirements) | MUST | Present section titles and prompt labels as the accessible name. — **MUST** | |
| [APR-RENDER-002](../APR_SPECIFICATION.md#renderer-requirements) | MUST NOT | Use a placeholder as the only label. — **MUST NOT** | |
| [APR-RENDER-003](../APR_SPECIFICATION.md#renderer-requirements) | MUST | Associate `helpText` programmatically with its prompt, not merely place it adjacent to it. — **MUST** | |
| [APR-RENDER-004](../APR_SPECIFICATION.md#renderer-requirements) | MUST | Convey section nesting structurally — heading levels, groups, landmarks — and not by indentation alone. — **MUST** | |
| [APR-RENDER-005](../APR_SPECIFICATION.md#renderer-requirements) | MUST | Make every prompt reachable by keyboard. — **MUST** | |
| [APR-RENDER-006](../APR_SPECIFICATION.md#renderer-requirements) | MUST NOT | Block saving because of a hint mismatch. — **MUST NOT** | |
| [APR-RENDER-007](../APR_SPECIFICATION.md#renderer-requirements) | SHOULD | Present a table section with header association, not as a purely visual grid. — **SHOULD** | |
| [APR-RENDER-009](../APR_SPECIFICATION.md#export) | MUST NOT | A renderer **MUST NOT** write export layout back into the APR document. | |
| [APR-RENDER-010](../APR_SPECIFICATION.md#ordering) | MUST | Present sections in array order. — **MUST** | |
| [APR-RENDER-011](../APR_SPECIFICATION.md#ordering) | MUST | Present the prompts within a section in array order. — **MUST** | |
| [APR-RENDER-012](../APR_SPECIFICATION.md#ordering) | MUST | Present a section's own prompts before its child sections. — **MUST** | |
| [APR-RENDER-013](../APR_SPECIFICATION.md#ordering) | MAY | A renderer **MAY** paginate, group, or lazily load a form. | |
| [APR-RENDER-014](../APR_SPECIFICATION.md#renderer-requirements) | MUST | Accept a response to every prompt from the keyboard. — **MUST** | |
| [APR-RENDER-015](../APR_SPECIFICATION.md#export) | MAY | A renderer **MAY** introduce layout — page size, margins, footers — into an export to PDF, HTML, or print. | |
| [APR-TEXT-013](../APR_SPECIFICATION.md#filled-never-rewritten) | SHOULD | A renderer **SHOULD** show a code point that [Human-facing text](../APR_SPECIFICATION.md#human-text) excludes visibly, escaped or badged, wherever it presents one, without altering the stored value. | |
| [APR-VAL-006](../APR_SPECIFICATION.md#warnings) | MUST NOT | A renderer **MUST NOT** let a warning prevent saving. | |
| [APR-VAL-033](../APR_SPECIFICATION.md#warnings) | MUST NOT | A renderer **MUST NOT** let a warning prevent entering text. | |

### Host

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-SEC-008](../APR_SPECIFICATION.md#media-types) | SHOULD | A host **SHOULD** ask for a new filename when it turns a template into a filled form, so the blank template is not overwritten. | |

### Documents

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-MODEL-002](../APR_SPECIFICATION.md#any-string) | MAY | A `prompt.response` **MAY** be any string, whatever its hints ask for. | |
| [APR-MODEL-005](../APR_SPECIFICATION.md#root-object) | REQUIRED | `sections` — array — **REQUIRED** — At least one section ([Section](../APR_SPECIFICATION.md#section-object)). | |
| [APR-MODEL-007](../APR_SPECIFICATION.md#metadata) | REQUIRED | `title` — non-blank string — **REQUIRED** — The form's name. | |
| [APR-MODEL-008](../APR_SPECIFICATION.md#metadata) | REQUIRED | `templateId` — string — **REQUIRED** when `documentType` is `filledForm` — A URI identifying the template a filled form answers ([Template identity](../APR_SPECIFICATION.md#template-identity)). Optional on a template. | |
| [APR-MODEL-009](../APR_SPECIFICATION.md#section-object) | MUST | A section **MUST** contain at least one prompt or at least one child section, tables included. | |
| [APR-MODEL-010](../APR_SPECIFICATION.md#prompt-object) | MUST | Within each namespace, an id **MUST** be unique across the whole document, not merely among siblings. | |
| [APR-MODEL-014](../APR_SPECIFICATION.md#table-ragged) | SHOULD | The instances of a table **SHOULD** carry the same number of prompts and the same label at each position. | |
| [APR-MODEL-026](../APR_SPECIFICATION.md#roles) | REQUIRED | `id` — string — **REQUIRED** — The identifier a `role` member names. | |
| [APR-MODEL-029](../APR_SPECIFICATION.md#extensions) | MUST | **An extension member is named by its owner.** An extension member name **MUST** begin with a reverse-DNS prefix its writer owns, followed by a dot: `com.example.priority`, `gov.ct.dmv.routing`. | |
| [APR-MODEL-036](../APR_SPECIFICATION.md#metadata) | MUST | A `templateId` **MUST** be a URI (RFC 3986). | |
| [APR-MODEL-043](../APR_SPECIFICATION.md#regarding) | MUST | `regarding` **MUST** be an array of distinct digest strings, each in the digest form ([Digests](../APR_SPECIFICATION.md#digests)). | |
| [APR-MODEL-046](../APR_SPECIFICATION.md#table-rows) | MUST | A section carrying `kind: "table"` **MUST** carry at least one child section. | |
| [APR-MODEL-047](../APR_SPECIFICATION.md#section-object) | MUST | A section's `maxRows` **MUST** be at least 1. | |
| [APR-MODEL-053](../APR_SPECIFICATION.md#root-object) | REQUIRED | `aprVersion` — string — **REQUIRED** — The version of *this specification* the record is written to, never the form's own ([Version compatibility](../APR_SPECIFICATION.md#version-compatibility)). | |
| [APR-MODEL-054](../APR_SPECIFICATION.md#root-object) | OPTIONAL | `documentType` — string — **OPTIONAL** — `template` or `filledForm`; absent means `template` ([Document type](../APR_SPECIFICATION.md#media-types)). | |
| [APR-MODEL-055](../APR_SPECIFICATION.md#root-object) | REQUIRED | `metadata` — object — **REQUIRED** — [Metadata](../APR_SPECIFICATION.md#metadata) | |
| [APR-MODEL-056](../APR_SPECIFICATION.md#root-object) | OPTIONAL | `roles` — array — **OPTIONAL** — [Roles](../APR_SPECIFICATION.md#roles) | |
| [APR-MODEL-057](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `description` — string — **OPTIONAL** — Prose about the form as a whole. | |
| [APR-MODEL-058](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `created` — date-time — **OPTIONAL** — RFC 3339. | |
| [APR-MODEL-059](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `modified` — date-time — **OPTIONAL** — RFC 3339. | |
| [APR-MODEL-060](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `author` — string — **OPTIONAL** — A person. | |
| [APR-MODEL-061](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `publisher` — string — **OPTIONAL** — The organization standing behind the form. | |
| [APR-MODEL-062](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `language` — string — **OPTIONAL** — A BCP 47 language tag for the form's human-facing authoring text. | |
| [APR-MODEL-063](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `templateVersion` — string — **OPTIONAL** — The template revision answered. | |
| [APR-MODEL-064](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `submissionUrls` — array of string — **OPTIONAL** — Ordered delivery choices, each `https` or `mailto` ([Submission targets](../APR_SPECIFICATION.md#submission)). | |
| [APR-MODEL-065](../APR_SPECIFICATION.md#metadata) | OPTIONAL | `regarding` — array of string — **OPTIONAL** — Digests of the records this form was completed with reference to ([Related records](../APR_SPECIFICATION.md#regarding)). | |
| [APR-MODEL-066](../APR_SPECIFICATION.md#section-object) | REQUIRED | `id` — non-blank string — **REQUIRED** — Unique document-wide among sections ([Prompt](../APR_SPECIFICATION.md#prompt-object)). | |
| [APR-MODEL-067](../APR_SPECIFICATION.md#section-object) | REQUIRED | `title` — non-blank string — **REQUIRED** — The section's heading in the document outline. | |
| [APR-MODEL-068](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `description` — string — **OPTIONAL** | |
| [APR-MODEL-069](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `sections` — array — **OPTIONAL** — Child sections, recursively. | |
| [APR-MODEL-070](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `prompts` — array — **OPTIONAL** | |
| [APR-MODEL-071](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `kind` — string — **OPTIONAL** — `table` when this section's child sections are repeating instances ([Tables](../APR_SPECIFICATION.md#tables)). | |
| [APR-MODEL-072](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `canAddRows` — boolean — **OPTIONAL** — Whether a filler can add or remove instances ([Rows and instances](../APR_SPECIFICATION.md#table-rows)). | |
| [APR-MODEL-073](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `maxRows` — integer — **OPTIONAL** — Advisory cap on instance count. | |
| [APR-MODEL-074](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `role` — string — **OPTIONAL** — [Roles](../APR_SPECIFICATION.md#roles) | |
| [APR-MODEL-075](../APR_SPECIFICATION.md#section-object) | OPTIONAL | `language` — string — **OPTIONAL** — A BCP 47 language tag, overriding the language the section inherits ([Metadata](../APR_SPECIFICATION.md#metadata)). | |
| [APR-MODEL-076](../APR_SPECIFICATION.md#prompt-object) | REQUIRED | `id` — non-blank string — **REQUIRED** — Unique document-wide among prompts. | |
| [APR-MODEL-077](../APR_SPECIFICATION.md#prompt-object) | REQUIRED | `label` — non-blank string — **REQUIRED** — The accessible name. | |
| [APR-MODEL-078](../APR_SPECIFICATION.md#prompt-object) | OPTIONAL | `response` — string — **OPTIONAL** — Absent means empty ([Responses are strings](../APR_SPECIFICATION.md#responses)). | |
| [APR-MODEL-079](../APR_SPECIFICATION.md#prompt-object) | OPTIONAL | `hints` — object — **OPTIONAL** — [Hints](../APR_SPECIFICATION.md#hints-object). Advisory in full. | |
| [APR-MODEL-080](../APR_SPECIFICATION.md#prompt-object) | OPTIONAL | `role` — string — **OPTIONAL** — Overrides the containing section's role ([Roles](../APR_SPECIFICATION.md#roles)). | |
| [APR-MODEL-081](../APR_SPECIFICATION.md#prompt-object) | OPTIONAL | `language` — string — **OPTIONAL** — A BCP 47 language tag, overriding the language the prompt inherits ([Metadata](../APR_SPECIFICATION.md#metadata)). | |
| [APR-MODEL-092](../APR_SPECIFICATION.md#prompt-object) | MAY | A section and a prompt **MAY** share an id. | |
| [APR-MODEL-093](../APR_SPECIFICATION.md#prompt-object) | SHOULD | A template **SHOULD** keep each id unchanged across its versions. | |
| [APR-MODEL-102](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `placeholder` — string — **OPTIONAL** — Text shown in an empty control. Never a substitute for `label`. | |
| [APR-MODEL-103](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `expectedDataType` — string — **OPTIONAL** — Suggested input affordance, from the registry below. | |
| [APR-MODEL-104](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `suggestedValues` — array of string — **OPTIONAL** — Offered as options. A response outside the list is still valid. | |
| [APR-MODEL-105](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `helpText` — string — **OPTIONAL** — Explanatory text for the prompt. | |
| [APR-MODEL-106](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `validationPattern` — string — **OPTIONAL** — Advisory regular expression. | |
| [APR-MODEL-107](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `min` — number or string — **OPTIONAL** — Suggested lower bound for an ordered field. A number on `number`, `currency`, and `range`; a canonical-form string on `date`, `time`, and `datetime`. | |
| [APR-MODEL-108](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `max` — number or string — **OPTIONAL** — Suggested upper bound for an ordered field. Typed as `min`. | |
| [APR-MODEL-109](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `step` — number — **OPTIONAL** — Suggested increment for an ordered field. Applies to `number`, `currency`, and `range`. | |
| [APR-MODEL-110](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `exprHidden` — string — **OPTIONAL** — CEL. Truthy hides this prompt ([Expressions](../APR_SPECIFICATION.md#expressions)). | |
| [APR-MODEL-111](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `exprValue` — string — **OPTIONAL** — CEL. Computed value. | |
| [APR-MODEL-112](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `exprExpected` — string — **OPTIONAL** — CEL. Truthy marks the prompt as expected. | |
| [APR-MODEL-113](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `exprValidation` — string — **OPTIONAL** — CEL. Returns a message; empty means valid. | |
| [APR-MODEL-114](../APR_SPECIFICATION.md#hints-object) | OPTIONAL | `exprReadOnly` — string — **OPTIONAL** — CEL. Truthy makes this prompt read-only in a renderer. | |
| [APR-MODEL-123](../APR_SPECIFICATION.md#roles) | OPTIONAL | `name` — string — **OPTIONAL** — The name shown to a person. | |
| [APR-MODEL-124](../APR_SPECIFICATION.md#roles) | OPTIONAL | `description` — string — **OPTIONAL** — Who this role is, where the name alone is not obvious. | |
| [APR-REP-002](../APR_SPECIFICATION.md#encoding) | MUST | A document **MUST** be encoded as UTF-8 (RFC 3629). | |
| [APR-REP-004](../APR_SPECIFICATION.md#encoding) | MUST NOT | A string in a document **MUST NOT** contain U+0000, an unpaired surrogate in the range U+D800 to U+DFFF, or a control character in the range U+0001 to U+001F other than tab (U+0009), line feed (U+000A), and carriage return (U+000D). | |
| [APR-REP-005](../APR_SPECIFICATION.md#apr-jsonc) | MUST | An APR-JSONC document **MUST** match production [1] `apr-jsonc-text`. | |
| [APR-REP-007](../APR_SPECIFICATION.md#apr-yaml) | MUST | An APR-YAML document **MUST** be a well-formed YAML 1.2.2 stream, per that specification's character, structural, flow, block, and document-stream productions (chapters 5 to 9, through [211] `l-yaml-stream`). | |
| [APR-REP-009](../APR_SPECIFICATION.md#apr-yaml) | MUST | Every mapping key in an APR-YAML document **MUST** resolve to a string by [Scalar resolution](../APR_SPECIFICATION.md#yaml-resolution). | |
| [APR-REP-010](../APR_SPECIFICATION.md#apr-yaml) | MUST NOT | Anchors and aliases — [101] `c-ns-anchor-property`, [104] `c-ns-alias-node` — **MUST NOT** — `YAML_ANCHOR_FORBIDDEN` | |
| [APR-REP-016](../APR_SPECIFICATION.md#json-subset) | MUST | A structural member that no JSON type fits **MUST** be a string in the form its member table states: an RFC 3339 string for a timestamp, and for a bound on a temporal field, that field's canonical write form ([Canonical value forms](../APR_SPECIFICATION.md#canonical-values)). | |
| [APR-REP-020](../APR_SPECIFICATION.md#apr-yaml) | MUST NOT | Tags, including `!!str`, `!!binary`, and local tags — [97] `c-ns-tag-property` — **MUST NOT** — `YAML_TAG_FORBIDDEN` | |
| [APR-REP-021](../APR_SPECIFICATION.md#apr-yaml) | MUST NOT | Merge keys: a plain `<<` in key position — none; the merge key is a YAML 1.1 type — **MUST NOT** — `YAML_MERGE_KEY_FORBIDDEN` | |
| [APR-REP-022](../APR_SPECIFICATION.md#apr-yaml) | MUST NOT | Directives, including `%YAML` and `%TAG` — [82] `l-directive` — **MUST NOT** — `YAML_DIRECTIVE_FORBIDDEN` | |
| [APR-TEXT-011](../APR_SPECIFICATION.md#human-text) | MUST | The human-facing text of a form **MUST** be in Normalization Form C (UAX #15) and **MUST NOT** contain a code point that is unassigned, a surrogate, private-use, a control other than U+0009 and U+000A, or that UTS #39 classifies with an `Identifier_Type` of `Default_Ignorable`, `Deprecated`, or `Not_Character`. | |

## `core+streams`

20 rules.

### Implementation

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-DIGEST-002](../APR_SPECIFICATION.md#digests) | MUST | An implementation **MUST** include in a form digest every APR-defined member and every unknown member that survived parsing, and exclude only source trivia. | |
| [APR-DIGEST-006](../APR_SPECIFICATION.md#digests) | MUST | An implementation that computes a semantic digest **MUST** compute it over the RFC 8785 JCS serialization of the fully parsed JSON semantic model, encoded as UTF-8, and express it as lowercase hexadecimal SHA-256 (FIPS 180-4) prefixed with `sha256:`. | |
| [APR-DIGEST-010](../APR_SPECIFICATION.md#digests) | SHOULD | An implementation producing a manifest **SHOULD** give it one entry for every value in the semantic model at every depth, unknown members included. | |

### Reader

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-CONF-002](../APR_SPECIFICATION.md#profile-streams) | MUST NOT | A reader that claims `core+streams` but not `core+attestations` **MUST NOT** reject a stream because it contains attestation records. | |
| [APR-STREAM-001](../APR_SPECIFICATION.md#streams) | MUST | A reader **MUST** reject a stream that mixes representations. | |
| [APR-STREAM-002](../APR_SPECIFICATION.md#stream-equivalence) | MUST | A reader **MUST** produce the same sequence of semantic records from an APR-JSONC stream and an APR-YAML stream whose records, in order, have equal semantic models. | |
| [APR-STREAM-003](../APR_SPECIFICATION.md#streams) | MUST NOT | A reader **MUST NOT** deduplicate repeated form occurrences, even when their semantic digests are identical. | |
| [APR-STREAM-004](../APR_SPECIFICATION.md#streams) | MUST | A reader asked for a single form that is given a stream **MUST** report `APR_STREAM_REQUIRES_ITERATION` and **MUST NOT** select a record by position. | |
| [APR-STREAM-005](../APR_SPECIFICATION.md#streams) | MUST NOT | A reader **MUST NOT** derive a subject, a revision, a chronology, or a trust relationship from the position of a record. | |
| [APR-STREAM-006](../APR_SPECIFICATION.md#jsonc-framing) | MUST | A reader **MUST** reject an APR-JSONC stream in which a record is not preceded by `RS`. | |
| [APR-STREAM-007](../APR_SPECIFICATION.md#streams) | MUST | A reader **MUST** reject a record that is neither a form nor an attestation. | |
| [APR-STREAM-008](../APR_SPECIFICATION.md#streams) | MUST | A reader **MUST** read a record that carries `recordType` as an attestation, and any other record as a form. | |

### Writer

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-CONF-011](../APR_SPECIFICATION.md#profile-streams) | MUST | A writer that claims `core+streams` but not `core+attestations` **MUST** preserve attestation records across a round trip. | |

### Verifier

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-DIGEST-005](../APR_SPECIFICATION.md#digests) | MUST | Integrity comes from `root` alone. Entries are how a verifier explains *which* values differ without the manifest retaining what they used to be, so a manifest missing a path explains less and proves exactly as much. A verifier that finds a subject differing from `root` **MUST** report the difference at the most specific path the manifest carries, and **MUST NOT** report a path it does not. | |
| [APR-DIGEST-007](../APR_SPECIFICATION.md#digests) | MUST | A verifier that cannot preserve or digest an unknown member **MUST** report the assertion as `unverifiable`, not valid. | |

### Documents

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-DIGEST-001](../APR_SPECIFICATION.md#digests) | MUST | A digest value **MUST** match `^sha256:[0-9a-f]{64}$`. | |
| [APR-DIGEST-003](../APR_SPECIFICATION.md#digests) | MUST | `entries` **MUST** be ordered by `path`, compared as strings, and **MUST NOT** repeat a path. | |
| [APR-DIGEST-004](../APR_SPECIFICATION.md#digests) | MUST | `entries` **MUST** contain the root pointer. | |
| [APR-DIGEST-008](../APR_SPECIFICATION.md#digests) | REQUIRED | `path` — string — **REQUIRED** — A JSON Pointer (RFC 6901) to a value in the semantic model. | |
| [APR-DIGEST-009](../APR_SPECIFICATION.md#digests) | REQUIRED | `digest` — string — **REQUIRED** — The digest of the JCS encoding of the value at `path`. | |

## `core+attestations`

53 rules.

### Implementation

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-ATTEST-011](../APR_SPECIFICATION.md#never-gate) | MUST NOT | An implementation **MUST NOT** require an attestation in order to save, send, accept, or process a form. | |
| [APR-ATTEST-012](../APR_SPECIFICATION.md#never-gate) | MUST NOT | An implementation **MUST NOT** refuse to parse, validate, render, print, export, or extract data from a document because its attestations are absent, unrecognized, expired, untrusted, or outright invalid. | |
| [APR-ATTEST-013](../APR_SPECIFICATION.md#never-gate) | MAY | An implementation **MAY** warn, badge, or refuse to *act* on a document by its own policy — a receiving workflow is entitled to reject an unattested permit request. | |
| [APR-ATTEST-019](../APR_SPECIFICATION.md#never-gate) | MUST NOT | **An attestation is an assertion about a document, never a permission to read it.** An implementation **MUST NOT** treat the presence, absence, or state of an attestation as authorization to read, or to withhold, the data a form carries. | |
| [APR-ATTEST-031](../APR_SPECIFICATION.md#attestation-catalogue) | MUST | An implementation **MUST** preserve an attestation record's extension members across a round trip. | |
| [APR-ATTEST-035](../APR_SPECIFICATION.md#proofs) | MUST | An implementation producing a proof **MUST** compute it over the JCS serialization (RFC 8785) of the attestation's envelope: the record without its `proofs` member. | |
| [APR-ATTEST-038](../APR_SPECIFICATION.md#proofs) | MUST | An implementation **MUST** preserve a proof whose type it does not recognize. | |
| [APR-ATTEST-041](../APR_SPECIFICATION.md#proofs) | MUST NOT | An implementation **MUST NOT** conclude from a claimed signing time that one record precedes another. | |

### Reader

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-ATTEST-044](../APR_SPECIFICATION.md#changed-forms) | MUST | A reader **MUST** accept a stream holding several attestations of one form. | |
| [APR-ATTEST-045](../APR_SPECIFICATION.md#changed-forms) | MUST | A reader **MUST** accept an attestation that comes before its subject in a stream. | |

### Validator

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-ATTEST-053](../APR_SPECIFICATION.md#never-gate) | MUST NOT | A validator **MUST NOT** report attestation state as an error. | |

### Renderer

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-ATTEST-039](../APR_SPECIFICATION.md#proofs) | MUST NOT | A renderer **MUST NOT** present an `unverifiable` proof as `invalid`. | |
| [APR-ATTEST-040](../APR_SPECIFICATION.md#proofs) | MUST | A renderer that shows a claimed signing time **MUST** show it as claimed rather than proven. | |
| [APR-ATTEST-052](../APR_SPECIFICATION.md#never-gate) | MUST NOT | A renderer **MUST NOT** present an unattested document as deficient. | |

### Verifier

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-ATTEST-006](../APR_SPECIFICATION.md#attestation-scope) | MUST NOT | A verifier **MUST NOT** report a `fields` attestation as `invalid` because someone edited a section it does not cover. | |
| [APR-ATTEST-008](../APR_SPECIFICATION.md#proofs) | MUST | A verifier that does not recognize a proof type **MUST** report the proof as `unverifiable`, never as `invalid`. | |
| [APR-ATTEST-009](../APR_SPECIFICATION.md#changed-forms) | MUST NOT | A verifier **MUST NOT** transfer an attestation to a changed form. | |
| [APR-ATTEST-010](../APR_SPECIFICATION.md#verification) | MUST | A verifier **MUST** report whether a proof verifies separately from whether its certificate is trusted. | |
| [APR-ATTEST-015](../APR_SPECIFICATION.md#attestation-scope) | MUST NOT | A verifier **MUST NOT** report `invalid` merely because the form it holds is a later one. | |
| [APR-ATTEST-016](../APR_SPECIFICATION.md#attestation-scope) | MAY | A verifier **MAY** compare a `fields` manifest's entries against a changed form and report which attested paths still match. | |
| [APR-ATTEST-017](../APR_SPECIFICATION.md#attestation-catalogue) | MUST | A verifier **MUST** resolve a subject by `subject.digest` alone, and never by stream position, filename, or document id. | |
| [APR-ATTEST-018](../APR_SPECIFICATION.md#verification) | MUST | A verifier **MUST** report each result independently of the others, so an attestation can be both `unverifiable` and `witnessed`. | |
| [APR-ATTEST-020](../APR_SPECIFICATION.md#changed-forms) | SHOULD | **Say what happened, not only what is missing.** An attestation whose subject resolves to nothing is `unresolved` ([Verification vocabulary](../APR_SPECIFICATION.md#verification)), and that is the whole of what the vocabulary states. Where a verifier holds a form occurrence that is not the subject, it **SHOULD** report both facts: that the attested form is absent, and that a different form is present. | |
| [APR-ATTEST-032](../APR_SPECIFICATION.md#attestation-scope) | MUST NOT | A verifier **MUST NOT** report that comparison as a verification result. | |
| [APR-ATTEST-036](../APR_SPECIFICATION.md#proofs) | MUST | A verifier **MUST** verify a proof over that same serialization. | |
| [APR-ATTEST-046](../APR_SPECIFICATION.md#verification) | MUST | `valid` — The subject resolved, digest and manifest match, and a recognized proof verifies. — **MUST** | |
| [APR-ATTEST-047](../APR_SPECIFICATION.md#verification) | MUST | `invalid` — A recognized proof fails, or a resolved subject differs from the attested digest or manifest. — **MUST** | |
| [APR-ATTEST-048](../APR_SPECIFICATION.md#verification) | MUST | `unresolved` — No matching form occurrence is available. — **MUST** | |
| [APR-ATTEST-049](../APR_SPECIFICATION.md#verification) | MUST | `unverifiable` — Required representation, extension, digest, or proof support is unavailable. — **MUST** | |
| [APR-ATTEST-050](../APR_SPECIFICATION.md#verification) | MUST | `witnessed` — One or more referenced envelopes resolve and match. — **MUST** | |
| [APR-ATTEST-051](../APR_SPECIFICATION.md#verification) | MUST NOT | A verifier **MUST NOT** report `unresolved` as a failure of the assertion. | |

### Documents

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-ATTEST-001](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `recordType` — string — **REQUIRED** — Exactly `attestation`. | |
| [APR-ATTEST-002](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `aprVersion` — string — **REQUIRED** — Exactly `1.0-beta.6`. | |
| [APR-ATTEST-003](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `subject.canonicalization` — string — **REQUIRED** — Exactly `jcs-sha256`. | |
| [APR-ATTEST-004](../APR_SPECIFICATION.md#attestation-catalogue) | MAY | An attestation record **MAY** carry extension members. | |
| [APR-ATTEST-005](../APR_SPECIFICATION.md#attestation-scope) | MUST | The manifest of a `fields` attestation **MUST** include each selected prompt, its response and hints, and every ancestor section's id, title, description, kind, and role. | |
| [APR-ATTEST-007](../APR_SPECIFICATION.md#proofs) | MUST NOT | A proof **MUST NOT** carry a copy of the subject digest or the scope. | |
| [APR-ATTEST-014](../APR_SPECIFICATION.md#proofs) | MAY | A proof **MAY** carry a claimed signing time. | |
| [APR-ATTEST-021](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `subject` — object — **REQUIRED** — `digest` and `canonicalization`, and no other member. | |
| [APR-ATTEST-022](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `subject.digest` — string — **REQUIRED** — The digest ([Digests and manifests](../APR_SPECIFICATION.md#digests)) of the subject form's complete semantic model. | |
| [APR-ATTEST-023](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `scope` — object — **REQUIRED** — `kind` and `fields` ([Scope](../APR_SPECIFICATION.md#attestation-scope)), and no other member. | |
| [APR-ATTEST-024](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `scope.kind` — string — **REQUIRED** — `document` or `fields`. | |
| [APR-ATTEST-025](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `scope.fields` — array — **REQUIRED** when `kind` is `fields` — One or more prompt ids, none blank. | |
| [APR-ATTEST-026](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `manifest` — object — **REQUIRED** — `root` and `entries`, and no other member. | |
| [APR-ATTEST-027](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `manifest.root` — string — **REQUIRED** — The digest of the subject form. | |
| [APR-ATTEST-028](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `manifest.entries` — array — **REQUIRED** — Entries as [Digests and manifests](../APR_SPECIFICATION.md#digests) defines them, each with no other member. | |
| [APR-ATTEST-029](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `proofs` — array — **REQUIRED** — Zero or more proofs ([Proofs](../APR_SPECIFICATION.md#proofs)). | |
| [APR-ATTEST-030](../APR_SPECIFICATION.md#attestation-catalogue) | REQUIRED | `witnesses` — array — **REQUIRED** — Zero or more witnesses ([Witnesses](../APR_SPECIFICATION.md#witnesses)). | |
| [APR-ATTEST-033](../APR_SPECIFICATION.md#proofs) | REQUIRED | `type` — string — **REQUIRED** — The proof type. | |
| [APR-ATTEST-034](../APR_SPECIFICATION.md#proofs) | REQUIRED | `value` — string — **REQUIRED** — The proof, encoded as its type defines. | |
| [APR-ATTEST-037](../APR_SPECIFICATION.md#proofs) | MUST | `cms/ecdsa-p256-sha256` — ECDSA over the P-256 curve with SHA-256 (FIPS 186-5), carried as CMS SignedData (RFC 5652) with the X.509 certificate chain (RFC 5280) included, encoded as base64 (RFC 4648). — **MUST** | |
| [APR-ATTEST-042](../APR_SPECIFICATION.md#witnesses) | MUST | Each entry in `witnesses` **MUST** be the digest of an earlier attestation's envelope. | |
| [APR-ATTEST-043](../APR_SPECIFICATION.md#witnesses) | MUST NOT | `witnesses` **MUST NOT** repeat a digest. | |

## `core+expressions`

35 rules.

### Implementation

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-EXPR-001](../APR_SPECIFICATION.md#expr-invariants) | MUST NOT | An implementation evaluating an expression **MUST NOT** reject, rewrite, or invalidate a response. | |
| [APR-EXPR-002](../APR_SPECIFICATION.md#expr-invariants) | MUST NOT | An implementation **MUST NOT** expose filesystem, network, process, clock, randomness, reflection, environment, or document-mutation access to an expression. | |
| [APR-EXPR-003](../APR_SPECIFICATION.md#expr-invariants) | MUST NOT | An implementation **MUST NOT** let a failed evaluation propagate as an error into a filling workflow. | |
| [APR-EXPR-004](../APR_SPECIFICATION.md#expr-activation) | MUST NOT | An implementation **MUST NOT** let a direct binding shadow `_this`, `_id`, `_now`, `_today`, or `ctx`. | |
| [APR-EXPR-005](../APR_SPECIFICATION.md#expr-activation) | MUST | An implementation **MUST** take `_now` and `_today` from the caller and never from the host clock during evaluation. | |
| [APR-EXPR-006](../APR_SPECIFICATION.md#expr-unbound) | MUST | An implementation **MUST** treat a response that cannot be converted to its declared type — free text in a `number` field, an unparseable date, or an empty one — as unbound, and never as a default. | |
| [APR-EXPR-008](../APR_SPECIFICATION.md#expr-computed) | MUST NOT | An implementation **MUST NOT** overwrite an authored response when it recomputes. | |
| [APR-EXPR-009](../APR_SPECIFICATION.md#expr-computed) | MUST | An implementation **MUST** order computed prompts by their direct references so that a subtotal feeds a tax feeds a total in one pass. | |
| [APR-EXPR-010](../APR_SPECIFICATION.md#expr-authoring) | SHOULD | An implementation **SHOULD** type-check expressions against the document's type environment when a template is authored, and report failures to the author with position information. | |
| [APR-EXPR-011](../APR_SPECIFICATION.md#expr-limits) | MUST | An implementation **MUST** bound expression size, complexity, and evaluation cost, so that evaluation terminates. | |
| [APR-EXPR-012](../APR_SPECIFICATION.md#expr-language) | MUST | The language is **cel-spec release `v0.25.3`**: its language definition, its standard library, and its standard macros. An implementation claiming `core+expressions` **MUST** evaluate expressions as that release specifies and **MUST** pass that release's conformance suite for the surface it exposes. | |
| [APR-EXPR-013](../APR_SPECIFICATION.md#expr-language) | MUST | An implementation **MUST** provide the standard library and the standard macros, and **MUST NOT** provide any extension library or custom function. | |
| [APR-EXPR-015](../APR_SPECIFICATION.md#expr-activation) | MUST | An implementation **MUST** evaluate an expression against this read-only activation and nothing else. | |
| [APR-EXPR-016](../APR_SPECIFICATION.md#expr-activation) | MUST NOT | An implementation **MUST NOT** give a prompt whose id is not a valid CEL identifier a direct binding, nor make it reachable from an expression by any other name. | |
| [APR-EXPR-017](../APR_SPECIFICATION.md#expr-activation) | MUST | a prompt's `id` — that prompt's bound type — Direct binding, where the id is a valid CEL identifier and not reserved. — **MUST** | |
| [APR-EXPR-018](../APR_SPECIFICATION.md#expr-activation) | MUST | `_this` — the owning prompt's bound type — The response of the prompt carrying this hint. — **MUST** | |
| [APR-EXPR-019](../APR_SPECIFICATION.md#expr-activation) | MUST | `_id` — `string` — The owning prompt's id. — **MUST** | |
| [APR-EXPR-020](../APR_SPECIFICATION.md#expr-activation) | MUST | `_now` — `timestamp` — The evaluation instant, supplied by the caller. — **MUST** | |
| [APR-EXPR-021](../APR_SPECIFICATION.md#expr-activation) | MUST | `_today` — `string` — The evaluation date, supplied by the caller. — **MUST** | |
| [APR-EXPR-022](../APR_SPECIFICATION.md#expr-activation) | MUST | `ctx` — `map` — Host-supplied context ([Context](../APR_SPECIFICATION.md#expr-context)). — **MUST** | |
| [APR-EXPR-023](../APR_SPECIFICATION.md#expr-binding) | MUST | `number`, `currency`, `range` — `double` — **MUST** | |
| [APR-EXPR-024](../APR_SPECIFICATION.md#expr-binding) | MUST | `boolean` — `bool` — **MUST** | |
| [APR-EXPR-025](../APR_SPECIFICATION.md#expr-binding) | MUST | `date`, `time`, `datetime` — `timestamp` — **MUST** | |
| [APR-EXPR-026](../APR_SPECIFICATION.md#expr-binding) | MUST | `multichoice` — `list<string>` — **MUST** | |
| [APR-EXPR-027](../APR_SPECIFICATION.md#expr-binding) | MUST | everything else, or absent — `string` — **MUST** | |
| [APR-EXPR-028](../APR_SPECIFICATION.md#expr-fallback) | MUST | An implementation **MUST** write a result back to a stored string through the canonical write forms of [Canonical value forms](../APR_SPECIFICATION.md#canonical-values). | |
| [APR-EXPR-029](../APR_SPECIFICATION.md#expr-fallback) | MUST | `exprHidden` — `bool` — false — show the prompt — **MUST** | |
| [APR-EXPR-030](../APR_SPECIFICATION.md#expr-fallback) | MUST | `exprExpected` — `bool` — false — do not mark expected — **MUST** | |
| [APR-EXPR-031](../APR_SPECIFICATION.md#expr-fallback) | MUST | `exprReadOnly` — `bool` — false — keep editable — **MUST** | |
| [APR-EXPR-032](../APR_SPECIFICATION.md#expr-fallback) | MUST | `exprValidation` — `string` — empty — no advisory — **MUST** | |
| [APR-EXPR-033](../APR_SPECIFICATION.md#expr-fallback) | MUST | `exprValue` — the prompt's bound type — Do not write; retain the stored response exactly — **MUST** | |
| [APR-EXPR-035](../APR_SPECIFICATION.md#expr-limits) | MUST | An implementation **MUST** report reaching a bound as a failure that applies the fallback, never as partial mutation. | |

### Reader

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-EXPR-034](../APR_SPECIFICATION.md#expr-computed) | MAY | A reader **MAY** replace a response it computed itself in the same session. | |

### Renderer

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-EXPR-014](../APR_SPECIFICATION.md#expr-computed) | MUST | A renderer **MUST** keep a computed prompt editable. | |

### Host

| Rule | Level | Requirement | Supported |
| --- | --- | --- | --- |
| [APR-EXPR-007](../APR_SPECIFICATION.md#expr-context) | MUST NOT | A host **MUST NOT** place credentials, secrets, authorization decisions, or facts private to its own systems in `ctx`. | |

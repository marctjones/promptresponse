#!/usr/bin/env python3
"""Validate APR documents against the specification's error and warning tables.

The command line tool reports only that a file parsed. That is not validation:
a document can parse perfectly and still repeat an id, leave a prompt unlabelled,
or spell a structural member as a string. This reports what the specification
says a validator reports, by the codes the specification names.

    python3 scripts/validate-apr.py FILE...          # human readable
    python3 scripts/validate-apr.py --json FILE...   # one JSON report
    python3 scripts/validate-apr.py --quiet FILE...  # exit status only
    python3 scripts/validate-apr.py --rule-coverage  # which catalog rules it enforces

Exit status is 1 when any document has an error. Warnings never affect it,
because a warning never affects validity.

**The member vocabulary is read from the specification's own member tables**,
not from the schema and not from a copy kept here. A member renamed in the
document is renamed here on the next run, and a table this tool cannot find is
reported rather than assumed. The schema is a derived artifact and may lag; this
follows the authority ordering instead.

What it does not check: whether a response is *true*, which the format never
validates; cryptographic proofs, which scripts/check-corpus.py covers; and the
expression profile, which needs a CEL implementation.
"""
from __future__ import annotations

import json
import pathlib
import re
import sys
import unicodedata

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import aprlib  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = ROOT / "docs" / "APR_SPECIFICATION.md"

HEADING = re.compile(r"^#{2,4}\s+.*\{#([a-z0-9-]+)\}\s*$")
MEMBER = re.compile(r"^\|\s*`([A-Za-z0-9_.]+)`\s*\|([^|]*)\|([^|]*)\|")

# Specification anchor -> the object whose members that table defines.
TABLES = {
    "root-object": "form",
    "metadata": "metadata",
    "section-object": "section",
    "prompt-object": "prompt",
    "hints-object": "hints",
}

FORMAT_VERSION = "1.0-beta.6"
# Human-facing members: text whose whole purpose is to be read or heard, held to
# the Unicode floor the text-handling section states.
HUMAN_TEXT = {
    ("metadata", "title"), ("metadata", "description"), ("metadata", "author"),
    ("metadata", "publisher"), ("section", "title"), ("section", "description"),
    ("prompt", "label"), ("hints", "placeholder"), ("hints", "helpText"),
}
CONTROL_OK = {0x09, 0x0A}

# Where a member's own rule is more specific than the generic native-types rules,
# name it. A blank title breaks the title rule, not the rule about JSON types.
MEMBER_RULES = {
    ("metadata", "title"): ("APR-MODEL-007",),
    ("form", "sections"): ("APR-MODEL-005",),
    ("prompt", "response"): ("APR-MODEL-001",),
}


def is_required(cell: str) -> bool:
    """Whether a Requirement cell makes a member required unconditionally.

    "REQUIRED when ..." is a condition a rule of its own states, so at the level
    of the member table that member is optional.
    """
    return cell.replace("*", "").strip() in {"REQUIRED", "Yes"}


def row_rules() -> dict[tuple[str, str], str]:
    """(object, member) -> the rule its member-table row states."""
    text = SPEC.read_text(encoding="utf-8")
    rules: dict[tuple[str, str], str] = {}
    anchor = None
    for line in text.split("\n"):
        heading = HEADING.match(line)
        if heading:
            anchor = heading.group(1)
            continue
        match = MEMBER.match(line) if anchor in TABLES else None
        rule = re.search(r"\[(APR-[A-Z]+-\d{3})\]", line) if match else None
        if rule:
            rules[(TABLES[anchor], match.group(1))] = rule.group(1)
    return rules


def spec_members() -> dict[str, dict[str, tuple[str, bool]]]:
    """Member name -> (declared type, required), per specification table."""
    text = SPEC.read_text(encoding="utf-8")
    tables: dict[str, dict[str, tuple[str, bool]]] = {}
    anchor = None
    for line in text.split("\n"):
        heading = HEADING.match(line)
        if heading:
            anchor = heading.group(1)
            continue
        if anchor not in TABLES:
            continue
        match = MEMBER.match(line)
        if not match:
            continue
        name, declared, required = match.group(1), match.group(2), match.group(3)
        if "." in name:  # a dotted row documents a nested member
            continue
        tables.setdefault(TABLES[anchor], {})[name] = (
            declared.strip().strip("`"), is_required(required))
    missing = set(TABLES.values()) - set(tables)
    if missing:
        raise SystemExit(
            f"cannot find member tables for {sorted(missing)} in the specification; "
            f"this tool reads its vocabulary from them")
    return tables


# Member presence is stated row by row, so a missing or blank member cites its row.
ROW_RULES = row_rules()


def data_types() -> set[str]:
    """The `expectedDataType` registry, read from the specification's own list."""
    text = SPEC.read_text(encoding="utf-8")
    match = re.search(r"`expectedDataType` registry:(.*?)\n\n", text, re.S)
    if not match:
        raise SystemExit("cannot find the expectedDataType registry in the specification")
    return set(re.findall(r"`([a-z]+)`", match.group(1)))


def as_number(value: str):
    try:
        return float(value.strip())
    except (TypeError, ValueError):
        return None


def type_ok(value, declared: str) -> bool:
    declared = declared.lower()
    if declared.startswith("array"):
        if not isinstance(value, list):
            return False
        if "of string" in declared:
            return all(isinstance(v, str) for v in value)
        return True
    if declared == "object":
        return isinstance(value, dict)
    if declared == "boolean":
        return isinstance(value, bool)
    if declared == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if declared == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if "number or string" in declared:
        return isinstance(value, str) or (
            isinstance(value, (int, float)) and not isinstance(value, bool))
    if "non-blank string" in declared:
        return isinstance(value, str) and value.strip() != ""
    if declared in {"string", "date-time"}:
        return isinstance(value, str)
    return True  # a type this tool does not model is not an error it can claim


class Report:
    def __init__(self, where: str):
        self.where = where
        self.findings: list[dict] = []

    def add(self, severity: str, code: str, path: str, message: str,
            rules: tuple[str, ...] = ()) -> None:
        # One finding per code per location. Two passes can reach the same defect
        # — a member table says a response must be a string, and the responses
        # rule says so more pointedly — and reporting it twice helps nobody.
        for existing in self.findings:
            if existing["code"] == code and existing["path"] == path:
                # Two passes can reach one defect from different rules — a member
                # table says the type is wrong, and the null rule says why. Keep one
                # finding, but keep both attributions.
                for rule in rules:
                    if rule not in existing["rules"]:
                        existing["rules"].append(rule)
                return
        self.findings.append({"severity": severity, "code": code, "path": path,
                              "message": message, "rules": list(rules)})

    # The code each condition in the errors and warnings tables is reported under.
    # Reporting a condition under its row's code is what that row's rule requires,
    # so every such finding is evidence for the row as well as for the rule the
    # condition is about.
    ERROR_ROWS = {
        "NULL_DOCUMENT": "APR-VAL-011", "REQUIRED_FIELD": "APR-VAL-012",
        "UNSUPPORTED_VERSION": "APR-VAL-013", "DUPLICATE_ID": "APR-VAL-014",
        "EMPTY_SECTION": "APR-VAL-015", "EMPTY_TABLE": "APR-VAL-016",
        "WRONG_TYPE": "APR-VAL-017",
    }
    WARNING_ROWS = {
        "RESPONSE_CONTRADICTS_TYPE": "APR-VAL-018", "RESPONSE_PATTERN_MISMATCH": "APR-VAL-019",
        "RESPONSE_OUTSIDE_BOUNDS": "APR-VAL-020", "RESPONSE_OUTSIDE_SUGGESTED_VALUES": "APR-VAL-021",
        "HINT_UNUSABLE": "APR-VAL-022", "UNREGISTERED_DATA_TYPE": "APR-VAL-023",
        "UNDECLARED_ROLE": "APR-VAL-024", "TABLE_RAGGED": "APR-VAL-025",
        "TABLE_LABEL_MISMATCH": "APR-VAL-026", "TABLE_OVER_CAPACITY": "APR-VAL-027",
        "TABLE_MEMBERS_ON_A_PLAIN_SECTION": "APR-VAL-028", "UNPREFIXED_MEMBER": "APR-VAL-029",
        "SUBMISSION_URL_UNSUPPORTED": "APR-VAL-030", "NON_NFC_TEXT": "APR-VAL-031",
        "FORBIDDEN_CODE_POINT": "APR-VAL-032",
    }

    def error(self, code, path, msg, *rules):
        row = self.ERROR_ROWS.get(code)
        if row and row not in rules:
            rules = (*rules, row)
        self.add("error", code, path, msg, rules)

    def warn(self, code, path, msg, *rules):
        row = self.WARNING_ROWS.get(code)
        if row and row not in rules:
            rules = (*rules, row)
        self.add("warning", code, path, msg, rules)

    @property
    def errors(self) -> int:
        return sum(1 for f in self.findings if f["severity"] == "error")


def check_text(report: Report, path: str, value: str) -> None:
    """The Unicode floor for text meant to be read or heard by a person.

    Specification 8.2.3 places NON_NFC_TEXT and FORBIDDEN_CODE_POINT in the
    warnings table (7.2), not the errors table (7.1, exhaustive by APR-VAL-007):
    a validator MUST report them, but a warning never makes a document invalid
    or blocks saving (APR-VAL-002, APR-VAL-006). report.error() here would
    reject a document the format requires to stay valid.
    """
    if unicodedata.normalize("NFC", value) != value:
        report.warn("NON_NFC_TEXT", path,
                    "human-facing text must be in Normalization Form C",
                    "APR-TEXT-011")
    for char in value:
        point = ord(char)
        category = unicodedata.category(char)
        if category == "Cc" and point not in CONTROL_OK:
            report.warn("FORBIDDEN_CODE_POINT", path,
                        f"U+{point:04X} is a control character", "APR-TEXT-011")
        elif category in {"Cs", "Co", "Cn"}:
            report.warn("FORBIDDEN_CODE_POINT", path,
                         f"U+{point:04X} is a surrogate, private-use or unassigned",
                         "APR-TEXT-011")
        elif category == "Cf":
            # Category Cf ("Format") is not uniformly invisible: it also holds ZWJ
            # and ZWNJ, load-bearing for correct glyph shaping in Persian, Hindi and
            # other scripts. The floor still flags the whole category (UTS #39
            # classifies most of it Default_Ignorable regardless of rendering
            # effect), so the message says what the rule actually is, not why.
            report.warn("FORBIDDEN_CODE_POINT", path,
                        f"U+{point:04X} is a code point the human-facing text floor excludes",
                        "APR-TEXT-011")


# APR-TEXT-012: "SHOULD apply the confusable and mixed-script detection of
# UTS #39... report what it finds." Full UTS #39 restriction-level analysis
# needs a declared document language to avoid flagging ordinary multi-script
# text (Japanese Han+Hiragana+Katakana, Korean Hangul+Han, Latin loanwords in
# Indic/Arabic/Hebrew text) -- APR has no metadata.language member yet, so
# that full analysis isn't attempted here.
#
# What doesn't need a declared language: Latin, Cyrillic and Greek have
# extensive letter-shape homoglyphs between them (Cyrillic а/Latin a, Greek
# Α/Latin A) and essentially no legitimate reason to co-occur within one
# title or label -- unlike CJK/Hangul/Indic scripts, which routinely mix with
# Latin for brand names, loanwords and numerals. Flagging only these three
# scripts mixing with each other is a narrow, script-agnostic slice of UTS #39
# that produces zero known false positives on real multi-script text.
#
# Not in specification 7.2's warnings table -- CONFUSABLE_SCRIPT_MIX is this
# implementation's own spelling of an APR-VAL-034 "MAY report a warning for a
# condition the table does not name" extension, not a code every
# implementation must use.
_CONFUSABLE_SCRIPTS = ("LATIN", "CYRILLIC", "GREEK")


def _confusable_script_of(char: str) -> str | None:
    if not unicodedata.category(char).startswith("L"):
        return None  # Not a letter: digits, punctuation and spaces are script-neutral.
    name = unicodedata.name(char, "")
    for script in _CONFUSABLE_SCRIPTS:
        if name.startswith(script):
            return script
    return None


def check_confusable_script_mix(report: Report, path: str, value: str) -> None:
    scripts = {_confusable_script_of(char) for char in value}
    scripts.discard(None)
    if len(scripts) > 1:
        report.warn("CONFUSABLE_SCRIPT_MIX", path,
                    f"mixes {', '.join(sorted(scripts)).title()} letters in one field; "
                    "Latin, Cyrillic and Greek share look-alike letters, and a mix "
                    "within one title or label is rarely intentional",
                    "APR-TEXT-012")


def check_object(report: Report, node, kind: str, path: str, members) -> None:
    declared = members[kind]
    for name, (declared_type, required) in declared.items():
        if kind == "prompt" and name == "response":
            continue  # null is tolerated here alone; check_prompt states the rule
        # A member is checked after resolution, so an unquoted APR-YAML scalar that
        # resolved to the wrong JSON type fails here exactly as the JSONC spelling
        # does, whichever representation the document was read from.
        rules = MEMBER_RULES.get((kind, name), ("APR-REP-015", "APR-REP-016"))
        # Absence and blankness break the requirement the member's own row states.
        presence = (ROW_RULES[(kind, name)],) if (kind, name) in ROW_RULES else rules
        if required and (name not in node or node[name] is None):
            report.error("REQUIRED_FIELD", f"{path}/{name}",
                         f"{kind}.{name} is required", *presence)
        elif name in node and node[name] is None:
            # `null` is not an APR value, and outside a response position it is a parse
            # failure rather than a wrong type: the member is not carrying the wrong
            # kind of value, it is carrying something the format has no place for.
            report.error("PARSE_ERROR", f"{path}/{name}",
                         f"{kind}.{name} is null; null is not an APR value outside a "
                         f"response position", "APR-REP-014", "APR-VAL-010")
        elif (name in node and "non-blank string" in declared_type
                and isinstance(node[name], str) and not node[name].strip()):
            # A blank string is the right type carrying nothing. Section 7.1 names
            # REQUIRED_FIELD for a blank title, id or label, and reporting WRONG_TYPE
            # tells a reader to look at the type of a value whose type is fine.
            report.error("REQUIRED_FIELD", f"{path}/{name}",
                         f"{kind}.{name} is blank; it must contain a non-whitespace "
                         f"character", *presence)
        elif name in node and not type_ok(node[name], declared_type):
            report.error("WRONG_TYPE", f"{path}/{name}",
                         f"{kind}.{name} must be {declared_type}, "
                         f"got {type(node[name]).__name__}", *rules)
        if (kind, name) in HUMAN_TEXT and isinstance(node.get(name), str):
            check_text(report, f"{path}/{name}", node[name])
            check_confusable_script_mix(report, f"{path}/{name}", node[name])
    for name in node:
        if name in declared:
            continue
        if "." not in name:
            report.warn("UNPREFIXED_MEMBER", f"{path}/{name}",
                        f"unknown member {name!r} carries no reverse-DNS prefix; "
                        f"unprefixed names are reserved to the specification",
                        "APR-MODEL-029", "APR-MODEL-031")


def check_prompt(report: Report, prompt, path, members, ids, roles) -> None:
    if not isinstance(prompt, dict):
        report.error("WRONG_TYPE", path, "a prompt must be an object")
        return
    check_object(report, prompt, "prompt", path, members)
    identifier = prompt.get("id")
    if isinstance(identifier, str) and identifier.strip():
        if identifier in ids["prompt"]:
            report.error("DUPLICATE_ID", f"{path}/id",
                         f"prompt id {identifier!r} is already used",
                         "APR-MODEL-010", "APR-MODEL-011")
        ids["prompt"].add(identifier)
    # A null response, and an absent one, are both read as the empty string. Any
    # other non-string is the coercion the format refuses.
    if prompt.get("response") is not None and not isinstance(prompt["response"], str):
        report.error("WRONG_TYPE", f"{path}/response",
                     "a response is always a JSON string, never a number or boolean; "
                     "it is refused, never coerced to \"42\"",
                     "APR-MODEL-001", "APR-MODEL-049")
    role = prompt.get("role")
    if isinstance(role, str) and roles and role not in roles:
        report.warn("UNDECLARED_ROLE", f"{path}/role", f"role {role!r} is not declared; a validator may warn about "
                    f"one and must not reject it",
                    "APR-MODEL-051")
    hints = prompt.get("hints")
    if isinstance(hints, dict):
        check_object(report, hints, "hints", f"{path}/hints", members)
        response = prompt.get("response")
        suggested = hints.get("suggestedValues")
        if (isinstance(response, str) and response != ""
                and isinstance(suggested, list) and suggested
                and hints.get("expectedDataType") == "select"
                and response not in suggested):
            report.warn("RESPONSE_OUTSIDE_SUGGESTED_VALUES", f"{path}/response",
                        "response is not one of the offered values, which is valid",
                        "APR-MODEL-002", "APR-VAL-005")
        declared = hints.get("expectedDataType")
        if isinstance(declared, str) and declared not in data_types():
            # An unrecognised type degrades to a plain text field. It is reported so
            # an author learns, and never as an error, which is what lets the
            # registry grow.
            report.warn("UNREGISTERED_DATA_TYPE", f"{path}/hints/expectedDataType",
                        f"{declared!r} is not in the registry and degrades to a text "
                        f"field", "APR-MODEL-018")
        if isinstance(response, str) and response.strip() and declared in {"number", "currency"} \
                and as_number(response) is None:
            report.warn("RESPONSE_CONTRADICTS_TYPE", f"{path}/response",
                        "the response is not a number, which is valid: the format never "
                        "validates what a response means",
                        "APR-MODEL-002", "APR-VAL-005")
        if isinstance(response, str) and as_number(response) is not None:
            for bound, worse in (("min", float.__lt__), ("max", float.__gt__)):
                limit = hints.get(bound)
                if isinstance(limit, (int, float)) and not isinstance(limit, bool) \
                        and worse(as_number(response), float(limit)):
                    report.warn("RESPONSE_OUTSIDE_BOUNDS", f"{path}/response",
                                f"the response is outside {bound}, which is still valid: "
                                f"a bound is an offer, not a limit", "APR-MODEL-003")

        temporal = hints.get("expectedDataType") in {"date", "time", "datetime"}
        for bound in ("min", "max"):
            if temporal and bound in hints and not isinstance(hints[bound], str):
                report.error("WRONG_TYPE", f"{path}/hints/{bound}",
                             f"a {hints['expectedDataType']} bound has no JSON type, so "
                             f"it is a string in that type's canonical form",
                             "APR-REP-016")
        pattern = hints.get("validationPattern")
        if isinstance(pattern, str):
            # A pattern that will not compile is unusable whether or not anyone has
            # answered yet, so this is checked before the response is consulted.
            try:
                compiled = re.compile(pattern)
            except re.error:
                compiled = None
                report.warn("HINT_UNUSABLE", f"{path}/hints/validationPattern",
                            "validationPattern is not a usable regular expression; "
                            "a hint a reader cannot use is never an error",
                            "APR-MODEL-039", "APR-MODEL-116")
            if compiled is not None and isinstance(response, str) and response \
                    and not compiled.search(response):
                report.warn("RESPONSE_PATTERN_MISMATCH", f"{path}/response",
                            "response does not match the advisory pattern, "
                            "which is valid", "APR-VAL-005", "APR-VAL-002", "APR-MODEL-003")


def check_section(report: Report, section, path, members, ids, roles, depth) -> None:
    if not isinstance(section, dict):
        report.error("WRONG_TYPE", path, "a section must be an object")
        return
    check_object(report, section, "section", path, members)
    identifier = section.get("id")
    if isinstance(identifier, str) and identifier.strip():
        if identifier in ids["section"]:
            report.error("DUPLICATE_ID", f"{path}/id",
                         f"section id {identifier!r} is already used",
                         "APR-MODEL-010", "APR-MODEL-011")
        ids["section"].add(identifier)

    prompts = section.get("prompts") or []
    children = section.get("sections") or []
    if not prompts and not children:
        report.error("EMPTY_SECTION", path,
                     "a section must carry at least one prompt or child section",
                     "APR-MODEL-009")

    role = section.get("role")
    if isinstance(role, str) and roles and role not in roles:
        report.warn("UNDECLARED_ROLE", f"{path}/role", f"role {role!r} is not declared; a validator may warn about "
                    f"one and must not reject it",
                    "APR-MODEL-051")

    if section.get("kind") == "table":
        if not children:
            report.error("EMPTY_TABLE", path,
                         "a table carries at least one instance; a table's cells live "
                         "in its instances, so prompts alone are not a table",
                         "APR-MODEL-046")
        cap = section.get("maxRows")
        if isinstance(cap, int) and not isinstance(cap, bool) and cap < 1:
            report.error("WRONG_TYPE", f"{path}/maxRows",
                         "an advisory cap of fewer than one instance is not a cap",
                         "APR-MODEL-047")
        if isinstance(cap, int) and not isinstance(cap, bool) and len(children) > cap:
            report.warn("TABLE_OVER_CAPACITY", path,
                        f"{len(children)} instances exceed the advisory cap of {cap}",
                        "APR-MODEL-099")
        shapes = {len(child.get("prompts") or []) for child in children
                  if isinstance(child, dict)}
        if len(shapes) > 1:
            report.warn("TABLE_RAGGED", path,
                        "instances disagree in prompt count, which is still valid",
                        "APR-MODEL-014", "APR-MODEL-100")
        labels = [tuple((p or {}).get("label") for p in (child.get("prompts") or []))
                  for child in children if isinstance(child, dict)]
        if len(set(labels)) > 1:
            report.warn("TABLE_LABEL_MISMATCH", path,
                        "instances disagree in the label at some position", "APR-MODEL-014", "APR-MODEL-100")
    elif "maxRows" in section or "canAddRows" in section:
        report.warn("TABLE_MEMBERS_ON_A_PLAIN_SECTION", path,
                    "a table is never inferred; these members are preserved and ignored",
                    "APR-MODEL-038", "APR-MODEL-097")

    for index, prompt in enumerate(prompts):
        check_prompt(report, prompt, f"{path}/prompts/{index}", members, ids, roles)
    for index, child in enumerate(children):
        check_section(report, child, f"{path}/sections/{index}", members, ids, roles, depth + 1)


ATTESTATION_MEMBERS = {"recordType", "aprVersion", "subject", "scope", "manifest",
                       "proofs", "witnesses"}
CLOSED = {"subject": {"digest", "canonicalization"},
          "manifest": {"root", "entries"}}


def validate_attestation(report: Report, record) -> None:
    """An attestation is a record with its own rules, and they were unenforced."""
    if record.get("recordType") != "attestation":
        report.error("WRONG_TYPE", "/recordType",
                     "recordType must be exactly 'attestation'", "APR-ATTEST-001")
    version = record.get("aprVersion")
    if version is None:
        report.error("REQUIRED_FIELD", "/aprVersion", "aprVersion is required",
                     "APR-ATTEST-002")
    elif version != FORMAT_VERSION:
        report.error("UNSUPPORTED_VERSION", "/aprVersion",
                     f"{version!r} is not exactly {FORMAT_VERSION!r}", "APR-ATTEST-002")

    for name in ("subject", "scope", "manifest", "proofs", "witnesses"):
        if name not in record:
            report.error("REQUIRED_FIELD", f"/{name}",
                         f"an attestation must carry {name}", "APR-ATTEST-004")

    # subject, scope, manifest and their entries admit no additional members.
    for name, allowed in CLOSED.items():
        node = record.get(name)
        if isinstance(node, dict):
            for extra in sorted(set(node) - allowed):
                report.error("WRONG_TYPE", f"/{name}/{extra}",
                             f"{name} admits no member {extra!r}", "APR-ATTEST-004")

    subject = record.get("subject")
    if isinstance(subject, dict):
        if subject.get("canonicalization") != "jcs-sha256":
            report.error("WRONG_TYPE", "/subject/canonicalization",
                         "canonicalization must be 'jcs-sha256'", "APR-ATTEST-003")
        digest = subject.get("digest")
        if not (isinstance(digest, str) and aprlib.DIGEST_PATTERN.match(digest)):
            report.error("WRONG_TYPE", "/subject/digest",
                         "a digest is 'sha256:' and 64 lowercase hex characters",
                         "APR-DIGEST-001")

    manifest = record.get("manifest")
    if isinstance(manifest, dict):
        root = manifest.get("root")
        if not (isinstance(root, str) and aprlib.DIGEST_PATTERN.match(root)):
            report.error("WRONG_TYPE", "/manifest/root",
                         "a digest is 'sha256:' and 64 lowercase hex characters",
                         "APR-DIGEST-001")
        entries = manifest.get("entries")
        if isinstance(entries, list):
            paths = [e.get("path") for e in entries if isinstance(e, dict)]
            if paths != sorted(paths):
                report.error("WRONG_TYPE", "/manifest/entries",
                             "manifest entries must be ordered by path",
                             "APR-DIGEST-003")
            if len(set(paths)) != len(paths):
                report.error("WRONG_TYPE", "/manifest/entries",
                             "manifest entries must not repeat a path",
                             "APR-DIGEST-003")
            if paths and "" not in paths:
                report.error("REQUIRED_FIELD", "/manifest/entries",
                             "a manifest carrying entries must carry the root pointer",
                             "APR-DIGEST-004")

    subject_digest = (record.get("subject") or {}).get("digest")
    for index, proof in enumerate(record.get("proofs") or []):
        if not isinstance(proof, dict):
            continue
        for extra in sorted(set(proof) - {"type", "value"}):
            report.error("WRONG_TYPE", f"/proofs/{index}/{extra}",
                         "a proof carries a type and a value; a second copy of the "
                         "subject digest or scope is what an earlier scheme verified "
                         "against instead of the real one",
                         "APR-ATTEST-007")

    for name in sorted(set(record) - ATTESTATION_MEMBERS):
        if "." not in name:
            report.warn("UNPREFIXED_MEMBER", f"/{name}",
                        f"unknown member {name!r} carries no reverse-DNS prefix",
                        "APR-MODEL-029", "APR-MODEL-031")


def validate_form(report: Report, form, members) -> None:
    if form is None:
        report.error("NULL_DOCUMENT", "", "no document")
        return
    if not isinstance(form, dict):
        report.error("WRONG_TYPE", "", "a form must be an object")
        return

    version_member = next((m for m in members["form"] if m.lower().endswith("version")), None)
    version = form.get(version_member) if version_member else None
    # Absence is REQUIRED_FIELD, which the member-table pass below already reports.
    if version is not None and version != FORMAT_VERSION:
        report.error("UNSUPPORTED_VERSION", f"/{version_member}",
                     f"{version!r} is not exactly {FORMAT_VERSION!r}", "APR-SEC-002")

    check_object(report, form, "form", "", members)

    metadata = form.get("metadata")
    if isinstance(metadata, dict):
        check_object(report, metadata, "metadata", "/metadata", members)
        template = metadata.get("templateId")
        if isinstance(template, str) and not re.match(r"^[A-Za-z][A-Za-z0-9+.-]*:", template):
            report.error("WRONG_TYPE", "/metadata/templateId",
                         "a templateId is a URI; it identifies, and need not resolve",
                         "APR-MODEL-036")
        if form.get("documentType") == "filledForm" and not metadata.get("templateId"):
            report.error("REQUIRED_FIELD", "/metadata/templateId",
                         "a filled form must name the template it answers", "APR-MODEL-008")
        for index, url in enumerate(metadata.get("submissionUrls") or []):
            if isinstance(url, str) and url.split(":", 1)[0].lower() not in {"https", "mailto"}:
                report.warn("SUBMISSION_URL_UNSUPPORTED",
                            f"/metadata/submissionUrls/{index}",
                            "only https and mailto targets are defined", "APR-MODEL-091")
        for index, reference in enumerate(metadata.get("regarding") or []):
            if not (isinstance(reference, str) and aprlib.DIGEST_PATTERN.match(reference)):
                report.error("WRONG_TYPE", f"/metadata/regarding/{index}",
                             "a reference is a sha256: digest of a record", "APR-MODEL-043")

    def nulls(node, path: str) -> None:
        if isinstance(node, dict):
            for key, value in node.items():
                where = f"{path}/{key}"
                # A prompt's response is the one place a reader tolerates null,
                # coercing it to the empty string. Anywhere else it is not a value.
                if value is None and key != "response":
                    report.error("WRONG_TYPE", where,
                                 "null is not an APR value outside a response position",
                                 "APR-REP-014")
                nulls(value, where)
        elif isinstance(node, list):
            for index, value in enumerate(node):
                if value is None:
                    report.error("WRONG_TYPE", f"{path}/{index}",
                                 "null is not an APR value outside a response position",
                                 "APR-REP-014")
                nulls(value, f"{path}/{index}")

    nulls(form, "")

    sections = form.get("sections")
    if not isinstance(sections, list) or not sections:
        report.error("REQUIRED_FIELD", "/sections", "a form must carry at least one section", "APR-MODEL-005")
        return
    for index, role in enumerate(form.get("roles") or []):
        where = f"/roles/{index}"
        if not isinstance(role, dict):
            report.error("WRONG_TYPE", where, "a role entry is an object", "APR-MODEL-026")
        elif "id" in role and not isinstance(role["id"], str):
            report.error("WRONG_TYPE", f"{where}/id", "a role id is a string", "APR-MODEL-026")
        elif not (role.get("id") or "").strip():
            report.error("REQUIRED_FIELD", f"{where}/id", "a role entry names its id", "APR-MODEL-026")
    roles = {r.get("id") for r in (form.get("roles") or []) if isinstance(r, dict)}
    ids = {"section": set(), "prompt": set()}
    for index, section in enumerate(sections):
        check_section(report, section, f"/sections/{index}", members, ids, roles, 1)


def validate_file(path: pathlib.Path, members) -> Report:
    report = Report(str(path))
    try:
        records = aprlib.read_file(path)
    except aprlib.MissingDependency:
        raise  # a missing package is an environment fault, never a bad document
    except aprlib.AprError as exc:
        report.error("PARSE_ERROR", "", str(exc))
        return report
    except Exception as exc:  # noqa: BLE001 - any parse failure is a parse error
        report.error("PARSE_ERROR", "", f"{type(exc).__name__}: {exc}")
        return report
    if not records:
        report.error("NULL_DOCUMENT", "", "no document")
        return report
    for index, record in enumerate(records):
        prefix = f"[{index}]" if len(records) > 1 else ""
        sub = Report(report.where)
        if isinstance(record, dict) and "recordType" in record:
            validate_attestation(sub, record)
        else:
            validate_form(sub, record, members)
        for finding in sub.findings:
            finding["path"] = prefix + finding["path"]
            report.findings.append(finding)
    return report


def validate_spec_examples(members) -> int:
    """Hold every executable example in the specification to this validator.

    An example marked valid that this rejects means one of the two is wrong, and
    an example marked reject that this accepts means the validator is missing a
    rule the specification states. Both are worth knowing.
    """
    vectors = json.loads(
        (ROOT / "tests" / "Conformance" / "beta6" / "spec-examples.json")
        .read_text(encoding="utf-8"))
    problems: list[str] = []
    checked = 0
    for example in vectors["examples"]:
        if example["representation"].endswith("stream"):
            continue  # framing, not document validity
        try:
            records = aprlib.read_records(
                example["document"],
                "yaml" if example["representation"] == "yaml" else "jsonc")
        except aprlib.MissingDependency:
            raise  # a missing package is an environment fault, never a bad example
        except Exception:  # noqa: BLE001
            records = None
        report = Report(example["id"])
        if records is None:
            report.error("PARSE_ERROR", "", "will not parse")
        else:
            # A stream holding no record holds no document, which validate_form reports.
            for record in records or [None]:
                if not aprlib.is_attestation(record):
                    validate_form(report, record, members)
        checked += 1
        if example["expect"] == "valid" and report.errors:
            problems.append(f"{example['id']}: the specification marks this valid, but "
                            f"{report.findings[0]['code']} at "
                            f"{report.findings[0]['path'] or '/'}")
        elif example["expect"] == "reject" and not report.errors:
            problems.append(f"{example['id']}: the specification marks this rejected, "
                            f"expecting {example.get('diagnostic', '?')}, but nothing here "
                            f"objects")
        elif example.get("warns"):
            raised = {f["code"] for f in report.findings if f["severity"] != "error"}
            missing = [code for code in example["warns"] if code not in raised]
            if missing:
                problems.append(f"{example['id']}: the specification says this warns "
                                f"{', '.join(missing)}, but nothing here reports it")
    print(f"specification examples validated: {checked}")
    if problems:
        print(f"\n{len(problems)} DISAGREEMENT(S):")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("Every executable example agrees with this validator.")
    return 0


def enforced_rules() -> set[str]:
    """The rule identifiers this validator's checks declare they enforce.

    Read from this file's own source, so a check that names a rule is counted and
    one that does not is not. A rule counted here is a rule some diagnostic cites,
    which is a claim about implementation, not about test coverage.
    """
    source = pathlib.Path(__file__).read_text(encoding="utf-8")
    body = source.split("def enforced_rules", 1)[0]
    checks = set(re.findall(r"APR-[A-Z]+-\d{3}", body))
    # A representation rule is decided while reading, so it is enforced in the
    # reference library and never reaches a check here. Counting only this file
    # would report those rules as unimplemented when they are the best-covered
    # ones in the format.
    return checks | {rule for rules in aprlib.ENFORCES.values() for rule in rules}


def report_rule_coverage() -> int:
    """Which rules in the OSCAL catalog this validator enforces, and which it does not."""
    catalog = json.loads(
        (ROOT / "docs" / "release" / "apr-oscal-catalog.json").read_text(encoding="utf-8"))
    controls = [(c["props"][0]["value"], c["parts"][0]["prose"])
                for group in catalog["catalog"]["groups"] for c in group["controls"]]
    enforced = enforced_rules()
    unknown = enforced - {rule for rule, _ in controls}
    if unknown:
        print(f"checks cite {sorted(unknown)}, which the catalog does not contain")
        return 1

    covered = [r for r, _ in controls if r in enforced]
    print(f"rules in the OSCAL catalog       {len(controls):>4}")
    print(f"enforced by a check here         {len(covered):>4}")
    print(f"not enforced here                {len(controls) - len(covered):>4}")
    if "--gaps" in sys.argv:
        print("\nNot enforced by this validator:\n")
        for rule, prose in controls:
            if rule not in enforced:
                print(f"  {rule}  {prose.strip()[:96]}")
    else:
        print("\nRun with --gaps to list them. A rule absent here is one this validator "
              "does not check;\nwhether any test would catch that is a separate question, "
              "measured by\nscripts/check-suite-coverage.py.")
    return 0


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    as_json = "--json" in sys.argv
    quiet = "--quiet" in sys.argv
    if "--rule-coverage" in sys.argv:
        return report_rule_coverage()
    if "--spec-examples" in sys.argv:
        return validate_spec_examples(spec_members())
    if not args:
        print(__doc__.strip().split("\n\n")[2])
        return 2

    members = spec_members()
    reports = [validate_file(pathlib.Path(a), members) for a in args]

    if as_json:
        print(json.dumps({
            "formatVersion": FORMAT_VERSION,
            "documents": [{"file": r.where, "valid": r.errors == 0, "findings": r.findings}
                          for r in reports],
        }, indent=2, ensure_ascii=False))
    elif not quiet:
        for report in reports:
            status = "VALID" if report.errors == 0 else "INVALID"
            print(f"{status}  {report.where}")
            for finding in report.findings:
                mark = "E" if finding["severity"] == "error" else "w"
                where = finding["path"] or "/"
                print(f"    {mark} {finding['code']:<34} {where}\n"
                      f"        {finding['message']}")
        total = sum(r.errors for r in reports)
        warnings = sum(len(r.findings) - r.errors for r in reports)
        print(f"\n{len(reports)} document(s): {total} error(s), {warnings} warning(s). "
              f"A warning never affects validity.")
    return 1 if any(r.errors for r in reports) else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except aprlib.MissingDependency as missing:
        # Say what is missing. A gate that reports a document problem it did not
        # find is worse than one that does not run.
        print(f"cannot run: {missing}", file=sys.stderr)
        sys.exit(2)

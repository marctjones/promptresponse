#!/usr/bin/env python3
"""Per-implementation custom-test tasks the portable conformance suite cannot set.

The portable suite (tests/Conformance/beta6) is what every implementation is
scored by, but scripts/check-suite-coverage.py shows it cannot reach every
rule: a renderer obligation, a security posture, or a host behavior is not a
document that is either accepted or refused. A rule gated only inside this
repository is a rule a new implementation can violate and still score full
marks.

This tool closes that loop mechanically. It takes the suite-unreached rules
from `check-suite-coverage.py --json` (single source of truth, not recomputed
here), joins them to the requirement records in `tests/registry.json` (which
say why the suite cannot reach each rule and what evidence exists instead),
and emits the custom-test task list per shipped implementation, in a shape a
code-generation tool can execute directly:

    python3 scripts/conformance-tasks.py                   # human readable
    python3 scripts/conformance-tasks.py --implementation python
    python3 scripts/conformance-tasks.py --json             # machine readable

Scope: requirements holding at least one suite-unreached rule. A requirement
the suite reaches fully needs no custom conformance test (an implementation
still wants its own unit tests, but that is ordinary development, not a
conformance task).

Every task names its evidence kind so a generator knows what to produce:

    unit-test              write the test described in `suggestion`
    already-gated          mirror the named in-repo tests; nothing to invent
    holds-by-construction  assert nothing; the property follows from the API
                           shape (re-check on API change)
    human-review           no test can show it; review the named artifact
    harness-owned          enforced by scripts/run-conformance.py within a run
    unimplemented          the bounded thing does not exist yet; build first
    unclassified           the registry grew a requirement this tool does not
                           know; classify it in TASKS below (exit status is 1
                           until then, so drift is loud, not silent)
"""

from __future__ import annotations

import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
REGISTRY = ROOT / "tests" / "registry.json"
COVERAGE = ROOT / "scripts" / "check-suite-coverage.py"
SPEC = "docs/APR_SPECIFICATION.md"

# Shipped implementation surfaces, per docs/IMPLEMENTATION_REGISTRY.md.
# `tests` is where that implementation's custom tests live.
IMPLEMENTATIONS = [
    {"id": "dotnet", "name": ".NET Core (reference SDK)",
     "kinds": ["reader"], "tests": "tests/PromptResponse.Core.Tests"},
    {"id": "python", "name": "Python SDK",
     "kinds": ["reader"], "tests": "python/tests/"},
    {"id": "typescript", "name": "TypeScript SDK and HTML renderer",
     "kinds": ["reader", "renderer"], "tests": "typescript/src/test/"},
    {"id": "java", "name": "Java SDK",
     "kinds": ["reader"], "tests": "java/src/test/java"},
    {"id": "cli", "name": "CLI",
     "kinds": ["host"], "tests": "tests/PromptResponse.Cli.Tests"},
    {"id": "desktop", "name": "Desktop client",
     "kinds": ["host", "renderer"], "tests": "tests/PromptResponse.Desktop.Tests"},
    {"id": "pdf", "name": "PDF renderer/importer",
     "kinds": ["renderer"], "tests": "tests/PromptResponse.Rendering.Pdf.Tests"},
    {"id": "browser-demo", "name": "Browser demo (local stream viewer)",
     "kinds": ["reader", "renderer"], "tests": "demos/web/src/test/",
     "note": "Thin over the TypeScript SDK; inherits its tasks unless the demo adds behavior."},
]

# One entry per requirement known to hold suite-unreached rules. `applies`
# maps implementation id -> why it applies; an implementation absent from the
# map has no task for that requirement. Keep this table aligned with
# tests/registry.json: anything unmapped is reported as `unclassified`.
TASKS = {
    "REQ-declaring-conformance": {
        "evidence": "harness-owned",
        "applies": {},
        "note": "No per-implementation test: the harness holds a claimed "
                "profile to every case within a run. What remains is human "
                "review of the published claim (tracked in #527).",
    },
    "REQ-conformance-profiles": {
        "evidence": "harness-owned",
        "applies": {},
        "note": "No per-implementation test: the harness refuses a declaration "
                "claiming core+attestations without core+streams, and answering "
                "the core cases is what shows core conformance.",
    },
    "REQ-tables-structural": {
        "evidence": "holds-by-construction",
        "applies": {
            "dotnet": "reader: permission, not obligation",
            "python": "reader: permission, not obligation",
            "typescript": "reader: permission, not obligation",
            "java": "reader: permission, not obligation",
        },
        "note": "A permission to present a table any way, which no document can "
                "violate because the document does not choose the presentation. "
                "Generate no test.",
    },
    "REQ-generated-ids": {
        "evidence": "unit-test",
        "applies": {
            "dotnet": "reader must reproduce the worked id digests",
            "python": "reader must reproduce the worked id digests",
            "typescript": "reader must reproduce the worked id digests",
            "java": "reader must reproduce the worked id digests",
        },
        "suggestion": "Add a digest vector for the three worked ids beside the "
                       "digest fixtures, asserting the generation algorithm "
                       "produces exactly those digests.",
        "done_when": "The vector passes and the ids match across implementations.",
    },
    "REQ-table-assertion": {
        "evidence": "holds-by-construction",
        "applies": {},
        "note": "A recommendation about id spelling; no document is wrong for "
                "ignoring it. The editor's tests already assert the cells it "
                "creates are named {instanceId}.{columnId}. Generate no test.",
    },
    "REQ-any-string": {
        "evidence": "already-gated",
        "applies": {
            "dotnet": "mirror the named tests",
            "python": "mirror the named tests",
            "typescript": "mirror the named tests",
            "java": "mirror the named tests",
        },
        "note": "Already covered in-repo; mirror rather than invent.",
    },
    "REQ-ordering": {
        "evidence": "already-gated",
        "applies": {
            "typescript": "renderer case order-is-document-order and own-prompts-before-child-sections",
            "desktop": "same renderer cases through the Avalonia client",
            "pdf": "same ordering through export",
        },
        "note": "Scored by renderer cases; answer them through your renderer.",
    },
    "REQ-export-no-writeback": {
        "evidence": "already-gated",
        "applies": {
            "typescript": "renderer case an-export-is-not-written-back",
            "desktop": "same renderer case through the Avalonia client",
            "pdf": "same property through export",
        },
        "note": "Scored by a renderer case; answer it through your renderer.",
    },
    "REQ-no-media-type": {
        "evidence": "unit-test",
        "applies": {
            "desktop": "filenames are a desktop affordance: extension-vs-content, save-as pairs, templateId on convert",
            "cli": "same filename behavior wherever the CLI touches the filesystem",
        },
        "suggestion": "Cover which extension/documentType pairs disagree, that "
                       "converting to a filled form records a templateId, that "
                       "content (never the extension) decides representation, "
                       "and that submission paths are exercised without sending. "
                       "See SaveAsRespectsDocumentTypeTests and the submission tests.",
        "done_when": "Filename-driven behavior is asserted without any document vector.",
    },
    "REQ-offline-read": {
        "evidence": "unit-test",
        "applies": {
            "dotnet": "nesting tests exist; add size/stream bounds once defined",
            "python": "mirror the .NET nesting tests; add size/stream bounds once defined",
            "typescript": "mirror the .NET nesting tests; add size/stream bounds once defined",
            "java": "mirror the .NET nesting tests; add size/stream bounds once defined",
        },
        "suggestion": "Refuse an over-nested document with a parse error, not a "
                       "crash. For size and stream length no bound exists yet: "
                       "bounds are implementation-defined, so define one, then "
                       "assert that crossing it refuses cleanly (the shape "
                       "APR-SEC-019 already has for nesting).",
        "done_when": "Some bound exists per reader and crossing it refuses cleanly.",
    },
    "REQ-expr-invariants": {
        "evidence": "human-review",
        "applies": {
            "dotnet": "inspect the evaluator host for filesystem/network/process/clock/randomness",
            "python": "inspect the evaluator host for filesystem/network/process/clock/randomness",
            "typescript": "inspect the evaluator host for filesystem/network/process/clock/randomness",
            "java": "inspect the evaluator host for filesystem/network/process/clock/randomness",
        },
        "note": "Purity (no filesystem, network, process, clock, randomness) "
                "cannot be reached by a document: a document cannot ask for a "
                "capability the language does not have. Proving absence needs "
                "inspection of the host, not a case.",
    },
    "REQ-expr-context": {
        "evidence": "human-review",
        "applies": {
            "desktop": "review what the client places in ctx for a real filler",
            "cli": "review what the CLI places in ctx for a real run",
        },
        "note": "A document cannot show what a host placed in ctx: every case "
                "supplies ctx itself. Closing this is a review of the actual "
                "embedding, and no test asserts the absence of a class of value.",
    },
    "REQ-expr-authoring": {
        "evidence": "unit-test",
        "applies": {
            "dotnet": "checker exists (FormExpressionContext.Check); assert position info in diagnostics",
            "python": "no authoring-time checker; add the entry point, then the tests",
            "typescript": "no authoring-time checker; add the entry point, then the tests",
            "java": "no authoring-time checker; add the entry point, then the tests",
        },
        "suggestion": "Expose an authoring-time check compiling an expression "
                       "against the document type environment (see "
                       "Check_ReportsTypeErrorsAtAuthoringTime). Assert a sound "
                       "expression yields no diagnostics, a bool-condition type "
                       "error and an undeclared reference each yield some, and "
                       "every diagnostic carries position information.",
        "done_when": "All four SDKs expose the entry point with positioned diagnostics.",
    },
    "REQ-expr-limits": {
        "evidence": "unimplemented",
        "applies": {
            "dotnet": "no size/complexity/budget bound exists; define one",
            "python": "no size/complexity/budget bound exists; define one",
            "typescript": "no size/complexity/budget bound exists; define one",
            "java": "no size/complexity/budget bound exists; define one",
        },
        "suggestion": "Define implementation-defined bounds on expression size, "
                       "complexity and evaluation cost, then assert that some "
                       "bound exists and that crossing it applies the fallback "
                       "rather than mutating in part. (CEL terminating by "
                       "construction is not the same claim.)",
        "done_when": "Bounds exist per implementation and the fallback test passes.",
    },
    "REQ-validator-rules": {
        "evidence": "unit-test",
        "applies": {
            "dotnet": "assert the parse/validation stage taxonomy if stages are separate",
            "python": "assert the parse/validation stage taxonomy if stages are separate",
            "typescript": "assert the parse/validation stage taxonomy if stages are separate",
            "java": "assert the parse/validation stage taxonomy if stages are separate",
        },
        "suggestion": "If the reader separates parse from validation stages, "
                       "assert a case is refused at the specified stage. If it "
                       "does not, both stages answer reject and there is nothing "
                       "to assert: record that instead of generating a test.",
        "done_when": "The stage behavior is either asserted or recorded as answer-equivalent.",
    },
}


def load_unreached() -> set[str]:
    """Suite-unreached rules, from the coverage tool's machine output."""
    proc = subprocess.run(
        [sys.executable, str(COVERAGE), "--json"],
        capture_output=True, text=True, cwd=str(ROOT))
    if proc.returncode not in (0, 1):
        # The coverage tool returns 1 only for uncited cases; anything else
        # (including 2 for a crash) means its answer is not trustworthy.
        raise SystemExit(f"check-suite-coverage.py failed:\n{proc.stderr}")
    try:
        return set(json.loads(proc.stdout)["unreached"])
    except (json.JSONDecodeError, KeyError) as exc:
        raise SystemExit(f"could not read coverage output: {exc}") from exc


# Implementations that add no format behavior of their own inherit another
# implementation's tasks instead of duplicating them.
INHERITS = {"browser-demo": "typescript"}


def build_tasks() -> tuple[list[dict], list[str]]:
    """(tasks, unclassified-requirement-ids)."""
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    unreached = load_unreached()
    by_id = {r.get("id"): r for r in registry.get("requirements", [])}

    tasks: list[dict] = []
    unclassified: list[str] = []
    for req_id, requirement in sorted(by_id.items()):
        rules = requirement.get("rules", []) or []
        missing = sorted(set(rules) & unreached)
        if not missing:
            continue
        reached = sorted(set(rules) - unreached)
        known = TASKS.get(req_id)
        if known is None:
            unclassified.append(req_id)
            tasks.append({
                "requirement": req_id,
                "section": requirement.get("section", ""),
                "statement": requirement.get("statement", ""),
                "rules": sorted(rules),
                "suite_reached": reached,
                "suite_unreached": missing,
                "evidence": "unclassified",
                "applies": {},
                "references": requirement.get("gates", []) or [],
                "guidance": requirement.get("gap", ""),
                "note": "The registry grew a requirement this tool does not "
                        "classify. Classify it in TASKS.",
            })
            continue
        tasks.append({
            "requirement": req_id,
            "section": requirement.get("section", ""),
            "statement": requirement.get("statement", ""),
            "rules": sorted(rules),
            "suite_reached": reached,
            "suite_unreached": missing,
            "evidence": known["evidence"],
            "applies": dict(known.get("applies", {})),
            "references": requirement.get("gates", []) or [],
            "guidance": requirement.get("gap", ""),
            "suggestion": known.get("suggestion", ""),
            "done_when": known.get("done_when", ""),
            "note": known.get("note", ""),
        })
    for task in tasks:
        for heir, parent in INHERITS.items():
            if parent in task["applies"] and heir not in task["applies"]:
                task["applies"][heir] = f"via {parent}: {task['applies'][parent]}"
    return tasks, unclassified


def render_human(tasks: list[dict], only: str | None) -> str:
    impls = [i for i in IMPLEMENTATIONS if only is None or i["id"] == only]
    if only is not None and not impls:
        raise SystemExit(
            f"unknown implementation {only!r}; choose from "
            + ", ".join(i["id"] for i in IMPLEMENTATIONS))
    out: list[str] = []
    for impl in impls:
        mine = [t for t in tasks if impl["id"] in t["applies"]]
        out.append(f"## {impl['name']}  (tests: {impl['tests']})")
        if impl.get("note"):
            out.append(f"  {impl['note']}")
        if not mine:
            out.append("  no custom conformance tasks: the portable suite decides.")
            out.append("")
            continue
        out.append(f"  {len(mine)} custom task(s):")
        for task in mine:
            out.append(f"\n  [{task['evidence']}] {task['requirement']}")
            out.append(f"    rules: {', '.join(task['suite_unreached'])}")
            out.append(f"    spec:  {SPEC}#{task['section']}")
            out.append(f"    why:   {impl['id']}: {task['applies'][impl['id']]}")
            for key in ("suggestion", "done_when", "note"):
                if task.get(key):
                    out.append(f"    {key}: {task[key]}")
            refs = [f"{g.get('type')}:{g.get('ref')}" for g in task["references"]]
            if refs:
                out.append(f"    see:   {', '.join(refs[:6])}")
        out.append("")
    harness = [t for t in tasks if t["evidence"] == "harness-owned"]
    if harness and only is None:
        out.append("## Conformance harness (not per-implementation)")
        for task in harness:
            out.append(f"  [{task['requirement']}] {', '.join(task['suite_unreached'])}: "
                       f"{task['note'] or task['guidance'][:120]}")
    return "\n".join(out)


def main() -> int:
    args = sys.argv[1:]
    tasks, unclassified = build_tasks()
    if "--json" in args:
        print(json.dumps({
            "implementations": IMPLEMENTATIONS,
            "tasks": tasks,
            "unclassified": unclassified,
        }, indent=2))
        return 1 if unclassified else 0
    only = None
    if "--implementation" in args:
        only = args[args.index("--implementation") + 1]
    print(render_human(tasks, only))
    if unclassified:
        print("UNCLASSIFIED requirements (classify in TASKS): "
              + ", ".join(unclassified))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

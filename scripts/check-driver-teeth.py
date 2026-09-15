#!/usr/bin/env python3
"""Prove each SDK's conformance driver fails when it stops asking its SDK.

`check-harness-teeth.py` proves the harness fails a broken driver, by damaging the
reference driver. The real drivers are separate programs: a thin adapter per SDK that
turns what the library returned into the harness protocol. A defect in that adapter,
such as a driver that stops passing documents to its validator and reports everything
it parsed as valid, is invisible to the harness teeth, because they never run it.

For each driver this builds two copies the same way. The control is undamaged and must
score as cleanly as the real driver, which proves the copying changed nothing. The
mutant drops the validator's errors on the floor and must fail cases. A mutant that
still passes is the bug: in the driver's evidence, not in the mutant.

    python3 scripts/check-driver-teeth.py
    python3 scripts/check-driver-teeth.py --drivers python,typescript,java
    python3 scripts/check-driver-teeth.py --drivers dotnet-library,dotnet-cli
"""
from __future__ import annotations

import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
HARNESS = ROOT / "scripts" / "run-conformance.py"
# Under obj/, which git ignores, so a .NET copy still finds the repository's
# Directory.Build.props, nuget.config and global.json by walking up.
DOTNET_SCRATCH = ROOT / "obj" / "driver-teeth"


def once(text: str, old: str, new: str, where: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{where}: expected the mutation target exactly once, found {count}: {old!r}")
    return text.replace(old, new)


def python_driver(workspace: pathlib.Path, mutate: bool) -> str:
    source = (ROOT / "python" / "conformance_driver.py").read_text(encoding="utf-8")
    source = once(source, "sys.path.insert(0, str(Path(__file__).resolve().parent))",
                  f"sys.path.insert(0, {str(ROOT / 'python')!r})", "python driver")
    if mutate:
        source = once(source, "all_findings.extend(report.errors)", "pass", "python driver")
    path = workspace / "conformance_driver.py"
    path.write_text(source, encoding="utf-8")
    return f"{ROOT / 'python' / '.venv' / 'bin' / 'python'} {path}"


def typescript_driver(workspace: pathlib.Path, mutate: bool) -> str:
    source = (ROOT / "typescript" / "conformance-driver.mjs").read_text(encoding="utf-8")
    source = once(source, '"./dist/index.js"',
                  json.dumps((ROOT / "typescript" / "dist" / "index.js").as_uri()), "typescript driver")
    if mutate:
        source = once(source, "allErrors.push(...validate(record.document).errors)",
                      "validate(record.document)", "typescript driver")
    path = workspace / "conformance-driver.mjs"
    path.write_text(source, encoding="utf-8")
    return f"node {path}"


def java_tool(name: str) -> str:
    home = os.environ.get("JAVA_HOME")
    return str(pathlib.Path(home) / "bin" / name) if home else name


_java_classpath: str | None = None


def java_driver(workspace: pathlib.Path, mutate: bool) -> str:
    global _java_classpath
    java = ROOT / "java"
    if _java_classpath is None:
        # What java/run-conformance-driver.sh does before it runs the driver.
        subprocess.run(["./mvnw", "-q", "test-compile", "dependency:build-classpath",
                        "-Dmdep.outputFile=target/classpath.txt", "-Dmdep.includeScope=test"],
                       cwd=java, check=True)
        dependencies = (java / "target" / "classpath.txt").read_text(encoding="utf-8").strip()
        _java_classpath = f"{java / 'target' / 'test-classes'}:{java / 'target' / 'classes'}:{dependencies}"
    relative = pathlib.Path("org") / "promptresponse" / "AprConformanceDriver.java"
    source = (java / "src" / "test" / "java" / relative).read_text(encoding="utf-8")
    if mutate:
        source = once(source, "allErrors.addAll(Apr.validate(form.document()).errors());",
                      "Apr.validate(form.document());", "java driver")
    path = workspace / "src" / relative
    path.parent.mkdir(parents=True)
    path.write_text(source, encoding="utf-8")
    classes = workspace / "classes"
    subprocess.run([java_tool("javac"), "-d", str(classes), "-cp", _java_classpath, str(path)], check=True)
    # The copy's class comes first, so it shadows the one the build compiled.
    return f"{java_tool('java')} -cp {classes}:{_java_classpath} org.promptresponse.AprConformanceDriver"


def dotnet_driver(project: pathlib.Path, source_file: str, assembly: str, arguments: str):
    def prepare(workspace: pathlib.Path, mutate: bool) -> str:
        copy = DOTNET_SCRATCH / f"{project.name}-{'mutant' if mutate else 'control'}"
        shutil.rmtree(copy, ignore_errors=True)
        shutil.copytree(project, copy / project.name, ignore=shutil.ignore_patterns("bin", "obj"))
        csproj = next((copy / project.name).glob("*.csproj"))
        csproj.write_text(re.sub(
            r'(<ProjectReference Include=")([^"]+)(")',
            lambda m: m.group(1) + str((project / m.group(2).replace("\\", "/")).resolve()) + m.group(3),
            csproj.read_text(encoding="utf-8")), encoding="utf-8")
        if mutate:
            target = copy / project.name / source_file
            target.write_text(once(target.read_text(encoding="utf-8"), "if (!result.IsValid)",
                                   "if (!result.IsValid && result.Errors.Count < 0)", str(source_file)),
                              encoding="utf-8")
        output = copy / "out"
        subprocess.run(["dotnet", "build", str(csproj), "-nologo", "-v", "q", "-o", str(output)], check=True)
        return f"dotnet {output / assembly}{arguments}"
    return prepare


DRIVERS = {
    "python": python_driver,
    "typescript": typescript_driver,
    "java": java_driver,
    "dotnet-library": dotnet_driver(ROOT / "tools" / "PromptResponse.ConformanceDriver", "Program.cs",
                                    "PromptResponse.ConformanceDriver.dll", ""),
    "dotnet-cli": dotnet_driver(ROOT / "src" / "PromptResponse.Cli", "Commands/ConformanceCommand.cs",
                                "apr.dll", " conformance"),
}


def real_command(name: str) -> str:
    """The command CI scores the undamaged driver with, run from the repository."""
    return {
        "python": f"{ROOT / 'python' / '.venv' / 'bin' / 'python'} {ROOT / 'python' / 'conformance_driver.py'}",
        "typescript": f"node {ROOT / 'typescript' / 'conformance-driver.mjs'}",
        "java": str(ROOT / "java" / "run-conformance-driver.sh"),
        "dotnet-library": f"dotnet run --project {ROOT / 'tools' / 'PromptResponse.ConformanceDriver'} --no-build -v q",
        "dotnet-cli": f"dotnet run --project {ROOT / 'src' / 'PromptResponse.Cli'} --no-build -v q -- conformance",
    }[name]


def score(command: str) -> dict | None:
    completed = subprocess.run([sys.executable, str(HARNESS), "--driver", command, "--json"],
                               capture_output=True, text=True, cwd=ROOT)
    try:
        return json.loads(completed.stdout)["tally"]
    except (json.JSONDecodeError, KeyError):
        return None


def main() -> int:
    selected = list(DRIVERS)
    if "--drivers" in sys.argv:
        selected = sys.argv[sys.argv.index("--drivers") + 1].split(",")
        unknown = [name for name in selected if name not in DRIVERS]
        if unknown:
            raise SystemExit(f"unknown drivers {unknown}; choose from {list(DRIVERS)}")

    problems: list[str] = []
    try:
        with tempfile.TemporaryDirectory() as scratch:
            for name in selected:
                tallies = {}
                for role in ("control", "mutant"):
                    workspace = pathlib.Path(scratch) / name / role
                    workspace.mkdir(parents=True)
                    tallies[role] = score(DRIVERS[name](workspace, role == "mutant"))
                control, mutant = tallies["control"], tallies["mutant"]
                if control is None or mutant is None:
                    problems.append(f"{name}: a copy did not run under the harness "
                                    f"(control {'ran' if control else 'failed'}, mutant {'ran' if mutant else 'failed'})")
                    continue
                # The control must score exactly as the real driver does. A driver may warn
                # about more than a case names (APR-VAL-002), so a discrepancy is not itself
                # damage; a count that differs from the real driver's is.
                real = score(real_command(name)) or {}
                if control["fail"] or control.get("discrepancy", 0) != real.get("discrepancy", 0):
                    problems.append(f"{name}: the undamaged copy fails {control['fail']} cases with "
                                    f"{control.get('discrepancy', 0)} discrepancies where the real driver "
                                    f"reports {real.get('discrepancy', 0)}, so the mutant's failures cannot "
                                    "be told from the copying")
                if mutant["fail"] <= control["fail"]:
                    problems.append(f"{name}: a driver that ignores its validator still fails only "
                                    f"{mutant['fail']} cases. Nothing scores what this driver's validation reports.")
                print(f"  {name:16} control {control['pass']:>4} pass {control['fail']:>3} fail   "
                      f"mutant {mutant['pass']:>4} pass {mutant['fail']:>3} fail   validator errors ignored")
    finally:
        shutil.rmtree(DOTNET_SCRATCH, ignore_errors=True)

    if problems:
        print(f"\n{len(problems)} PROBLEM(S):")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("\nEvery driver fails cases once it stops reporting what its validator found, "
          "and every\nundamaged copy scores cleanly.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

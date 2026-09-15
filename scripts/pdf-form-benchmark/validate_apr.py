"""Validate a .aprt file using this repo's own apr CLI -- the same authority
the document-to-apr skill tells a human agent to use. We do not re-implement
schema validation; a derived check that disagreed with the CLI would itself
be the defect (see CLAUDE.md).
"""
import json
import os
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DOTNET_HOME = str(Path.home() / ".dotnet")


def validate(aprt_path: Path) -> dict:
    env = dict(os.environ)
    env["PATH"] = f"{DOTNET_HOME}:{env.get('PATH', '')}"
    try:
        proc = subprocess.run(
            ["dotnet", "run", "--project", "src/PromptResponse.Cli", "--",
             "validate", str(aprt_path)],
            cwd=REPO_ROOT, env=env, capture_output=True, text=True, timeout=120,
        )
    except subprocess.TimeoutExpired:
        return {"valid": False, "errors": [{"code": "TIMEOUT", "message": "CLI validate timed out"}]}
    out = proc.stdout.strip()
    try:
        start = out.find("{")
        return json.loads(out[start:]) if start != -1 else {
            "valid": False, "errors": [{"code": "NO_JSON_OUTPUT", "message": out[-2000:] + proc.stderr[-2000:]}]
        }
    except json.JSONDecodeError as exc:
        return {"valid": False, "errors": [{"code": "UNPARSEABLE_OUTPUT", "message": f"{exc}: {out[-1000:]}"}]}

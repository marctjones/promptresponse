#!/usr/bin/env python3
"""Download the benchmark's PDF form corpus listed in corpus_manifest.json.

Idempotent: skips files that already exist with a non-zero size. Every form
here is a public-domain government form fetched from its own agency's site.
"""
import hashlib
import json
import sys
import urllib.request
from pathlib import Path

HERE = Path(__file__).parent
MANIFEST = HERE / "corpus_manifest.json"
CORPUS_DIR = HERE / "corpus"


def fetch(form: dict) -> None:
    dest = CORPUS_DIR / f"{form['id']}.pdf"
    if dest.exists() and dest.stat().st_size > 0:
        print(f"  skip  {form['id']} (already downloaded)")
        return
    req = urllib.request.Request(form["url"], headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        data = resp.read()
    dest.write_bytes(data)
    sha = hashlib.sha256(data).hexdigest()[:12]
    print(f"  fetched  {form['id']}  ({len(data):,} bytes, sha256:{sha})  <- {form['url']}")


def main() -> int:
    manifest = json.loads(MANIFEST.read_text())
    CORPUS_DIR.mkdir(exist_ok=True)
    failures = []
    for form in manifest["forms"]:
        try:
            fetch(form)
        except Exception as exc:  # noqa: BLE001 - report and continue
            failures.append((form["id"], str(exc)))
            print(f"  FAILED  {form['id']}: {exc}", file=sys.stderr)
    if failures:
        print(f"\n{len(failures)} form(s) failed to download.", file=sys.stderr)
        return 1
    print(f"\nAll {len(manifest['forms'])} forms present in {CORPUS_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

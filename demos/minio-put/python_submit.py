#!/usr/bin/env python3
"""Fill the dog-license template with the real Python SDK and PUT it as APR-YAML.

A third, independent client for demo.sh's end-to-end submission demo (alongside
the real `apr` CLI and the real Avalonia desktop GUI), run with the checked-out
tree's own `python/.venv` -- the same interpreter the conformance driver uses,
not a container. It exercises the YAML writer landed for issue #402: before
that fix, `promptresponse` could read APR-YAML but had no writer for it at all.

Usage:
    python_submit.py <template.aprt> <output.apr.yaml> <presigned-url> <ca-cert>

Exits non-zero (with the server's response body on stderr) if the PUT does not
return 2xx.
"""

import datetime
import pathlib
import ssl
import sys
import urllib.request

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / "python"))

from promptresponse import read_beta6_form, write_beta6_form  # noqa: E402

RESPONSES = {
    "prompt_dog_name": "Nala",
    "prompt_breed": "Australian Shepherd",
    "prompt_owner_name": "Alex Kim",
    "prompt_owner_phone": "+1 (555) 987-6543",
    "prompt_rabies_vaccination_date": datetime.date.today().isoformat(),
}


def main() -> None:
    if len(sys.argv) != 5:
        sys.exit(f"Usage: {sys.argv[0]} <template.aprt> <output.apr.yaml> <presigned-url> <ca-cert>")
    template_path, output_path, presigned_url, ca_cert = sys.argv[1:]

    document = read_beta6_form(pathlib.Path(template_path).read_text(encoding="utf-8"), "jsonc")
    for prompt in document.all_prompts():
        if prompt.id in RESPONSES:
            prompt.response = RESPONSES[prompt.id]
    document.document_type = "filledForm"
    document.metadata.modified = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    yaml_source = write_beta6_form(document, "yaml")
    pathlib.Path(output_path).write_text(yaml_source, encoding="utf-8")
    print(f"Filled form saved to {output_path}")

    # The MinIO instance's certificate is self-signed for this demo only; trust
    # it explicitly here rather than asking the host's own trust store to, the
    # same posture the CLI and GUI legs take (see demo.sh and README.md).
    context = ssl.create_default_context(cafile=ca_cert)
    request = urllib.request.Request(
        presigned_url,
        data=yaml_source.encode("utf-8"),
        method="PUT",
        headers={"Content-Type": "application/vnd.apr+yaml"},
    )
    try:
        with urllib.request.urlopen(request, context=context) as response:
            print(f"Submitted (HTTP {response.status}).")
    except urllib.error.HTTPError as error:
        sys.exit(f"PUT failed: HTTP {error.code}\n{error.read().decode('utf-8', 'replace')}")


if __name__ == "__main__":
    main()

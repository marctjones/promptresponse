#!/usr/bin/env python3
"""A minimal, stateless HTTPS submission receiver.

This stands in for the pre-signed HTTPS PUT target that APR-MODEL-033
(docs/APR_SPECIFICATION.md, section 5.2.1, "Submission targets") defines:
a single HTTP PUT of the complete document to a URL the document itself
names, using the URL verbatim. The spec's rationale for that section says
plainly that "a pre-signed browser POST is deliberately absent" from the
format -- this server exists to make that distinction concrete and
demonstrable rather than theoretical.

What it does:
  - PUT to any path: writes the raw request body to disk under DATA_DIR,
    unmodified, and answers 201 Created. The path is treated as opaque (a
    pre-signed URL's path has no meaning a receiver is required to
    interpret) but is folded into the output filename for traceability.
  - Every other method (GET, POST, HEAD, DELETE, PATCH, OPTIONS, ...):
    405 Method Not Allowed, with an "Allow: PUT" header. This is the point
    of the exercise -- it exists specifically to catch a client that sends
    POST where the spec requires PUT.
  - Content-Type: logged, never validated or rejected. The spec says it
    directly: "What the receiver holds after a PUT is the request body,
    byte for byte: the stream as the client wrote it, already a valid APR
    file. No processing on the receiving side is assumed or permitted to
    be needed." This receiver does not parse or gate on the media type,
    it just says what it saw so a demo run's output is legible.

What it deliberately does not do: authenticate, authorize, persist across
runs, or retain any state beyond the files it writes. The spec's own
rationale for defining no authentication step is that "a pre-signed URL
*is* the authorisation: the grant travels in the query string, so the
client never holds a credential." This toy receiver stands in for that
pre-signed target, so during a demo it accepts anything PUT to it.
"""

from __future__ import annotations

import datetime
import os
import re
import ssl
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DATA_DIR = os.environ.get("DATA_DIR", "/data")
PORT = int(os.environ.get("PORT", "8443"))
CERT_FILE = os.environ.get("CERT_FILE", "/certs/receiver.pem")
KEY_FILE = os.environ.get("KEY_FILE", "/certs/receiver-key.pem")

# Anything not a path/filename-safe character becomes "_". This also
# collapses ".." segments so a malicious or malformed path can't escape
# DATA_DIR -- the path is opaque input, not something the receiver trusts.
_UNSAFE = re.compile(r"[^A-Za-z0-9._-]+")


def _safe_name_from_path(path: str) -> str:
    trimmed = path.split("?", 1)[0].strip("/")
    if not trimmed:
        trimmed = "root"
    safe = _UNSAFE.sub("_", trimmed)
    return safe.strip("._") or "root"


class SubmissionHandler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server_version = "apr-submission-receiver/1.0"

    def _send_plain(self, status: int, body: str, extra_headers: dict | None = None) -> None:
        payload = body.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        for key, value in (extra_headers or {}).items():
            self.send_header(key, value)
        self.end_headers()
        self.wfile.write(payload)

    def _reject_not_put(self) -> None:
        self.log_message(
            "%s rejected: this receiver only accepts PUT (APR-MODEL-033)", self.command
        )
        self._send_plain(
            405,
            f"405 Method Not Allowed: this receiver accepts only PUT.\n"
            f"'{self.command}' is not the submission method APR-MODEL-033 defines.\n",
            extra_headers={"Allow": "PUT"},
        )

    def do_PUT(self) -> None:
        content_length_header = self.headers.get("Content-Length")
        if content_length_header is None:
            self._send_plain(411, "411 Length Required: a PUT body must declare Content-Length.\n")
            return
        try:
            length = int(content_length_header)
        except ValueError:
            self._send_plain(400, "400 Bad Request: Content-Length is not an integer.\n")
            return

        body = self.rfile.read(length)

        content_type = self.headers.get("Content-Type", "(none)")
        timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        name = _safe_name_from_path(self.path)
        filename = f"{timestamp}__{name}"
        out_path = os.path.join(DATA_DIR, filename)

        os.makedirs(DATA_DIR, exist_ok=True)
        with open(out_path, "wb") as handle:
            handle.write(body)

        self.log_message(
            "PUT %s -> %s (%d bytes, Content-Type: %s)",
            self.path, out_path, len(body), content_type,
        )

        self._send_plain(
            201,
            f"201 Created: {len(body)} bytes written.\n"
            f"path: {self.path}\n"
            f"content-type seen (not validated): {content_type}\n"
            f"stored as: {filename}\n",
        )

    def do_GET(self) -> None:
        self._reject_not_put()

    def do_POST(self) -> None:
        self._reject_not_put()

    def do_HEAD(self) -> None:
        self._reject_not_put()

    def do_DELETE(self) -> None:
        self._reject_not_put()

    def do_PATCH(self) -> None:
        self._reject_not_put()

    def do_OPTIONS(self) -> None:
        self._reject_not_put()

    # BaseHTTPRequestHandler only dispatches to do_<VERB> for verbs it
    # already knows about; anything else falls through to its own 501.
    # That is an acceptable outcome for exotic verbs (still "not PUT"),
    # but every verb curl or a real HTTP client would plausibly send is
    # covered explicitly above so the demo's POST check gets a real 405.


def main() -> None:
    os.makedirs(DATA_DIR, exist_ok=True)

    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.load_cert_chain(certfile=CERT_FILE, keyfile=KEY_FILE)

    httpd = ThreadingHTTPServer(("0.0.0.0", PORT), SubmissionHandler)
    httpd.socket = context.wrap_socket(httpd.socket, server_side=True)

    print(f"apr submission receiver: listening on https://0.0.0.0:{PORT}", flush=True)
    print(f"apr submission receiver: writing accepted PUTs under {DATA_DIR}", flush=True)
    print(
        "apr submission receiver: PUT succeeds (201); every other method "
        "fails (405) -- that distinction is the point.",
        flush=True,
    )

    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()


if __name__ == "__main__":
    main()

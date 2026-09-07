#!/usr/bin/env python3
"""Generate a real S3 pre-signed PUT URL against a running MinIO instance.

This is what specification 5.2.1 (APR-MODEL-033) means by an `https`
submission target: "a pre-signed object-store PUT target," using "the S3
pre-signed URL convention." `mc` (MinIO's own CLI) has no plain presigned-PUT
command -- its `share upload` generates a pre-signed *POST* (a form with
policy/signature fields), which is the mechanism this specification's
rationale explicitly rejects. A pre-signed PUT URL is generated the way any
S3 client library does it: boto3's `generate_presigned_url("put_object", ...)`.

Usage:
    uv run --with boto3 python3 presign.py <bucket> <key> [--content-type=TYPE] [--expires=SECONDS]

Requires the MinIO instance built from this directory's Containerfile to be
running (see README.md) with the default demo credentials.
"""

import argparse
import sys

try:
    import boto3
    import urllib3
except ImportError:
    sys.exit(
        "boto3 is required: run this with `uv run --with boto3 python3 presign.py ...`"
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bucket")
    parser.add_argument("key")
    parser.add_argument("--content-type", default="application/vnd.apr+json")
    parser.add_argument("--expires", type=int, default=3600, help="seconds (default: 1 hour)")
    parser.add_argument("--endpoint", default="https://localhost:9000")
    parser.add_argument("--access-key", default="root")
    parser.add_argument("--secret-key", default="password")
    args = parser.parse_args()

    # The demo MinIO container's certificate is self-signed and this script
    # doesn't ask the host to trust it -- see README.md for why, and how the
    # CLI is pointed at it without touching your system trust store either.
    urllib3.disable_warnings()

    client = boto3.client(
        "s3",
        endpoint_url=args.endpoint,
        aws_access_key_id=args.access_key,
        aws_secret_access_key=args.secret_key,
        verify=False,
    )

    url = client.generate_presigned_url(
        "put_object",
        Params={
            "Bucket": args.bucket,
            "Key": args.key,
            "ContentType": args.content_type,
        },
        ExpiresIn=args.expires,
    )
    print(url)


if __name__ == "__main__":
    main()

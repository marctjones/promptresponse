#!/usr/bin/env python3
"""Generate a real S3 pre-signed PUT URL against a real Cloudflare R2 bucket.

The R2 counterpart to demos/minio-put/presign.py -- same boto3 call, same
S3 pre-signed URL convention specification 5.2.1 (APR-MODEL-033) means by an
`https` submission target, pointed at a real hosted bucket instead of a
throwaway local container. Unlike MinIO, R2 has a publicly trusted TLS
certificate, so there is no self-signed-cert dance here: no `verify=False`,
no throwaway trust store for the CLI or GUI to be pointed at.

Usage:
    uv run --with boto3 python3 presign.py <bucket> <key> \\
        --account-id=<cloudflare-account-id> \\
        --access-key=<r2-access-key-id> --secret-key=<r2-secret-access-key> \\
        [--content-type=TYPE] [--expires=SECONDS]

Requires an R2 bucket and an S3 API token (Access Key ID / Secret Access Key
pair) created for it -- see README.md. Reads credentials from
R2_ACCOUNT_ID / R2_ACCESS_KEY_ID / R2_SECRET_ACCESS_KEY if the matching flag
is omitted, so a real secret need not appear on the command line or in shell
history.
"""

import argparse
import os
import sys

try:
    import boto3
except ImportError:
    sys.exit("boto3 is required: run this with `uv run --with boto3 python3 presign.py ...`")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bucket")
    parser.add_argument("key")
    parser.add_argument("--content-type", default="application/vnd.apr+json")
    parser.add_argument("--expires", type=int, default=3600, help="seconds (default: 1 hour; R2's own maximum is 7 days)")
    parser.add_argument(
        "--if-none-match", action="store_true",
        help="Bind If-None-Match: * into the presigned URL's own signature, so the PUT "
             "only validates if the client sends that exact header -- not optional, "
             "not something a careless client can skip. R2 (and S3) then reject the "
             "write with 412 if an object already exists at this key: first write wins, "
             "every later one to the same key fails instead of overwriting it.",
    )
    parser.add_argument("--account-id", default=os.environ.get("R2_ACCOUNT_ID"))
    parser.add_argument("--access-key", default=os.environ.get("R2_ACCESS_KEY_ID"))
    parser.add_argument("--secret-key", default=os.environ.get("R2_SECRET_ACCESS_KEY"))
    args = parser.parse_args()

    missing = [name for name, value in [("--account-id/R2_ACCOUNT_ID", args.account_id), ("--access-key/R2_ACCESS_KEY_ID", args.access_key), ("--secret-key/R2_SECRET_ACCESS_KEY", args.secret_key)] if not value]
    if missing:
        sys.exit(f"Missing: {', '.join(missing)}. See README.md for how to create an R2 API token.")

    client = boto3.client(
        "s3",
        endpoint_url=f"https://{args.account_id}.r2.cloudflarestorage.com",
        aws_access_key_id=args.access_key,
        aws_secret_access_key=args.secret_key,
        region_name="auto",  # required by the SDK; not used by R2 itself
    )

    params = {"Bucket": args.bucket, "Key": args.key, "ContentType": args.content_type}
    if args.if_none_match:
        params["IfNoneMatch"] = "*"

    url = client.generate_presigned_url("put_object", Params=params, ExpiresIn=args.expires)
    print(url)


if __name__ == "__main__":
    main()

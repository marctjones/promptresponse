#!/bin/bash
# Generate S3 Pre-Signed POST Policy for APR Templates
#
# This script helps generate pre-signed POST policies for S3 that can be embedded
# in APR templates to enable direct form submission to S3 buckets.
#
# Usage:
#   ./dev/generate-s3-policy.sh
#
# For MinIO testing, use:
#   Endpoint: http://localhost:9000
#   Access Key: promptresponse
#   Secret Key: promptresponse123
#   Bucket: filled-forms
#   Region: us-east-1

set -e

echo "========================================="
echo "S3 Pre-Signed POST Policy Generator"
echo "========================================="
echo ""
echo "This tool generates a pre-signed POST policy for S3 that can be"
echo "embedded in APR template metadata to enable direct form submission."
echo ""

# Check if AWS CLI or MinIO client is available
if ! command -v aws &> /dev/null && ! command -v ./dev/minio/mc &> /dev/null; then
    echo "Error: Neither AWS CLI nor MinIO client found."
    echo ""
    echo "For MinIO testing, run: ./dev/minio-setup.sh"
    echo "For AWS S3, install AWS CLI: https://aws.amazon.com/cli/"
    exit 1
fi

# Prompt for configuration
read -p "S3 Endpoint URL (default: http://localhost:9000): " ENDPOINT
ENDPOINT=${ENDPOINT:-http://localhost:9000}

read -p "Bucket name (default: filled-forms): " BUCKET
BUCKET=${BUCKET:-filled-forms}

read -p "AWS Access Key ID (default: promptresponse): " ACCESS_KEY
ACCESS_KEY=${ACCESS_KEY:-promptresponse}

read -sp "AWS Secret Access Key (default: promptresponse123): " SECRET_KEY
echo ""
SECRET_KEY=${SECRET_KEY:-promptresponse123}

read -p "AWS Region (default: us-east-1): " REGION
REGION=${REGION:-us-east-1}

read -p "Key prefix (default: filled-forms/): " KEY_PREFIX
KEY_PREFIX=${KEY_PREFIX:-filled-forms/}

read -p "Max file size in MB (default: 10): " MAX_SIZE_MB
MAX_SIZE_MB=${MAX_SIZE_MB:-10}

read -p "Expiration days (default: 7): " EXPIRATION_DAYS
EXPIRATION_DAYS=${EXPIRATION_DAYS:-7}

echo ""
echo "========================================="
echo "Configuration"
echo "========================================="
echo "Endpoint:    $ENDPOINT"
echo "Bucket:      $BUCKET"
echo "Access Key:  $ACCESS_KEY"
echo "Region:      $REGION"
echo "Key Prefix:  $KEY_PREFIX"
echo "Max Size:    ${MAX_SIZE_MB}MB"
echo "Expires:     ${EXPIRATION_DAYS} days"
echo ""

# Calculate expiration timestamp
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    EXPIRATION=$(date -u -v+${EXPIRATION_DAYS}d +"%Y-%m-%dT%H:%M:%SZ")
else
    # Linux
    EXPIRATION=$(date -u -d "+${EXPIRATION_DAYS} days" +"%Y-%m-%dT%H:%M:%SZ")
fi

# Convert MB to bytes
MAX_SIZE_BYTES=$((MAX_SIZE_MB * 1024 * 1024))

# Create policy JSON
POLICY=$(cat <<EOF
{
  "expiration": "$EXPIRATION",
  "conditions": [
    {"bucket": "$BUCKET"},
    ["starts-with", "\$key", "$KEY_PREFIX"],
    {"acl": "private"},
    ["content-length-range", 0, $MAX_SIZE_BYTES]
  ]
}
EOF
)

# Base64 encode the policy
POLICY_B64=$(echo -n "$POLICY" | base64 | tr -d '\n')

# Calculate signature (HMAC-SHA1 for AWS Signature Version 2)
# Note: AWS Signature Version 4 is more complex and requires the aws CLI
# For MinIO, Signature V2 works fine

SIGNATURE=$(echo -n "$POLICY_B64" | openssl dgst -sha1 -hmac "$SECRET_KEY" -binary | base64 | tr -d '\n')

echo "========================================="
echo "Generated Pre-Signed POST Configuration"
echo "========================================="
echo ""
echo "Copy this JSON into your APR template metadata:"
echo ""
cat <<EOF
{
  "submissionConfig": {
    "type": "s3-presigned-post",
    "url": "$ENDPOINT/",
    "fields": {
      "key": "${KEY_PREFIX}\${filename}",
      "AWSAccessKeyId": "$ACCESS_KEY",
      "policy": "$POLICY_B64",
      "signature": "$SIGNATURE",
      "acl": "private"
    },
    "expiresAt": "$EXPIRATION"
  }
}
EOF

echo ""
echo "========================================="
echo ""
echo "Note: This policy is valid until $EXPIRATION"
echo ""
echo "To test submission:"
echo "1. Add the above submissionConfig to your template metadata"
echo "2. Open the template in PromptResponse Desktop"
echo "3. Fill out the form"
echo "4. Click 'Submit to S3' button"
echo ""
echo "To verify submission:"
echo "  MinIO Console: http://localhost:9001"
echo "  Username: promptresponse"
echo "  Password: promptresponse123"
echo ""

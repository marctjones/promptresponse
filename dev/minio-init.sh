#!/bin/bash
# Initialize MinIO with buckets and test configuration
# Creates test buckets for S3 pre-signed POST testing
#
# Usage: ./dev/minio-init.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MINIO_DIR="$SCRIPT_DIR/minio"
MINIO_CLIENT="$MINIO_DIR/mc"

# MinIO configuration
MINIO_ENDPOINT="http://localhost:9000"
MINIO_USER="promptresponse"
MINIO_PASSWORD="promptresponse123"
MINIO_ALIAS="local"

# Bucket names
BUCKET_FILLED_FORMS="filled-forms"
BUCKET_TEMPLATES="templates"
BUCKET_TEST="test-uploads"

echo "========================================="
echo "Initializing MinIO for PromptResponse"
echo "========================================="
echo ""

# Check if MinIO client exists
if [ ! -f "$MINIO_CLIENT" ]; then
    echo "Error: MinIO client (mc) not found!"
    echo "Please run: ./dev/minio-setup.sh"
    exit 1
fi

# Check if MinIO is running
if ! curl -s "$MINIO_ENDPOINT/minio/health/live" > /dev/null 2>&1; then
    echo "Error: MinIO is not running!"
    echo "Please run: ./dev/minio-start.sh"
    exit 1
fi

echo "Configuring MinIO client..."
"$MINIO_CLIENT" alias set "$MINIO_ALIAS" "$MINIO_ENDPOINT" "$MINIO_USER" "$MINIO_PASSWORD" > /dev/null 2>&1
echo "✓ MinIO client configured (alias: $MINIO_ALIAS)"
echo ""

# Create buckets
echo "Creating buckets..."

for BUCKET in "$BUCKET_FILLED_FORMS" "$BUCKET_TEMPLATES" "$BUCKET_TEST"; do
    if "$MINIO_CLIENT" ls "$MINIO_ALIAS/$BUCKET" > /dev/null 2>&1; then
        echo "  ✓ Bucket '$BUCKET' already exists"
    else
        "$MINIO_CLIENT" mb "$MINIO_ALIAS/$BUCKET"
        echo "  ✓ Created bucket '$BUCKET'"
    fi
done

echo ""
echo "Configuring bucket policies..."

# Set anonymous read/write policy for test bucket (for testing only!)
"$MINIO_CLIENT" anonymous set download "$MINIO_ALIAS/$BUCKET_TEST" > /dev/null 2>&1
echo "  ✓ Set download policy for '$BUCKET_TEST'"

# Set private policy for filled-forms bucket (production-like)
"$MINIO_CLIENT" anonymous set none "$MINIO_ALIAS/$BUCKET_FILLED_FORMS" > /dev/null 2>&1
echo "  ✓ Set private policy for '$BUCKET_FILLED_FORMS'"

# Set private policy for templates bucket
"$MINIO_CLIENT" anonymous set none "$MINIO_ALIAS/$BUCKET_TEMPLATES" > /dev/null 2>&1
echo "  ✓ Set private policy for '$BUCKET_TEMPLATES'"

echo ""
echo "Listing buckets..."
"$MINIO_CLIENT" ls "$MINIO_ALIAS"

echo ""
echo "========================================="
echo "✓ MinIO initialization complete!"
echo "========================================="
echo ""
echo "Available buckets:"
echo "  • $BUCKET_FILLED_FORMS  - For filled form submissions"
echo "  • $BUCKET_TEMPLATES      - For template storage"
echo "  • $BUCKET_TEST           - For testing uploads"
echo ""
echo "MinIO Console: http://localhost:9001"
echo "MinIO API:     http://localhost:9000"
echo ""
echo "Example S3 endpoint for testing:"
echo "  Endpoint: $MINIO_ENDPOINT"
echo "  Bucket:   $BUCKET_FILLED_FORMS"
echo "  Region:   us-east-1 (MinIO default)"
echo ""
echo "To test pre-signed POST:"
echo "  1. Generate policy using AWS SDK with endpoint=$MINIO_ENDPOINT"
echo "  2. Use forcePathStyle=true in S3 client config"
echo "  3. POST filled forms to http://localhost:9000/$BUCKET_FILLED_FORMS"
echo ""

#!/bin/bash
# Setup MinIO Template Gallery for Testing
#
# Creates buckets and policies for template publishing/downloading
#
# Usage: ./dev/minio-setup-gallery.sh

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
BUCKET_GALLERY="template-gallery"
BUCKET_SUBMISSIONS="form-submissions"

echo "========================================="
echo "Setting Up MinIO Template Gallery"
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
echo "✓ MinIO client configured"
echo ""

echo "Creating template gallery structure..."

# Create template gallery bucket
if "$MINIO_CLIENT" ls "$MINIO_ALIAS/$BUCKET_GALLERY" > /dev/null 2>&1; then
    echo "  ✓ Bucket '$BUCKET_GALLERY' already exists"
else
    "$MINIO_CLIENT" mb "$MINIO_ALIAS/$BUCKET_GALLERY"
    echo "  ✓ Created bucket '$BUCKET_GALLERY'"
fi

# Create directory structure
"$MINIO_CLIENT" ls "$MINIO_ALIAS/$BUCKET_GALLERY/templates/" > /dev/null 2>&1 || {
    echo "Creating templates/ directory structure..."
}

# Set public read policy for templates
echo "Setting public read policy for templates..."
"$MINIO_CLIENT" anonymous set download "$MINIO_ALIAS/$BUCKET_GALLERY/templates" 2>/dev/null || {
    "$MINIO_CLIENT" anonymous set download "$MINIO_ALIAS/$BUCKET_GALLERY"
}
echo "  ✓ Public read access enabled for templates"

# Create form submissions bucket (if not exists from minio-init.sh)
if "$MINIO_CLIENT" ls "$MINIO_ALIAS/$BUCKET_SUBMISSIONS" > /dev/null 2>&1; then
    echo "  ✓ Bucket '$BUCKET_SUBMISSIONS' already exists"
else
    "$MINIO_CLIENT" mb "$MINIO_ALIAS/$BUCKET_SUBMISSIONS"
    echo "  ✓ Created bucket '$BUCKET_SUBMISSIONS'"
fi

# Set private policy for form submissions
"$MINIO_CLIENT" anonymous set none "$MINIO_ALIAS/$BUCKET_SUBMISSIONS" > /dev/null 2>&1
echo "  ✓ Private policy set for '$BUCKET_SUBMISSIONS'"

echo ""
echo "========================================="
echo "✓ Template Gallery Setup Complete!"
echo "========================================="
echo ""
echo "Template Gallery Structure:"
echo "  $BUCKET_GALLERY/"
echo "    ├── templates/"
echo "    │   ├── official/      (for official signed templates)"
echo "    │   ├── community/     (for community templates)"
echo "    │   └── test/          (for testing)"
echo "    └── (public read access)"
echo ""
echo "Form Submissions:"
echo "  $BUCKET_SUBMISSIONS/"
echo "    └── (private, pre-signed POST only)"
echo ""
echo "Publishing Configuration:"
echo "  Endpoint:    $MINIO_ENDPOINT"
echo "  Gallery:     $BUCKET_GALLERY"
echo "  Prefix:      templates/official/ (or templates/community/)"
echo "  Access Key:  $MINIO_USER"
echo "  Secret Key:  $MINIO_PASSWORD"
echo ""
echo "Gallery Configuration (for users):"
echo "  Endpoint:    $MINIO_ENDPOINT"
echo "  Bucket:      $BUCKET_GALLERY"
echo "  Prefix:      templates/"
echo "  Access Key:  $MINIO_USER (read-only, could be different)"
echo "  Secret Key:  $MINIO_PASSWORD"
echo ""
echo "MinIO Console: http://localhost:9001"
echo ""
echo "Next steps:"
echo "  1. Sign a template in PromptResponse Desktop"
echo "  2. Open Template Publisher view"
echo "  3. Configure S3 with settings above"
echo "  4. Publish template to gallery"
echo "  5. Browse gallery in Template Gallery view"
echo ""

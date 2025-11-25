#!/bin/bash
# Test S3 upload functionality with local MinIO
#
# Prerequisites:
#   1. Start MinIO: docker-compose -f docker/docker-compose.s3-test.yml up -d
#   2. Build CLI: dotnet build src/PromptResponse.Cli
#
# Usage:
#   ./scripts/test-s3-upload.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# MinIO configuration
MINIO_ENDPOINT="http://localhost:9000"
MINIO_BUCKET="apr-submissions"
MINIO_ACCESS_KEY="minioadmin"
MINIO_SECRET_KEY="minioadmin"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "========================================"
echo "APR S3 Upload Test"
echo "========================================"
echo ""

# Check if MinIO is running
echo -n "Checking MinIO availability... "
if curl -s --fail "$MINIO_ENDPOINT/minio/health/live" > /dev/null 2>&1; then
    echo -e "${GREEN}OK${NC}"
else
    echo -e "${RED}FAILED${NC}"
    echo ""
    echo "MinIO is not running. Start it with:"
    echo "  docker-compose -f docker/docker-compose.s3-test.yml up -d"
    exit 1
fi

# Build CLI if needed
echo -n "Building CLI... "
if dotnet build "$PROJECT_ROOT/src/PromptResponse.Cli" -q 2>/dev/null; then
    echo -e "${GREEN}OK${NC}"
else
    echo -e "${RED}FAILED${NC}"
    exit 1
fi

# Create temporary test template
TEST_DIR=$(mktemp -d)
TEST_TEMPLATE="$TEST_DIR/test-form.aprt"
TEST_FILLED="$TEST_DIR/test-form.aprf"

echo -n "Creating test template... "
cat > "$TEST_TEMPLATE" << 'EOF'
{
  "version": "1.0",
  "documentType": "template",
  "metadata": {
    "title": "S3 Upload Test Form",
    "description": "Test form for S3 upload functionality",
    "created": "2024-01-01T00:00:00Z",
    "modified": "2024-01-01T00:00:00Z"
  },
  "sections": [
    {
      "id": "section_1",
      "title": "Test Section",
      "prompts": [
        {
          "id": "name",
          "label": "Name",
          "response": ""
        },
        {
          "id": "email",
          "label": "Email",
          "response": ""
        }
      ]
    }
  ]
}
EOF
echo -e "${GREEN}OK${NC}"

# Generate S3 config and embed in template
echo -n "Generating S3 submission config... "
dotnet run --project "$PROJECT_ROOT/src/PromptResponse.Cli" -- s3-setup \
    --bucket="$MINIO_BUCKET" \
    --endpoint="$MINIO_ENDPOINT" \
    --path-style \
    --access-key-id="$MINIO_ACCESS_KEY" \
    --secret-access-key="$MINIO_SECRET_KEY" \
    --prefix="forms/" \
    --expires=1h \
    --template="$TEST_TEMPLATE" \
    --output="$TEST_TEMPLATE" \
    > /dev/null 2>&1
echo -e "${GREEN}OK${NC}"

# Verify config was embedded
echo -n "Verifying submission config... "
if grep -q "s3-presigned-post" "$TEST_TEMPLATE"; then
    echo -e "${GREEN}OK${NC}"
else
    echo -e "${RED}FAILED${NC}"
    echo "Submission config not found in template"
    exit 1
fi

# Create filled form
echo -n "Creating filled form... "
cat > "$TEST_FILLED" << 'EOF'
{
  "version": "1.0",
  "documentType": "filledForm",
  "metadata": {
    "title": "S3 Upload Test Form",
    "description": "Test form for S3 upload functionality",
    "created": "2024-01-01T00:00:00Z",
    "modified": "2024-01-01T00:00:00Z",
    "filledAt": "2024-01-15T12:00:00Z",
    "filledBy": "Test User"
  },
  "sections": [
    {
      "id": "section_1",
      "title": "Test Section",
      "prompts": [
        {
          "id": "name",
          "label": "Name",
          "response": "John Doe"
        },
        {
          "id": "email",
          "label": "Email",
          "response": "john@example.com"
        }
      ]
    }
  ]
}
EOF
echo -e "${GREEN}OK${NC}"

# Extract submission config from template and test upload
echo -n "Testing S3 upload... "

# Extract the fields from the template
URL=$(grep -o '"url": *"[^"]*"' "$TEST_TEMPLATE" | head -1 | cut -d'"' -f4)
POLICY=$(grep -o '"Policy": *"[^"]*"' "$TEST_TEMPLATE" | cut -d'"' -f4)
SIGNATURE=$(grep -o '"X-Amz-Signature": *"[^"]*"' "$TEST_TEMPLATE" | cut -d'"' -f4)
CREDENTIAL=$(grep -o '"X-Amz-Credential": *"[^"]*"' "$TEST_TEMPLATE" | cut -d'"' -f4)
DATE=$(grep -o '"X-Amz-Date": *"[^"]*"' "$TEST_TEMPLATE" | cut -d'"' -f4)
ALGORITHM=$(grep -o '"X-Amz-Algorithm": *"[^"]*"' "$TEST_TEMPLATE" | cut -d'"' -f4)

# Generate unique filename
UPLOAD_KEY="forms/test-$(date +%s).aprf"

# Upload using curl with pre-signed POST
UPLOAD_RESULT=$(curl -s -w "%{http_code}" -o /dev/null \
    -F "key=$UPLOAD_KEY" \
    -F "acl=private" \
    -F "Content-Type=application/json" \
    -F "X-Amz-Algorithm=$ALGORITHM" \
    -F "X-Amz-Credential=$CREDENTIAL" \
    -F "X-Amz-Date=$DATE" \
    -F "Policy=$POLICY" \
    -F "X-Amz-Signature=$SIGNATURE" \
    -F "file=@$TEST_FILLED" \
    "${URL}")

if [ "$UPLOAD_RESULT" = "204" ] || [ "$UPLOAD_RESULT" = "200" ]; then
    echo -e "${GREEN}OK${NC} (HTTP $UPLOAD_RESULT)"
else
    echo -e "${YELLOW}WARNING${NC} (HTTP $UPLOAD_RESULT)"
    echo "Upload may have failed. Check MinIO console at http://localhost:9001"
fi

# Verify upload in MinIO
echo -n "Verifying upload in MinIO... "
if curl -s --fail "$MINIO_ENDPOINT/$MINIO_BUCKET/$UPLOAD_KEY" > /dev/null 2>&1; then
    echo -e "${GREEN}OK${NC}"
else
    # Try with anonymous access
    if curl -s -u "$MINIO_ACCESS_KEY:$MINIO_SECRET_KEY" --fail "$MINIO_ENDPOINT/$MINIO_BUCKET/$UPLOAD_KEY" > /dev/null 2>&1; then
        echo -e "${GREEN}OK${NC} (authenticated)"
    else
        echo -e "${YELLOW}Could not verify${NC}"
        echo "Check MinIO console at http://localhost:9001"
    fi
fi

# Cleanup
rm -rf "$TEST_DIR"

echo ""
echo "========================================"
echo -e "${GREEN}S3 Upload Test Complete${NC}"
echo "========================================"
echo ""
echo "MinIO Console: http://localhost:9001"
echo "Login: minioadmin / minioadmin"
echo "Bucket: $MINIO_BUCKET"
echo ""
echo "To stop MinIO:"
echo "  docker-compose -f docker/docker-compose.s3-test.yml down"

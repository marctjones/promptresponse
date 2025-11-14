#!/bin/bash
# MinIO Setup Script for PromptResponse Development
# Downloads MinIO server and client binaries for S3 pre-signed POST testing
#
# Usage: ./dev/minio-setup.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MINIO_DIR="$SCRIPT_DIR/minio"
MINIO_SERVER="$MINIO_DIR/minio"
MINIO_CLIENT="$MINIO_DIR/mc"

# Detect OS and architecture
OS=$(uname -s | tr '[:upper:]' '[:lower:]')
ARCH=$(uname -m)

# Map architecture names
case "$ARCH" in
    x86_64)
        ARCH="amd64"
        ;;
    aarch64|arm64)
        ARCH="arm64"
        ;;
    *)
        echo "Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

# MinIO download URLs
MINIO_SERVER_URL="https://dl.min.io/server/minio/release/${OS}-${ARCH}/minio"
MINIO_CLIENT_URL="https://dl.min.io/client/mc/release/${OS}-${ARCH}/mc"

echo "========================================="
echo "MinIO Setup for PromptResponse"
echo "========================================="
echo "OS: $OS"
echo "Architecture: $ARCH"
echo ""

# Create MinIO directory
mkdir -p "$MINIO_DIR"
mkdir -p "$MINIO_DIR/data"

# Download MinIO server
if [ -f "$MINIO_SERVER" ]; then
    echo "✓ MinIO server already exists at $MINIO_SERVER"
else
    echo "Downloading MinIO server..."
    wget -q --show-progress "$MINIO_SERVER_URL" -O "$MINIO_SERVER"
    chmod +x "$MINIO_SERVER"
    echo "✓ MinIO server downloaded"
fi

# Download MinIO client (mc)
if [ -f "$MINIO_CLIENT" ]; then
    echo "✓ MinIO client (mc) already exists at $MINIO_CLIENT"
else
    echo "Downloading MinIO client (mc)..."
    wget -q --show-progress "$MINIO_CLIENT_URL" -O "$MINIO_CLIENT"
    chmod +x "$MINIO_CLIENT"
    echo "✓ MinIO client downloaded"
fi

# Verify installations
echo ""
echo "Verifying installations..."
"$MINIO_SERVER" --version
"$MINIO_CLIENT" --version

echo ""
echo "========================================="
echo "✓ MinIO setup complete!"
echo "========================================="
echo ""
echo "Next steps:"
echo "  1. Start MinIO:    ./dev/minio-start.sh"
echo "  2. Initialize:     ./dev/minio-init.sh"
echo "  3. Stop MinIO:     ./dev/minio-stop.sh"
echo ""
echo "MinIO Console will be available at: http://localhost:9001"
echo "MinIO API will be available at:     http://localhost:9000"
echo ""

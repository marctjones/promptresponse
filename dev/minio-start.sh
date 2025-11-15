#!/bin/bash
# Start MinIO server for PromptResponse S3 testing
#
# Usage: ./dev/minio-start.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MINIO_DIR="$SCRIPT_DIR/minio"
MINIO_SERVER="$MINIO_DIR/minio"
MINIO_DATA="$MINIO_DIR/data"
MINIO_PID_FILE="$MINIO_DIR/minio.pid"

# MinIO configuration
export MINIO_ROOT_USER="promptresponse"
export MINIO_ROOT_PASSWORD="promptresponse123"
export MINIO_BROWSER_REDIRECT_URL="http://localhost:9001"

# Check if MinIO is already running
if [ -f "$MINIO_PID_FILE" ]; then
    PID=$(cat "$MINIO_PID_FILE")
    if ps -p "$PID" > /dev/null 2>&1; then
        echo "MinIO is already running (PID: $PID)"
        echo "Console: http://localhost:9001"
        echo "API:     http://localhost:9000"
        exit 0
    else
        # Stale PID file, remove it
        rm "$MINIO_PID_FILE"
    fi
fi

# Check if MinIO binary exists
if [ ! -f "$MINIO_SERVER" ]; then
    echo "Error: MinIO server not found!"
    echo "Please run: ./dev/minio-setup.sh"
    exit 1
fi

# Create data directory
mkdir -p "$MINIO_DATA"

echo "========================================="
echo "Starting MinIO Server"
echo "========================================="
echo "Data directory: $MINIO_DATA"
echo "Console URL:    http://localhost:9001"
echo "API URL:        http://localhost:9000"
echo "Username:       $MINIO_ROOT_USER"
echo "Password:       $MINIO_ROOT_PASSWORD"
echo ""

# Start MinIO in background
"$MINIO_SERVER" server "$MINIO_DATA" \
    --address ":9000" \
    --console-address ":9001" \
    > "$MINIO_DIR/minio.log" 2>&1 &

MINIO_PID=$!
echo "$MINIO_PID" > "$MINIO_PID_FILE"

# Wait for MinIO to be ready
echo "Waiting for MinIO to start..."
for i in {1..30}; do
    if curl -s http://localhost:9000/minio/health/live > /dev/null 2>&1; then
        echo ""
        echo "✓ MinIO is ready!"
        echo ""
        echo "========================================="
        echo "MinIO Console: http://localhost:9001"
        echo "MinIO API:     http://localhost:9000"
        echo "Username:      $MINIO_ROOT_USER"
        echo "Password:      $MINIO_ROOT_PASSWORD"
        echo "========================================="
        echo ""
        echo "Log file: $MINIO_DIR/minio.log"
        echo "PID file: $MINIO_PID_FILE (PID: $MINIO_PID)"
        echo ""
        echo "Next step: ./dev/minio-init.sh (to create buckets)"
        echo ""
        exit 0
    fi
    sleep 1
    echo -n "."
done

echo ""
echo "Error: MinIO failed to start within 30 seconds"
echo "Check logs: $MINIO_DIR/minio.log"
exit 1

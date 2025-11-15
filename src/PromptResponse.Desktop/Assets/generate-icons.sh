#!/bin/bash

# Script to generate app icons from SVG source
# Requires: inkscape or imagemagick

set -e

SVG_FILE="app-icon.svg"
OUTPUT_DIR="."

echo "Generating PromptResponse application icons..."

# Check if inkscape is available (preferred for SVG conversion)
if command -v inkscape &> /dev/null; then
    echo "Using Inkscape for conversion..."

    # Generate PNG files at different sizes
    inkscape "$SVG_FILE" -w 16 -h 16 -o "$OUTPUT_DIR/app-icon-16.png"
    inkscape "$SVG_FILE" -w 32 -h 32 -o "$OUTPUT_DIR/app-icon-32.png"
    inkscape "$SVG_FILE" -w 48 -h 48 -o "$OUTPUT_DIR/app-icon-48.png"
    inkscape "$SVG_FILE" -w 256 -h 256 -o "$OUTPUT_DIR/app-icon-256.png"
    inkscape "$SVG_FILE" -w 512 -h 512 -o "$OUTPUT_DIR/app-icon-512.png"

    echo "✓ PNG files generated"

    # Generate ICO file (multi-resolution) if ImageMagick is available
    if command -v convert &> /dev/null; then
        convert "$OUTPUT_DIR/app-icon-16.png" \
                "$OUTPUT_DIR/app-icon-32.png" \
                "$OUTPUT_DIR/app-icon-48.png" \
                "$OUTPUT_DIR/app-icon-256.png" \
                "$OUTPUT_DIR/app-icon.ico"
        echo "✓ ICO file generated"
    fi

elif command -v convert &> /dev/null; then
    echo "Using ImageMagick for conversion..."

    # Generate PNG files
    convert -background none "$SVG_FILE" -resize 16x16 "$OUTPUT_DIR/app-icon-16.png"
    convert -background none "$SVG_FILE" -resize 32x32 "$OUTPUT_DIR/app-icon-32.png"
    convert -background none "$SVG_FILE" -resize 48x48 "$OUTPUT_DIR/app-icon-48.png"
    convert -background none "$SVG_FILE" -resize 256x256 "$OUTPUT_DIR/app-icon-256.png"
    convert -background none "$SVG_FILE" -resize 512x512 "$OUTPUT_DIR/app-icon-512.png"

    echo "✓ PNG files generated"

    # Generate ICO file
    convert "$OUTPUT_DIR/app-icon-16.png" \
            "$OUTPUT_DIR/app-icon-32.png" \
            "$OUTPUT_DIR/app-icon-48.png" \
            "$OUTPUT_DIR/app-icon-256.png" \
            "$OUTPUT_DIR/app-icon.ico"
    echo "✓ ICO file generated"

else
    echo "❌ Error: Neither Inkscape nor ImageMagick found"
    echo ""
    echo "Please install one of the following:"
    echo "  - Inkscape: sudo apt install inkscape"
    echo "  - ImageMagick: sudo apt install imagemagick"
    echo ""
    echo "Or use an online converter:"
    echo "  1. Open app-icon.svg in a browser"
    echo "  2. Take a screenshot or export as PNG"
    echo "  3. Use https://convertio.co/svg-ico/ to create .ico file"
    exit 1
fi

echo ""
echo "✓ Icon generation complete!"
echo ""
echo "Generated files:"
ls -lh app-icon*.png app-icon.ico 2>/dev/null || true

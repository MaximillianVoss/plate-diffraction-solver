#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
SOURCE_FILE="$SCRIPT_DIR/src/DiffractionCpu.cpp"
OUTPUT_FILE="$BUILD_DIR/DiffractionCpu"

mkdir -p "$BUILD_DIR"

g++ -std=c++17 -O3 "$SOURCE_FILE" -o "$OUTPUT_FILE"

echo "Built: $OUTPUT_FILE"

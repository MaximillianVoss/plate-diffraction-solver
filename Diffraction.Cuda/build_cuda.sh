#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
SOURCE_FILE="$SCRIPT_DIR/src/DiffractionCuda.cu"
OUTPUT_FILE="$BUILD_DIR/DiffractionCuda"
CUDA_ARCH="${1:-75}"

mkdir -p "$BUILD_DIR"

nvcc -std=c++17 -O3 -arch="sm_${CUDA_ARCH}" "$SOURCE_FILE" -lcusolver -lcublas -o "$OUTPUT_FILE"

echo "Built: $OUTPUT_FILE"

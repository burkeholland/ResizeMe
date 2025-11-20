#!/bin/bash

# ResizeMe Build & Release Script
# Builds self-contained .exe files for distribution

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT_FILE="$PROJECT_ROOT/ResizeMe/ResizeMe.csproj"
RELEASE_DIR="$PROJECT_ROOT/releases"

echo "🔨 Building ResizeMe self-contained releases..."
echo "Project: $PROJECT_FILE"
echo "Release dir: $RELEASE_DIR"

# Clean old releases
rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

# Build x64 standalone
echo ""
echo "📦 Building x64 standalone..."
dotnet publish "$PROJECT_FILE" \
  -c Release \
  -p:PublishProfile="win-x64-standalone" \
  -p:Platform="x64"

# Copy x64 exe to releases
X64_EXE="$PROJECT_ROOT/ResizeMe/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/ResizeMe.exe"
if [ -f "$X64_EXE" ]; then
  cp "$X64_EXE" "$RELEASE_DIR/ResizeMe-x64.exe"
  echo "✅ Created: ResizeMe-x64.exe"
else
  echo "⚠️  Could not find x64 exe at $X64_EXE"
fi

# Build x86 standalone
echo ""
echo "📦 Building x86 standalone..."
dotnet publish "$PROJECT_FILE" \
  -c Release \
  -p:PublishProfile="win-x86-standalone" \
  -p:Platform="x86"

# Copy x86 exe to releases
X86_EXE="$PROJECT_ROOT/ResizeMe/bin/Release/net8.0-windows10.0.19041.0/win-x86/publish/ResizeMe.exe"
if [ -f "$X86_EXE" ]; then
  cp "$X86_EXE" "$RELEASE_DIR/ResizeMe-x86.exe"
  echo "✅ Created: ResizeMe-x86.exe"
else
  echo "⚠️  Could not find x86 exe at $X86_EXE"
fi

echo ""
echo "✨ Build complete! Releases in: $RELEASE_DIR"
ls -lh "$RELEASE_DIR"

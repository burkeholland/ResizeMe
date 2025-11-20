# ResizeMe v1.1.1 Release Notes

## 🎯 Optimized Standalone Builds for All Platforms

This release brings the file size optimizations to **x86** and **ARM64** builds, ensuring a lightweight experience on all supported architectures.

### 📦 Download

- **ResizeMe-x64.exe** (~150 KB) — 64-bit Windows (Standard)
- **ResizeMe-x86.exe** (~120 KB) — 32-bit Windows (Older hardware)
- **ResizeMe-arm64.exe** (~150 KB) — Windows on ARM (Surface Pro X, etc.)

### ✨ What's New

**Build Optimizations**
- Enabled trimming and compression for **x86** and **ARM64** standalone builds.
- Reduced file sizes from ~200MB+ to <200KB for all platforms.
- Added dedicated ARM64 standalone build profile.

**Previous v1.1.0 Features**
- Framework-dependent `.exe` files.
- Requires .NET 8 runtime (downloads automatically on first run if needed).
- SmartScreen bypass instructions in `DISTRIBUTION.md`.

### 🚀 Installation

1. Download the `.exe` for your system architecture.
2. Run it directly.
3. On first launch, Windows may ask for permission—click "More info" → "Run anyway".
4. ResizeMe runs in the system tray.
5. Use `Ctrl+Win+R` to toggle the window visibility.

### 📋 System Requirements

- Windows 10 (build 17763) or later
- .NET 8 Runtime (downloads automatically on first run if not present)

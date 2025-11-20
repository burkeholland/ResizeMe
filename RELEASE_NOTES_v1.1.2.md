# ResizeMe v1.1.2 Release Notes

## 🐛 Bug Fixes

### Critical Fix for Standalone Builds
- Fixed a crash that occurred when opening the Settings window in standalone `.exe` builds.
- Resolved an issue where aggressive code trimming was removing necessary WinUI/WinRT components.
- Added robust error handling for the Settings window initialization.

### 📦 Download

- **ResizeMe-x64.exe** (~150 KB) — 64-bit Windows (Standard)
- **ResizeMe-x86.exe** (~120 KB) — 32-bit Windows (Older hardware)
- **ResizeMe-arm64.exe** (~150 KB) — Windows on ARM (Surface Pro X, etc.)

### 🚀 Installation

1. Download the `.exe` for your system architecture.
2. Run it directly.
3. On first launch, Windows may ask for permission—click "More info" → "Run anyway".
4. ResizeMe runs in the system tray.
5. Use `Ctrl+Win+R` to toggle the window visibility.

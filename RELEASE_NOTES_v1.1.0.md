# ResizeMe v1.1.0 Release Notes

## 🎯 Standalone .exe Distribution

This release provides simple, standalone executable files for direct download and execution—no complex folder structures or confusing build output.

### 📦 Download

- **ResizeMe-x64.exe** (152 KB) — 64-bit Windows (recommended for modern systems)
- **ResizeMe-x86.exe** (121 KB) — 32-bit Windows

### ✨ What's New

**Simplified Distribution**
- Framework-dependent `.exe` files (~120–150 KB each)
- Requires .NET 8 runtime (downloads automatically on first run if needed)
- No ZIP files, no folder clutter—just download and run
- SmartScreen bypass instructions in `DISTRIBUTION.md`

**Architecture Refactoring**
Comprehensive refactor implementing AI-first software engineering principles:
- ✅ Modular feature-scoped services (`Features/SystemIntegration`, `Features/WindowManagement`, `Features/Settings`, `Features/MainLayout`)
- ✅ Shared utilities layer (`Shared/Logging`, `Shared/Config`) for cross-cutting concerns
- ✅ Centralized settings store (`UserSettingsStore`) with typed static properties
- ✅ Centralized logging (`AppLog`) for consistent diagnostic output
- ✅ Removed 20+ legacy monolithic service files
- ✅ Clean dependency model: services communicate via events and dependency injection

### 🚀 Installation

1. Download the `.exe` for your system (usually **x64**)
2. Run it directly
3. On first launch, Windows may ask for permission—click "More info" → "Run anyway"
4. ResizeMe runs in the system tray
5. Use `Ctrl+Win+R` to toggle the window visibility

### 📋 System Requirements

- Windows 10 (build 17763) or later
- .NET 8 Runtime (downloads automatically on first run if not present)

### 📚 Documentation

See `DISTRIBUTION.md` for:
- Detailed installation instructions
- Windows Defender SmartScreen bypass methods
- Troubleshooting guide
- Building from source

### 🔧 Technical Improvements

**Code Maintainability**
- Model-friendly architecture enables effective AI assistant usage for future development
- Clear separation of concerns reduces cognitive load for maintainers
- Services designed for testability and mocking

**Debugging & Logging**
- Modular services isolate issues
- Centralized logging provides clear diagnostics via `AppLog`

### 📝 Known Limitations

- Unsigned `.exe` files may trigger Windows SmartScreen on first run (bypass instructions in DISTRIBUTION.md)
- Requires .NET 8 runtime (not pre-installed on all Windows systems)

### 🛣️ Roadmap

- [ ] Code-signing to eliminate SmartScreen prompts
- [ ] Microsoft Store publication
- [ ] ARM64 support
- [ ] Auto-update mechanism

### 💬 Feedback

If you encounter issues or have suggestions, please open an issue on GitHub or contact the maintainer.

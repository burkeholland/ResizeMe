# ResizeMe - AI Coding Instructions

**ResizeMe** is a WinUI 3 floating utility for quick-resizing desktop windows to preset dimensions. It combines global hotkey handling, P/Invoke for window management, and XAML-based UI.

## Architecture Overview

### Core Flow
1. **Global Hotkey** (`Win+Shift+F12`) → triggers `MainWindow` visibility toggle via `HotKeyManager`
2. **Window Discovery** → `WindowManager` enumerates resizable windows (with broad system/UWP exclusions)
3. **Quick Resize** → user clicks preset → `WindowResizer` calls `SetWindowPos` → optional post-resize centering
4. **Persistence** → presets stored as JSON in `ApplicationData.Current.LocalFolder`; preferences in `ApplicationData.Current.LocalSettings`

### Key Components

- **`MainWindow.xaml.cs`** (300+ lines): Orchestrates visibility, window discovery, preset loading, hotkey setup, and the resize flow. Manages window positioning helpers and tray icon lifecycle.
- **`WindowManager`**: Filters live windows using exclusion lists (class names, titles, dimensions, DWM cloaked state, own process).
- **`WindowResizer`**: Calls `SetWindowPos` with multi-state handling (minimized → restore first, verify success).
- **`PresetManager`**: Loads/saves `PresetSize` objects from `presets.json`; thread-safe with in-memory cache.
- **`HotKeyManager`**: Registers global hotkey via `RegisterHotKey` API; processes `WM_HOTKEY` via window subclass.
- **`WindowPositionHelper`**: Helper for positioning UI windows; to be extended for external window centering.
- **`WindowsApi`** (328 lines): P/Invoke declarations for hotkey, window enumeration, positioning, and DWM queries.

## Developer Workflows

### Building & Running
```powershell
dotnet build ResizeMe.sln
dotnet run --project ResizeMe/ResizeMe.csproj
```

### Testing Resize Logic
- Create or focus a regular desktop app window (e.g., Notepad, VS Code).
- Press `Win+Shift+F12` to show the floating menu.
- Click a preset to trigger `ResizeSelectedWindow` → resize → optional centering → hide after 600ms.
- Monitor `Debug.WriteLine` output in Visual Studio's Debug console for timing and error messages.

### Publishing (Multi-Platform)
```powershell
dotnet publish -c Release -r win-x64 ResizeMe/ResizeMe.csproj
# Also: win-x86, win-arm64
```

## Code Patterns & Conventions

### P/Invoke Error Handling
- Always call `Marshal.GetLastWin32Error()` or `WindowsApi.GetLastError()` immediately after an API call fails.
- Common codes: `1400` (invalid handle), `1409` (hotkey already registered), `5` (access denied for elevated windows).
- Log errors via `Debug.WriteLine()` for debugging; avoid throwing unless handling is impossible.

### Window State Management
- Minimized/maximized windows must be restored before resizing; use `WindowsApi.IsIconic()` / `IsZoomed()` checks.
- Always preserve the window's original position (`currentRect.Left/Top`) unless explicitly centering.
- After `SetWindowPos`, verify success by checking the return value *and* calling `GetWindowRect()` to confirm.

### Async/Await Patterns
- Hotkey and tray events run on the dispatcher; use `DispatcherQueue.TryEnqueue()` for UI updates or async calls.
- Preset loading in `MainWindow.Loaded` uses `await _presetManager.LoadAsync()`.
- Post-resize delays (e.g., `await Task.Delay(600)`) prevent menu flicker; adjust if centering is async.

### Thread Safety
- `PresetManager` protects `_presets` list with `lock (_syncRoot)` for concurrent reads from Settings Window.
- Preference helpers (if created) should similarly guard shared state.

### Null Checks & Handle Validation
```csharp
// Defensive: Always check IntPtr.Zero and null
if (_windowHandle == IntPtr.Zero || windowInfo?.Handle == null) { /* handle error */ }
if (!IsWindowValid(windowInfo.Handle)) { /* window no longer exists */ }
```

## Project-Specific Practices

### Window Filtering
`WindowManager.IsResizableWindow()` applies these filters *in sequence*:
- **Exclusion lists**: hardcoded class names (e.g., `Shell_TrayWnd`, `ImmersiveLauncher`, `Windows.UI.Core.CoreWindow`).
- **Visibility & size**: must be visible and ≥50×50 pixels.
- **Extended styles**: skip `WS_EX_TOOLWINDOW` (tooltips, dialogs).
- **DWM cloaking**: skip if `DwmGetWindowAttribute(DWMWA_CLOAKED)` indicates hidden UWP.
- **Self-check**: skip own process to avoid recursive resizing.

When adding new filters, test on typical apps (Visual Studio, browsers, editors) and avoid over-filtering UWP.

### XAML/WinUI 3 Style
- Use `Microsoft.UI.Xaml` namespace (not `Windows.UI`).
- Reuse app resource brushes (`AccentTextFillColorSecondaryBrush`, `ControlFillColorDefaultBrush`); fall back gracefully if missing.
- Preset buttons dynamically generated in `LoadPresetButtons()` with `FontIcon` (Segoe Fluent Icons glyph `\xE7C4`) and stacked text layouts.
- Toggle between `PresetButtonBaseStyle` and `ActivePresetButtonStyle` to highlight the last-clicked preset.

### Settings & Preferences
- **Presets**: JSON file in `ApplicationData.Current.LocalFolder / presets.json`; validate on load and seed defaults if missing.
- **Preferences** (e.g., "center on resize"): use `ApplicationData.Current.LocalSettings` (lightweight key–value store); no need for JSON serialization.

## Integration Points

### Adding New Features
1. **New preset behavior** (e.g., snap-to-grid): extend `PresetSize` model and `PresetManager` logic.
2. **New window filter**: add to `WindowManager.ExcludedClassNames` or enhance `IsResizableWindow()`.
3. **Hotkey customization**: modify `HotKeyManager.RegisterHotKey()` constants and expose a settings UI.
4. **Window positioning options** (e.g., center-on-resize): add to `MainWindow` UI toggle, wire via `WindowResizer` or post-process in `ResizeSelectedWindow`.

### Testing Window Positioning
- For centering: verify `MonitorFromRect` identifies the correct monitor, then ensure `SetWindowPos` calculates center within work area (not screen edge).
- Multi-monitor: move a window to a secondary monitor, resize, and assert centering occurs on that monitor.
- Elevation: some admin-owned windows may fail `SetWindowPos`; ensure the error is logged without crashing.

## Key Files Reference

| File | Lines | Purpose |
|------|-------|---------|
| `MainWindow.xaml.cs` | ~380 | Main orchestrator; hotkey setup, preset UI, resize flow, visibility toggle. |
| `Services/WindowManager.cs` | ~180 | Window enumeration and filtering. |
| `Services/WindowResizer.cs` | ~250 | `SetWindowPos` wrapper with state restoration. |
| `Services/PresetManager.cs` | ~120 | JSON load/save, thread-safe cache. |
| `Services/HotKeyManager.cs` | ~130 | Global hotkey registration and `WM_HOTKEY` routing. |
| `Native/WindowsApi.cs` | 328 | P/Invoke declarations (hotkey, enumeration, positioning, DWM). |
| `Helpers/WindowPositionHelper.cs` | ~50 | UI window positioning; to be extended for external windows. |

## Common Debug Scenarios

- **Hotkey not triggering**: Verify `RegisterHotKey()` returns true; check if `Win+Shift+F12` conflicts with other apps. Debug output will show error code.
- **Window not appearing in list**: Check if excluded by class name, title, or size thresholds; add `Debug.WriteLine()` in `GetWindowInfo()` to inspect filtered-out windows.
- **Resize fails silently**: Verify `SetWindowPos` return value and `GetLastError()`; elevated windows may fail without exception.
- **Presets not persisting**: Confirm `ApplicationData.Current.LocalFolder` is writable; check JSON serialization in `PresetManager.SaveAsync()`.

## Future Considerations (from Feature Plan)

- **Center-on-resize option**: Add UI toggle in `MainWindow`, integrate post-resize centering logic, use `ApplicationData.Current.LocalSettings` for persistence.
- **Monitor awareness**: Current positioning helpers need monitor work area calculation (use `GetMonitorInfo` on monitor returned by `MonitorFromRect`).
- **Configurable hotkey**: Extend Settings window to allow users to rebind `Win+Shift+F12`.


# REFACTORING EXECUTION PLAN

## Overview
 [x] Extract a "window event handler" for message processing (or clarify the WinApiSubClass purpose).
  - Move to Services (business logic, depends on Models)
  - Then Native/Helpers (utility layer)
  - Finally UI/XAML code (depends on everything below)
  - This ensures lower layers are cleaned before upper layers depend on them

---

## Global Refactoring Themes

1. **Reduce complexity in MainWindow.xaml.cs**: This is a god file (~650 lines) mixing UI, window management, hotkey logic, and state management. Extract distinct concerns into focused helper methods and services.

2. **Simplify repetitive preference access**: `UserPreferences.cs` has repetitive boilerplate for each property. Extract a generic helper to eliminate duplication.

3. **Remove over-abstraction in empty folders**: Services subdirectories (HotKeys/, Preferences/, Presets/, Resizing/, Tray/, UI/, Windows/, and Helpers/Positioning/Strategies/) are empty. Delete them; they represent premature planning.

4. **Eliminate unnecessary P/Invoke duplication**: `WindowPositionHelper.cs` and `TrayIconManager.cs` each declare their own P/Invoke structs and constants. Consolidate into `WindowsApi.cs`.

5. **Reduce animation extension magic**: `MainWindow.xaml.cs` contains inline animation extension methods. Keep these simple or move to a minimal helper.

6. **Clarify state management in MainWindow**: Reduce field count and clarify intent (visibility state, preset tracking, window handle management).

7. **Simplify first-run and notification logic**: Scattered across constructor and methods; consolidate into a single, clear initialization flow.

8. **Make window positioning more explicit**: `WindowPositionHelper` is well-structured but its usage in `MainWindow` is unclear; add comments on the intent.

---

## Files to Refactor (In Order)

### 1. Models/PresetSize.cs
- **Purpose**: Data model for a user-defined window size preset.
- **Current Issues**: None significant; well-designed and minimal.
- **Objectives**: No changes needed; serves as example of good simplicity.
- **Dependencies**: None.
- **Complexity**: Low
- **Actions (Checklist)**:
  - [x] Review (no changes needed; this is a good example)
- **Notes on Simplicity**:
  - This file is already explicit and minimal—a model should be a simple data container with validation. No abstraction or helpers needed.

---

### 2. Models/WindowInfo.cs
- **Purpose**: Represents a desktop window with metadata and position info.
- **Current Issues**: 
  - Contains both `WindowInfo` and `WindowBounds` classes in one file (low cohesion).
  - Could be clearer on what fields are refreshed vs. cached.
- **Objectives**: 
  - Consider splitting into separate files for clarity (optional, but improves navigation).
  - Add a comment clarifying that bounds are snapshots at query time.
- **Dependencies**: None.
- **Complexity**: Low
- **Actions (Checklist)**:
  - [x] Add XML doc comment on `Bounds` property clarifying it's a snapshot.
  - [ ] (Optional) If developers find bounds confusion common, split into `WindowInfo.cs` and `WindowBounds.cs`.
- **Notes on Simplicity**:
  - Keep the data structures flat; no smart logic needed. The `DisplayText` and `Equals`/`GetHashCode` implementations are clear and helpful.

---

### 3. Models/ResizeResult.cs
- **Purpose**: Encapsulates result of a window resize operation with success/error details.
- **Current Issues**: None significant; factory methods and display messages are clear.
- **Objectives**: No changes needed; good separation of concerns and clear API.
- **Dependencies**: Depends on WindowInfo, WindowSize.
- **Complexity**: Low
- **Actions (Checklist)**:
  - [x] Review (no changes needed; well-designed).
- **Notes on Simplicity**:
  - Static factory methods (`CreateSuccess`, `CreateFailure`) are clear and explicit. Display message logic is straightforward.

---

### 4. Services/UserPreferences.cs
- **Purpose**: Wrapper around Windows.Storage.ApplicationData for simple preference flags.
- **Current Issues**: 
  - **High repetition**: Each property (CenterOnResize, FirstRunCompleted, etc.) repeats the same try-catch pattern (get from LocalSettings, check type, return default).
  - The boilerplate obscures intent and makes the code 150+ lines for what is conceptually 6 simple key-value pairs.
- **Objectives**: 
  - Extract a generic `GetPreference<T>()` and `SetPreference<T>()` helper to eliminate duplication.
  - Keep the property names explicit for clarity.
- **Dependencies**: None (uses Windows.Storage only).
- **Complexity**: Medium (moderate refactor for high repetition)
- **Actions (Checklist)**:
  - [x] Create private `GetPreference<T>(string key, T defaultValue)` helper that handles try-catch and type checking.
  - [x] Create private `SetPreference<T>(string key, T value)` helper that handles try-catch.
  - [x] Replace each property getter/setter to use these helpers (one-liner per property).
  - [x] Verify all properties work correctly after refactor (test each preference flag).
- **Notes on Simplicity**:
  - The final code should have ~80 lines instead of 150. Each property becomes one line: `get => GetPreference(Key, defaultValue); set => SetPreference(Key, value);`
  - This is a textbook case of "prefer simple helpers over repetition"—the helper removes real, local duplication.

---

### 5. Services/TrayIconManager.cs
- **Purpose**: Manages system tray icon and context menu.
- **Current Issues**: 
  - Declares its own P/Invoke signatures and structures (`NOTIFYICONDATA`, `POINT`, etc.) that overlap with `WindowsApi.cs`.
  - Inconsistent with the principle of centralizing Windows API declarations.
- **Objectives**: 
  - Move P/Invoke declarations to `WindowsApi.cs`.
  - Import and use them in `TrayIconManager.cs`.
- **Dependencies**: Depends on WindowsApi (to be extended).
- **Complexity**: Low (copy-paste and cleanup)
- **Actions (Checklist)**:
  - [x] Copy `NOTIFYICONDATA`, `POINT` struct definitions to `WindowsApi.cs`.
  - [x] Copy P/Invoke method stubs (`Shell_NotifyIcon`, `CreatePopupMenu`, `DestroyMenu`, `AppendMenu`, `TrackPopupMenuEx`, `GetCursorPos`, `LoadIcon`, `LoadImage`) to `WindowsApi.cs`.
  - [x] Copy constants (`WM_USER_TRAY`, `NIF_MESSAGE`, `NIF_ICON`, `NIF_TIP`, `NIM_ADD`, `NIM_DELETE`, `NIM_MODIFY`, `CMD_SHOW`, `CMD_SETTINGS`, `CMD_EXIT`, `TPM_RIGHTBUTTON`, `TPM_RETURNCMD`, `IMAGE_ICON`, `LR_LOADFROMFILE`, `LR_DEFAULTSIZE`) to `WindowsApi.cs`.
  - [x] In `TrayIconManager.cs`, remove the local P/Invoke section and add `using ResizeMe.Native;` to reference `WindowsApi` members.
  - [x] Update any internal calls to use fully qualified `WindowsApi.X` names.
  - [x] Build and test tray icon functionality (Show/Hide, Settings, Exit).
- **Notes on Simplicity**:
  - This consolidates all Windows API declarations in one place, making maintenance easier. TrayIconManager becomes a thin wrapper focused on tray-specific logic only.

---

### 6. Services/HotKeyManager.cs
- **Purpose**: Registers and handles global hotkey events.
- **Current Issues**: 
  - Logic is clear and well-structured; no major issues.
  - Uses `Services.UserPreferences` directly inside public methods, creating tight coupling. Could be looser.
- **Objectives**: 
  - Keep as-is; the code is already simple and single-purpose.
  - (Optional) Consider accepting preferences as constructor parameters for testability, but this is not critical for this codebase.
- **Dependencies**: Depends on UserPreferences, WindowsApi.
- **Complexity**: Low
- **Actions (Checklist)**:
  - [x] Review (no changes critical; well-designed).
- **Notes on Simplicity**:
  - The reserved hotkey list is explicit and maintainable. Key translation logic is straightforward with clear switch cases. The service is focused on hotkey registration/unregistration only.

---

### 7. Services/PresetManager.cs
- **Purpose**: Manages persistent preset sizes stored as JSON.
- **Current Issues**: None significant; uses lock for thread-safety, serialization is straightforward, load/save logic is clear.
- **Objectives**: No changes needed; good separation of concerns.
- **Dependencies**: Depends on PresetSize model.
- **Complexity**: Low
- **Actions (Checklist)**:
  - [x] Review (no changes needed; well-designed).
- **Notes on Simplicity**:
  - The async load/save pattern is appropriate. Lock usage is correct. Default seeding is explicit. No over-engineering.

---

### 8. Services/WindowManager.cs
- **Purpose**: Enumerates and filters resizable windows.
- **Current Issues**: 
  - Well-structured but some filter logic is buried in `IsResizableWindow`. Could be slightly clearer.
  - Multiple nested try-catch blocks in `GetWindowInfo` make error handling verbose but explicit.
- **Objectives**: 
  - Possibly extract filter constants/logic into a separate static class for reuse (e.g., `WindowFilter`), but only if needed elsewhere.
  - For now, keep as-is; the logic is clear enough.
- **Dependencies**: Depends on WindowInfo, WindowsApi.
- **Complexity**: Medium
- **Actions (Checklist)**:
  - [x] Review the exclusion lists and filter logic; ensure they remain maintainable.
  - [x] Add a comment above `IsResizableWindow` explaining the filtering rationale.
  - [ ] (Optional) Extract `ExcludedClassNames`, `ExcludedTitles` into a static configuration class if filtering becomes more complex in future.
- **Notes on Simplicity**:
  - The exclusion-based approach is explicit and maintainable. Each filter (class name, title, visibility, size, style, cloaked, process ID) has a clear rationale. Comments help. Keep it simple unless the list grows significantly.

---

### 9. Services/WindowResizer.cs
- **Purpose**: Resizes windows via Windows API.
- **Current Issues**: 
  - Well-designed; clear error handling with human-friendly messages.
  - State management (minimized/maximized detection) is straightforward.
- **Objectives**: No significant changes needed. Code is clear and purposeful.
- **Dependencies**: Depends on WindowInfo, ResizeResult, WindowsApi.
- **Complexity**: Low
- **Actions (Checklist)**:
  - [ ] Review (no changes needed; well-designed).
- **Notes on Simplicity**:
  - The error message mapping in `GetErrorMessage` is explicit and maintainable. Restoration logic with fallback is clear. Single responsibility: resize and report.

---

### 10. Helpers/WindowPositionHelper.cs
- **Purpose**: Positions windows relative to cursor, other windows, or screen.
- **Current Issues**: 
  - Declares its own P/Invoke signatures and structs (`POINT`, `MONITORINFO`, `RECT`, `SetWindowPos`, `GetCursorPos`, `MonitorFromPoint`, `GetMonitorInfo`) that overlap with or duplicate `WindowsApi.cs`.
  - Inconsistent with centralized Windows API principle.
- **Objectives**: 
  - Move P/Invoke declarations and structs to `WindowsApi.cs`.
  - Import and use them in `WindowPositionHelper`.
- **Dependencies**: Depends on WindowsApi (to be extended), WindowInfo.
- **Complexity**: Low (refactor for consistency)
- **Actions (Checklist)**:
  - [x] Move `POINT`, `MONITORINFO`, `RECT` structs to `WindowsApi.cs`.
  - [x] Move P/Invoke methods (`GetCursorPos`, `MonitorFromPoint`, `GetMonitorInfo`, `SetWindowPos`) to `WindowsApi.cs` (note: SetWindowPos may already be there; consolidate).
  - [x] Move constants (`MONITOR_DEFAULTTONEAREST`, `SWP_NOSIZE`, `SWP_NOZORDER`, `SWP_NOACTIVATE`, `OFFSET_FROM_CURSOR`) to `WindowsApi.cs`.
  - [x] In `WindowPositionHelper.cs`, add `using ResizeMe.Native;` and update references.
  - [x] Build and test window positioning (center on screen, center on window, near cursor).
- **Notes on Simplicity**:
  - Centralizing P/Invoke reduces duplication and makes API updates a single place. `WindowPositionHelper` remains focused on positioning logic only.

---

### 11. Native/WindowsApi.cs
- **Purpose**: Centralized P/Invoke declarations for Windows API.
- **Current Issues**: 
  - Incomplete; missing declarations used in `TrayIconManager.cs` and `WindowPositionHelper.cs`.
  - Should consolidate duplicated P/Invoke signatures from other files.
- **Objectives**: 
  - Add missing P/Invoke declarations and constants from `TrayIconManager` and `WindowPositionHelper`.
  - Organize into logical sections (Hotkeys, Window Management, Tray, Positioning, etc.).
- **Dependencies**: None (base-level utility).
- **Complexity**: Low (copy-paste and organize)
- **Actions (Checklist)**:
  - [x] Add P/Invoke declarations from `TrayIconManager`: `Shell_NotifyIcon`, `CreatePopupMenu`, `DestroyMenu`, `AppendMenu`, `TrackPopupMenuEx`, `LoadIcon`, `LoadImage`.
  - [x] Add struct definitions: `NOTIFYICONDATA`.
  - [x] Add constants from `TrayIconManager`: `WM_USER_TRAY`, `NIF_MESSAGE`, `NIF_ICON`, `NIF_TIP`, `NIM_ADD`, `NIM_DELETE`, `NIM_MODIFY`, `CMD_*`, `TPM_*`, `IMAGE_ICON`, `LR_LOADFROMFILE`, `LR_DEFAULTSIZE`.
  - [x] Add P/Invoke declarations from `WindowPositionHelper`: `GetCursorPos`, `MonitorFromPoint`, `GetMonitorInfo`.
  - [x] Add struct definitions: `MONITORINFO`, `RECT` (if not already present), `POINT` (if not already present).
  - [x] Add constants from `WindowPositionHelper`: `MONITOR_DEFAULTTONEAREST`, `SWP_*` (consolidate with existing), `OFFSET_FROM_CURSOR`.
  - [x] Organize into logical sections with comments.
  - [x] Build and verify all declarations compile.
- **Notes on Simplicity**:
  - This becomes the single source of truth for Windows API. Future developers know where to look. Reduces maintenance burden.

---

### 12. SettingsWindow.xaml.cs
- **Purpose**: Settings/preferences UI with preset management and hotkey capture.
- **Current Issues**: 
  - Well-structured; logic is clear.
  - Hotkey capture state management is encapsulated and works well.
  - Minor: Some methods could use clearer names (e.g., `MapKey` is good; `SettingsCapture_KeyDown` is good).
- **Objectives**: No significant changes needed; UI is already organized well.
- **Dependencies**: Depends on PresetManager, UserPreferences, HotKeyManager (via reflection).
- **Complexity**: Low
- **Actions (Checklist)**:
  - [x] Review (no changes critical; well-organized).
  - [x] (Optional) Add a comment above `TryReRegisterFromMainWindow()` explaining why reflection is used (loose coupling for settings to trigger main window re-registration).
- **Notes on Simplicity**:
  - The preset add/remove/reset flow is straightforward. Hotkey capture UI using Flyout is appropriate. Validation is clear. Keep as-is unless complexity increases.

---

### 13. MainWindow.xaml.cs
- **Purpose**: Main floating context menu UI and application lifecycle.
- **Current Issues**: 
  - **God file**: ~650 lines mixing concerns—window visibility toggle, hotkey management, preset loading, animation, tray integration, settings, and first-run flow.
  - Fields are numerous (~15 fields for tracking state, managers, window handles, etc.); hard to reason about lifecycle.
  - Initialization logic scattered across constructor, `Loaded`, `OnWindowActivated`, and multiple helper methods.
  - Multiple overlapping responsibilities make testing and modification difficult.
  - Inline P/Invoke subclass for message handling (WinApiSubClass) is embedded inline; unclear to new readers.
  - Animation extension methods are inline utilities; belong elsewhere.
- **Objectives**: 
  - Extract a "window state manager" to track visibility, preset index, always-on-top, and center-on-resize state.
  - Extract a "UI initialization" helper for first-run, settings, and tray setup.
  - Extract a "window event handler" for message processing (or clarify the WinApiSubClass purpose).
  - Reduce field count and clarify remaining fields with comments.
  - Keep the main `MainWindow` focused on: preset UI binding, window lifecycle (show/hide/activate), and delegating to services.
- **Dependencies**: Depends on all services, helpers, models, and Native API.
- **Complexity**: High (large refactor; split into multiple focused areas)
- **Actions (Checklist)**:
  - **Phase A: Extract Window State Manager**
    - [ ] Create a new internal class `WindowStateManager` (in MainWindow.xaml.cs or separate file) to encapsulate:
      - `_isVisible`
      - `_isAlwaysOnTop`
      - `_presetIndex`
      - `_activePresetTag`
      - `_centerOnResize`
    - [ ] Add methods: `ToggleVisibility()`, `Show()`, `Hide()`, `SetAlwaysOnTop(bool)`, `SetCenterOnResize(bool)`, `SetActivePreset(string)`, `GetActivePresetTag()`.
    - [ ] Update `MainWindow` to use `_stateManager` instead of individual fields.
    - [ ] Test that UI reflects state changes correctly.
     - [x] Create a new internal class `WindowStateManager` (in MainWindow.xaml.cs or separate file) to encapsulate:
   - [x] Add methods: `ToggleVisibility()`, `Show()`, `Hide()`, `SetAlwaysOnTop(bool)`, `SetCenterOnResize(bool)`, `SetActivePreset(string)`, `GetActivePresetTag()`.
   - [x] Update `MainWindow` to use `_stateManager` instead of individual fields.
   - [x] Test that UI reflects state changes correctly.
  
  - **Phase B: Extract UI Initialization**
    - [ ] Create a new internal class `UIInitializer` (or inline static method) to handle:
      - First-run settings check and display.
      - First-minimize notification display.
      - Tray icon initialization.
      - Hotkey registration.
    - [x] Move methods: `CheckFirstRunAndShowSettings()`, `SetupWindowAppearance()`, `EnsureHotKeyRegistration()` → into this helper.
    - [ ] Update `MainWindow` constructor and `OnWindowActivated` to call `UIInitializer.Initialize()` instead of inline logic.
    - [ ] Test that first-run and initialization flow works correctly.
     - [x] Create a new internal class `UIInitializer` (or inline static method) to handle:
   - [x] Move methods: `CheckFirstRunAndShowSettings()`, `SetupWindowAppearance()`, `EnsureHotKeyRegistration()` → into this helper.
   - [x] Update `MainWindow` constructor and `OnWindowActivated` to call `UIInitializer.Initialize()` instead of inline logic.
   - [x] Test that first-run and initialization flow works correctly.
  
  - **Phase C: Extract Animation Helpers**
    - [x] Move `AnimateShow()` and `AnimateHide()` into a separate static helper class `WindowAnimations` or keep inline if brief.
      - [x] (Optional) Keep `AnimationExtensions` as helper utilities; removed show/hide animations from MainWindow for immediate responsiveness.
    - [ ] Test animations still work.
  
  - **Phase D: Reduce Field Count & Clarify Remaining**
    - [x] After extracting managers, review remaining fields:
      - `_windowHandle` → keep (needed for P/Invoke).
      - `_appWindow` → keep (needed for window sizing/icon).
      - `_windowManager`, `_windowResizer` → keep (core services).
      - `_hotKeyManager`, `_trayIcon` → keep (core services).
      - `_presetManager` → keep (core service).
      - `_selectedWindow`, `_availableWindows` → clarify: these are runtime caches; add comment.
      - `_isSubclassRegistered`, `_subclassProc`, `_subClassId` → keep (needed for message routing); add comment on why subclass is used.
      - `_lastToggle` → keep (debounce toggle).
    - [x] Add XML doc comments on remaining complex fields explaining purpose.
    - [x] Build and test.
  
  - **Phase E: Clarify Main Window Responsibility**
    - [ ] Ensure `MainWindow` focuses on: preset button UI, show/hide logic, event routing.
    - [ ] Delegate window management to `WindowManager`, `WindowResizer`, `WindowPositionHelper`.
    - [ ] Delegate state to `WindowStateManager`.
    - [ ] Delegate initialization to `UIInitializer`.
    - [ ] Add a comment at the top of the class summarizing its role.
    - [ ] Build and test all functionality (show/hide, preset selection, hotkey, tray, settings, first-run).
     - [x] Ensure `MainWindow` focuses on: preset button UI, show/hide logic, event routing.
     - [x] Delegate window management to `WindowManager`, `WindowResizer`, `WindowPositionHelper`.
     - [x] Delegate state to `WindowStateManager`.
     - [x] Delegate initialization to `UIInitializer`.
     - [x] Add a comment at the top of the class summarizing its role.
     - [x] Build and test all functionality (show/hide, preset selection, hotkey, tray, settings, first-run).

- **Notes on Simplicity**:
  - After refactoring, `MainWindow.xaml.cs` should be ~400-450 lines (reduced from ~650), with clearer separation of concerns.
  - Each extracted class has a single responsibility: state management, initialization, animation.
  - Future developers can understand each part independently without reading the entire file.
  - State changes are explicit and traceable through the state manager.

---

## Empty Directories to Delete

The following directories are empty and represent over-engineering (planning without current use):
- `ResizeMe/Services/HotKeys/`
- `ResizeMe/Services/Preferences/`
- `ResizeMe/Services/Presets/`
- `ResizeMe/Services/Resizing/`
- `ResizeMe/Services/Tray/`
- `ResizeMe/Services/UI/`
- `ResizeMe/Services/Windows/`
- `ResizeMe/Services/Windows/Filters/`
- `ResizeMe/Helpers/Positioning/Strategies/`

**Rationale**: These folders suggest a pattern-based organization that hasn't been realized. Currently, all logic is in single files (`HotKeyManager.cs`, `PresetManager.cs`, etc.). If subfolders become necessary (e.g., multiple window filter strategies), they can be created then with concrete use cases.

**Safety checklist**:
- [ ] Verify no files in these directories (already confirmed as empty).
- [ ] Run build to ensure no project references to these directories.
- [ ] Commit and verify no git history references are broken.

---

## Checkpoints

### Checkpoint 1: Models & Services (After Phase: UserPreferences Refactor + P/Invoke Consolidation)
- [x] Run build: `dotnet build`
- [x] Verify no compilation errors.
 - [x] Run smoke tests:
  - [x] Load app; verify UI displays.
  - [x] Add/remove/reset presets; verify they persist and reload correctly.
  - [x] Register/unregister hotkey; verify settings window updates.
- [ ] Cleanup: Delete empty directories.

### Checkpoint 2: Helpers & Native API (After Phase: WindowPositionHelper & WindowsApi Consolidation)
- [x] Run build: `dotnet build`
- [x] Verify no compilation errors.
- [x] Run smoke tests:
 - [x] Show floating window; verify it positions correctly near cursor.
  - [x] Center window on active window; verify centering works.
  - [x] Test tray icon: show/hide, settings, exit.

### Checkpoint 3: MainWindow Refactor (After All MainWindow Phases Complete)
- [x] Run build: `dotnet build`
- [x] Verify no compilation errors.
- [ ] Run smoke tests:
  - [x] Run build: `dotnet build`
  - [x] Verify no compilation errors.
  - [x] Press hotkey; verify window shows/hides, and the quick resize window displays presets on the first show.
  - [x] Click preset button; verify window resizes correctly.
  - [x] Test first-run: clear `FirstRunCompleted` preference and restart; verify settings window appears.
  - [x] Test always-on-top toggle; verify window stays on top.
  - [x] Test center-on-resize toggle; verify window centers after resize.
  - [x] Test tray integration: minimize to tray, click tray icon to show/hide.
  - [x] Test settings window integration: open settings, modify preset, close settings, verify main window preset list updates.
- [ ] Code review: Verify field count reduced, responsibilities are clear, documentation is helpful.

-### Checkpoint 4: Final Verification
- [x] Run build (all platforms: x64, x86, ARM64): `dotnet build -c Release`
- [x] Manual smoke test on primary platform (x64).
- [x] Verify no new warnings introduced.
- [x] Code coverage: Confirm no logic changed; only refactored for clarity.

 - [x] Run build (all platforms: x64, x86, ARM64): `dotnet build -c Release`

---

## Dependency Graph

```
┌─────────────────────────────────────────────────────────────┐
│                      MainWindow.xaml.cs                     │
│                    (God File → Refactor)                    │
└──────────┬──────────────────────────────────────────────────┘
           │
    ┌──────┴──────┬──────────┬─────────────┬──────────────┐
    │             │          │             │              │
┌───▼───────┐ ┌──▼──────┐ ┌─▼────────┐ ┌─▼────────┐ ┌──▼───────┐
│ Services: │ │ Helpers:│ │ Native:  │ │  XAML    │ │ Extract: │
│-WindowMgr │ │-PosHlpr │ │-WindowsAP│ │ MainWin  │ │ States   │
│-WindowRes │ │         │ │          │ │ SettingsW│ │ Init     │
│-HotKeyMgr │ │         │ │          │ │          │ │ Anim     │
│-TrayMgr   │ │         │ │          │ │          │ │          │
│-PresetMgr │ │         │ │          │ │          │ │          │
│-UserPref  │ │         │ │          │ │          │ │          │
└───┬───────┘ └──┬──────┘ └─┬────────┘ └──────────┘ └──────────┘
    │            │          │
    └────┬───────┴──────────┘
         │
    ┌────▼─────────┐
    │   Models:    │
    │- PresetSize  │
    │- WindowInfo  │
    │- ResizeResult│
    └──────────────┘
```

**Read-order**: Models → Services → Native/Helpers → UI → MainWindow extractions.

---

## Risk Assessment

### Potential Risks

1. **Risk: Breaking window visibility toggle or hotkey during MainWindow refactor**
   - **Mitigation**: Checkpoint 3 explicitly tests hotkey and show/hide. Refactor in phases (state manager first, UI init second) so each piece is testable in isolation.

2. **Risk: P/Invoke consolidation breaks tray or positioning**
   - **Mitigation**: Move P/Invoke in phases; test after each move. Build frequently.

3. **Risk: Preferences refactor breaks settings persistence**
   - **Mitigation**: Create unit test (or manual test) that sets each preference, reads it back, and verifies it persists across app restart.

4. **Risk: MainWindow becomes too large after refactoring (instead of smaller)**
   - **Mitigation**: Extract only concrete, cohesive concerns (state manager, initialization helper). Don't create micro-classes for every small method. Aim for ~400-450 lines and 5-7 fields (after extraction).

5. **Risk: Over-abstraction: Extract classes like `WindowStateManager` are simple enough to not need extraction**
   - **Mitigation**: This is a valid concern. However, reducing MainWindow field count and establishing clear state transitions (via a state manager) improves readability for future maintainers. Keep the extracted classes simple and focused; don't add unnecessary methods or properties.

### Mitigations

- **Frequent builds**: Run `dotnet build` after each phase.
- **Smoke tests**: Follow Checkpoints 1-4; don't skip any.
- **Code review**: After each phase, re-read the refactored code to ensure it's simpler and more understandable, not more complex.
- **Git commits**: Commit after each checkpoint to allow easy rollback if issues arise.
- **Keep extracted classes focused**: A state manager should manage state; an initializer should initialize; don't bloat them with unrelated logic.

---

## Implementation Notes

### Order of Execution (Recommended)

1. **Phase 1: Models & Data** (Quick win)
   - Review PresetSize, WindowInfo, ResizeResult.
   - Make minor documentation improvements if any.

2. **Phase 2: Services Utilities** (Medium effort, high impact)
   - Refactor UserPreferences (eliminate repetition).
   - This reduces ~70 lines and clarifies the codebase.

3. **Phase 3: P/Invoke Consolidation** (Medium effort, medium impact)
   - Extend WindowsApi with missing P/Invoke declarations.
   - Update TrayIconManager and WindowPositionHelper to use centralized WindowsApi.
   - Test tray and positioning after this phase.

4. **Phase 4: MainWindow Refactor** (High effort, high impact)
   - Execute in sub-phases (A-E) to allow testing at each step.
   - This is the highest-complexity work; take time to verify each step.

5. **Phase 5: Cleanup**
   - Delete empty directories.
   - Final build and smoke test.

### Estimated Effort

- **Phase 1 (Models)**: 15 minutes (review only)
- **Phase 2 (UserPreferences)**: 45 minutes
- **Phase 3 (P/Invoke)**: 60 minutes
- **Phase 4 (MainWindow)**: 2-3 hours (broken into sub-phases with testing)
- **Phase 5 (Cleanup)**: 15 minutes

**Total**: ~4-5 hours of focused work.

---

## Files Recommended for Deletion

### Empty Directory Structure

**All of the following are empty and should be removed:**

1. `ResizeMe/Services/HotKeys/`
2. `ResizeMe/Services/Preferences/`
3. `ResizeMe/Services/Presets/`
4. `ResizeMe/Services/Resizing/`
5. `ResizeMe/Services/Tray/`
6. `ResizeMe/Services/UI/`
7. `ResizeMe/Services/Windows/` (and `ResizeMe/Services/Windows/Filters/`)
8. `ResizeMe/Helpers/Positioning/Strategies/`

- **Reason**: Represents over-planning. No files currently use these organizational patterns, and the code is simpler with a flat service structure. If such organization becomes necessary in the future (e.g., multiple window filter strategies), these directories can be created with concrete use cases.

- **Impact**: No code changes; only deletes empty directories. Project file may have references to these folders; verify and remove any references.

- **Dependencies**: None (folders are empty).

- **Safety Checklist**:
  - [ ] Confirm all directories are empty (already verified).
  - [ ] Remove from `.csproj` file if explicitly referenced.
  - [ ] Run `dotnet build` to ensure no build errors after removal.
  - [ ] Commit deletion and verify git status is clean.

---

## Final Notes

This refactoring plan follows the principles in `refactor.instructions.md`:

1. **Optimize for humans**: Each refactoring reduces complexity and makes intent clearer.
2. **Minimal abstraction**: Extract only real duplication (UserPreferences property repetition) and real domain concepts (WindowStateManager).
3. **Straightforward control flow**: Consolidating P/Invoke and extracting initialization makes control flow more obvious.
4. **Domain-first structure**: Consolidating P/Invoke into `WindowsApi.cs` centralizes platform concerns; services remain focused on domain logic.
5. **Avoid over-engineering**: Delete empty folders; don't create them speculatively.
6. **Light documentation**: Add comments only where intent is non-obvious (e.g., why WinApiSubClass is used, what fields are cached vs. live).

After completing this plan, the codebase will be:
- **Simpler**: Fewer god files; clearer responsibilities.
- **Maintainable**: P/Invoke centralized; services focused; state transitions explicit.
- **Extensible**: Future features (e.g., more positioning strategies) can be added without architectural rework.
- **Understandable**: A new developer can read each class/method and understand its purpose in one pass.

# ResizeMe - Development Plan

## Project Overview

ResizeMe is a lightweight WinUI 3 utility that enables fast window resizing via a hotkey-activated floating menu of presets and a separate settings window for managing those presets. The goal is to consolidate the two-window UX into a single, unified, NavigationView-based interface that preserves quick-access behavior (hotkey, auto-positioning, auto-hide) while improving discoverability, maintainability, and scalability.

## Architecture & Technology Stack

### Recommended Approach
Use a single, floating `MainWindow` that embeds a `NavigationView` with frame-based navigation. Keep the current services (`PresetManager`, `WindowManager`, `WindowResizer`, `HotKeyManager`, `TrayIconManager`) and migrate the preset UI and the settings UI into separate pages (`PresetsPage`, `SettingsPage`) hosted inside the `NavigationView` content frame. Preserve the hotkey and auto-positioning behavior so the app remains a quick-access utility.

### Key Technologies
- WinUI 3 / Windows App SDK: native modern UI and controls (NavigationView, TabView, VisualStateManager).
- .NET 8: existing runtime in project.
- NavigationView + Frame navigation: structured single-window navigation.
- Adaptive Triggers / VisualStateManager: responsive layout for compact vs wide floating window.
- Existing service classes: `PresetManager`, `WindowResizer`, `WindowManager`, `HotKeyManager` (reused, not rewritten).

### High-Level Architecture
- MainWindow (floating; default ~500×400, adaptive)
  - NavigationView (left pane / compact behavior)
    - Menu: Quick Resize (PresetsPage), Settings (SettingsPage)
  - ContentFrame: hosts `PresetsPage` and `SettingsPage`
- Services (unchanged): `PresetManager`, `WindowManager`, `WindowResizer`, `HotKeyManager`, `TrayIconManager`
- Data persistence: existing JSON persistence in `PresetManager`

## Project Phases & PR Breakdown

### Phase 1: NavigationView Foundation
What this phase accomplishes: Replace the dual-window plumbing with a unified `MainWindow` scaffold that supports navigation and can host both the preset UI and the settings UI as pages. Preserve existing hotkey, positioning, and resizing behaviors.

#### PR 1.1: NavigationView Infrastructure
**Branch:** `feature/navigation-view-foundation`  
**Description:** Integrate `NavigationView` into `MainWindow` and add `ContentFrame` for page navigation.  
**Goal:** Prepare `MainWindow` to host `PresetsPage` and `SettingsPage`.  
**Key Components/Files:**
- `ResizeMe/MainWindow.xaml` — add `NavigationView` + `Frame` skeleton
- `ResizeMe/MainWindow.xaml.cs` — navigation selection handling, sizing defaults, preserve hotkey toggle hook
- `ResizeMe/Pages/PresetsPage.xaml` (initial stub)
- `ResizeMe/Pages/SettingsPage.xaml` (initial stub)  
**Dependencies:** None

#### PR 1.2: Preset Page Migration
**Branch:** `feature/presets-page-migration`  
**Description:** Migrate current preset menu UI and logic into `PresetsPage` (content within `NavigationView`).  
**Goal:** Preserve existing preset functionality and hotkey toggle while running inside the `NavigationView` content frame.  
**Key Components/Files:**
- `ResizeMe/Pages/PresetsPage.xaml` — full preset UI (cards, buttons, styling)
- `ResizeMe/Pages/PresetsPage.xaml.cs` — move code that generated dynamic preset buttons from `MainWindow.xaml.cs`
- Minor updates to `App.xaml` styles if needed for NavigationView context  
**Dependencies:** PR 1.1

### Phase 2: Settings Integration
What this phase accomplishes: Remove the modal `SettingsWindow` and convert its UI/logic into `SettingsPage` inside the `NavigationView`. Ensure real-time updates to presets and safe migration of data.

#### PR 2.1: Settings Page Creation
**Branch:** `feature/settings-page-integration`  
**Description:** Convert `SettingsWindow.xaml` and `SettingsWindow.xaml.cs` into `SettingsPage.xaml`/`.cs` and integrate into navigation.  
**Goal:** Eliminate the separate settings window; enable in-place preset management.  
**Key Components/Files:**
- `ResizeMe/Pages/SettingsPage.xaml` — two-column layout (Current Presets | Add/Edit controls)
- `ResizeMe/Pages/SettingsPage.xaml.cs` — move event handlers, validation, persistence calls to `PresetManager`
- Remove or deprecate `SettingsWindow.xaml` / `.cs` (keep until final verification)  
**Dependencies:** PR 1.2

#### PR 2.2: Adaptive Layout & Focus Handling
**Branch:** `feature/adaptive-navigation-layout`  
**Description:** Implement VisualState-based layout adjustments and ensure focus/auto-hide behavior works correctly with NavigationView.  
**Goal:** Ensure the floating window is usable in both compact and expanded states without breaking auto-hide or hotkey behavior.  
**Key Components/Files:**
- `ResizeMe/MainWindow.xaml` — VisualStateManager & `AdaptiveTrigger` rules
- `ResizeMe/MainWindow.xaml.cs` — focus-handling and auto-hide tweaks (when navigation pane opened/closed)
- Unit/Integration checks for HotKey toggle behavior  
**Dependencies:** PR 2.1

### Phase 3: Enhanced User Experience
What this phase accomplishes: Add productivity features (inline edit, search/filter, categories) and polish visual details to match Windows 11 Fluent Design.

#### PR 3.1: Inline Preset Editing & Quick Actions
**Branch:** `feature/inline-preset-editing`  
**Description:** Add quick edit and flyout actions for presets directly from `PresetsPage`.  
**Goal:** Allow fast edits without leaving the main quick-resize flow.  
**Key Components/Files:**
- `ResizeMe/Pages/PresetsPage.xaml` — flyouts, edit controls, inline modes
- `ResizeMe/Pages/PresetsPage.xaml.cs` — preview logic, undo/cancel flows
- `ResizeMe/Services/PresetManager.cs` — small API extension for edit operations  
**Dependencies:** PR 1.2, PR 2.1

#### PR 3.2: Search, Filtering & Categories
**Branch:** `feature/preset-search-organization`  
**Description:** Add a search box and category filters; extend `PresetManager` for categories/tags.  
**Goal:** Keep the UI usable as preset count grows.  
**Key Components/Files:**
- `ResizeMe/Pages/PresetsPage.xaml` — search box in header, filter UI
- `ResizeMe/Services/PresetManager.cs` — add search/filter API
- `ResizeMe/Pages/SettingsPage.xaml` — category management controls  
**Dependencies:** PR 3.1

---

## Implementation Sequence

1. Phase 1 (PR 1.1 → PR 1.2) establishes the foundation and migrates presets.
2. Phase 2 (PR 2.1 → PR 2.2) migrates and polishes settings as a page.
3. Phase 3 (PR 3.1 → PR 3.2) implements extra UX features and scalability improvements.

PRs within each phase can be worked in parallel where dependencies allow (e.g., UI styling updates vs service API additions) but NavigationView foundation (PR 1.1) is the critical path.

## Testing Strategy

- Unit tests around `PresetManager` persistence and CRUD operations.
- Integration checks:
  - Hotkey toggling still shows/hides `MainWindow` reliably.
  - Window auto-positioning near active window preserved.
  - Preset application resizes target windows as before.
- Manual UX tests:
  - Navigation between Presets and Settings pages behaves correctly.
  - No regressions in focus/auto-hide when interacting with `NavigationView`.
- Regression verification: run existing manual test scenarios that exercised the old modal settings workflow.

## Success Criteria

- Single `MainWindow` replaces dual-window UX while preserving:
  - Hotkey behavior (Win+Shift+F12).
  - Auto-positioning and auto-hide.
  - Preset application behavior (resizing correctness).
- Settings are accessible in-place and allow full management of presets.
- Visual polish consistent with Windows 11 Fluent Design and responsive layout.
- No data loss: existing preset JSON remains valid and readable by new UI.

## Known Constraints & Considerations

- Window sizing: the floating UI needs slightly more default width (recommended ~500×400) to accommodate NavigationView without feeling cramped.
- Focus/auto-hide: NavigationView may change focus behavior; ensure pressing the hotkey returns focus to the window in a predictable way.
- Backward compatibility: preserve or migrate existing `SettingsWindow` until the new page is verified.
- Performance: main path (hotkey → show → apply preset) should remain fast; avoid heavy initialization on show.
- Accessibility: maintain keyboard navigation and screen-reader compatibility inside the NavigationView pages.

---

If you approve this plan I will:
- Stop here and wait for your confirmation (per the planning workflow), or
- When you confirm, produce the explicit PR patches and implement Phase 1 (starting with `feature/navigation-view-foundation`), including code changes and a build/test run.

Would you like me to proceed with implementing Phase 1 now, or revise the plan first?

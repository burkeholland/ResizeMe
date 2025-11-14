# ResizeMe UI Refresh - Development Plan

## Project Overview

Enhance the visual polish, usability, and professionalism of ResizeMe by refining the floating preset menu, title bar layout, and Settings window. Target users are power users and developers wanting fast window resizing with a clean, modern Windows 11 Fluent aesthetic.

## Architecture & Technology Stack

### Recommended Approach
Leverage existing WinUI 3 layout primitives and resource dictionaries for styling changes without introducing new dependencies. Keep logic intact—limit changes to XAML, styles, and lightweight helper adjustments for adaptive sizing.

### Key Technologies
- WinUI 3 (Windows App SDK): Native Fluent controls and styling.
- AppWindow / ExtendsContentIntoTitleBar: Custom chrome and layout control.
- ResourceDictionary Styles: Centralized theming for buttons, text, spacing.

### High-Level Architecture
Window surfaces:
- MainWindow: Floating context menu (host for presets, status, title bar).
- SettingsWindow: Modal-style configuration surface.
Supporting services (unchanged): PresetManager, WindowResizer, TrayIconManager.
Focus only on presentation layer—no changes to service/data architecture.

## Project Phases & PR Breakdown

### Phase 1: UI Look & Feel Update
Refactor visual layout for clarity and professionalism: fix truncated preset text, prevent overlap of Settings button with system close control, streamline spacing, and redesign Settings window to a balanced, responsive layout with improved typography and interaction states.

#### PR 1.1: Unified UI Refresh
**Branch:** `ui-refresh-phase-1`
**Description:** Implements visual/style updates to main floating menu and Settings window.
**Goal:** Deliver a polished, correctly sized, accessible UI matching Fluent guidance.
**Key Components/Files:**
- `MainWindow.xaml` (layout, sizing, title bar regions)
- `SettingsWindow.xaml` (responsive sizing, spacing, control grouping)
- `App.xaml` (style additions/updates: buttons, text styles, spacing tokens)
- `MainWindow.xaml.cs` (optional minor adjustments for initial size / focus logic)
**Dependencies:** None

---

## Implementation Sequence
1. Phase 1 (single PR). Merge after visual QA.

## Testing Strategy
- Manual DPI checks (100%, 125%, 150%).
- Verify keyboard navigation order in both windows.
- High contrast theme smoke test.
- Confirm preset buttons never truncate at planned min window width.
- Validate Settings form: add/remove/reset flows unaffected.

## Success Criteria
- No overlapping title bar elements at any scale.
- Preset buttons fully readable; active state visually distinct.
- Settings window presents balanced layout (reasonable max width ~600–680px, height ~auto with scroll if overflow).
- Accessibility: tab traversal reaches all interactive elements; focus visuals present.
- No regressions in resize functionality or preset management.

## Known Constraints & Considerations
- Must not alter existing hotkey/tray logic.
- Avoid expanding window footprint excessively—floating menu should remain quick and unobtrusive.
- Future theming extensibility: keep styles in `App.xaml` for potential dark/light adjustments.
- Localization not yet implemented; avoid hard-coded layouts that assume English length extremes.

---

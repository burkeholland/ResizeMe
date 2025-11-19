# ResizeMe - Comprehensive Refactoring Plan

## Overview

- **Total files to refactor**: 9
- **Total files to delete**: 0
- **Estimated complexity**: Medium-High
- **Execution order rationale**: Dependencies first (Models & Native), then Services (which depend on Models), then Helpers, then UI layers (which depend on Services)

---

## Files to Refactor (In-Place Updates)

### [Priority 1] MainWindow.xaml.cs

**File**: `ResizeMe/MainWindow.xaml.cs`

**Current issues**:
- Massive single file with 1000+ lines mixing UI logic, event handling, window management, and animation concerns
- Multiple responsibilities: visibility toggling, preset loading, window positioning, hotkey registration, state management, animation
- Deep nesting in methods (10+ levels) makes code hard to follow
- Duplicated code for style and resource lookups scattered throughout
- Mixture of concerns: window state (_isVisible, _isAlwaysOnTop, _presetIndex), button generation, resizing operations
- Error handling is inconsistent (some methods swallow exceptions, others propagate)
- Animated show/hide logic is tightly coupled to UI
- First-run flow is tangled with window setup

**Objectives**:
1. Extract window state management into dedicated handler classes
2. Extract preset button generation into a separate presenter/factory
3. Extract animation logic into a reusable component
4. Extract hotkey initialization into a setup service
5. Break down large methods into smaller, single-responsibility methods
6. Consolidate resource lookups
7. Separate concerns: UI event handlers, state management, business logic

**Actions**:
1. Create `WindowStateManager` class to handle visibility, always-on-top, preset tracking
2. Create `PresetButtonBuilder` class to handle preset button generation and styling
3. Create `MainWindowAnimator` class to handle show/hide animations
4. Create `FirstRunSetupService` to handle first-run experience
5. Extract window positioning logic calls into a dedicated `WindowPositioner` service
6. Consolidate all resource lookups into a `UIResourceProvider` or similar
7. Break down `MainWindow` constructor into smaller initialization methods
8. Reduce method sizes to 15-20 lines maximum where possible
9. Extract `PresetButton_Click` and `OnKeyDown` into focused event handlers
10. Replace inline debug/status text updates with a centralized status service

**Dependencies**: 
- WindowManager (services)
- WindowResizer (services)
- PresetManager (services)
- HotKeyManager (services)
- TrayIconManager (services)
- WindowPositionHelper (helpers)
- All Models

**Complexity**: **High**

---

### [Priority 2] SettingsWindow.xaml.cs

**File**: `ResizeMe/SettingsWindow.xaml.cs`

**Current issues**:
- Complex hotkey capture logic mixed with UI event handling
- Hotkey validation and reserved key checking scattered across multiple methods
- String parsing for key mapping is fragmented and could be centralized
- Reflection-based re-registration is hacky and tightly couples Settings to MainWindow
- Preset validation logic is basic and could be more robust
- Input validation for dimensions scattered across methods
- Error messages shown directly with no abstraction
- Hotkey capture state (_capturing, _capturedKey, _capturedModifiers) is fragile and could be a separate class

**Objectives**:
1. Extract hotkey capture logic into dedicated `HotKeyCaptureManager` class
2. Extract key mapping logic into a `KeyMapper` utility
3. Create `PresetValidator` for validation logic
4. Create a service interface for re-registering hotkeys instead of reflection hack
5. Consolidate input validation helpers
6. Extract UI message display into a notification service
7. Simplify event handlers

**Actions**:
1. Create `HotKeyCaptureManager` class to encapsulate capture state and logic
2. Extract `MapKey` into `KeyMapper.MapVirtualKeyToToken(VirtualKey)` utility
3. Create `PresetValidator.ValidateDimensions()` and `ValidatePresetName()` methods
4. Create `IHotKeyReRegistrationService` interface and implementation
5. Replace reflection-based hotkey re-registration with service injection
6. Extract input validation into methods like `ValidateDimensionInput()`, `ValidatePresetNameInput()`
7. Simplify `AddButton_Click`, `RemoveButton_Click` to use extracted validation
8. Create `NotificationService` to handle error/success messages

**Dependencies**: 
- PresetManager (services)
- HotKeyManager (services)
- UserPreferences (services)
- MainWindow (for re-registration - will be replaced with service)

**Complexity**: **Medium-High**

---

### [Priority 3] WindowManager.cs

**File**: `ResizeMe/Services/WindowManager.cs`

**Current issues**:
- Large exclusion lists (class names, titles) are hardcoded and difficult to maintain
- Window filtering logic has multiple deeply nested conditions
- Validation logic for "is resizable" is mixed with enumeration concerns
- Comments reference future steps (DWM attribute check) but could be clearer
- Some error handling is too broad (catches all exceptions)
- Window title filtering is case-sensitive in some places, not others
- Debug output is verbose but could be more structured
- `GetWindowInfo` does too much and could be split

**Objectives**:
1. Extract exclusion lists into configuration/constants
2. Extract window validation logic into dedicated filter chain/strategy classes
3. Separate window enumeration from window validation
4. Create structured debug logging helpers
5. Improve case-handling consistency
6. Reduce method sizes
7. Make filtering rules extensible

**Actions**:
1. Create `WindowExclusionFilters` static class with configurable exclusion lists
2. Create `IWindowFilter` interface with implementations: `ExcludedClassFilter`, `ExcludedTitleFilter`, `VisibilityFilter`, `SizeFilter`, `ToolWindowFilter`, `CloakedWindowFilter`, `CurrentProcessFilter`, `ProblemWindowFilter`
3. Create `WindowFilterChain` that applies multiple filters
4. Extract window info retrieval into `WindowInfoBuilder` class
5. Extract window validation into `WindowValidator` that uses filter chain
6. Refactor `GetResizableWindows()` to use filter chain
7. Simplify `IsResizableWindow()` to use filters instead of inline checks
8. Add structured logging utility for debug output
9. Consolidate error handling with specific exception types

**Dependencies**: 
- WindowInfo model
- WindowsApi (native)

**Complexity**: **Medium**

---

### [Priority 4] WindowResizer.cs

**File**: `ResizeMe/Services/WindowResizer.cs`

**Current issues**:
- Error message mapping is hardcoded dictionary - could be more maintainable
- Window state restoration logic is duplicated between `RestoreWindow` and inline checks
- Magic numbers for error codes scattered through code
- `GetWindowState` returns enum but state checking is also done inline elsewhere
- Exception handling inconsistent (some catch all, some specific)
- Comments indicate "Fallback" but logic isn't clearly explained
- Large method `ResizeWindow` with multiple concerns
- No logging beyond debug output

**Objectives**:
1. Extract error code mapping into dedicated service
2. Extract window state restoration into service
3. Extract window validation into service
4. Reduce `ResizeWindow` method size by extracting steps
5. Improve error handling specificity
6. Add operation logging

**Actions**:
1. Create `WindowResizeErrorHandler` to map error codes to messages
2. Create `WindowStateRestorer` to encapsulate restoration logic
3. Create `WindowValidator` to validate handles and check window existence
4. Extract resize operation into smaller steps: validate → restore → capture position → perform resize → verify
5. Add comprehensive logging for each step
6. Replace magic error codes with named constants
7. Improve exception categorization

**Dependencies**: 
- WindowInfo model
- WindowSize model
- ResizeResult model
- WindowsApi (native)

**Complexity**: **Medium**

---

### [Priority 5] HotKeyManager.cs

**File**: `ResizeMe/Services/HotKeyManager.cs`

**Current issues**:
- Key translation logic is centralized but could be more extensible
- Reserved hotkey list is hardcoded and could be data-driven
- Modifier mask building repeated pattern could be abstracted
- Virtual key constants scattered across codebase (some in here, some in WindowsApi)
- Error code handling is basic (only checks for 1409)
- String parsing for modifiers could be more robust
- No validation of key tokens before translation

**Objectives**:
1. Create extensible key translation system
2. Extract reserved hotkey checking into service
3. Centralize modifier/key parsing logic
4. Improve error code handling
5. Add key validation

**Actions**:
1. Create `KeyTranslator` service with methods like `TranslateModifierToken()`, `TranslateKeyToken()`
2. Create `ReservedHotKeyValidator` with data-driven reserved key list
3. Create `ModifierMaskBuilder` to build modifier bitmasks from tokens
4. Extract `TranslateKeyToken()` into `KeyTranslator.TranslateKey()`
5. Extract modifier mask building into `KeyTranslator.BuildModifierMask()`
6. Create more specific error handling for RegisterHotKey failures
7. Add key token validation before translation

**Dependencies**: 
- WindowsApi (native)
- UserPreferences (services)

**Complexity**: **Low-Medium**

---

### [Priority 6] TrayIconManager.cs

**File**: `ResizeMe/Services/TrayIconManager.cs`

**Current issues**:
- P/Invoke declarations are verbose and scattered throughout the class
- Menu command constants mixed with flags and message constants
- Context menu creation is inline and could be extracted
- Hard to extend with new menu items
- Icon loading has dual fallback logic that could be cleaner
- Win32 structures mixed with service logic

**Objectives**:
1. Extract P/Invoke declarations into separate file
2. Consolidate constants into organized groups
3. Extract context menu building into factory
4. Make menu items configurable/extensible
5. Extract icon loading into dedicated service
6. Improve error handling

**Actions**:
1. Create `TrayNativeApi.cs` file with all P/Invoke declarations and structures
2. Create `TrayConstants.cs` for menu commands, flags, and message constants
3. Create `TrayContextMenuBuilder` to handle menu creation and item management
4. Create `TrayIconLoader` to handle icon loading with fallback logic
5. Refactor `TrayIconManager` to use these new services
6. Make menu items configurable (factory pattern)

**Dependencies**: 
- None (self-contained)

**Complexity**: **Medium**

---

### [Priority 7] PresetManager.cs

**File**: `ResizeMe/Services/PresetManager.cs`

**Current issues**:
- JSON serialization options repeated for each save
- File I/O error handling is too broad (swallows all exceptions)
- Validation of presets is minimal (only checks IsValid)
- Default seeding is hardcoded
- Thread synchronization with `_syncRoot` is good but could be abstracted
- Async loading could handle more failure modes
- No duplicate detection on load (could have malformed file)

**Objectives**:
1. Extract JSON serialization options to constant
2. Extract preset validation into dedicated validator
3. Extract default presets into configuration
4. Improve error handling specificity
5. Add duplicate detection during load
6. Create file operations abstraction

**Actions**:
1. Create `PresetValidator` to validate individual and collections of presets
2. Create `DefaultPresetsProvider` to return default presets
3. Create `PresetStorageService` for file I/O operations (separate from business logic)
4. Extract JSON options to `PresetSerializationOptions` constant
5. Add specific exception handling instead of broad catches
6. Add duplicate detection during load with logging
7. Improve error recovery (e.g., attempt to validate loaded data)
8. Add method to get validation errors instead of silent failures

**Dependencies**: 
- PresetSize model

**Complexity**: **Low-Medium**

---

### [Priority 8] UserPreferences.cs

**File**: `ResizeMe/Services/UserPreferences.cs`

**Current issues**:
- Massive property repetition (get/set pattern identical for each property)
- String constants repeated throughout (key names)
- Try-catch blocks identical for every property (swallow all exceptions)
- Default values scattered across properties
- No type safety - all stored as objects
- Hard to add new preferences without boilerplate
- No validation of preference values

**Objectives**:
1. Reduce boilerplate with generic preference accessor
2. Centralize string constants
3. Improve error handling without sacrificing robustness
4. Add validation and type safety
5. Create preference definition structure

**Actions**:
1. Create `PreferenceKey` enum to replace string constants
2. Create `PreferenceDefinition<T>` class to define preferences with metadata (default value, validation)
3. Create `PreferenceStore` generic base class with common get/set logic
4. Refactor `UserPreferences` to use `PreferenceDefinition` for each property
5. Extract get/set logic into generic helper methods
6. Create `IPreferenceValidator` for validation
7. Add typed access methods instead of direct property storage
8. Create method to bulk-get all preferences

**Dependencies**: 
- ApplicationData (WinRT)

**Complexity**: **Medium**

---

### [Priority 9] WindowPositionHelper.cs

**File**: `ResizeMe/Helpers/WindowPositionHelper.cs`

**Current issues**:
- P/Invoke declarations mixed with logic (should be separated)
- Constants and structures mixed with implementation
- Multiple similar positioning methods with duplication
- DPI awareness missing (not accounting for monitor scaling)
- Hard-coded offset values (OFFSET_FROM_CURSOR = 20)
- Complex nested conditionals in adjustment logic
- Bounds checking logic could be extracted
- No abstraction for screen/monitor operations

**Objectives**:
1. Separate P/Invoke into dedicated file
2. Extract positioning strategies into strategy classes
3. Extract bounds checking into dedicated service
4. Extract monitor information retrieval
5. Reduce code duplication in positioning methods
6. Add configurability for offsets and margins
7. Improve readability of adjustment logic

**Actions**:
1. Create `WindowPositionNativeApi.cs` for P/Invoke declarations and structures
2. Create `ScreenBoundsProvider` service for monitor/work area retrieval
3. Create `WindowBoundsAdjuster` service for keeping window within bounds
4. Create positioning strategy classes: `CursorRelativePositioner`, `ScreenCenterPositioner`, `WindowCenterPositioner`
5. Create `PositionConfiguration` class for offsets and margins
6. Refactor existing position methods to use strategies
7. Extract bounds adjustment logic into separate methods
8. Add DPI awareness (if needed based on testing)

**Dependencies**: 
- Window (WinUI)
- WindowInfo model
- WindowsApi (native)

**Complexity**: **Medium**

---

## Summary of Refactoring Benefits

### Maintainability
- **Before**: 1000+ line files with mixed concerns
- **After**: Small, focused classes (50-200 lines) with single responsibility
- Large classes broken into cohesive, testable units

### Testability
- **Before**: Hard to unit test due to tight coupling and mixed concerns
- **After**: Extracted services can be tested independently with mocks
- Clear dependency injection points

### Extensibility
- **Before**: Hard to add new features (e.g., new positioning strategies, new validation rules)
- **After**: Strategy patterns, filter chains, and service-based architecture allow easy extension

### Code Reuse
- **Before**: Validation logic, positioning logic, error handling duplicated across files
- **After**: Centralized, reusable services (e.g., `KeyTranslator`, `ScreenBoundsProvider`, `WindowBoundsAdjuster`)

### Debugging & Maintenance
- **Before**: Scattered debug output, mixed concerns make debugging difficult
- **After**: Clear method names, focused responsibilities, consistent logging

### Error Handling
- **Before**: Inconsistent error handling (some swallow exceptions, some propagate)
- **After**: Specific exception types, consistent error recovery patterns

---

## Dependency Graph

```
Models (Independent)
├── PresetSize
├── WindowInfo
├── WindowBounds
├── WindowSize
└── ResizeResult

Native Layer (Independent)
├── WindowsApi

Configuration & Constants (Independent)
├── TrayConstants
├── PreferenceKey (enum)
├── PreferenceDefinition

Utility & Helper Services (Depend on Models & Native)
├── KeyTranslator (WindowsApi)
├── KeyMapper
├── ScreenBoundsProvider (WindowsApi)
├── WindowBoundsAdjuster
├── WindowPositionNativeApi (WindowsApi)
├── TrayNativeApi (WindowsApi)
├── WindowResizeErrorHandler
├── WindowStateRestorer (WindowsApi)
├── WindowValidator (WindowsApi)
├── PresetValidator (PresetSize)
├── DefaultPresetsProvider (PresetSize)
├── PresetStorageService (PresetSize, FileIO)
├── TrayIconLoader
├── ReservedHotKeyValidator

Core Services (Depend on Utils, Models, Native)
├── UserPreferences (PreferenceStore, PreferenceValidator)
├── PresetManager (PresetValidator, DefaultPresetsProvider, PresetStorageService)
├── HotKeyManager (KeyTranslator, ReservedHotKeyValidator, WindowsApi)
├── TrayIconManager (TrayNativeApi, TrayContextMenuBuilder, TrayIconLoader)
├── WindowManager (WindowValidator, PresetManager, WindowsApi)
├── WindowResizer (WindowValidator, WindowStateRestorer, WindowResizeErrorHandler, WindowsApi)

Helpers (Depend on Core Services)
├── WindowPositionHelper (ScreenBoundsProvider, WindowBoundsAdjuster, WindowPositionNativeApi)

Presenters/Builders (Depend on Services & Models)
├── PresetButtonBuilder (PresetManager)
├── MainWindowAnimator
├── TrayContextMenuBuilder

UI Layer (MainWindow, SettingsWindow - Depend on all above)
├── MainWindow (WindowManager, WindowResizer, PresetManager, HotKeyManager, TrayIconManager, WindowPositionHelper, MainWindowAnimator, PresetButtonBuilder, FirstRunSetupService)
├── SettingsWindow (PresetManager, HotKeyManager, UserPreferences, HotKeyCaptureManager, KeyMapper, PresetValidator)
```

---

## Risk Assessment

### Breaking Changes
- **Reflection-based hotkey re-registration** in SettingsWindow will be replaced with service-based approach - requires interface definition
- **P/Invoke declarations** will be moved to separate files - refactor all call sites to use new locations
- **String constants** will be replaced with enums in some cases - may require conscious migration

### Mitigation Strategies
1. **Define service interfaces first** before moving implementations to ensure compatibility
2. **Create adapters/facades** if needed to maintain existing APIs during transition
3. **Test heavily** at each layer after refactoring to catch breaking changes early
4. **Preserve existing method signatures** where possible (add new methods, deprecate old ones)
5. **Create unit tests** for critical paths before refactoring to ensure correctness

### Testing Strategy
1. **Unit test extracted services** individually with mocks
2. **Integration test** service combinations
3. **UI smoke tests** to verify mainWindow and settings workflows still function
4. **Hotkey registration testing** after changes to ensure it still works
5. **Window positioning testing** with different monitor configurations

---

## Execution Notes

- Start with Models and Native abstractions (no dependencies)
- Build up Services layer (depends on models/native)
- Then UI layer (depends on services)
- Use feature flagging or interfaces to maintain backwards compatibility during refactoring
- Each refactoring should be a separate commit with clear message
- Run full test suite after each major refactoring step
- Consider creating a "Refactoring" or "Cleanup" branch to isolate these changes

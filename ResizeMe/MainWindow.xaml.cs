using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text;
using ResizeMe.Native;
using ResizeMe.Services;
using ResizeMe.Models;
using ResizeMe.Helpers;
using WinRT.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;

namespace ResizeMe
{
    /// <summary>
    /// Floating context menu window providing preset resize buttons with toggle behavior.
    /// Responsibilities: rendering presets, handling show/hide lifecycle, delegating window
    /// enumeration/resizing to services, and wiring up initialization, hotkeys, and tray.
    /// Keeps UI logic minimal by delegating domain concerns to services/helpers.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public event RoutedEventHandler? Loaded;
        /// <summary>Manager handling global hotkey registration and processing for the app window.</summary>
        private HotKeyManager? _hotKeyManager;
        /// <summary>Service enumerating windows and determining resizable windows.</summary>
        private WindowManager? _windowManager;
        /// <summary>Service that performs resize operations on external windows.</summary>
        private WindowResizer? _windowResizer;
        private readonly PresetManager _presetManager = new();
        private readonly PresetPresenter _presetPresenter;
        private StatusManager? _statusManager;
        /// <summary>Native handle for the app's Win32 window.</summary>
        private IntPtr _windowHandle = IntPtr.Zero;
        /// <summary>WinUI AppWindow wrapper for the main window (used for icon/size).</summary>
        private AppWindow? _appWindow;
        /// <summary>Indicates whether the native window subclass for message interception is registered.</summary>
        private bool _isSubclassRegistered;
        private readonly WindowStateManager _stateManager = new();
        /// <summary>Currently selected external window to operate on.</summary>
        private WindowInfo? _selectedWindow;
        /// <summary>Cache of available resizable windows discovered on refresh.</summary>
        private List<WindowInfo> _availableWindows = new();
        // active preset, preset index and center-on-resize are now managed by _stateManager
        private TrayIconManager? _trayIcon;

        // Window message constants for system commands & tray interaction
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_COMMAND = 0x0111;
        private const int WM_RBUTTONUP = 0x0205; // Right mouse button up

        /// <summary>Delegate for the Win32 window subclass procedure.</summary>
        private WinApiSubClass.SubClassProc? _subclassProc;
        private WindowMessageHandler? _messageHandler;
        /// <summary>Application-defined ID used when registering the window subclass.</summary>
        private readonly IntPtr _subClassId = new(1001);
        /// <summary>Throttle toggles to avoid rapid accidental opens/closes.</summary>
        private DateTime _lastToggle = DateTime.MinValue;

        private class WindowMessageHandler
        {
            private readonly MainWindow _owner;
            public WindowMessageHandler(MainWindow owner) { _owner = owner; }
            public IntPtr Handle(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
            {
                if (uMsg == WM_SYSCOMMAND && wParam.ToInt32() == SC_CLOSE)
                {
                    _owner.HideWindow();
                    return IntPtr.Zero;
                }
                if (_owner._trayIcon != null && uMsg == (uint)_owner._trayIcon.TrayCallbackMessage)
                {
                    int lParamVal = lParam.ToInt32();
                    if (lParamVal == WM_RBUTTONUP)
                    {
                        _owner._trayIcon.ShowContextMenu();
                        return IntPtr.Zero;
                    }
                    if (lParamVal == 0x0202)
                    {
                        _owner.ToggleVisibility();
                        return IntPtr.Zero;
                    }
                }
                if (_owner._hotKeyManager?.ProcessMessage(uMsg, wParam, lParam) == true) return IntPtr.Zero;
                return WinApiSubClass.DefSubclassProc(hWnd, uMsg, wParam, lParam);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            _presetPresenter = new PresetPresenter(_presetManager);
            _windowManager = new WindowManager();
            _windowResizer = new WindowResizer();
            AttachWindowLoadedHandler();
            Loaded += async (_, _) =>
            {
                await _presetManager.LoadAsync();
                    LoadPresetButtons();
                DispatcherQueue.TryEnqueue(() => CheckFirstRunAndShowSettings());
            };
            UIInitializer.Initialize(this);
            AttachKeyDownHandler();
            Activated += OnWindowActivated;
            Closed += Window_Closed;

            // Load persisted "center on resize" preference into state manager
            try
            {
                var persisted = ResizeMe.Services.UserPreferences.CenterOnResize;
                _stateManager.SetCenterOnResize(persisted);
                if (CenterOnResizeToggle != null)
                {
                    CenterOnResizeToggle.IsOn = persisted;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error reading CenterOnResize preference: {ex.Message}");
            }
        }

        private void AttachWindowLoadedHandler()
        {
            if (Content is FrameworkElement root)
            {
                root.Loaded += Root_Loaded;
            }
            else
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await _presetManager.LoadAsync();
                    LoadPresetButtons();
                });
            }
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            if (Content is FrameworkElement root)
            {
                root.Loaded -= Root_Loaded;
            }
            Loaded?.Invoke(this, e);
        }

        private void AttachKeyDownHandler()
        {
            if (Content is FrameworkElement element)
            {
                element.KeyDown -= OnKeyDown;
                element.KeyDown += OnKeyDown;
            }
        }

        private void SetupWindowAppearance()
        {
            try
            {
                Title = "ResizeMe";
                // Use standard Windows title bar - no custom chrome needed
                EnsureWindowHandle();
                if (_windowHandle != IntPtr.Zero)
                {
                    var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
                    _appWindow = AppWindow.GetFromWindowId(windowId);
                    // Set window icon
                    try
                    {
                        var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
                        if (System.IO.File.Exists(iconPath))
                        {
                            _appWindow?.SetIcon(iconPath);
                        }
                    }
                    catch (Exception iconEx)
                    {
                        Debug.WriteLine($"Failed to set window icon: {iconEx.Message}");
                    }
                    // Modern card-based layout with minimal width and increased vertical space for more items
                    _appWindow?.Resize(new SizeInt32(320, 540));
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                }
                _stateManager.Hide();
                // First minimize tray notification
                try
                {
                    if (!Services.UserPreferences.FirstMinimizeNotificationShown)
                    {
                        EnsureWindowHandle();
                        string hotkey = "Ctrl+Win+R"; // Will be dynamic in later steps
                        var message = $"ResizeMe is running in the system tray. Right-click the tray icon for Settings or Exit. Press {hotkey} to open the quick resize window.";
                        WindowsApi.MessageBoxW(IntPtr.Zero, message, "ResizeMe Running", WindowsApi.MB_OK | WindowsApi.MB_TOPMOST);
                        // Some hosts may reactivate the main window when the message box closes.
                        // Ensure we remain hidden so first-run users stay in the tray immediately.
                        try
                        {
                                if (_windowHandle != IntPtr.Zero)
                                {
                                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                                    _stateManager.Hide();
                                }
                        }
                        catch (Exception hideEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"First minimize message re-hide error: {hideEx.Message}");
                        }
                        Services.UserPreferences.FirstMinimizeNotificationShown = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"First minimize notification error: {ex.Message}");
                }
                _statusManager?.SetStatus("Ready", TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetupWindowAppearance error: {ex.Message}");
            }
        }

    internal static class UIInitializer
    {
        public static void Initialize(MainWindow window)
        {
            try
            {
                window.SetupWindowAppearance();
                // Ensure hotkeys and tray icon are initialized when the app becomes activated
                window.EnsureHotKeyRegistration();
                // Run first-run check after appearance and hotkey/tray setup
                try { window.CheckFirstRunAndShowSettings(); } catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UIInitializer: Initialization error: {ex.Message}");
            }
        }
    }

        private void EnsureWindowHandle()
        {
            if (_windowHandle != IntPtr.Zero) return;
            _windowHandle = WindowNative.GetWindowHandle(this);
            if (_windowHandle != IntPtr.Zero && _appWindow == null)
            {
                var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
                _appWindow = AppWindow.GetFromWindowId(windowId);
            }
        }

        private void EnsureHotKeyRegistration()
        {
            EnsureWindowHandle();
            if (_windowHandle == IntPtr.Zero || _hotKeyManager != null) return;
            _hotKeyManager = new HotKeyManager(_windowHandle);
            _hotKeyManager.HotKeyPressed += (_, _) => ToggleVisibility();
            bool success = _hotKeyManager.RegisterHotKey();
            if (success)
            {
                // Show the configured hotkey in status
                _statusManager?.SetStatus($"Ready ({Services.UserPreferences.HotKeyModifiers}+{Services.UserPreferences.HotKeyCode})", TimeSpan.Zero);
            }
            else
            {
                _statusManager?.SetStatus("Hotkey failed", TimeSpan.Zero);
            }
            if (!_isSubclassRegistered)
            {
                _messageHandler = new WindowMessageHandler(this);
                _subclassProc = _messageHandler.Handle;
                if (WinApiSubClass.SetWindowSubclass(_windowHandle, _subclassProc, _subClassId, IntPtr.Zero))
                {
                    _isSubclassRegistered = true;
                }
            }
            if (_trayIcon == null && _windowHandle != IntPtr.Zero)
            {
                _trayIcon = new TrayIconManager(_windowHandle);
                if (_trayIcon.Initialize())
                {
                    Debug.WriteLine("Tray icon initialized");
                    // Hook tray events to main window actions
                    _trayIcon.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(ToggleVisibility);
                    _trayIcon.SettingsRequested += (_, _) => DispatcherQueue.TryEnqueue(OpenSettingsWindow);
                    _trayIcon.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(PerformExit);
                }
            }
            HideWindow();
        }

        private void ToggleVisibility()
        {
            // Debounce toggles occurring too fast (accidental repeats)
            if ((DateTime.UtcNow - _lastToggle).TotalMilliseconds < 150) return;
            _lastToggle = DateTime.UtcNow;

            if (_stateManager.IsVisible) HideWindow(); else _ = ShowWindowAsync();
        }

        private async System.Threading.Tasks.Task ShowWindowAsync()
        {
            try
            {
                EnsureWindowHandle();
                RefreshWindowList();
                // Ensure presets are loaded before rendering UI controls so the first show isn't blank
                try
                {
                    if (!_presetManager.IsLoaded)
                    {
                        await _presetManager.LoadAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MainWindow: Preset load failed during ShowWindow: {ex.Message}");
                }
                var anchorWindow = _windowManager?.GetActiveResizableWindow() ?? _availableWindows.FirstOrDefault();
                if (anchorWindow != null) WindowPositionHelper.CenterOnWindow(this, anchorWindow); else WindowPositionHelper.CenterOnScreen(this);
                if (_windowHandle != IntPtr.Zero)
                {
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_RESTORE);
                    WindowsApi.SetForegroundWindow(_windowHandle);
                    ApplyAlwaysOnTop();
                }
                Activate();
                _stateManager.Show();
                _statusManager?.SetStatus("Menu shown", TimeSpan.Zero);
                LoadPresetButtons();
                if (StatusText != null) _statusManager = new StatusManager(StatusText);
                // Do not animate show — display immediately for responsiveness.
            }
            catch (Exception ex)
            {
                _statusManager?.SetStatus("Show error", TimeSpan.Zero);
                Debug.WriteLine($"ShowWindow error: {ex.Message}");
            }
        }

        private void HideWindow()
        {
            if (!_stateManager.IsVisible)
            {
                try
                {
                    EnsureWindowHandle();
                    if (_windowHandle != IntPtr.Zero)
                    {
                        WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"HideWindow redundant hide error: {ex.Message}");
                }
                return;
            }
            try
            {
                // Hide immediately — no animation for quick responsiveness.
                EnsureWindowHandle();
                if (_windowHandle != IntPtr.Zero) WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                _stateManager.Hide();
                _statusManager?.SetStatus("Hidden", TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HideWindow error: {ex.Message}");
            }
        }

        private void RefreshWindowList()
        {
            try
            {
                if (_windowManager == null) return;
                _availableWindows = _windowManager.GetResizableWindows().ToList();
                var activeWindow = _windowManager.GetActiveResizableWindow();
                if (activeWindow != null)
                {
                    _selectedWindow = activeWindow;
                }
                else
                {
                    _selectedWindow = _availableWindows.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshWindowList error: {ex.Message}");
            }
        }

        private void LoadPresetButtons()
        {
            if (DynamicPresetsPanel == null) return;
            DynamicPresetsPanel.Children.Clear();

            Style? baseStyle = null;
            Style? activeStyle = null;
            var resources = App.Current?.Resources;
            if (resources != null)
            {
                if (resources.TryGetValue("PresetButtonBaseStyle", out var baseStyleObj) && baseStyleObj is Style basePresetStyle)
                {
                    baseStyle = basePresetStyle;
                }
                if (resources.TryGetValue("ActivePresetButtonStyle", out var activeStyleObj) && activeStyleObj is Style activePresetStyle)
                {
                    activeStyle = activePresetStyle;
                }
            }

            _presetPresenter.RenderPresets(DynamicPresetsPanel, App.Current?.Resources, PresetButton_Click);
            
            if (!_presetManager.Presets.Any())
            {
                PresetHint.Text = "No presets defined. Add in Settings.";
                _stateManager.ResetPresetIndex();
            }
            else
            {
                PresetHint.Text = "Customize in Settings";
                var idx = _presetPresenter.FocusPreset(DynamicPresetsPanel, 0);
                _stateManager.SetPresetIndex(idx);
            }
        }

        private void FocusPreset(int index)
        {
            if (DynamicPresetsPanel == null) return;
            var buttons = DynamicPresetsPanel.Children.OfType<Button>().ToList();
            if (!buttons.Any()) return;
            if (index < 0) index = 0;
            if (index >= buttons.Count) index = buttons.Count - 1;
            _stateManager.SetPresetIndex(index);
            var target = buttons[index];
            target.Focus(FocusState.Programmatic);
        }

        private async Task ResizeSelectedWindow(int width, int height, string sizeTag)
        {
            try
            {
                var targetWindow = _selectedWindow ?? _availableWindows.FirstOrDefault();
                if (targetWindow == null)
                {
                    _statusManager?.SetStatus("No window", TimeSpan.Zero);
                    return;
                }
                if (_windowResizer == null)
                {
                    _statusManager?.SetStatus("No resizer", TimeSpan.Zero);
                    return;
                }
                _statusManager?.SetStatus($"Resizing {sizeTag}...", TimeSpan.FromSeconds(5));
                var result = _windowResizer.ResizeWindow(targetWindow, width, height);
                if (result.Success)
                {
                    _statusManager?.SetStatus($"✅ {sizeTag}", TimeSpan.FromSeconds(2));
                    // If user enabled center-on-resize, center the external window on its monitor
                    if (_stateManager.CenterOnResize)
                    {
                        try
                        {
                            WindowPositionHelper.CenterExternalWindowOnMonitor(targetWindow);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"MainWindow: Error centering window after resize: {ex.Message}");
                        }
                    }

                    _windowResizer.ActivateWindow(targetWindow);
                        SetActivePreset(sizeTag);
                    await Task.Delay(600);
                    HideWindow();
                }
                else
                {
                    _statusManager?.SetStatus($"❌ {result.ErrorMessage}", TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                _statusManager?.SetStatus("Resize failed", TimeSpan.FromSeconds(3));
                Debug.WriteLine($"ResizeSelectedWindow error: {ex.Message}");
            }
        }

        private void SetActivePreset(string sizeTag)
        {
            _stateManager.SetActivePreset(sizeTag);

            Style? activeStyle = null;
            Style? baseStyle = null;
            var resources = App.Current?.Resources;
            if (resources != null)
            {
                if (resources.TryGetValue("ActivePresetButtonStyle", out var activeObj) && activeObj is Style activePresetStyle)
                {
                    activeStyle = activePresetStyle;
                }
                if (resources.TryGetValue("PresetButtonBaseStyle", out var baseObj) && baseObj is Style basePresetStyle)
                {
                    baseStyle = basePresetStyle;
                }
            }

            if (DynamicPresetsPanel == null || activeStyle == null || baseStyle == null) return;
            foreach (var child in DynamicPresetsPanel.Children.OfType<Button>())
            {
                if (child.Tag is string tag && tag == sizeTag) child.Style = activeStyle; else child.Style = baseStyle;
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string sizeTag) return;
            var parts = sizeTag.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
            {
                _statusManager?.SetStatus("Invalid size", TimeSpan.FromSeconds(3));
                return;
            }
            _ = ResizeSelectedWindow(width, height, sizeTag);
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_stateManager.IsVisible) return;
            var buttons = DynamicPresetsPanel?.Children.OfType<Button>().ToList();
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Escape:
                    HideWindow();
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Right:
                case Windows.System.VirtualKey.Down:
                    if (buttons != null && buttons.Count > 0)
                    {
                        FocusPreset(_stateManager.PresetIndex + 1);
                        e.Handled = true;
                    }
                    break;
                case Windows.System.VirtualKey.Left:
                case Windows.System.VirtualKey.Up:
                    if (buttons != null && buttons.Count > 0)
                    {
                        FocusPreset(_stateManager.PresetIndex - 1);
                        e.Handled = true;
                    }
                    break;
                case Windows.System.VirtualKey.Enter:
                    if (buttons != null && _stateManager.PresetIndex >= 0 && _stateManager.PresetIndex < buttons.Count)
                    {
                        PresetButton_Click(buttons[_stateManager.PresetIndex], new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                if (_stateManager.IsVisible && !_stateManager.IsAlwaysOnTop)
                {
                    // Delay briefly to avoid hiding when clicking inside quickly
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(150);
                        DispatcherQueue.TryEnqueue(HideWindow);
                    });
                }
                return;
            }
            EnsureHotKeyRegistration();
        }

        private void AlwaysOnTopButton_Click(object sender, RoutedEventArgs e)
        {
            var newState = !_stateManager.IsAlwaysOnTop;
            _stateManager.SetAlwaysOnTop(newState);
            ApplyAlwaysOnTop();
                    _statusManager?.SetStatus(_stateManager.IsAlwaysOnTop ? "Pinned" : "Normal", TimeSpan.FromSeconds(2));
        }

        // Toggle handler for CenterOnResize preference
        private void CenterOnResizeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CenterOnResizeToggle != null)
                {
                    var newVal = CenterOnResizeToggle.IsOn;
                    _stateManager.SetCenterOnResize(newVal);
                    ResizeMe.Services.UserPreferences.CenterOnResize = newVal;
                            _statusManager?.SetStatus(_stateManager.CenterOnResize ? "Center on resize: On" : "Center on resize: Off", TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error writing CenterOnResize preference: {ex.Message}");
            }
        }

        private void ApplyAlwaysOnTop()
        {
            if (_windowHandle == IntPtr.Zero) return;
            var insertAfter = _stateManager.IsAlwaysOnTop ? WindowsApi.HWND_TOPMOST : WindowsApi.HWND_NOTOPMOST;
            WindowsApi.SetWindowPos(_windowHandle, insertAfter, 0, 0, 0, 0, WindowsApi.SWP_NOMOVE | WindowsApi.SWP_NOSIZE | WindowsApi.SWP_NOACTIVATE);
        }

        private void Window_Closed(object sender, WindowEventArgs e)
        {
            _trayIcon?.Dispose();
            _hotKeyManager?.Dispose();
            if (_isSubclassRegistered && _windowHandle != IntPtr.Zero && _subclassProc != null)
            {
                WinApiSubClass.RemoveWindowSubclass(_windowHandle, _subclassProc, _subClassId);
                _isSubclassRegistered = false;
            }
        }

        

        private void OpenSettingsWindow()
        {
            try
            {
                var win = new SettingsWindow();
                win.PresetsChanged += OnSettingsPresetsChanged;
                win.Closed += async (_, _) =>
                {
                    win.PresetsChanged -= OnSettingsPresetsChanged;
                    await _presetManager.LoadAsync(true);
                    DispatcherQueue.TryEnqueue(LoadPresetButtons);
                };
                win.Activate();
                _statusManager?.SetStatus("Settings opened", TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _statusManager?.SetStatus("Settings failed", TimeSpan.FromSeconds(3));
                System.Diagnostics.Debug.WriteLine($"Settings open error: {ex.Message}");
            }
        }

        private void CheckFirstRunAndShowSettings()
        {
            try
            {
                if (!Services.UserPreferences.FirstRunCompleted)
                {
                    var win = new SettingsWindow();
                    win.PresetsChanged += OnSettingsPresetsChanged;
                    win.Closed += async (_, _) =>
                    {
                        win.PresetsChanged -= OnSettingsPresetsChanged;
                        Services.UserPreferences.FirstRunCompleted = true; // Mark completion
                        await _presetManager.LoadAsync(true);
                        DispatcherQueue.TryEnqueue(LoadPresetButtons);
                    };
                    win.Activate();
                    _statusManager?.SetStatus("First-run settings", TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"First-run settings error: {ex.Message}");
            }
        }

        private void PerformExit()
        {
            try
            {
                _trayIcon?.Dispose();
                _hotKeyManager?.Dispose();
                Close(); // Close the window to exit
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exit error: {ex.Message}");
            }
        }

        private void SettingsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // Reuse the single entry point to open settings so behavior is consistent
            OpenSettingsWindow();
        }

        private async void OnSettingsPresetsChanged(object? sender, EventArgs e)
        {
            await _presetManager.LoadAsync(true);
            DispatcherQueue.TryEnqueue(LoadPresetButtons);
        }

        // Animation helpers are implemented in `WindowAnimations` helper.
    }

    /// <summary>
    /// Simple state manager for the main window to centralize visibility,
    /// always-on-top, active preset tag, preset index and center-on-resize state.
    /// </summary>
    internal class WindowStateManager
    {
        public bool IsVisible { get; private set; }
        public bool IsAlwaysOnTop { get; private set; }
        public int PresetIndex { get; private set; } = -1;
        public string? ActivePresetTag { get; private set; }
        public bool CenterOnResize { get; private set; }

        public void Show() => IsVisible = true;
        public void Hide() { IsVisible = false; PresetIndex = -1; }
        public void Toggle() => IsVisible = !IsVisible;
        public void SetAlwaysOnTop(bool value) => IsAlwaysOnTop = value;
        public void SetCenterOnResize(bool value) => CenterOnResize = value;
        public void SetActivePreset(string? tag) => ActivePresetTag = tag;
        public string? GetActivePresetTag() => ActivePresetTag;
        public void SetPresetIndex(int index) => PresetIndex = index;
        public void ResetPresetIndex() => PresetIndex = -1;
    }

    internal static class WinApiSubClass
    {
        public delegate IntPtr SubClassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("comctl32.dll")] public static extern bool RemoveWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass);
    }

    

    internal static class AnimationExtensions
    {
        public static Microsoft.UI.Composition.CompositionScopedBatch CreateBatch(this Microsoft.UI.Composition.Compositor compositor) => compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);
        public static Microsoft.UI.Composition.CompositionAnimation Start(this Microsoft.UI.Composition.CompositionAnimation animation, UIElement element, string property)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
            visual.StartAnimation(property, animation);
            return animation;
        }
        public static Microsoft.UI.Composition.ScalarKeyFrameAnimation CreateDoubleAnimation(this UIElement element, double from, double to, int durationMs)
        {
            var compositor = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element).Compositor;
            var anim = compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(0f, (float)from);
            anim.InsertKeyFrame(1f, (float)to);
            anim.Duration = TimeSpan.FromMilliseconds(durationMs);
            return anim;
        }
    }
}

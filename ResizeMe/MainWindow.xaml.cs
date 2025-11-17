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
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public event RoutedEventHandler? Loaded;
        private HotKeyManager? _hotKeyManager;
        private WindowManager? _windowManager;
        private WindowResizer? _windowResizer;
        private readonly PresetManager _presetManager = new();
        private IntPtr _windowHandle = IntPtr.Zero;
        private AppWindow? _appWindow;
        private bool _isSubclassRegistered;
        private bool _isVisible;
        private bool _isAlwaysOnTop;
        private WindowInfo? _selectedWindow;
        private List<WindowInfo> _availableWindows = new();
        private string? _activePresetTag;
        private int _presetIndex = -1;
        private bool _centerOnResize;
        private TrayIconManager? _trayIcon;

        // Window message constants for system commands & tray interaction
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_CLOSE = 0xF060;
        private const int WM_COMMAND = 0x0111;
        private const int WM_RBUTTONUP = 0x0205; // Right mouse button up

        private WinApiSubClass.SubClassProc? _subclassProc;
        private readonly IntPtr _subClassId = new(1001);
        private DateTime _lastToggle = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            _windowManager = new WindowManager();
            _windowResizer = new WindowResizer();
            AttachWindowLoadedHandler();
            Loaded += async (_, _) =>
            {
                await _presetManager.LoadAsync();
                LoadPresetButtons();
                DispatcherQueue.TryEnqueue(() => CheckFirstRunAndShowSettings());
            };
            SetupWindowAppearance();
            AttachKeyDownHandler();
            Activated += OnWindowActivated;
            Closed += Window_Closed;

            // Load persisted "center on resize" preference
            try
            {
                _centerOnResize = ResizeMe.Services.UserPreferences.CenterOnResize;
                if (CenterOnResizeToggle != null)
                {
                    CenterOnResizeToggle.IsOn = _centerOnResize;
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
                    // Modern card-based layout with minimal width and increased vertical space for more items
                    _appWindow?.Resize(new SizeInt32(320, 540));
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                }
                _isVisible = false;
                // First minimize tray notification
                try
                {
                    if (!Services.UserPreferences.FirstMinimizeNotificationShown)
                    {
                        EnsureWindowHandle();
                        string hotkey = "Win+Shift+F12"; // Will be dynamic in later steps
                        var message = $"ResizeMe is running in the system tray. Right-click the tray icon for Settings or Exit. Press {hotkey} to open the quick resize window.";
                        WindowsApi.MessageBoxW(IntPtr.Zero, message, "ResizeMe Running", WindowsApi.MB_OK | WindowsApi.MB_TOPMOST);
                        // Some hosts may reactivate the main window when the message box closes.
                        // Ensure we remain hidden so first-run users stay in the tray immediately.
                        try
                        {
                            if (_windowHandle != IntPtr.Zero)
                            {
                                WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                                _isVisible = false;
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
                StatusText.Text = "Ready";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetupWindowAppearance error: {ex.Message}");
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
            StatusText.Text = success ? "Ready" : "Hotkey failed";
            if (!_isSubclassRegistered)
            {
                _subclassProc = WndProcSubClass;
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

            if (_isVisible) HideWindow(); else ShowWindow();
        }

        private void ShowWindow()
        {
            try
            {
                EnsureWindowHandle();
                RefreshWindowList();
                var anchorWindow = _windowManager?.GetActiveResizableWindow() ?? _availableWindows.FirstOrDefault();
                if (anchorWindow != null) WindowPositionHelper.CenterOnWindow(this, anchorWindow); else WindowPositionHelper.CenterOnScreen(this);
                if (_windowHandle != IntPtr.Zero)
                {
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_RESTORE);
                    WindowsApi.SetForegroundWindow(_windowHandle);
                    ApplyAlwaysOnTop();
                }
                Activate();
                _isVisible = true;
                StatusText.Text = "Menu shown";
                LoadPresetButtons();
                AnimateShow();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Show error";
                Debug.WriteLine($"ShowWindow error: {ex.Message}");
            }
        }

        private void HideWindow()
        {
            if (!_isVisible)
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
                AnimateHide();
                EnsureWindowHandle();
                if (_windowHandle != IntPtr.Zero) WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                _isVisible = false;
                _presetIndex = -1;
                StatusText.Text = "Hidden";
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

            foreach (var preset in _presetManager.Presets)
            {
                // No icon glyphs in minimal floating UI — we only display name + dimensions

                // Match the compact settings list item template: small icon + name + dimensions
                var itemBorder = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 1, 0, 1)
                };

                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var itemIcon = new FontIcon
                {
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    Glyph = "\xE7C4",
                    FontSize = 14,
                    Opacity = 0.9,
                    // Use settings view accent color when available
                    Foreground = resources?.TryGetValue("AccentTextFillColorSecondaryBrush", out var accentBrush) == true ? accentBrush as Microsoft.UI.Xaml.Media.Brush : null,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(itemIcon, 0);

                var itemTextPanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };

                // Reuse app styles if available (PresetHeaderTextStyle and PresetSubTextStyle)
                Style headerStyle = null;
                Style subTextStyle = null;
                // reuse 'resources' defined above for styles and brushes
                if (resources != null)
                {
                    if (resources.TryGetValue("PresetHeaderTextStyle", out var hs) && hs is Style) headerStyle = (Style)hs;
                    if (resources.TryGetValue("PresetSubTextStyle", out var ss) && ss is Style) subTextStyle = (Style)ss;
                }

                var nameText = new TextBlock { Text = preset.Name, FontWeight = FontWeights.SemiBold };
                if (headerStyle != null) nameText.Style = headerStyle; else nameText.FontSize = 13;

                var sizeText = new TextBlock { Text = $"{preset.Width} x {preset.Height}", Opacity = 0.8 };
                if (subTextStyle != null) sizeText.Style = subTextStyle; else sizeText.FontSize = 11;

                itemTextPanel.Children.Add(nameText);
                itemTextPanel.Children.Add(sizeText);
                Grid.SetColumn(itemTextPanel, 1);

                itemGrid.Children.Add(itemIcon);
                itemGrid.Children.Add(itemTextPanel);
                // Match settings item background if available
                if (resources?.TryGetValue("ControlFillColorDefaultBrush", out var bg) == true && bg is Microsoft.UI.Xaml.Media.Brush borderBg) itemBorder.Background = borderBg;
                itemBorder.Child = itemGrid;

                var btn = new Button
                {
                    Content = itemBorder,
                    Tag = $"{preset.Width}x{preset.Height}",
                    Margin = new Thickness(0,0,0,0)
                };
                if (baseStyle != null)
                {
                    btn.Style = baseStyle;
                }
                btn.Height = double.NaN;
                btn.MinHeight = 44;
                // remove extra button padding — inner border already provides the desired spacing
                btn.Padding = new Thickness(0);
                btn.HorizontalContentAlignment = HorizontalAlignment.Left;
                btn.VerticalContentAlignment = VerticalAlignment.Center;
                btn.Click += PresetButton_Click;
                DynamicPresetsPanel.Children.Add(btn);
            }
            if (!_presetManager.Presets.Any())
            {
                PresetHint.Text = "No presets defined. Add in Settings.";
                _presetIndex = -1;
            }
            else
            {
                PresetHint.Text = "Customize in Settings";
                FocusPreset(0);
            }
        }

        private void FocusPreset(int index)
        {
            if (DynamicPresetsPanel == null) return;
            var buttons = DynamicPresetsPanel.Children.OfType<Button>().ToList();
            if (!buttons.Any()) return;
            if (index < 0) index = 0;
            if (index >= buttons.Count) index = buttons.Count - 1;
            _presetIndex = index;
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
                    StatusText.Text = "No window";
                    return;
                }
                if (_windowResizer == null)
                {
                    StatusText.Text = "No resizer";
                    return;
                }
                StatusText.Text = $"Resizing {sizeTag}...";
                var result = _windowResizer.ResizeWindow(targetWindow, width, height);
                if (result.Success)
                {
                    StatusText.Text = $"✅ {sizeTag}";
                    // If user enabled center-on-resize, center the external window on its monitor
                    if (_centerOnResize)
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
                    StatusText.Text = $"❌ {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Resize failed";
                Debug.WriteLine($"ResizeSelectedWindow error: {ex.Message}");
            }
        }

        private void SetActivePreset(string sizeTag)
        {
            _activePresetTag = sizeTag;

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
                StatusText.Text = "Invalid size";
                return;
            }
            _ = ResizeSelectedWindow(width, height, sizeTag);
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!_isVisible) return;
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
                        FocusPreset(_presetIndex + 1);
                        e.Handled = true;
                    }
                    break;
                case Windows.System.VirtualKey.Left:
                case Windows.System.VirtualKey.Up:
                    if (buttons != null && buttons.Count > 0)
                    {
                        FocusPreset(_presetIndex - 1);
                        e.Handled = true;
                    }
                    break;
                case Windows.System.VirtualKey.Enter:
                    if (buttons != null && _presetIndex >= 0 && _presetIndex < buttons.Count)
                    {
                        PresetButton_Click(buttons[_presetIndex], new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                if (_isVisible && !_isAlwaysOnTop)
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
            _isAlwaysOnTop = !_isAlwaysOnTop;
            ApplyAlwaysOnTop();
            StatusText.Text = _isAlwaysOnTop ? "Pinned" : "Normal";
        }

        // Toggle handler for CenterOnResize preference
        private void CenterOnResizeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CenterOnResizeToggle != null)
                {
                    _centerOnResize = CenterOnResizeToggle.IsOn;
                    ResizeMe.Services.UserPreferences.CenterOnResize = _centerOnResize;
                    StatusText.Text = _centerOnResize ? "Center on resize: On" : "Center on resize: Off";
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
            var insertAfter = _isAlwaysOnTop ? WindowsApi.HWND_TOPMOST : WindowsApi.HWND_NOTOPMOST;
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

        private IntPtr WndProcSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            // Intercept system close (titlebar 'X') and hide to tray instead of closing
            if (uMsg == WM_SYSCOMMAND && wParam.ToInt32() == SC_CLOSE)
            {
                HideWindow();
                return IntPtr.Zero;
            }

            // Tray icon callback handling: respond to right-click menu and left-click toggle
            if (_trayIcon != null && uMsg == (uint)_trayIcon.TrayCallbackMessage)
            {
                int lParamVal = lParam.ToInt32();
                // Right button up shows context menu
                if (lParamVal == WM_RBUTTONUP)
                {
                    _trayIcon.ShowContextMenu();
                    return IntPtr.Zero;
                }
                // WM_LBUTTONUP (0x0202) toggles show/hide
                if (lParamVal == 0x0202)
                {
                    ToggleVisibility();
                    return IntPtr.Zero;
                }
            }
            // Preserve hotkey processing
            if (_hotKeyManager?.ProcessMessage(uMsg, wParam, lParam) == true) return IntPtr.Zero;
            return WinApiSubClass.DefSubclassProc(hWnd, uMsg, wParam, lParam);
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
                StatusText.Text = "Settings opened";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Settings failed";
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
                    StatusText.Text = "First-run settings";
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

        private void AnimateShow()
        {
            if (RootGrid == null) return;
            RootGrid.Opacity = 0;
            var animation = RootGrid.CreateDoubleAnimation(0, 1, 180);
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(RootGrid);
            var compositor = visual.Compositor;
            var batch = compositor.CreateBatch();
            visual.StartAnimation("Opacity", animation);
            batch.Completed += (_, _) => RootGrid.DispatcherQueue.TryEnqueue(() => RootGrid.Opacity = 1);
            batch.End();
        }

        private void AnimateHide()
        {
            if (RootGrid == null) return;
            var animation = RootGrid.CreateDoubleAnimation(RootGrid.Opacity, 0, 120);
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(RootGrid);
            var compositor = visual.Compositor;
            var batch = compositor.CreateBatch();
            visual.StartAnimation("Opacity", animation);
            batch.Completed += (_, _) => RootGrid.DispatcherQueue.TryEnqueue(() => RootGrid.Opacity = 0);
            batch.End();
        }
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

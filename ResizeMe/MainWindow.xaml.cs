using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

        private WinApiSubClass.SubClassProc? _subclassProc;
        private readonly IntPtr _subClassId = new(1001);
        private DateTime _lastToggle = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            _windowManager = new WindowManager();
            _windowResizer = new WindowResizer();
            AttachWindowLoadedHandler();
            Loaded += async (_, _) => await _presetManager.LoadAsync();
            SetupWindowAppearance();
            AttachKeyDownHandler();
            Activated += OnWindowActivated;
            Closed += Window_Closed;
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
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(TitleBar);
                EnsureWindowHandle();
                if (_windowHandle != IntPtr.Zero)
                {
                    var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
                    _appWindow = AppWindow.GetFromWindowId(windowId);
                    _appWindow?.Resize(new SizeInt32(300, 340));
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                }
                _isVisible = false;
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
                if (_windowHandle != IntPtr.Zero)
                {
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_RESTORE);
                    WindowsApi.SetForegroundWindow(_windowHandle);
                    ApplyAlwaysOnTop();
                }
                Activate();
                if (anchorWindow != null) WindowPositionHelper.CenterOnWindow(this, anchorWindow); else WindowPositionHelper.CenterOnScreen(this);
                _isVisible = true;
                StatusText.Text = "Menu shown";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Show error";
                Debug.WriteLine($"ShowWindow error: {ex.Message}");
            }
        }

        private void HideWindow()
        {
            try
            {
                EnsureWindowHandle();
                if (_windowHandle != IntPtr.Zero) WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                _isVisible = false;
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
                _selectedWindow = _availableWindows.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RefreshWindowList error: {ex.Message}");
            }
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
            var activeStyle = (Style?)App.Current.Resources["ActivePresetButtonStyle"];
            var baseStyle = (Style?)App.Current.Resources["PresetButtonBaseStyle"];
            if (PresetsGrid == null || activeStyle == null || baseStyle == null) return;
            foreach (var child in PresetsGrid.Children.OfType<Button>())
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
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Escape:
                    HideWindow();
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

        private void ApplyAlwaysOnTop()
        {
            if (_windowHandle == IntPtr.Zero) return;
            var insertAfter = _isAlwaysOnTop ? WindowsApi.HWND_TOPMOST : WindowsApi.HWND_NOTOPMOST;
            WindowsApi.SetWindowPos(_windowHandle, insertAfter, 0, 0, 0, 0, WindowsApi.SWP_NOMOVE | WindowsApi.SWP_NOSIZE | WindowsApi.SWP_NOACTIVATE);
        }

        private void Window_Closed(object sender, WindowEventArgs e)
        {
            _hotKeyManager?.Dispose();
            if (_isSubclassRegistered && _windowHandle != IntPtr.Zero && _subclassProc != null)
            {
                WinApiSubClass.RemoveWindowSubclass(_windowHandle, _subclassProc, _subClassId);
                _isSubclassRegistered = false;
            }
        }

        private IntPtr WndProcSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (_hotKeyManager?.ProcessMessage(uMsg, wParam, lParam) == true) return IntPtr.Zero;
            return WinApiSubClass.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void SettingsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var win = new SettingsWindow();
                win.Activate();
                StatusText.Text = "Settings opened";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Settings failed";
                System.Diagnostics.Debug.WriteLine($"Settings open error: {ex.Message}");
            }
        }
    }

    internal static class WinApiSubClass
    {
        public delegate IntPtr SubClassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("comctl32.dll")] public static extern bool RemoveWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass);
    }
}

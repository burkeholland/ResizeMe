using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    /// Floating context menu window providing preset resize buttons.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private HotKeyManager? _hotKeyManager;
        private WindowManager? _windowManager;
        private WindowResizer? _windowResizer;
        private IntPtr _windowHandle = IntPtr.Zero;
        private AppWindow? _appWindow;
        private bool _isSubclassRegistered;
        private bool _isVisible;
        private bool _isAlwaysOnTop = false;
        private WindowInfo? _selectedWindow;
        private List<WindowInfo> _availableWindows = new();
        private string? _activePresetTag; // Tracks current active preset (e.g., "1280x720")

        private WinApiSubClass.SubClassProc? _subclassProc; // Keep delegate alive
        private IntPtr _subClassId = new IntPtr(1001);

        public MainWindow()
        {
            InitializeComponent();
            _windowManager = new WindowManager();
            _windowResizer = new WindowResizer();
            SetupWindowAppearance();
            Activated += OnWindowActivated;
            Closed += Window_Closed;
            Debug.WriteLine("MainWindow: Initialized");
        }

        /// <summary>
        /// Configures window appearance (size, hidden by default).
        /// </summary>
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
                    _appWindow?.Resize(new SizeInt32(280, 320));
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                }
                _isVisible = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Appearance error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures HWND retrieval.
        /// </summary>
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

        /// <summary>
        /// Registers hotkey & subclass for message processing.
        /// </summary>
        private void EnsureHotKeyRegistration()
        {
            EnsureWindowHandle();
            if (_windowHandle == IntPtr.Zero || _hotKeyManager != null) return;
            _hotKeyManager = new HotKeyManager(_windowHandle);
            _hotKeyManager.HotKeyPressed += OnHotKeyPressed;
            bool success = _hotKeyManager.RegisterHotKey();
            StatusText.Text = success ? "Ready" : "Hotkey failed";
            Debug.WriteLine(success ? "Hotkey registered" : "Hotkey registration failed");
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

        /// <summary>
        /// Hotkey event: toggle visibility.
        /// </summary>
        private void OnHotKeyPressed(object? sender, EventArgs e)
        {
            if (_isVisible) HideWindow(); else ShowWindow();
        }

        /// <summary>
        /// Shows the menu and centers it relative to active window or screen.
        /// </summary>
        private void ShowWindow()
        {
            try
            {
                EnsureWindowHandle();
                RefreshWindowList();
                WindowInfo? anchorWindow = _windowManager?.GetActiveResizableWindow();
                if (anchorWindow != null)
                {
                    var match = _availableWindows.FirstOrDefault(w => w.Handle == anchorWindow.Handle);
                    _selectedWindow = match ?? anchorWindow;
                }
                else
                {
                    _selectedWindow = _availableWindows.FirstOrDefault();
                }
                if (anchorWindow == null)
                {
                    anchorWindow = _selectedWindow;
                }
                if (_windowHandle != IntPtr.Zero)
                {
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_RESTORE);
                    WindowsApi.BringWindowToTop(_windowHandle);
                    WindowsApi.SetForegroundWindow(_windowHandle);
                    ApplyAlwaysOnTop();
                }
                Activate();
                if (anchorWindow != null) WindowPositionHelper.CenterOnWindow(this, anchorWindow); else WindowPositionHelper.CenterOnScreen(this);
                _isVisible = true;
                StatusText.Text = $"Presets ready";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Show error {ex.Message}");
                StatusText.Text = "Show error";
            }
        }

        /// <summary>
        /// Hides the floating window.
        /// </summary>
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
                Debug.WriteLine($"MainWindow: Hide error {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh available windows (simplified for this PR scope).
        /// </summary>
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
                Debug.WriteLine($"MainWindow: Refresh error {ex.Message}");
            }
        }

        /// <summary>
        /// Resize handler invoked by preset buttons.
        /// </summary>
        private async void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string sizeTag) return;
            var parts = sizeTag.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
            {
                StatusText.Text = "Invalid size";
                return;
            }
            await ResizeSelectedWindow(width, height, sizeTag);
        }

        /// <summary>
        /// Core resize operation and post-success UI update.
        /// </summary>
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
                    await Task.Delay(900);
                    HideWindow();
                }
                else
                {
                    StatusText.Text = $"❌ {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Resize error {ex.Message}");
                StatusText.Text = "Resize failed";
            }
        }

        /// <summary>
        /// Highlights the active preset button using style swap.
        /// </summary>
        private void SetActivePreset(string sizeTag)
        {
            _activePresetTag = sizeTag;
            var activeStyle = (Style?)App.Current.Resources["ActivePresetButtonStyle"];
            var baseStyle = (Style?)App.Current.Resources["PresetButtonBaseStyle"];
            if (PresetsGrid == null || activeStyle == null || baseStyle == null) return;
            foreach (var child in PresetsGrid.Children.OfType<Button>())
            {
                if (child.Tag is string tag && tag == sizeTag)
                {
                    child.Style = activeStyle;
                }
                else
                {
                    child.Style = baseStyle;
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Settings";
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

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                if (_isVisible && !_isAlwaysOnTop) HideWindow();
                return;
            }
            EnsureHotKeyRegistration();
        }

        private void ApplyAlwaysOnTop()
        {
            if (_windowHandle == IntPtr.Zero) return;
            var insertAfter = _isAlwaysOnTop ? WindowsApi.HWND_TOPMOST : WindowsApi.HWND_NOTOPMOST;
            WindowsApi.SetWindowPos(_windowHandle, insertAfter, 0, 0, 0, 0, WindowsApi.SWP_NOMOVE | WindowsApi.SWP_NOSIZE | WindowsApi.SWP_NOACTIVATE);
        }

        /// <summary>
        /// Subclass procedure to process messages (hotkey delegation).
        /// </summary>
        private IntPtr WndProcSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (_hotKeyManager?.ProcessMessage(uMsg, wParam, lParam) == true) return IntPtr.Zero;
            return WinApiSubClass.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }
    }

    /// <summary>
    /// Win32 subclass helper.
    /// </summary>
    internal static class WinApiSubClass
    {
        public delegate IntPtr SubClassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] public static extern bool SetWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);
        [DllImport("comctl32.dll")] public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("comctl32.dll")] public static extern bool RemoveWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass);
    }
}

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
    /// Floating context menu window for ResizeMe application
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

        private WinApiSubClass.SubClassProc? _subclassProc; // Keep delegate alive
        private IntPtr _subClassId = new IntPtr(1001);
        public MainWindow()
        {
            this.InitializeComponent();
            
            // Initialize services
            _windowManager = new WindowManager();
            _windowResizer = new WindowResizer();
            
            // Configure window appearance
            SetupWindowAppearance();
            
            this.Activated += OnWindowActivated;
            this.Closed += Window_Closed;
            
            Debug.WriteLine("MainWindow: Constructor completed");
        }

        /// <summary>
        /// Sets up the window appearance and behavior
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
                    _appWindow?.Resize(new SizeInt32(280, 400));

                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                }

                _isVisible = false;

                Debug.WriteLine("MainWindow: Window appearance configured");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error setting up appearance: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the global hotkey press event
        /// </summary>
        private void OnHotKeyPressed(object? sender, EventArgs e)
        {
            Debug.WriteLine("MainWindow: Hotkey Ctrl+Alt+R pressed - toggling menu");
            
            if (_isVisible)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        /// <summary>
        /// Shows the floating menu centered over the active window
        /// </summary>
        private void ShowWindow()
        {
            try
            {
                Debug.WriteLine("MainWindow: Showing floating menu");

                EnsureWindowHandle();

                RefreshWindowList();

                WindowInfo? anchorWindow = null;

                if (_windowManager != null)
                {
                    anchorWindow = _windowManager.GetActiveResizableWindow();

                    if (anchorWindow != null)
                    {
                        var listMatch = _availableWindows.FirstOrDefault(w => w.Handle == anchorWindow.Handle);
                        if (listMatch != null)
                        {
                            anchorWindow = listMatch;
                            _selectedWindow = listMatch;

                            if (_availableWindows.Count > 1)
                            {
                                WindowList.SelectedItem = listMatch;
                            }
                            else
                            {
                                CurrentWindowText.Text = listMatch.DisplayText;
                                CurrentWindowSize.Text = listMatch.Bounds.ToString();
                            }
                        }
                        else
                        {
                            anchorWindow = _windowManager.RefreshWindowInfo(anchorWindow) ?? anchorWindow;
                            _selectedWindow = anchorWindow;
                        }
                    }
                }

                if (anchorWindow == null)
                {
                    anchorWindow = _selectedWindow ?? _availableWindows.FirstOrDefault();
                }

                if (anchorWindow != null && _windowManager != null)
                {
                    anchorWindow = _windowManager.RefreshWindowInfo(anchorWindow) ?? anchorWindow;
                }

                if (_windowHandle != IntPtr.Zero)
                {
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_RESTORE);
                    WindowsApi.BringWindowToTop(_windowHandle);
                    WindowsApi.SetForegroundWindow(_windowHandle);
                    ApplyAlwaysOnTop();
                }

                Activate();

                if (anchorWindow != null)
                {
                    WindowPositionHelper.CenterOnWindow(this, anchorWindow);
                }
                else
                {
                    WindowPositionHelper.CenterOnScreen(this);
                }

                _isVisible = true;
                StatusText.Text = $"Found {_availableWindows.Count} windows";

                Debug.WriteLine($"MainWindow: Menu shown with {_availableWindows.Count} windows");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error showing window: {ex.Message}");
                StatusText.Text = "Error showing";
            }
        }

        /// <summary>
        /// Hides the floating menu
        /// </summary>
        private void HideWindow()
        {
            try
            {
                Debug.WriteLine("MainWindow: Hiding floating menu");

                EnsureWindowHandle();

                if (_windowHandle != IntPtr.Zero)
                {
                    WindowsApi.ShowWindow(_windowHandle, WindowsApi.SW_HIDE);
                }

                _isVisible = false;
                StatusText.Text = "Hidden";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error hiding window: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the list of available windows
        /// </summary>
        private void RefreshWindowList()
        {
            try
            {
                if (_windowManager != null)
                {
                    _availableWindows = _windowManager.GetResizableWindows().ToList();
                    
                    // Update UI
                    WindowList.ItemsSource = _availableWindows;
                    
                    // Show window selection if multiple windows, hide if just one
                    if (_availableWindows.Count > 1)
                    {
                        WindowSelectionSection.Visibility = Visibility.Visible;
                        CurrentWindowSection.Visibility = Visibility.Collapsed;
                    }
                    else if (_availableWindows.Count == 1)
                    {
                        WindowSelectionSection.Visibility = Visibility.Collapsed;
                        CurrentWindowSection.Visibility = Visibility.Visible;
                        
                        _selectedWindow = _availableWindows[0];
                        CurrentWindowText.Text = _selectedWindow.DisplayText;
                        CurrentWindowSize.Text = _selectedWindow.Bounds.ToString();
                    }
                    else
                    {
                        WindowSelectionSection.Visibility = Visibility.Collapsed;
                        CurrentWindowSection.Visibility = Visibility.Collapsed;
                        _selectedWindow = null;
                    }

                    Debug.WriteLine($"MainWindow: Refreshed window list - found {_availableWindows.Count} windows");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error refreshing window list: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles preset button clicks
        /// </summary>
        private async void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string sizeString)
            {
                try
                {
                    // Parse size string (e.g., "1920x1080")
                    var parts = sizeString.Split('x');
                    if (parts.Length == 2 && 
                        int.TryParse(parts[0], out int width) && 
                        int.TryParse(parts[1], out int height))
                    {
                        await ResizeSelectedWindow(width, height);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MainWindow: Error parsing preset size: {ex.Message}");
                    StatusText.Text = "Invalid size";
                }
            }
        }

        /// <summary>
        /// Resizes the currently selected window
        /// </summary>
        private async System.Threading.Tasks.Task ResizeSelectedWindow(int width, int height)
        {
            try
            {
                var targetWindow = _selectedWindow ?? _availableWindows.FirstOrDefault();
                
                if (targetWindow == null)
                {
                    StatusText.Text = "No window selected";
                    return;
                }

                if (_windowResizer == null)
                {
                    StatusText.Text = "Resizer not available";
                    return;
                }

                Debug.WriteLine($"MainWindow: Resizing {targetWindow.DisplayText} to {width}x{height}");
                StatusText.Text = "Resizing...";

                var result = _windowResizer.ResizeWindow(targetWindow, width, height);
                
                if (result.Success)
                {
                    StatusText.Text = $"✅ Resized to {width}x{height}";
                    Debug.WriteLine($"MainWindow: ✅ {result.DisplayMessage}");
                    
                    // Activate the resized window
                    _windowResizer.ActivateWindow(targetWindow);
                    
                    // Hide the menu after successful resize
                    await System.Threading.Tasks.Task.Delay(1500); // Show success message briefly
                    HideWindow();
                }
                else
                {
                    StatusText.Text = $"❌ {result.ErrorMessage}";
                    Debug.WriteLine($"MainWindow: ❌ {result.DisplayMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error resizing window: {ex.Message}");
                StatusText.Text = "Resize failed";
            }
        }

        // Event Handlers for UI elements

        private void Window_Closed(object sender, WindowEventArgs e)
        {
            Debug.WriteLine("MainWindow: Closing - cleaning up resources");
            _hotKeyManager?.Dispose();
            if (_isSubclassRegistered && _windowHandle != IntPtr.Zero && _subclassProc != null)
            {
                WinApiSubClass.RemoveWindowSubclass(_windowHandle, _subclassProc, _subClassId);
                _isSubclassRegistered = false;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Settings (coming soon)";
            Debug.WriteLine("MainWindow: Settings button clicked");
        }

        private void WindowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WindowList.SelectedItem is WindowInfo selectedWindow)
            {
                _selectedWindow = selectedWindow;
                Debug.WriteLine($"MainWindow: Selected window: {_selectedWindow.DisplayText}");
            }
        }

        private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindowList();
        }

        private void CustomSizeButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Custom size (coming soon)";
            Debug.WriteLine("MainWindow: Custom size button clicked");
        }

        private void MorePresetsButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "More presets (coming soon)";
            Debug.WriteLine("MainWindow: More presets button clicked");
        }

        private void AlwaysOnTopButton_Click(object sender, RoutedEventArgs e)
        {
            _isAlwaysOnTop = !_isAlwaysOnTop;
            ApplyAlwaysOnTop();
            StatusText.Text = _isAlwaysOnTop ? "Always on top" : "Normal mode";
            Debug.WriteLine($"MainWindow: Always on top: {_isAlwaysOnTop}");
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            HideWindow();
        }

        /// <summary>
        /// Window subclass procedure to handle Windows messages
        /// </summary>
        private IntPtr WndProcSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            // Let our hotkey manager process the message
            if (_hotKeyManager?.ProcessMessage(uMsg, wParam, lParam) == true)
            {
                return IntPtr.Zero; // Message handled
            }
            
            // Call default window procedure for unhandled messages
            return WinApiSubClass.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void EnsureWindowHandle()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                return;
            }

            _windowHandle = WindowNative.GetWindowHandle(this);
            if (_windowHandle != IntPtr.Zero && _appWindow == null)
            {
                var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
                _appWindow = AppWindow.GetFromWindowId(windowId);
            }
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                if (_isVisible && !_isAlwaysOnTop)
                {
                    HideWindow();
                }

                return;
            }

            EnsureHotKeyRegistration();

            if (_isVisible && _availableWindows.Any())
            {
                StatusText.Text = $"Found {_availableWindows.Count} windows";
            }
        }

        private void EnsureHotKeyRegistration()
        {
            EnsureWindowHandle();

            if (_windowHandle == IntPtr.Zero || _hotKeyManager != null)
            {
                return;
            }

            _hotKeyManager = new HotKeyManager(_windowHandle);
            _hotKeyManager.HotKeyPressed += OnHotKeyPressed;

            bool success = _hotKeyManager.RegisterHotKey();
            StatusText.Text = success ? "Ready" : "Hotkey failed";
            Debug.WriteLine(success
                ? "MainWindow: Global hotkey Ctrl+Alt+R registered successfully"
                : "MainWindow: Failed to register global hotkey");

            if (!_isSubclassRegistered)
            {
                _subclassProc = WndProcSubClass; // store delegate to prevent GC
                if (WinApiSubClass.SetWindowSubclass(_windowHandle, _subclassProc, _subClassId, IntPtr.Zero))
                {
                    _isSubclassRegistered = true;
                }
                else
                {
                    Debug.WriteLine("MainWindow: Failed to set window subclass for message processing");
                }
            }

            HideWindow();
        }

        private void ApplyAlwaysOnTop()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            var insertAfter = _isAlwaysOnTop ? WindowsApi.HWND_TOPMOST : WindowsApi.HWND_NOTOPMOST;
            WindowsApi.SetWindowPos(
                _windowHandle,
                insertAfter,
                0,
                0,
                0,
                0,
                WindowsApi.SWP_NOMOVE | WindowsApi.SWP_NOSIZE | WindowsApi.SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// Helper class for window subclassing to handle Windows messages in WinUI
    /// </summary>
    internal static class WinApiSubClass
    {
        public delegate IntPtr SubClassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll")]
        public static extern bool SetWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll")]
        public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("comctl32.dll")]
        public static extern bool RemoveWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass);
    }
}

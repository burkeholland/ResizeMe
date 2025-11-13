using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ResizeMe.Services;
using ResizeMe.Models;
using WinRT.Interop;

namespace ResizeMe
{
    public sealed partial class MainWindow : Window
    {
        private HotKeyManager? _hotKeyManager;
        private WindowManager? _windowManager;
        private WindowResizer? _windowResizer;

        public MainWindow()
        {
            this.InitializeComponent();
            
            // Initialize services
            _windowManager = new WindowManager();
            _windowResizer = new WindowResizer();
            
            // Initialize hotkey manager after window is created
            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
            
            Debug.WriteLine("MainWindow: Constructor completed");
        }

        /// <summary>
        /// Called when window is first activated - set up hotkey registration
        /// </summary>
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated && _hotKeyManager == null)
            {
                // Get the window handle (HWND) for this WinUI window
                IntPtr hwnd = WindowNative.GetWindowHandle(this);
                
                if (hwnd != IntPtr.Zero)
                {
                    // Initialize and register hotkey
                    _hotKeyManager = new HotKeyManager(hwnd);
                    _hotKeyManager.HotKeyPressed += HotKeyManager_HotKeyPressed;
                    
                    bool success = _hotKeyManager.RegisterHotKey();
                    if (success)
                    {
                        Debug.WriteLine("MainWindow: Global hotkey Ctrl+Alt+R registered successfully");
                    }
                    else
                    {
                        Debug.WriteLine("MainWindow: Failed to register global hotkey");
                    }
                    
                    // Hook into the window's message processing
                    var subClassId = new IntPtr(1001);
                    WinApiSubClass.SetWindowSubclass(hwnd, WndProcSubClass, subClassId, IntPtr.Zero);
                }
                
                // Only need to do this once
                this.Activated -= MainWindow_Activated;
            }
        }

        /// <summary>
        /// Handles the hotkey press event
        /// </summary>
        private void HotKeyManager_HotKeyPressed(object? sender, EventArgs e)
        {
            Debug.WriteLine("MainWindow: Hotkey Ctrl+Alt+R was pressed!");
            
            try
            {
                // Get list of resizable windows
                var windows = _windowManager?.GetResizableWindows();
                var targetWindow = _windowManager?.GetActiveResizableWindow();
                if (windows != null && windows.Any())
                {
                    Debug.WriteLine($"MainWindow: Found {windows.Count()} resizable windows");
                    
                    foreach (var window in windows.Take(10))
                    {
                        Debug.WriteLine($"  - {window}");
                        Debug.WriteLine($"    Bounds: {window.Bounds}");
                    }
                    
                    var resizeTarget = targetWindow ?? windows.First();
                    if (targetWindow != null)
                    {
                        Debug.WriteLine($"MainWindow: Foreground window prioritized: {resizeTarget}");
                    }
                    else
                    {
                        Debug.WriteLine("MainWindow: No active window available, falling back to first entry");
                    }
                    
                    if (_windowResizer != null)
                    {
                        Debug.WriteLine($"MainWindow: Testing resize on: {resizeTarget}");
                        var result = _windowResizer.ResizeWindow(resizeTarget, 800, 600);
                        
                        if (result.Success)
                        {
                            Debug.WriteLine($"MainWindow: ✅ {result.DisplayMessage}");
                            this.Title = $"ResizeMe - ✅ Resized {resizeTarget.DisplayText} to 800x600";
                            
                            if (_windowResizer.ActivateWindow(resizeTarget))
                            {
                                Debug.WriteLine($"MainWindow: ✅ Activated window {resizeTarget.DisplayText}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"MainWindow: ❌ {result.DisplayMessage}");
                            this.Title = $"ResizeMe - ❌ Failed to resize: {result.ErrorMessage}";
                        }
                        
                        if (result.WindowStateChanged)
                        {
                            Debug.WriteLine("MainWindow: Window state was changed during resize");
                        }
                        
                        if (result.ActualSize != null)
                        {
                            Debug.WriteLine($"MainWindow: Actual resulting size: {result.ActualSize}");
                        }
                    }
                    else
                    {
                        this.Title = "ResizeMe - WindowResizer not initialized";
                        Debug.WriteLine("MainWindow: WindowResizer is null");
                    }
                }
                else
                {
                    this.Title = "ResizeMe - No resizable windows found";
                    Debug.WriteLine("MainWindow: No resizable windows found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error during resize test: {ex.Message}");
                this.Title = $"ResizeMe - Error: {ex.Message}";
            }
            
            // Bring our window back to the front
            this.Activate();
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

        /// <summary>
        /// Cleanup when window is closed
        /// </summary>
        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Debug.WriteLine("MainWindow: Closing - cleaning up hotkey registration");
            _hotKeyManager?.Dispose();
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

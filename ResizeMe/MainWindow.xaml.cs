using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using ResizeMe.Services;
using WinRT.Interop;

namespace ResizeMe
{
    public sealed partial class MainWindow : Window
    {
        private HotKeyManager? _hotKeyManager;
        private WinApiSubClass.SubClassProc? _subclassProc;
        private readonly IntPtr _subClassId = new IntPtr(1001);

        public MainWindow()
        {
            InitializeComponent();

            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Only register once
            if (_hotKeyManager == null)
            {
                InitializeHotKeys();
            }
        }

        private void InitializeHotKeys()
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                if (hwnd == IntPtr.Zero)
                {
                    Debug.WriteLine("Unable to retrieve window handle for hotkey registration.");
                    return;
                }

                _hotKeyManager = new HotKeyManager(hwnd);
                _hotKeyManager.HotKeyPressed += HotKeyManager_HotKeyPressed;
                var success = _hotKeyManager.RegisterHotKey();
                Debug.WriteLine(success
                    ? "Hotkey registered successfully (Ctrl+Alt+R)"
                    : "Failed to register global hotkey");

                // Hook into the window's message processing and keep delegate alive
                _subclassProc = WndProcSubClass;
                WinApiSubClass.SetWindowSubclass(hwnd, _subclassProc, _subClassId, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize hotkey: {ex.Message}");
            }
        }

        private void HotKeyManager_HotKeyPressed(object? sender, EventArgs e)
        {
            Debug.WriteLine("MainWindow: Hotkey Ctrl+Alt+R was pressed!");
            this.Activate();
            this.Title = $"ResizeMe - Hotkey pressed at {DateTime.Now:HH:mm:ss}";
        }

        private IntPtr WndProcSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (_hotKeyManager?.ProcessMessage(uMsg, wParam, lParam) == true)
            {
                return IntPtr.Zero; // Message handled
            }

            return WinApiSubClass.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _hotKeyManager?.Dispose();
            _hotKeyManager = null;

            // Remove subclass to be safe
            if (_subclassProc != null)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                if (hwnd != IntPtr.Zero)
                {
                    WinApiSubClass.RemoveWindowSubclass(hwnd, _subclassProc, _subClassId);
                }
            }
        }

    internal static class WinApiSubClass
    {
        public delegate IntPtr SubClassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll")]
        public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RemoveWindowSubclass(IntPtr hWnd, SubClassProc pfnSubclass, IntPtr uIdSubclass);
    }
    }
}

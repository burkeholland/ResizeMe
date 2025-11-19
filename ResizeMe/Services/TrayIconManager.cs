using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using ResizeMe.Native;

namespace ResizeMe.Services
{
    /// <summary>
    /// Manages system tray icon and context menu for the application.
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private readonly IntPtr _hWnd;
        private bool _added;
        private const int WM_USER_TRAY = WindowsApi.WM_USER_TRAY;

        // Menu item command IDs
        private const int CMD_SHOW = 10001;
        private const int CMD_SETTINGS = 10002;
        private const int CMD_EXIT = 10003;

        // TrackPopupMenu flags
        private const uint TPM_RIGHTBUTTON = WindowsApi.TPM_RIGHTBUTTON;
        private const uint TPM_RETURNCMD = WindowsApi.TPM_RETURNCMD;

        // Win32 P/Invoke helpers for popup menu
        // Use centralized WindowsApi for native helpers
        private const uint IMAGE_ICON = WindowsApi.IMAGE_ICON;
        private const uint LR_LOADFROMFILE = WindowsApi.LR_LOADFROMFILE;
        private const uint LR_DEFAULTSIZE = WindowsApi.LR_DEFAULTSIZE;

        public event EventHandler? ShowRequested;
        public event EventHandler? SettingsRequested;
        public event EventHandler? ExitRequested;

        public TrayIconManager(IntPtr hWnd)
        {
            _hWnd = hWnd;
        }

        public bool Initialize()
        {
            if (_added) return true;
            
            // Try to load custom icon from AppIcon.ico
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    hIcon = WindowsApi.LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TrayIconManager: Failed to load custom icon: {ex.Message}");
            }
            
            // Fallback to default application icon if custom icon fails
            if (hIcon == IntPtr.Zero)
            {
                hIcon = WindowsApi.LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
            }
            
            var data = new WindowsApi.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WindowsApi.NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1,
                uFlags = WindowsApi.NIF_MESSAGE | WindowsApi.NIF_ICON | WindowsApi.NIF_TIP,
                uCallbackMessage = WM_USER_TRAY,
                hIcon = hIcon,
                szTip = "ResizeMe"
            };
            _added = WindowsApi.Shell_NotifyIcon(WindowsApi.NIM_ADD, ref data);
            return _added;
        }

        /// <summary>
        /// Shows the context menu at current cursor position and fires selection events.
        /// </summary>
        public void ShowContextMenu()
        {
            if (_hWnd == IntPtr.Zero) return;
            try
            {
                // Ensure window is foreground so menu dismiss behaves correctly.
                WindowsApi.SetForegroundWindow(_hWnd);
                IntPtr hMenu = WindowsApi.CreatePopupMenu();
                if (hMenu == IntPtr.Zero) return;

                WindowsApi.AppendMenu(hMenu, 0, CMD_SHOW, "Show/Hide");
                WindowsApi.AppendMenu(hMenu, 0, CMD_SETTINGS, "Settings...");
                WindowsApi.AppendMenu(hMenu, 0, CMD_EXIT, "Exit");

                WindowsApi.GetCursorPos(out var pt);
                uint cmd = WindowsApi.TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, _hWnd, IntPtr.Zero);
                WindowsApi.DestroyMenu(hMenu);

                if (cmd == CMD_SHOW) ShowRequested?.Invoke(this, EventArgs.Empty);
                else if (cmd == CMD_SETTINGS) SettingsRequested?.Invoke(this, EventArgs.Empty);
                else if (cmd == CMD_EXIT) ExitRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrayIconManager: Context menu error {ex.Message}");
            }
        }

        /// <summary>Tray callback message identifier for external WndProc processing.</summary>
        public int TrayCallbackMessage => WM_USER_TRAY;

        public void Dispose()
        {
            if (!_added) return;
            var data = new WindowsApi.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WindowsApi.NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1
            };
            WindowsApi.Shell_NotifyIcon(WindowsApi.NIM_DELETE, ref data);
            _added = false;
        }
    }
}

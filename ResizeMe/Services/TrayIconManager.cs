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
        private const int WM_USER_TRAY = 0x0400 + 500;
        private const int NIF_MESSAGE = 0x0001;
        private const int NIF_ICON = 0x0002;
        private const int NIF_TIP = 0x0004;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_MODIFY = 0x00000001;

        // Menu item command IDs
        private const int CMD_SHOW = 10001;
        private const int CMD_SETTINGS = 10002;
        private const int CMD_EXIT = 10003;

        // TrackPopupMenu flags
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;

        // Win32 P/Invoke helpers for popup menu
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTpm);

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInstance, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;

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
                    hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TrayIconManager: Failed to load custom icon: {ex.Message}");
            }
            
            // Fallback to default application icon if custom icon fails
            if (hIcon == IntPtr.Zero)
            {
                hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
            }
            
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_USER_TRAY,
                hIcon = hIcon,
                szTip = "ResizeMe"
            };
            _added = Shell_NotifyIcon(NIM_ADD, ref data);
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
                SetForegroundWindow(_hWnd);
                IntPtr hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero) return;

                AppendMenu(hMenu, 0, CMD_SHOW, "Show/Hide");
                AppendMenu(hMenu, 0, CMD_SETTINGS, "Settings...");
                AppendMenu(hMenu, 0, CMD_EXIT, "Exit");

                GetCursorPos(out var pt);
                uint cmd = TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, _hWnd, IntPtr.Zero);
                DestroyMenu(hMenu);

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
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
    }
}

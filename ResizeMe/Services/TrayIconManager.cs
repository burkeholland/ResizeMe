using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_USER_TRAY,
                hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)), // IDI_APPLICATION
                szTip = "ResizeMe"
            };
            _added = Shell_NotifyIcon(NIM_ADD, ref data);
            return _added;
        }

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

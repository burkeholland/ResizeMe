using System;
using System.Runtime.InteropServices;

namespace ResizeMe.Interop
{
    /// <summary>
    /// PInvoke declarations for Windows hotkey APIs.
    /// </summary>
    internal static class HotKeyInterop
    {
        // Constants for RegisterHotKey
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;
        public const int MOD_NOREPEAT = 0x4000;

        // Virtual key codes
        public const int VK_R = 0x52; // 'R' key

        // Window message
        public const int WM_HOTKEY = 0x0312;

        // Window procedure index for SetWindowLongPtr
        public const int GWLP_WNDPROC = -4;
        public const int GWLP_USERDATA = -21;

        private const string USER32 = "user32.dll";

        /// <summary>
        /// Registers a hotkey with the Windows system.
        /// </summary>
        [DllImport(USER32, SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>
        /// Unregisters a previously registered hotkey.
        /// </summary>
        [DllImport(USER32, SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>
        /// Gets extended error information.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int GetLastError();

        /// <summary>
        /// Sets the window procedure for a window (used for subclassing).
        /// </summary>
        [DllImport(USER32, SetLastError = true)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        /// <summary>
        /// Gets the window procedure pointer.
        /// </summary>
        [DllImport(USER32, SetLastError = true)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        /// <summary>
        /// Calls the original window procedure.
        /// </summary>
        [DllImport(USER32)]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Delegate for window procedure callback.
        /// </summary>
        public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}

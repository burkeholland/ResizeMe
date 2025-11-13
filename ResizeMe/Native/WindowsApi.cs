using System;
using System.Runtime.InteropServices;

namespace ResizeMe.Native
{
    /// <summary>
    /// P/Invoke declarations for Windows API functions needed for global hotkey registration
    /// </summary>
    internal static class WindowsApi
    {
        // Constants for hotkey registration
        public const int WM_HOTKEY = 0x0312;
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        // Virtual key codes
        public const int VK_R = 0x52;
        public const int VK_F12 = 0x7B; // F12 virtual key code

        /// <summary>
        /// Registers a hotkey with the system
        /// </summary>
        /// <param name="hWnd">Handle to window that will receive WM_HOTKEY messages</param>
        /// <param name="id">Application-defined identifier for the hotkey</param>
        /// <param name="fsModifiers">Modifier keys (MOD_ALT, MOD_CONTROL, etc.)</param>
        /// <param name="vk">Virtual key code</param>
        /// <returns>True if successful, false if failed</returns>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>
        /// Unregisters a hotkey that was registered by RegisterHotKey
        /// </summary>
        /// <param name="hWnd">Handle to window that registered the hotkey</param>
        /// <param name="id">Application-defined identifier for the hotkey</param>
        /// <returns>True if successful, false if failed</returns>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}

using System;
using System.Runtime.InteropServices;
using System.Text;

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

        // Additional constants for window enumeration
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const uint WS_VISIBLE = 0x10000000;
        public const uint WS_MINIMIZE = 0x20000000;
        public const uint WS_DISABLED = 0x08000000;
        public const uint WS_EX_TOOLWINDOW = 0x00000080;
        public const uint WS_EX_APPWINDOW = 0x00040000;
        public const int DWMWA_CLOAKED = 14;

        // Delegate for EnumWindows callback
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// Enumerates all top-level windows on the desktop
        /// </summary>
        /// <param name="enumFunc">Application-defined callback function</param>
        /// <param name="lParam">Application-defined value to be passed to the callback</param>
        /// <returns>True if successful, false otherwise</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

        /// <summary>
        /// Retrieves the length of the specified window's title bar text
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>Length of the text in characters</returns>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        /// <summary>
        /// Copies the text of the specified window's title bar into a buffer
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="text">Buffer to receive the text</param>
        /// <param name="count">Maximum number of characters to copy</param>
        /// <returns>Length of the copied string</returns>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        /// <summary>
        /// Retrieves information about the specified window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="nIndex">Zero-based offset to the value to be retrieved</param>
        /// <returns>The requested value</returns>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

        /// <summary>
        /// Determines the visibility state of the specified window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>True if the window is visible, false otherwise</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// Determines whether the specified window is minimized (iconic)
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>True if the window is minimized, false otherwise</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        /// <summary>
        /// Retrieves the name of the class to which the specified window belongs
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="lpClassName">Buffer to receive the class name</param>
        /// <param name="nMaxCount">Length of the buffer</param>
        /// <returns>Number of characters copied</returns>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        /// <summary>
        /// Retrieves the identifier of the thread that created the specified window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="lpdwProcessId">Pointer to variable to receive process identifier</param>
        /// <returns>Thread identifier</returns>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// Retrieves the dimensions of the bounding rectangle of the specified window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="lpRect">Pointer to structure to receive rectangle</param>
        /// <returns>True if successful, false otherwise</returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// Retrieves the DWM window attribute
        /// </summary>
        /// <param name="hwnd">Handle to the window</param>
        /// <param name="dwAttribute">Attribute to retrieve</param>
        /// <param name="pvAttribute">Pointer to buffer to receive attribute</param>
        /// <param name="cbAttribute">Size of buffer</param>
        /// <returns>S_OK if successful</returns>
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        /// <summary>
        /// Structure representing a rectangle
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }
    }
}

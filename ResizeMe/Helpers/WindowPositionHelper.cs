using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRT.Interop;
using ResizeMe.Models;
using ResizeMe.Native;

namespace ResizeMe.Helpers
{
    /// <summary>
    /// Helper class for positioning windows relative to cursor and screen bounds
    /// </summary>
    public static class WindowPositionHelper
    {
        // P/Invoke declarations for cursor and screen information
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        // Constants
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int OFFSET_FROM_CURSOR = 20; // Pixels offset from cursor

        // Structures
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;

            public static MONITORINFO Default
            {
                get
                {
                    var mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(mi);
                    return mi;
                }
            }
        }

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

        /// <summary>
        /// Positions a WinUI window near the cursor while keeping it within screen bounds
        /// </summary>
        /// <param name="window">The window to position</param>
        /// <param name="preferredSide">Preferred side relative to cursor (Right, Left, Below, Above)</param>
        public static void PositionNearCursor(Window window, PreferredPosition preferredSide = PreferredPosition.Right)
        {
            try
            {
                // Get cursor position
                if (!GetCursorPos(out POINT cursorPos))
                {
                    Debug.WriteLine("WindowPositionHelper: Failed to get cursor position");
                    return;
                }

                Debug.WriteLine($"WindowPositionHelper: Cursor position: {cursorPos.X}, {cursorPos.Y}");

                // Get monitor information for the cursor location
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = MONITORINFO.Default;
                
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                {
                    Debug.WriteLine("WindowPositionHelper: Failed to get monitor information");
                    return;
                }

                var workArea = monitorInfo.rcWork;
                Debug.WriteLine($"WindowPositionHelper: Work area: {workArea.Left},{workArea.Top} - {workArea.Right},{workArea.Bottom}");

                // Get window handle
                IntPtr hWnd = WindowNative.GetWindowHandle(window);
                if (hWnd == IntPtr.Zero)
                {
                    Debug.WriteLine("WindowPositionHelper: Failed to get window handle");
                    return;
                }

                // Get window dimensions
                int windowWidth = (int)window.Bounds.Width;
                int windowHeight = (int)window.Bounds.Height;

                // If bounds are not available, use default size
                if (windowWidth == 0) windowWidth = 280;
                if (windowHeight == 0) windowHeight = 400;

                // Calculate initial position based on preference
                int targetX, targetY;
                CalculateInitialPosition(cursorPos, preferredSide, windowWidth, windowHeight, out targetX, out targetY);

                // Adjust position to keep window within screen bounds
                AdjustForScreenBounds(workArea, ref targetX, ref targetY, windowWidth, windowHeight);

                Debug.WriteLine($"WindowPositionHelper: Positioning window at {targetX}, {targetY} (size: {windowWidth}x{windowHeight})");

                // Set the window position
                bool success = SetWindowPos(hWnd, IntPtr.Zero, targetX, targetY, 0, 0, 
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                if (!success)
                {
                    Debug.WriteLine($"WindowPositionHelper: SetWindowPos failed with error {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Debug.WriteLine("WindowPositionHelper: Window positioned successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowPositionHelper: Exception positioning window: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculates the initial position based on cursor location and preference
        /// </summary>
        private static void CalculateInitialPosition(POINT cursorPos, PreferredPosition preference, 
            int windowWidth, int windowHeight, out int x, out int y)
        {
            switch (preference)
            {
                case PreferredPosition.Right:
                    x = cursorPos.X + OFFSET_FROM_CURSOR;
                    y = cursorPos.Y - (windowHeight / 2);
                    break;

                case PreferredPosition.Left:
                    x = cursorPos.X - windowWidth - OFFSET_FROM_CURSOR;
                    y = cursorPos.Y - (windowHeight / 2);
                    break;

                case PreferredPosition.Below:
                    x = cursorPos.X - (windowWidth / 2);
                    y = cursorPos.Y + OFFSET_FROM_CURSOR;
                    break;

                case PreferredPosition.Above:
                    x = cursorPos.X - (windowWidth / 2);
                    y = cursorPos.Y - windowHeight - OFFSET_FROM_CURSOR;
                    break;

                default:
                    x = cursorPos.X + OFFSET_FROM_CURSOR;
                    y = cursorPos.Y - (windowHeight / 2);
                    break;
            }
        }

        /// <summary>
        /// Adjusts position to ensure window stays within screen bounds
        /// </summary>
        private static void AdjustForScreenBounds(RECT workArea, ref int x, ref int y, int windowWidth, int windowHeight)
        {
            // Adjust horizontal position
            if (x < workArea.Left)
            {
                x = workArea.Left + 10; // Small margin from edge
            }
            else if (x + windowWidth > workArea.Right)
            {
                x = workArea.Right - windowWidth - 10;
            }

            // Adjust vertical position
            if (y < workArea.Top)
            {
                y = workArea.Top + 10;
            }
            else if (y + windowHeight > workArea.Bottom)
            {
                y = workArea.Bottom - windowHeight - 10;
            }
        }

        /// <summary>
        /// Centers a window on the screen
        /// </summary>
        /// <param name="window">The window to center</param>
        public static void CenterOnScreen(Window window)
        {
            try
            {
                // Get cursor position to determine which monitor
                if (!GetCursorPos(out POINT cursorPos))
                {
                    cursorPos = new POINT { X = 0, Y = 0 };
                }

                // Get monitor information
                IntPtr monitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = MONITORINFO.Default;
                
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return;
                }

                var workArea = monitorInfo.rcWork;
                IntPtr hWnd = WindowNative.GetWindowHandle(window);
                
                if (hWnd == IntPtr.Zero) return;

                // Calculate center position
                int windowWidth = (int)window.Bounds.Width;
                int windowHeight = (int)window.Bounds.Height;

                if (windowWidth == 0) windowWidth = 280;
                if (windowHeight == 0) windowHeight = 400;

                int centerX = workArea.Left + (workArea.Width - windowWidth) / 2;
                int centerY = workArea.Top + (workArea.Height - windowHeight) / 2;

                SetWindowPos(hWnd, IntPtr.Zero, centerX, centerY, 0, 0, 
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowPositionHelper: Exception centering window: {ex.Message}");
            }
        }

        /// <summary>
        /// Centers a window over another window
        /// </summary>
        /// <param name="window">The window to position</param>
        /// <param name="targetWindow">The target window to center over</param>
        public static void CenterOnWindow(Window window, WindowInfo targetWindow)
        {
            if (window == null)
            {
                return;
            }

            if (targetWindow == null)
            {
                CenterOnScreen(window);
                return;
            }

            try
            {
                var bounds = targetWindow.Bounds;
                if (bounds == null || (bounds.Width == 0 && bounds.Height == 0))
                {
                    CenterOnScreen(window);
                    return;
                }

                IntPtr hWnd = WindowNative.GetWindowHandle(window);
                if (hWnd == IntPtr.Zero)
                {
                    Debug.WriteLine("WindowPositionHelper: Failed to get window handle");
                    return;
                }

                int windowWidth = (int)window.Bounds.Width;
                int windowHeight = (int)window.Bounds.Height;

                if (windowWidth == 0) windowWidth = 280;
                if (windowHeight == 0) windowHeight = 400;

                int anchorCenterX = bounds.X + (bounds.Width / 2);
                int anchorCenterY = bounds.Y + (bounds.Height / 2);

                int targetX = anchorCenterX - (windowWidth / 2);
                int targetY = anchorCenterY - (windowHeight / 2);

                var monitorPoint = new POINT { X = anchorCenterX, Y = anchorCenterY };
                var monitorInfo = MONITORINFO.Default;
                IntPtr monitor = MonitorFromPoint(monitorPoint, MONITOR_DEFAULTTONEAREST);

                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref monitorInfo))
                {
                    AdjustForScreenBounds(monitorInfo.rcWork, ref targetX, ref targetY, windowWidth, windowHeight);
                }

                bool success = SetWindowPos(hWnd, IntPtr.Zero, targetX, targetY, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                if (!success)
                {
                    Debug.WriteLine($"WindowPositionHelper: CenterOnWindow failed with error {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Debug.WriteLine($"WindowPositionHelper: Window centered over target at {targetX},{targetY}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowPositionHelper: Exception centering on window: {ex.Message}");
            }
        }

        /// <summary>
        /// Centers an external window (not the WinUI menu itself) on the monitor containing the window.
        /// Safe no-op if the handle is invalid or monitor info can't be determined.
        /// </summary>
        /// <param name="windowInfo">WindowInfo representing the external window</param>
        public static void CenterExternalWindowOnMonitor(WindowInfo windowInfo)
        {
            if (windowInfo == null || windowInfo.Handle == IntPtr.Zero) return;

            try
            {
                // Get the current bounds (more accurate than cached WindowInfo at times)
                if (!WindowsApi.GetWindowRect(windowInfo.Handle, out var rect))
                {
                    Debug.WriteLine("WindowPositionHelper: Failed to GetWindowRect for target window.");
                    return;
                }

                var windowWidth = rect.Width;
                var windowHeight = rect.Height;

                // Compute center point of the window to find the correct monitor
                var center = new POINT { X = rect.Left + (windowWidth / 2), Y = rect.Top + (windowHeight / 2) };

                IntPtr monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = MONITORINFO.Default;
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                {
                    Debug.WriteLine("WindowPositionHelper: Failed to get monitor info for external window.");
                    return;
                }

                var workArea = monitorInfo.rcWork;

                int targetX = workArea.Left + (workArea.Width - windowWidth) / 2;
                int targetY = workArea.Top + (workArea.Height - windowHeight) / 2;

                bool success = SetWindowPos(windowInfo.Handle, IntPtr.Zero, targetX, targetY, 0, 0,
                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                if (!success)
                {
                    Debug.WriteLine($"WindowPositionHelper: Failed to center external window, error {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Debug.WriteLine($"WindowPositionHelper: Centered external window at {targetX},{targetY}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowPositionHelper: Exception centering external window: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Preferred position for window relative to cursor
    /// </summary>
    public enum PreferredPosition
    {
        Right,
        Left,
        Below,
        Above
    }
}

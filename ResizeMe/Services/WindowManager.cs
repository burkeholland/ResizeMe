using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ResizeMe.Models;
using ResizeMe.Native;

namespace ResizeMe.Services
{
    /// <summary>
    /// Service for enumerating and filtering windows on the desktop
    /// </summary>
    public class WindowManager
    {
        // List of window class names to exclude from enumeration
        private static readonly HashSet<string> ExcludedClassNames = new()
        {
            "Shell_TrayWnd",        // Taskbar
            "Shell_SecondaryTrayWnd", // Secondary taskbar on multi-monitor
            "DV2ControlHost",       // Windows shell
            "MsgrIMEWindowClass",   // Windows input method
            "SysShadow",            // Drop shadows
            "Button",               // Various buttons
            "Progman",              // Desktop window
            "WorkerW",              // Desktop worker window
            "ImmersiveLauncher",    // Start menu
            "Windows.UI.Core.CoreWindow", // UWP system windows
            "ApplicationFrameWindow", // Some UWP container windows
            "ForegroundStaging",    // Windows 11 staging window
        };

        // List of window titles to exclude
        private static readonly HashSet<string> ExcludedTitles = new()
        {
            "",                     // Empty titles
            "Program Manager",      // Desktop
            "Settings",             // Some system settings (too generic)
        };

        /// <summary>
        /// Gets all visible, resizable windows currently open on the desktop
        /// </summary>
        /// <returns>Collection of window information objects</returns>
        public IEnumerable<WindowInfo> GetResizableWindows()
        {
            var windows = new List<WindowInfo>();

            try
            {
                // Enumerate all top-level windows
                WindowsApi.EnumWindows(EnumWindowCallback, IntPtr.Zero);

                // Local callback function that captures the windows list
                bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
                {
                    try
                    {
                        var windowInfo = GetWindowInfo(hWnd);
                        if (windowInfo != null && IsResizableWindow(windowInfo))
                        {
                            windows.Add(windowInfo);
                            Debug.WriteLine($"WindowManager: Found resizable window: {windowInfo}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"WindowManager: Error processing window {hWnd:X8}: {ex.Message}");
                    }

                    return true; // Continue enumeration
                }

                Debug.WriteLine($"WindowManager: Found {windows.Count} resizable windows");
                return windows.OrderBy(w => w.Title).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowManager: Error during window enumeration: {ex.Message}");
                return new List<WindowInfo>();
            }
        }

        /// <summary>
        /// Gets detailed information about a specific window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>WindowInfo object or null if information couldn't be retrieved</returns>
        private WindowInfo? GetWindowInfo(IntPtr hWnd)
        {
            try
            {
                // Get window title
                int titleLength = WindowsApi.GetWindowTextLength(hWnd);
                var titleBuilder = new StringBuilder(titleLength + 1);
                WindowsApi.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string title = titleBuilder.ToString();

                // Get window class name
                var classNameBuilder = new StringBuilder(256);
                WindowsApi.GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                string className = classNameBuilder.ToString();

                // Get process ID
                WindowsApi.GetWindowThreadProcessId(hWnd, out uint processId);

                // Get window bounds
                WindowsApi.GetWindowRect(hWnd, out var rect);
                var bounds = new WindowBounds
                {
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                };

                // Get window state information
                bool isVisible = WindowsApi.IsWindowVisible(hWnd);
                bool isMinimized = WindowsApi.IsIconic(hWnd);

                return new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ClassName = className,
                    ProcessId = processId,
                    IsVisible = isVisible,
                    IsMinimized = isMinimized,
                    Bounds = bounds,
                    CanResize = true // Will be validated in IsResizableWindow
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowManager: Failed to get window info for {hWnd:X8}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines whether a window should be considered resizable by the user.
        /// Uses a conservative set of filters to exclude system, tool, or cloaked windows
        /// (class names, titles, visibility, size, extended styles, DWM cloaking, and process id checks).
        /// This prevents trying to resize windows that are not user windows or are system-managed.
        /// </summary>
        /// <param name="windowInfo">Window information to evaluate</param>
        /// <returns>True if the window can be resized, false otherwise</returns>
        private bool IsResizableWindow(WindowInfo windowInfo)
        {
            try
            {
                // Skip windows with excluded class names
                if (ExcludedClassNames.Contains(windowInfo.ClassName))
                {
                    return false;
                }

                // Skip windows with excluded titles
                if (ExcludedTitles.Contains(windowInfo.Title))
                {
                    return false;
                }

                // Skip invisible windows
                if (!windowInfo.IsVisible)
                {
                    return false;
                }

                // Skip windows that are too small (likely system windows)
                if (windowInfo.Bounds.Width < 50 || windowInfo.Bounds.Height < 50)
                {
                    return false;
                }

                // Get extended window styles
                long exStyle = WindowsApi.GetWindowLongPtr(windowInfo.Handle, WindowsApi.GWL_EXSTYLE);
                
                // Skip tool windows (like tooltips, etc.)
                if ((exStyle & WindowsApi.WS_EX_TOOLWINDOW) != 0)
                {
                    return false;
                }

                // Check if window is cloaked (hidden by DWM, like UWP background windows)
                try
                {
                    int result = WindowsApi.DwmGetWindowAttribute(
                        windowInfo.Handle, 
                        WindowsApi.DWMWA_CLOAKED, 
                        out bool isCloaked, 
                        sizeof(bool));
                    
                    if (result == 0 && isCloaked) // S_OK and is cloaked
                    {
                        return false;
                    }
                }
                catch
                {
                    // DWM attribute check failed, continue anyway
                }

                // Check if it's our own window (avoid recursive resizing)
                try
                {
                    var currentProcess = Process.GetCurrentProcess();
                    if (windowInfo.ProcessId == currentProcess.Id)
                    {
                        return false;
                    }
                }
                catch
                {
                    // Process check failed, allow the window anyway
                }

                // Additional filters for common problematic windows
                var titleLower = windowInfo.Title.ToLowerInvariant();
                if (titleLower.Contains("task view") || 
                    titleLower.Contains("cortana") ||
                    titleLower.Contains("search") && windowInfo.ClassName.Contains("Windows.UI"))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowManager: Error filtering window {windowInfo.Handle:X8}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets information about a specific window by its handle
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>WindowInfo object or null if not found/invalid</returns>
        public WindowInfo? GetWindowById(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return null;

            var windowInfo = GetWindowInfo(hWnd);
            return windowInfo != null && IsResizableWindow(windowInfo) ? windowInfo : null;
        }

        /// <summary>
        /// Refreshes and returns current window information for a previously discovered window
        /// </summary>
        /// <param name="windowInfo">Previously discovered window info</param>
        /// <returns>Updated window info or null if window no longer exists</returns>
        public WindowInfo? RefreshWindowInfo(WindowInfo windowInfo)
        {
            return GetWindowById(windowInfo.Handle);
        }

        /// <summary>
        /// Gets the currently active/resizable window
        /// </summary>
        /// <returns>WindowInfo object for the active window or null if not found/invalid</returns>
        public WindowInfo? GetActiveResizableWindow()
        {
            try
            {
                var foregroundHandle = WindowsApi.GetForegroundWindow();
                if (foregroundHandle == IntPtr.Zero)
                {
                    return null;
                }

                return GetWindowById(foregroundHandle);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowManager: Failed to get active window: {ex.Message}");
                return null;
            }
        }
    }
}

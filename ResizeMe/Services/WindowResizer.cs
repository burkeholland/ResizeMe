using System;
using System.Diagnostics;
using ResizeMe.Models;
using ResizeMe.Native;

namespace ResizeMe.Services
{
    /// <summary>
    /// Service for resizing windows using Windows API
    /// </summary>
    public class WindowResizer
    {
        /// <summary>
        /// Resizes the specified window to the given dimensions
        /// </summary>
        /// <param name="windowInfo">Information about the window to resize</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <returns>Result of the resize operation</returns>
        public ResizeResult ResizeWindow(WindowInfo windowInfo, int width, int height)
        {
            var requestedSize = new WindowSize(width, height);
            
            if (windowInfo?.Handle == null || windowInfo.Handle == IntPtr.Zero)
            {
                return ResizeResult.CreateFailure(windowInfo!, requestedSize, "Invalid window handle");
            }

            if (!requestedSize.IsValid)
            {
                return ResizeResult.CreateFailure(windowInfo, requestedSize, "Invalid target size (width and height must be greater than 0)");
            }

            try
            {
                Debug.WriteLine($"WindowResizer: Attempting to resize {windowInfo} to {requestedSize}");

                // Check if window still exists and is valid
                if (!IsWindowValid(windowInfo.Handle))
                {
                    return ResizeResult.CreateFailure(windowInfo, requestedSize, "Window no longer exists or is not accessible");
                }

                // Handle minimized/maximized windows
                bool windowStateChanged = false;
                var currentState = GetWindowState(windowInfo.Handle);
                
                if (currentState != WindowState.Normal)
                {
                    Debug.WriteLine($"WindowResizer: Window is {currentState}, restoring to normal state");
                    if (!RestoreWindow(windowInfo.Handle))
                    {
                        return ResizeResult.CreateFailure(windowInfo, requestedSize, 
                            $"Failed to restore window from {currentState} state", WindowsApi.GetLastError());
                    }
                    windowStateChanged = true;
                }

                // Get current window position to preserve it
                if (!WindowsApi.GetWindowRect(windowInfo.Handle, out var currentRect))
                {
                    return ResizeResult.CreateFailure(windowInfo, requestedSize, 
                        "Failed to get current window position", WindowsApi.GetLastError());
                }

                // Perform the resize operation
                bool success = WindowsApi.SetWindowPos(
                    windowInfo.Handle,
                    WindowsApi.HWND_TOP,
                    currentRect.Left,  // Keep current X position
                    currentRect.Top,   // Keep current Y position
                    width,             // New width
                    height,            // New height
                    WindowsApi.SWP_NOZORDER | WindowsApi.SWP_NOACTIVATE | WindowsApi.SWP_SHOWWINDOW
                );

                if (!success)
                {
                    uint errorCode = WindowsApi.GetLastError();
                    string errorMessage = GetErrorMessage(errorCode);
                    return ResizeResult.CreateFailure(windowInfo, requestedSize, errorMessage, errorCode);
                }

                // Verify the resize was successful by checking new dimensions
                if (!WindowsApi.GetWindowRect(windowInfo.Handle, out var newRect))
                {
                    Debug.WriteLine("WindowResizer: Warning - Could not verify new window size");
                }

                var actualSize = new WindowSize(newRect.Width, newRect.Height);
                Debug.WriteLine($"WindowResizer: Successfully resized to {actualSize}");

                return ResizeResult.CreateSuccess(windowInfo, requestedSize, actualSize, windowStateChanged);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowResizer: Exception during resize: {ex.Message}");
                return ResizeResult.CreateFailure(windowInfo, requestedSize, $"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Resizes a window using a WindowSize object
        /// </summary>
        /// <param name="windowInfo">Information about the window to resize</param>
        /// <param name="targetSize">Target size for the window</param>
        /// <returns>Result of the resize operation</returns>
        public ResizeResult ResizeWindow(WindowInfo windowInfo, WindowSize targetSize)
        {
            return ResizeWindow(windowInfo, targetSize.Width, targetSize.Height);
        }

        /// <summary>
        /// Gets the current state of a window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>Current window state</returns>
        private WindowState GetWindowState(IntPtr hWnd)
        {
            try
            {
                if (WindowsApi.IsIconic(hWnd))
                {
                    return WindowState.Minimized;
                }

                if (WindowsApi.IsZoomed(hWnd))
                {
                    return WindowState.Maximized;
                }

                return WindowState.Normal;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowResizer: Error getting window state: {ex.Message}");
                return WindowState.Normal;
            }
        }

        /// <summary>
        /// Restores a window from minimized or maximized state
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool RestoreWindow(IntPtr hWnd)
        {
            try
            {
                // First try ShowWindow with SW_RESTORE
                bool success = WindowsApi.ShowWindow(hWnd, WindowsApi.SW_RESTORE);
                
                if (success)
                {
                    // Give the window time to restore
                    System.Threading.Thread.Sleep(100);
                    
                    // Verify it's actually restored
                    if (!WindowsApi.IsIconic(hWnd) && !WindowsApi.IsZoomed(hWnd))
                    {
                        return true;
                    }
                }

                // Fallback: Try using SetWindowPlacement
                var placement = WindowsApi.WINDOWPLACEMENT.Default;
                if (WindowsApi.GetWindowPlacement(hWnd, ref placement))
                {
                    placement.showCmd = WindowsApi.SW_SHOWNORMAL;
                    return WindowsApi.SetWindowPlacement(hWnd, ref placement);
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowResizer: Error restoring window: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a window handle is still valid and accessible
        /// </summary>
        /// <param name="hWnd">Handle to check</param>
        /// <returns>True if valid, false otherwise</returns>
        private bool IsWindowValid(IntPtr hWnd)
        {
            try
            {
                // Try to get window rect - this will fail if window doesn't exist
                return WindowsApi.GetWindowRect(hWnd, out var _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a user-friendly error message for common Windows API error codes
        /// </summary>
        /// <param name="errorCode">Windows API error code</param>
        /// <returns>Human-readable error message</returns>
        private string GetErrorMessage(uint errorCode)
        {
            return errorCode switch
            {
                0 => "Operation completed successfully",
                5 => "Access denied - the window may be owned by a privileged process",
                6 => "Invalid handle - the window may have been closed",
                87 => "Invalid parameter - the specified coordinates may be out of range",
                1400 => "Invalid window handle - the window no longer exists",
                1401 => "Invalid menu handle",
                1402 => "Invalid cursor handle",
                1403 => "Invalid accelerator table handle",
                1404 => "Invalid hook handle",
                1405 => "Invalid DWP (Deferred Window Position) handle",
                1406 => "Cannot create top-level child window",
                1407 => "Cannot find window class",
                1408 => "Invalid window - cannot find window",
                1409 => "Invalid index",
                _ => $"Windows API error {errorCode}"
            };
        }

        /// <summary>
        /// Activates a window and brings it to the foreground
        /// </summary>
        /// <param name="windowInfo">Information about the window to activate</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool ActivateWindow(WindowInfo windowInfo)
        {
            if (windowInfo?.Handle == null || windowInfo.Handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                // Restore if minimized
                if (WindowsApi.IsIconic(windowInfo.Handle))
                {
                    WindowsApi.ShowWindow(windowInfo.Handle, WindowsApi.SW_RESTORE);
                }

                // Bring to front and activate
                bool success = WindowsApi.SetForegroundWindow(windowInfo.Handle);
                if (!success)
                {
                    // Fallback: Try BringWindowToTop
                    success = WindowsApi.BringWindowToTop(windowInfo.Handle);
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WindowResizer: Error activating window: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Enumeration of possible window states
    /// </summary>
    internal enum WindowState
    {
        Normal,
        Minimized,
        Maximized
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ResizeMe.Native;

namespace ResizeMe.Services
{
    /// <summary>
    /// Manages global hotkey registration and handles hotkey events.
    /// </summary>
    public class HotKeyManager : IDisposable
    {
        private const int HOTKEY_ID = 9000; // Unique identifier for our hotkey
        private readonly IntPtr _windowHandle;
        private bool _isRegistered;
        private bool _disposed;

        /// <summary>
        /// Event fired when the registered hotkey is pressed
        /// </summary>
        public event EventHandler? HotKeyPressed;

        /// <summary>
        /// Gets whether the hotkey is currently registered
        /// </summary>
        public bool IsRegistered => _isRegistered;

        public HotKeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        /// <summary>
        /// Registers the global hotkey (Win+Shift+F12)
        /// </summary>
        public bool RegisterHotKey()
        {
            if (_isRegistered)
            {
                Debug.WriteLine("HotKeyManager: Hotkey is already registered");
                return true;
            }

            if (_windowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("HotKeyManager: Invalid window handle");
                return false;
            }

            try
            {
                bool success = WindowsApi.RegisterHotKey(
                    _windowHandle,
                    HOTKEY_ID,
                    (uint)(WindowsApi.MOD_WIN | WindowsApi.MOD_SHIFT),
                    (uint)WindowsApi.VK_F12);

                if (success)
                {
                    _isRegistered = true;
                    Debug.WriteLine("HotKeyManager: Successfully registered Win+Shift+F12 hotkey");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"HotKeyManager: Failed to register hotkey. Win32Error: {errorCode}");

                    // 1409 (ERROR_HOTKEY_ALREADY_REGISTERED) - hotkey already in use
                    if (errorCode == 1409)
                    {
                        Debug.WriteLine("HotKeyManager: Hotkey Win+Shift+F12 is already registered by another application");
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HotKeyManager: Exception during hotkey registration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unregisters the global hotkey
        /// </summary>
        public bool UnregisterHotKey()
        {
            if (!_isRegistered)
            {
                Debug.WriteLine("HotKeyManager: No hotkey to unregister");
                return true;
            }

            try
            {
                bool success = WindowsApi.UnregisterHotKey(_windowHandle, HOTKEY_ID);

                if (success)
                {
                    _isRegistered = false;
                    Debug.WriteLine("HotKeyManager: Successfully unregistered hotkey");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"HotKeyManager: Failed to unregister hotkey. Win32Error: {errorCode}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HotKeyManager: Exception during hotkey unregistration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Processes Windows messages to detect hotkey presses
        /// </summary>
        public bool ProcessMessage(uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WindowsApi.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                Debug.WriteLine("HotKeyManager: Hotkey pressed - firing event");
                HotKeyPressed?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_isRegistered)
                {
                    UnregisterHotKey();
                }
                _disposed = true;
            }
        }

        ~HotKeyManager()
        {
            Dispose(false);
        }
    }
}

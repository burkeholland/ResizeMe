using System;
using System.Diagnostics;
using System.Linq;
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

        // Reserved system hotkeys (normalized tokens)
        private static readonly System.Collections.Generic.HashSet<string> ReservedHotkeys = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "ALT+TAB", "ALT+F4", "CTRL+ALT+DELETE", "WIN+D", "WIN+L", "WIN+R", "WIN+E", "WIN+V", "WIN+M", "WIN+SHIFT+S"
        };

        public static bool IsReserved(string modifiers, string key)
        {
            var normalized = Normalize(modifiers, key);
            return ReservedHotkeys.Contains(normalized);
        }

        private static string Normalize(string modifiers, string key)
        {
            var order = new[] { "CTRL", "ALT", "SHIFT", "WIN" };
            var parts = modifiers
                .Split('+', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
                .Select(p => p.ToUpperInvariant())
                .Where(p => order.Contains(p))
                .Distinct()
                .OrderBy(p => System.Array.IndexOf(order, p));
            string k = key.ToUpperInvariant();
            return string.Join("+", parts.Append(k));
        }

        public HotKeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        /// <summary>
        /// Registers the global hotkey (Ctrl+Win+R)
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
                // Build modifier mask from user preferences
                uint modMask = 0;
                var mods = Services.UserPreferences.HotKeyModifiers.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var m in mods)
                {
                    switch (m.ToUpperInvariant())
                    {
                        case "CTRL": modMask |= Native.WindowsApi.MOD_CONTROL; break;
                        case "ALT": modMask |= Native.WindowsApi.MOD_ALT; break;
                        case "SHIFT": modMask |= Native.WindowsApi.MOD_SHIFT; break;
                        case "WIN": modMask |= Native.WindowsApi.MOD_WIN; break;
                    }
                }
                if (modMask == 0) modMask = Native.WindowsApi.MOD_WIN; // Safety fallback

                // Key translation (supports F-keys and letters/numbers)
                string keyToken = Services.UserPreferences.HotKeyCode.ToUpperInvariant();
                uint vk = TranslateKeyToken(keyToken);

                bool success = Native.WindowsApi.RegisterHotKey(_windowHandle, HOTKEY_ID, modMask, vk);

                if (success)
                {
                    _isRegistered = true;
                    Debug.WriteLine($"HotKeyManager: Registered {Services.UserPreferences.HotKeyModifiers}+{Services.UserPreferences.HotKeyCode}");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"HotKeyManager: Failed to register hotkey. Win32Error: {errorCode}");

                    // 1409 (ERROR_HOTKEY_ALREADY_REGISTERED) - hotkey already in use
                    if (errorCode == 1409)
                    {
                            Debug.WriteLine("HotKeyManager: Hotkey is already registered by another application");
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

        private static uint TranslateKeyToken(string token)
        {
            // Map special tokens (non-alphanumeric) to OEM VK codes where appropriate
            switch (token)
            {
                case "+":
                    return Native.WindowsApi.VK_OEM_PLUS;
                case ",":
                    return Native.WindowsApi.VK_OEM_COMMA;
                case "-":
                    return Native.WindowsApi.VK_OEM_MINUS;
                case ".":
                    return Native.WindowsApi.VK_OEM_PERIOD;
            }
            if (token.StartsWith("F") && int.TryParse(token.Substring(1), out int fNum) && fNum >= 1 && fNum <= 24)
            {
                return (uint)(0x70 + (fNum - 1)); // VK_F1=0x70
            }
            if (token.Length == 1)
            {
                char c = token[0];
                if (c >= '0' && c <= '9') return (uint)c; // digits map directly
                if (c >= 'A' && c <= 'Z') return (uint)c; // letters map directly
            }
            // fallback F12
            return Native.WindowsApi.VK_F12;
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

        public bool ReRegister()
        {
            if (_isRegistered) UnregisterHotKey();
            return RegisterHotKey();
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

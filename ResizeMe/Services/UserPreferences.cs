using System;
using Windows.Storage;

namespace ResizeMe.Services
{
    /// <summary>
    /// Small typed wrapper around ApplicationData.Current.LocalSettings for simple flags.
    /// </summary>
    public static class UserPreferences
    {
        private const string CenterKey = "CenterOnResize";
        private const string FirstRunKey = "FirstRunCompleted";
        private const string FirstMinimizeKey = "FirstMinimizeNotificationShown";
        private const string HotKeyModifiersKey = "HotKeyModifiers"; // e.g. WIN+SHIFT
        private const string HotKeyCodeKey = "HotKeyKey";           // e.g. F12

        /// <summary>
        /// Gets or sets whether windows should be centered after a quick resize.
        /// </summary>
        public static bool CenterOnResize
        {
            get
            {
                try
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    if (settings.Values.TryGetValue(CenterKey, out var val) && val is bool b)
                    {
                        return b;
                    }
                }
                catch
                {
                    // Swallow - preferences should never crash the app.
                }
                return false;
            }
            set
            {
                try
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values[CenterKey] = value;
                }
                catch
                {
                    // Swallow - ignore preferences save failures
                }
            }
        }

        /// <summary>
        /// Indicates whether the application has completed the first run experience.
        /// </summary>
        public static bool FirstRunCompleted
        {
            get
            {
                try
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    if (settings.Values.TryGetValue(FirstRunKey, out var val) && val is bool b)
                    {
                        return b;
                    }
                }
                catch
                {
                    // Swallow - preferences should never crash the app.
                }
                return false; // default: not completed
            }
            set
            {
                try
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values[FirstRunKey] = value;
                }
                catch
                {
                    // Swallow - ignore save failures
                }
            }
        }

        /// <summary>
        /// Indicates whether the tray/hotkey notification has already been shown.
        /// </summary>
        public static bool FirstMinimizeNotificationShown
        {
            get
            {
                try
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    if (settings.Values.TryGetValue(FirstMinimizeKey, out var val) && val is bool b)
                    {
                        return b;
                    }
                }
                catch { }
                return false;
            }
            set
            {
                try
                {
                    var settings = ApplicationData.Current.LocalSettings;
                    settings.Values[FirstMinimizeKey] = value;
                }
                catch { }
            }
        }

        /// <summary>
        /// Hotkey modifier list (canonical upper-case plus-separated: CTRL+ALT+SHIFT+WIN).
        /// </summary>
        public static string HotKeyModifiers
        {
            get
            {
                try
                {
                    var s = Windows.Storage.ApplicationData.Current.LocalSettings;
                    if (s.Values.TryGetValue(HotKeyModifiersKey, out var val) && val is string str && !string.IsNullOrWhiteSpace(str))
                    {
                        return str.ToUpperInvariant();
                    }
                }
                catch { }
                return "CTRL+WIN"; // default
            }
            set
            {
                try
                {
                    var s = Windows.Storage.ApplicationData.Current.LocalSettings;
                    s.Values[HotKeyModifiersKey] = value.ToUpperInvariant();
                }
                catch { }
            }
        }

        /// <summary>
        /// Hotkey primary key (single token, e.g. F12, R, 1).
        /// </summary>
        public static string HotKeyCode
        {
            get
            {
                try
                {
                    var s = Windows.Storage.ApplicationData.Current.LocalSettings;
                    if (s.Values.TryGetValue(HotKeyCodeKey, out var val) && val is string str && !string.IsNullOrWhiteSpace(str))
                    {
                        return str.ToUpperInvariant();
                    }
                }
                catch { }
                return "R"; // default
            }
            set
            {
                try
                {
                    var s = Windows.Storage.ApplicationData.Current.LocalSettings;
                    s.Values[HotKeyCodeKey] = value.ToUpperInvariant();
                }
                catch { }
            }
        }
    }
}
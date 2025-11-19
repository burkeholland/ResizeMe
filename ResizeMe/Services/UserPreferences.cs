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

        private static T GetPreference<T>(string key, T defaultValue)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                if (settings.Values.TryGetValue(key, out var val) && val is T t)
                {
                    return t;
                }
            }
            catch
            {
                // Preferences should never crash the app.
            }
            return defaultValue;
        }

        private static void SetPreference<T>(string key, T value)
        {
            try
            {
                var settings = ApplicationData.Current.LocalSettings;
                settings.Values[key] = value;
            }
            catch
            {
                // Swallow - ignore preferences save failures
            }
        }

        /// <summary>
        /// Gets or sets whether windows should be centered after a quick resize.
        /// </summary>
        public static bool CenterOnResize
        {
            get => GetPreference(CenterKey, false);
            set => SetPreference(CenterKey, value);
        }

        /// <summary>
        /// Indicates whether the application has completed the first run experience.
        /// </summary>
        public static bool FirstRunCompleted
        {
            get => GetPreference(FirstRunKey, false);
            set => SetPreference(FirstRunKey, value);
        }

        /// <summary>
        /// Indicates whether the tray/hotkey notification has already been shown.
        /// </summary>
        public static bool FirstMinimizeNotificationShown
        {
            get => GetPreference(FirstMinimizeKey, false);
            set => SetPreference(FirstMinimizeKey, value);
        }

        /// <summary>
        /// Hotkey modifier list (canonical upper-case plus-separated: CTRL+ALT+SHIFT+WIN).
        /// </summary>
        public static string HotKeyModifiers
        {
            get
            {
                var value = GetPreference<string>(HotKeyModifiersKey, "CTRL+WIN");
                return string.IsNullOrWhiteSpace(value) ? "CTRL+WIN" : value.ToUpperInvariant();
            }
            set => SetPreference(HotKeyModifiersKey, string.IsNullOrWhiteSpace(value) ? "CTRL+WIN" : value.ToUpperInvariant());
        }

        /// <summary>
        /// Hotkey primary key (single token, e.g. F12, R, 1).
        /// </summary>
        public static string HotKeyCode
        {
            get
            {
                var value = GetPreference<string>(HotKeyCodeKey, "R");
                return string.IsNullOrWhiteSpace(value) ? "R" : value.ToUpperInvariant();
            }
            set => SetPreference(HotKeyCodeKey, string.IsNullOrWhiteSpace(value) ? "R" : value.ToUpperInvariant());
        }
    }
}
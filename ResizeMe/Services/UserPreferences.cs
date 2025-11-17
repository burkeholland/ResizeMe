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
    }
}
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
    }
}
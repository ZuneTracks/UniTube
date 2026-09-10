using System;
using System.Globalization;
using Windows.ApplicationModel.Resources;

namespace YouTube.Uwp.Services
{
    internal static class Localizer
    {
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A resource key is required.", "key");
            }

            string resourceKey = key.Replace('.', '/');
            string value = ResourceLoader.GetForCurrentView().GetString(resourceKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("The required localization resource '" + key + "' is missing or empty.");
            }

            return value;
        }

        public static string Format(string key, params object[] arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException("arguments");
            }

            return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
        }
    }
}

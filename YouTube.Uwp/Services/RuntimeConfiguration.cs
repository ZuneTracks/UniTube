using System;
using Windows.Storage;

namespace YouTube.Uwp.Services
{
    public sealed class RuntimeConfiguration
    {
        private const string ApiKeyResource = "YourTube.ApiKey";
        private const string ApiKeyUserName = "PublicReadOnly";
        private const string OAuthClientIdKey = "OAuthClientId";
        private const string OAuthClientSecretResource = "YourTube.OAuthClientSecret";
        private const string OAuthClientSecretUserName = "LimitedInputDevice";
        private const string SafeModeEnabledKey = "SafeModeEnabled";

        public string OAuthClientId
        {
            get
            {
                return HasStoredOAuthDeviceCredentials
                    ? StoredOAuthClientId
                    : GetBuildDefaultOAuthClientId();
            }
        }

        public string StoredOAuthClientId
        {
            get { return ReadSetting(OAuthClientIdKey); }
        }

        public bool HasApiKey
        {
            get { return !string.IsNullOrWhiteSpace(GetApiKey()); }
        }

        public bool HasOAuthDeviceCredentials
        {
            get
            {
                return !string.IsNullOrWhiteSpace(OAuthClientId)
                    && !string.IsNullOrWhiteSpace(GetOAuthClientSecret());
            }
        }

        public bool HasStoredOAuthDeviceCredentials
        {
            get
            {
                return !string.IsNullOrWhiteSpace(StoredOAuthClientId)
                    && !string.IsNullOrWhiteSpace(
                        SecureCredentialStore.Read(OAuthClientSecretResource, OAuthClientSecretUserName));
            }
        }

        public bool HasBuildDefaultOAuthDeviceCredentials
        {
            get
            {
                return !string.IsNullOrWhiteSpace(GetBuildDefaultOAuthClientId())
                    && !string.IsNullOrWhiteSpace(GetBuildDefaultOAuthClientSecret());
            }
        }

        public bool HasStoredApiKey
        {
            get { return !string.IsNullOrWhiteSpace(SecureCredentialStore.Read(ApiKeyResource, ApiKeyUserName)); }
        }

        public bool HasBuildDefaultApiKey
        {
            get { return !string.IsNullOrWhiteSpace(GetBuildDefaultApiKey()); }
        }

        public bool IsSafeModeEnabled
        {
            get
            {
                object value;
                if (!ApplicationData.Current.LocalSettings.Values.TryGetValue(SafeModeEnabledKey, out value))
                {
                    return true;
                }

                return value is bool ? (bool)value : true;
            }
        }

        public void SetSafeModeEnabled(bool isEnabled)
        {
            ApplicationData.Current.LocalSettings.Values[SafeModeEnabledKey] = isEnabled;
        }

        public string GetApiKey()
        {
            return FirstConfiguredValue(
                SecureCredentialStore.Read(ApiKeyResource, ApiKeyUserName),
                GetBuildDefaultApiKey());
        }

        public void SaveApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException(Localizer.Get("Runtime.ApiKeyRequired"), "apiKey");
            }

            SecureCredentialStore.Write(ApiKeyResource, ApiKeyUserName, apiKey.Trim());
        }

        public void ClearApiKey()
        {
            SecureCredentialStore.Delete(ApiKeyResource, ApiKeyUserName);
        }

        public string GetOAuthClientSecret()
        {
            return HasStoredOAuthDeviceCredentials
                ? SecureCredentialStore.Read(OAuthClientSecretResource, OAuthClientSecretUserName)
                : GetBuildDefaultOAuthClientSecret();
        }

        public bool HasIncompleteStoredOAuthDeviceCredentials
        {
            get
            {
                bool hasStoredClientId = !string.IsNullOrWhiteSpace(StoredOAuthClientId);
                bool hasStoredClientSecret = !string.IsNullOrWhiteSpace(
                    SecureCredentialStore.Read(OAuthClientSecretResource, OAuthClientSecretUserName));
                return hasStoredClientId != hasStoredClientSecret;
            }
        }

        public string GetOAuthDeviceCredentialSource()
        {
            if (HasStoredOAuthDeviceCredentials)
            {
                return "saved";
            }

            if (HasBuildDefaultOAuthDeviceCredentials)
            {
                return HasIncompleteStoredOAuthDeviceCredentials
                    ? "built-in (incomplete saved override ignored)"
                    : "built-in";
            }

            return "unconfigured";
        }

        public void SaveOAuthDeviceSettings(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException(Localizer.Get("Runtime.OAuthClientIdRequired"), "clientId");
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException(Localizer.Get("Runtime.OAuthClientSecretRequired"), "clientSecret");
            }

            string normalizedClientId = clientId.Trim();
            string normalizedClientSecret = clientSecret.Trim();
            if (!string.Equals(OAuthClientId, normalizedClientId, StringComparison.Ordinal)
                || !string.Equals(GetOAuthClientSecret(), normalizedClientSecret, StringComparison.Ordinal))
            {
                OAuthDeviceAuthorizationService.ClearStoredToken();
            }

            SaveSetting(OAuthClientIdKey, normalizedClientId);
            SecureCredentialStore.Write(OAuthClientSecretResource, OAuthClientSecretUserName, normalizedClientSecret);
        }

        private static string ReadSetting(string key)
        {
            object value;
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value))
            {
                return value as string;
            }

            return null;
        }

        private static void SaveSetting(string key, string value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
        }

        private static string FirstConfiguredValue(string storedValue, string buildDefault)
        {
            return string.IsNullOrWhiteSpace(storedValue) ? buildDefault : storedValue;
        }

        private static string GetBuildDefaultApiKey()
        {
#if LOCAL_BUILD_CONFIGURATION
            return NormalizeBuildDefault(LocalBuildConfiguration.ApiKey);
#else
            return null;
#endif
        }

        private static string GetBuildDefaultOAuthClientId()
        {
#if LOCAL_BUILD_CONFIGURATION
            return NormalizeBuildDefault(LocalBuildConfiguration.OAuthClientId);
#else
            return null;
#endif
        }

        private static string GetBuildDefaultOAuthClientSecret()
        {
#if LOCAL_BUILD_CONFIGURATION
            return NormalizeBuildDefault(LocalBuildConfiguration.OAuthClientSecret);
#else
            return null;
#endif
        }

        private static string NormalizeBuildDefault(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.StartsWith("REPLACE_WITH_", StringComparison.Ordinal))
            {
                return null;
            }

            return value.Trim();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace YouTube.Uwp.Services
{
    public sealed class OAuthDeviceAuthorizationService
    {
        public const string YouTubeUploadScope = "https://www.googleapis.com/auth/youtube.upload";
        public const string YouTubeReadonlyScope = "https://www.googleapis.com/auth/youtube.readonly";
        public const string YouTubeDeviceAuthorizationScope = YouTubeUploadScope + " " + YouTubeReadonlyScope;
        private const string DeviceAuthorizationEndpoint = "https://oauth2.googleapis.com/device/code";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string TokenAccessResource = "YourTube.OAuthToken.Access";
        private const string TokenRefreshResource = "YourTube.OAuthToken.Refresh";
        private const string TokenExpiryResource = "YourTube.OAuthToken.Expiry";
        private const string CredentialUserName = "CurrentUser";
        private readonly RuntimeConfiguration configuration;
        private readonly HttpClient httpClient;

        public OAuthDeviceAuthorizationService(RuntimeConfiguration configuration)
            : this(configuration, new HttpClient())
        {
        }

        private static void ReportProgress(
            IProgress<DeviceAuthorizationProgress> progress,
            DateTimeOffset expiresAt,
            int pollIntervalSeconds,
            string status)
        {
            if (progress != null)
            {
                progress.Report(new DeviceAuthorizationProgress(
                    Math.Max(0, (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalSeconds)),
                    pollIntervalSeconds,
                    status));
            }
        }

        private static string GetErrorIdentifier(string content)
        {
            JsonObject responseJson;
            return JsonObject.TryParse(content, out responseJson)
                ? responseJson.GetNamedString("error", "unknown_error")
                : "unknown_error";
        }

        public OAuthDeviceAuthorizationService(RuntimeConfiguration configuration, HttpClient httpClient)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            this.configuration = configuration;
            this.httpClient = httpClient;
        }

        public async Task<DeviceAuthorizationInfo> BeginAuthorizationAsync(CancellationToken cancellationToken)
        {
            OAuthDeviceCredentials credentials = GetValidatedCredentials();
            DiagnosticLog.Write("OAuth.DeviceCode", "Requesting device authorization code.");
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("client_id", credentials.ClientId);
            parameters.Add("scope", YouTubeDeviceAuthorizationScope);

            using (HttpResponseMessage response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, DeviceAuthorizationEndpoint)
                {
                    Content = new FormUrlEncodedContent(parameters)
                },
                cancellationToken))
            {
                string content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string error = GetErrorIdentifier(content);
                    DiagnosticLog.Write("OAuth.DeviceCode", "Device authorization request failed with HTTP " + ((int)response.StatusCode) + ": " + error + ".");
                    if (error == "invalid_client")
                    {
                        throw new OAuthException(
                            Localizer.Get("OAuth.InvalidClient"));
                    }

                    throw new OAuthException(Localizer.Format("OAuth.AuthorizationStartFailed", error));
                }

                JsonObject responseJson = ParseJson(content, Localizer.Get("OAuth.InvalidDeviceAuthorizationResponse"));
                string deviceCode = responseJson.GetNamedString("device_code", string.Empty);
                string userCode = responseJson.GetNamedString("user_code", string.Empty);
                string verificationUrl = responseJson.GetNamedString("verification_url", string.Empty);
                if (string.IsNullOrWhiteSpace(verificationUrl))
                {
                    verificationUrl = responseJson.GetNamedString("verification_uri", string.Empty);
                }

                Uri verificationUri;
                if (string.IsNullOrWhiteSpace(deviceCode)
                    || string.IsNullOrWhiteSpace(userCode)
                    || !Uri.TryCreate(verificationUrl, UriKind.Absolute, out verificationUri))
                {
                    throw new OAuthException(Localizer.Get("OAuth.InvalidVerificationCodeResponse"));
                }

                return new DeviceAuthorizationInfo(
                    deviceCode,
                    userCode,
                    verificationUri,
                    responseJson.GetNamedNumber("expires_in", 1800),
                    responseJson.GetNamedNumber("interval", 5));
            }
        }

        public async Task<OAuthToken> CompleteAuthorizationAsync(
            DeviceAuthorizationInfo authorization,
            IProgress<DeviceAuthorizationProgress> progress,
            CancellationToken cancellationToken)
        {
            if (authorization == null)
            {
                throw new ArgumentNullException("authorization");
            }

            OAuthDeviceCredentials credentials = GetValidatedCredentials();
            DiagnosticLog.Write("OAuth.Poll", "Waiting for device authorization approval.");
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddSeconds(authorization.ExpiresInSeconds);
            int pollIntervalSeconds = authorization.PollIntervalSeconds;
            while (DateTimeOffset.UtcNow < expiresAt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReportProgress(progress, expiresAt, pollIntervalSeconds, Localizer.Get("Settings.WaitingForGoogleApproval"));
                TimeSpan remaining = expiresAt - DateTimeOffset.UtcNow;
                TimeSpan delay = remaining < TimeSpan.FromSeconds(pollIntervalSeconds)
                    ? remaining
                    : TimeSpan.FromSeconds(pollIntervalSeconds);
                await Task.Delay(delay, cancellationToken);
                if (DateTimeOffset.UtcNow >= expiresAt)
                {
                    break;
                }

                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("device_code", authorization.DeviceCode);
                parameters.Add("client_id", credentials.ClientId);
                parameters.Add("client_secret", credentials.ClientSecret);
                parameters.Add("grant_type", "urn:ietf:params:oauth:grant-type:device_code");

                DeviceTokenResponse response = await RequestDeviceTokenAsync(parameters, cancellationToken);
                if (response.Token != null)
                {
                    DiagnosticLog.Write("OAuth.Poll", "Google returned tokens; saving them in Credential Locker.");
                    SaveToken(response.Token);
                    DiagnosticLog.Write("OAuth.Poll", "Token persistence completed.");
                    return response.Token;
                }

                if (response.Error == "authorization_pending")
                {
                    ReportProgress(progress, expiresAt, pollIntervalSeconds, Localizer.Get("Settings.WaitingForGoogleApproval"));
                    continue;
                }

                if (response.Error == "slow_down")
                {
                    pollIntervalSeconds += 5;
                    DiagnosticLog.Write("OAuth.Poll", "Google requested a slower polling interval.");
                    ReportProgress(progress, expiresAt, pollIntervalSeconds, Localizer.Get("OAuth.SlowerPolling"));
                    continue;
                }

                if (response.Error == "expired_token")
                {
                    throw new DeviceAuthorizationExpiredException();
                }

                DiagnosticLog.Write("OAuth.Poll", "Google returned terminal device authorization error: " + response.Error + ".");
                if (response.Error == "invalid_client")
                {
                    throw new OAuthException(
                        Localizer.Get("OAuth.InvalidCredentials"));
                }

                throw new OAuthException(Localizer.Format("OAuth.AuthorizationIncomplete", response.Error));
            }

            throw new DeviceAuthorizationExpiredException();
        }

        public async Task<string> GetValidAccessTokenAsync()
        {
            OAuthDeviceCredentials credentials = GetValidatedCredentials();
            OAuthToken token = ReadToken();
            if (token == null)
            {
                throw new OAuthException(Localizer.Get("OAuth.NoAccountAuthorized"));
            }

            if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1) && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return token.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                throw new OAuthException(Localizer.Get("OAuth.AuthorizationMustBeRepeated"));
            }

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("client_id", credentials.ClientId);
            parameters.Add("client_secret", credentials.ClientSecret);
            parameters.Add("refresh_token", token.RefreshToken);
            parameters.Add("grant_type", "refresh_token");
            OAuthToken refreshed = await RequestTokenAsync(parameters, CancellationToken.None);
            refreshed.RefreshToken = token.RefreshToken;
            SaveToken(refreshed);
            return refreshed.AccessToken;
        }

        private async Task<DeviceTokenResponse> RequestDeviceTokenAsync(
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(parameters)
                },
                cancellationToken))
            {
                string content = await response.Content.ReadAsStringAsync();
                JsonObject responseJson = ParseJson(content, Localizer.Get("OAuth.InvalidTokenResponse"));
                if (!response.IsSuccessStatusCode)
                {
                    return new DeviceTokenResponse(null, responseJson.GetNamedString("error", "unknown_error"));
                }

                return new DeviceTokenResponse(CreateToken(responseJson), null);
            }
        }

        private async Task<OAuthToken> RequestTokenAsync(
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(parameters)
                },
                cancellationToken))
            {
                string content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new OAuthException(Localizer.Format("OAuth.TokenRefreshFailed", content));
                }

                return CreateToken(ParseJson(content, Localizer.Get("OAuth.InvalidTokenResponse")));
            }
        }

        private static OAuthToken CreateToken(JsonObject responseJson)
        {
            string accessToken = responseJson.GetNamedString("access_token", string.Empty);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new OAuthException(Localizer.Get("OAuth.NoAccessToken"));
            }

            return new OAuthToken
            {
                AccessToken = accessToken,
                RefreshToken = responseJson.GetNamedString("refresh_token", string.Empty),
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(responseJson.GetNamedNumber("expires_in", 3600))
            };
        }

        private OAuthDeviceCredentials GetValidatedCredentials()
        {
            string clientId = configuration.OAuthClientId;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new OAuthException(Localizer.Get("OAuth.ClientIdRequired"));
            }

            string clientSecret = configuration.GetOAuthClientSecret();
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new OAuthException(Localizer.Get("OAuth.ClientSecretRequired"));
            }

            DiagnosticLog.Write(
                "OAuth.Credentials",
                "Using "
                + configuration.GetOAuthDeviceCredentialSource()
                + " OAuth credentials (client ID length "
                + clientId.Length
                + ", client secret length "
                + clientSecret.Length
                + ").");
            return new OAuthDeviceCredentials(clientId, clientSecret);
        }

        public static void ClearStoredToken()
        {
            SecureCredentialStore.Delete(TokenAccessResource, CredentialUserName);
            SecureCredentialStore.Delete(TokenRefreshResource, CredentialUserName);
            SecureCredentialStore.Delete(TokenExpiryResource, CredentialUserName);
        }

        public static bool HasStoredToken()
        {
            return !string.IsNullOrWhiteSpace(SecureCredentialStore.Read(TokenAccessResource, CredentialUserName))
                && !string.IsNullOrWhiteSpace(SecureCredentialStore.Read(TokenExpiryResource, CredentialUserName));
        }

        private OAuthToken ReadToken()
        {
            string accessToken = SecureCredentialStore.Read(TokenAccessResource, CredentialUserName);
            string refreshToken = SecureCredentialStore.Read(TokenRefreshResource, CredentialUserName);
            string expiryValue = SecureCredentialStore.Read(TokenExpiryResource, CredentialUserName);
            if (string.IsNullOrWhiteSpace(accessToken)
                && string.IsNullOrWhiteSpace(refreshToken)
                && string.IsNullOrWhiteSpace(expiryValue))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(expiryValue))
            {
                throw new OAuthException(Localizer.Get("OAuth.StoredTokenMalformed"));
            }

            long expiresAt;
            if (!long.TryParse(expiryValue, out expiresAt))
            {
                throw new OAuthException(Localizer.Get("OAuth.StoredTokenExpirationMalformed"));
            }

            return new OAuthToken
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt)
            };
        }

        private static void SaveToken(OAuthToken token)
        {
            try
            {
                DiagnosticLog.Write("OAuth.TokenStore", "Saving access token entry.");
                SecureCredentialStore.Write(TokenAccessResource, CredentialUserName, token.AccessToken);
                DiagnosticLog.Write("OAuth.TokenStore", "Saving refresh token entry.");
                SecureCredentialStore.Write(TokenRefreshResource, CredentialUserName, token.RefreshToken ?? string.Empty);
                DiagnosticLog.Write("OAuth.TokenStore", "Saving token expiry entry.");
                SecureCredentialStore.Write(
                    TokenExpiryResource,
                    CredentialUserName,
                    token.ExpiresAt.ToUnixTimeSeconds().ToString());
            }
            catch (Exception exception)
            {
                DiagnosticLog.WriteException("OAuth.TokenStore", exception);
                throw new OAuthException(
                    Localizer.Format("OAuth.TokenSaveFailed", exception.HResult.ToString("X8")));
            }
        }

        private static JsonObject ParseJson(string content, string errorMessage)
        {
            JsonObject value;
            if (!JsonObject.TryParse(content, out value))
            {
                throw new OAuthException(errorMessage);
            }

            return value;
        }

        private sealed class DeviceTokenResponse
        {
            public DeviceTokenResponse(OAuthToken token, string error)
            {
                Token = token;
                Error = error;
            }

            public OAuthToken Token { get; private set; }

            public string Error { get; private set; }
        }

        private sealed class OAuthDeviceCredentials
        {
            public OAuthDeviceCredentials(string clientId, string clientSecret)
            {
                ClientId = clientId;
                ClientSecret = clientSecret;
            }

            public string ClientId { get; private set; }

            public string ClientSecret { get; private set; }
        }
    }

    public sealed class DeviceAuthorizationInfo
    {
        public DeviceAuthorizationInfo(
            string deviceCode,
            string userCode,
            Uri verificationUri,
            double expiresInSeconds,
            double pollIntervalSeconds)
        {
            DeviceCode = deviceCode;
            UserCode = userCode;
            VerificationUri = verificationUri;
            ExpiresInSeconds = Math.Max(1, (int)expiresInSeconds);
            PollIntervalSeconds = Math.Max(1, (int)pollIntervalSeconds);
        }

        internal string DeviceCode { get; private set; }

        public string UserCode { get; private set; }

        public Uri VerificationUri { get; private set; }

        internal int ExpiresInSeconds { get; private set; }

        internal int PollIntervalSeconds { get; private set; }
    }

    public sealed class DeviceAuthorizationProgress
    {
        public DeviceAuthorizationProgress(int secondsRemaining, int pollIntervalSeconds, string status)
        {
            SecondsRemaining = Math.Max(0, secondsRemaining);
            PollIntervalSeconds = Math.Max(1, pollIntervalSeconds);
            Status = status ?? string.Empty;
        }

        public int SecondsRemaining { get; private set; }

        public int PollIntervalSeconds { get; private set; }

        public string Status { get; private set; }
    }

    public sealed class OAuthToken
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }

    public class OAuthException : Exception
    {
        public OAuthException(string message)
            : base(message)
        {
        }
    }

    public sealed class DeviceAuthorizationExpiredException : OAuthException
    {
        public DeviceAuthorizationExpiredException()
            : base(Localizer.Get("OAuth.AuthorizationExpired"))
        {
        }
    }
}

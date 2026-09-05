using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.Data.Json;
using YouTube.Uwp.Models;

namespace YouTube.Uwp.Services
{
    public interface IYouTubeDataApiClient
    {
        Task<DataPage<VideoSummary>> SearchVideosAsync(string query, string pageToken, int maxResults);

        Task<DataPage<VideoSummary>> GetMostPopularVideosAsync(string regionCode, string pageToken, int maxResults);

        Task<DataPage<VideoSummary>> GetMostPopularVideosAsync(string regionCode, string categoryId, string pageToken, int maxResults);

        Task<IReadOnlyList<VideoCategory>> GetVideoCategoriesAsync(string regionCode);

        Task<IReadOnlyList<RegionOption>> GetSupportedRegionsAsync();

        Task<VideoDetails> GetVideoAsync(string videoId);

        Task<ChannelDetails> GetChannelAsync(string channelId);

        Task<ChannelDetails> GetMyChannelAsync();

        Task<DataPage<SubscriptionSummary>> GetSubscriptionsAsync(string pageToken, int maxResults);

        Task<DataPage<PlaylistSummary>> GetPlaylistsAsync(string pageToken, int maxResults);

        Task<DataPage<VideoSummary>> GetPlaylistVideosAsync(string playlistId, string pageToken, int maxResults);

        Task<DataPage<VideoSummary>> GetLikedVideosAsync(string pageToken, int maxResults);
    }

    public sealed class YouTubeDataApiClient : IYouTubeDataApiClient
    {
        private const string ApiBaseUrl = "https://www.googleapis.com/youtube/v3/";
        private readonly HttpClient httpClient;
        private readonly Func<string> apiKeyProvider;
        private readonly Func<Task<string>> accessTokenProvider;
        private readonly Func<bool> safeModeProvider;

        public YouTubeDataApiClient(Func<string> apiKeyProvider)
            : this(apiKeyProvider, null, null, new HttpClient())
        {
        }

        public YouTubeDataApiClient(Func<string> apiKeyProvider, Func<Task<string>> accessTokenProvider)
            : this(apiKeyProvider, accessTokenProvider, null, new HttpClient())
        {
        }

        public static YouTubeDataApiClient CreatePublicClient(
            Func<string> apiKeyProvider,
            Func<bool> safeModeProvider)
        {
            return new YouTubeDataApiClient(apiKeyProvider, null, safeModeProvider, new HttpClient());
        }

        public YouTubeDataApiClient(
            Func<string> apiKeyProvider,
            Func<Task<string>> accessTokenProvider,
            HttpClient httpClient)
            : this(apiKeyProvider, accessTokenProvider, null, httpClient)
        {
        }

        public YouTubeDataApiClient(
            Func<string> apiKeyProvider,
            Func<Task<string>> accessTokenProvider,
            Func<bool> safeModeProvider,
            HttpClient httpClient)
        {
            if (apiKeyProvider == null)
            {
                throw new ArgumentNullException("apiKeyProvider");
            }

            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            this.apiKeyProvider = apiKeyProvider;
            this.accessTokenProvider = accessTokenProvider;
            this.safeModeProvider = safeModeProvider;
            this.httpClient = httpClient;
        }

        public async Task<DataPage<VideoSummary>> SearchVideosAsync(string query, string pageToken, int maxResults)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Enter text to search for.", "query");
            }

            Dictionary<string, string> parameters = CreatePageParameters(pageToken, maxResults);
            parameters.Add("part", "snippet");
            parameters.Add("type", "video");
            parameters.Add("q", query.Trim());
            parameters.Add("safeSearch", safeModeProvider != null && safeModeProvider() ? "strict" : "moderate");
            parameters.Add("order", "relevance");

            JsonObject response = await GetPublicJsonAsync("search", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<VideoSummary> results = new List<VideoSummary>();

            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                JsonObject identifier = item.GetNamedObject("id", new JsonObject());
                string videoId = identifier.GetNamedString("videoId", string.Empty);
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    results.Add(MapVideoSummary(videoId, item.GetNamedObject("snippet", new JsonObject())));
                }
            }

            return new DataPage<VideoSummary>(results, response.GetNamedString("nextPageToken", string.Empty));
        }

        public Task<DataPage<VideoSummary>> GetMostPopularVideosAsync(string regionCode, string pageToken, int maxResults)
        {
            return GetMostPopularVideosAsync(regionCode, null, pageToken, maxResults);
        }

        public async Task<DataPage<VideoSummary>> GetMostPopularVideosAsync(string regionCode, string categoryId, string pageToken, int maxResults)
        {
            Dictionary<string, string> parameters = CreatePageParameters(pageToken, maxResults);
            parameters.Add("part", "snippet,contentDetails,statistics,status");
            parameters.Add("chart", "mostPopular");
            if (!string.IsNullOrWhiteSpace(regionCode))
            {
                parameters.Add("regionCode", regionCode.Trim().ToUpperInvariant());
            }

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                parameters.Add("videoCategoryId", categoryId.Trim());
            }

            JsonObject response = await GetPublicJsonAsync("videos", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<VideoSummary> results = new List<VideoSummary>();

            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                results.Add(MapVideoSummary(item.GetNamedString("id", string.Empty), item.GetNamedObject("snippet", new JsonObject())));
            }

            return new DataPage<VideoSummary>(results, response.GetNamedString("nextPageToken", string.Empty));
        }

        public async Task<IReadOnlyList<VideoCategory>> GetVideoCategoriesAsync(string regionCode)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("part", "snippet");
            if (!string.IsNullOrWhiteSpace(regionCode))
            {
                parameters.Add("regionCode", regionCode.Trim().ToUpperInvariant());
            }

            JsonObject response = await GetPublicJsonAsync("videoCategories", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<VideoCategory> categories = new List<VideoCategory>();

            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                JsonObject snippet = item.GetNamedObject("snippet", new JsonObject());
                string id = item.GetNamedString("id", string.Empty);
                string title = snippet.GetNamedString("title", string.Empty);
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title) && snippet.GetNamedBoolean("assignable", false))
                {
                    categories.Add(new VideoCategory
                    {
                        Id = id,
                        Title = title
                    });
                }
            }

            return categories;
        }

        public async Task<IReadOnlyList<RegionOption>> GetSupportedRegionsAsync()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("part", "snippet");

            JsonObject response = await GetPublicJsonAsync("i18nRegions", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<RegionOption> regions = new List<RegionOption>();

            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                string code = item.GetNamedString("id", string.Empty);
                string name = item.GetNamedObject("snippet", new JsonObject()).GetNamedString("name", string.Empty);
                if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
                {
                    regions.Add(new RegionOption
                    {
                        Code = code,
                        Name = name
                    });
                }
            }

            regions.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
            return regions;
        }

        public async Task<VideoDetails> GetVideoAsync(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException("A video ID is required.", "videoId");
            }

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("part", "snippet,contentDetails,statistics,status");
            parameters.Add("id", videoId);

            JsonObject response = await GetPublicJsonAsync("videos", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            if (items.Count == 0)
            {
                return null;
            }

            JsonObject item = items.GetObjectAt(0);
            JsonObject snippet = item.GetNamedObject("snippet", new JsonObject());
            JsonObject statistics = item.GetNamedObject("statistics", new JsonObject());
            JsonObject contentDetails = item.GetNamedObject("contentDetails", new JsonObject());
            JsonObject status = item.GetNamedObject("status", new JsonObject());
            VideoSummary summary = MapVideoSummary(item.GetNamedString("id", string.Empty), snippet);

            return new VideoDetails
            {
                Id = summary.Id,
                Title = summary.Title,
                Description = summary.Description,
                ChannelId = summary.ChannelId,
                ChannelTitle = summary.ChannelTitle,
                ThumbnailUrl = summary.ThumbnailUrl,
                PublishedAt = summary.PublishedAt,
                Duration = contentDetails.GetNamedString("duration", string.Empty),
                ViewCount = GetUnsignedNumber(statistics, "viewCount"),
                LikeCount = GetUnsignedNumber(statistics, "likeCount"),
                CommentCount = GetUnsignedNumber(statistics, "commentCount"),
                PrivacyStatus = status.GetNamedString("privacyStatus", string.Empty),
                Embeddable = status.GetNamedBoolean("embeddable", false)
            };
        }

        public async Task<ChannelDetails> GetChannelAsync(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw new ArgumentException("A channel ID is required.", "channelId");
            }

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("part", "snippet,contentDetails,statistics");
            parameters.Add("id", channelId);

            JsonObject response = await GetPublicJsonAsync("channels", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            if (items.Count == 0)
            {
                return null;
            }

            JsonObject item = items.GetObjectAt(0);
            return MapChannel(item);
        }

        public async Task<ChannelDetails> GetMyChannelAsync()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("part", "snippet,contentDetails,statistics");
            parameters.Add("mine", "true");

            JsonObject response = await GetAuthenticatedJsonAsync("channels", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            if (items.Count == 0)
            {
                return null;
            }

            return MapChannel(items.GetObjectAt(0));
        }

        public async Task<DataPage<SubscriptionSummary>> GetSubscriptionsAsync(string pageToken, int maxResults)
        {
            Dictionary<string, string> parameters = CreatePageParameters(pageToken, maxResults);
            parameters.Add("part", "snippet,contentDetails");
            parameters.Add("mine", "true");

            JsonObject response = await GetAuthenticatedJsonAsync("subscriptions", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<SubscriptionSummary> results = new List<SubscriptionSummary>();
            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                JsonObject snippet = item.GetNamedObject("snippet", new JsonObject());
                JsonObject resource = snippet.GetNamedObject("resourceId", new JsonObject());
                results.Add(new SubscriptionSummary
                {
                    Id = item.GetNamedString("id", string.Empty),
                    ChannelId = resource.GetNamedString("channelId", string.Empty),
                    ChannelTitle = snippet.GetNamedString("title", string.Empty),
                    Description = snippet.GetNamedString("description", string.Empty),
                    ThumbnailUrl = GetThumbnailUrl(snippet)
                });
            }

            return new DataPage<SubscriptionSummary>(results, response.GetNamedString("nextPageToken", string.Empty));
        }

        public async Task<DataPage<PlaylistSummary>> GetPlaylistsAsync(string pageToken, int maxResults)
        {
            Dictionary<string, string> parameters = CreatePageParameters(pageToken, maxResults);
            parameters.Add("part", "snippet,contentDetails");
            parameters.Add("mine", "true");

            JsonObject response = await GetAuthenticatedJsonAsync("playlists", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<PlaylistSummary> results = new List<PlaylistSummary>();
            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                JsonObject snippet = item.GetNamedObject("snippet", new JsonObject());
                JsonObject contentDetails = item.GetNamedObject("contentDetails", new JsonObject());
                results.Add(new PlaylistSummary
                {
                    Id = item.GetNamedString("id", string.Empty),
                    Title = snippet.GetNamedString("title", string.Empty),
                    Description = snippet.GetNamedString("description", string.Empty),
                    ThumbnailUrl = GetThumbnailUrl(snippet),
                    PublishedAt = ParseDate(snippet.GetNamedString("publishedAt", string.Empty)),
                    VideoCount = GetUnsignedNumber(contentDetails, "itemCount")
                });
            }

            return new DataPage<PlaylistSummary>(results, response.GetNamedString("nextPageToken", string.Empty));
        }

        public async Task<DataPage<VideoSummary>> GetPlaylistVideosAsync(
            string playlistId,
            string pageToken,
            int maxResults)
        {
            if (string.IsNullOrWhiteSpace(playlistId))
            {
                throw new ArgumentException("A playlist ID is required.", "playlistId");
            }

            Dictionary<string, string> parameters = CreatePageParameters(pageToken, maxResults);
            parameters.Add("part", "snippet,contentDetails");
            parameters.Add("playlistId", playlistId);

            JsonObject response = await GetAuthenticatedJsonAsync("playlistItems", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<VideoSummary> results = new List<VideoSummary>();
            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                JsonObject snippet = item.GetNamedObject("snippet", new JsonObject());
                JsonObject contentDetails = item.GetNamedObject("contentDetails", new JsonObject());
                JsonObject resource = snippet.GetNamedObject("resourceId", new JsonObject());
                string videoId = contentDetails.GetNamedString("videoId", string.Empty);
                if (string.IsNullOrWhiteSpace(videoId))
                {
                    videoId = resource.GetNamedString("videoId", string.Empty);
                }

                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    results.Add(MapVideoSummary(videoId, snippet));
                }
            }

            return new DataPage<VideoSummary>(results, response.GetNamedString("nextPageToken", string.Empty));
        }

        public async Task<DataPage<VideoSummary>> GetLikedVideosAsync(string pageToken, int maxResults)
        {
            Dictionary<string, string> parameters = CreatePageParameters(pageToken, maxResults);
            parameters.Add("part", "snippet,contentDetails,statistics,status");
            parameters.Add("myRating", "like");

            JsonObject response = await GetAuthenticatedJsonAsync("videos", parameters);
            JsonArray items = response.GetNamedArray("items", new JsonArray());
            List<VideoSummary> results = new List<VideoSummary>();
            for (int index = 0; index < items.Count; index++)
            {
                JsonObject item = items.GetObjectAt((uint)index);
                string videoId = item.GetNamedString("id", string.Empty);
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    results.Add(MapVideoSummary(videoId, item.GetNamedObject("snippet", new JsonObject())));
                }
            }

            return new DataPage<VideoSummary>(results, response.GetNamedString("nextPageToken", string.Empty));
        }

        private async Task<JsonObject> GetPublicJsonAsync(string resource, IDictionary<string, string> parameters)
        {
            string apiKey = apiKeyProvider();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Add a Google API key before using public YouTube Data API v3 features.");
            }

            parameters.Add("key", apiKey);
            Uri requestUri = BuildUri(resource, parameters);
            using (HttpResponseMessage response = await httpClient.GetAsync(requestUri))
            {
                string content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new YouTubeApiException(response.StatusCode, content);
                }

                JsonObject responseBody;
                if (!JsonObject.TryParse(content, out responseBody))
                {
                    throw new YouTubeApiResponseException("The YouTube Data API returned an invalid response. Check the network connection and try again.");
                }

                return responseBody;
            }
        }

        private async Task<JsonObject> GetAuthenticatedJsonAsync(
            string resource,
            IDictionary<string, string> parameters)
        {
            if (accessTokenProvider == null)
            {
                throw new OAuthException("Google account access is not configured. Sign in before opening Profile.");
            }

            string accessToken = await accessTokenProvider();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new OAuthException("Google authorization did not provide an access token. Sign in again.");
            }

            Uri requestUri = BuildUri(resource, parameters);
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using (HttpResponseMessage response = await httpClient.SendAsync(request))
                {
                    string content = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new YouTubeApiException(response.StatusCode, content);
                    }

                    JsonObject responseBody;
                    if (!JsonObject.TryParse(content, out responseBody))
                    {
                        throw new YouTubeApiResponseException("The authenticated YouTube Data API returned an invalid response. Check the network connection and try again.");
                    }

                    return responseBody;
                }
            }
        }

        private static Dictionary<string, string> CreatePageParameters(string pageToken, int maxResults)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("maxResults", Math.Max(1, Math.Min(maxResults, 50)).ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                parameters.Add("pageToken", pageToken);
            }

            return parameters;
        }

        private static Uri BuildUri(string resource, IDictionary<string, string> parameters)
        {
            List<string> queryValues = new List<string>();
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                queryValues.Add(Uri.EscapeDataString(parameter.Key) + "=" + Uri.EscapeDataString(parameter.Value ?? string.Empty));
            }

            return new Uri(ApiBaseUrl + resource + "?" + string.Join("&", queryValues));
        }

        private static VideoSummary MapVideoSummary(string id, JsonObject snippet)
        {
            return new VideoSummary
            {
                Id = id,
                Title = snippet.GetNamedString("title", string.Empty),
                Description = snippet.GetNamedString("description", string.Empty),
                ChannelId = snippet.GetNamedString("channelId", string.Empty),
                ChannelTitle = snippet.GetNamedString("channelTitle", string.Empty),
                ThumbnailUrl = GetThumbnailUrl(snippet),
                PublishedAt = ParseDate(snippet.GetNamedString("publishedAt", string.Empty))
            };
        }

        private static ChannelDetails MapChannel(JsonObject item)
        {
            JsonObject snippet = item.GetNamedObject("snippet", new JsonObject());
            JsonObject statistics = item.GetNamedObject("statistics", new JsonObject());
            JsonObject contentDetails = item.GetNamedObject("contentDetails", new JsonObject());
            JsonObject playlists = contentDetails.GetNamedObject("relatedPlaylists", new JsonObject());

            return new ChannelDetails
            {
                Id = item.GetNamedString("id", string.Empty),
                Title = snippet.GetNamedString("title", string.Empty),
                Description = snippet.GetNamedString("description", string.Empty),
                CustomUrl = snippet.GetNamedString("customUrl", string.Empty),
                ThumbnailUrl = GetThumbnailUrl(snippet),
                PublishedAt = ParseDate(snippet.GetNamedString("publishedAt", string.Empty)),
                UploadsPlaylistId = playlists.GetNamedString("uploads", string.Empty),
                ViewCount = GetUnsignedNumber(statistics, "viewCount"),
                SubscriberCount = GetUnsignedNumber(statistics, "subscriberCount"),
                VideoCount = GetUnsignedNumber(statistics, "videoCount")
            };
        }

        private static string GetThumbnailUrl(JsonObject snippet)
        {
            JsonObject thumbnails = snippet.GetNamedObject("thumbnails", new JsonObject());
            string[] preferredSizes = { "maxres", "standard", "high", "medium", "default" };
            foreach (string size in preferredSizes)
            {
                if (!thumbnails.ContainsKey(size))
                {
                    continue;
                }

                JsonObject thumbnail = thumbnails.GetNamedObject(size);
                string url = thumbnail.GetNamedString("url", string.Empty);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }

            return string.Empty;
        }

        private static DateTimeOffset? ParseDate(string value)
        {
            DateTimeOffset parsed;
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed)
                ? parsed
                : (DateTimeOffset?)null;
        }

        private static ulong GetUnsignedNumber(JsonObject source, string name)
        {
            JsonValue value = source.GetNamedValue(name, null);
            if (value == null || value.ValueType == JsonValueType.Null)
            {
                return 0;
            }

            if (value.ValueType == JsonValueType.Number)
            {
                return value.GetNumber() < 0
                    ? 0
                    : (ulong)value.GetNumber();
            }

            ulong result;
            return value.ValueType == JsonValueType.String
                && ulong.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }
    }

    public sealed class YouTubeApiException : Exception
    {
        public YouTubeApiException(HttpStatusCode statusCode, string responseBody)
            : base("YouTube Data API v3 returned " + ((int)statusCode).ToString(CultureInfo.InvariantCulture) + " (" + statusCode + "). " + responseBody)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; private set; }
    }

    public sealed class YouTubeApiResponseException : Exception
    {
        public YouTubeApiResponseException(string message)
            : base(message)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace YouTube.Uwp.Services
{
    public interface IYouTubeUploadClient
    {
        Task<VideoUploadResult> UploadAsync(
            VideoUploadRequest request,
            IProgress<VideoUploadProgress> progress,
            CancellationToken cancellationToken);
    }

    public sealed class YouTubeResumableUploadClient : IYouTubeUploadClient
    {
        private const string ResumableUploadEndpoint =
            "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet%2Cstatus";
        private const int ChunkSize = 256 * 1024;
        private readonly Func<Task<string>> accessTokenProvider;
        private readonly HttpClient httpClient;

        public YouTubeResumableUploadClient(Func<Task<string>> accessTokenProvider)
            : this(accessTokenProvider, new HttpClient())
        {
        }

        public YouTubeResumableUploadClient(Func<Task<string>> accessTokenProvider, HttpClient httpClient)
        {
            if (accessTokenProvider == null)
            {
                throw new ArgumentNullException("accessTokenProvider");
            }

            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            this.accessTokenProvider = accessTokenProvider;
            this.httpClient = httpClient;
        }

        public async Task<VideoUploadResult> UploadAsync(
            VideoUploadRequest request,
            IProgress<VideoUploadProgress> progress,
            CancellationToken cancellationToken)
        {
            ValidateRequest(request);
            cancellationToken.ThrowIfCancellationRequested();

            BasicProperties properties = await request.File.GetBasicPropertiesAsync();
            if (properties.Size == 0)
            {
                throw new ArgumentException(Localizer.Get("Upload.FileContainsNoData"), "request");
            }

            if (properties.Size > long.MaxValue)
            {
                throw new ArgumentException(Localizer.Get("Upload.FileTooLarge"), "request");
            }

            string accessToken = await accessTokenProvider();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new OAuthException(Localizer.Get("DataApi.AccessTokenMissing"));
            }

            string contentType = GetContentType(request.File);
            Uri uploadUri = await CreateResumableSessionAsync(request, properties.Size, contentType, accessToken, cancellationToken);
            progress.Report(new VideoUploadProgress(0, properties.Size));

            ulong uploaded = 0;
            using (IRandomAccessStream stream = await request.File.OpenReadAsync())
            {
                while (uploaded < properties.Size)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    uint bytesToRead = (uint)Math.Min((ulong)ChunkSize, properties.Size - uploaded);
                    byte[] bytes = await ReadChunkAsync(stream, uploaded, bytesToRead);
                    if (bytes.Length == 0)
                    {
                        throw new YouTubeUploadException(Localizer.Get("Upload.FileEndedEarly"));
                    }

                    ulong end = uploaded + (uint)bytes.Length - 1;
                    ulong nextUploaded;
                    using (HttpResponseMessage response = await SendChunkAsync(
                        uploadUri,
                        bytes,
                        uploaded,
                        end,
                        properties.Size,
                        contentType,
                        accessToken,
                        cancellationToken))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string body = await response.Content.ReadAsStringAsync();
                            progress.Report(new VideoUploadProgress(properties.Size, properties.Size));
                            return new VideoUploadResult(GetVideoId(body));
                        }

                        if ((int)response.StatusCode != 308)
                        {
                            throw await CreateUploadExceptionAsync(response);
                        }

                        nextUploaded = GetNextUploadOffset(response, end + 1);
                    }

                    uploaded = nextUploaded;
                    progress.Report(new VideoUploadProgress(uploaded, properties.Size));
                }
            }

            throw new YouTubeUploadException(Localizer.Get("Upload.NoCompletedResponse"));
        }

        private async Task<Uri> CreateResumableSessionAsync(
            VideoUploadRequest request,
            ulong contentLength,
            string contentType,
            string accessToken,
            CancellationToken cancellationToken)
        {
            using (HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, ResumableUploadEndpoint))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                message.Headers.Add("X-Upload-Content-Length", contentLength.ToString(CultureInfo.InvariantCulture));
                message.Headers.Add("X-Upload-Content-Type", contentType);
                message.Content = new StringContent(CreateMetadataJson(request), Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw await CreateUploadExceptionAsync(response);
                    }

                    Uri location;
                    if (response.Headers.Location == null
                        || !Uri.TryCreate(response.Headers.Location.ToString(), UriKind.Absolute, out location))
                    {
                        throw new YouTubeUploadException(Localizer.Get("Upload.NoResumableLocation"));
                    }

                    return location;
                }
            }
        }

        private async Task<HttpResponseMessage> SendChunkAsync(
            Uri uploadUri,
            byte[] bytes,
            ulong start,
            ulong end,
            ulong total,
            string contentType,
            string accessToken,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Put, uploadUri);
            ByteArrayContent content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Headers.ContentRange = new ContentRangeHeaderValue(
                (long)start,
                (long)end,
                (long)total);

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            message.Content = content;
            return await httpClient.SendAsync(message, cancellationToken);
        }

        private static async Task<byte[]> ReadChunkAsync(IRandomAccessStream stream, ulong offset, uint count)
        {
            using (IInputStream input = stream.GetInputStreamAt(offset))
            {
                using (DataReader reader = new DataReader(input))
                {
                    uint loaded = await reader.LoadAsync(count);
                    byte[] bytes = new byte[loaded];
                    reader.ReadBytes(bytes);
                    return bytes;
                }
            }
        }

        private static string CreateMetadataJson(VideoUploadRequest request)
        {
            JsonObject snippet = new JsonObject();
            snippet.SetNamedValue("title", JsonValue.CreateStringValue(request.Title.Trim()));
            snippet.SetNamedValue("description", JsonValue.CreateStringValue((request.Description ?? string.Empty).Trim()));

            JsonObject status = new JsonObject();
            status.SetNamedValue("privacyStatus", JsonValue.CreateStringValue(request.PrivacyStatus));

            JsonObject metadata = new JsonObject();
            metadata.SetNamedValue("snippet", snippet);
            metadata.SetNamedValue("status", status);
            return metadata.Stringify();
        }

        private static async Task<YouTubeUploadException> CreateUploadExceptionAsync(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync();
            return new YouTubeUploadException(
                Localizer.Format("Upload.ResponseError", (int)response.StatusCode, response.StatusCode, body));
        }

        private static ulong GetNextUploadOffset(HttpResponseMessage response, ulong defaultOffset)
        {
            IEnumerable<string> rangeHeaders;
            if (!response.Headers.TryGetValues("Range", out rangeHeaders))
            {
                return defaultOffset;
            }

            foreach (string rangeHeader in rangeHeaders)
            {
                int dash = rangeHeader.LastIndexOf('-');
                ulong lastByte;
                if (dash >= 0
                    && ulong.TryParse(rangeHeader.Substring(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out lastByte))
                {
                    return lastByte + 1;
                }
            }

            return defaultOffset;
        }

        private static string GetVideoId(string responseBody)
        {
            JsonObject response;
            return JsonObject.TryParse(responseBody, out response)
                ? response.GetNamedString("id", string.Empty)
                : string.Empty;
        }

        private static string GetContentType(StorageFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.ContentType)
                && file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return file.ContentType;
            }

            switch (file.FileType.ToLowerInvariant())
            {
                case ".mp4":
                    return "video/mp4";
                case ".wmv":
                    return "video/x-ms-wmv";
                case ".mov":
                    return "video/quicktime";
                case ".avi":
                    return "video/x-msvideo";
                case ".mkv":
                    return "video/x-matroska";
                default:
                    throw new ArgumentException(Localizer.Get("Upload.UnrecognizedFile"), "file");
            }
        }

        private static void ValidateRequest(VideoUploadRequest request)
        {
            if (request == null || request.File == null)
            {
                throw new ArgumentException(Localizer.Get("Upload.FileRequired"), "request");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException(Localizer.Get("Upload.TitleRequired"), "request");
            }

            if (request.Title.Trim().Length > 100)
            {
                throw new ArgumentException(Localizer.Get("Upload.TitleTooLong"), "request");
            }

            if (!string.IsNullOrEmpty(request.Description) && request.Description.Trim().Length > 5000)
            {
                throw new ArgumentException(Localizer.Get("Upload.DescriptionTooLong"), "request");
            }

            if (string.IsNullOrWhiteSpace(request.PrivacyStatus)
                || (request.PrivacyStatus != "private"
                    && request.PrivacyStatus != "unlisted"
                    && request.PrivacyStatus != "public"))
            {
                throw new ArgumentException(Localizer.Get("Upload.PrivacyRequired"), "request");
            }
        }
    }

    public sealed class VideoUploadRequest
    {
        public StorageFile File { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string PrivacyStatus { get; set; }
    }

    public sealed class VideoUploadProgress
    {
        public VideoUploadProgress(ulong bytesUploaded, ulong totalBytes)
        {
            BytesUploaded = bytesUploaded;
            TotalBytes = totalBytes;
        }

        public ulong BytesUploaded { get; private set; }

        public ulong TotalBytes { get; private set; }

        public double Percentage
        {
            get
            {
                return TotalBytes == 0
                    ? 0
                    : (double)BytesUploaded * 100 / TotalBytes;
            }
        }
    }

    public sealed class VideoUploadResult
    {
        public VideoUploadResult(string videoId)
        {
            VideoId = videoId;
        }

        public string VideoId { get; private set; }
    }

    public sealed class YouTubeUploadException : Exception
    {
        public YouTubeUploadException(string message)
            : base(message)
        {
        }
    }
}

using System;
using Windows.Storage;
using YouTube.Uwp.Models;

namespace YouTube.Uwp.Services
{
    // Public video metadata is retained locally only to restore the live-tile queue.
    internal static class LiveTileStateStore
    {
        private const string LastPlayedPrefix = "LiveTile.LastPlayed.";
        private const string TrendingPrefix = "LiveTile.Trending.";
        private const string TrendingRegionKey = "LiveTile.Trending.Region";

        public static void SaveLastPlayed(VideoSummary video)
        {
            SaveVideo(LastPlayedPrefix, video);
        }

        public static VideoSummary GetLastPlayed()
        {
            return GetVideo(LastPlayedPrefix);
        }

        public static void SaveTrending(VideoSummary video, string regionCode)
        {
            SaveVideo(TrendingPrefix, video);
            ApplicationData.Current.LocalSettings.Values[TrendingRegionKey] = NormalizeRegion(regionCode);
        }

        public static VideoSummary GetTrending()
        {
            return GetVideo(TrendingPrefix);
        }

        public static string GetTrendingRegion()
        {
            return ReadValue(TrendingRegionKey);
        }

        private static void SaveVideo(string prefix, VideoSummary video)
        {
            if (video == null || string.IsNullOrWhiteSpace(video.Id) || string.IsNullOrWhiteSpace(video.Title))
            {
                throw new ArgumentException(Localizer.Get("LiveTile.MetadataRequired"), "video");
            }

            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            settings.Values[prefix + "Id"] = video.Id.Trim();
            settings.Values[prefix + "Title"] = video.Title.Trim();
            settings.Values[prefix + "ChannelTitle"] = video.ChannelTitle ?? string.Empty;
            settings.Values[prefix + "ThumbnailUrl"] = video.ThumbnailUrl ?? string.Empty;
        }

        private static VideoSummary GetVideo(string prefix)
        {
            string id = ReadValue(prefix + "Id");
            string title = ReadValue(prefix + "Title");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            return new VideoSummary
            {
                Id = id,
                Title = title,
                ChannelTitle = ReadValue(prefix + "ChannelTitle"),
                ThumbnailUrl = ReadValue(prefix + "ThumbnailUrl")
            };
        }

        private static string ReadValue(string key)
        {
            object value;
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value)
                ? value as string
                : null;
        }

        private static string NormalizeRegion(string regionCode)
        {
            return string.IsNullOrWhiteSpace(regionCode)
                ? string.Empty
                : regionCode.Trim().ToUpperInvariant();
        }
    }
}

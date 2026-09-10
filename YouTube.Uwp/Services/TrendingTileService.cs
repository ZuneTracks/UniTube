using System;
using System.Net;
using System.Text;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using YouTube.Uwp.Models;

namespace YouTube.Uwp.Services
{
    // Keeps the rotating live-tile queue separate from the page and API flows.
    public sealed class TrendingTileService
    {
        private const string LastPlayedTag = "YourTube.LiveTile.LastPlayed";
        private const string TrendingTag = "YourTube.LiveTile.Trending";
        private const string BrandTag = "YourTube.LiveTile.Brand";

        public void Update(VideoSummary video, string regionCode)
        {
            if (video == null)
            {
                throw new ArgumentNullException("video");
            }

            if (string.IsNullOrWhiteSpace(video.Title))
            {
                throw new ArgumentException(Localizer.Get("LiveTile.VideoTitleRequired"), "video");
            }

            LiveTileStateStore.SaveTrending(video, regionCode);
            RefreshQueue();
        }

        public void UpdateLastPlayed(VideoSummary video)
        {
            LiveTileStateStore.SaveLastPlayed(video);
            RefreshQueue();
        }

        private static void RefreshQueue()
        {
            TileUpdater updater = TileUpdateManager.CreateTileUpdaterForApplication();
            updater.EnableNotificationQueue(true);
            updater.Clear();

            VideoSummary lastPlayed = LiveTileStateStore.GetLastPlayed();
            if (lastPlayed != null)
            {
                AddNotification(updater, CreateVideoTileXml(lastPlayed, Localizer.Get("LiveTile.LastPlayed")), LastPlayedTag);
            }

            VideoSummary trending = LiveTileStateStore.GetTrending();
            if (trending != null)
            {
                AddNotification(updater, CreateVideoTileXml(trending, CreateTrendingHeading(LiveTileStateStore.GetTrendingRegion())), TrendingTag);
            }

            if (lastPlayed != null || trending != null)
            {
                AddNotification(updater, CreateBrandTileXml(), BrandTag);
            }
        }

        private static void AddNotification(TileUpdater updater, string xml, string tag)
        {
            XmlDocument tileXml = new XmlDocument();
            tileXml.LoadXml(xml);

            TileNotification notification = new TileNotification(tileXml);
            notification.Tag = tag;
            updater.Update(notification);
        }

        private static string CreateVideoTileXml(VideoSummary video, string heading)
        {
            string title = Encode(TrimForTile(video.Title, 96));
            string channel = Encode(TrimForTile(video.ChannelTitle, 48));
            string encodedHeading = Encode(heading);
            string image = CreateImageElement(video.ThumbnailUrl);

            StringBuilder tile = new StringBuilder();
            tile.Append("<tile><visual>");
            AppendBinding(tile, "TileMedium", encodedHeading, title, channel, image);
            AppendBinding(tile, "TileWide", encodedHeading, title, channel, image);
            tile.Append("</visual></tile>");
            return tile.ToString();
        }

        private static string CreateBrandTileXml()
        {
            return "<tile><visual>"
                + "<binding template=\"TileMedium\" branding=\"name\"><image placement=\"background\" hint-overlay=\"0\" src=\"ms-appx:///Assets/Square150x150Logo.png\" /></binding>"
                + "<binding template=\"TileWide\" branding=\"name\"><image placement=\"background\" hint-overlay=\"0\" src=\"ms-appx:///Assets/Wide310x150Logo.png\" /></binding>"
                + "</visual></tile>";
        }

        private static void AppendBinding(StringBuilder tile, string template, string heading, string title, string channel, string image)
        {
            tile.Append("<binding template=\"").Append(template).Append("\" branding=\"name\">");
            tile.Append(image);
            tile.Append("<text hint-style=\"subtitle\">").Append(heading).Append("</text>");
            tile.Append("<text hint-style=\"captionSubtle\" hint-wrap=\"true\">").Append(title).Append("</text>");
            if (!string.IsNullOrWhiteSpace(channel))
            {
                tile.Append("<text hint-style=\"captionSubtle\">").Append(channel).Append("</text>");
            }

            tile.Append("</binding>");
        }

        private static string CreateImageElement(string thumbnailUrl)
        {
            Uri thumbnailUri;
            if (!Uri.TryCreate(thumbnailUrl, UriKind.Absolute, out thumbnailUri) ||
                (thumbnailUri.Scheme != "http" && thumbnailUri.Scheme != "https"))
            {
                return string.Empty;
            }

            return "<image placement=\"peek\" hint-overlay=\"28\" src=\"" + Encode(thumbnailUri.AbsoluteUri) + "\" />";
        }

        private static string CreateTrendingHeading(string regionCode)
        {
            string region = string.IsNullOrWhiteSpace(regionCode)
                ? string.Empty
                : regionCode.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(region)
                ? Localizer.Get("LiveTile.TrendingNow")
                : Localizer.Format("LiveTile.TrendingNowRegion", region);
        }

        private static string TrimForTile(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            return trimmed.Length <= maximumLength
                ? trimmed
                : trimmed.Substring(0, maximumLength - 3) + "...";
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}

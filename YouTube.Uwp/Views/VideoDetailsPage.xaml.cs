using System;
using System.Globalization;
using System.Net.Http;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.System;
using YouTube.Uwp.Models;
using YouTube.Uwp.Services;

namespace YouTube.Uwp.Views
{
    public sealed partial class VideoDetailsPage : Page
    {
        private readonly YouTubeDataApiClient client;
        private readonly TrendingTileService trendingTileService;
        private VideoDetails video;

        public VideoDetailsPage()
        {
            InitializeComponent();
            client = new YouTubeDataApiClient(App.Configuration.GetApiKey);
            trendingTileService = new TrendingTileService();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            string videoId = e.Parameter as string;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                StatusText.Text = Localizer.Get("VideoDetails.VideoIdRequired");
                return;
            }

            try
            {
                StatusText.Text = Localizer.Get("VideoDetails.Loading");
                video = await client.GetVideoAsync(videoId);
                if (video == null)
                {
                    StatusText.Text = Localizer.Get("VideoDetails.Unavailable");
                    return;
                }

                TitleText.Text = video.Title;
                ChannelText.Text = video.ChannelTitle;
                MetadataText.Text = Localizer.Format(
                    "VideoDetails.Metadata",
                    video.PublishedAt.HasValue
                        ? video.PublishedAt.Value.ToString("g", CultureInfo.CurrentCulture)
                        : Localizer.Get("Common.Unknown"),
                    video.ViewCount,
                    video.Duration);
                DescriptionText.Text = video.Description;
                if (!string.IsNullOrWhiteSpace(video.ThumbnailUrl))
                {
                    ThumbnailImage.Source = new BitmapImage(new Uri(video.ThumbnailUrl));
                }

                PlayInAppButton.IsEnabled = true;
                PlaybackStatusText.Text = Localizer.Get("VideoDetails.PlayInAppStatus");
                StatusText.Text = string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                StatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                StatusText.Text = exception.Message;
            }
            catch (HttpRequestException)
            {
                StatusText.Text = Localizer.Get("Common.ApiNetworkFailure");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void OpenChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (video != null && !string.IsNullOrWhiteSpace(video.ChannelId))
            {
                Frame.Navigate(typeof(ChannelDetailsPage), video.ChannelId);
            }
        }

        private void PlayInAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (video != null && !string.IsNullOrWhiteSpace(video.Id))
            {
                UpdateLastPlayedTile();
                Frame.Navigate(typeof(VideoPlayerPage), video.Id);
            }
        }

        private async void WatchOnYouTubeButton_Click(object sender, RoutedEventArgs e)
        {
            if (video != null && !string.IsNullOrWhiteSpace(video.Id))
            {
                UpdateLastPlayedTile();
                await Launcher.LaunchUriAsync(new Uri("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(video.Id)));
            }
        }

        private void UpdateLastPlayedTile()
        {
            try
            {
                trendingTileService.UpdateLastPlayed(video);
            }
            catch (UnauthorizedAccessException)
            {
                PlaybackStatusText.Text = Localizer.Get("VideoDetails.TilePermissionFailure");
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                PlaybackStatusText.Text = Localizer.Get("VideoDetails.TileUpdateFailure");
            }
            catch (ArgumentException)
            {
                PlaybackStatusText.Text = Localizer.Get("VideoDetails.TileMetadataFailure");
            }
        }
    }
}

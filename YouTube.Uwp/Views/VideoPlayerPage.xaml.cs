using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using YouTube.Uwp.Services;

namespace YouTube.Uwp.Views
{
    public sealed partial class VideoPlayerPage : Page
    {
        private string videoId;

        public VideoPlayerPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            videoId = e.Parameter as string;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                StatusText.Text = Localizer.Get("Player.VideoIdRequired");
                return;
            }

            StatusText.Text = Localizer.Get("Player.Loading");
            PlayerWebView.Navigate(CreateMobileWatchUri(videoId));
            base.OnNavigatedTo(e);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void PlayerWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            StatusText.Text = args.IsSuccess
                ? string.Empty
                : Localizer.Get("Player.LoadFailed");
        }

        private void PlayerWebView_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            StatusText.Text = Localizer.Get("Player.LoadFailed");
        }

        private async void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(videoId))
            {
                await Launcher.LaunchUriAsync(CreateWatchUri(videoId));
            }
        }

        private static Uri CreateMobileWatchUri(string id)
        {
            return new Uri("https://m.youtube.com/watch?v=" + Uri.EscapeDataString(id));
        }

        private static Uri CreateWatchUri(string id)
        {
            return new Uri("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id));
        }
    }
}

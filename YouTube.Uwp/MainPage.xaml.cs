using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using YouTube.Uwp.Models;
using YouTube.Uwp.Services;
using YouTube.Uwp.Views;

namespace YouTube.Uwp
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private readonly YouTubeDataApiClient client;
        private readonly YouTubeDataApiClient authenticatedClient;
        private readonly TrendingTileService trendingTileService;
        private bool profileLoaded;
        private bool profileRequestInProgress;
        private string subscriptionsNextPageToken;
        private string playlistsNextPageToken;
        private string playlistVideosNextPageToken;
        private string uploadedVideosNextPageToken;
        private string likedVideosNextPageToken;
        private string selectedPlaylistId;
        private string uploadsPlaylistId;
        private string profileLoadStage;
        private string selectedRegionCode;

        public MainPage()
        {
            InitializeComponent();
            Results = new ObservableCollection<VideoSummary>();
            Categories = new ObservableCollection<VideoCategory>();
            Regions = new ObservableCollection<RegionOption>
            {
                new RegionOption { Code = "US", Name = Localizer.Get("Regions.UnitedStates") }
            };
            Subscriptions = new ObservableCollection<SubscriptionSummary>();
            Playlists = new ObservableCollection<PlaylistSummary>();
            PlaylistVideos = new ObservableCollection<VideoSummary>();
            UploadedVideos = new ObservableCollection<VideoSummary>();
            LikedVideos = new ObservableCollection<VideoSummary>();
            DataContext = this;
            SelectedRegionCode = GetHomeRegionCode();
            client = YouTubeDataApiClient.CreatePublicClient(
                App.Configuration.GetApiKey,
                () => App.Configuration.IsSafeModeEnabled);
            OAuthDeviceAuthorizationService oauthService = new OAuthDeviceAuthorizationService(App.Configuration);
            authenticatedClient = new YouTubeDataApiClient(App.Configuration.GetApiKey, oauthService.GetValidAccessTokenAsync);
            trendingTileService = new TrendingTileService();
            Loaded += MainPage_Loaded;
        }

        public ObservableCollection<VideoSummary> Results { get; private set; }

        public ObservableCollection<VideoCategory> Categories { get; private set; }

        public ObservableCollection<RegionOption> Regions { get; private set; }

        public string SelectedRegionCode
        {
            get { return selectedRegionCode; }
            set
            {
                if (string.Equals(selectedRegionCode, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                selectedRegionCode = value;
                OnPropertyChanged("SelectedRegionCode");
            }
        }

        public ObservableCollection<SubscriptionSummary> Subscriptions { get; private set; }

        public ObservableCollection<PlaylistSummary> Playlists { get; private set; }

        public ObservableCollection<VideoSummary> PlaylistVideos { get; private set; }

        public ObservableCollection<VideoSummary> UploadedVideos { get; private set; }

        public ObservableCollection<VideoSummary> LikedVideos { get; private set; }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PublicStatusText.Text = Localizer.Get("Search.Loading");
                DataPage<VideoSummary> page = await client.SearchVideosAsync(SearchBox.Text, null, 25);
                ReplaceResults(page);
                PublicStatusText.Text = Localizer.Format("Search.Results", Results.Count);
            }
            catch (ArgumentException exception)
            {
                PublicStatusText.Text = exception.Message;
            }
            catch (InvalidOperationException exception)
            {
                PublicStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                PublicStatusText.Text = exception.Message;
            }
            catch (YouTubeApiResponseException exception)
            {
                PublicStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                PublicStatusText.Text = Localizer.Get("Common.ApiTimedOut");
            }
            catch (HttpRequestException)
            {
                PublicStatusText.Text = Localizer.Get("Common.ApiNetworkFailure");
            }
        }

        private async void PopularButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PublicStatusText.Text = Localizer.Get("Popular.Loading");
                HomeStatusText.Text = Localizer.Get("Popular.Loading");
                DataPage<VideoSummary> page = await client.GetMostPopularVideosAsync(GetSelectedRegionCode(), null, 25);
                ReplaceResults(page);
                PublicStatusText.Text = Localizer.Format("Popular.Results", Results.Count);
                HomeStatusText.Text = Localizer.Format("Popular.Results", Results.Count);
                UpdateTrendingTile(page);
            }
            catch (InvalidOperationException exception)
            {
                PublicStatusText.Text = exception.Message;
                HomeStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                PublicStatusText.Text = exception.Message;
                HomeStatusText.Text = exception.Message;
            }
            catch (YouTubeApiResponseException exception)
            {
                PublicStatusText.Text = exception.Message;
                HomeStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                PublicStatusText.Text = Localizer.Get("Common.ApiTimedOut");
                HomeStatusText.Text = PublicStatusText.Text;
            }
            catch (HttpRequestException)
            {
                PublicStatusText.Text = Localizer.Get("Common.ApiNetworkFailure");
                HomeStatusText.Text = PublicStatusText.Text;
            }
        }

        private void UpdateTrendingTile(DataPage<VideoSummary> page)
        {
            if (page.Items.Count == 0)
            {
                return;
            }

            try
            {
                trendingTileService.Update(page.Items[0], GetSelectedRegionCode());
                HomeStatusText.Text = Localizer.Format("Popular.TileUpdated", Results.Count);
            }
            catch (UnauthorizedAccessException)
            {
                HomeStatusText.Text = Localizer.Format("Popular.TilePermissionFailure", Results.Count);
            }
            catch (COMException)
            {
                HomeStatusText.Text = Localizer.Format("Popular.TileUpdateFailure", Results.Count);
            }
        }

        private void ShowSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void ShowUploadButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(UploadVideoPage));
        }

        private async void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePivotHeaders();
            if (MainPivot.SelectedIndex == 2 && Categories.Count == 0)
            {
                await LoadCategoriesAsync();
            }

            if (MainPivot.SelectedIndex == 3 && !profileLoaded)
            {
                try
                {
                    await LoadProfileAsync();
                }
                catch (Exception exception)
                {
                    ShowProfileFailure("pivot", exception, ProfileStatusText);
                }
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_Loaded;
            await LoadSupportedRegionsAsync();
        }

        private async Task LoadSupportedRegionsAsync()
        {
            try
            {
                RegionStatusText.Text = Localizer.Get("Regions.Loading");
                IReadOnlyList<RegionOption> supportedRegions = await client.GetSupportedRegionsAsync();
                string homeRegionCode = GetHomeRegionCode();

                if (supportedRegions.Count == 0)
                {
                    RegionStatusText.Text = Localizer.Get("Regions.NoneReturned");
                    return;
                }

                Regions.Clear();
                foreach (RegionOption region in supportedRegions)
                {
                    Regions.Add(region);
                }

                RegionOption selectedRegion = FindRegion(SelectedRegionCode)
                    ?? FindRegion(homeRegionCode)
                    ?? FindRegion("US");
                if (selectedRegion != null)
                {
                    Regions.Remove(selectedRegion);
                    Regions.Insert(0, selectedRegion);
                    SelectedRegionCode = null;
                    SelectedRegionCode = selectedRegion.Code;
                }

                RegionStatusText.Text = string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                RegionStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                RegionStatusText.Text = exception.Message;
            }
            catch (YouTubeApiResponseException exception)
            {
                RegionStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                RegionStatusText.Text = Localizer.Get("Regions.LoadTimedOut");
            }
            catch (HttpRequestException)
            {
                RegionStatusText.Text = Localizer.Get("Regions.LoadNetworkFailure");
            }
        }

        private async void RegionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RegionOption selectedRegion = e.AddedItems.Count == 0
                ? null
                : e.AddedItems[0] as RegionOption;
            if (selectedRegion != null)
            {
                SelectedRegionCode = selectedRegion.Code;
            }

            Categories.Clear();
            if (MainPivot.SelectedIndex == 2)
            {
                await LoadCategoriesAsync();
            }
        }

        private async void RefreshProfileButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadProfileAsync();
        }

        private async Task LoadProfileAsync()
        {
            if (profileRequestInProgress)
            {
                return;
            }

            profileRequestInProgress = true;
            profileLoaded = false;
            profileLoadStage = "starting";
            subscriptionsNextPageToken = null;
            playlistsNextPageToken = null;
            playlistVideosNextPageToken = null;
            uploadedVideosNextPageToken = null;
            likedVideosNextPageToken = null;
            selectedPlaylistId = null;
            uploadsPlaylistId = null;
            Subscriptions.Clear();
            Playlists.Clear();
            PlaylistVideos.Clear();
            UploadedVideos.Clear();
            LikedVideos.Clear();
            ProfileTitleText.Text = string.Empty;
            ProfileMetadataText.Text = string.Empty;
            ProfileDescriptionText.Text = string.Empty;
            SelectedPlaylistText.Text = Localizer.Get("PlaylistVideosHeading.Text");
            ProfileStatusText.Text = Localizer.Get("Profile.Loading");
            SubscriptionsStatusText.Text = string.Empty;
            PlaylistsStatusText.Text = string.Empty;
            PlaylistVideosStatusText.Text = string.Empty;
            UploadedVideosStatusText.Text = string.Empty;
            LikedVideosStatusText.Text = string.Empty;
            UpdateProfileControls();

            try
            {
                profileLoadStage = "channel";
                ChannelDetails channel = await authenticatedClient.GetMyChannelAsync();
                if (channel == null)
                {
                    ProfileStatusText.Text = Localizer.Get("Profile.NoChannel");
                    return;
                }

                ProfileTitleText.Text = channel.Title;
                ProfileMetadataText.Text = Localizer.Format(
                    "Profile.Metadata",
                    channel.SubscriberCount,
                    channel.VideoCount,
                    channel.ViewCount);
                ProfileDescriptionText.Text = channel.Description;

                uploadsPlaylistId = channel.UploadsPlaylistId;
                if (string.IsNullOrWhiteSpace(uploadsPlaylistId))
                {
                    UploadedVideosStatusText.Text = Localizer.Get("Profile.NoUploadsPlaylist");
                }
                else
                {
                    profileLoadStage = "uploaded videos";
                    DataPage<VideoSummary> uploadedVideos = await authenticatedClient.GetPlaylistVideosAsync(
                        uploadsPlaylistId,
                        null,
                        25);
                    foreach (VideoSummary video in uploadedVideos.Items)
                    {
                        UploadedVideos.Add(video);
                    }

                    uploadedVideosNextPageToken = uploadedVideos.NextPageToken;
                    UploadedVideosStatusText.Text = Localizer.Format("Profile.UploadedVideosLoaded", UploadedVideos.Count);
                }

                profileLoadStage = "subscriptions";
                DataPage<SubscriptionSummary> subscriptions = await authenticatedClient.GetSubscriptionsAsync(null, 25);
                foreach (SubscriptionSummary subscription in subscriptions.Items)
                {
                    Subscriptions.Add(subscription);
                }

                subscriptionsNextPageToken = subscriptions.NextPageToken;
                SubscriptionsStatusText.Text = Localizer.Format("Profile.SubscriptionsLoaded", Subscriptions.Count);

                profileLoadStage = "playlists";
                DataPage<PlaylistSummary> playlists = await authenticatedClient.GetPlaylistsAsync(null, 25);
                foreach (PlaylistSummary playlist in playlists.Items)
                {
                    Playlists.Add(playlist);
                }

                playlistsNextPageToken = playlists.NextPageToken;
                PlaylistsStatusText.Text = Localizer.Format("Profile.PlaylistsLoaded", Playlists.Count);
                PlaylistVideosStatusText.Text = Localizer.Get("Profile.SelectPlaylist");
                LikedVideosStatusText.Text = Localizer.Get("Profile.SelectLikedVideos");
                ProfileStatusText.Text = Localizer.Get("Profile.Loaded");
                profileLoaded = true;
            }
            catch (OAuthException exception)
            {
                ProfileStatusText.Text = Localizer.Format("Profile.TokenScopeMissing", exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                ProfileStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                ProfileStatusText.Text = GetProfileApiErrorMessage(exception);
            }
            catch (YouTubeApiResponseException exception)
            {
                ProfileStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                ProfileStatusText.Text = Localizer.Get("Profile.RequestTimedOut");
            }
            catch (HttpRequestException)
            {
                ProfileStatusText.Text = Localizer.Get("Profile.RequestNetworkFailure");
            }
            catch (Exception exception)
            {
                ShowProfileFailure(profileLoadStage, exception, ProfileStatusText);
            }
            finally
            {
                profileRequestInProgress = false;
                UpdateProfileControls();
            }
        }

        private async void MoreSubscriptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (profileRequestInProgress || string.IsNullOrWhiteSpace(subscriptionsNextPageToken))
            {
                return;
            }

            profileRequestInProgress = true;
            UpdateProfileControls();
            try
            {
                profileLoadStage = "subscriptions";
                DataPage<SubscriptionSummary> page = await authenticatedClient.GetSubscriptionsAsync(subscriptionsNextPageToken, 25);
                foreach (SubscriptionSummary subscription in page.Items)
                {
                    Subscriptions.Add(subscription);
                }

                subscriptionsNextPageToken = page.NextPageToken;
                SubscriptionsStatusText.Text = Localizer.Format("Profile.SubscriptionsLoaded", Subscriptions.Count);
            }
            catch (OAuthException exception)
            {
                SubscriptionsStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                SubscriptionsStatusText.Text = GetProfileApiErrorMessage(exception);
            }
            catch (YouTubeApiResponseException exception)
            {
                SubscriptionsStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                SubscriptionsStatusText.Text = Localizer.Get("Profile.SubscriptionsTimedOut");
            }
            catch (HttpRequestException)
            {
                SubscriptionsStatusText.Text = Localizer.Get("Profile.SubscriptionsNetworkFailure");
            }
            catch (Exception exception)
            {
                ShowProfileFailure(profileLoadStage, exception, SubscriptionsStatusText);
            }
            finally
            {
                profileRequestInProgress = false;
                UpdateProfileControls();
            }
        }

        private async void MoreUploadedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            if (profileRequestInProgress || string.IsNullOrWhiteSpace(uploadsPlaylistId)
                || string.IsNullOrWhiteSpace(uploadedVideosNextPageToken))
            {
                return;
            }

            profileRequestInProgress = true;
            UploadedVideosStatusText.Text = Localizer.Get("Profile.LoadingMoreUploadedVideos");
            UpdateProfileControls();
            try
            {
                profileLoadStage = "uploaded videos";
                DataPage<VideoSummary> page = await authenticatedClient.GetPlaylistVideosAsync(
                    uploadsPlaylistId,
                    uploadedVideosNextPageToken,
                    25);
                foreach (VideoSummary video in page.Items)
                {
                    UploadedVideos.Add(video);
                }

                uploadedVideosNextPageToken = page.NextPageToken;
                UploadedVideosStatusText.Text = Localizer.Format("Profile.UploadedVideosLoaded", UploadedVideos.Count);
            }
            catch (OAuthException exception)
            {
                UploadedVideosStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                UploadedVideosStatusText.Text = GetProfileApiErrorMessage(exception);
            }
            catch (YouTubeApiResponseException exception)
            {
                UploadedVideosStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                UploadedVideosStatusText.Text = Localizer.Get("Profile.UploadedVideosTimedOut");
            }
            catch (HttpRequestException)
            {
                UploadedVideosStatusText.Text = Localizer.Get("Profile.UploadedVideosNetworkFailure");
            }
            catch (Exception exception)
            {
                ShowProfileFailure(profileLoadStage, exception, UploadedVideosStatusText);
            }
            finally
            {
                profileRequestInProgress = false;
                UpdateProfileControls();
            }
        }

        private async void MorePlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            if (profileRequestInProgress || string.IsNullOrWhiteSpace(playlistsNextPageToken))
            {
                return;
            }

            profileRequestInProgress = true;
            UpdateProfileControls();
            try
            {
                profileLoadStage = "playlists";
                DataPage<PlaylistSummary> page = await authenticatedClient.GetPlaylistsAsync(playlistsNextPageToken, 25);
                foreach (PlaylistSummary playlist in page.Items)
                {
                    Playlists.Add(playlist);
                }

                playlistsNextPageToken = page.NextPageToken;
                PlaylistsStatusText.Text = Localizer.Format("Profile.PlaylistsLoaded", Playlists.Count);
            }
            catch (OAuthException exception)
            {
                PlaylistsStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                PlaylistsStatusText.Text = GetProfileApiErrorMessage(exception);
            }
            catch (YouTubeApiResponseException exception)
            {
                PlaylistsStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                PlaylistsStatusText.Text = Localizer.Get("Profile.PlaylistsTimedOut");
            }
            catch (HttpRequestException)
            {
                PlaylistsStatusText.Text = Localizer.Get("Profile.PlaylistsNetworkFailure");
            }
            catch (Exception exception)
            {
                ShowProfileFailure(profileLoadStage, exception, PlaylistsStatusText);
            }
            finally
            {
                profileRequestInProgress = false;
                UpdateProfileControls();
            }
        }

        private async void PlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            PlaylistSummary playlist = button == null ? null : button.Tag as PlaylistSummary;
            if (playlist == null || profileRequestInProgress)
            {
                return;
            }

            selectedPlaylistId = playlist.Id;
            SelectedPlaylistText.Text = playlist.Title;
            PlaylistVideos.Clear();
            playlistVideosNextPageToken = null;
            await LoadPlaylistVideosAsync(null);
        }

        private async void MorePlaylistVideosButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(selectedPlaylistId))
            {
                await LoadPlaylistVideosAsync(playlistVideosNextPageToken);
            }
        }

        private async Task LoadPlaylistVideosAsync(string pageToken)
        {
            if (profileRequestInProgress || string.IsNullOrWhiteSpace(selectedPlaylistId))
            {
                return;
            }

            profileRequestInProgress = true;
            PlaylistVideosStatusText.Text = pageToken == null
                ? Localizer.Get("Profile.LoadingPlaylistVideos")
                : Localizer.Get("Profile.LoadingMorePlaylistVideos");
            UpdateProfileControls();
            try
            {
                profileLoadStage = "playlist videos";
                DataPage<VideoSummary> page = await authenticatedClient.GetPlaylistVideosAsync(
                    selectedPlaylistId,
                    pageToken,
                    25);
                foreach (VideoSummary video in page.Items)
                {
                    PlaylistVideos.Add(video);
                }

                playlistVideosNextPageToken = page.NextPageToken;
                PlaylistVideosStatusText.Text = Localizer.Format("Profile.PlaylistVideosLoaded", PlaylistVideos.Count);
            }
            catch (OAuthException exception)
            {
                PlaylistVideosStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                PlaylistVideosStatusText.Text = GetProfileApiErrorMessage(exception);
            }
            catch (YouTubeApiResponseException exception)
            {
                PlaylistVideosStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                PlaylistVideosStatusText.Text = Localizer.Get("Profile.PlaylistVideosTimedOut");
            }
            catch (HttpRequestException)
            {
                PlaylistVideosStatusText.Text = Localizer.Get("Profile.PlaylistVideosNetworkFailure");
            }
            catch (Exception exception)
            {
                ShowProfileFailure(profileLoadStage, exception, PlaylistVideosStatusText);
            }
            finally
            {
                profileRequestInProgress = false;
                UpdateProfileControls();
            }
        }

        private async void LoadLikedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            if (profileRequestInProgress)
            {
                return;
            }

            LikedVideos.Clear();
            likedVideosNextPageToken = null;
            await LoadLikedVideosAsync(null);
        }

        private async void MoreLikedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadLikedVideosAsync(likedVideosNextPageToken);
        }

        private async Task LoadLikedVideosAsync(string pageToken)
        {
            if (profileRequestInProgress)
            {
                return;
            }

            profileRequestInProgress = true;
            LikedVideosStatusText.Text = pageToken == null
                ? Localizer.Get("Profile.LoadingLikedVideos")
                : Localizer.Get("Profile.LoadingMoreLikedVideos");
            UpdateProfileControls();
            try
            {
                profileLoadStage = "liked videos";
                DataPage<VideoSummary> page = await authenticatedClient.GetLikedVideosAsync(pageToken, 25);
                foreach (VideoSummary video in page.Items)
                {
                    LikedVideos.Add(video);
                }

                likedVideosNextPageToken = page.NextPageToken;
                LikedVideosStatusText.Text = LikedVideos.Count == 0
                    ? Localizer.Get("Profile.NoLikedVideos")
                    : Localizer.Format("Profile.LikedVideosLoaded", LikedVideos.Count);
            }
            catch (OAuthException exception)
            {
                LikedVideosStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                LikedVideosStatusText.Text = GetProfileApiErrorMessage(exception);
            }
            catch (YouTubeApiResponseException exception)
            {
                LikedVideosStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                LikedVideosStatusText.Text = Localizer.Get("Profile.LikedVideosTimedOut");
            }
            catch (HttpRequestException)
            {
                LikedVideosStatusText.Text = Localizer.Get("Profile.LikedVideosNetworkFailure");
            }
            catch (Exception exception)
            {
                ShowProfileFailure(profileLoadStage, exception, LikedVideosStatusText);
            }
            finally
            {
                profileRequestInProgress = false;
                UpdateProfileControls();
            }
        }

        private void ProfileVideoButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            NavigateToVideo(button == null ? null : button.Tag as VideoSummary);
        }

        private void ToggleUploadedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleProfileSection(UploadedVideosContentPanel, UploadedVideosToggleGlyph);
        }

        private void ToggleSubscriptionsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleProfileSection(SubscriptionsContentPanel, SubscriptionsToggleGlyph);
        }

        private void TogglePlaylistsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleProfileSection(PlaylistsContentPanel, PlaylistsToggleGlyph);
        }

        private void TogglePlaylistVideosButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleProfileSection(PlaylistVideosContentPanel, PlaylistVideosToggleGlyph);
        }

        private void ToggleLikedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleProfileSection(LikedVideosContentPanel, LikedVideosToggleGlyph);
        }

        private static void ToggleProfileSection(FrameworkElement content, TextBlock glyph)
        {
            bool expanded = content.Visibility == Visibility.Visible;
            content.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            glyph.Text = expanded ? "+" : "-";
        }

        private void UpdateProfileControls()
        {
            if (MoreSubscriptionsButton == null)
            {
                return;
            }

            bool canLoad = !profileRequestInProgress;
            RefreshProfileButton.IsEnabled = canLoad;
            MoreSubscriptionsButton.IsEnabled = canLoad && !string.IsNullOrWhiteSpace(subscriptionsNextPageToken);
            MoreUploadedVideosButton.IsEnabled = canLoad && !string.IsNullOrWhiteSpace(uploadedVideosNextPageToken);
            MorePlaylistsButton.IsEnabled = canLoad && !string.IsNullOrWhiteSpace(playlistsNextPageToken);
            MorePlaylistVideosButton.IsEnabled = canLoad && !string.IsNullOrWhiteSpace(playlistVideosNextPageToken);
            LoadLikedVideosButton.IsEnabled = canLoad && profileLoaded;
            MoreLikedVideosButton.IsEnabled = canLoad && !string.IsNullOrWhiteSpace(likedVideosNextPageToken);
        }

        private static string GetProfileApiErrorMessage(YouTubeApiException exception)
        {
            if (exception.StatusCode == System.Net.HttpStatusCode.Forbidden
                || exception.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Localizer.Get("Profile.AccessDenied");
            }

            return exception.Message;
        }

        private static void ShowProfileFailure(string stage, Exception exception, TextBlock statusText)
        {
            string safeStage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
            DiagnosticLog.WriteException("Profile." + safeStage, exception);
            statusText.Text = Localizer.Format(
                "Profile.LoadFailed",
                GetProfileStageName(safeStage),
                exception.HResult.ToString("X8"));
        }

        private static string GetProfileStageName(string stage)
        {
            switch (stage)
            {
                case "starting":
                    return Localizer.Get("Profile.StageStarting");
                case "channel":
                    return Localizer.Get("Profile.StageChannel");
                case "uploaded videos":
                    return Localizer.Get("Profile.StageUploadedVideos");
                case "subscriptions":
                    return Localizer.Get("Profile.StageSubscriptions");
                case "playlists":
                    return Localizer.Get("Profile.StagePlaylists");
                case "playlist videos":
                    return Localizer.Get("Profile.StagePlaylistVideos");
                case "liked videos":
                    return Localizer.Get("Profile.StageLikedVideos");
                default:
                    return Localizer.Get("Common.Unknown");
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                CategoryStatusText.Text = Localizer.Get("Categories.Loading");
                IReadOnlyList<VideoCategory> categories = await client.GetVideoCategoriesAsync(GetSelectedRegionCode());
                Categories.Clear();
                foreach (VideoCategory category in categories)
                {
                    Categories.Add(category);
                }

                CategoryStatusText.Text = Localizer.Format("Categories.Available", Categories.Count, GetRegionLabel());
            }
            catch (InvalidOperationException exception)
            {
                CategoryStatusText.Text = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                CategoryStatusText.Text = exception.Message;
            }
            catch (YouTubeApiResponseException exception)
            {
                CategoryStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                CategoryStatusText.Text = Localizer.Get("Common.ApiTimedOut");
            }
            catch (HttpRequestException)
            {
                CategoryStatusText.Text = Localizer.Get("Common.ApiNetworkFailure");
            }
        }

        private async void CategoryToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            VideoCategory category = button == null ? null : button.Tag as VideoCategory;
            if (category == null)
            {
                return;
            }

            if (category.IsExpanded)
            {
                category.IsExpanded = false;
                return;
            }

            foreach (VideoCategory otherCategory in Categories)
            {
                if (otherCategory != category)
                {
                    otherCategory.IsExpanded = false;
                }
            }

            category.IsExpanded = true;
            if (category.HasLoaded)
            {
                return;
            }

            try
            {
                category.StatusMessage = Localizer.Format("Categories.LoadingVideos", category.Title);
                DataPage<VideoSummary> page = await client.GetMostPopularVideosAsync(GetSelectedRegionCode(), category.Id, null, 25);
                category.SetVideos(page.Items);
                category.StatusMessage = Localizer.Format(
                    "Categories.PopularVideos",
                    category.Videos.Count,
                    category.Title,
                    GetRegionLabel());
            }
            catch (InvalidOperationException exception)
            {
                category.StatusMessage = exception.Message;
            }
            catch (YouTubeApiException exception)
            {
                category.StatusMessage = exception.Message;
            }
            catch (YouTubeApiResponseException exception)
            {
                category.StatusMessage = exception.Message;
            }
            catch (TaskCanceledException)
            {
                category.StatusMessage = Localizer.Get("Common.ApiTimedOut");
            }
            catch (HttpRequestException)
            {
                category.StatusMessage = Localizer.Get("Common.ApiNetworkFailure");
            }
        }

        private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            VideoSummary video = e.ClickedItem as VideoSummary;
            NavigateToVideo(video);
        }

        private void CategoryVideoButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            NavigateToVideo(button == null ? null : button.Tag as VideoSummary);
        }

        private void NavigateToVideo(VideoSummary video)
        {
            if (video != null)
            {
                Frame.Navigate(typeof(VideoDetailsPage), video.Id);
            }
        }

        private void ReplaceResults(DataPage<VideoSummary> page)
        {
            Results.Clear();
            foreach (VideoSummary video in page.Items)
            {
                Results.Add(video);
            }
        }

        private void UpdatePivotHeaders()
        {
            if (HomePivotHeader == null || SearchPivotHeader == null || CategoriesPivotHeader == null || ProfilePivotHeader == null)
            {
                return;
            }

            HomePivotHeader.IsSelected = MainPivot.SelectedIndex == 0;
            SearchPivotHeader.IsSelected = MainPivot.SelectedIndex == 1;
            CategoriesPivotHeader.IsSelected = MainPivot.SelectedIndex == 2;
            ProfilePivotHeader.IsSelected = MainPivot.SelectedIndex == 3;
        }

        private string GetRegionLabel()
        {
            RegionOption selectedRegion = FindRegion(SelectedRegionCode);
            return selectedRegion == null ? Localizer.Get("Regions.UnitedStates") : selectedRegion.Name;
        }

        private string GetSelectedRegionCode()
        {
            return string.IsNullOrWhiteSpace(SelectedRegionCode) ? "US" : SelectedRegionCode;
        }

        private static string GetHomeRegionCode()
        {
            string homeRegionCode = Windows.System.UserProfile.GlobalizationPreferences.HomeGeographicRegion;
            return string.IsNullOrWhiteSpace(homeRegionCode) ? "US" : homeRegionCode.Trim();
        }

        private RegionOption FindRegion(string regionCode)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return null;
            }

            foreach (RegionOption region in Regions)
            {
                if (string.Equals(region.Code, regionCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return region;
                }
            }

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

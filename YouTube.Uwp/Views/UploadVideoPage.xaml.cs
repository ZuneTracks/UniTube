using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using YouTube.Uwp.Services;

namespace YouTube.Uwp.Views
{
    public sealed partial class UploadVideoPage : Page
    {
        private readonly IYouTubeUploadClient uploadClient;
        private StorageFile selectedFile;
        private CancellationTokenSource uploadCancellation;

        public UploadVideoPage()
        {
            InitializeComponent();
            OAuthDeviceAuthorizationService oauthService = new OAuthDeviceAuthorizationService(App.Configuration);
            uploadClient = new YouTubeResumableUploadClient(oauthService.GetValidAccessTokenAsync);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CancelUpload();
            base.OnNavigatedFrom(e);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void SelectVideoButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".wmv");
            picker.FileTypeFilter.Add(".mov");
            picker.FileTypeFilter.Add(".avi");
            picker.FileTypeFilter.Add(".mkv");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                ProgressText.Text = Localizer.Get("Upload.SelectionCanceled");
                return;
            }

            BasicProperties properties = await file.GetBasicPropertiesAsync();
            selectedFile = file;
            SelectedFileText.Text = file.Name + " (" + FormatFileSize(properties.Size) + ")";
            ProgressText.Text = Localizer.Format("Upload.ReadyToUpload", file.Name);
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (uploadCancellation != null)
            {
                return;
            }

            uploadCancellation = new CancellationTokenSource();
            SetUploadControls(false);
            UploadProgressBar.Value = 0;
            ProgressText.Text = Localizer.Get("Upload.Starting");

            try
            {
                ComboBoxItem privacyItem = PrivacyBox.SelectedItem as ComboBoxItem;
                VideoUploadRequest request = new VideoUploadRequest
                {
                    File = selectedFile,
                    Title = TitleBox.Text,
                    Description = DescriptionBox.Text,
                    PrivacyStatus = privacyItem == null ? null : privacyItem.Tag as string
                };

                await uploadClient.UploadAsync(
                    request,
                    new Progress<VideoUploadProgress>(UpdateProgress),
                    uploadCancellation.Token);
                string completionMessage = Localizer.Get("Upload.CompletedProcessing");
                ProgressText.Text = completionMessage;
                await new MessageDialog(completionMessage).ShowAsync();
                ResetUploadForm();
            }
            catch (TaskCanceledException)
            {
                ProgressText.Text = uploadCancellation.IsCancellationRequested
                    ? Localizer.Get("Upload.Canceled")
                    : Localizer.Get("Upload.TimedOut");
            }
            catch (OperationCanceledException)
            {
                ProgressText.Text = Localizer.Get("Upload.Canceled");
            }
            catch (ArgumentException exception)
            {
                ProgressText.Text = exception.Message;
            }
            catch (OAuthException exception)
            {
                ProgressText.Text = exception.Message;
            }
            catch (YouTubeUploadException exception)
            {
                ProgressText.Text = exception.Message;
            }
            catch (HttpRequestException)
            {
                ProgressText.Text = Localizer.Get("Upload.NetworkFailure");
            }
            finally
            {
                uploadCancellation.Dispose();
                uploadCancellation = null;
                SetUploadControls(true);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelUpload();
        }

        private void CancelUpload()
        {
            if (uploadCancellation != null && !uploadCancellation.IsCancellationRequested)
            {
                uploadCancellation.Cancel();
                ProgressText.Text = Localizer.Get("Upload.Canceling");
            }
        }

        private void SetUploadControls(bool ready)
        {
            UploadButton.IsEnabled = ready;
            CancelButton.IsEnabled = !ready;
        }

        private void UpdateProgress(VideoUploadProgress progress)
        {
            UploadProgressBar.Value = progress.Percentage;
            ProgressText.Text = FormatFileSize(progress.BytesUploaded)
                + " / "
                + FormatFileSize(progress.TotalBytes)
                + " ("
                + progress.Percentage.ToString("F0", CultureInfo.CurrentCulture)
                + "%)";
        }

        private void ResetUploadForm()
        {
            selectedFile = null;
            SelectedFileText.Text = Localizer.Get("SelectedFileText.Text");
            TitleBox.Text = string.Empty;
            DescriptionBox.Text = string.Empty;
            PrivacyBox.SelectedIndex = 0;
            UploadProgressBar.Value = 0;
            ProgressText.Text = Localizer.Get("UploadReadyStatus.Text");
        }

        private static string FormatFileSize(ulong bytes)
        {
            const double Kilobyte = 1024d;
            const double Megabyte = Kilobyte * 1024d;

            double value = bytes >= Megabyte
                ? bytes / Megabyte
                : bytes / Kilobyte;
            string unit = bytes >= Megabyte ? "MB" : "KB";
            string format = value < 10 ? "0.#" : "0";
            return value.ToString(format, CultureInfo.CurrentCulture) + " " + unit;
        }
    }
}

using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using YouTube.Uwp.Services;

namespace YouTube.Uwp.Views
{
    public sealed partial class DiagnosticsPage : Page
    {
        public DiagnosticsPage()
        {
            InitializeComponent();
            RefreshLog();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshLog();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            DiagnosticLog.Clear();
            RefreshLog();
            StatusText.Text = Localizer.Get("Diagnostics.LogCleared");
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = "UniTube-diagnostics";
            picker.FileTypeChoices.Add(Localizer.Get("Diagnostics.TextFileType"), new[] { ".txt" });

            try
            {
                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null)
                {
                    StatusText.Text = Localizer.Get("Diagnostics.SaveCanceled");
                    return;
                }

                await FileIO.WriteTextAsync(file, DiagnosticLog.Read());
                StatusText.Text = Localizer.Format("Diagnostics.LogSaved", file.Name);
            }
            catch (Exception exception)
            {
                DiagnosticLog.WriteException("Diagnostics.Save", exception);
                StatusText.Text = Localizer.Format("Diagnostics.SaveFailed", exception.HResult.ToString("X8"));
            }
        }

        private void RefreshLog()
        {
            string log = DiagnosticLog.Read();
            LogTextBox.Text = string.IsNullOrWhiteSpace(log)
                ? Localizer.Get("Diagnostics.NoEvents")
                : log;
        }
    }
}

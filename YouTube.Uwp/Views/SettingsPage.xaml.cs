using System;
using Windows.ApplicationModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;
using YouTube.Uwp.Services;

namespace YouTube.Uwp.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly OAuthDeviceAuthorizationService oauthService;
        private CancellationTokenSource authorizationCancellation;
        private readonly DispatcherTimer authorizationCountdownTimer;
        private DateTimeOffset authorizationExpiresAt;
        private int authorizationPollIntervalSeconds;
        private string authorizationPollingStatus;

        public SettingsPage()
        {
            InitializeComponent();
            oauthService = new OAuthDeviceAuthorizationService(App.Configuration);
            authorizationCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            authorizationCountdownTimer.Tick += AuthorizationCountdownTimer_Tick;
            ApiKeyStatusText.Text = GetApiKeyStatus();
            OAuthClientIdBox.Text = App.Configuration.StoredOAuthClientId ?? string.Empty;
            SafeModeToggle.IsOn = App.Configuration.IsSafeModeEnabled;
            UpdateAuthorizationStatus();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            UpdateAuthorizationStatus();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CancelAuthorization();
            StopAuthorizationCountdown();
            base.OnNavigatedFrom(e);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Configuration.SaveApiKey(ApiKeyBox.Password);
                ApiKeyBox.Password = string.Empty;
                ApiKeyStatusText.Text = GetApiKeyStatus();
            }
            catch (ArgumentException exception)
            {
                ApiKeyStatusText.Text = exception.Message;
            }
        }

        private void ClearApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            App.Configuration.ClearApiKey();
            ApiKeyBox.Password = string.Empty;
            ApiKeyStatusText.Text = GetApiKeyStatus();
        }

        private void SafeModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            App.Configuration.SetSafeModeEnabled(SafeModeToggle.IsOn);
        }

        private void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DiagnosticsPage));
        }

        private static string GetApiKeyStatus()
        {
            if (App.Configuration.HasStoredApiKey)
            {
                return Localizer.Get("Settings.ApiKeyStored");
            }

            if (App.Configuration.HasBuildDefaultApiKey)
            {
                return Localizer.Get("Settings.ApiKeyBuiltIn");
            }

            return Localizer.Get("Settings.ApiKeyNotConfigured");
        }

        private void UpdateAuthorizationStatus()
        {
            if (OAuthDeviceAuthorizationService.HasStoredToken())
            {
                AuthStatusText.Text = Localizer.Get("Settings.AuthorizationStored");
            }
            else if (App.Configuration.HasStoredOAuthDeviceCredentials)
            {
                AuthStatusText.Text = Localizer.Get("Settings.OAuthCredentialsStored");
            }
            else if (App.Configuration.HasBuildDefaultOAuthDeviceCredentials)
            {
                AuthStatusText.Text = App.Configuration.HasIncompleteStoredOAuthDeviceCredentials
                    ? Localizer.Get("Settings.OAuthIncompleteOverride")
                    : Localizer.Get("Settings.OAuthBuiltIn");
            }
            else if (App.Configuration.HasOAuthDeviceCredentials)
            {
                AuthStatusText.Text = Localizer.Get("Settings.OAuthManaged");
            }
            else
            {
                AuthStatusText.Text = Localizer.Get("Settings.OAuthNotConfigured");
            }

            UpdateAuthorizationControls();
        }

        private void UpdateAuthorizationControls()
        {
            bool authorizing = authorizationCancellation != null;
            SignInButton.IsEnabled = !authorizing;
            SignOutButton.IsEnabled = !authorizing && OAuthDeviceAuthorizationService.HasStoredToken();
        }

        private void SaveOAuthSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Configuration.SaveOAuthDeviceSettings(OAuthClientIdBox.Text, OAuthClientSecretBox.Password);
                OAuthClientSecretBox.Password = string.Empty;
                AuthStatusText.Text = Localizer.Get("Settings.OAuthCredentialsSaved");
                UpdateAuthorizationControls();
            }
            catch (ArgumentException exception)
            {
                AuthStatusText.Text = exception.Message;
            }
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            if (authorizationCancellation != null)
            {
                return;
            }

            authorizationCancellation = new CancellationTokenSource();
            CancelAuthorizationButton.IsEnabled = true;
            SignInButton.IsEnabled = false;
            SignOutButton.IsEnabled = false;
            VerificationUrlText.Text = string.Empty;
            VerificationCodeText.Text = string.Empty;
            AuthorizationCountdownText.Text = string.Empty;
            DiagnosticLog.Write("OAuth.SignIn", "Device authorization requested.");

            try
            {
                bool renewedCode = false;
                while (true)
                {
                    DeviceAuthorizationInfo authorization = await oauthService.BeginAuthorizationAsync(authorizationCancellation.Token);
                    ShowDeviceAuthorization(authorization, renewedCode);
                    try
                    {
                        await oauthService.CompleteAuthorizationAsync(
                            authorization,
                            new Progress<DeviceAuthorizationProgress>(UpdateDeviceAuthorizationProgress),
                            authorizationCancellation.Token);
                        StopAuthorizationCountdown();
                        AuthStatusText.Text = Localizer.Get("Settings.AuthorizationCompleted");
                        DiagnosticLog.Write("OAuth.SignIn", "Device authorization completed and token persistence returned.");
                        break;
                    }
                    catch (DeviceAuthorizationExpiredException)
                    {
                        authorizationCancellation.Token.ThrowIfCancellationRequested();
                        StopAuthorizationCountdown();
                        AuthStatusText.Text = Localizer.Get("Settings.VerificationCodeExpiredRequesting");
                        DiagnosticLog.Write("OAuth.SignIn", "Device authorization code expired; requesting a replacement.");
                        renewedCode = true;
                    }
                }
            }
            catch (OAuthException exception)
            {
                DiagnosticLog.WriteException("OAuth.SignIn", exception);
                AuthStatusText.Text = exception.Message;
            }
            catch (TaskCanceledException)
            {
                DiagnosticLog.Write("OAuth.SignIn", "Device authorization canceled or timed out.");
                AuthStatusText.Text = Localizer.Get("Settings.AuthorizationCanceled");
            }
            catch (OperationCanceledException)
            {
                DiagnosticLog.Write("OAuth.SignIn", "Device authorization canceled.");
                AuthStatusText.Text = Localizer.Get("Settings.AuthorizationCanceled");
            }
            catch (HttpRequestException)
            {
                DiagnosticLog.Write("OAuth.SignIn", "Network failure during device authorization.");
                AuthStatusText.Text = Localizer.Get("Settings.AuthorizationNetworkFailure");
            }
            catch (Exception exception)
            {
                DiagnosticLog.WriteException("OAuth.SignIn", exception);
                AuthStatusText.Text = Localizer.Format("Settings.AuthorizationUnexpectedFailure", exception.HResult.ToString("X8"));
            }
            finally
            {
                StopAuthorizationCountdown();
                authorizationCancellation.Dispose();
                authorizationCancellation = null;
                CancelAuthorizationButton.IsEnabled = false;
                UpdateAuthorizationControls();
            }
        }

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            OAuthDeviceAuthorizationService.ClearStoredToken();
            VerificationUrlText.Text = string.Empty;
            VerificationCodeText.Text = string.Empty;
            AuthStatusText.Text = Localizer.Get("Settings.AuthorizationRemoved");
            UpdateAuthorizationControls();
        }

        private void ShowDeviceAuthorization(DeviceAuthorizationInfo authorization, bool renewedCode)
        {
            authorizationExpiresAt = DateTimeOffset.UtcNow.AddSeconds(authorization.ExpiresInSeconds);
            authorizationPollIntervalSeconds = authorization.PollIntervalSeconds;
            authorizationPollingStatus = Localizer.Get("Settings.WaitingForGoogleApproval");
            VerificationUrlText.Text = Localizer.Format(
                "Settings.VerificationUrl",
                authorization.VerificationUri.AbsoluteUri);
            VerificationCodeText.Text = Localizer.Format("Settings.VerificationCode", authorization.UserCode);
            AuthStatusText.Text = renewedCode
                ? Localizer.Get("Settings.VerificationCodeRenewed")
                : Localizer.Get("Settings.VerificationCodeInstructions");
            UpdateAuthorizationCountdown();
            authorizationCountdownTimer.Start();
        }

        private void UpdateDeviceAuthorizationProgress(DeviceAuthorizationProgress progress)
        {
            authorizationPollIntervalSeconds = progress.PollIntervalSeconds;
            authorizationPollingStatus = progress.Status;
            UpdateAuthorizationCountdown();
        }

        private void AuthorizationCountdownTimer_Tick(object sender, object e)
        {
            UpdateAuthorizationCountdown();
        }

        private void UpdateAuthorizationCountdown()
        {
            int secondsRemaining = Math.Max(0, (int)Math.Ceiling((authorizationExpiresAt - DateTimeOffset.UtcNow).TotalSeconds));
            if (secondsRemaining == 0)
            {
                AuthorizationCountdownText.Text = Localizer.Get("Settings.VerificationCodeExpiredRequesting");
                return;
            }

            TimeSpan remaining = TimeSpan.FromSeconds(secondsRemaining);
            AuthorizationCountdownText.Text = Localizer.Format(
                "Settings.AuthorizationCountdown",
                authorizationPollingStatus,
                (int)remaining.TotalMinutes,
                remaining.Seconds,
                authorizationPollIntervalSeconds);
        }

        private void StopAuthorizationCountdown()
        {
            authorizationCountdownTimer.Stop();
            authorizationExpiresAt = DateTimeOffset.MinValue;
            authorizationPollIntervalSeconds = 0;
            authorizationPollingStatus = string.Empty;
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.Left
            };
            var panel = new StackPanel
            {
                Width = 320,
                Padding = new Thickness(12)
            };

            panel.Children.Add(new TextBlock
            {
                Text = Localizer.Get("Settings.AboutTitle"),
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(new TextBlock
            {
                Text = Localizer.Get("Settings.AboutDescription"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            PackageVersion version = Package.Current.Id.Version;
            panel.Children.Add(new TextBlock
            {
                Text = Localizer.Format("Settings.AboutBuild", version.Major, version.Minor, version.Build, version.Revision),
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(new TextBlock
            {
                Text = Localizer.Get("Settings.AboutAffiliation"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var developerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            developerPanel.Children.Add(new TextBlock
            {
                Text = Localizer.Get("Settings.AboutDeveloper"),
                VerticalAlignment = VerticalAlignment.Center
            });
            developerPanel.Children.Add(new HyperlinkButton
            {
                Content = "ZuneTracks",
                NavigateUri = new Uri("https://github.com/ZuneTracks/YourTube-UWP"),
                Margin = new Thickness(4, 0, 0, 0)
            });
            panel.Children.Add(developerPanel);
            panel.Children.Add(new TextBlock
            {
                Text = Localizer.Get("Settings.AboutSpecialThanks"),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 4)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "RU and UA translation provided by @msnmoney (Discord)",
                TextWrapping = TextWrapping.Wrap
            });

            var closeButton = new Button
            {
                Content = Localizer.Get("CloseButton.Content"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            closeButton.Click += (s, args) => flyout.Hide();
            panel.Children.Add(closeButton);

            flyout.Content = panel;
            flyout.ShowAt((FrameworkElement)sender);
        }

        private void CancelAuthorizationButton_Click(object sender, RoutedEventArgs e)
        {
            CancelAuthorization();
        }

        private void CancelAuthorization()
        {
            if (authorizationCancellation != null && !authorizationCancellation.IsCancellationRequested)
            {
                authorizationCancellation.Cancel();
            }
        }
    }
}

namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;
using Re_RunApp.Core;
using System.Text.Json;

public partial class SummaryScreen : ContentPage
{
    private PlayerStatistics _statistics;
    private bool _pulseActive = true;
    private string _updatedGpxData;

    private WebView? _webView;
    private bool _isProcessingCallback = false;

    // Add thread safety fields
    private readonly SemaphoreSlim _authSemaphore = new SemaphoreSlim(1, 1);
    private bool _disposed = false;
    private CancellationTokenSource? _cancellationTokenSource;

    private static readonly string SCOPE = "activity:write,activity:read";

    public SummaryScreen(PlayerStatistics statistics, string updatedGpxData)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        _updatedGpxData = updatedGpxData ?? throw new ArgumentNullException(nameof(updatedGpxData));
        _cancellationTokenSource = new CancellationTokenSource();
        InitializeComponent();

        this.Unloaded += SummaryScreen_Unloaded;
    }

    private void SummaryScreen_Unloaded(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _disposed = true;
            _pulseActive = false;
            _cancellationTokenSource?.Cancel();

            DisconnectWebView();
            _authSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_disposed) return;

        // Start pulse animation - fire and forget
        _ = StartStatisticsPulseAsync();

        // Thread-safe UI updates
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_disposed) return;

            int avgHeartRate = 0;
            int maxHeartRate = 0;
            if (_statistics.HeartRateSamples.Count > 0)
            {
                avgHeartRate = (int)_statistics.HeartRateSamples.Average();
                maxHeartRate = _statistics.HeartRateSamples.Max();
            }

            TimeSpan avgSpeedMinKm = TimeSpan.FromMinutes(0);
            if (_statistics.CurrentDistanceM.HasValue && _statistics.SecondsElapsed > 0)
            {
                var avgSpeedKmh = (decimal)_statistics.CurrentDistanceM.Value / (decimal)_statistics.SecondsElapsed * 3.6m;
                avgSpeedMinKm = TimeSpan.FromMinutes(60 / (double)avgSpeedKmh);
            }

            AscendLabel.Text = $"{_statistics.TotalInclinationM:N0}";
            AverageHeartRateLabel.Text = avgHeartRate > 0 ? $"{avgHeartRate}" : "--";
            MaxHeartRateLabel.Text = maxHeartRate > 0 ? $"{maxHeartRate}" : "--";
            AverageSpeedLabel.Text = $"{avgSpeedMinKm:mm\\:ss}";
            DistanceLabel.Text = $"{_statistics.CurrentDistanceM:N0}";
            DurationLabel.Text = $"{TimeSpan.FromSeconds(_statistics.SecondsElapsed):hh\\:mm\\:ss}";
            TitleLabel.Text = $"#Treadmill - {Runtime.RunSettings?.Name}";

            var enable = (Runtime.StravaSettings != null && !string.IsNullOrEmpty(Runtime.StravaSettings.ClientId) && !string.IsNullOrEmpty(Runtime.StravaSettings.ClientSecret));
            StravaCheckbox.IsChecked = enable;
            StravaCheckbox.IsEnabled = enable;
        });
    }

    protected override void OnDisappearing()
    {
        _pulseActive = false;
        base.OnDisappearing();
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        if (_disposed) return;

        // post to strava if requested, otherwise bail out
        if (StravaCheckbox.IsChecked == true)
        {
            await StartAuthenticationAsync();
        }
        else
        {
            await Navigation.PopToRootAsync();
        }
    }

    private async Task StartStatisticsPulseAsync()
    {
        try
        {
            var token = _cancellationTokenSource?.Token ?? CancellationToken.None;

            while (_pulseActive && !token.IsCancellationRequested && !_disposed)
            {
                if (StatisticsImage != null && !_disposed)
                {
                    await StatisticsImage.ScaleTo(1.02, 1400, Easing.SinInOut);
                    if (_pulseActive && !_disposed)
                    {
                        await StatisticsImage.ScaleTo(0.98, 1400, Easing.SinInOut);
                    }
                }

                if (!_disposed)
                {
                    await Task.Delay(50, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in pulse animation: {ex}");
        }
    }

    private async Task StartAuthenticationAsync()
    {
        if (!await _authSemaphore.WaitAsync(100))
        {
            return; // Prevent multiple concurrent authentication attempts
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_disposed) return;

                StartAuthenticationSync();
            });
        }
        finally
        {
            _authSemaphore.Release();
        }
    }

    private void StartAuthenticationSync()
    {
        // Clear any existing content in the popup
        PopupContent.Children.Clear();

        // Ensure StravaSettings is not null before dereferencing
        if (Runtime.StravaSettings == null)
        {
            // Fire and forget error display - keep your pattern
            _ = ShowErrorPage("Strava settings are not configured.");
            return;
        }

        // Create and add the WebView for Strava authentication
        _webView = new WebView
        {
            BackgroundColor = Colors.White,
            Source = $"https://www.strava.com/oauth/authorize?client_id={Runtime.StravaSettings.ClientId}&redirect_uri={Runtime.StravaSettings.RedirectUrl}&response_type=code&scope={SCOPE}"
        };

        // Attach the Navigating event to handle redirects
        _webView.Navigating += WebView_Navigating;

        PopupContent.Children.Add(_webView);

        // Show the popup
        WebViewPopup.IsVisible = true;
    }

    private async void WebView_Navigating(object? sender, WebNavigatingEventArgs e)
    {
        if (_disposed) return;

        // Ensure StravaSettings is not null before dereferencing
        if (Runtime.StravaSettings == null)
        {
            _ = ShowErrorPage("Strava settings are not configured."); // Keep fire-and-forget
            return;
        }

        // Intercept the redirect to localhost BEFORE it loads
        if (e.Url.StartsWith(Runtime.StravaSettings.RedirectUrl) && !_isProcessingCallback)
        {
            // Cancel the navigation to prevent showing the broken page
            e.Cancel = true;

            _isProcessingCallback = true; // Prevent multiple processing

            try
            {
                var uri = new Uri(e.Url);
                var authorizationCode = uri.GetQueryParameter("code");

                if (!string.IsNullOrEmpty(authorizationCode))
                {
                    // Show loading message while processing
                    _ = ShowLoadingPage(); // Keep fire-and-forget

                    var accessToken = await ExchangeAuthorizationCodeForTokenAsync(authorizationCode);

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Post activity to Strava
                        var activityResult = await UploadGpxToStravaAsync(accessToken);

                        if (activityResult)
                        {
                            _ = ShowSuccessPage(); // Keep fire-and-forget
                        }
                        else
                        {
                            _ = ShowErrorPage("Failed to post activity to Strava"); // Keep fire-and-forget
                        }
                    }
                    else
                    {
                        _ = ShowErrorPage("Failed to get access token"); // Keep fire-and-forget
                    }
                }
                else if (uri.GetQueryParameter("error") != null)
                {
                    var error = uri.GetQueryParameter("error");
                    _ = ShowErrorPage($"Authentication failed: {error}"); // Keep fire-and-forget
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing callback: {ex.Message}");
                _ = ShowErrorPage("Authentication failed"); // Keep fire-and-forget
            }
            finally
            {
                // Disconnect the WebView event to prevent further navigation handling
                DisconnectWebView();
                _isProcessingCallback = false;
            }
        }
        // Allow all other navigation (like going to Strava's auth page)
    }

    private void DisconnectWebView()
    {
        try
        {
            if (_webView != null)
            {
                _webView.Navigating -= WebView_Navigating;
                _webView = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disconnecting WebView: {ex}");
        }
    }

    private async Task ShowLoadingPage()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_disposed) return;

            PopupContent.Children.Clear();

            PopupContent.Children.Add(new StackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new ActivityIndicator { IsRunning = true, Color = Colors.Blue },
                    new Label
                    {
                        Text = "Processing authentication and posting activity...",
                        HorizontalOptions = LayoutOptions.Center,
                        Margin = new Thickness(20),
                        FontSize = 28
                    }
                }
            });

            WebViewPopup.IsVisible = true;
        });
    }

    private async Task ShowSuccessPage()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_disposed) return;

            PopupContent.Children.Clear();

            PopupContent.Children.Add(new StackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 20,
                Padding = new Thickness(40),
                Children =
                {
                    new Label
                    {
                        Text = "✅",
                        FontSize = 48,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "Success!",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "Activity posted to Strava successfully!",
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Button
                    {
                        Text = "Done",
                        Command = new Command(async () =>
                        {
                            if (!_disposed)
                            {
                                WebViewPopup.IsVisible = false;
                                await Shell.Current.GoToAsync("//MainPage");
                            }
                        })
                    }
                }
            });

            WebViewPopup.IsVisible = true;
        });
    }

    private async Task ShowErrorPage(string errorMessage)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_disposed) return;

            PopupContent.Children.Clear();

            PopupContent.Children.Add(new StackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 20,
                Padding = new Thickness(40),
                Children =
                {
                    new Label
                    {
                        Text = "❌",
                        FontSize = 48,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = "Error",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = errorMessage,
                        HorizontalOptions = LayoutOptions.Center,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Button
                    {
                        Text = "Exit",
                        Command = new Command(async () =>
                        {
                            if (!_disposed)
                            {
                                WebViewPopup.IsVisible = false;
                                await Shell.Current.GoToAsync("//MainPage");
                            }
                        })
                    }
                }
            });

            WebViewPopup.IsVisible = true;
        });
    }

    private async Task<string?> ExchangeAuthorizationCodeForTokenAsync(string authorizationCode)
    {
        try
        {
            // Ensure StravaSettings is not null before dereferencing
            if (Runtime.StravaSettings == null)
            {
                return null;
            }

            using var client = new HttpClient();
            var values = new Dictionary<string, string>
            {
                { "client_id", Runtime.StravaSettings.ClientId },
                { "client_secret", Runtime.StravaSettings.ClientSecret },
                { "code", authorizationCode },
                { "grant_type", "authorization_code" }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://www.strava.com/oauth/token", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenData = JsonSerializer.Deserialize<StravaTokenResponse>(responseString);
            return tokenData?.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> UploadGpxToStravaAsync(string accessToken)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var form = new MultipartFormDataContent();
            using var gpxStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_updatedGpxData));
            using var fileContent = new StreamContent(gpxStream);

            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", "re-run.gpx"); // virtual file name 
            form.Add(new StringContent("gpx"), "data_type"); // Specify the file type
            form.Add(new StringContent($"#Treadmill - {Runtime.RunSettings?.Name}"), "name"); // Optional: Activity name
            form.Add(new StringContent("0"), "trainer"); // Optional: Mark as trainer activity
            form.Add(new StringContent("Virtual run posted from the Re-Run App!"), "description"); // Add the description

            var response = await client.PostAsync("https://www.strava.com/api/v3/uploads", form);
            var responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Upload response: {responseString}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("GPX file uploaded successfully!");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to upload GPX file: {responseString}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading GPX file: {ex.Message}");
            return false;
        }
    }
}

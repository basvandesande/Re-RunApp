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

    private static readonly string SCOPE = "activity:write,activity:read";


    public SummaryScreen(PlayerStatistics statistics, string updatedGpxData)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        _updatedGpxData = updatedGpxData ?? throw new ArgumentNullException(nameof(updatedGpxData));
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartStatisticsPulse();

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

            avgSpeedMinKm=TimeSpan.FromMinutes(60 / (double)avgSpeedKmh);
        }

        AscendLabel.Text = $"{_statistics.TotalInclinationM:N0}";
        AverageHeartRateLabel.Text = $"{avgHeartRate}";
        MaxHeartRateLabel.Text = $"{maxHeartRate}";
        AverageSpeedLabel.Text = $"{avgSpeedMinKm:mm\\:ss}";
        DistanceLabel.Text = $"{_statistics.CurrentDistanceM:N0}";
        DurationLabel.Text = $"{TimeSpan.FromSeconds(_statistics.SecondsElapsed):hh\\:mm\\:ss}";
        TitleLabel.Text = $"#Treadmill - {Runtime.RunSettings?.Name}";


        var enable = (Runtime.StravaSettings != null && !string.IsNullOrEmpty(Runtime.StravaSettings.ClientId) && !string.IsNullOrEmpty(Runtime.StravaSettings.ClientSecret));
        StravaCheckbox.IsChecked = enable;
        StravaCheckbox.IsEnabled = enable;
    }

    protected override void OnDisappearing()
    {
        _pulseActive = false;
        base.OnDisappearing();
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        // post to strava if requested, otherwise bail out
        if (StravaCheckbox.IsChecked == true)
        {
            StartAuthentication();
        }
        else
        {
            await Navigation.PopToRootAsync();
        }
    }

    private async void StartStatisticsPulse()
    {
        while (_pulseActive)
        {
            await StatisticsImage.ScaleTo(1.02, 1400, Easing.SinInOut);
            await StatisticsImage.ScaleTo(0.98, 1400, Easing.SinInOut);
        }
    }

    private async Task ShowLoadingPage()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Content = new StackLayout
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
            };
        });
    }


    private void StartAuthentication()
    {
        _webView = new WebView
        {
            BackgroundColor = Colors.White,
            Source = $"https://www.strava.com/oauth/authorize?client_id={Runtime.StravaSettings.ClientId}&redirect_uri={Runtime.StravaSettings.RedirectUrl}&response_type=code&scope={SCOPE}"
        };

        // Use Navigating instead of Navigated to intercept BEFORE the page loads
        _webView.Navigating += WebView_Navigating;
        Content = _webView;
    }

    private async void WebView_Navigating(object sender, WebNavigatingEventArgs e)
    {

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
                    await ShowLoadingPage();

                    var accessToken = await ExchangeAuthorizationCodeForTokenAsync(authorizationCode);

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Post activity to Strava
                        //var activityResult = await PostActivityToStravaAsync(accessToken);
                        var activityResult = await UploadGpxToStravaAsync(accessToken);

                        if (activityResult)
                        {
                            await ShowSuccessPage();
                        }
                        else
                        {
                            await ShowErrorPage("Failed to post activity to Strava");
                        }
                    }
                    else
                    {
                        await ShowErrorPage("Failed to get access token");
                    }
                }
                else if (uri.GetQueryParameter("error") != null)
                {
                    var error = uri.GetQueryParameter("error");
                    await ShowErrorPage($"Authentication failed: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing callback: {ex.Message}");
                await ShowErrorPage("Authentication failed");
            }
            finally
            {
                // Disconnect the WebView event to prevent further navigation handling
                DisconnectWebView();
            }
        }
        // Allow all other navigation (like going to Strava's auth page)
    }

    private void DisconnectWebView()
    {
        if (_webView != null)
        {
            _webView.Navigating -= WebView_Navigating;
            _webView = null;
        }
    }

    private async Task ShowSuccessPage()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Content = new StackLayout
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
                            Command = new Command(async () => await Shell.Current.GoToAsync("//MainPage"))
                        }
                    }
            };
        });
    }

    private async Task ShowErrorPage(string errorMessage)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Content = new StackLayout
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
                            Command = new Command(async () => await Shell.Current.GoToAsync("//MainPage"))
                        }
                    }
            };
        });
    }

    private async Task<string?> ExchangeAuthorizationCodeForTokenAsync(string authorizationCode)
    {
        try
        {
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
        catch (Exception ex)
        {
            return null;
        }
    }

    private async Task<bool> PostActivityToStravaAsync(string accessToken)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var activity = new StravaActivity
            {
                Name = $"#Treadmill - {Runtime.RunSettings?.Name}",
                Type = "Run",
                SportType = "VirtualRun",
                StartDateLocal = _statistics.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ElapsedTime =  (int)_statistics.SecondsElapsed,
                Distance = (int)_statistics.CurrentDistanceM,
                Description = "Posted from the Re-Run App!",
            };


            string json = JsonSerializer.Serialize(activity, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://www.strava.com/api/v3/activities", content);
            var responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Activity post response: {responseString}");

            if (response.IsSuccessStatusCode)
            {
                var activityResponse = JsonSerializer.Deserialize<StravaActivityResponse>(responseString);
                Console.WriteLine($"Activity created with ID: {activityResponse?.Id}");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to post activity: {responseString}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error posting activity: {ex.Message}");
            return false;
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
            form.Add(new StringContent("Posted from the Re-Run App!"), "description"); // Add the description

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

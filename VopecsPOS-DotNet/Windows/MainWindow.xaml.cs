using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using VopecsPOS.Services;

namespace VopecsPOS.Windows
{
    public partial class MainWindow : Window
    {
        private bool _isFabExpanded = false;
        private bool _isFullScreen = false;
        private WindowStyle _previousWindowStyle;
        private WindowState _previousWindowState;
        private readonly SettingsService _settings;

        public MainWindow()
        {
            LogService.Info("MainWindow constructor called");
            InitializeComponent();
            _settings = SettingsService.Instance;
            LogService.Info("MainWindow initialized");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LogService.Info("MainWindow Window_Loaded called");
            try
            {
                // Initialize WebView2
                LogService.Info("Initializing WebView2...");
                await WebView.EnsureCoreWebView2Async();
                LogService.Info("WebView2 initialized successfully");

                // Configure WebView2 settings
                LogService.Info("Configuring WebView2 settings...");
                WebView.CoreWebView2.Settings.IsScriptEnabled = true;
                WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebView.CoreWebView2.Settings.IsPinchZoomEnabled = true;

                // Handle navigation events
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                LogService.Info("WebView2 events attached");

                // Inject JavaScript for touch scrolling fix
                WebView.CoreWebView2.NavigationCompleted += async (s, args) =>
                {
                    if (args.IsSuccess)
                    {
                        await WebView.CoreWebView2.ExecuteScriptAsync(@"
                            // Fix touch scrolling for all scrollable elements
                            document.addEventListener('touchstart', function(e) {}, {passive: true});
                            document.addEventListener('touchmove', function(e) {}, {passive: true});

                            // Enable smooth scrolling
                            document.documentElement.style.scrollBehavior = 'smooth';

                            // Intercept print calls for silent printing
                            window.originalPrint = window.print;
                            window.print = function() {
                                window.chrome.webview.postMessage({type: 'print'});
                            };
                        ");
                    }
                };

                // Handle messages from JavaScript
                WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Load the saved URL
                var url = _settings.GetUrlToLoad();
                LogService.Info($"URL to load: {url}");

                if (!string.IsNullOrEmpty(url))
                {
                    LogService.Info($"Navigating to: {url}");
                    WebView.CoreWebView2.Navigate(url);
                }
                else
                {
                    LogService.Warning("No URL configured");
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    MessageBox.Show("No URL configured. Please configure a URL in settings.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to initialize WebView2", ex);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nPlease ensure WebView2 Runtime is installed.\n\nLog file: {LogService.GetLogFilePath()}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.WebMessageAsJson;
                LogService.Info($"WebMessage received: {message}");

                if (message.Contains("\"type\":\"print\""))
                {
                    await PrintSilent();
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Error handling web message", ex);
            }
        }

        private async System.Threading.Tasks.Task PrintSilent()
        {
            try
            {
                LogService.Info("Starting silent print...");

                var printSettings = WebView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.ShouldPrintHeaderAndFooter = false;
                printSettings.ShouldPrintSelectionOnly = false;

                // Set margins to 0 to avoid cutting
                printSettings.MarginTop = 0;
                printSettings.MarginBottom = 0;
                printSettings.MarginLeft = 0;
                printSettings.MarginRight = 0;

                // Print silently to default printer
                var result = await WebView.CoreWebView2.PrintAsync(printSettings);

                LogService.Info($"Silent print result: {result}");
            }
            catch (Exception ex)
            {
                LogService.Error("Silent print error", ex);
                // Fallback to normal print dialog
                WebView.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LogService.Info($"Navigation starting: {e.Uri}");
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogService.Info($"Navigation completed. Success: {e.IsSuccess}");
            LoadingOverlay.Visibility = Visibility.Collapsed;

            // Save last visited URL
            if (e.IsSuccess && WebView.CoreWebView2.Source != null)
            {
                _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
                LogService.Info($"Saved last visited URL: {WebView.CoreWebView2.Source}");
            }
            else if (!e.IsSuccess)
            {
                LogService.Error($"Navigation failed with error: {e.WebErrorStatus}");
            }
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Open new windows in the same WebView
            e.NewWindow = WebView.CoreWebView2;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // When maximized, go to true fullscreen
            if (WindowState == WindowState.Maximized && !_isFullScreen)
            {
                EnterFullScreen();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // F11 or Escape to toggle/exit fullscreen
            if (e.Key == Key.F11)
            {
                if (_isFullScreen)
                    ExitFullScreen();
                else
                    EnterFullScreen();
            }
            else if (e.Key == Key.Escape && _isFullScreen)
            {
                ExitFullScreen();
            }
        }

        private void EnterFullScreen()
        {
            if (_isFullScreen) return;

            LogService.Info("Entering fullscreen mode");
            _isFullScreen = true;
            _previousWindowStyle = WindowStyle;
            _previousWindowState = WindowState;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal; // Need to set Normal first
            WindowState = WindowState.Maximized;
            Topmost = true;

            // Hide FAB in fullscreen? Optional
            // FabPanel.Visibility = Visibility.Collapsed;
        }

        private void ExitFullScreen()
        {
            if (!_isFullScreen) return;

            LogService.Info("Exiting fullscreen mode");
            _isFullScreen = false;

            Topmost = false;
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;

            // FabPanel.Visibility = Visibility.Visible;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save current URL before closing
            if (WebView.CoreWebView2?.Source != null)
            {
                _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
            }

            // Shutdown application when main window closes
            LogService.Info("MainWindow closing, shutting down application");
            Application.Current.Shutdown();
        }

        private void FabButton_Click(object sender, RoutedEventArgs e)
        {
            _isFabExpanded = !_isFabExpanded;
            FabMenu.Visibility = _isFabExpanded ? Visibility.Visible : Visibility.Collapsed;
            FabButton.Content = _isFabExpanded ? "✕" : "☰";
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            CloseFabMenu();
            var homeUrl = _settings.SavedUrl;
            if (!string.IsNullOrEmpty(homeUrl) && WebView.CoreWebView2 != null)
            {
                LogService.Info($"Navigating to home: {homeUrl}");
                WebView.CoreWebView2.Navigate(homeUrl);
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseFabMenu();

            var result = MessageBox.Show(
                "Do you want to change the system URL?\n\nThis will restart the application.",
                "Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Clear saved URL and restart
                _settings.ClearAll();

                // Open setup window
                var setupWindow = new SetupWindow();
                setupWindow.Show();

                // Close this window
                Close();
            }
        }

        private void CloseFabMenu()
        {
            _isFabExpanded = false;
            FabMenu.Visibility = Visibility.Collapsed;
            FabButton.Content = "☰";
        }
    }
}

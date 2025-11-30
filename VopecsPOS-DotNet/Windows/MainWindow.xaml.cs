using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using VopecsPOS.Services;

namespace VopecsPOS.Windows
{
    public partial class MainWindow : Window
    {
        private bool _isFabExpanded = false;
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

                // Handle navigation events
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                LogService.Info("WebView2 events attached");

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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save current URL before closing
            if (WebView.CoreWebView2?.Source != null)
            {
                _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
            }
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

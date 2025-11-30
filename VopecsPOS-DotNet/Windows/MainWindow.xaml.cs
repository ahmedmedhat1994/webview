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
            InitializeComponent();
            _settings = SettingsService.Instance;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize WebView2
                await WebView.EnsureCoreWebView2Async();

                // Configure WebView2 settings
                WebView.CoreWebView2.Settings.IsScriptEnabled = true;
                WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Handle navigation events
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

                // Load the saved URL
                var url = _settings.GetUrlToLoad();
                if (!string.IsNullOrEmpty(url))
                {
                    WebView.CoreWebView2.Navigate(url);
                }
                else
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    MessageBox.Show("No URL configured. Please configure a URL in settings.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nPlease ensure WebView2 Runtime is installed.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;

            // Save last visited URL
            if (e.IsSuccess && WebView.CoreWebView2.Source != null)
            {
                _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
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

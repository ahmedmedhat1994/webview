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

            // Check for updates in background
            _ = CheckForUpdatesAsync();

            try
            {
                // Initialize WebView2
                LogService.Info("Initializing WebView2...");
                await WebView.EnsureCoreWebView2Async();
                LogService.Info("WebView2 initialized successfully");

                // Configure WebView2 settings for better performance
                LogService.Info("Configuring WebView2 settings...");
                WebView.CoreWebView2.Settings.IsScriptEnabled = true;
                WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false; // Disable for performance
                WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = false; // Disable for performance
                WebView.CoreWebView2.Settings.IsPinchZoomEnabled = true;
                WebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false; // Disable for stability

                // Handle navigation events
                WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                WebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed; // Auto-recovery

                // Handle messages from JavaScript (for silent printing)
                WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                LogService.Info("WebMessage handler registered for print interception");

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

        private async void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            LogService.Error($"WebView2 process failed: {e.Reason}");

            // Auto-recovery: reload the page
            if (e.Reason == CoreWebView2ProcessFailedReason.Crashed ||
                e.Reason == CoreWebView2ProcessFailedReason.Unresponsive)
            {
                LogService.Info("Attempting auto-recovery...");
                await System.Threading.Tasks.Task.Delay(1000);

                try
                {
                    var url = _settings.GetUrlToLoad();
                    if (!string.IsNullOrEmpty(url))
                    {
                        WebView.CoreWebView2.Navigate(url);
                        LogService.Info("Auto-recovery successful");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("Auto-recovery failed", ex);
                }
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
            string? pdfPath = null;
            try
            {
                var scale = _settings.PrintScale;
                var paperSize = _settings.PaperSize;
                LogService.Info($"Starting silent print with paper={paperSize}, scale={scale}%...");

                // Create print settings with correct paper size
                var printSettings = WebView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.ShouldPrintHeaderAndFooter = false;
                printSettings.ShouldPrintSelectionOnly = false;
                printSettings.MarginTop = 0;
                printSettings.MarginBottom = 0;
                printSettings.MarginLeft = 0;
                printSettings.MarginRight = 0;

                // Set scale
                double scaleFactor = (double)scale / 100.0;
                printSettings.ScaleFactor = scaleFactor;

                // Set page size based on paper selection (in inches)
                switch (paperSize)
                {
                    case "58mm":
                        printSettings.PageWidth = 2.28;  // 58mm
                        printSettings.PageHeight = 11.0;
                        break;
                    case "80mm":
                        printSettings.PageWidth = 3.15;  // 80mm
                        printSettings.PageHeight = 11.0;
                        break;
                    case "A4":
                        printSettings.PageWidth = 8.27;  // 210mm
                        printSettings.PageHeight = 11.69; // 297mm
                        break;
                    default:
                        printSettings.PageWidth = 3.15;  // 80mm default
                        printSettings.PageHeight = 11.0;
                        break;
                }

                LogService.Info($"Print settings: width={printSettings.PageWidth}, height={printSettings.PageHeight}, scale={scaleFactor}");

                // Get default printer
                var printerName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                LogService.Info($"Default printer: {printerName}");

                // Set printer name for direct printing
                printSettings.PrinterName = printerName;

                // Print directly without dialog
                var result = await WebView.CoreWebView2.PrintAsync(printSettings);

                LogService.Info($"Print result: {result}");

                if (result == CoreWebView2PrintStatus.Succeeded)
                {
                    LogService.Info("Silent print completed successfully");
                }
                else if (result == CoreWebView2PrintStatus.PrinterUnavailable)
                {
                    LogService.Error("Printer unavailable");
                    MessageBox.Show("Printer is not available. Please check your printer connection.", "Print Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    LogService.Warning($"Print status: {result}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Silent print error", ex);
                MessageBox.Show($"Print error: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Clean up temp file if any
                if (pdfPath != null && System.IO.File.Exists(pdfPath))
                {
                    try
                    {
                        System.IO.File.Delete(pdfPath);
                    }
                    catch { }
                }
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            LogService.Info($"Navigation starting: {e.Uri}");
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LogService.Info($"Navigation completed. Success: {e.IsSuccess}");
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (e.IsSuccess)
            {
                // Save last visited URL
                if (WebView.CoreWebView2.Source != null)
                {
                    _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
                }

                // Inject JavaScript for touch scrolling fix only
                // Print interception is now handled by WebView2 PrintRequested event
                try
                {
                    await WebView.CoreWebView2.ExecuteScriptAsync(@"
                        (function() {
                            // Fix touch scrolling for all elements
                            var style = document.createElement('style');
                            style.textContent = '* { touch-action: manipulation; -ms-touch-action: manipulation; } ::-webkit-scrollbar { width: 8px; } ::-webkit-scrollbar-track { background: #f1f1f1; } ::-webkit-scrollbar-thumb { background: #888; border-radius: 4px; }';
                            document.head.appendChild(style);

                            // Enable touch scrolling on all scrollable elements
                            document.querySelectorAll('*').forEach(function(el) {
                                var cs = window.getComputedStyle(el);
                                if (cs.overflow === 'auto' || cs.overflow === 'scroll' ||
                                    cs.overflowY === 'auto' || cs.overflowY === 'scroll') {
                                    el.style.touchAction = 'pan-y';
                                    el.style.webkitOverflowScrolling = 'touch';
                                }
                            });
                        })();
                    ");
                    LogService.Info("Touch scrolling fix injected");
                }
                catch (Exception ex)
                {
                    LogService.Error("Failed to inject JavaScript", ex);
                }
            }
            else
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
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            Topmost = true;

            // Re-open FAB popup after fullscreen
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FabPopup.IsOpen = false;
                FabPopup.IsOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ExitFullScreen()
        {
            if (!_isFullScreen) return;

            LogService.Info("Exiting fullscreen mode");
            _isFullScreen = false;

            Topmost = false;
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;

            // Re-open FAB popup after exiting fullscreen
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FabPopup.IsOpen = false;
                FabPopup.IsOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            // Update popup position when window moves
            UpdatePopupPosition();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update popup position when window resizes
            UpdatePopupPosition();
        }

        private void UpdatePopupPosition()
        {
            if (FabPopup.IsOpen)
            {
                // Force popup to recalculate position
                FabPopup.IsOpen = false;
                FabPopup.IsOpen = true;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save current URL before closing
            if (WebView.CoreWebView2?.Source != null)
            {
                _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
            }

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

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            CloseFabMenu();
            LogService.Info("Print button clicked from FAB menu");
            await PrintSilent();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseFabMenu();

            var currentUrl = _settings.SavedUrl ?? "https://";
            var currentScale = _settings.PrintScale;
            var currentPaperSize = _settings.PaperSize;
            var dialog = new SettingsDialog(currentUrl, currentScale, currentPaperSize);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Save print settings
                _settings.PrintScale = dialog.NewPrintScale;
                _settings.PaperSize = dialog.NewPaperSize;
                LogService.Info($"Print settings: scale={dialog.NewPrintScale}%, paper={dialog.NewPaperSize}");

                // Save and navigate to URL if changed
                if (!string.IsNullOrEmpty(dialog.NewUrl) && dialog.NewUrl != currentUrl)
                {
                    LogService.Info($"Changing URL to: {dialog.NewUrl}");
                    _settings.SavedUrl = dialog.NewUrl;

                    if (WebView.CoreWebView2 != null)
                    {
                        WebView.CoreWebView2.Navigate(dialog.NewUrl);
                    }
                }
            }
        }

        private void CloseFabMenu()
        {
            _isFabExpanded = false;
            FabMenu.Visibility = Visibility.Collapsed;
            FabButton.Content = "☰";
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                var updateInfo = await UpdateService.CheckForUpdatesAsync();

                if (updateInfo.IsUpdateAvailable)
                {
                    LogService.Info($"Update available: {updateInfo.LatestVersion}");

                    // Show update dialog on UI thread
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var dialog = new UpdateDialog(updateInfo);
                        dialog.ShowDialog();
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Error checking for updates", ex);
            }
        }
    }
}

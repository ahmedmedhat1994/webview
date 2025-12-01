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

                // Inject iframe print interception script BEFORE any page scripts run
                await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    (function() {
                        // Mark this as VopecsPOS app for server-side detection
                        window.isVopecsPOS = true;
                        window.VopecsPOSVersion = '1.7.0';
                        console.log('VopecsPOS: App detected, modal will be skipped');

                        // Function to send print message with HTML content
                        const sendPrintMessage = (html) => {
                            if (window.chrome && window.chrome.webview) {
                                console.log('VopecsPOS: Sending iframe content for printing');
                                window.chrome.webview.postMessage({type: 'print', html: html});
                            }
                        };

                        // Patch appendChild to detect iframe additions
                        const origAppendChild = Node.prototype.appendChild;
                        Node.prototype.appendChild = function(child) {
                            const result = origAppendChild.call(this, child);

                            if (child && child.tagName && child.tagName.toLowerCase() === 'iframe') {
                                console.log('VopecsPOS: Iframe detected, setting up print interception');

                                // Continuously try to patch print for 2 seconds (iframe gets document.write)
                                let attempts = 0;
                                const patchInterval = setInterval(() => {
                                    try {
                                        if (child.contentWindow && typeof child.contentWindow.print === 'function') {
                                            const origPrint = child.contentWindow.print.bind(child.contentWindow);
                                            child.contentWindow.print = function() {
                                                console.log('VopecsPOS: Iframe print() intercepted!');

                                                // Get iframe content
                                                let html = '';
                                                try {
                                                    const doc = child.contentWindow.document || child.contentDocument;
                                                    if (doc && doc.documentElement) {
                                                        html = doc.documentElement.outerHTML;
                                                    }
                                                } catch(e) {
                                                    console.log('VopecsPOS: Could not get iframe content', e);
                                                }

                                                if (html) {
                                                    sendPrintMessage(html);
                                                } else {
                                                    // Fallback to original print
                                                    origPrint();
                                                }
                                            };
                                            console.log('VopecsPOS: Iframe print patched successfully');
                                        }
                                    } catch(e) {}

                                    attempts++;
                                    if (attempts > 40) clearInterval(patchInterval); // 2 seconds max
                                }, 50);
                            }
                            return result;
                        };

                        // Also override window.print for direct calls
                        const origWindowPrint = window.print.bind(window);
                        window.print = function() {
                            console.log('VopecsPOS: window.print() intercepted');
                            sendPrintMessage(document.documentElement.outerHTML);
                        };

                        console.log('VopecsPOS: Iframe print interception installed');
                    })();
                ");
                LogService.Info("Iframe print interception script added");

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
                    // Check if HTML content is included
                    if (message.Contains("\"html\":"))
                    {
                        // Parse HTML from message
                        var json = Newtonsoft.Json.Linq.JObject.Parse(message);
                        var html = json["html"]?.ToString();

                        if (!string.IsNullOrEmpty(html))
                        {
                            LogService.Info("Print with HTML content received");
                            await PrintHtmlContent(html);
                        }
                        else
                        {
                            LogService.Warning("HTML content is empty, printing current page");
                            await PrintSilent();
                        }
                    }
                    else
                    {
                        // No HTML, print current page
                        await PrintSilent();
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Error handling web message", ex);
            }
        }

        private async System.Threading.Tasks.Task PrintHtmlContent(string html)
        {
            try
            {
                LogService.Info("Starting HTML print with hidden WebView...");

                // Get the base URL from settings
                var baseUrl = _settings.SavedUrl ?? "https://pos.megacaresa.com";
                var baseUri = new Uri(baseUrl);
                var origin = $"{baseUri.Scheme}://{baseUri.Host}";

                // Fix relative CSS/resource paths to absolute URLs
                html = html.Replace("href=\"/css/", $"href=\"{origin}/css/");
                html = html.Replace("href='/css/", $"href='{origin}/css/");
                html = html.Replace("src=\"/images/", $"src=\"{origin}/images/");
                html = html.Replace("src='/images/", $"src='{origin}/images/");
                html = html.Replace("src=\"/", $"src=\"{origin}/");
                html = html.Replace("src='/", $"src='{origin}/");
                LogService.Info($"Fixed relative paths with base: {origin}");

                // Create a temporary HTML file
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"print_{Guid.NewGuid()}.html");
                await System.IO.File.WriteAllTextAsync(tempPath, html);
                LogService.Info($"Temp HTML file created: {tempPath}");

                // Create hidden WebView for printing
                var hiddenWebView = new Microsoft.Web.WebView2.Wpf.WebView2();
                hiddenWebView.Visibility = Visibility.Collapsed;
                hiddenWebView.Width = 0;
                hiddenWebView.Height = 0;

                // Add to visual tree temporarily (required for WebView2 to work)
                var mainGrid = (System.Windows.Controls.Grid)this.Content;
                mainGrid.Children.Add(hiddenWebView);

                try
                {
                    // Initialize hidden WebView
                    await hiddenWebView.EnsureCoreWebView2Async();
                    LogService.Info("Hidden WebView initialized");

                    // Navigate to temp file
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                    hiddenWebView.CoreWebView2.NavigationCompleted += async (s, args) =>
                    {
                        if (args.IsSuccess)
                        {
                            try
                            {
                                // Wait for content to render
                                await System.Threading.Tasks.Task.Delay(500);

                                // Print silently
                                var printSettings = hiddenWebView.CoreWebView2.Environment.CreatePrintSettings();
                                printSettings.ShouldPrintBackgrounds = true;
                                printSettings.ShouldPrintHeaderAndFooter = false;
                                printSettings.MarginTop = 0;
                                printSettings.MarginBottom = 0;
                                printSettings.MarginLeft = 0;
                                printSettings.MarginRight = 0;

                                // Set scale and paper size
                                var scale = _settings.PrintScale;
                                var paperSize = _settings.PaperSize;
                                printSettings.ScaleFactor = (double)scale / 100.0;

                                switch (paperSize)
                                {
                                    case "58mm":
                                        printSettings.PageWidth = 2.28;
                                        printSettings.PageHeight = 11.0;
                                        break;
                                    case "80mm":
                                        printSettings.PageWidth = 3.15;
                                        printSettings.PageHeight = 11.0;
                                        break;
                                    case "A4":
                                        printSettings.PageWidth = 8.27;
                                        printSettings.PageHeight = 11.69;
                                        break;
                                    default:
                                        printSettings.PageWidth = 3.15;
                                        printSettings.PageHeight = 11.0;
                                        break;
                                }

                                // Get default printer
                                var printerName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                                printSettings.PrinterName = printerName;
                                LogService.Info($"Printing to: {printerName}");

                                var result = await hiddenWebView.CoreWebView2.PrintAsync(printSettings);
                                LogService.Info($"Hidden WebView print result: {result}");

                                tcs.TrySetResult(result == CoreWebView2PrintStatus.Succeeded);
                            }
                            catch (Exception ex)
                            {
                                LogService.Error("Print error in hidden WebView", ex);
                                tcs.TrySetResult(false);
                            }
                        }
                        else
                        {
                            LogService.Error("Hidden WebView navigation failed");
                            tcs.TrySetResult(false);
                        }
                    };

                    hiddenWebView.CoreWebView2.Navigate($"file:///{tempPath.Replace("\\", "/")}");

                    // Wait for print to complete (timeout 30 seconds)
                    var completed = await System.Threading.Tasks.Task.WhenAny(
                        tcs.Task,
                        System.Threading.Tasks.Task.Delay(30000)
                    );

                    if (completed != tcs.Task)
                    {
                        LogService.Warning("Print timeout");
                    }
                    else if (tcs.Task.Result)
                    {
                        LogService.Info("Silent print from hidden WebView completed successfully");
                    }
                }
                finally
                {
                    // Clean up
                    mainGrid.Children.Remove(hiddenWebView);
                    hiddenWebView.Dispose();

                    // Delete temp file
                    try
                    {
                        if (System.IO.File.Exists(tempPath))
                            System.IO.File.Delete(tempPath);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("PrintHtmlContent error", ex);
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
                // Save last visited URL (skip for print navigations)
                if (WebView.CoreWebView2.Source != null && !WebView.CoreWebView2.Source.StartsWith("data:"))
                {
                    _settings.LastVisitedUrl = WebView.CoreWebView2.Source;
                }

                // Inject JavaScript for touch scrolling fix only
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

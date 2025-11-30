using System;
using System.Windows;
using System.Windows.Threading;
using VopecsPOS.Services;
using VopecsPOS.Windows;

namespace VopecsPOS
{
    public partial class App : Application
    {
        private SplashWindow? _splash;
        private DispatcherTimer? _timer;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Setup global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogService.Error("Unhandled AppDomain Exception", ex);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogService.Error("Unhandled Dispatcher Exception", args.Exception);
                args.Handled = true;
            };

            try
            {
                LogService.Info("=== Application Starting ===");
                LogService.Info($"Log file: {LogService.GetLogFilePath()}");

                // Show splash screen
                LogService.Info("Showing splash screen...");
                _splash = new SplashWindow();
                _splash.Show();

                // Use timer instead of Task.Delay
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
                LogService.Info("Timer started, waiting 2 seconds...");
            }
            catch (Exception ex)
            {
                LogService.Error("Startup error", ex);
                MessageBox.Show($"Startup error: {ex.Message}\n\nCheck log file at:\n{LogService.GetLogFilePath()}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer?.Stop();
            LogService.Info("Timer tick - splash complete");

            try
            {
                _splash?.Close();
                LogService.Info("Splash closed");

                // Initialize settings
                LogService.Info("Initializing settings...");
                var settings = SettingsService.Instance;
                LogService.Info($"Settings loaded. SavedUrl: {settings.SavedUrl ?? "null"}");

                // Check if URL is configured
                if (string.IsNullOrEmpty(settings.SavedUrl))
                {
                    LogService.Info("No URL configured, showing setup window...");
                    var setupWindow = new SetupWindow();
                    setupWindow.Show();
                    LogService.Info("Setup window shown");
                }
                else
                {
                    LogService.Info("URL found, showing main window...");
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    LogService.Info("Main window shown");
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Timer_Tick error", ex);
                MessageBox.Show($"Error: {ex.Message}\n\nCheck log file at:\n{LogService.GetLogFilePath()}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

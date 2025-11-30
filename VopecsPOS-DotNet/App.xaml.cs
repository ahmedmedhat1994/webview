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
            try
            {
                // Show splash screen
                _splash = new SplashWindow();
                _splash.Show();

                // Use timer instead of Task.Delay
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer?.Stop();

            try
            {
                _splash?.Close();

                // Initialize settings
                var settings = SettingsService.Instance;

                // Check if URL is configured
                if (string.IsNullOrEmpty(settings.SavedUrl))
                {
                    // Show setup window
                    var setupWindow = new SetupWindow();
                    setupWindow.Show();
                }
                else
                {
                    // Show main window with WebView
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}

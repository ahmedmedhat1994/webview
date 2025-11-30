using System;
using System.Windows;
using VopecsPOS.Services;
using VopecsPOS.Windows;

namespace VopecsPOS
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Show splash screen
            var splash = new SplashWindow();
            splash.Show();

            // Initialize settings
            var settings = SettingsService.Instance;

            // Simulate loading
            System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    splash.Close();

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
                });
            });
        }
    }
}

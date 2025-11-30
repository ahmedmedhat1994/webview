using System;
using System.Windows;
using VopecsPOS.Services;

namespace VopecsPOS.Windows
{
    public partial class SetupWindow : Window
    {
        public SetupWindow()
        {
            LogService.Info("SetupWindow constructor called");
            InitializeComponent();
            UrlTextBox.Focus();
            UrlTextBox.CaretIndex = UrlTextBox.Text.Length;
            LogService.Info("SetupWindow initialized");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            LogService.Info($"Save button clicked. URL: {url}");

            // Validate URL
            if (!ValidateUrl(url))
            {
                return;
            }

            try
            {
                // Save URL
                LogService.Info("Saving URL...");
                SettingsService.Instance.SavedUrl = url;
                LogService.Info("URL saved successfully");

                // Open main window
                LogService.Info("Opening main window...");
                var mainWindow = new MainWindow();
                mainWindow.Show();
                LogService.Info("Main window opened");

                // Close setup window
                LogService.Info("Closing setup window...");
                Close();
            }
            catch (Exception ex)
            {
                LogService.Error("Error in SaveButton_Click", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateUrl(string url)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowError("Please enter a URL");
                return false;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                ShowError("URL must start with http:// or https://");
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? result))
            {
                ShowError("Please enter a valid URL");
                return false;
            }

            if (result.Host.Length == 0)
            {
                ShowError("Please enter a valid domain");
                return false;
            }

            LogService.Info("URL validation passed");
            return true;
        }

        private void ShowError(string message)
        {
            LogService.Warning($"Validation error: {message}");
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}

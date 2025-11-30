using System;
using System.Windows;
using System.Windows.Input;
using VopecsPOS.Services;

namespace VopecsPOS.Windows
{
    public partial class SetupWindow : Window
    {
        public SetupWindow()
        {
            InitializeComponent();
            UrlTextBox.Focus();
            UrlTextBox.CaretIndex = UrlTextBox.Text.Length;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();

            // Validate URL
            if (!ValidateUrl(url))
            {
                return;
            }

            // Save URL
            SettingsService.Instance.SavedUrl = url;

            // Open main window
            var mainWindow = new MainWindow();
            mainWindow.Show();

            // Close setup window
            Close();
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

            return true;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}

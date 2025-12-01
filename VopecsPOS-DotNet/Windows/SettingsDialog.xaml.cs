using System;
using System.Windows;

namespace VopecsPOS.Windows
{
    public partial class SettingsDialog : Window
    {
        public string? NewUrl { get; private set; }

        public SettingsDialog(string currentUrl)
        {
            InitializeComponent();
            UrlTextBox.Text = currentUrl;
            UrlTextBox.Focus();
            UrlTextBox.SelectAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();

            if (!ValidateUrl(url))
            {
                return;
            }

            NewUrl = url;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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

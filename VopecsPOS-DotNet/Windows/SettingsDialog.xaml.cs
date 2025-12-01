using System;
using System.Windows;
using System.Windows.Controls;
using VopecsPOS.Services;

namespace VopecsPOS.Windows
{
    public partial class SettingsDialog : Window
    {
        public string? NewUrl { get; private set; }
        public int NewPrintScale { get; private set; }
        public string NewPaperSize { get; private set; } = "80mm";

        public SettingsDialog(string currentUrl, int currentPrintScale, string currentPaperSize)
        {
            InitializeComponent();
            UrlTextBox.Text = currentUrl;
            ScaleSlider.Value = currentPrintScale;
            ScaleValueText.Text = $"{currentPrintScale}%";

            // Set paper size selection
            foreach (ComboBoxItem item in PaperSizeCombo.Items)
            {
                if (item.Tag?.ToString() == currentPaperSize)
                {
                    PaperSizeCombo.SelectedItem = item;
                    break;
                }
            }

            UrlTextBox.Focus();
            UrlTextBox.SelectAll();
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ScaleValueText != null)
            {
                ScaleValueText.Text = $"{(int)ScaleSlider.Value}%";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();

            if (!ValidateUrl(url))
            {
                return;
            }

            NewUrl = url;
            NewPrintScale = (int)ScaleSlider.Value;

            // Get paper size from selected item
            if (PaperSizeCombo.SelectedItem is ComboBoxItem selectedItem)
            {
                NewPaperSize = selectedItem.Tag?.ToString() ?? "80mm";
            }

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

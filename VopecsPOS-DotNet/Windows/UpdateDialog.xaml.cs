using System;
using System.Windows;
using VopecsPOS.Services;

namespace VopecsPOS.Windows
{
    public partial class UpdateDialog : Window
    {
        private readonly UpdateInfo _updateInfo;

        public UpdateDialog(UpdateInfo updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;

            VersionText.Text = $"Version {_updateInfo.LatestVersion} is now available.\nYou have version {_updateInfo.CurrentVersion}";
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
            {
                MessageBox.Show("Download URL not available. Please download manually from GitHub.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show progress
            ButtonPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;

            try
            {
                var installerPath = await UpdateService.DownloadUpdateAsync(
                    _updateInfo.DownloadUrl,
                    progress =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DownloadProgress.Value = progress;
                            ProgressText.Text = $"Downloading... {progress}%";
                        });
                    });

                if (!string.IsNullOrEmpty(installerPath))
                {
                    ProgressText.Text = "Installing...";
                    UpdateService.InstallUpdate(installerPath);
                }
                else
                {
                    MessageBox.Show("Failed to download update. Please try again later.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Show buttons again
                    ProgressPanel.Visibility = Visibility.Collapsed;
                    ButtonPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Update failed", ex);
                MessageBox.Show($"Update failed: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Show buttons again
                ProgressPanel.Visibility = Visibility.Collapsed;
                ButtonPanel.Visibility = Visibility.Visible;
            }
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

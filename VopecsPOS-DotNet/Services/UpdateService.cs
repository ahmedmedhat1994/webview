using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VopecsPOS.Services
{
    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public bool IsUpdateAvailable { get; set; }
    }

    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/ahmedmedhat1994/webview/releases/latest";
        private const string UserAgent = "VopecsPOS-Updater";
        private static readonly HttpClient _httpClient = new HttpClient();

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        public static string GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var updateInfo = new UpdateInfo
            {
                CurrentVersion = GetCurrentVersion(),
                IsUpdateAvailable = false
            };

            try
            {
                LogService.Info("Checking for updates...");
                var response = await _httpClient.GetStringAsync(GitHubApiUrl);
                var json = JObject.Parse(response);

                var tagName = json["tag_name"]?.ToString() ?? "";
                var latestVersion = tagName.TrimStart('v', 'V');
                updateInfo.LatestVersion = latestVersion;
                updateInfo.ReleaseNotes = json["body"]?.ToString() ?? "";

                // Find the .exe download URL
                var assets = json["assets"] as JArray;
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        var name = asset["name"]?.ToString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            updateInfo.DownloadUrl = asset["browser_download_url"]?.ToString() ?? "";
                            break;
                        }
                    }
                }

                // Compare versions
                if (IsNewerVersion(updateInfo.CurrentVersion, latestVersion))
                {
                    updateInfo.IsUpdateAvailable = true;
                    LogService.Info($"Update available: {latestVersion} (current: {updateInfo.CurrentVersion})");
                }
                else
                {
                    LogService.Info($"No update available. Current: {updateInfo.CurrentVersion}, Latest: {latestVersion}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to check for updates", ex);
            }

            return updateInfo;
        }

        private static bool IsNewerVersion(string current, string latest)
        {
            try
            {
                var currentParts = current.Split('.');
                var latestParts = latest.Split('.');

                for (int i = 0; i < Math.Max(currentParts.Length, latestParts.Length); i++)
                {
                    int currentNum = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;
                    int latestNum = i < latestParts.Length ? int.Parse(latestParts[i]) : 0;

                    if (latestNum > currentNum) return true;
                    if (latestNum < currentNum) return false;
                }
            }
            catch
            {
                // If parsing fails, assume no update
            }

            return false;
        }

        public static async Task<string?> DownloadUpdateAsync(string downloadUrl, Action<int>? progressCallback = null)
        {
            try
            {
                LogService.Info($"Downloading update from: {downloadUrl}");

                var tempPath = Path.Combine(Path.GetTempPath(), "VopecsPOS_Update.exe");

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var totalRead = 0L;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0 && progressCallback != null)
                            {
                                var progress = (int)((totalRead * 100) / totalBytes);
                                progressCallback(progress);
                            }
                        }
                    }
                }

                LogService.Info($"Update downloaded to: {tempPath}");
                return tempPath;
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to download update", ex);
                return null;
            }
        }

        public static void InstallUpdate(string installerPath)
        {
            try
            {
                LogService.Info($"Installing update: {installerPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // Exit current application
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to install update", ex);
            }
        }
    }
}

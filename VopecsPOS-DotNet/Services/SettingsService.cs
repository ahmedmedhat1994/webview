using System;
using System.IO;
using Newtonsoft.Json;

namespace VopecsPOS.Services
{
    public class SettingsService
    {
        private static SettingsService? _instance;
        private static readonly object _lock = new object();
        private readonly string _settingsPath;
        private AppSettings _settings;

        public static SettingsService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new SettingsService();
                    }
                }
                return _instance;
            }
        }

        private SettingsService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VopecsPOS"
            );

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _settingsPath = Path.Combine(appDataPath, "settings.json");
            _settings = LoadSettings();
        }

        public string? SavedUrl
        {
            get => _settings.SavedUrl;
            set
            {
                _settings.SavedUrl = value;
                SaveSettings();
            }
        }

        public string? LastVisitedUrl
        {
            get => _settings.LastVisitedUrl;
            set
            {
                _settings.LastVisitedUrl = value;
                SaveSettings();
            }
        }

        public string Language
        {
            get => _settings.Language ?? "en";
            set
            {
                _settings.Language = value;
                SaveSettings();
            }
        }

        public int PrintScale
        {
            get => _settings.PrintScale > 0 ? _settings.PrintScale : 80;
            set
            {
                _settings.PrintScale = value;
                SaveSettings();
            }
        }

        public string PaperSize
        {
            get => _settings.PaperSize ?? "80mm";
            set
            {
                _settings.PaperSize = value;
                SaveSettings();
            }
        }

        public string GetUrlToLoad()
        {
            return LastVisitedUrl ?? SavedUrl ?? string.Empty;
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
            return new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public void ClearAll()
        {
            _settings = new AppSettings();
            SaveSettings();
        }
    }

    public class AppSettings
    {
        public string? SavedUrl { get; set; }
        public string? LastVisitedUrl { get; set; }
        public string? Language { get; set; }
        public int PrintScale { get; set; } = 80;
        public string? PaperSize { get; set; } = "80mm";
    }
}

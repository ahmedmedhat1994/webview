using System;
using System.IO;

namespace VopecsPOS.Services
{
    public static class LogService
    {
        private static readonly string LogDirectory;
        private static readonly string LogFile;

        static LogService()
        {
            LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VopecsPOS",
                "logs"
            );

            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            // Log file with date
            LogFile = Path.Combine(LogDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex != null
                ? $"{message}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}"
                : message;
            Write("ERROR", fullMessage);
        }

        public static void Warning(string message)
        {
            Write("WARNING", message);
        }

        public static void Debug(string message)
        {
            Write("DEBUG", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static string GetLogFilePath() => LogFile;

        public static string GetLogDirectory() => LogDirectory;
    }
}

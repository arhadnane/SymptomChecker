using System;
using System.IO;
using System.Text;

namespace SymptomCheckerApp.Services
{
    public class LoggerService
    {
        private readonly string _logDirectory;
        private readonly string _currentLogPath;
        private readonly object _lock = new();
        private const int MaxFiles = 5; // keep last 5 logs
        private const long MaxSizeBytes = 512 * 1024; // 512 KB per file

        public LoggerService(string logDirectory)
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);
            _currentLogPath = Path.Combine(_logDirectory, $"log_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
            PruneOldLogs();
        }

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message, Exception? ex = null) => Write("ERROR", message + (ex != null ? Environment.NewLine + ex : string.Empty));

        private void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();
                    var line = $"{DateTime.UtcNow:O}\t{level}\t{message}";
                    File.AppendAllText(_currentLogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* swallow logging errors */ }
        }

        private void RotateIfNeeded()
        {
            try
            {
                if (File.Exists(_currentLogPath))
                {
                    var fi = new FileInfo(_currentLogPath);
                    if (fi.Length > MaxSizeBytes)
                    {
                        // Start a new file
                        File.Move(_currentLogPath, _currentLogPath + ".old", true);
                    }
                }
            }
            catch { }
        }

        private void PruneOldLogs()
        {
            try
            {
                var files = new DirectoryInfo(_logDirectory).GetFiles("log_*.txt")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .ToList();
                for (int i = MaxFiles; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }
    }
}

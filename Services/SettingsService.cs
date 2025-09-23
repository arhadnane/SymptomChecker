using System.Text.Json;

namespace SymptomCheckerApp.Services;

public class SettingsService
{
    public class AppSettings
    {
        public string? Language { get; set; }
        public bool DarkMode { get; set; }
        public string? Model { get; set; }
        public int ThresholdPercent { get; set; }
        public int MinMatch { get; set; }
        public int TopK { get; set; }
        public bool ShowOnlyCategory { get; set; }
        public string? SelectedCategory { get; set; }
        public string? FilterText { get; set; }
        // Vitals (0 or null means not set)
        public double? TempC { get; set; }
        public int? HeartRate { get; set; }
        public int? RespRate { get; set; }
        public int? SystolicBP { get; set; }
        public int? DiastolicBP { get; set; }
        public int? SpO2 { get; set; }
        public double? WeightKg { get; set; }
        // Decision rules context
        public int? AgeYears { get; set; }
    }

    private readonly string _settingsPath;
    public AppSettings Settings { get; private set; } = new();

    public SettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        Load();
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) { Settings = new AppSettings(); return; }
            var json = File.ReadAllText(_settingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
}

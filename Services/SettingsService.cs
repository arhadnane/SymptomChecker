using System.Text.Json;

namespace SymptomCheckerApp.Services;

public class SettingsService
{
    private static AppSettings CreateDefaultSettings() => new()
    {
        Language = "en",
        DarkMode = false,
        Model = SymptomCheckerService.DetectionModel.Ensemble.ToString(),
        ThresholdPercent = 0,
        MinMatch = 1,
        TopK = 0,
        ShowOnlyCategory = false,
        OllamaUrl = OllamaService.DefaultBaseUrl,
        OllamaModel = OllamaService.DefaultModelName
    };

    private static AppSettings ApplyMissingDefaults(AppSettings settings)
    {
        settings.Language ??= "en";
        settings.Model ??= SymptomCheckerService.DetectionModel.Ensemble.ToString();
        settings.OllamaUrl ??= OllamaService.DefaultBaseUrl;
        settings.OllamaModel ??= OllamaService.DefaultModelName;
        return settings;
    }

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
        // PERC flags (true means risk factor present)
        public bool? PercHemoptysis { get; set; }
        public bool? PercEstrogenUse { get; set; }
        public bool? PercPriorDvtPe { get; set; }
        public bool? PercUnilateralLegSwelling { get; set; }
        public bool? PercRecentSurgeryTrauma { get; set; }
        public string? LastExportFolder { get; set; }
        public Dictionary<string,double>? CategoryWeights { get; set; }
        public double? NaiveBayesTemperature { get; set; }
        // Ollama AI settings
        public string? OllamaUrl { get; set; }
        public string? OllamaModel { get; set; }
        public bool AutoAi { get; set; }
        // UI layout prefs
        public bool? LeftPanelCollapsed { get; set; }
        // Guided Diagnosis Assistant (spec 001-guided-diagnosis-ux). All
        // additive and nullable so older settings.json files load unchanged.
        public Models.UiMode? UiMode { get; set; }
        public int? PatientWizardLastStep { get; set; }
        public Dictionary<string, bool>? CollapsedSections { get; set; }
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
            if (!File.Exists(_settingsPath)) { Settings = CreateDefaultSettings(); return; }
            var json = File.ReadAllText(_settingsPath);
            Settings = ApplyMissingDefaults(JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? CreateDefaultSettings());
        }
        catch
        {
            Settings = CreateDefaultSettings();
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

    public void Reset()
    {
        Settings = CreateDefaultSettings();
        Save();
    }
}

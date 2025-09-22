using System.Text.Json;

namespace SymptomChecker.Services;

public class TranslationService
{
    public record UiString(string Key, string En, string Fr, string Ar);
    public record SymptomString(string Key, string? Fr, string? Ar);
    public record ConditionString(string Key, string? Fr, string? Ar);
    public record MessageString(string Key, string En, string Fr, string Ar);
    public record CategoryString(string Key, string Fr, string Ar);
    public record UiDetailsString(string Key, string En, string Fr, string Ar);

    public class TranslationDatabase
    {
        public List<string> Languages { get; set; } = new();
        public List<UiString> Ui { get; set; } = new();
        public List<SymptomString> Symptoms { get; set; } = new();
        public List<ConditionString> Conditions { get; set; } = new();
        public List<MessageString> Messages { get; set; } = new();
    public List<CategoryString> Categories { get; set; } = new();
    public List<UiDetailsString> Ui_Details { get; set; } = new();
    }

    private readonly TranslationDatabase _db;
    public string CurrentLanguage { get; private set; } = "en";
    private readonly HashSet<string> _missing = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> MissingKeys => _missing;

    public TranslationService(string path)
    {
        if (!File.Exists(path))
        {
            _db = new TranslationDatabase();
            return;
        }
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _db = JsonSerializer.Deserialize<TranslationDatabase>(json, options) ?? new TranslationDatabase();
    }

    public IEnumerable<string> GetSupportedLanguages() => _db.Languages.Count > 0 ? _db.Languages : new[] { "en", "fr", "ar" };

    public void SetLanguage(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return;
        CurrentLanguage = lang.ToLowerInvariant();
    }

    public string T(string key)
    {
        // UI translations by key
        var ui = _db.Ui.FirstOrDefault(u => string.Equals(u.Key, key, StringComparison.OrdinalIgnoreCase));
        if (ui != null)
        {
            var val = CurrentLanguage switch
            {
                "fr" => string.IsNullOrEmpty(ui.Fr) ? ui.En : ui.Fr,
                "ar" => string.IsNullOrEmpty(ui.Ar) ? ui.En : ui.Ar,
                _ => ui.En
            } ?? key;
            if ((CurrentLanguage == "ar" && string.IsNullOrEmpty(ui.Ar)) || (CurrentLanguage == "fr" && string.IsNullOrEmpty(ui.Fr))) _missing.Add($"ui:{key}:{CurrentLanguage}");
            return val;
        }
        // Messages fallback
        var msg = _db.Messages.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
        if (msg != null)
        {
            var val = CurrentLanguage switch
            {
                "fr" => string.IsNullOrEmpty(msg.Fr) ? msg.En : msg.Fr,
                "ar" => string.IsNullOrEmpty(msg.Ar) ? msg.En : msg.Ar,
                _ => msg.En
            } ?? key;
            if ((CurrentLanguage == "ar" && string.IsNullOrEmpty(msg.Ar)) || (CurrentLanguage == "fr" && string.IsNullOrEmpty(msg.Fr))) _missing.Add($"msg:{key}:{CurrentLanguage}");
            return val;
        }
        return key;
    }

    public string TDetails(string key)
    {
        var d = _db.Ui_Details.FirstOrDefault(u => string.Equals(u.Key, key, StringComparison.OrdinalIgnoreCase));
        if (d != null)
        {
            var val = CurrentLanguage switch
            {
                "fr" => string.IsNullOrEmpty(d.Fr) ? d.En : d.Fr,
                "ar" => string.IsNullOrEmpty(d.Ar) ? d.En : d.Ar,
                _ => d.En
            } ?? key;
            if ((CurrentLanguage == "ar" && string.IsNullOrEmpty(d.Ar)) || (CurrentLanguage == "fr" && string.IsNullOrEmpty(d.Fr))) _missing.Add($"uidet:{key}:{CurrentLanguage}");
            return val;
        }
        return key;
    }

    public string Symptom(string canonical)
    {
        var s = _db.Symptoms.FirstOrDefault(x => string.Equals(x.Key, canonical, StringComparison.OrdinalIgnoreCase));
        if (s == null) { _missing.Add($"sym:{canonical}:{CurrentLanguage}"); return canonical; }
        var val = CurrentLanguage switch
        {
            "fr" => string.IsNullOrEmpty(s.Fr) ? canonical : s.Fr!,
            "ar" => string.IsNullOrEmpty(s.Ar) ? canonical : s.Ar!,
            _ => canonical
        };
        if ((CurrentLanguage == "ar" && string.IsNullOrEmpty(s.Ar)) || (CurrentLanguage == "fr" && string.IsNullOrEmpty(s.Fr))) _missing.Add($"sym:{canonical}:{CurrentLanguage}");
        return val;
    }

    public string Condition(string canonical)
    {
        var c = _db.Conditions.FirstOrDefault(x => string.Equals(x.Key, canonical, StringComparison.OrdinalIgnoreCase));
        if (c == null) { _missing.Add($"cond:{canonical}:{CurrentLanguage}"); return canonical; }
        var val = CurrentLanguage switch
        {
            "fr" => string.IsNullOrEmpty(c.Fr) ? canonical : c.Fr!,
            "ar" => string.IsNullOrEmpty(c.Ar) ? canonical : c.Ar!,
            _ => canonical
        };
        if ((CurrentLanguage == "ar" && string.IsNullOrEmpty(c.Ar)) || (CurrentLanguage == "fr" && string.IsNullOrEmpty(c.Fr))) _missing.Add($"cond:{canonical}:{CurrentLanguage}");
        return val;
    }

    public string Category(string canonical)
    {
        var c = _db.Categories.FirstOrDefault(x => string.Equals(x.Key, canonical, StringComparison.OrdinalIgnoreCase));
        if (c == null) { _missing.Add($"cat:{canonical}:{CurrentLanguage}"); return canonical; }
        var val = CurrentLanguage switch
        {
            "fr" => string.IsNullOrEmpty(c.Fr) ? canonical : c.Fr,
            "ar" => string.IsNullOrEmpty(c.Ar) ? canonical : c.Ar,
            _ => canonical
        };
        if ((CurrentLanguage == "ar" && string.IsNullOrEmpty(c.Ar)) || (CurrentLanguage == "fr" && string.IsNullOrEmpty(c.Fr))) _missing.Add($"cat:{canonical}:{CurrentLanguage}");
        return val;
    }

    public void SaveMissingReport(string path)
    {
        try
        {
            if (_missing.Count == 0) return;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllLines(path, _missing.OrderBy(x => x));
        }
        catch { }
    }
}

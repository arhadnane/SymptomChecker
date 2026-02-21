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

    // O(1) lookup dictionaries built from flat lists
    private readonly Dictionary<string, UiString> _uiMap;
    private readonly Dictionary<string, MessageString> _msgMap;
    private readonly Dictionary<string, UiDetailsString> _detailsMap;
    private readonly Dictionary<string, SymptomString> _symptomMap;
    private readonly Dictionary<string, ConditionString> _conditionMap;
    private readonly Dictionary<string, CategoryString> _categoryMap;

    public TranslationService(string path)
    {
        if (!File.Exists(path))
        {
            _db = new TranslationDatabase();
        }
        else
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _db = JsonSerializer.Deserialize<TranslationDatabase>(json, options) ?? new TranslationDatabase();
        }

        // Build O(1) dictionaries
        var cmp = StringComparer.OrdinalIgnoreCase;
        _uiMap = new Dictionary<string, UiString>(cmp);
        foreach (var u in _db.Ui)
            if (!string.IsNullOrEmpty(u.Key) && !_uiMap.ContainsKey(u.Key))
                _uiMap[u.Key] = u;

        _msgMap = new Dictionary<string, MessageString>(cmp);
        foreach (var m in _db.Messages)
            if (!string.IsNullOrEmpty(m.Key) && !_msgMap.ContainsKey(m.Key))
                _msgMap[m.Key] = m;

        _detailsMap = new Dictionary<string, UiDetailsString>(cmp);
        foreach (var d in _db.Ui_Details)
            if (!string.IsNullOrEmpty(d.Key) && !_detailsMap.ContainsKey(d.Key))
                _detailsMap[d.Key] = d;

        _symptomMap = new Dictionary<string, SymptomString>(cmp);
        foreach (var s in _db.Symptoms)
            if (!string.IsNullOrEmpty(s.Key) && !_symptomMap.ContainsKey(s.Key))
                _symptomMap[s.Key] = s;

        _conditionMap = new Dictionary<string, ConditionString>(cmp);
        foreach (var c in _db.Conditions)
            if (!string.IsNullOrEmpty(c.Key) && !_conditionMap.ContainsKey(c.Key))
                _conditionMap[c.Key] = c;

        _categoryMap = new Dictionary<string, CategoryString>(cmp);
        foreach (var c in _db.Categories)
            if (!string.IsNullOrEmpty(c.Key) && !_categoryMap.ContainsKey(c.Key))
                _categoryMap[c.Key] = c;
    }

    public IEnumerable<string> GetSupportedLanguages() => _db.Languages.Count > 0 ? _db.Languages : new[] { "en", "fr", "ar" };

    public void SetLanguage(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return;
        CurrentLanguage = lang.ToLowerInvariant();
    }

    public string T(string key)
    {
        // UI translations by key (O(1))
        if (_uiMap.TryGetValue(key, out var ui))
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
        // Messages fallback (O(1))
        if (_msgMap.TryGetValue(key, out var msg))
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
        if (_detailsMap.TryGetValue(key, out var d))
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
        if (!_symptomMap.TryGetValue(canonical, out var s))
        {
            _missing.Add($"sym:{canonical}:{CurrentLanguage}");
            return canonical;
        }
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
        if (!_conditionMap.TryGetValue(canonical, out var c))
        {
            _missing.Add($"cond:{canonical}:{CurrentLanguage}");
            return canonical;
        }
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
        if (!_categoryMap.TryGetValue(canonical, out var c))
        {
            _missing.Add($"cat:{canonical}:{CurrentLanguage}");
            return canonical;
        }
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

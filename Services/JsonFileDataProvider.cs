using System;
using System.IO;
using System.Text.Json;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Default IDataProvider that reads/writes JSON files from disk.
    /// </summary>
    public class JsonFileDataProvider : IDataProvider
    {
        private readonly string _dataDir;
        private static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        public JsonFileDataProvider(string dataDirectory)
        {
            _dataDir = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        public ConditionDatabase LoadConditions()
        {
            var path = Path.Combine(_dataDir, "conditions.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Conditions JSON not found at '{path}'");

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConditionDatabase>(json, _readOptions) ?? new ConditionDatabase();
        }

        public void SaveConditions(ConditionDatabase db)
        {
            var path = Path.Combine(_dataDir, "conditions.json");
            var json = JsonSerializer.Serialize(db, _writeOptions);
            File.WriteAllText(path, json);
        }

        public SymptomCategoryDatabase LoadCategories()
        {
            var path = Path.Combine(_dataDir, "categories.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Categories JSON not found at '{path}'");

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SymptomCategoryDatabase>(json, _readOptions) ?? new SymptomCategoryDatabase();
        }

        public SynonymDatabase LoadSynonyms()
        {
            var path = Path.Combine(_dataDir, "synonyms.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Synonyms JSON not found at '{path}'");

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SynonymDatabase>(json, _readOptions) ?? new SynonymDatabase();
        }
    }
}

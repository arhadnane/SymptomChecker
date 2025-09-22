using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    public class CategoriesService
    {
        private readonly SymptomCategoryDatabase _db;
        private readonly StringComparison _cmp = StringComparison.OrdinalIgnoreCase;

        public CategoriesService(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"Categories JSON not found at '{jsonPath}'");
            }
            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _db = JsonSerializer.Deserialize<SymptomCategoryDatabase>(json, options) ?? new SymptomCategoryDatabase();
        }

        public IReadOnlyList<SymptomCategory> GetAllCategories() => _db.Categories;

        // Build the set of symptoms matching a category from a global vocabulary.
        // If a category has an explicit Symptoms list, use it; otherwise infer by keywords.
        public HashSet<string> BuildCategorySet(SymptomCategory cat, IEnumerable<string> vocabulary)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (cat.Symptoms != null && cat.Symptoms.Count > 0)
            {
                foreach (var s in cat.Symptoms)
                {
                    if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
                }
                return set;
            }
            var kws = (cat.Keywords ?? new List<string>()).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            foreach (var sym in vocabulary)
            {
                foreach (var k in kws)
                {
                    if (sym.IndexOf(k, _cmp) >= 0)
                    {
                        set.Add(sym);
                        break;
                    }
                }
            }
            return set;
        }
    }
}

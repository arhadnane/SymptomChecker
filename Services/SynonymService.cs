using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    public class SynonymService
    {
        private readonly SynonymDatabase _db;
        private readonly StringComparer _cmp = StringComparer.OrdinalIgnoreCase;

        public SynonymService(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"Synonyms JSON not found at '{jsonPath}'");
            }
            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _db = JsonSerializer.Deserialize<SynonymDatabase>(json, options) ?? new SynonymDatabase();
        }

        // Build a map of alias -> canonical (case-insensitive), only for aliases that appear in vocabulary
        public Dictionary<string, string> BuildAliasToCanonical(IEnumerable<string> vocabulary)
        {
            var vocabSet = new HashSet<string>(vocabulary, _cmp);
            var map = new Dictionary<string, string>(_cmp);
            foreach (var entry in _db.Synonyms)
            {
                if (string.IsNullOrWhiteSpace(entry.Canonical)) continue;
                foreach (var alias in entry.Aliases ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(alias)) continue;
                    // Record mapping even if alias isn't in vocab, for filter matching
                    if (!map.ContainsKey(alias))
                    {
                        map[alias] = entry.Canonical;
                    }
                }
                // Ensure canonical maps to itself as well
                if (!map.ContainsKey(entry.Canonical))
                {
                    map[entry.Canonical] = entry.Canonical;
                }
            }
            return map;
        }

        // Expand a query term via synonyms; return all vocabulary items that match the term or its synonyms
        public IEnumerable<string> MatchSymptomsByQuery(string query, IEnumerable<string> vocabulary)
        {
            if (string.IsNullOrWhiteSpace(query)) return vocabulary;
            var vocab = vocabulary.ToList();
            var q = query.Trim();
            var map = BuildAliasToCanonical(vocab);

            // If query matches an alias or canonical, prefer filtering to the mapped canonical/aliases; otherwise substring match
            var possible = new HashSet<string>(_cmp);

            foreach (var kv in map)
            {
                if (kv.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var canonical = kv.Value;
                    // Include all vocab items that equal canonical or alias maps to canonical
                    foreach (var v in vocab)
                    {
                        if (string.Equals(v, canonical, StringComparison.OrdinalIgnoreCase)) possible.Add(v);
                        // Also, if vocab entry is alias that maps to canonical
                        if (string.Equals(map.TryGetValue(v, out var can) ? can : v, canonical, StringComparison.OrdinalIgnoreCase))
                        {
                            possible.Add(v);
                        }
                    }
                }
            }

            if (possible.Count == 0)
            {
                // Fallback to substring match on vocab
                foreach (var v in vocab)
                {
                    if (v.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) possible.Add(v);
                }
            }
            return possible;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    public class SymptomCheckerService
    {
        private readonly ConditionDatabase _db;
        private readonly string _jsonPath;
        private List<string> _vocabulary; // all unique symptoms
        private Dictionary<string, HashSet<string>> _conditionSets = new(StringComparer.OrdinalIgnoreCase); // cached per-condition symptom sets

        // Model registry: maps DetectionModel enum to IMatchingModel implementation
        private readonly Dictionary<DetectionModel, IMatchingModel> _models;

        /// <summary>All registered matching models.</summary>
        public IReadOnlyDictionary<DetectionModel, IMatchingModel> Models => _models;

        public SymptomCheckerService(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"JSON data file not found at '{jsonPath}'");
            }

            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            _db = JsonSerializer.Deserialize<ConditionDatabase>(json, options) ?? new ConditionDatabase();
            _jsonPath = jsonPath;
            _vocabulary = new List<string>();

            // Register default models
            _models = new Dictionary<DetectionModel, IMatchingModel>
            {
                { DetectionModel.Jaccard, new JaccardModel() },
                { DetectionModel.Cosine, new CosineModel() },
                { DetectionModel.NaiveBayes, new NaiveBayesModel() }
            };

            RebuildVocabulary();
        }

        public IReadOnlyCollection<string> GetAllUniqueSymptoms()
        {
            return _vocabulary.ToArray();
        }

        public List<string> CheckConditions(IEnumerable<string> selectedSymptoms, int threshold = 2)
        {
            var selected = selectedSymptoms
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var results = new List<string>();

            foreach (var condition in _db.Conditions)
            {
                int matchCount = condition.Symptoms.Count(symptom => selected.Contains(symptom));
                if (matchCount >= threshold)
                {
                    results.Add(condition.Name);
                }
            }

            return results;
        }

        // Mathematical detection models
        public enum DetectionModel
        {
            Jaccard,
            Cosine,
            NaiveBayes
        }

        // Get scored results for a given model. threshold is interpreted as:
        // - Jaccard/Cosine: minimum score [0..1]
        // - NaiveBayes: minimum normalized probability [0..1]
        public List<ConditionMatch> GetMatches(
            IEnumerable<string> selectedSymptoms,
            DetectionModel model,
            double threshold = 0.0,
            int? topK = null,
            int minMatchCount = 0,
            IReadOnlyDictionary<string,double>? categoryWeights = null,
            double? naiveBayesTemperature = null,
            Func<string, IEnumerable<string>>? getConditionCategories = null)
        {
            var selectedSet = selectedSymptoms
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // If nothing is selected, do not suggest any condition.
            if (selectedSet.Count == 0)
            {
                return new List<ConditionMatch>();
            }

            // Delegate to the registered IMatchingModel
            if (!_models.TryGetValue(model, out var matchingModel))
            {
                throw new NotSupportedException($"Model '{model}' not supported.");
            }

            var options = new MatchingOptions
            {
                NaiveBayesTemperature = naiveBayesTemperature
            };

            var results = matchingModel.ComputeMatches(
                selectedSet,
                _db.Conditions,
                _conditionSets,
                _vocabulary,
                threshold,
                options);

            // Apply minimum match count filter
            if (minMatchCount > 0)
            {
                results = results.Where(r => r.MatchCount >= minMatchCount).ToList();
            }

            // Apply category weighting before final ordering
            if (categoryWeights != null && categoryWeights.Count > 0 && getConditionCategories != null)
            {
                foreach (var r in results)
                {
                    try
                    {
                        double bestWeight = 1.0;
                        var cats = getConditionCategories(r.Name) ?? Array.Empty<string>();
                        foreach (var c in cats)
                        {
                            if (categoryWeights.TryGetValue(c, out var w) && w > bestWeight) bestWeight = w;
                        }
                        r.Score *= bestWeight;
                    }
                    catch { }
                }
                // Renormalize if NaiveBayes after weighting to keep probabilistic interpretation
                if (model == DetectionModel.NaiveBayes)
                {
                    double sumW = results.Sum(r => r.Score);
                    if (sumW > 0) foreach (var r in results) r.Score /= sumW;
                }
            }

            // Order by score desc, then match count desc, then name
            results = results
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.MatchCount)
                .ThenBy(r => r.Name)
                .ToList();

            if (topK.HasValue && topK.Value > 0 && results.Count > topK.Value)
            {
                results = results.Take(topK.Value).ToList();
            }

            return results;
        }

        public void RebuildVocabulary()
        {
            _vocabulary = _db.Conditions
                .SelectMany(c => c.Symptoms)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();
            // Rebuild condition sets cache
            _conditionSets.Clear();
            foreach (var c in _db.Conditions)
            {
                _conditionSets[c.Name] = c.Symptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        public int MergeConditions(IEnumerable<Condition> newConditions)
        {
            int changes = 0;
            var comparer = StringComparer.OrdinalIgnoreCase;
            var dict = _db.Conditions
                .GroupBy(c => c.Name, comparer)
                .ToDictionary(g => g.Key, g => g.First(), comparer);

            foreach (var nc in newConditions)
            {
                if (string.IsNullOrWhiteSpace(nc.Name)) continue;
                if (!dict.TryGetValue(nc.Name, out var existing))
                {
                    // Add brand new condition
                    var uniqueSymptoms = nc.Symptoms.Distinct(comparer).OrderBy(s => s, comparer).ToList();
                    _db.Conditions.Add(new Condition { Name = nc.Name.Trim(), Symptoms = uniqueSymptoms });
                    dict[nc.Name.Trim()] = _db.Conditions[^1];
                    changes++;
                }
                else
                {
                    // Merge symptoms
                    var before = existing.Symptoms.Count;
                    existing.Symptoms = existing.Symptoms
                        .Concat(nc.Symptoms)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(comparer)
                        .OrderBy(s => s, comparer)
                        .ToList();
                    if (existing.Symptoms.Count > before)
                    {
                        changes++;
                    }
                }
            }

            if (changes > 0)
            {
                RebuildVocabulary(); // also rebuilds cached sets
            }

            return changes;
        }

        public bool TryGetCondition(string name, out Condition? condition)
        {
            condition = _db.Conditions.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            return condition != null;
        }

        public void SaveDatabase(string? path = null)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_db, options);
            File.WriteAllText(path ?? _jsonPath, json);
        }
    }
}

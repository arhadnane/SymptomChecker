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
            int minMatchCount = 0)
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

            var results = new List<ConditionMatch>();

            switch (model)
            {
                case DetectionModel.Jaccard:
                    foreach (var c in _db.Conditions)
                    {
                        var condSet = c.Symptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        int inter = condSet.Intersect(selectedSet, StringComparer.OrdinalIgnoreCase).Count();
                        int union = condSet.Union(selectedSet, StringComparer.OrdinalIgnoreCase).Count();
                        double score = union == 0 ? 0 : (double)inter / union;
                        if (score >= threshold)
                        {
                            results.Add(new ConditionMatch { Name = c.Name, Score = score, MatchCount = inter });
                        }
                    }
                    break;
                case DetectionModel.Cosine:
                    // Binary vector cosine similarity
                    foreach (var c in _db.Conditions)
                    {
                        var condSet = c.Symptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        int dot = condSet.Intersect(selectedSet, StringComparer.OrdinalIgnoreCase).Count();
                        double denom = Math.Sqrt(condSet.Count) * Math.Sqrt(selectedSet.Count);
                        double score = denom == 0 ? 0 : dot / denom;
                        if (score >= threshold)
                        {
                            results.Add(new ConditionMatch { Name = c.Name, Score = score, MatchCount = dot });
                        }
                    }
                    break;
                case DetectionModel.NaiveBayes:
                    // Simple Bernoulli Naive Bayes with Laplace smoothing.
                    // P(c|S) ∝ P(c) * Π_{sym in V} P(x_sym|c), where x_sym=1 for selected, else 0
                    // Use equal priors and normalize across conditions.
                    int V = _vocabulary.Count;
                    // Per condition, estimate P(sym=1|c) as (#sym in condition + 1) / (1 + 2) with Bernoulli smoothing.
                    // Here, feature presence is deterministic for our dataset: symptom present or not.
                    // We set p_present = (1 + 1) / (2 + 2) if present else (0 + 1) / (2 + 2) to avoid 0/1 extremes.
                    // Alternatively, use p_present = 0.8 if present, 0.2 otherwise (heuristic). We'll use Laplace Bernoulli below.
                    var condScores = new List<(string name, double logProb, int matchCount)>();
                    foreach (var c in _db.Conditions)
                    {
                        var condSet = c.Symptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);
                        double logP = 0.0; // log prior same for all → omitted
                        int matchCount = condSet.Intersect(selectedSet, StringComparer.OrdinalIgnoreCase).Count();

                        foreach (var sym in _vocabulary)
                        {
                            bool presentInCond = condSet.Contains(sym);
                            // Laplace: P(x=1|c) = (count_present + 1) / (N + 2). Here count_present is 1 if presentInCond else 0, N=1 feature per symptom.
                            double p1 = (presentInCond ? 2.0 : 1.0) / 3.0; // (1+1)/3=0.666.. if present, (0+1)/3≈0.333.. if absent
                            double p0 = 1 - p1;

                            bool selected = selectedSet.Contains(sym);
                            logP += Math.Log(selected ? p1 : p0);
                        }
                        condScores.Add((c.Name, logP, matchCount));
                    }
                    // Normalize log probs to [0,1] via softmax
                    double maxLog = condScores.Max(t => t.logProb);
                    var soft = condScores.Select(t => (t.name, Math.Exp(t.logProb - maxLog), t.matchCount)).ToList();
                    double Z = soft.Sum(t => t.Item2);
                    foreach (var (name, exp, matchCount) in soft)
                    {
                        double prob = Z == 0 ? 0 : exp / Z;
                        if (prob >= threshold)
                        {
                            results.Add(new ConditionMatch { Name = name, Score = prob, MatchCount = matchCount });
                        }
                    }
                    break;
                default:
                    throw new NotSupportedException($"Model '{model}' not supported.");
            }

            // Apply minimum match count filter
            if (minMatchCount > 0)
            {
                results = results.Where(r => r.MatchCount >= minMatchCount).ToList();
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
                RebuildVocabulary();
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

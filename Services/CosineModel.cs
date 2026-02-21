using System;
using System.Collections.Generic;
using System.Linq;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Binary-vector cosine similarity: dot(A,B) / (|A| * |B|)
    /// </summary>
    public class CosineModel : IMatchingModel
    {
        public string Name => "Cosine";

        public List<ConditionMatch> ComputeMatches(
            HashSet<string> selectedSymptoms,
            IReadOnlyList<Condition> conditions,
            IReadOnlyDictionary<string, HashSet<string>> conditionSets,
            IReadOnlyList<string> vocabulary,
            double threshold,
            MatchingOptions? options = null)
        {
            var results = new List<ConditionMatch>();

            foreach (var c in conditions)
            {
                if (!conditionSets.TryGetValue(c.Name, out var condSet)) continue;

                var interSymptoms = condSet.Intersect(selectedSymptoms, StringComparer.OrdinalIgnoreCase).ToList();
                int dot = interSymptoms.Count;
                double denom = Math.Sqrt(condSet.Count) * Math.Sqrt(selectedSymptoms.Count);
                double score = denom == 0 ? 0 : dot / denom;

                if (score >= threshold)
                {
                    results.Add(new ConditionMatch
                    {
                        Name = c.Name,
                        Score = score,
                        MatchCount = dot,
                        MatchedSymptoms = interSymptoms
                    });
                }
            }

            return results;
        }
    }
}

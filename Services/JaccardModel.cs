using System;
using System.Collections.Generic;
using System.Linq;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Jaccard similarity: |A ∩ B| / |A ∪ B|
    /// </summary>
    public class JaccardModel : IMatchingModel
    {
        public string Name => "Jaccard";

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
                int inter = interSymptoms.Count;
                int union = condSet.Union(selectedSymptoms, StringComparer.OrdinalIgnoreCase).Count();
                double score = union == 0 ? 0 : (double)inter / union;

                if (score >= threshold)
                {
                    results.Add(new ConditionMatch
                    {
                        Name = c.Name,
                        Score = score,
                        MatchCount = inter,
                        MatchedSymptoms = interSymptoms
                    });
                }
            }

            return results;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Combines the similarity-based and probabilistic matchers into a single
    /// consensus score. Each base model is normalized against its own top score
    /// for the current query, then the normalized scores are averaged.
    /// </summary>
    public sealed class EnsembleModel : IMatchingModel
    {
        private static readonly IMatchingModel[] BaseModels =
        {
            new JaccardModel(),
            new CosineModel(),
            new NaiveBayesModel()
        };

        public string Name => "Ensemble";

        public List<ConditionMatch> ComputeMatches(
            HashSet<string> selectedSymptoms,
            IReadOnlyList<Condition> conditions,
            IReadOnlyDictionary<string, HashSet<string>> conditionSets,
            IReadOnlyList<string> vocabulary,
            double threshold,
            MatchingOptions? options = null)
        {
            var accumulators = new Dictionary<string, Accumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (var condition in conditions)
            {
                if (!conditionSets.TryGetValue(condition.Name, out var conditionSymptoms))
                {
                    continue;
                }

                var matchedSymptoms = conditionSymptoms
                    .Intersect(selectedSymptoms, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                accumulators[condition.Name] = new Accumulator
                {
                    Name = condition.Name,
                    MatchCount = matchedSymptoms.Count,
                    MatchedSymptoms = matchedSymptoms
                };
            }

            foreach (var model in BaseModels)
            {
                var modelResults = model.ComputeMatches(
                    selectedSymptoms,
                    conditions,
                    conditionSets,
                    vocabulary,
                    threshold: 0.0,
                    options);

                double maxScore = modelResults.Count == 0 ? 0.0 : modelResults.Max(result => result.Score);

                foreach (var result in modelResults)
                {
                    if (!accumulators.TryGetValue(result.Name, out var accumulator))
                    {
                        continue;
                    }

                    accumulator.ScoreSum += maxScore > 0.0 ? result.Score / maxScore : 0.0;
                    accumulator.MatchCount = result.MatchCount;
                    accumulator.MatchedSymptoms = result.MatchedSymptoms;
                }
            }

            double divisor = BaseModels.Length;

            return accumulators.Values
                .Select(accumulator => new ConditionMatch
                {
                    Name = accumulator.Name,
                    Score = accumulator.ScoreSum / divisor,
                    MatchCount = accumulator.MatchCount,
                    MatchedSymptoms = accumulator.MatchedSymptoms
                })
                .Where(result => result.Score >= threshold)
                .ToList();
        }

        private sealed class Accumulator
        {
            public string Name { get; set; } = string.Empty;
            public double ScoreSum { get; set; }
            public int MatchCount { get; set; }
            public List<string> MatchedSymptoms { get; set; } = new();
        }
    }
}
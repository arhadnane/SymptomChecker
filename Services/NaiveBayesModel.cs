using System;
using System.Collections.Generic;
using System.Linq;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.Services
{
    /// <summary>
    /// Bernoulli Naive Bayes with Laplace smoothing and optional temperature scaling.
    /// P(c|S) ∝ P(c) · Π P(x_sym|c), normalized via softmax.
    /// </summary>
    public class NaiveBayesModel : IMatchingModel
    {
        public string Name => "NaiveBayes";

        public List<ConditionMatch> ComputeMatches(
            HashSet<string> selectedSymptoms,
            IReadOnlyList<Condition> conditions,
            IReadOnlyDictionary<string, HashSet<string>> conditionSets,
            IReadOnlyList<string> vocabulary,
            double threshold,
            MatchingOptions? options = null)
        {
            var results = new List<ConditionMatch>();
            double temp = (options?.NaiveBayesTemperature.HasValue == true && options.NaiveBayesTemperature.Value > 0.01)
                ? options.NaiveBayesTemperature.Value
                : 1.0;

            var condScores = new List<(string name, double logProb, int matchCount, List<string> matched)>();

            foreach (var c in conditions)
            {
                if (!conditionSets.TryGetValue(c.Name, out var condSet)) continue;

                double logP = 0.0;
                var matched = condSet.Intersect(selectedSymptoms, StringComparer.OrdinalIgnoreCase).ToList();
                int matchCount = matched.Count;

                foreach (var sym in vocabulary)
                {
                    bool presentInCond = condSet.Contains(sym);
                    // Laplace: P(x=1|c) = (count_present + 1) / (N + 2)
                    double p1 = (presentInCond ? 2.0 : 1.0) / 3.0;
                    double p0 = 1 - p1;

                    bool selected = selectedSymptoms.Contains(sym);
                    logP += Math.Log(selected ? p1 : p0);
                }

                condScores.Add((c.Name, logP, matchCount, matched));
            }

            if (condScores.Count == 0) return results;

            // Normalize via softmax
            double maxLog = condScores.Max(t => t.logProb);
            var soft = condScores
                .Select(t => (t.name, exp: Math.Exp(t.logProb - maxLog), t.matchCount, t.matched))
                .ToList();
            double Z = soft.Sum(t => t.exp);

            foreach (var (name, exp, matchCount, matched) in soft)
            {
                double probRaw = Z == 0 ? 0 : exp / Z;
                double prob = probRaw;

                if (Math.Abs(temp - 1.0) > 1e-6)
                {
                    prob = Math.Pow(probRaw, 1.0 / temp);
                }

                if (prob >= threshold)
                {
                    results.Add(new ConditionMatch
                    {
                        Name = name,
                        Score = prob,
                        MatchCount = matchCount,
                        MatchedSymptoms = matched
                    });
                }
            }

            // If temperature scaling used, renormalize
            if (results.Count > 0 && Math.Abs(temp - 1.0) > 1e-6)
            {
                double sumT = results.Sum(r => r.Score);
                if (sumT > 0)
                {
                    foreach (var r in results) r.Score /= sumT;
                }
            }

            return results;
        }
    }
}

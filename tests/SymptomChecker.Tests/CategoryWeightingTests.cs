using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class CategoryWeightingTests
    {
        private static string DataPath(string file) => Path.Combine(AppContext.BaseDirectory, "TestData", file);

        // Minimal fake categories: Respiratory includes Cough, Runny Nose, Shortness of Breath; General includes Fever
        private readonly Dictionary<string, HashSet<string>> _categorySets = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Respiratory", new HashSet<string>(new [] { "Cough", "Runny Nose", "Shortness of Breath"}, StringComparer.OrdinalIgnoreCase) },
            { "General", new HashSet<string>(new [] { "Fever"}, StringComparer.OrdinalIgnoreCase) }
        };

        private IEnumerable<string> GetCats(string conditionName)
        {
            // Very small mapping via overlap with condition symptoms (read from service)
            var svc = new SymptomCheckerService(DataPath("conditions.min.json"));
            if (svc.TryGetCondition(conditionName, out var cond) && cond != null)
            {
                foreach (var kvp in _categorySets)
                {
                    if (cond.Symptoms.Any(s => kvp.Value.Contains(s))) yield return kvp.Key;
                }
            }
        }

        [Fact]
        public void CategoryWeight_BoostsScores()
        {
            var svc = new SymptomCheckerService(DataPath("conditions.min.json"));
            var sel = new [] { "Cough", "Runny Nose" }; // strongly Respiratory
            var baseRes = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.Jaccard, threshold:0, getConditionCategories: GetCats);
            var baseCommonCold = baseRes.First(r => r.Name == "Common Cold").Score;
            // Apply respiratory weight 2x (use Jaccard – NaiveBayes renormalizes, cancelling uniform boosts)
            var weights = new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase) { { "Respiratory", 2.0 } };
            var weightedRes = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.Jaccard, threshold:0, categoryWeights: weights, getConditionCategories: GetCats);
            var weightedCommonCold = weightedRes.First(r => r.Name == "Common Cold").Score;
            Assert.True(weightedCommonCold > baseCommonCold, $"Expected boosted score > base ({weightedCommonCold} > {baseCommonCold})");
        }
    }
}

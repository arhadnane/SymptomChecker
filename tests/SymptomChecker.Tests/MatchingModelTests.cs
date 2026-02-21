using System;
using System.Collections.Generic;
using System.Linq;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class MatchingModelTests
    {
        // Shared test data
        private static readonly List<Condition> _conditions = new()
        {
            new Condition { Name = "Common Cold", Symptoms = new List<string> { "Cough", "Runny Nose", "Sore Throat" } },
            new Condition { Name = "Pneumonia", Symptoms = new List<string> { "Fever", "Shortness of Breath", "Cough" } },
            new Condition { Name = "PE", Symptoms = new List<string> { "Chest Pain", "Shortness of Breath" } }
        };

        private static readonly List<string> _vocabulary;
        private static readonly Dictionary<string, HashSet<string>> _conditionSets;

        static MatchingModelTests()
        {
            var allSymptoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _conditionSets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in _conditions)
            {
                var set = new HashSet<string>(c.Symptoms, StringComparer.OrdinalIgnoreCase);
                _conditionSets[c.Name] = set;
                foreach (var s in c.Symptoms) allSymptoms.Add(s);
            }
            _vocabulary = allSymptoms.OrderBy(s => s).ToList();
        }

        // --- Jaccard Tests ---

        [Fact]
        public void Jaccard_PerfectMatch_ReturnsScore1()
        {
            var model = new JaccardModel();
            var selected = new HashSet<string>(new[] { "Cough", "Runny Nose", "Sore Throat" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            var cold = results.First(r => r.Name == "Common Cold");
            Assert.Equal(1.0, cold.Score, 3);
        }

        [Fact]
        public void Jaccard_PartialMatch_ScoreLessThan1()
        {
            var model = new JaccardModel();
            var selected = new HashSet<string>(new[] { "Cough" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            var cold = results.First(r => r.Name == "Common Cold");
            // Jaccard: |{Cough}∩{Cough,Runny Nose,Sore Throat}| / |union| = 1/3
            Assert.True(cold.Score > 0 && cold.Score < 1.0);
            Assert.Equal(1.0 / 3.0, cold.Score, 3);
        }

        [Fact]
        public void Jaccard_NoMatch_AllScoresZero()
        {
            var model = new JaccardModel();
            var selected = new HashSet<string>(new[] { "Headache" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            Assert.All(results, r => Assert.Equal(0.0, r.Score));
        }

        [Fact]
        public void Jaccard_ThresholdFilters()
        {
            var model = new JaccardModel();
            var selected = new HashSet<string>(new[] { "Cough" }, StringComparer.OrdinalIgnoreCase);
            // Score for Common Cold = 1/3 ≈ 0.333; threshold 0.5 should exclude it
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.5, null);
            Assert.DoesNotContain(results, r => r.Name == "Common Cold");
        }

        // --- Cosine Tests ---

        [Fact]
        public void Cosine_PerfectMatch_ReturnsScore1()
        {
            var model = new CosineModel();
            var selected = new HashSet<string>(new[] { "Chest Pain", "Shortness of Breath" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            var pe = results.First(r => r.Name == "PE");
            Assert.Equal(1.0, pe.Score, 3);
        }

        [Fact]
        public void Cosine_PartialOverlap_ScoreCorrect()
        {
            var model = new CosineModel();
            var selected = new HashSet<string>(new[] { "Cough", "Fever" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            // Pneumonia has Fever+Cough overlap = 2; |sel|=2, |cond|=3
            // Cosine = 2 / (sqrt(2) * sqrt(3)) ≈ 0.816
            var pneu = results.First(r => r.Name == "Pneumonia");
            Assert.InRange(pneu.Score, 0.8, 0.9);
        }

        [Fact]
        public void Cosine_NoOverlap_AllScoresZero()
        {
            var model = new CosineModel();
            var selected = new HashSet<string>(new[] { "Headache" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            Assert.All(results, r => Assert.Equal(0.0, r.Score));
        }

        // --- NaiveBayes Tests ---

        [Fact]
        public void NaiveBayes_ReturnsNormalizedProbabilities()
        {
            var model = new NaiveBayesModel();
            var selected = new HashSet<string>(new[] { "Cough", "Runny Nose" }, StringComparer.OrdinalIgnoreCase);
            var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            // All results should have score between 0 and 1
            foreach (var r in results)
            {
                Assert.InRange(r.Score, 0.0, 1.0);
            }
            // Common Cold should rank highest with those symptoms
            var top = results.OrderByDescending(r => r.Score).First();
            Assert.Equal("Common Cold", top.Name);
        }

        [Fact]
        public void NaiveBayes_TemperatureScaling_ChangesDistribution()
        {
            var model = new NaiveBayesModel();
            var selected = new HashSet<string>(new[] { "Cough", "Fever" }, StringComparer.OrdinalIgnoreCase);

            var resultsNoTemp = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            var resultsHighTemp = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0,
                new MatchingOptions { NaiveBayesTemperature = 2.0 });

            // With higher temperature, scores should be more evenly distributed (flatter)
            var maxNoTemp = resultsNoTemp.Max(r => r.Score);
            var minNoTemp = resultsNoTemp.Min(r => r.Score);
            var maxHighTemp = resultsHighTemp.Max(r => r.Score);
            var minHighTemp = resultsHighTemp.Min(r => r.Score);

            // Gap should be smaller with higher temperature
            double gapNoTemp = maxNoTemp - minNoTemp;
            double gapHighTemp = maxHighTemp - minHighTemp;
            Assert.True(gapHighTemp <= gapNoTemp + 0.001, "Higher temperature should flatten the score distribution");
        }

        [Fact]
        public void NaiveBayes_LowTemperature_SharpensDistribution()
        {
            var model = new NaiveBayesModel();
            var selected = new HashSet<string>(new[] { "Cough", "Fever" }, StringComparer.OrdinalIgnoreCase);

            var resultsNoTemp = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
            var resultsLowTemp = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0,
                new MatchingOptions { NaiveBayesTemperature = 0.5 });

            // With lower temperature, the top score should be higher (sharper)
            var maxLowTemp = resultsLowTemp.Max(r => r.Score);
            var maxNoTemp = resultsNoTemp.Max(r => r.Score);
            Assert.True(maxLowTemp >= maxNoTemp - 0.001, "Lower temperature should sharpen the distribution");
        }

        // --- Interface contract tests ---

        [Fact]
        public void AllModels_ReturnMatchedSymptoms()
        {
            IMatchingModel[] models = { new JaccardModel(), new CosineModel(), new NaiveBayesModel() };
            var selected = new HashSet<string>(new[] { "Cough", "Fever" }, StringComparer.OrdinalIgnoreCase);

            foreach (var model in models)
            {
                var results = model.ComputeMatches(selected, _conditions, _conditionSets, _vocabulary, 0.0, null);
                var pneu = results.FirstOrDefault(r => r.Name == "Pneumonia");
                Assert.NotNull(pneu);
                Assert.Contains("Cough", pneu!.MatchedSymptoms);
                Assert.Contains("Fever", pneu.MatchedSymptoms);
            }
        }

        [Fact]
        public void AllModels_HaveCorrectName()
        {
            Assert.Equal("Jaccard", new JaccardModel().Name);
            Assert.Equal("Cosine", new CosineModel().Name);
            Assert.Equal("NaiveBayes", new NaiveBayesModel().Name);
        }
    }
}

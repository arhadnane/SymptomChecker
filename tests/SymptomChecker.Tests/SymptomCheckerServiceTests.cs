using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class SymptomCheckerServiceTests
    {
        private static string DataPath(string file) => Path.Combine(AppContext.BaseDirectory, "TestData", file);

        [Fact]
        public void Jaccard_BasicMatch_Works()
        {
            var svc = new SymptomCheckerService(DataPath("conditions.min.json"));
            var sel = new[] { "Cough", "Fever" };
            var res = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.Jaccard, threshold: 0.0, topK: null, minMatchCount: 1);
            Assert.Contains(res, r => r.Name == "Pneumonia");
            Assert.Contains(res, r => r.Name == "Common Cold");
            // PE should not match with those unless chest pain/SOB selected
            Assert.DoesNotContain(res, r => r.Name == "PE");
        }

        [Fact]
        public void Cosine_MinMatch_Filters()
        {
            var svc = new SymptomCheckerService(DataPath("conditions.min.json"));
            var sel = new[] { "Cough" };
            var res = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.Cosine, threshold: 0.0, topK: null, minMatchCount: 2);
            // With minMatch=2, nothing should pass because only single symptom selected
            Assert.Empty(res);
        }

        [Fact]
        public void NaiveBayes_TopK_Works()
        {
            var svc = new SymptomCheckerService(DataPath("conditions.min.json"));
            var sel = new[] { "Cough", "Runny Nose" };
            var res = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.NaiveBayes, threshold: 0.0, topK: 1, minMatchCount: 0);
            Assert.Single(res);
            Assert.Equal("Common Cold", res[0].Name);
        }
    }
}

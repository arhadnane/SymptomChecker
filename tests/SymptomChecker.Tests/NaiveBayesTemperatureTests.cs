using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class NaiveBayesTemperatureTests
    {
        private static string DataPath(string file) => Path.Combine(AppContext.BaseDirectory, "TestData", file);

        [Fact]
        public void TemperatureScaling_SharpensAndFlattens()
        {
            var svc = new SymptomCheckerService(DataPath("conditions.min.json"));
            var sel = new [] { "Cough", "Fever", "Shortness of Breath" }; // mixture to differentiate
            // Base (T=1)
            var baseRes = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.NaiveBayes, threshold:0);
            double maxBase = baseRes.Max(r => r.Score);
            double entropyBase = - baseRes.Sum(r => r.Score > 0 ? r.Score * Math.Log(r.Score) : 0);
            // Sharper T<1 (e.g. 0.5)
            var sharpRes = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.NaiveBayes, threshold:0, naiveBayesTemperature:0.5);
            double maxSharp = sharpRes.Max(r => r.Score);
            double entropySharp = - sharpRes.Sum(r => r.Score > 0 ? r.Score * Math.Log(r.Score) : 0);
            // Flatter T>1 (e.g. 2.0)
            var flatRes = svc.GetMatches(sel, SymptomCheckerService.DetectionModel.NaiveBayes, threshold:0, naiveBayesTemperature:2.0);
            double maxFlat = flatRes.Max(r => r.Score);
            double entropyFlat = - flatRes.Sum(r => r.Score > 0 ? r.Score * Math.Log(r.Score) : 0);
            Assert.True(maxSharp > maxBase + 1e-9, "Expected sharper distribution raises max probability");
            Assert.True(entropySharp < entropyBase - 1e-9, "Expected sharper distribution lowers entropy");
            Assert.True(maxFlat < maxBase - 1e-9, "Expected flatter distribution lowers max probability");
            Assert.True(entropyFlat > entropyBase + 1e-9, "Expected flatter distribution increases entropy");
        }
    }
}

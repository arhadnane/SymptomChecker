using System.Collections.Generic;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    public class TriageServiceTests
    {
        [Fact]
        public void EvaluateV2_Vitals_TriggersFlags()
        {
            var selected = new HashSet<string>(new[] { "Cough" }, System.StringComparer.OrdinalIgnoreCase);
            var keys = TriageService.EvaluateV2(selected, tempC: 40.1, heartRate: 130, respRate: 32, systolicBP: 85, diastolicBP: 125, spO2: 88, percPositiveWithChestOrSob: true);
            // Expect many flags
            Assert.Contains("RF_Hypoxia", keys);
            Assert.Contains("RF_Hypotension", keys);
            Assert.Contains("RF_SevereHypertension", keys);
            Assert.Contains("RF_Tachycardia", keys);
            Assert.Contains("RF_Tachypnea", keys);
            Assert.Contains("RF_HighFever", keys);
            Assert.Contains("RF_PERC_Positive", keys);
        }

        [Fact]
        public void Evaluate_SymptomCombos_YieldExpected()
        {
            var selected = new HashSet<string>(new[] { "Chest Pain", "Shortness of Breath" }, System.StringComparer.OrdinalIgnoreCase);
            var keys = TriageService.Evaluate(selected);
            Assert.Contains("RF_ChestPain_SOB", keys);
        }
    }
}

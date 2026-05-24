using System;
using System.Collections.Generic;
using System.Linq;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    /// <summary>
    /// Spec 001-guided-diagnosis-ux T051 — typed RedFlag detection.
    /// Tests the contract in specs/001-guided-diagnosis-ux/contracts/red-flags-triage.md.
    /// </summary>
    public class RedFlagDetectionTests
    {
        private static IReadOnlyList<RedFlag> Run(
            int? spo2 = null, int? sbp = null, int? dbp = null,
            int? hr = null, int? rr = null, double? temp = null,
            IEnumerable<string>? symptoms = null, bool percPositive = false)
        {
            var v = new VitalsSnapshot(temp, hr, rr, sbp, dbp, spo2);
            return TriageService.DetectRedFlags(v, (symptoms ?? Array.Empty<string>()).ToList(), percPositive);
        }

        [Fact]
        public void Spo2Low_Triggers()
        {
            var flags = Run(spo2: 88);
            Assert.Contains(flags, f => f.Code == "RF_Hypoxia");
            Assert.Equal("RedFlag_Spo2_Low", flags.First(f => f.Code == "RF_Hypoxia").MessageKey);
        }

        [Fact]
        public void Spo2Normal_DoesNotTrigger()
        {
            Assert.DoesNotContain(Run(spo2: 96), f => f.Code == "RF_Hypoxia");
        }

        [Fact]
        public void Spo2Null_DoesNotTrigger()
        {
            Assert.DoesNotContain(Run(spo2: null), f => f.Code == "RF_Hypoxia");
        }

        [Fact]
        public void SbpLow_Triggers()
        {
            Assert.Contains(Run(sbp: 80), f => f.Code == "RF_Hypotension");
        }

        [Fact]
        public void SbpHigh_Triggers()
        {
            Assert.Contains(Run(sbp: 185), f => f.Code == "RF_SevereHypertension");
        }

        [Fact]
        public void DbpHigh_Triggers()
        {
            Assert.Contains(Run(dbp: 125), f => f.Code == "RF_SevereHypertension");
        }

        [Fact]
        public void HrHigh_Triggers()
        {
            Assert.Contains(Run(hr: 130), f => f.Code == "RF_Tachycardia");
        }

        [Fact]
        public void RrHigh_Triggers()
        {
            Assert.Contains(Run(rr: 32), f => f.Code == "RF_Tachypnea");
        }

        [Fact]
        public void TempHigh_Triggers()
        {
            Assert.Contains(Run(temp: 40.2), f => f.Code == "RF_HighFever");
        }

        [Fact]
        public void PercWithChestPain_Triggers()
        {
            var flags = Run(symptoms: new[] { "Chest Pain" }, percPositive: true);
            Assert.Contains(flags, f => f.Code == "RF_PERC_Positive");
        }

        [Fact]
        public void PercWithoutChestOrSob_DoesNotTrigger()
        {
            var flags = Run(symptoms: new[] { "Headache" }, percPositive: true);
            Assert.DoesNotContain(flags, f => f.Code == "RF_PERC_Positive");
        }

        [Fact]
        public void MultiTrigger_OrderedBySeverityDescending()
        {
            var flags = Run(spo2: 85, hr: 130, temp: 40.5,
                            symptoms: new[] { "Chest Pain", "Shortness of Breath" },
                            percPositive: true);
            // Severity 4 first: spo2.low / perc.chest (sorted by code ascending within severity)
            Assert.True(flags.Count >= 4);
            Assert.True(flags[0].Severity >= flags[^1].Severity);
            Assert.Contains(flags, f => f.Code == "RF_Hypoxia");
            Assert.Contains(flags, f => f.Code == "RF_HighFever");
            Assert.Contains(flags, f => f.Code == "RF_Tachycardia");
            Assert.Contains(flags, f => f.Code == "RF_PERC_Positive");
        }

        [Fact]
        public void NoTriggers_ReturnsEmpty()
        {
            var flags = Run();
            Assert.Empty(flags);
        }

        [Fact]
        public void TypedFlags_AllHaveMessageKey()
        {
            var flags = Run(spo2: 85, sbp: 80, hr: 130, rr: 32, temp: 40.2);
            Assert.All(flags, f => Assert.False(string.IsNullOrEmpty(f.MessageKey)));
            Assert.All(flags, f => Assert.True(f.Severity >= 1 && f.Severity <= 5));
        }
    }
}

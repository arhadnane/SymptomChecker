using System.Collections.Generic;
using System.IO;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using Xunit;

namespace SymptomChecker.Tests
{
    /// <summary>
    /// Spec 001-guided-diagnosis-ux T053 — settings round-trip for the new
    /// Guided Diagnosis Assistant fields (UiMode, PatientWizardLastStep,
    /// CollapsedSections). Older settings.json (without these fields) must
    /// still load cleanly.
    /// </summary>
    public class SettingsServiceTests
    {
        private static string TempPath() => Path.Combine(Path.GetTempPath(),
            "sc-settings-" + System.Guid.NewGuid().ToString("N") + ".json");

        [Fact]
        public void RoundTrip_PreservesNewFields()
        {
            string path = TempPath();
            try
            {
                var svc = new SettingsService(path);
                svc.Settings.UiMode = UiMode.Patient;
                svc.Settings.PatientWizardLastStep = 3;
                svc.Settings.CollapsedSections = new Dictionary<string, bool>
                {
                    { "pro.ai", true },
                    { "pro.rules", false }
                };
                svc.Save();

                var reloaded = new SettingsService(path);
                Assert.Equal(UiMode.Patient, reloaded.Settings.UiMode);
                Assert.Equal(3, reloaded.Settings.PatientWizardLastStep);
                Assert.NotNull(reloaded.Settings.CollapsedSections);
                Assert.True(reloaded.Settings.CollapsedSections!["pro.ai"]);
                Assert.False(reloaded.Settings.CollapsedSections!["pro.rules"]);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Load_OlderFileWithoutNewFields_DoesNotCrash()
        {
            string path = TempPath();
            try
            {
                // Simulate an old settings file (pre-spec-001) — no UiMode/PatientWizardLastStep/CollapsedSections.
                File.WriteAllText(path,
                    @"{ ""Language"": ""en"", ""DarkMode"": true, ""ThresholdPercent"": 10 }");

                var svc = new SettingsService(path);
                Assert.Equal("en", svc.Settings.Language);
                Assert.True(svc.Settings.DarkMode);
                Assert.Null(svc.Settings.UiMode);
                Assert.Null(svc.Settings.PatientWizardLastStep);
                Assert.Null(svc.Settings.CollapsedSections);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Load_MissingFile_UsesDefaultDetectionAndOllamaModels()
        {
            string path = TempPath();
            try
            {
                var svc = new SettingsService(path);

                Assert.Equal("Ensemble", svc.Settings.Model);
                Assert.Equal("gemma4", svc.Settings.OllamaModel);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Reset_ClearsUiMode()
        {
            string path = TempPath();
            try
            {
                var svc = new SettingsService(path);
                svc.Settings.UiMode = UiMode.Professional;
                svc.Save();

                svc.Reset();
                Assert.Equal("Ensemble", svc.Settings.Model);
                Assert.Equal("gemma4", svc.Settings.OllamaModel);
                Assert.Null(svc.Settings.UiMode);
                Assert.Null(svc.Settings.PatientWizardLastStep);
                Assert.Null(svc.Settings.CollapsedSections);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SymptomChecker.Tests
{
    public class PatientTranslationAuditTests
    {
        [Fact]
        public void PatientFacingUiStrings_AvoidHighlyTechnicalTerms()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "data", "translations.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            string[] blockedTerms =
            {
                "vitals",
                "ranking",
                "relevance",
                "threshold",
                "top-k",
                "naive bayes",
                "ollama",
                "centor",
                "mcisaac",
                "perc",
                "sbp",
                "dbp",
                "spo2",
                "constantes",
                "pertinence",
                "classement",
                "seuil",
                "modèle",
                "بايز",
                "عتبة"
            };

            var offenders = new List<string>();
            foreach (var entry in doc.RootElement.GetProperty("ui").EnumerateArray())
            {
                string key = entry.GetProperty("key").GetString() ?? string.Empty;
                if (!key.StartsWith("Patient_", StringComparison.OrdinalIgnoreCase) &&
                    !key.StartsWith("Mode_Patient", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (string lang in new[] { "en", "fr", "ar" })
                {
                    if (!entry.TryGetProperty(lang, out var valueElement)) continue;
                    string value = valueElement.GetString() ?? string.Empty;
                    string normalized = value.ToLowerInvariant();
                    if (blockedTerms.Any(term => normalized.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    {
                        offenders.Add($"{key}:{lang}:{value}");
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "Technical terms found in patient-facing UI strings:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }
    }
}
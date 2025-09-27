using System;
using System.Collections.Generic;

namespace SymptomCheckerApp.Services
{
    // Simple, rule-based red flag triage (educational only)
    public static class TriageService
    {
        // Lower number = higher priority (more critical)
        private static readonly Dictionary<string, int> SeverityPriority = new(StringComparer.OrdinalIgnoreCase)
        {
            { "RF_ChestPain_SOB", 1 },
            { "RF_Fainting_ChestPain", 1 },
            { "RF_Confusion", 1 },

            { "RF_Fever_NeckPain_Light", 2 },
            { "RF_SevereCough_SOB", 2 },
            { "RF_BloodInStool", 2 },
            { "RF_BloodInUrine", 2 },

            { "RF_TesticularPain", 3 },

            // Vitals-based triage v2
            { "RF_Hypoxia", 1 },            // SpO2 < 92%
            { "RF_Hypotension", 1 },        // SBP < 90
            { "RF_PERC_Positive", 1 },      // PERC positive with chest pain/SOB
            { "RF_SevereHypertension", 2 }, // SBP >= 180 or DBP >= 120
            { "RF_Tachycardia", 2 },        // HR >= 120
            { "RF_Tachypnea", 2 },          // RR >= 30
            { "RF_HighFever", 2 },          // Temp >= 40°C
        };
        /// <summary>
        /// Evaluate selected symptoms and return localized message keys for red‑flag warnings (educational only).
        /// Inputs are canonical symptom names. Matching is case-insensitive.
        /// </summary>
        public static List<string> Evaluate(HashSet<string> selectedSymptoms)
        {
            // Normalize to case-insensitive set to be robust regardless of caller's comparer
            var set = selectedSymptoms != null
                ? new HashSet<string>(selectedSymptoms, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var res = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0) return new List<string>();

            bool Has(string s) => set.Contains(s);

            // Chest pain + shortness of breath
            if (Has("Chest Pain") && Has("Shortness of Breath"))
                res.Add("RF_ChestPain_SOB");

            // Fainting with chest pain
            if (Has("Fainting") && Has("Chest Pain"))
                res.Add("RF_Fainting_ChestPain");

            // Fever + neck pain or sensitivity to light (possible meningitis)
            if (Has("Fever") && (Has("Neck Pain") || Has("Sensitivity to Light")))
                res.Add("RF_Fever_NeckPain_Light");

            // Blood in stool
            if (Has("Blood in Stool"))
                res.Add("RF_BloodInStool");

            // Blood in urine
            if (Has("Blood in Urine"))
                res.Add("RF_BloodInUrine");

            // Severe cough + shortness of breath (possible lower respiratory compromise)
            if (Has("Severe Cough") && Has("Shortness of Breath"))
                res.Add("RF_SevereCough_SOB");

            // Acute testicular pain
            if (Has("Testicular Pain"))
                res.Add("RF_TesticularPain");

            // Confusion (acute change in mental status)
            if (Has("Confusion"))
                res.Add("RF_Confusion");

            // Convert to list and sort by severity (then by key for stability)
            var list = new List<string>(res);
            list.Sort((a, b) =>
            {
                int pa = SeverityPriority.TryGetValue(a, out var va) ? va : 99;
                int pb = SeverityPriority.TryGetValue(b, out var vb) ? vb : 99;
                int cmp = pa.CompareTo(pb);
                return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        /// <summary>
        /// Triage v2: include vitals and PERC context for more nuanced urgency signaling (educational only).
        /// </summary>
        public static List<string> EvaluateV2(
            HashSet<string> selectedSymptoms,
            double? tempC = null,
            int? heartRate = null,
            int? respRate = null,
            int? systolicBP = null,
            int? diastolicBP = null,
            int? spO2 = null,
            bool? percPositiveWithChestOrSob = null)
        {
            var keys = new HashSet<string>(Evaluate(selectedSymptoms), StringComparer.OrdinalIgnoreCase);

            // Add vitals-based red flags
            if (spO2.HasValue && spO2.Value < 92) keys.Add("RF_Hypoxia");
            if (systolicBP.HasValue && systolicBP.Value < 90) keys.Add("RF_Hypotension");
            if (systolicBP.HasValue && systolicBP.Value >= 180) keys.Add("RF_SevereHypertension");
            if (diastolicBP.HasValue && diastolicBP.Value >= 120) keys.Add("RF_SevereHypertension");
            if (heartRate.HasValue && heartRate.Value >= 120) keys.Add("RF_Tachycardia");
            if (respRate.HasValue && respRate.Value >= 30) keys.Add("RF_Tachypnea");
            if (tempC.HasValue && tempC.Value >= 40.0) keys.Add("RF_HighFever");

            // PERC positive in the context of chest pain / SOB increases urgency
            if (percPositiveWithChestOrSob == true) keys.Add("RF_PERC_Positive");

            var list = new List<string>(keys);
            list.Sort((a, b) =>
            {
                int pa = SeverityPriority.TryGetValue(a, out var va) ? va : 99;
                int pb = SeverityPriority.TryGetValue(b, out var vb) ? vb : 99;
                int cmp = pa.CompareTo(pb);
                return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Services;

namespace SymptomCheckerApp.UI
{
    // Decision rules: PERC, Centor/McIsaac, Triage banner
    public partial class MainForm
    {
        private void UpdatePercRule()
        {
            var t = _translationService;
            bool ageOk = _numAge.Value < 50;
            bool hrOk = _numHR.Value < 100;
            bool spo2Ok = _numSpO2.Value >= 95;
            bool hemoptysisOk = !_percHemoptysis.Checked;
            bool estrogenOk = !_percEstrogen.Checked;
            bool priorOk = !_percPriorDvtPe.Checked;
            bool unilatOk = !_percUnilateralLeg.Checked;
            bool surgeryOk = !_percRecentSurgery.Checked;
            bool percNegative = ageOk && hrOk && spo2Ok && hemoptysisOk && estrogenOk && priorOk && unilatOk && surgeryOk;

            string neg = t?.T("PERC_Negative") ?? "PERC negative — PE unlikely if pretest probability is low.";
            string pos = t?.T("PERC_Positive") ?? "PERC positive — cannot rule out PE; consider further testing if suspicion persists.";
            _percResult.Text = percNegative ? neg : pos;
        }

        private void UpdateDecisionRules()
        {
            if (_grpCentor == null) return;
            var t = _translationService;
            bool hasFever = false;
            try
            {
                if (_settingsService?.Settings.TempC.HasValue == true)
                    hasFever = _settingsService!.Settings.TempC!.Value >= 38.0;
            }
            catch { }
            hasFever = hasFever || _checkedSymptoms.Contains("Fever");

            bool tonsils = _checkedSymptoms.Contains("Sore Throat") || _checkedSymptoms.Contains("Tonsillar Exudates") || _checkedSymptoms.Contains("Tonsillar Swelling");
            bool nodes = _checkedSymptoms.Contains("Swollen Lymph Nodes");
            bool noCough = !_checkedSymptoms.Contains("Cough");

            try { _centorFever.Checked = hasFever; } catch { }
            try { _centorTonsils.Checked = tonsils; } catch { }
            try { _centorNodes.Checked = nodes; } catch { }
            try { _centorNoCough.Checked = noCough; } catch { }

            int centor = 0;
            if (hasFever) centor++;
            if (tonsils) centor++;
            if (nodes) centor++;
            if (noCough) centor++;

            int age = (int)_numAge.Value;
            int ageAdj = 0;
            if (age < 15) ageAdj = 1; else if (age >= 45) ageAdj = -1;
            int mcIsaac = centor + ageAdj;
            if (mcIsaac < 0) mcIsaac = 0; if (mcIsaac > 5) mcIsaac = 5;

            string centorLabel = t?.T("CentorLabel") ?? "Centor:";
            string mcIsaacLabel = t?.T("McIsaacLabel") ?? "McIsaac:";
            _centorScore.Text = $"{centorLabel} {centor}";
            _mcIsaacScore.Text = $"{mcIsaacLabel} {mcIsaac}";

            string advice = mcIsaac switch
            {
                <= 1 => t?.T("CentorAdvice_0_1") ?? "Low risk: likely viral. No antibiotics. Consider symptomatic care.",
                2 => t?.T("CentorAdvice_2") ?? "Intermediate risk: consider rapid strep test (RADT).",
                3 => t?.T("CentorAdvice_3") ?? "Higher risk: RADT and/or consider empiric antibiotics as per local guidance.",
                _ => t?.T("CentorAdvice_4_5") ?? "High risk: consider testing and/or empiric antibiotics per guidelines."
            };
            _centorAdvice.Text = advice;
        }

        private void UpdateTriageBanner()
        {
            var selected = new HashSet<string>(_checkedSymptoms, StringComparer.OrdinalIgnoreCase);
            bool chestOrSob = selected.Contains("Chest Pain") || selected.Contains("Shortness of Breath");
            bool percPositive = false;
            try
            {
                bool ageOk = _numAge.Value < 50;
                bool hrOk = _numHR.Value < 100;
                bool spo2Ok = _numSpO2.Value >= 95;
                bool hemoptysisOk = !_percHemoptysis.Checked;
                bool estrogenOk = !_percEstrogen.Checked;
                bool priorOk = !_percPriorDvtPe.Checked;
                bool unilatOk = !_percUnilateralLeg.Checked;
                bool surgeryOk = !_percRecentSurgery.Checked;
                bool percNeg = ageOk && hrOk && spo2Ok && hemoptysisOk && estrogenOk && priorOk && unilatOk && surgeryOk;
                percPositive = !percNeg;
            }
            catch { }
            var keys = TriageService.EvaluateV2(
                selected,
                tempC: (double?)_numTempC.Value,
                heartRate: (int?)_numHR.Value,
                respRate: (int?)_numRR.Value,
                systolicBP: (int?)_numSBP.Value,
                diastolicBP: (int?)_numDBP.Value,
                spO2: (int?)_numSpO2.Value,
                percPositiveWithChestOrSob: chestOrSob && percPositive
            );
            if (keys.Count == 0)
            {
                _triageBanner.Visible = false;
                return;
            }
            var t = _translationService;
            var header = t?.T("RedFlagsHeader") ?? "Possible red flags:";
            var messages = new List<string>();
            foreach (var k in keys)
            {
                messages.Add(t?.T(k) ?? k);
            }
            var notice = t?.T("SeekCareDisclaimer") ?? "If these apply, consider seeking urgent medical attention. This tool is educational, not medical advice.";
            bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            if (rtl)
            {
                var lines = messages.Select(m => m + "  •");
                var joined = string.Join(Environment.NewLine, lines);
                _triageBanner.Text = header + Environment.NewLine + joined + Environment.NewLine + notice;
            }
            else
            {
                var bullet = string.Join(Environment.NewLine + " • ", messages);
                _triageBanner.Text = header + Environment.NewLine + " • " + bullet + Environment.NewLine + notice;
            }
            _triageBanner.Visible = true;
        }
    }
}

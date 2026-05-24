using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.UI
{
    public partial class MainForm
    {
        private SplitContainer? _mainSplitHost;
        private SplitContainer? _mainVerticalSplitHost;
        private PatientShell? _patientShell;
        private string? _patientSelectedCategory;
        private bool _syncingPatientShell;

        private void EnsurePatientShell()
        {
            if (_mainVerticalSplitHost?.Panel1 == null || _patientShell != null) return;

            _patientShell = new PatientShell(_translationService, _darkModeToggle.Checked)
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            _patientShell.CategorySelected += OnPatientCategorySelected;
            _patientShell.SymptomsSelectionChanged += OnPatientSymptomsSelectionChanged;
            _patientShell.VitalsChanged += OnPatientVitalsChanged;
            _patientShell.RunCheckRequested += () => CheckButton_Click(this, EventArgs.Empty);
            _patientShell.StepChanged += OnPatientStepChanged;
            _patientShell.RestartRequested += RestartPatientWizard;

            _mainVerticalSplitHost.Panel1.Controls.Add(_patientShell);
            _patientShell.BringToFront();
        }

        private void SyncPatientShellFromState(bool restoreStep)
        {
            EnsurePatientShell();
            if (_patientShell == null) return;

            _syncingPatientShell = true;
            try
            {
                _patientSelectedCategory = ResolvePatientCategory();
                _patientShell.UpdatePresentation(_translationService, _darkModeToggle.Checked);
                _patientShell.SetCategories(BuildPatientCategoryOptions(), _patientSelectedCategory);
                _patientShell.SetSymptoms(BuildPatientSymptomOptions(_patientSelectedCategory), _checkedSymptoms);
                _patientShell.SetVitals(ReadCurrentPatientVitalsState());

                if (restoreStep)
                {
                    _patientShell.SetStep(GetSuggestedPatientStep(), raiseEvent: false);
                }

                UpdatePatientShellResults();
            }
            finally
            {
                _syncingPatientShell = false;
            }
        }

        private List<PatientShell.CategoryOption> BuildPatientCategoryOptions()
        {
            if (_categoriesService == null) return new List<PatientShell.CategoryOption>();

            return _categoriesService
                .GetAllCategories()
                .Select(category => new PatientShell.CategoryOption(
                    category.Name,
                    _translationService?.Category(category.Name) ?? category.Name,
                    GetCategorySet(category).Count))
                .OrderBy(option => option.Display, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private List<PatientShell.SymptomOption> BuildPatientSymptomOptions(string? categoryCanonical)
        {
            if (_categoriesService == null || string.IsNullOrWhiteSpace(categoryCanonical))
            {
                return new List<PatientShell.SymptomOption>();
            }

            var category = _categoriesService
                .GetAllCategories()
                .FirstOrDefault(item => string.Equals(item.Name, categoryCanonical, StringComparison.OrdinalIgnoreCase));
            if (category == null) return new List<PatientShell.SymptomOption>();

            return GetCategorySet(category)
                .Select(symptom => new PatientShell.SymptomOption(symptom, _translationService?.Symptom(symptom) ?? symptom))
                .OrderBy(option => option.Display, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private HashSet<string> GetCategorySet(SymptomCategory category)
        {
            if (!_categorySetsCache.TryGetValue(category.Name, out var set))
            {
                try
                {
                    set = _categoriesService?.BuildCategorySet(category, _allSymptoms) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                _categorySetsCache[category.Name] = set;
            }

            return set;
        }

        private string? ResolvePatientCategory()
        {
            if (!string.IsNullOrWhiteSpace(_patientSelectedCategory))
            {
                return _patientSelectedCategory;
            }

            string? inferred = InferPatientCategoryFromSelection();
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                return inferred;
            }

            return (_categorySelector.SelectedItem as CatItem)?.Canonical;
        }

        private string? InferPatientCategoryFromSelection()
        {
            if (_categoriesService == null || _checkedSymptoms.Count == 0) return null;

            string? bestCategory = null;
            int bestOverlap = 0;
            foreach (var category in _categoriesService.GetAllCategories())
            {
                int overlap = GetCategorySet(category).Count(symptom => _checkedSymptoms.Contains(symptom));
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestCategory = category.Name;
                }
            }

            return bestOverlap > 0 ? bestCategory : null;
        }

        private int GetSuggestedPatientStep()
        {
            if (_settingsService?.Settings.PatientWizardLastStep is int persistedStep && persistedStep >= 1 && persistedStep <= 3)
            {
                return persistedStep;
            }

            if (_lastResults.Count > 0 && _checkedSymptoms.Count > 0)
            {
                return 4;
            }

            if (_checkedSymptoms.Count > 0)
            {
                return 3;
            }

            if (!string.IsNullOrWhiteSpace(_patientSelectedCategory))
            {
                return 2;
            }

            return 1;
        }

        private PatientShell.PatientVitalsState ReadCurrentPatientVitalsState()
        {
            return new PatientShell.PatientVitalsState(
                AgeYears: _numAge.Value,
                TempC: _numTempC.Value,
                HeartRate: _numHR.Value,
                RespRate: _numRR.Value,
                SystolicBP: _numSBP.Value,
                DiastolicBP: _numDBP.Value,
                SpO2: _numSpO2.Value,
                Hemoptysis: _percHemoptysis.Checked,
                EstrogenUse: _percEstrogen.Checked,
                PriorDvtPe: _percPriorDvtPe.Checked,
                UnilateralLegSwelling: _percUnilateralLeg.Checked,
                RecentSurgeryTrauma: _percRecentSurgery.Checked);
        }

        private void OnPatientCategorySelected(string? categoryCanonical)
        {
            if (_syncingPatientShell) return;

            _patientSelectedCategory = categoryCanonical;
            if (!string.IsNullOrWhiteSpace(categoryCanonical))
            {
                _showOnlyCategory.Checked = true;
                for (int index = 0; index < _categorySelector.Items.Count; index++)
                {
                    if (_categorySelector.Items[index] is CatItem item &&
                        string.Equals(item.Canonical, categoryCanonical, StringComparison.OrdinalIgnoreCase))
                    {
                        _categorySelector.SelectedIndex = index;
                        break;
                    }
                }
            }

            SyncPatientShellFromState(restoreStep: false);
        }

        private void OnPatientSymptomsSelectionChanged(IReadOnlyCollection<string> selectedSymptoms)
        {
            if (_syncingPatientShell) return;

            var currentOptions = BuildPatientSymptomOptions(_patientSelectedCategory)
                .Select(option => option.Canonical)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var symptom in currentOptions)
            {
                if (selectedSymptoms.Contains(symptom))
                {
                    _checkedSymptoms.Add(symptom);
                }
                else
                {
                    _checkedSymptoms.Remove(symptom);
                }
            }

            RefreshSymptomList();
            UpdateCheckButtonEnabled();
            UpdateDecisionRules();
            UpdatePercRule();
        }

        private void OnPatientVitalsChanged(PatientShell.PatientVitalsState state)
        {
            if (_syncingPatientShell) return;

            ApplyNumericValue(_numAge, state.AgeYears);
            ApplyNumericValue(_numTempC, state.TempC);
            ApplyNumericValue(_numHR, state.HeartRate);
            ApplyNumericValue(_numRR, state.RespRate);
            ApplyNumericValue(_numSBP, state.SystolicBP);
            ApplyNumericValue(_numDBP, state.DiastolicBP);
            ApplyNumericValue(_numSpO2, state.SpO2);

            _percHemoptysis.Checked = state.Hemoptysis;
            _percEstrogen.Checked = state.EstrogenUse;
            _percPriorDvtPe.Checked = state.PriorDvtPe;
            _percUnilateralLeg.Checked = state.UnilateralLegSwelling;
            _percRecentSurgery.Checked = state.RecentSurgeryTrauma;

            UpdateDecisionRules();
            UpdatePercRule();
        }

        private static void ApplyNumericValue(NumericUpDown control, decimal value)
        {
            decimal clamped = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
            if (control.Value != clamped)
            {
                control.Value = clamped;
            }
        }

        private void OnPatientStepChanged(int step)
        {
            if (_settingsService != null)
            {
                _settingsService.Settings.PatientWizardLastStep = step < 4 ? step : 0;
                _settingsService.Save();
            }

            if (step == 4)
            {
                UpdatePatientShellResults();
            }
        }

        private void UpdatePatientShellResults()
        {
            if (_patientShell == null) return;

            var flags = GetCurrentRedFlags();
            string summary = _lastResults.Count > 0
                ? string.Format(_translationService?.T("Patient_Results_Summary") ?? "Here are {0} educational matches ordered by relevance.", _lastResults.Count)
                : (_translationService?.T("Patient_NoResults") ?? (_translationService?.T("NoMatches") ?? "No matching conditions found based on the current selection."));
            string disclaimer = _translationService?.T("Patient_Disclaimer")
                ?? (_translationService?.T("Card_AlwaysConsult") ?? "Educational tool only. Always consult a qualified healthcare professional for diagnosis or treatment.");

            _patientShell.SetResultsState(summary, disclaimer, flags);
            RebuildResultCards(_patientShell.ResultCardsHost);
        }

        private void RestartPatientWizard()
        {
            _patientSelectedCategory = null;
            _checkedSymptoms.Clear();
            _lastResults = new List<ConditionMatch>();

            RefreshSymptomList();
            UpdateCheckButtonEnabled();
            RebuildResultsListItems();
            UpdateDecisionRules();
            UpdatePercRule();
            UpdateTriageBanner();
            RefreshRedFlagBanner();

            if (_patientShell != null)
            {
                _syncingPatientShell = true;
                try
                {
                    _patientShell.SetStep(1, raiseEvent: false);
                }
                finally
                {
                    _syncingPatientShell = false;
                }
                SyncPatientShellFromState(restoreStep: false);
            }

            if (_settingsService != null)
            {
                _settingsService.Settings.PatientWizardLastStep = 1;
                _settingsService.Save();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SymptomChecker.Services;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.UI.Controls;

namespace SymptomCheckerApp.UI
{
    public sealed class PatientShell : UserControl
    {
        public sealed record CategoryOption(string Canonical, string Display, int SymptomCount);
        public sealed record SymptomOption(string Canonical, string Display);
        public sealed record PatientVitalsState(
            decimal AgeYears,
            decimal TempC,
            decimal HeartRate,
            decimal RespRate,
            decimal SystolicBP,
            decimal DiastolicBP,
            decimal SpO2,
            bool Hemoptysis,
            bool EstrogenUse,
            bool PriorDvtPe,
            bool UnilateralLegSwelling,
            bool RecentSurgeryTrauma);

        private sealed class SymptomItem
        {
            public string Canonical { get; }
            public string Display { get; }

            public SymptomItem(string canonical, string display)
            {
                Canonical = canonical;
                Display = display;
            }

            public override string ToString() => Display;
        }

        private readonly TableLayoutPanel _root = new();
        private readonly TableLayoutPanel _header = new();
        private readonly Label _modeLabel = new();
        private readonly Label _safetyLabel = new();
        private readonly Label _progressLabel = new();
        private readonly Label _stepTitleLabel = new();
        private readonly Label _stepSubtitleLabel = new();
        private readonly Panel _contentHost = new();
        private readonly Panel _step1Panel = new();
        private readonly TableLayoutPanel _step2Panel = new();
        private readonly TableLayoutPanel _step3Panel = new();
        private readonly TableLayoutPanel _step4Panel = new();
        private readonly FlowLayoutPanel _categoryCardsPanel = new();
        private readonly Label _selectionSummaryLabel = new();
        private readonly CheckedListBox _symptomsList = new();
        private readonly FlowLayoutPanel _vitalsPanel = new();
        private readonly GroupBox _riskFactorsGroup = new();
        private readonly FlowLayoutPanel _riskFactorsPanel = new();
        private readonly NumericUpDown _ageInput = new();
        private readonly NumericUpDown _tempInput = new();
        private readonly NumericUpDown _heartRateInput = new();
        private readonly NumericUpDown _respRateInput = new();
        private readonly NumericUpDown _sbpInput = new();
        private readonly NumericUpDown _dbpInput = new();
        private readonly NumericUpDown _spo2Input = new();
        private readonly CheckBox _hemoptysisToggle = new();
        private readonly CheckBox _estrogenToggle = new();
        private readonly CheckBox _priorClotToggle = new();
        private readonly CheckBox _unilateralLegToggle = new();
        private readonly CheckBox _recentSurgeryToggle = new();
        private readonly Label _resultsSummaryLabel = new();
        private readonly RedFlagBanner _resultsRedFlagBanner;
        private readonly FlowLayoutPanel _resultsCards = new();
        private readonly Label _disclaimerLabel = new();
        private readonly FlowLayoutPanel _navigationBar = new();
        private readonly Button _backButton = new();
        private readonly Button _nextButton = new();
        private readonly Button _restartButton = new();

        private TranslationService? _translationService;
        private bool _darkMode;
        private bool _applyingState;
        private int _currentStep = 1;
        private string? _selectedCategory;
        private List<CategoryOption> _categoryOptions = new();
        private List<SymptomOption> _symptomOptions = new();
        private readonly HashSet<string> _selectedSymptoms = new(StringComparer.OrdinalIgnoreCase);

        public event Action<int>? StepChanged;
        public event Action<string?>? CategorySelected;
        public event Action<IReadOnlyCollection<string>>? SymptomsSelectionChanged;
        public event Action<PatientVitalsState>? VitalsChanged;
        public event Action? RunCheckRequested;
        public event Action? RestartRequested;

        public int CurrentStep => _currentStep;
        public FlowLayoutPanel ResultCardsHost => _resultsCards;

        public PatientShell(TranslationService? translationService, bool darkMode)
        {
            _translationService = translationService;
            _darkMode = darkMode;
            _resultsRedFlagBanner = new RedFlagBanner(translationService, darkMode)
            {
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 10)
            };

            Dock = DockStyle.Fill;
            AutoScroll = true;
            Padding = new Padding(18);
            AccessibleName = "Guided patient shell";

            BuildLayout();
            BuildStepPanels();
            UpdatePresentation(_translationService, _darkMode);
            SetStep(1, raiseEvent: false);
        }

        public void UpdatePresentation(TranslationService? translationService, bool darkMode)
        {
            _translationService = translationService;
            _darkMode = darkMode;

            bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;

            _modeLabel.Text = T("Mode_Patient", "Patient (guided)");
            _safetyLabel.Text = T("Patient_Wizard_Subtitle", "Educational only — this assistant cannot replace professional medical advice.");
            _riskFactorsGroup.Text = T("Patient_RiskFactors_Title", "Helpful safety questions");

            _backButton.Text = T("Patient_Back", "Back");
            _restartButton.Text = T("Patient_Restart", "Start over");
            _nextButton.Text = _currentStep == 3
                ? T("Patient_RunCheck", "Show results")
                : T("Patient_Next", "Next");

            _hemoptysisToggle.Text = T("Patient_Risk_Hemoptysis", "I am coughing up blood");
            _estrogenToggle.Text = T("Patient_Risk_Estrogen", "I use estrogen or hormone treatment");
            _priorClotToggle.Text = T("Patient_Risk_PriorClot", "I had a blood clot before");
            _unilateralLegToggle.Text = T("Patient_Risk_UnilateralLeg", "One leg is more swollen than the other");
            _recentSurgeryToggle.Text = T("Patient_Risk_RecentSurgery", "I had recent surgery or a significant injury");

            ApplyStepTexts();
            ApplyTheme();
            RenderCategoryButtons();
            UpdateSelectionSummary();
        }

        public void SetCategories(IEnumerable<CategoryOption> categories, string? selectedCategory)
        {
            _categoryOptions = categories.ToList();
            _selectedCategory = selectedCategory;
            RenderCategoryButtons();
            UpdateNavigationState();
        }

        public void SetSymptoms(IEnumerable<SymptomOption> symptoms, IReadOnlyCollection<string> selectedSymptoms)
        {
            _symptomOptions = symptoms.ToList();
            _selectedSymptoms.Clear();
            foreach (var symptom in _symptomOptions.Select(option => option.Canonical))
            {
                if (selectedSymptoms.Contains(symptom))
                {
                    _selectedSymptoms.Add(symptom);
                }
            }

            _applyingState = true;
            try
            {
                _symptomsList.Items.Clear();
                foreach (var option in _symptomOptions)
                {
                    _symptomsList.Items.Add(new SymptomItem(option.Canonical, option.Display), _selectedSymptoms.Contains(option.Canonical));
                }
            }
            finally
            {
                _applyingState = false;
            }

            UpdateSelectionSummary();
            UpdateNavigationState();
        }

        public void SetVitals(PatientVitalsState state)
        {
            _applyingState = true;
            try
            {
                _ageInput.Value = Clamp(state.AgeYears, _ageInput.Minimum, _ageInput.Maximum);
                _tempInput.Value = Clamp(state.TempC, _tempInput.Minimum, _tempInput.Maximum);
                _heartRateInput.Value = Clamp(state.HeartRate, _heartRateInput.Minimum, _heartRateInput.Maximum);
                _respRateInput.Value = Clamp(state.RespRate, _respRateInput.Minimum, _respRateInput.Maximum);
                _sbpInput.Value = Clamp(state.SystolicBP, _sbpInput.Minimum, _sbpInput.Maximum);
                _dbpInput.Value = Clamp(state.DiastolicBP, _dbpInput.Minimum, _dbpInput.Maximum);
                _spo2Input.Value = Clamp(state.SpO2, _spo2Input.Minimum, _spo2Input.Maximum);
                _hemoptysisToggle.Checked = state.Hemoptysis;
                _estrogenToggle.Checked = state.EstrogenUse;
                _priorClotToggle.Checked = state.PriorDvtPe;
                _unilateralLegToggle.Checked = state.UnilateralLegSwelling;
                _recentSurgeryToggle.Checked = state.RecentSurgeryTrauma;
            }
            finally
            {
                _applyingState = false;
            }
        }

        public void SetResultsState(string summaryText, string disclaimerText, IReadOnlyCollection<RedFlag> redFlags)
        {
            _resultsSummaryLabel.Text = summaryText;
            _disclaimerLabel.Text = disclaimerText;
            _resultsRedFlagBanner.UpdateTranslation(_translationService, _darkMode);
            _resultsRedFlagBanner.SetFlags(redFlags.ToList());
        }

        public void SetStep(int step, bool raiseEvent)
        {
            _currentStep = Math.Max(1, Math.Min(4, step));
            ApplyStepTexts();
            UpdateStepVisibility();
            UpdateNavigationState();
            if (raiseEvent)
            {
                StepChanged?.Invoke(_currentStep);
            }
        }

        public void FocusCurrentStepControl()
        {
            switch (_currentStep)
            {
                case 1:
                    if (_categoryCardsPanel.Controls.Count > 0)
                    {
                        _categoryCardsPanel.Controls[0].Focus();
                    }
                    break;
                case 2:
                    _symptomsList.Focus();
                    break;
                case 3:
                    _ageInput.Focus();
                    break;
                default:
                    _restartButton.Focus();
                    break;
            }
        }

        private void BuildLayout()
        {
            _root.Dock = DockStyle.Fill;
            _root.ColumnCount = 1;
            _root.RowCount = 3;
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _header.Dock = DockStyle.Fill;
            _header.ColumnCount = 1;
            _header.RowCount = 5;
            _header.AutoSize = true;
            _header.Margin = new Padding(0, 0, 0, 12);
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _header.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _modeLabel.AutoSize = true;
            _modeLabel.Font = new Font(Font.FontFamily, 13f, FontStyle.Bold);
            _modeLabel.Margin = new Padding(0, 0, 0, 4);

            _safetyLabel.AutoSize = true;
            _safetyLabel.MaximumSize = new Size(Scale(980), 0);
            _safetyLabel.Margin = new Padding(0, 0, 0, 8);

            _progressLabel.AutoSize = true;
            _progressLabel.Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            _progressLabel.Margin = new Padding(0, 0, 0, 4);

            _stepTitleLabel.AutoSize = true;
            _stepTitleLabel.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            _stepTitleLabel.Margin = new Padding(0, 0, 0, 4);

            _stepSubtitleLabel.AutoSize = true;
            _stepSubtitleLabel.MaximumSize = new Size(Scale(980), 0);
            _stepSubtitleLabel.Margin = new Padding(0);

            _header.Controls.Add(_modeLabel, 0, 0);
            _header.Controls.Add(_safetyLabel, 0, 1);
            _header.Controls.Add(_progressLabel, 0, 2);
            _header.Controls.Add(_stepTitleLabel, 0, 3);
            _header.Controls.Add(_stepSubtitleLabel, 0, 4);

            _contentHost.Dock = DockStyle.Fill;
            _contentHost.Margin = new Padding(0);

            _navigationBar.Dock = DockStyle.Fill;
            _navigationBar.AutoSize = true;
            _navigationBar.FlowDirection = FlowDirection.RightToLeft;
            _navigationBar.WrapContents = false;
            _navigationBar.Padding = new Padding(0, 12, 0, 0);

            _backButton.AutoSize = true;
            _backButton.FlatStyle = FlatStyle.Flat;
            _backButton.Padding = new Padding(12, 7, 12, 7);
            _backButton.Click += (s, e) =>
            {
                if (_currentStep > 1)
                {
                    SetStep(_currentStep - 1, raiseEvent: true);
                }
            };

            _nextButton.AutoSize = true;
            _nextButton.FlatStyle = FlatStyle.Flat;
            _nextButton.Padding = new Padding(14, 7, 14, 7);
            _nextButton.Click += (s, e) => HandleNextStep();

            _restartButton.AutoSize = true;
            _restartButton.FlatStyle = FlatStyle.Flat;
            _restartButton.Padding = new Padding(12, 7, 12, 7);
            _restartButton.Click += (s, e) => RestartRequested?.Invoke();

            _navigationBar.Controls.Add(_nextButton);
            _navigationBar.Controls.Add(_backButton);
            _navigationBar.Controls.Add(_restartButton);

            _root.Controls.Add(_header, 0, 0);
            _root.Controls.Add(_contentHost, 0, 1);
            _root.Controls.Add(_navigationBar, 0, 2);
            Controls.Add(_root);
        }

        private void BuildStepPanels()
        {
            _step1Panel.Dock = DockStyle.Fill;
            _categoryCardsPanel.Dock = DockStyle.Fill;
            _categoryCardsPanel.WrapContents = true;
            _categoryCardsPanel.AutoScroll = true;
            _categoryCardsPanel.Padding = new Padding(0);
            _step1Panel.Controls.Add(_categoryCardsPanel);

            _step2Panel.Dock = DockStyle.Fill;
            _step2Panel.ColumnCount = 1;
            _step2Panel.RowCount = 2;
            _step2Panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _step2Panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _selectionSummaryLabel.AutoSize = true;
            _selectionSummaryLabel.Margin = new Padding(0, 0, 0, 8);
            _symptomsList.Dock = DockStyle.Fill;
            _symptomsList.CheckOnClick = true;
            _symptomsList.AccessibleName = "Patient symptom selection";
            _symptomsList.ItemCheck += SymptomsList_ItemCheck;
            _step2Panel.Controls.Add(_selectionSummaryLabel, 0, 0);
            _step2Panel.Controls.Add(_symptomsList, 0, 1);

            _step3Panel.Dock = DockStyle.Fill;
            _step3Panel.ColumnCount = 1;
            _step3Panel.RowCount = 2;
            _step3Panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _step3Panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            ConfigureNumeric(_ageInput, 0, 120, 25, 0, 1M, "Patient age");
            ConfigureNumeric(_tempInput, 30, 45, 37, 1, 0.1M, "Patient temperature");
            ConfigureNumeric(_heartRateInput, 20, 240, 72, 0, 1M, "Patient heart rate");
            ConfigureNumeric(_respRateInput, 4, 80, 16, 0, 1M, "Patient respiratory rate");
            ConfigureNumeric(_sbpInput, 50, 260, 120, 0, 1M, "Patient systolic blood pressure");
            ConfigureNumeric(_dbpInput, 30, 160, 80, 0, 1M, "Patient diastolic blood pressure");
            ConfigureNumeric(_spo2Input, 50, 100, 98, 0, 1M, "Patient oxygen saturation");

            _vitalsPanel.Dock = DockStyle.Top;
            _vitalsPanel.AutoSize = true;
            _vitalsPanel.WrapContents = true;
            _vitalsPanel.Controls.Add(BuildFieldLabel("Patient_Field_Age", "Age"));
            _vitalsPanel.Controls.Add(_ageInput);
            _vitalsPanel.Controls.Add(BuildFieldLabel("Patient_Field_Temp", "Temperature (C)"));
            _vitalsPanel.Controls.Add(_tempInput);
            _vitalsPanel.Controls.Add(BuildFieldLabel("Patient_Field_HeartRate", "Heart rate"));
            _vitalsPanel.Controls.Add(_heartRateInput);
            _vitalsPanel.Controls.Add(BuildFieldLabel("Patient_Field_RespRate", "Breathing rate"));
            _vitalsPanel.Controls.Add(_respRateInput);
            _vitalsPanel.Controls.Add(BuildFieldLabel("Patient_Field_BloodPressure", "Blood pressure"));
            _vitalsPanel.Controls.Add(_sbpInput);
            _vitalsPanel.Controls.Add(_dbpInput);
            _vitalsPanel.Controls.Add(BuildFieldLabel("Patient_Field_SpO2", "Oxygen saturation"));
            _vitalsPanel.Controls.Add(_spo2Input);

            _riskFactorsGroup.Dock = DockStyle.Top;
            _riskFactorsGroup.AutoSize = true;
            _riskFactorsGroup.Padding = new Padding(10);
            _riskFactorsPanel.Dock = DockStyle.Fill;
            _riskFactorsPanel.AutoSize = true;
            _riskFactorsPanel.FlowDirection = FlowDirection.TopDown;
            _riskFactorsPanel.WrapContents = false;
            _riskFactorsPanel.Controls.Add(_hemoptysisToggle);
            _riskFactorsPanel.Controls.Add(_estrogenToggle);
            _riskFactorsPanel.Controls.Add(_priorClotToggle);
            _riskFactorsPanel.Controls.Add(_unilateralLegToggle);
            _riskFactorsPanel.Controls.Add(_recentSurgeryToggle);
            _riskFactorsGroup.Controls.Add(_riskFactorsPanel);

            _step3Panel.Controls.Add(_vitalsPanel, 0, 0);
            _step3Panel.Controls.Add(_riskFactorsGroup, 0, 1);

            WireVitalsEvents();

            _step4Panel.Dock = DockStyle.Fill;
            _step4Panel.ColumnCount = 1;
            _step4Panel.RowCount = 4;
            _step4Panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _step4Panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _step4Panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _step4Panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _resultsSummaryLabel.AutoSize = true;
            _resultsSummaryLabel.MaximumSize = new Size(Scale(980), 0);
            _resultsSummaryLabel.Margin = new Padding(0, 0, 0, 10);

            _resultsCards.Dock = DockStyle.Fill;
            _resultsCards.AutoScroll = true;
            _resultsCards.FlowDirection = FlowDirection.TopDown;
            _resultsCards.WrapContents = false;

            _disclaimerLabel.AutoSize = true;
            _disclaimerLabel.MaximumSize = new Size(Scale(980), 0);
            _disclaimerLabel.Margin = new Padding(0, 10, 0, 0);
            _disclaimerLabel.Padding = new Padding(10);

            _step4Panel.Controls.Add(_resultsSummaryLabel, 0, 0);
            _step4Panel.Controls.Add(_resultsRedFlagBanner, 0, 1);
            _step4Panel.Controls.Add(_resultsCards, 0, 2);
            _step4Panel.Controls.Add(_disclaimerLabel, 0, 3);

            _contentHost.Controls.Add(_step1Panel);
            _contentHost.Controls.Add(_step2Panel);
            _contentHost.Controls.Add(_step3Panel);
            _contentHost.Controls.Add(_step4Panel);
        }

        private void ConfigureNumeric(NumericUpDown input, decimal min, decimal max, decimal value, int decimals, decimal increment, string accessibleName)
        {
            input.Minimum = min;
            input.Maximum = max;
            input.Value = Clamp(value, min, max);
            input.DecimalPlaces = decimals;
            input.Increment = increment;
            input.Width = Scale(72);
            input.AccessibleName = accessibleName;
        }

        private Label BuildFieldLabel(string key, string fallback)
        {
            return new Label
            {
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 0),
                Margin = new Padding(0, 0, 6, 0),
                Text = T(key, fallback)
            };
        }

        private void WireVitalsEvents()
        {
            _ageInput.ValueChanged += (_, _) => RaiseVitalsChanged();
            _tempInput.ValueChanged += (_, _) => RaiseVitalsChanged();
            _heartRateInput.ValueChanged += (_, _) => RaiseVitalsChanged();
            _respRateInput.ValueChanged += (_, _) => RaiseVitalsChanged();
            _sbpInput.ValueChanged += (_, _) => RaiseVitalsChanged();
            _dbpInput.ValueChanged += (_, _) => RaiseVitalsChanged();
            _spo2Input.ValueChanged += (_, _) => RaiseVitalsChanged();
            _hemoptysisToggle.CheckedChanged += (_, _) => RaiseVitalsChanged();
            _estrogenToggle.CheckedChanged += (_, _) => RaiseVitalsChanged();
            _priorClotToggle.CheckedChanged += (_, _) => RaiseVitalsChanged();
            _unilateralLegToggle.CheckedChanged += (_, _) => RaiseVitalsChanged();
            _recentSurgeryToggle.CheckedChanged += (_, _) => RaiseVitalsChanged();
        }

        private void RaiseVitalsChanged()
        {
            if (_applyingState) return;

            VitalsChanged?.Invoke(new PatientVitalsState(
                AgeYears: _ageInput.Value,
                TempC: _tempInput.Value,
                HeartRate: _heartRateInput.Value,
                RespRate: _respRateInput.Value,
                SystolicBP: _sbpInput.Value,
                DiastolicBP: _dbpInput.Value,
                SpO2: _spo2Input.Value,
                Hemoptysis: _hemoptysisToggle.Checked,
                EstrogenUse: _estrogenToggle.Checked,
                PriorDvtPe: _priorClotToggle.Checked,
                UnilateralLegSwelling: _unilateralLegToggle.Checked,
                RecentSurgeryTrauma: _recentSurgeryToggle.Checked));
        }

        private void SymptomsList_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (_applyingState || e.Index < 0 || e.Index >= _symptomsList.Items.Count) return;
            if (_symptomsList.Items[e.Index] is not SymptomItem item) return;

            if (e.NewValue == CheckState.Checked)
            {
                _selectedSymptoms.Add(item.Canonical);
            }
            else
            {
                _selectedSymptoms.Remove(item.Canonical);
            }

            BeginInvoke(new Action(() =>
            {
                UpdateSelectionSummary();
                UpdateNavigationState();
                SymptomsSelectionChanged?.Invoke(_selectedSymptoms.ToArray());
            }));
        }

        private void HandleNextStep()
        {
            switch (_currentStep)
            {
                case 1 when !string.IsNullOrWhiteSpace(_selectedCategory):
                    SetStep(2, raiseEvent: true);
                    break;
                case 2 when _selectedSymptoms.Count > 0:
                    SetStep(3, raiseEvent: true);
                    break;
                case 3:
                    RunCheckRequested?.Invoke();
                    SetStep(4, raiseEvent: true);
                    break;
            }
        }

        private void RenderCategoryButtons()
        {
            if (_categoryCardsPanel == null) return;

            _categoryCardsPanel.SuspendLayout();
            try
            {
                foreach (Control control in _categoryCardsPanel.Controls)
                {
                    control.Dispose();
                }
                _categoryCardsPanel.Controls.Clear();

                foreach (var option in _categoryOptions)
                {
                    var button = new Button
                    {
                        AutoSize = false,
                        Width = Scale(260),
                        Height = Scale(86),
                        Margin = new Padding(0, 0, 12, 12),
                        Padding = new Padding(14, 12, 14, 12),
                        FlatStyle = FlatStyle.Flat,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Tag = option.Canonical,
                        AccessibleName = option.Display,
                        Text = option.Display + Environment.NewLine + string.Format(T("Patient_CategoryCount", "{0} related symptoms"), option.SymptomCount)
                    };
                    button.Click += (s, e) =>
                    {
                        _selectedCategory = option.Canonical;
                        RenderCategoryButtons();
                        UpdateNavigationState();
                        CategorySelected?.Invoke(option.Canonical);
                    };
                    ApplyCategoryButtonStyle(button, string.Equals(_selectedCategory, option.Canonical, StringComparison.OrdinalIgnoreCase));
                    _categoryCardsPanel.Controls.Add(button);
                }
            }
            finally
            {
                _categoryCardsPanel.ResumeLayout();
            }
        }

        private void ApplyCategoryButtonStyle(Button button, bool selected)
        {
            if (_darkMode)
            {
                button.BackColor = selected ? Color.FromArgb(58, 95, 140) : Color.FromArgb(48, 52, 58);
                button.ForeColor = Color.WhiteSmoke;
                button.FlatAppearance.BorderColor = selected ? Color.FromArgb(122, 180, 240) : Color.FromArgb(74, 78, 84);
                button.FlatAppearance.BorderSize = 1;
            }
            else
            {
                button.BackColor = selected ? Color.FromArgb(222, 235, 252) : Color.White;
                button.ForeColor = Color.FromArgb(28, 28, 28);
                button.FlatAppearance.BorderColor = selected ? Color.FromArgb(25, 118, 210) : Color.FromArgb(210, 214, 220);
                button.FlatAppearance.BorderSize = 1;
            }
        }

        private void ApplyStepTexts()
        {
            _progressLabel.Text = string.Format(T("Patient_Progress", "Step {0} of 4"), _currentStep);

            switch (_currentStep)
            {
                case 1:
                    _stepTitleLabel.Text = T("Patient_Step1_Title", "Choose the body area or symptom family");
                    _stepSubtitleLabel.Text = T("Patient_Step1_Subtitle", "Start with the area that best matches your main concern.");
                    break;
                case 2:
                    _stepTitleLabel.Text = T("Patient_Step2_Title", "Select the symptoms that apply");
                    _stepSubtitleLabel.Text = T("Patient_Step2_Subtitle", "Choose one or more symptoms so the educational ranking can narrow down possibilities.");
                    break;
                case 3:
                    _stepTitleLabel.Text = T("Patient_Step3_Title", "Add optional safety details");
                    _stepSubtitleLabel.Text = T("Patient_Step3_Subtitle", "Vitals and safety questions help surface red flags, but the tool remains educational only.");
                    break;
                default:
                    _stepTitleLabel.Text = T("Patient_Step4_Title", "Review educational results");
                    _stepSubtitleLabel.Text = T("Patient_Step4_Subtitle", "Use these cards as a conversation starter with a qualified healthcare professional.");
                    break;
            }

            _nextButton.Text = _currentStep == 3
                ? T("Patient_RunCheck", "Show results")
                : T("Patient_Next", "Next");
        }

        private void UpdateStepVisibility()
        {
            _step1Panel.Visible = _currentStep == 1;
            _step2Panel.Visible = _currentStep == 2;
            _step3Panel.Visible = _currentStep == 3;
            _step4Panel.Visible = _currentStep == 4;

            if (_currentStep == 1) _step1Panel.BringToFront();
            else if (_currentStep == 2) _step2Panel.BringToFront();
            else if (_currentStep == 3) _step3Panel.BringToFront();
            else _step4Panel.BringToFront();
        }

        private void UpdateNavigationState()
        {
            _backButton.Enabled = _currentStep > 1;
            _restartButton.Visible = true;

            _nextButton.Visible = _currentStep < 4;
            _nextButton.Enabled = _currentStep switch
            {
                1 => !string.IsNullOrWhiteSpace(_selectedCategory),
                2 => _selectedSymptoms.Count > 0,
                3 => true,
                _ => false
            };
        }

        private void UpdateSelectionSummary()
        {
            string helper = _selectedSymptoms.Count == 0
                ? T("Patient_Step2_Empty", "Select at least one symptom to continue.")
                : string.Format(T("Patient_SelectedCount", "{0} symptom(s) selected"), _selectedSymptoms.Count);
            _selectionSummaryLabel.Text = helper;
        }

        private void ApplyTheme()
        {
            BackColor = _darkMode ? Color.FromArgb(24, 26, 30) : Color.FromArgb(248, 250, 252);
            Color panel = _darkMode ? Color.FromArgb(36, 40, 45) : Color.White;
            Color text = _darkMode ? Color.WhiteSmoke : Color.FromArgb(28, 28, 28);
            Color subtle = _darkMode ? Color.Gainsboro : Color.DimGray;

            _root.BackColor = BackColor;
            _header.BackColor = BackColor;
            _contentHost.BackColor = BackColor;
            _step1Panel.BackColor = panel;
            _step2Panel.BackColor = panel;
            _step3Panel.BackColor = panel;
            _step4Panel.BackColor = panel;
            _modeLabel.ForeColor = text;
            _safetyLabel.ForeColor = subtle;
            _progressLabel.ForeColor = subtle;
            _stepTitleLabel.ForeColor = text;
            _stepSubtitleLabel.ForeColor = subtle;
            _selectionSummaryLabel.ForeColor = subtle;
            _resultsSummaryLabel.ForeColor = text;
            _disclaimerLabel.ForeColor = text;
            _disclaimerLabel.BackColor = _darkMode ? Color.FromArgb(64, 48, 0) : Color.FromArgb(255, 248, 232);
            _symptomsList.BackColor = _darkMode ? Color.FromArgb(28, 31, 36) : Color.White;
            _symptomsList.ForeColor = text;
            _riskFactorsGroup.BackColor = panel;
            _riskFactorsGroup.ForeColor = text;
            _riskFactorsPanel.BackColor = panel;
            _resultsCards.BackColor = panel;
            _vitalsPanel.BackColor = panel;
            _step1Panel.Padding = new Padding(14);
            _step2Panel.Padding = new Padding(14);
            _step3Panel.Padding = new Padding(14);
            _step4Panel.Padding = new Padding(14);

            foreach (Control control in _riskFactorsPanel.Controls)
            {
                control.BackColor = panel;
                control.ForeColor = text;
            }
            foreach (Control control in _vitalsPanel.Controls)
            {
                control.BackColor = panel;
                control.ForeColor = text;
            }

            StyleNavigationButton(_backButton, secondary: true);
            StyleNavigationButton(_restartButton, secondary: true);
            StyleNavigationButton(_nextButton, secondary: false);
        }

        private void StyleNavigationButton(Button button, bool secondary)
        {
            if (_darkMode)
            {
                button.BackColor = secondary ? Color.FromArgb(52, 56, 62) : Color.FromArgb(56, 102, 164);
                button.ForeColor = Color.WhiteSmoke;
            }
            else
            {
                button.BackColor = secondary ? Color.White : Color.FromArgb(25, 118, 210);
                button.ForeColor = secondary ? Color.FromArgb(28, 28, 28) : Color.White;
            }
            button.FlatAppearance.BorderColor = secondary ? Color.Silver : button.BackColor;
            button.FlatAppearance.BorderSize = 1;
        }

        private decimal Clamp(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));

        private int Scale(int px)
        {
            using var graphics = CreateGraphics();
            return (int)Math.Round(px * graphics.DpiX / 96f);
        }

        private string T(string key, string fallback) => _translationService?.T(key) ?? fallback;
    }
}
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Services;
using static SymptomCheckerApp.Services.SymptomCheckerService;
using System.Drawing;
using SymptomCheckerApp.Models;
using System.Collections.Generic;
using SymptomChecker.Services;

namespace SymptomCheckerApp.UI
{
    public class MainForm : Form
    {
    private class ListItem
    {
        public string Canonical { get; }
        public string Display { get; }
        public ListItem(string canonical, string display)
        {
            Canonical = canonical;
            Display = display;
        }
        public override string ToString() => Display;
    }

    private class LangItem
    {
        public string Code { get; }
        public string Display { get; }
        public LangItem(string code, string display)
        {
            Code = code; Display = display;
        }
        public override string ToString() => Display;
    }
    private class CatItem
    {
        public string Canonical { get; }
        public string Display { get; }
        public CatItem(string canonical, string display)
        {
            Canonical = canonical; Display = display;
        }
        public override string ToString() => Display;
    }

    // Header row used to group results by category in the owner-drawn ListBox
    private sealed class GroupHeader
    {
        public string CanonicalCategory { get; }
        public string Display { get; }
        public GroupHeader(string canonicalCategory, string display)
        {
            CanonicalCategory = canonicalCategory;
            Display = display;
        }
        public override string ToString() => Display;
    }

    private readonly CheckedListBox _symptomList = new CheckedListBox();
    private readonly Button _checkButton = new Button();
    private readonly Button _exitButton = new Button();
    private readonly ListBox _resultsList = new ListBox();
    private readonly Label _triageBanner = new Label();
        private readonly Label _disclaimer = new Label();
        private readonly ComboBox _modelSelector = new ComboBox();
        private readonly NumericUpDown _threshold = new NumericUpDown();
    private readonly NumericUpDown _minMatch = new NumericUpDown();
    private readonly NumericUpDown _topK = new NumericUpDown();
    private readonly TextBox _filterBox = new TextBox();
    private readonly ComboBox _categorySelector = new ComboBox();
    private readonly Button _selectCategoryButton = new Button();
    private readonly Button _clearCategoryButton = new Button();
    private readonly CheckBox _showOnlyCategory = new CheckBox();
    private readonly Button _selectVisibleButton = new Button();
    private readonly Button _clearVisibleButton = new Button();
    private readonly Button _saveSessionButton = new Button();
    private readonly Button _loadSessionButton = new Button();
    private readonly Button _syncButton = new Button();
    private readonly Button _missingTransButton = new Button();
    private readonly ComboBox _languageSelector = new ComboBox();
    private readonly ToolTip _toolTip = new ToolTip();
    private readonly ContextMenuStrip _resultsContext = new ContextMenuStrip();
    private readonly Label _lblLanguage = new Label();
    private readonly CheckBox _darkModeToggle = new CheckBox();
    private readonly Label _lblFilter = new Label();
    private readonly Label _lblCategory = new Label();
    private readonly Label _lblModel = new Label();
    private readonly Label _lblThresh = new Label();
    private readonly Label _lblMinMatch = new Label();
    private readonly Label _lblTopK = new Label();
    // Vitals controls
    private readonly Label _lblVitals = new Label();
    private readonly NumericUpDown _numTempC = new NumericUpDown();
    private readonly NumericUpDown _numHR = new NumericUpDown();
    private readonly NumericUpDown _numRR = new NumericUpDown();
    private readonly NumericUpDown _numSBP = new NumericUpDown();
    private readonly NumericUpDown _numDBP = new NumericUpDown();
    private readonly NumericUpDown _numSpO2 = new NumericUpDown();
    private readonly NumericUpDown _numWeightKg = new NumericUpDown();
    private readonly Label _lblTemp = new Label();
    private readonly Label _lblHR = new Label();
    private readonly Label _lblRR = new Label();
    private readonly Label _lblBP = new Label();
    private readonly Label _lblSpO2 = new Label();
    private readonly Label _lblWeight = new Label();
    // Decision rules (Centor/McIsaac)
    private readonly Label _lblRules = new Label();
    private readonly GroupBox _grpCentor = new GroupBox();
    private readonly CheckBox _centorFever = new CheckBox();
    private readonly CheckBox _centorTonsils = new CheckBox();
    private readonly CheckBox _centorNodes = new CheckBox();
    private readonly CheckBox _centorNoCough = new CheckBox();
    private readonly Label _centorScore = new Label();
    private readonly Label _mcIsaacScore = new Label();
    private readonly Label _centorAdvice = new Label();
    private readonly Label _lblAge = new Label();
    private readonly NumericUpDown _numAge = new NumericUpDown();
    // PERC Rule (Pulmonary Embolism Rule-out Criteria)
    private readonly GroupBox _grpPerc = new GroupBox();
    private readonly Label _percResult = new Label();
    private readonly CheckBox _percHemoptysis = new CheckBox();
    private readonly CheckBox _percEstrogen = new CheckBox();
    private readonly CheckBox _percPriorDvtPe = new CheckBox();
    private readonly CheckBox _percUnilateralLeg = new CheckBox();
    private readonly CheckBox _percRecentSurgery = new CheckBox();
    private SymptomCheckerService? _service;
    private CategoriesService? _categoriesService;
    private SynonymService? _synonymService;
    private TranslationService? _translationService;
    private SettingsService? _settingsService;
    private System.Collections.Generic.List<ConditionMatch> _lastResults = new System.Collections.Generic.List<ConditionMatch>();
    private List<string> _allSymptoms = new List<string>();
    private HashSet<string> _checkedSymptoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    // Maps each _resultsList item index to the corresponding _lastResults index; headers map to -1
    private readonly List<int> _resultIndexMap = new List<int>();

        public MainForm()
        {
            Text = "Symptom Checker (Educational)";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterScreen;

            // Accessibility: keyboard shortcuts
            KeyPreview = true; // enable form-level shortcut handling
            this.KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.F)
                {
                    _filterBox.Focus();
                    e.Handled = true;
                }
                else if (e.Alt && e.KeyCode == Keys.C)
                {
                    if (_checkButton.Enabled) _checkButton.PerformClick();
                    e.Handled = true;
                }
            };

            InitializeLayout();
            Load += MainForm_Load;
            this.FormClosing += (s, e) =>
            {
                try { _settingsService?.Save(); } catch { }
                try
                {
                    var reportPath = Path.Combine(AppContext.BaseDirectory, "data", "translation_report.txt");
                    _translationService?.SaveMissingReport(reportPath);
                }
                catch { }
            };
        }

        private void InitializeLayout()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 400
            };

            // Left side: filter controls + symptom list
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var filterBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(3) };
            _lblFilter.Text = "Filter:";
            _lblFilter.AutoSize = true;
            _lblFilter.Padding = new Padding(0, 6, 0, 0);
            _filterBox.Width = 220;
            _filterBox.AccessibleName = "Filter symptoms";
            _filterBox.AccessibleDescription = "Type text to filter the symptoms list";
            _filterBox.TextChanged += (s, e) =>
            {
                RefreshSymptomList();
                if (_settingsService != null)
                {
                    _settingsService.Settings.FilterText = _filterBox.Text;
                    _settingsService.Save();
                }
            };

            _selectVisibleButton.Text = "Select Visible";
            _selectVisibleButton.AutoSize = true;
            _selectVisibleButton.Click += (s, e) => SelectVisibleSymptoms();

            _clearVisibleButton.Text = "Clear Visible";
            _clearVisibleButton.AutoSize = true;
            _clearVisibleButton.Click += (s, e) => ClearVisibleSymptoms();

            _lblCategory.Text = "Category:";
            _lblCategory.AutoSize = true;
            _lblCategory.Padding = new Padding(10, 6, 0, 0);
            _categorySelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _categorySelector.Width = 180;
            _categorySelector.SelectedIndexChanged += (s, e) =>
            {
                _showOnlyCategory.Checked = true;
                if (_settingsService != null)
                {
                    _settingsService.Settings.SelectedCategory = (_categorySelector.SelectedItem as CatItem)?.Canonical;
                    _settingsService.Settings.ShowOnlyCategory = true;
                    _settingsService.Save();
                }
                RefreshSymptomList();
            };
            _selectCategoryButton.Text = "Select Category";
            _selectCategoryButton.AutoSize = true;
            _selectCategoryButton.Click += (s, e) => SelectByCategory();
            _clearCategoryButton.Text = "Clear Category";
            _clearCategoryButton.AutoSize = true;
            _clearCategoryButton.Click += (s, e) => ClearByCategory();
            _showOnlyCategory.Text = "Show only category";
            _showOnlyCategory.AutoSize = true;
            _showOnlyCategory.CheckedChanged += (s, e) =>
            {
                if (_settingsService != null)
                {
                    _settingsService.Settings.ShowOnlyCategory = _showOnlyCategory.Checked;
                    // Persist selected category too if any
                    _settingsService.Settings.SelectedCategory = (_categorySelector.SelectedItem as CatItem)?.Canonical;
                    _settingsService.Save();
                }
                RefreshSymptomList();
            };

            filterBar.Controls.Add(_lblFilter);
            filterBar.Controls.Add(_filterBox);
            filterBar.Controls.Add(_selectVisibleButton);
            filterBar.Controls.Add(_clearVisibleButton);
            _saveSessionButton.Text = "Save";
            _saveSessionButton.AutoSize = true;
            _saveSessionButton.Click += (s, e) => SaveSession();
            _loadSessionButton.Text = "Load";
            _loadSessionButton.AutoSize = true;
            _loadSessionButton.Click += (s, e) => LoadSession();
            filterBar.Controls.Add(_saveSessionButton);
            filterBar.Controls.Add(_loadSessionButton);
            filterBar.Controls.Add(_lblCategory);
            filterBar.Controls.Add(_categorySelector);
            filterBar.Controls.Add(_selectCategoryButton);
            filterBar.Controls.Add(_clearCategoryButton);
            filterBar.Controls.Add(_showOnlyCategory);

            _symptomList.Dock = DockStyle.Fill;
            _symptomList.CheckOnClick = true;
            _symptomList.AccessibleName = "Symptoms list";
            _symptomList.AccessibleDescription = "List of symptoms to select";
            _symptomList.ItemCheck += SymptomList_ItemCheck;

            var rightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6
            };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var topControls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var vitalsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var rulesRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

            _modelSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _modelSelector.Items.AddRange(new object[] { DetectionModel.Jaccard, DetectionModel.Cosine, DetectionModel.NaiveBayes });
            _modelSelector.SelectedIndexChanged += (s, e) =>
            {
                if (_settingsService != null && _modelSelector.SelectedItem != null)
                {
                    _settingsService.Settings.Model = _modelSelector.SelectedItem.ToString();
                    _settingsService.Save();
                }
            };
            _modelSelector.SelectedIndex = 0;

            _threshold.Minimum = 0;
            _threshold.Maximum = 100;
            _threshold.DecimalPlaces = 0;
            _threshold.Value = 0;
            _threshold.Width = 80;
            _threshold.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.ThresholdPercent = (int)_threshold.Value; _settingsService.Save(); } };
            _lblModel.Text = "Model:";
            _lblModel.AutoSize = true; _lblModel.Padding = new Padding(0, 6, 0, 0);
            _lblThresh.Text = "Threshold (%):";
            _lblThresh.AutoSize = true; _lblThresh.Padding = new Padding(10, 6, 0, 0);

            _minMatch.Minimum = 0; // 0 means any match count
            _minMatch.Maximum = 10;
            _minMatch.Value = 1;
            _minMatch.Width = 60;
            _minMatch.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.MinMatch = (int)_minMatch.Value; _settingsService.Save(); } };
            _lblMinMatch.Text = "Min match:"; _lblMinMatch.AutoSize = true; _lblMinMatch.Padding = new Padding(10, 6, 0, 0);

            _topK.Minimum = 0; // 0 = unlimited
            _topK.Maximum = 1000;
            _topK.Value = 0;
            _topK.Width = 70;
            _topK.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.TopK = (int)_topK.Value; _settingsService.Save(); } };
            _lblTopK.Text = "Top-K:"; _lblTopK.AutoSize = true; _lblTopK.Padding = new Padding(10, 6, 0, 0);

            _lblLanguage.Text = "Language:"; _lblLanguage.AutoSize = true; _lblLanguage.Padding = new Padding(10, 6, 0, 0);
            _languageSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _languageSelector.Width = 120;
            _languageSelector.SelectedIndexChanged += (s, e) => OnLanguageChanged();

            _darkModeToggle.Text = "Dark";
            _darkModeToggle.AutoSize = true;
            _darkModeToggle.CheckedChanged += (s, e) => { ApplyTheme(); if (_settingsService != null) { _settingsService.Settings.DarkMode = _darkModeToggle.Checked; _settingsService.Save(); } };

            _checkButton.Text = "Check";
            _checkButton.AutoSize = true;
            _checkButton.AccessibleName = "Check";
            _checkButton.AccessibleDescription = "Run the symptom checker";
            _checkButton.Click += CheckButton_Click;
            _checkButton.Enabled = false; // disabled until at least one symptom selected

            _exitButton.Text = "Exit";
            _exitButton.AutoSize = true;
            _exitButton.Click += (s, e) => this.Close();

            _syncButton.Text = "Sync";
            _syncButton.AutoSize = true;
            _syncButton.Click += async (s, e) => await SyncFromWikidataAsync();

            // Missing translations report UI
            _missingTransButton.Text = "Missing Translations";
            _missingTransButton.AutoSize = true;
            _missingTransButton.Click += (s, e) => ShowMissingTranslationsDialog();

            _resultsList.Dock = DockStyle.Fill;
            _resultsList.DrawMode = DrawMode.OwnerDrawVariable;
            _resultsList.ItemHeight = 22;
            _resultsList.AccessibleName = "Results";
            _resultsList.AccessibleDescription = "List of matching conditions";
            _resultsList.DrawItem += ResultsList_DrawItem;
            _resultsList.MeasureItem += ResultsList_MeasureItem;
            _resultsList.DoubleClick += ResultsList_DoubleClick;
            _resultsList.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) _resultsContext.Show(_resultsList, e.Location); };
            // Configure tooltip (infobulle) to guide user to double-click for details
            _toolTip.ShowAlways = true; _toolTip.IsBalloon = true; _toolTip.AutoPopDelay = 8000; _toolTip.InitialDelay = 500; _toolTip.ReshowDelay = 200;
            _toolTip.SetToolTip(_resultsList, "Tip: Double-click a result to view details (educational treatments, OTC examples, self-care).");

            _disclaimer.Text = "⚠️ Educational only. Not medical advice.";
            _disclaimer.AutoSize = true;
            _disclaimer.Padding = new Padding(5);
            _disclaimer.AccessibleName = "Disclaimer";
            _disclaimer.AccessibleDescription = "Educational only disclaimer";

            // Triage banner for red flags (localized content built at runtime)
            _triageBanner.Visible = false;
            _triageBanner.AutoSize = true;
            _triageBanner.Padding = new Padding(6);
            _triageBanner.Margin = new Padding(6, 3, 6, 3);
            _triageBanner.BackColor = Color.FromArgb(255, 245, 230); // soft orange background
            _triageBanner.ForeColor = Color.FromArgb(120, 60, 0);
            _triageBanner.AccessibleName = "Red flags";
            _triageBanner.AccessibleDescription = "Banner showing potential red flags";

            // Vitals setup and defaults
            _lblVitals.Text = "Vitals:"; _lblVitals.AutoSize = true; _lblVitals.Padding = new Padding(10, 6, 0, 0);
            _lblTemp.Text = "Temp (°C):"; _lblTemp.AutoSize = true; _lblTemp.Padding = new Padding(10, 6, 0, 0);
            _numTempC.DecimalPlaces = 1; _numTempC.Increment = 0.1M; _numTempC.Minimum = 30; _numTempC.Maximum = 45; _numTempC.Width = 70;
            _numTempC.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.TempC = (double)_numTempC.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblHR.Text = "HR (bpm):"; _lblHR.AutoSize = true; _lblHR.Padding = new Padding(10, 6, 0, 0);
            _numHR.Minimum = 20; _numHR.Maximum = 240; _numHR.Width = 70;
            _numHR.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.HeartRate = (int)_numHR.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblRR.Text = "RR (/min):"; _lblRR.AutoSize = true; _lblRR.Padding = new Padding(10, 6, 0, 0);
            _numRR.Minimum = 4; _numRR.Maximum = 80; _numRR.Width = 70;
            _numRR.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.RespRate = (int)_numRR.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblBP.Text = "BP (SBP/DBP):"; _lblBP.AutoSize = true; _lblBP.Padding = new Padding(10, 6, 0, 0);
            _numSBP.Minimum = 50; _numSBP.Maximum = 260; _numSBP.Width = 70;
            _numSBP.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.SystolicBP = (int)_numSBP.Value; _settingsService.Save(); } UpdateDecisionRules(); };
            _numDBP.Minimum = 30; _numDBP.Maximum = 160; _numDBP.Width = 70;
            _numDBP.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.DiastolicBP = (int)_numDBP.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblSpO2.Text = "SpO₂ (%):"; _lblSpO2.AutoSize = true; _lblSpO2.Padding = new Padding(10, 6, 0, 0);
            _numSpO2.Minimum = 50; _numSpO2.Maximum = 100; _numSpO2.Width = 70;
            _numSpO2.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.SpO2 = (int)_numSpO2.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblWeight.Text = "Weight (kg):"; _lblWeight.AutoSize = true; _lblWeight.Padding = new Padding(10, 6, 0, 0);
            _numWeightKg.DecimalPlaces = 1; _numWeightKg.Increment = 0.5M; _numWeightKg.Minimum = 2; _numWeightKg.Maximum = 350; _numWeightKg.Width = 80;
            _numWeightKg.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.WeightKg = (double)_numWeightKg.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            leftPanel.Controls.Add(filterBar, 0, 0);
            leftPanel.Controls.Add(_symptomList, 0, 1);
            split.Panel1.Controls.Add(leftPanel);
            topControls.Controls.Add(_lblModel);
            topControls.Controls.Add(_modelSelector);
            topControls.Controls.Add(_lblThresh);
            topControls.Controls.Add(_threshold);
            topControls.Controls.Add(_lblMinMatch);
            topControls.Controls.Add(_minMatch);
            topControls.Controls.Add(_lblTopK);
            topControls.Controls.Add(_topK);
            topControls.Controls.Add(_lblLanguage);
            topControls.Controls.Add(_languageSelector);
            topControls.Controls.Add(_darkModeToggle);
            topControls.Controls.Add(_checkButton);
            topControls.Controls.Add(_exitButton);
            topControls.Controls.Add(_syncButton);
            topControls.Controls.Add(_missingTransButton);
            // Top bar (models etc.)
            rightPanel.Controls.Add(topControls, 0, 0);
            // Vitals row
            vitalsRow.Controls.Add(_lblVitals);
            vitalsRow.Controls.Add(_lblTemp);
            vitalsRow.Controls.Add(_numTempC);
            vitalsRow.Controls.Add(_lblHR);
            vitalsRow.Controls.Add(_numHR);
            vitalsRow.Controls.Add(_lblRR);
            vitalsRow.Controls.Add(_numRR);
            vitalsRow.Controls.Add(_lblBP);
            vitalsRow.Controls.Add(_numSBP);
            vitalsRow.Controls.Add(_numDBP);
            vitalsRow.Controls.Add(_lblSpO2);
            vitalsRow.Controls.Add(_numSpO2);
            vitalsRow.Controls.Add(_lblWeight);
            vitalsRow.Controls.Add(_numWeightKg);
            rightPanel.Controls.Add(vitalsRow, 0, 1);
            // Decision rules row (Centor/McIsaac)
            _lblRules.Text = "Decision rules:"; _lblRules.AutoSize = true; _lblRules.Padding = new Padding(10, 6, 0, 0);
            _lblAge.Text = "Age (years):"; _lblAge.AutoSize = true; _lblAge.Padding = new Padding(10, 6, 0, 0);
            _numAge.Minimum = 0; _numAge.Maximum = 120; _numAge.Value = 25; _numAge.Width = 70;
            _numAge.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.AgeYears = (int)_numAge.Value; _settingsService.Save(); } UpdateDecisionRules(); };
            _grpCentor.Text = "Centor/McIsaac"; _grpCentor.AutoSize = true; _grpCentor.Padding = new Padding(6);
            _centorFever.Text = "Fever (or Temp ≥38°C)"; _centorFever.AutoSize = true; _centorFever.Enabled = false;
            _centorTonsils.Text = "Tonsillar exudates/swelling"; _centorTonsils.AutoSize = true; _centorTonsils.Enabled = false;
            _centorNodes.Text = "Tender anterior cervical nodes"; _centorNodes.AutoSize = true; _centorNodes.Enabled = false;
            _centorNoCough.Text = "Absence of cough"; _centorNoCough.AutoSize = true; _centorNoCough.Enabled = false;
            _centorScore.Text = "Centor: 0"; _centorScore.AutoSize = true; _centorScore.Padding = new Padding(10, 6, 0, 0);
            _mcIsaacScore.Text = "McIsaac: 0"; _mcIsaacScore.AutoSize = true; _mcIsaacScore.Padding = new Padding(10, 6, 0, 0);
            _centorAdvice.Text = ""; _centorAdvice.AutoSize = true; _centorAdvice.Padding = new Padding(10, 6, 0, 0);
            var centorPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            centorPanel.Controls.Add(_centorFever);
            centorPanel.Controls.Add(_centorTonsils);
            centorPanel.Controls.Add(_centorNodes);
            centorPanel.Controls.Add(_centorNoCough);
            centorPanel.Controls.Add(_centorScore);
            centorPanel.Controls.Add(_mcIsaacScore);
            centorPanel.Controls.Add(_centorAdvice);
            _grpCentor.Controls.Add(centorPanel);

            // PERC group
            _grpPerc.Text = "PERC"; _grpPerc.AutoSize = true; _grpPerc.Padding = new Padding(6);
            _percResult.Text = "PERC: not evaluated"; _percResult.AutoSize = true; _percResult.Padding = new Padding(10, 6, 0, 0);
            _percHemoptysis.Text = "Hemoptysis"; _percHemoptysis.AutoSize = true;
            _percHemoptysis.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercHemoptysis = _percHemoptysis.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percEstrogen.Text = "Estrogen use"; _percEstrogen.AutoSize = true;
            _percEstrogen.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercEstrogenUse = _percEstrogen.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percPriorDvtPe.Text = "Prior DVT/PE"; _percPriorDvtPe.AutoSize = true;
            _percPriorDvtPe.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercPriorDvtPe = _percPriorDvtPe.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percUnilateralLeg.Text = "Unilateral leg swelling"; _percUnilateralLeg.AutoSize = true;
            _percUnilateralLeg.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercUnilateralLegSwelling = _percUnilateralLeg.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percRecentSurgery.Text = "Recent surgery/trauma"; _percRecentSurgery.AutoSize = true;
            _percRecentSurgery.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercRecentSurgeryTrauma = _percRecentSurgery.Checked; _settingsService.Save(); } UpdatePercRule(); };
            var percPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            percPanel.Controls.Add(_percHemoptysis);
            percPanel.Controls.Add(_percEstrogen);
            percPanel.Controls.Add(_percPriorDvtPe);
            percPanel.Controls.Add(_percUnilateralLeg);
            percPanel.Controls.Add(_percRecentSurgery);
            percPanel.Controls.Add(_percResult);
            _grpPerc.Controls.Add(percPanel);
            rulesRow.Controls.Add(_lblRules);
            rulesRow.Controls.Add(_lblAge);
            rulesRow.Controls.Add(_numAge);
            rulesRow.Controls.Add(_grpCentor);
            rulesRow.Controls.Add(_grpPerc);
            rightPanel.Controls.Add(rulesRow, 0, 2);
            rightPanel.Controls.Add(_triageBanner, 0, 3);
            rightPanel.Controls.Add(_resultsList, 0, 4);
            rightPanel.Controls.Add(_disclaimer, 0, 5);
            split.Panel2.Controls.Add(rightPanel);

            Controls.Add(split);

            // Context menu for results (Copy details, Print)
            var miCopy = new ToolStripMenuItem("Copy Details", null, (s, e) => CopySelectedDetailsToClipboard());
            var miPrint = new ToolStripMenuItem("Print Details", null, (s, e) => PrintSelectedDetails());
            _resultsContext.Items.Add(miCopy);
            _resultsContext.Items.Add(miPrint);
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "data", "conditions.json");
                _service = new SymptomCheckerService(jsonPath);
                var symptoms = _service.GetAllUniqueSymptoms();
                _allSymptoms = symptoms.ToList();
                // Load settings
                var settingsPath = Path.Combine(AppContext.BaseDirectory, "data", "settings.json");
                _settingsService = new SettingsService(settingsPath);
                // Load translations
                var trPath = Path.Combine(AppContext.BaseDirectory, "data", "translations.json");
                if (File.Exists(trPath))
                {
                    _translationService = new TranslationService(trPath);
                    // Fill language selector
                    _languageSelector.Items.Clear();
                    foreach (var code in _translationService.GetSupportedLanguages())
                    {
                        string display = code.ToLowerInvariant() switch
                        {
                            "fr" => "Français",
                            "ar" => "العربية",
                            _ => "English"
                        };
                        _languageSelector.Items.Add(new LangItem(code, display));
                    }
                    if (_languageSelector.Items.Count > 0)
                    {
                        // Default: from settings or English
                        int idx = 0;
                        for (int i = 0; i < _languageSelector.Items.Count; i++)
                        {
                            if (_languageSelector.Items[i] is LangItem liItem)
                            {
                                var code = liItem.Code ?? string.Empty;
                                if (!string.IsNullOrEmpty(_settingsService?.Settings.Language) && code.Equals(_settingsService!.Settings.Language, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
                                if (string.IsNullOrEmpty(_settingsService?.Settings.Language) && code.Equals("en", StringComparison.OrdinalIgnoreCase)) { idx = i; }
                            }
                        }
                        _languageSelector.SelectedIndex = idx;
                    }
                }
                // Load categories
                var catPath = Path.Combine(AppContext.BaseDirectory, "data", "categories.json");
                if (File.Exists(catPath))
                {
                    _categoriesService = new CategoriesService(catPath);
                    var cats = _categoriesService.GetAllCategories();
                    _categorySelector.Items.Clear();
                    foreach (var c in cats)
                    {
                        string disp = _translationService?.Category(c.Name) ?? c.Name;
                        _categorySelector.Items.Add(new CatItem(c.Name, disp));
                    }
                    if (_categorySelector.Items.Count > 0)
                    {
                        // Try restore selection from settings
                        string? desired = _settingsService?.Settings.SelectedCategory;
                        if (!string.IsNullOrEmpty(desired))
                        {
                            for (int i = 0; i < _categorySelector.Items.Count; i++)
                            {
                                if (_categorySelector.Items[i] is CatItem ci && ci.Canonical != null && desired != null && ci.Canonical.Equals(desired, StringComparison.OrdinalIgnoreCase))
                                { _categorySelector.SelectedIndex = i; break; }
                            }
                        }
                        if (_categorySelector.SelectedIndex < 0) _categorySelector.SelectedIndex = 0;
                    }
                }
                    // Load synonyms
                    var synPath = Path.Combine(AppContext.BaseDirectory, "data", "synonyms.json");
                    if (File.Exists(synPath))
                    {
                        _synonymService = new SynonymService(synPath);
                    }
                ApplyTranslations();
                // Apply theme after settings are loaded
                _darkModeToggle.Checked = _settingsService?.Settings.DarkMode ?? false;
                // Restore other UI state from settings
                if (!string.IsNullOrEmpty(_settingsService?.Settings.Model))
                {
                    for (int i = 0; i < _modelSelector.Items.Count; i++)
                    {
                        if (string.Equals(_modelSelector.Items[i]?.ToString(), _settingsService!.Settings.Model, StringComparison.OrdinalIgnoreCase))
                        { _modelSelector.SelectedIndex = i; break; }
                    }
                }
                // Restore numeric values
                try { if (_settingsService != null) _threshold.Value = Math.Max(_threshold.Minimum, Math.Min(_threshold.Maximum, _settingsService.Settings.ThresholdPercent)); } catch { }
                try { if (_settingsService != null) _minMatch.Value = Math.Max(_minMatch.Minimum, Math.Min(_minMatch.Maximum, _settingsService.Settings.MinMatch)); } catch { }
                try { if (_settingsService != null) _topK.Value = Math.Max(_topK.Minimum, Math.Min(_topK.Maximum, _settingsService.Settings.TopK)); } catch { }
                // Restore vitals values if present
                try { if (_settingsService?.Settings.TempC.HasValue == true) _numTempC.Value = (decimal)Math.Max((double)_numTempC.Minimum, Math.Min((double)_numTempC.Maximum, _settingsService!.Settings.TempC!.Value)); } catch { }
                try { if (_settingsService?.Settings.HeartRate.HasValue == true) _numHR.Value = Math.Max(_numHR.Minimum, Math.Min(_numHR.Maximum, _settingsService!.Settings.HeartRate!.Value)); } catch { }
                try { if (_settingsService?.Settings.RespRate.HasValue == true) _numRR.Value = Math.Max(_numRR.Minimum, Math.Min(_numRR.Maximum, _settingsService!.Settings.RespRate!.Value)); } catch { }
                try { if (_settingsService?.Settings.SystolicBP.HasValue == true) _numSBP.Value = Math.Max(_numSBP.Minimum, Math.Min(_numSBP.Maximum, _settingsService!.Settings.SystolicBP!.Value)); } catch { }
                try { if (_settingsService?.Settings.DiastolicBP.HasValue == true) _numDBP.Value = Math.Max(_numDBP.Minimum, Math.Min(_numDBP.Maximum, _settingsService!.Settings.DiastolicBP!.Value)); } catch { }
                try { if (_settingsService?.Settings.SpO2.HasValue == true) _numSpO2.Value = Math.Max(_numSpO2.Minimum, Math.Min(_numSpO2.Maximum, _settingsService!.Settings.SpO2!.Value)); } catch { }
                try { if (_settingsService?.Settings.WeightKg.HasValue == true) _numWeightKg.Value = (decimal)Math.Max((double)_numWeightKg.Minimum, Math.Min((double)_numWeightKg.Maximum, _settingsService!.Settings.WeightKg!.Value)); } catch { }
                // Restore age and PERC persisted flags
                try { if (_settingsService?.Settings.AgeYears.HasValue == true) _numAge.Value = Math.Max(_numAge.Minimum, Math.Min(_numAge.Maximum, _settingsService!.Settings.AgeYears!.Value)); } catch { }
                try { if (_settingsService?.Settings.PercHemoptysis.HasValue == true) _percHemoptysis.Checked = _settingsService!.Settings.PercHemoptysis!.Value; } catch { }
                try { if (_settingsService?.Settings.PercEstrogenUse.HasValue == true) _percEstrogen.Checked = _settingsService!.Settings.PercEstrogenUse!.Value; } catch { }
                try { if (_settingsService?.Settings.PercPriorDvtPe.HasValue == true) _percPriorDvtPe.Checked = _settingsService!.Settings.PercPriorDvtPe!.Value; } catch { }
                try { if (_settingsService?.Settings.PercUnilateralLegSwelling.HasValue == true) _percUnilateralLeg.Checked = _settingsService!.Settings.PercUnilateralLegSwelling!.Value; } catch { }
                try { if (_settingsService?.Settings.PercRecentSurgeryTrauma.HasValue == true) _percRecentSurgery.Checked = _settingsService!.Settings.PercRecentSurgeryTrauma!.Value; } catch { }
                // Restore filter and category visibility
                try { _filterBox.Text = _settingsService?.Settings.FilterText ?? string.Empty; } catch { }
                try { _showOnlyCategory.Checked = _settingsService?.Settings.ShowOnlyCategory ?? false; } catch { }
                ApplyTheme();
                RefreshSymptomList();
                UpdateDecisionRules();
                UpdatePercRule();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnLanguageChanged()
        {
            if (_translationService == null) return;
            if (_languageSelector.SelectedItem is LangItem li)
            {
                try
                {
                    _translationService.SetLanguage(li.Code);
                    if (_settingsService != null) { _settingsService.Settings.Language = li.Code; _settingsService.Save(); }
                    ApplyTranslations();
                    // Rebuild categories with localized display
                    if (_categoriesService != null)
                    {
                        string? selectedCanonical = (_categorySelector.SelectedItem as CatItem)?.Canonical;
                        var cats = _categoriesService.GetAllCategories();
                        _categorySelector.Items.Clear();
                        foreach (var c in cats)
                        {
                            string disp = _translationService?.Category(c.Name) ?? c.Name;
                            _categorySelector.Items.Add(new CatItem(c.Name, disp));
                        }
                        if (!string.IsNullOrEmpty(selectedCanonical))
                        {
                            for (int i = 0; i < _categorySelector.Items.Count; i++)
                            {
                                if (_categorySelector.Items[i] is CatItem ci && ci.Canonical != null && ci.Canonical.Equals(selectedCanonical, StringComparison.OrdinalIgnoreCase))
                                { _categorySelector.SelectedIndex = i; break; }
                            }
                        }
                        else if (_categorySelector.Items.Count > 0) { _categorySelector.SelectedIndex = 0; }
                    }
                    RefreshSymptomList();
                    // Repaint results list to reflect translated texts
                    if (_lastResults != null && _lastResults.Count > 0)
                    {
                        RebuildResultsListItems();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Localization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyTranslations()
        {
            var t = _translationService;
            string TitleText = "Symptom Checker (Educational)";
            string DisclaimerText = "⚠️ Educational only. Not medical advice.";
            if (t != null)
            {
                Text = t.T("Title");
                _lblFilter.Text = t.T("Filter");
                _selectVisibleButton.Text = t.T("SelectVisible");
                _clearVisibleButton.Text = t.T("ClearVisible");
                _lblCategory.Text = t.T("Category");
                _selectCategoryButton.Text = t.T("SelectCategory");
                _clearCategoryButton.Text = t.T("ClearCategory");
                _showOnlyCategory.Text = t.T("ShowOnlyCategory");
                _lblModel.Text = t.T("Model");
                _lblThresh.Text = t.T("Threshold");
                _lblMinMatch.Text = t.T("MinMatch");
                _lblTopK.Text = t.T("TopK");
                _checkButton.Text = t.T("Check");
                _exitButton.Text = t.T("Exit");
                _syncButton.Text = t.T("Sync");
                _missingTransButton.Text = t.T("MissingTranslationsTitle");
                _lblLanguage.Text = t.T("Language");
                _disclaimer.Text = "⚠️ " + t.T("Disclaimer");
                _darkModeToggle.Text = t.T("DarkMode");
                _saveSessionButton.Text = t.T("Save") ?? _saveSessionButton.Text;
                _loadSessionButton.Text = t.T("Load") ?? _loadSessionButton.Text;
                // Vitals labels
                _lblVitals.Text = t.T("Vitals");
                _lblTemp.Text = t.T("TempC");
                _lblHR.Text = t.T("HeartRate");
                _lblRR.Text = t.T("RespRate");
                _lblBP.Text = t.T("BP");
                _lblSpO2.Text = t.T("SpO2");
                _lblWeight.Text = t.T("WeightKg");
                // Decision rules labels
                _lblRules.Text = t.T("DecisionRules") ?? _lblRules.Text;
                _grpCentor.Text = t.T("CentorMcIsaac") ?? _grpCentor.Text;
                _lblAge.Text = t.T("AgeYears") ?? _lblAge.Text;
                _centorFever.Text = t.T("Centor_Fever") ?? _centorFever.Text;
                _centorTonsils.Text = t.T("Centor_Tonsils") ?? _centorTonsils.Text;
                _centorNodes.Text = t.T("Centor_Nodes") ?? _centorNodes.Text;
                _centorNoCough.Text = t.T("Centor_NoCough") ?? _centorNoCough.Text;
                UpdateDecisionRules();

                // PERC labels
                _grpPerc.Text = t.T("PERC") ?? _grpPerc.Text;
                _percHemoptysis.Text = t.T("PERC_Hemoptysis") ?? _percHemoptysis.Text;
                _percEstrogen.Text = t.T("PERC_EstrogenUse") ?? _percEstrogen.Text;
                _percPriorDvtPe.Text = t.T("PERC_PriorDvtPe") ?? _percPriorDvtPe.Text;
                _percUnilateralLeg.Text = t.T("PERC_UnilateralLeg") ?? _percUnilateralLeg.Text;
                _percRecentSurgery.Text = t.T("PERC_RecentSurgery") ?? _percRecentSurgery.Text;
                UpdatePercRule();

                // Localize tooltip hint on results list
                _toolTip.SetToolTip(_resultsList, t.T("DoubleClickHint"));
                // Localize context menu entries if present
                if (_resultsContext.Items.Count >= 2)
                {
                    _resultsContext.Items[0].Text = t.T("CopyDetails");
                    _resultsContext.Items[1].Text = t.T("PrintDetails");
                }

                // RTL for Arabic
                bool rtl = string.Equals(t.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                ApplyRtl(this, rtl);
                // Align banner and menus for RTL
                _triageBanner.TextAlign = rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
                try { _triageBanner.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _resultsContext.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                // Ensure primary list controls mirror for RTL languages
                try { _resultsList.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _symptomList.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                // Rules row RTL
                try { _grpCentor.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _centorFever.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _centorTonsils.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _centorNodes.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _centorNoCough.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _grpPerc.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _percHemoptysis.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _percEstrogen.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _percPriorDvtPe.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _percUnilateralLeg.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { _percRecentSurgery.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                // ToolTip may not expose RightToLeft in this target; skip explicit RTL on tooltip

                // Rebuild triage banner for current language
                UpdateTriageBanner();
            }
            else
            {
                Text = TitleText;
                _disclaimer.Text = DisclaimerText;
            }
        }

        // Compute PERC rule result based on vitals, age, and history flags
        private void UpdatePercRule()
        {
            var t = _translationService;
            // Criteria
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

        // Compute Centor and McIsaac scores based on current selections and vitals
        private void UpdateDecisionRules()
        {
            // Guard: group might not be initialized during early constructor runs
            if (_grpCentor == null) return;
            var t = _translationService;
            // Determine Centor components from current symptoms and vitals
            bool hasFever = false;
            try
            {
                if (_settingsService?.Settings.TempC.HasValue == true)
                    hasFever = _settingsService!.Settings.TempC!.Value >= 38.0;
            }
            catch { }
            // Also infer from selected symptom 'Fever'
            hasFever = hasFever || _checkedSymptoms.Contains("Fever");

            bool tonsils = _checkedSymptoms.Contains("Sore Throat") || _checkedSymptoms.Contains("Tonsillar Exudates") || _checkedSymptoms.Contains("Tonsillar Swelling");
            // If we have a symptom like 'Tonsillar exudates/swelling' in translations only, we can't detect canonical; keep basic sore throat proxy
            bool nodes = _checkedSymptoms.Contains("Swollen Lymph Nodes");
            bool noCough = !_checkedSymptoms.Contains("Cough");

            // Update disabled checkboxes to reflect inferred state
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

            // Provide brief advice based on McIsaac score (educational)
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
            var keys = SymptomCheckerApp.Services.TriageService.Evaluate(selected);
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
                // For RTL, place bullet at the end for more natural reading
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

        private void ApplyRtl(Control root, bool rtl)
        {
            // Apply RTL at the form level only; most controls inherit safely from the form
            if (root is Form f)
            {
                try { f.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { f.RightToLeftLayout = rtl; } catch { }
            }
        }

        private void ApplyTheme()
        {
            bool dark = _darkModeToggle.Checked;
            Color back = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Window;
            Color fore = dark ? Color.Gainsboro : SystemColors.WindowText;
            Color panel = dark ? Color.FromArgb(24, 24, 24) : SystemColors.Control;

            this.BackColor = panel;
            foreach (Control c in this.Controls)
            {
                ApplyThemeToControl(c, back, fore, panel, dark);
            }
            _resultsList.Invalidate();
        }

        private void ApplyThemeToControl(Control c, Color back, Color fore, Color panel, bool dark)
        {
            switch (c)
            {
                case SplitContainer sc:
                    sc.BackColor = panel;
                    ApplyThemeToControl(sc.Panel1, back, fore, panel, dark);
                    ApplyThemeToControl(sc.Panel2, back, fore, panel, dark);
                    break;
                case TableLayoutPanel tl:
                    tl.BackColor = panel;
                    foreach (Control child in tl.Controls) ApplyThemeToControl(child, back, fore, panel, dark);
                    break;
                case FlowLayoutPanel fl:
                    fl.BackColor = panel;
                    foreach (Control child in fl.Controls) ApplyThemeToControl(child, back, fore, panel, dark);
                    break;
                case CheckedListBox clb:
                    clb.BackColor = back; clb.ForeColor = fore;
                    break;
                case ListBox lb:
                    lb.BackColor = back; lb.ForeColor = fore;
                    break;
                case TextBox tb:
                    tb.BackColor = back; tb.ForeColor = fore;
                    break;
                case ComboBox cb:
                    cb.BackColor = back; cb.ForeColor = fore;
                    break;
                case Label lbl:
                    lbl.BackColor = panel; lbl.ForeColor = fore;
                    break;
                case GroupBox gb:
                    gb.BackColor = panel; gb.ForeColor = fore;
                    foreach (Control child in gb.Controls) ApplyThemeToControl(child, back, fore, panel, dark);
                    break;
                case Button btn:
                    btn.BackColor = dark ? Color.FromArgb(60, 60, 60) : SystemColors.Control;
                    btn.ForeColor = fore;
                    break;
                case CheckBox chk:
                    chk.BackColor = panel; chk.ForeColor = fore;
                    break;
                case NumericUpDown nud:
                    nud.BackColor = back; nud.ForeColor = fore;
                    break;
                case Control generic:
                    generic.BackColor = panel; generic.ForeColor = fore;
                    break;
            }
            // Triage banner specific colors
            if (c == _triageBanner)
            {
                _triageBanner.BackColor = dark ? Color.FromArgb(64, 48, 0) : Color.FromArgb(255, 245, 230);
                _triageBanner.ForeColor = dark ? Color.Khaki : Color.FromArgb(120, 60, 0);
            }
        }

        private void CheckButton_Click(object? sender, EventArgs e)
        {
            if (_service == null) return;

            var selectedSymptoms = _checkedSymptoms.ToList();

            // Compute score threshold depending on model type: interpret threshold as percentage [0..100] for score
            var model = (DetectionModel)_modelSelector.SelectedItem!;
            double thr = (double)_threshold.Value / 100.0;
            int minMatch = (int)_minMatch.Value;
            int topK = (int)_topK.Value;
            int? topKOpt = topK > 0 ? topK : null;

            var matches = _service.GetMatches(selectedSymptoms, model, threshold: thr, topK: topKOpt, minMatchCount: minMatch);
            _lastResults = matches;
            RebuildResultsListItems();
            UpdateTriageBanner();
        }

        private void RebuildResultsListItems()
        {
            _resultsList.Items.Clear();
            _resultIndexMap.Clear();
            var t = _translationService;
            if (_lastResults == null || _lastResults.Count == 0)
            {
                _resultsList.Items.Add(t?.T("NoMatches") ?? "No matching conditions found based on the current selection.");
                _resultIndexMap.Add(-1);
                return;
            }

            // If categories service is available, group results by primary category (max overlap)
            var categories = _categoriesService?.GetAllCategories()?.ToList() ?? new List<SymptomCheckerApp.Models.SymptomCategory>();
            if (categories.Count == 0)
            {
                // Fallback: flat list
                for (int i = 0; i < _lastResults.Count; i++)
                {
                    var m = _lastResults[i];
                    string name = t?.Condition(m.Name) ?? m.Name;
                    string scoreLabel = t?.T("Score") ?? "Score:";
                    string matchesLabel = t?.T("Matches") ?? "matches:";
                    _resultsList.Items.Add($"{name} — {scoreLabel} {m.Score:F2} ({matchesLabel} {m.MatchCount})");
                    _resultIndexMap.Add(i);
                }
                _resultsList.Invalidate();
                return;
            }

            // Build category symptom sets once
            var catSets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in categories)
            {
                catSets[c.Name] = _categoriesService!.BuildCategorySet(c, _allSymptoms);
            }

            // Group results
            var grouped = new Dictionary<string, List<(int resultIndex, string line, double score)>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _lastResults.Count; i++)
            {
                var m = _lastResults[i];
                string displayName = t?.Condition(m.Name) ?? m.Name;
                string scoreLabel = t?.T("Score") ?? "Score:";
                string matchesLabel = t?.T("Matches") ?? "matches:";
                string line = $"{displayName} — {scoreLabel} {m.Score:F2} ({matchesLabel} {m.MatchCount})";

                // Determine best matching category
                string bestCat = "Other";
                if (_service != null && _service.TryGetCondition(m.Name, out var c) && c != null)
                {
                    int best = -1;
                    foreach (var kvp in catSets)
                    {
                        int overlap = 0;
                        foreach (var s in c.Symptoms)
                        {
                            if (kvp.Value.Contains(s)) overlap++;
                        }
                        if (overlap > best)
                        {
                            best = overlap; bestCat = kvp.Key;
                        }
                    }
                }
                var dispCat = t?.Category(bestCat) ?? bestCat;
                if (!grouped.TryGetValue(dispCat, out var list))
                {
                    list = new List<(int, string, double)>();
                    grouped[dispCat] = list;
                }
                list.Add((i, line, m.Score));
            }

            // Stable group order by localized category name
            foreach (var g in grouped.OrderBy(k => k.Key, StringComparer.CurrentCulture))
            {
                _resultsList.Items.Add(new GroupHeader(g.Key, g.Key));
                _resultIndexMap.Add(-1);
                foreach (var item in g.Value)
                {
                    _resultsList.Items.Add(item.line);
                    _resultIndexMap.Add(item.resultIndex);
                }
            }
            _resultsList.Invalidate();
        }

        private void ResultsList_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= _resultsList.Items.Count)
            {
                return;
            }

            string text = _resultsList.Items[e.Index]?.ToString() ?? string.Empty;

            // Render group headers differently
            if (_resultsList.Items[e.Index] is GroupHeader)
            {
                var boldFont = new Font(e.Font ?? SystemFonts.DefaultFont, FontStyle.Bold);
                bool rtlH = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                var flagsH = TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.GlyphOverhangPadding | TextFormatFlags.WordBreak;
                if (rtlH) flagsH |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                using (var back = new SolidBrush(Color.FromArgb(245, 245, 245)))
                {
                    e.Graphics.FillRectangle(back, e.Bounds);
                }
                var rectH = e.Bounds; rectH.Inflate(-6, -2);
                TextRenderer.DrawText(e.Graphics, text, boldFont, rectH, Color.DimGray, flagsH);
                e.DrawFocusRectangle();
                return;
            }

            bool isTop = false;
            // Only highlight if we have actual results and the item corresponds to a scored match
            if (_lastResults != null && _lastResults.Count > 0)
            {
                int resultIdx = (e.Index >= 0 && e.Index < _resultIndexMap.Count) ? _resultIndexMap[e.Index] : -1;
                if (resultIdx >= 0 && resultIdx < _lastResults.Count)
                {
                    double max = _lastResults[0].Score;
                    double sc = _lastResults[resultIdx].Score;
                    isTop = Math.Abs(sc - max) < 1e-9 && max > 0;
                }
            }

            Color backColor = isTop ? Color.FromArgb(230, 255, 230) : e.BackColor; // light green for top
            Color foreColor = isTop ? Color.DarkGreen : e.ForeColor;

            using (var backBrush = new SolidBrush(backColor))
            using (var foreBrush = new SolidBrush(foreColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
                var font = e.Font ?? SystemFonts.DefaultFont;
                bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                var flags = TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.GlyphOverhangPadding | TextFormatFlags.WordBreak;
                if (rtl) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                var rect = e.Bounds;
                rect.Inflate(-4, -2);
                TextRenderer.DrawText(e.Graphics, text, font, rect, foreColor, flags);
            }

            e.DrawFocusRectangle();
        }

        private void ResultsList_MeasureItem(object? sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _resultsList.Items.Count)
            {
                e.ItemHeight = 22;
                return;
            }
            string text = _resultsList.Items[e.Index]?.ToString() ?? string.Empty;
            if (_resultsList.Items[e.Index] is GroupHeader)
            {
                var boldFont = new Font(_resultsList.Font ?? SystemFonts.DefaultFont, FontStyle.Bold);
                bool rtlH = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                var flagsH = TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.GlyphOverhangPadding | TextFormatFlags.WordBreak;
                if (rtlH) flagsH |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                var widthH = Math.Max(10, _resultsList.ClientSize.Width - 12);
                var sizeH = TextRenderer.MeasureText(text, boldFont, new Size(widthH, int.MaxValue), flagsH);
                e.ItemHeight = Math.Max(22, sizeH.Height + 6);
                e.ItemWidth = widthH;
                return;
            }
            var font = _resultsList.Font ?? SystemFonts.DefaultFont;
            bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.GlyphOverhangPadding | TextFormatFlags.WordBreak;
            if (rtl) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
            var width = Math.Max(10, _resultsList.ClientSize.Width - 8);
            var size = TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue), flags);
            int min = 22;
            e.ItemHeight = Math.Max(min, size.Height + 4);
            e.ItemWidth = width;
        }

        private void ResultsList_DoubleClick(object? sender, EventArgs e)
        {
            if (_service == null) return;
            int idx = _resultsList.SelectedIndex;
            if (idx < 0) return;
            int resultIdx = (idx >= 0 && idx < _resultIndexMap.Count) ? _resultIndexMap[idx] : -1;
            if (resultIdx == -1) return; // header or unmapped
            if (resultIdx < 0 || resultIdx >= _lastResults.Count) return;

            var match = _lastResults[resultIdx];
            if (!_service.TryGetCondition(match.Name, out var condition) || condition == null)
            {
                MessageBox.Show(this, $"No details found for '{match.Name}'.", "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var t = _translationService;
            var sb = new System.Text.StringBuilder();
            BuildDetailsText(sb, t, match, condition);

            ShowDetailsDialog(t?.T("DetailsTitle") ?? "Condition Details", sb.ToString());
        }

        private void BuildDetailsText(System.Text.StringBuilder sb, TranslationService? t, ConditionMatch match, SymptomCheckerApp.Models.Condition condition)
        {
            string name = t?.Condition(match.Name) ?? match.Name;
            string scoreLabel = t?.T("Score") ?? "Score:";
            string matchedLabel = t?.T("MatchedSymptoms") ?? "Matched symptoms:";
            string symptomsLabel = t?.T("SymptomsLabel") ?? "Symptoms:";
            sb.AppendLine(name);
            sb.AppendLine($"{scoreLabel} {match.Score:F3}");
            sb.AppendLine($"{matchedLabel} {match.MatchCount}");
            sb.AppendLine();
            sb.AppendLine(symptomsLabel);
            foreach (var s in condition.Symptoms)
            {
                sb.AppendLine($" • {(t?.Symptom(s) ?? s)}");
            }

            // Explainability (simple)
            var model = (DetectionModel)_modelSelector.SelectedItem!;
            sb.AppendLine();
            sb.AppendLine(t?.T("ExplainabilityHeader") ?? "How this score was computed:");
            if (model == DetectionModel.Jaccard || model == DetectionModel.Cosine)
            {
                // Overlap based models
                sb.AppendLine($" • {(t?.T("Explain_MatchedOverlap") ?? "Matched overlap")}: {match.MatchCount}");
                sb.AppendLine($" • {(t?.T("Explain_Similarity") ?? "Similarity")}: {match.Score:F3}");
            }
            else if (model == DetectionModel.NaiveBayes)
            {
                sb.AppendLine($" • {(t?.T("Explain_Prob") ?? "Estimated probability")}: {match.Score:F3}");
            }

            // Optional treatment info (educational only). Prefer localized fields when available.
            List<string>? locTreat = null;
            List<string>? locMeds = null;
            string? locAdvice = null;
            var lang = _translationService?.CurrentLanguage?.ToLowerInvariant();
            if (lang == "fr")
            {
                locTreat = condition.Treatments_Fr ?? condition.Treatments;
                locMeds = condition.Medications_Fr ?? condition.Medications;
                locAdvice = condition.CareAdvice_Fr ?? condition.CareAdvice;
            }
            else if (lang == "ar")
            {
                locTreat = condition.Treatments_Ar ?? condition.Treatments;
                locMeds = condition.Medications_Ar ?? condition.Medications;
                locAdvice = condition.CareAdvice_Ar ?? condition.CareAdvice;
            }
            else
            {
                locTreat = condition.Treatments;
                locMeds = condition.Medications;
                locAdvice = condition.CareAdvice;
            }

            if (locTreat != null && locTreat.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(t?.TDetails("Treatments") ?? "Possible treatments (educational):");
                foreach (var tr in locTreat)
                {
                    sb.AppendLine($" • {tr}");
                }
            }
            if (locMeds != null && locMeds.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(t?.TDetails("Medications") ?? "Over‑the‑counter examples (educational):");
                foreach (var med in locMeds)
                {
                    sb.AppendLine($" • {med}");
                }
            }
            if (!string.IsNullOrWhiteSpace(locAdvice))
            {
                sb.AppendLine();
                sb.AppendLine(t?.TDetails("CareAdvice") ?? "Self‑care advice (educational):");
                sb.AppendLine($" • {locAdvice}");
            }

        }

        private void CopySelectedDetailsToClipboard()
        {
            if (_service == null) return;
            int idx = _resultsList.SelectedIndex;
            if (idx < 0 || idx >= _lastResults.Count) return;
            var match = _lastResults[idx];
            if (!_service.TryGetCondition(match.Name, out var condition) || condition == null) return;
            var t = _translationService;
            var sb = new System.Text.StringBuilder();
            BuildDetailsText(sb, t, match, condition);
            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        private void ShowDetailsDialog(string title, string text)
        {
            bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            using var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Width = 700,
                Height = 550
            };
            ApplyRtl(dlg, rtl);
            if (rtl)
            {
                text = TransformBulletsForRtl(text);
            }
            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                Text = text
            };
            if (rtl)
            {
                try { tb.RightToLeft = RightToLeft.Yes; } catch { }
                try { tb.TextAlign = HorizontalAlignment.Right; } catch { }
            }
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var btnClose = new Button { Text = _translationService?.T("Close") ?? "Close", AutoSize = true };
            btnClose.Click += (s, e) => dlg.Close();
            var btnCopy = new Button { Text = _translationService?.T("Copy") ?? "Copy", AutoSize = true };
            btnCopy.Click += (s, e) => { try { Clipboard.SetText(text); } catch { } };
            btnPanel.Controls.Add(btnClose);
            btnPanel.Controls.Add(btnCopy);
            dlg.Controls.Add(tb);
            dlg.Controls.Add(btnPanel);
            dlg.ShowDialog(this);
        }

        private string TransformBulletsForRtl(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var lines = input.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                var trimmed = l.TrimStart();
                if (trimmed.StartsWith("• ") || trimmed.StartsWith("•\t") || trimmed.StartsWith("•"))
                {
                    // Remove leading bullet and spaces, then append bullet at end
                    int idx = l.IndexOf('•');
                    if (idx >= 0)
                    {
                        var after = l.Substring(idx + 1).TrimStart();
                        lines[i] = after + "  •";
                    }
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        private void ShowMissingTranslationsDialog()
        {
            if (_translationService == null)
            {
                MessageBox.Show(this, "Translation service not loaded.", "Translations", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var missing = _translationService.MissingKeys?.OrderBy(x => x).ToList() ?? new List<string>();
            if (missing.Count == 0)
            {
                MessageBox.Show(this, _translationService.T("NoMissingTranslations") ?? "No missing translations detected.", _translationService.T("MissingTranslationsTitle") ?? "Missing Translations", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool rtl = string.Equals(_translationService.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            using var dlg = new Form
            {
                Text = _translationService.T("MissingTranslationsTitle") ?? "Missing Translations",
                StartPosition = FormStartPosition.CenterParent,
                Width = 700,
                Height = 500
            };
            ApplyRtl(dlg, rtl);
            var list = new ListBox { Dock = DockStyle.Fill }; if (rtl) try { list.RightToLeft = RightToLeft.Yes; } catch { }
            list.Items.AddRange(missing.Cast<object>().ToArray());
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var btnClose = new Button { Text = _translationService.T("Close") ?? "Close", AutoSize = true };
            btnClose.Click += (s, e) => dlg.Close();
            var btnCopy = new Button { Text = _translationService.T("Copy") ?? "Copy", AutoSize = true };
            btnCopy.Click += (s, e) => { try { Clipboard.SetText(string.Join(Environment.NewLine, missing)); } catch { } };
            var btnExport = new Button { Text = _translationService.T("ExportReport") ?? "Export", AutoSize = true };
            btnExport.Click += (s, e) =>
            {
                try
                {
                    var sfd = new SaveFileDialog { Filter = "Text (*.txt)|*.txt", FileName = "translation_report.txt" };
                    if (sfd.ShowDialog(dlg) == DialogResult.OK)
                    {
                        File.WriteAllLines(sfd.FileName, missing);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(dlg, ex.Message, _translationService.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            btnPanel.Controls.Add(btnClose);
            btnPanel.Controls.Add(btnExport);
            btnPanel.Controls.Add(btnCopy);
            dlg.Controls.Add(list);
            dlg.Controls.Add(btnPanel);
            dlg.ShowDialog(this);
        }

        private void PrintSelectedDetails()
        {
            if (_service == null) return;
            int idx = _resultsList.SelectedIndex;
            if (idx < 0 || idx >= _lastResults.Count) return;
            var match = _lastResults[idx];
            if (!_service.TryGetCondition(match.Name, out var condition) || condition == null) return;
            var t = _translationService;
            var sb = new System.Text.StringBuilder();
            BuildDetailsText(sb, t, match, condition);
            string text = sb.ToString();
            using var pd = new System.Drawing.Printing.PrintDocument();
            int charFrom = 0;
            pd.PrintPage += (s, e) =>
            {
                var font = new Font(FontFamily.GenericSansSerif, 10);
                var g = e.Graphics;
                if (g == null) { e.HasMorePages = false; return; }
                g.MeasureString(text.Substring(charFrom), font, e.MarginBounds.Size, StringFormat.GenericTypographic, out int chars, out int lines);
                g.DrawString(text.Substring(charFrom, chars), font, Brushes.Black, e.MarginBounds, StringFormat.GenericTypographic);
                charFrom += chars;
                e.HasMorePages = charFrom < text.Length;
            };
            try { using var dlg = new PrintPreviewDialog { Document = pd, Width = 800, Height = 600 }; dlg.ShowDialog(this); }
            catch { try { pd.Print(); } catch { } }
        }

        // Preserve checked state independent of filter
        private void RefreshSymptomList()
        {
            string q = _filterBox.Text?.Trim() ?? string.Empty;
            IEnumerable<string> items = _allSymptoms;
            if (_showOnlyCategory.Checked && _categoriesService != null && _categorySelector.SelectedItem != null)
            {
                string catName = (_categorySelector.SelectedItem as CatItem)?.Canonical ?? string.Empty;
                var cat = _categoriesService.GetAllCategories().FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
                if (cat != null)
                {
                    var set = _categoriesService.BuildCategorySet(cat, _allSymptoms);
                    items = items.Where(s => set.Contains(s));
                }
            }
                // Apply filter text with synonym support and translations
                if (!string.IsNullOrEmpty(q))
                {
                    HashSet<string> union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (_synonymService != null)
                    {
                        foreach (var s in _synonymService.MatchSymptomsByQuery(q, items)) union.Add(s);
                    }
                    else
                    {
                        foreach (var s in items.Where(s => s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)) union.Add(s);
                    }
                    // Add translation-based matches
                    if (_translationService != null)
                    {
                        foreach (var s in items)
                        {
                            var disp = _translationService.Symptom(s);
                            if (!string.IsNullOrEmpty(disp) && disp.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                union.Add(s);
                            }
                        }
                    }
                    items = union;
                }

            _symptomList.BeginUpdate();
            try
            {
                _symptomList.Items.Clear();
                var t = _translationService;
                foreach (var s in items)
                {
                    bool isChecked = _checkedSymptoms.Contains(s);
                    string display = t?.Symptom(s) ?? s;
                    _symptomList.Items.Add(new ListItem(s, display), isChecked);
                }
            }
            finally
            {
                _symptomList.EndUpdate();
            }
        }

        private void SymptomList_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _symptomList.Items.Count) return;
            var item = _symptomList.Items[e.Index] as ListItem;
            if (item == null) return;
            var name = item.Canonical;

            // ItemCheck fires before the state is updated; use NewValue
            if (e.NewValue == CheckState.Checked)
            {
                _checkedSymptoms.Add(name);
            }
            else
            {
                _checkedSymptoms.Remove(name);
            }
            UpdateCheckButtonEnabled();
        }

        private void SelectVisibleSymptoms()
        {
            for (int i = 0; i < _symptomList.Items.Count; i++)
            {
                var li = _symptomList.Items[i] as ListItem; if (li == null) continue; var name = li.Canonical;
                _symptomList.SetItemChecked(i, true);
                _checkedSymptoms.Add(name);
            }
            UpdateCheckButtonEnabled();
        }

        private void ClearVisibleSymptoms()
        {
            for (int i = 0; i < _symptomList.Items.Count; i++)
            {
                var li = _symptomList.Items[i] as ListItem; if (li == null) continue; var name = li.Canonical;
                _symptomList.SetItemChecked(i, false);
                _checkedSymptoms.Remove(name);
            }
            UpdateCheckButtonEnabled();
        }

        private void SelectByCategory()
        {
            if (_categoriesService == null) return;
            if (_categorySelector.SelectedItem == null) return;
            string catName = (_categorySelector.SelectedItem as CatItem)?.Canonical ?? string.Empty;
            var cat = _categoriesService.GetAllCategories().FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
            if (cat == null) return;
            var set = _categoriesService.BuildCategorySet(cat, _allSymptoms);
            // Check items in current view that belong to the category
            for (int i = 0; i < _symptomList.Items.Count; i++)
            {
                var li = _symptomList.Items[i] as ListItem; if (li == null) continue; var name = li.Canonical;
                if (set.Contains(name))
                {
                    _symptomList.SetItemChecked(i, true);
                    _checkedSymptoms.Add(name);
                }
            }
            UpdateCheckButtonEnabled();
        }

        private void ClearByCategory()
        {
            if (_categoriesService == null) return;
            if (_categorySelector.SelectedItem == null) return;
            string catName = (_categorySelector.SelectedItem as CatItem)?.Canonical ?? string.Empty;
            var cat = _categoriesService.GetAllCategories().FirstOrDefault(c => string.Equals(c.Name, catName, StringComparison.OrdinalIgnoreCase));
            if (cat == null) return;
            var set = _categoriesService.BuildCategorySet(cat, _allSymptoms);
            for (int i = 0; i < _symptomList.Items.Count; i++)
            {
                var li = _symptomList.Items[i] as ListItem; if (li == null) continue; var name = li.Canonical;
                if (set.Contains(name))
                {
                    _symptomList.SetItemChecked(i, false);
                    _checkedSymptoms.Remove(name);
                }
            }
            UpdateCheckButtonEnabled();
        }

        private void UpdateCheckButtonEnabled()
        {
            _checkButton.Enabled = _checkedSymptoms.Count > 0;
        }

        private class SessionData
        {
            public List<string> SelectedSymptoms { get; set; } = new();
            public string? Model { get; set; }
            public int ThresholdPercent { get; set; }
            public int MinMatch { get; set; }
            public int TopK { get; set; }
            public string? Language { get; set; }
        }

        private void SaveSession()
        {
            try
            {
                var sfd = new SaveFileDialog { Filter = "Session JSON (*.json)|*.json", FileName = "session.json" };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                var data = new SessionData
                {
                    SelectedSymptoms = _checkedSymptoms.ToList(),
                    Model = _modelSelector.SelectedItem?.ToString(),
                    ThresholdPercent = (int)_threshold.Value,
                    MinMatch = (int)_minMatch.Value,
                    TopK = (int)_topK.Value,
                    Language = (_languageSelector.SelectedItem as LangItem)?.Code
                };
                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(sfd.FileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSession()
        {
            try
            {
                var ofd = new OpenFileDialog { Filter = "Session JSON (*.json)|*.json" };
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                var json = System.IO.File.ReadAllText(ofd.FileName);
                var data = System.Text.Json.JsonSerializer.Deserialize<SessionData>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data == null) return;
                _checkedSymptoms = new HashSet<string>(data.SelectedSymptoms ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                // Restore UI states
                if (!string.IsNullOrEmpty(data.Language))
                {
                    for (int i = 0; i < _languageSelector.Items.Count; i++)
                    {
                        if (_languageSelector.Items[i] is LangItem li &&
                            data.Language is string lang &&
                            string.Equals(li.Code, lang, StringComparison.OrdinalIgnoreCase))
                        { _languageSelector.SelectedIndex = i; break; }
                    }
                }
                if (!string.IsNullOrEmpty(data.Model))
                {
                    for (int i = 0; i < _modelSelector.Items.Count; i++)
                    {
                        if (_modelSelector.Items[i]?.ToString()?.Equals(data.Model, StringComparison.OrdinalIgnoreCase) == true)
                        { _modelSelector.SelectedIndex = i; break; }
                    }
                }
                _threshold.Value = Math.Max(_threshold.Minimum, Math.Min(_threshold.Maximum, data.ThresholdPercent));
                _minMatch.Value = Math.Max(_minMatch.Minimum, Math.Min(_minMatch.Maximum, data.MinMatch));
                _topK.Value = Math.Max(_topK.Minimum, Math.Min(_topK.Maximum, data.TopK));
                RefreshSymptomList();
                UpdateCheckButtonEnabled();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task SyncFromWikidataAsync()
        {
            if (_service == null) return;
            try
            {
                _syncButton.Enabled = false;
                var originalText = _syncButton.Text;
                _syncButton.Text = "Syncing...";

                var importer = new WikidataImporter();
                var fetched = await importer.FetchConditionsAsync(limit: 200);
                int changes = _service.MergeConditions(fetched);
                if (changes > 0)
                {
                    _service.SaveDatabase();
                    // Refresh UI data
                    _allSymptoms = _service.GetAllUniqueSymptoms().ToList();
                    RefreshSymptomList();
                }

                var t = _translationService;
                string merged = changes > 0
                    ? string.Format(t?.T("MergedAddedFmt") ?? "Merged/added {0} conditions.", changes)
                    : (t?.T("NoChangesDetected") ?? "No changes detected.");
                MessageBox.Show(this, merged, t?.T("SyncCompleteTitle") ?? "Sync complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _syncButton.Text = originalText;
                _syncButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _syncButton.Enabled = true;
                _syncButton.Text = _translationService?.T("Sync") ?? "Sync";
                MessageBox.Show(this, ($"{(_translationService?.T("SyncFailed") ?? "Sync failed")}: {ex.Message}"), _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

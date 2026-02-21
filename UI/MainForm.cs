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
    public partial class MainForm : Form
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

    // Debounce timer for filter text input
    private readonly System.Windows.Forms.Timer _filterDebounce = new System.Windows.Forms.Timer { Interval = 250 };

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
    private readonly Button _selectAllButton = new Button();
    private readonly Button _deselectAllButton = new Button();
    private readonly Button _saveSessionButton = new Button();
    private readonly Button _loadSessionButton = new Button();
    private readonly Button _resetSettingsButton = new Button();
    private readonly Button _saveSettingsProfileButton = new Button();
    private readonly Button _loadSettingsProfileButton = new Button();
    private readonly Button _syncButton = new Button();
    private readonly Button _missingTransButton = new Button();
    private readonly Button _helpButton = new Button();
    private readonly Button _openLogsButton = new Button();
    private readonly ComboBox _languageSelector = new ComboBox();
    private readonly ToolTip _toolTip = new ToolTip();
    private readonly ContextMenuStrip _resultsContext = new ContextMenuStrip();
    private readonly Label _lblLanguage = new Label();
    private readonly Label _lblPerf = new Label();
    // UI: collapse toggle
    private Button? _collapseBtn;
    // Model tuning (new)
    private readonly ComboBox _weightCatSelector = new ComboBox();
    private readonly NumericUpDown _weightValue = new NumericUpDown();
    private readonly Button _applyWeightButton = new Button();
    private readonly Button _clearWeightsButton = new Button();
    private readonly CheckBox _nbTempEnable = new CheckBox();
    private readonly NumericUpDown _nbTempValue = new NumericUpDown();
    private readonly CheckBox _darkModeToggle = new CheckBox();
    private readonly Label _lblFilter = new Label();
    private readonly Label _lblCategory = new Label();
    private readonly Label _lblModel = new Label();
    private readonly Label _lblThresh = new Label();
    private readonly Label _lblMinMatch = new Label();
    private readonly Label _lblTopK = new Label();
    private readonly Label _lblSettingsPath = new Label();
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
    // Cached category -> symptom sets for performance
    private readonly Dictionary<string, HashSet<string>> _categorySetsCache = new(StringComparer.OrdinalIgnoreCase);

        public MainForm()
        {
            Text = "Symptom Checker (Educational)";
            AutoScaleMode = AutoScaleMode.Dpi;
            // Responsive: size relative to screen, with sensible minimum
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Width = Math.Max(800, (int)(screen.Width * 0.7));
            Height = Math.Max(550, (int)(screen.Height * 0.75));
            MinimumSize = new Size(700, 480);
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
            // Set initial focus for keyboard/screen reader users and ensure right panel is wide enough on first show
            this.Shown += (s, e) =>
            {
                try
                {
                    var sc = FindControl<SplitContainer>(this, "_mainSplit");
                    if (sc != null && sc.Width > 0)
                    {
                        // Responsive: ensure ~30/70 split on first show
                        int target = (int)(sc.Width * 0.30);
                        if (target >= sc.Panel1MinSize && (sc.Width - target - sc.SplitterWidth) >= sc.Panel2MinSize)
                            sc.SplitterDistance = target;
                    }
                    // Set vertical split proportionally
                    var vs = FindControl<SplitContainer>(this, "_mainVerticalSplit");
                    if (vs != null && vs.Height > 0)
                    {
                        int targetV = Math.Max(vs.Panel1MinSize, (int)(vs.Height * 0.60));
                        if ((vs.Height - targetV - vs.SplitterWidth) >= vs.Panel2MinSize)
                            vs.SplitterDistance = targetV;
                    }
                }
                catch { }
                _filterBox.Focus();
            };
            // Re-proportion splits on resize
            this.Resize += (s, e) =>
            {
                try
                {
                    var sc = FindControl<SplitContainer>(this, "_mainSplit");
                    if (sc != null && sc.Width > 0)
                    {
                        int target = (int)(sc.Width * 0.30);
                        if (target >= sc.Panel1MinSize && (sc.Width - target) >= sc.Panel2MinSize)
                            sc.SplitterDistance = target;
                    }
                    var vs = FindControl<SplitContainer>(this, "_mainVerticalSplit");
                    if (vs != null && vs.Height > 0)
                    {
                        int target = (int)(vs.Height * 0.60);
                        if (target >= vs.Panel1MinSize && (vs.Height - target) >= vs.Panel2MinSize)
                            vs.SplitterDistance = target;
                    }
                }
                catch { }
            };
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
                Name = "_mainSplit"
            };
            // Responsive: set splitter as percentage of width
            try
            {
                int p1Min = ScaleX(180);
                int p2Min = ScaleX(280);
                int splW = ScaleX(5);
                // Ensure the container is wide enough before setting min sizes
                int requiredWidth = p1Min + p2Min + splW + 1;
                if (split.Width < requiredWidth)
                    split.Width = Math.Max(requiredWidth, this.ClientSize.Width);
                split.Panel1MinSize = p1Min;
                split.Panel2MinSize = p2Min;
                split.SplitterWidth = splW;
                int desired = Math.Max(p1Min, (int)(this.ClientSize.Width * 0.30));
                int maxDist = split.Width - p2Min - splW;
                split.SplitterDistance = Math.Max(p1Min, Math.Min(desired, maxDist));
            }
            catch { try { split.SplitterDistance = 300; } catch { } }
            // Collapse/expand button (overlay small)
            var collapseBtn = new Button
            {
                Text = "≪",
                Width = 28,
                Height = 24,
                FlatStyle = FlatStyle.Standard,
                TabStop = false
            };
            _collapseBtn = collapseBtn;
            bool collapsed = false;
            collapseBtn.Click += (s, e) =>
            {
                if (!collapsed)
                {
                    split.Panel1Collapsed = true;
                    collapseBtn.Text = "≫";
                    collapsed = true;
                    try { _settingsService!.Settings.LeftPanelCollapsed = true; _settingsService.Save(); } catch { }
                }
                else
                {
                    split.Panel1Collapsed = false;
                    // Reassign a reasonable default width
                    try
                    {
                        int t = Math.Max(split.Panel1MinSize, (int)(split.Width * 0.30));
                        int m = split.Width - split.Panel2MinSize - split.SplitterWidth;
                        split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(t, m));
                    } catch { }
                    collapseBtn.Text = "≪";
                    collapsed = false;
                    try { _settingsService!.Settings.LeftPanelCollapsed = false; _settingsService.Save(); } catch { }
                }
            };
            // We'll place this control in the top controls row to avoid overlaying content

            // Left side: filter controls + symptom list
            var leftPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Filter / actions bar (left). Previously AutoSize could grow vertically and hide the list on small screens.
            var filterBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                AutoScroll = false, // We'll wrap; container will cap height
                Padding = new Padding(3),
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };
            _lblFilter.Text = "Filter:";
            _lblFilter.AutoSize = true;
            _lblFilter.Padding = new Padding(0, 6, 0, 0);
            _filterBox.Width = ScaleX(180);
            _filterBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _filterBox.TabIndex = 0;
            _filterBox.AccessibleName = "Filter symptoms";
            _filterBox.AccessibleDescription = "Type text to filter the symptoms list";
            _filterBox.TextChanged += (s, e) =>
            {
                // Debounce: restart the timer on each keystroke
                _filterDebounce.Stop();
                _filterDebounce.Start();
            };
            _filterDebounce.Tick += (s, e) =>
            {
                _filterDebounce.Stop();
                RefreshSymptomList();
                if (_settingsService != null)
                {
                    _settingsService.Settings.FilterText = _filterBox.Text;
                    _settingsService.Save();
                }
            };

            _selectVisibleButton.Text = "Select Visible";
            _selectVisibleButton.AutoSize = true;
            _selectVisibleButton.TabIndex = 1;
            _selectVisibleButton.Click += (s, e) => SelectVisibleSymptoms();

            _clearVisibleButton.Text = "Clear Visible";
            _clearVisibleButton.AutoSize = true;
            _clearVisibleButton.TabIndex = 2;
            _clearVisibleButton.Click += (s, e) => ClearVisibleSymptoms();

            _selectAllButton.Text = "Select All";
            _selectAllButton.AutoSize = true;
            _selectAllButton.AccessibleName = "Select all symptoms";
            _selectAllButton.Click += (s, e) => SelectAllSymptoms();

            _deselectAllButton.Text = "Deselect All";
            _deselectAllButton.AutoSize = true;
            _deselectAllButton.AccessibleName = "Deselect all symptoms";
            _deselectAllButton.Click += (s, e) => DeselectAllSymptoms();

            _lblCategory.Text = "Category:";
            _lblCategory.AutoSize = true;
            _lblCategory.Padding = new Padding(10, 6, 0, 0);
            _categorySelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _categorySelector.Width = ScaleX(150);
            _categorySelector.AccessibleName = "Category filter";
            _categorySelector.TabIndex = 3;
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
            _selectCategoryButton.TabIndex = 4;
            _selectCategoryButton.Click += (s, e) => SelectByCategory();
            _clearCategoryButton.Text = "Clear Category";
            _clearCategoryButton.AutoSize = true;
            _clearCategoryButton.TabIndex = 5;
            _clearCategoryButton.Click += (s, e) => ClearByCategory();
            _showOnlyCategory.Text = "Show only category";
            _showOnlyCategory.AutoSize = true;
            _showOnlyCategory.AccessibleName = "Toggle show only selected category";
            _showOnlyCategory.TabIndex = 6;
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
            filterBar.Controls.Add(_selectAllButton);
            filterBar.Controls.Add(_deselectAllButton);
            _saveSessionButton.Text = "Save";
            _saveSessionButton.AutoSize = true;
            _saveSessionButton.Click += (s, e) => SaveSession();
            _loadSessionButton.Text = "Load";
            _loadSessionButton.AutoSize = true;
            _loadSessionButton.Click += (s, e) => LoadSession();
            _resetSettingsButton.Text = "Reset Settings"; _resetSettingsButton.AutoSize = true; _resetSettingsButton.Click += (s, e) => ResetSettings();
            _saveSettingsProfileButton.Text = "Save Settings Profile"; _saveSettingsProfileButton.AutoSize = true; _saveSettingsProfileButton.Click += (s, e) => SaveSettingsProfile();
            _loadSettingsProfileButton.Text = "Load Settings Profile"; _loadSettingsProfileButton.AutoSize = true; _loadSettingsProfileButton.Click += (s, e) => LoadSettingsProfile();
            filterBar.Controls.Add(_saveSessionButton);
            filterBar.Controls.Add(_loadSessionButton);
            filterBar.Controls.Add(_resetSettingsButton);
            filterBar.Controls.Add(_saveSettingsProfileButton);
            filterBar.Controls.Add(_loadSettingsProfileButton);
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
            _symptomList.TabIndex = 7;

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

            var topControls = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(3)
            };
            var vitalsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var rulesRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };

            _modelSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _modelSelector.Items.AddRange(new object[] { DetectionModel.Jaccard, DetectionModel.Cosine, DetectionModel.NaiveBayes });
            _modelSelector.AccessibleName = "Detection model";
            _modelSelector.TabIndex = 10;
            _modelSelector.SelectedIndexChanged += (s, e) =>
            {
                if (_settingsService != null && _modelSelector.SelectedItem != null)
                {
                    _settingsService.Settings.Model = _modelSelector.SelectedItem.ToString();
                    _settingsService.Save();
                }
                // Enable temperature calibration controls only for NaiveBayes
                bool nb = _modelSelector.SelectedItem is DetectionModel dm && dm == DetectionModel.NaiveBayes;
                _nbTempEnable.Enabled = nb;
                _nbTempValue.Enabled = nb && _nbTempEnable.Checked;
            };
            _modelSelector.SelectedIndex = 0;

            _threshold.Minimum = 0;
            _threshold.Maximum = 100;
            _threshold.DecimalPlaces = 0;
            _threshold.Value = 0;
            _threshold.Width = ScaleX(65);
            _threshold.AccessibleName = "Score threshold percent";
            _threshold.TabIndex = 11;
            _threshold.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.ThresholdPercent = (int)_threshold.Value; _settingsService.Save(); } };
            _lblModel.Text = "Model:";
            _lblModel.AutoSize = true; _lblModel.Padding = new Padding(0, 6, 0, 0);
            _lblThresh.Text = "Threshold (%):";
            _lblThresh.AutoSize = true; _lblThresh.Padding = new Padding(10, 6, 0, 0);

            _minMatch.Minimum = 0; // 0 means any match count
            _minMatch.Maximum = 10;
            _minMatch.Value = 1;
            _minMatch.Width = ScaleX(55);
            _minMatch.AccessibleName = "Minimum matching symptoms";
            _minMatch.TabIndex = 12;
            _minMatch.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.MinMatch = (int)_minMatch.Value; _settingsService.Save(); } };
            _lblMinMatch.Text = "Min match:"; _lblMinMatch.AutoSize = true; _lblMinMatch.Padding = new Padding(10, 6, 0, 0);

            _topK.Minimum = 0; // 0 = unlimited
            _topK.Maximum = 1000;
            _topK.Value = 0;
            _topK.Width = ScaleX(60);
            _topK.AccessibleName = "Top K results";
            _topK.TabIndex = 13;
            _topK.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.TopK = (int)_topK.Value; _settingsService.Save(); } };
            _lblTopK.Text = "Top-K:"; _lblTopK.AutoSize = true; _lblTopK.Padding = new Padding(10, 6, 0, 0);

            // Category weighting UI (model tuning)
            _weightCatSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _weightCatSelector.Width = ScaleX(110);
            _weightCatSelector.AccessibleName = "Category weight selector";
            _weightCatSelector.TabIndex = 13;
            _weightCatSelector.SelectedIndexChanged += (s, e) =>
            {
                try
                {
                    if (_weightCatSelector.SelectedItem is CatItem ci && _settingsService != null)
                    {
                        double existing = 1.0;
                        if (_settingsService.Settings.CategoryWeights != null && _settingsService.Settings.CategoryWeights.TryGetValue(ci.Canonical, out var w) && w > 0)
                            existing = w;
                        var pct = (decimal)(existing * 100.0);
                        if (pct < _weightValue.Minimum) pct = _weightValue.Minimum;
                        if (pct > _weightValue.Maximum) pct = _weightValue.Maximum;
                        _weightValue.Value = pct;
                    }
                }
                catch { }
            };
            _weightValue.Minimum = 10; // 0.1x
            _weightValue.Maximum = 500; // 5x
            _weightValue.Value = 100; // 1x
            _weightValue.Width = ScaleX(60);
            _weightValue.Increment = 10;
            _weightValue.AccessibleName = "Category weight percent";
            _weightValue.TabIndex = 14;
            _applyWeightButton.Text = "ApplyW"; _applyWeightButton.AutoSize = true; _applyWeightButton.AccessibleName = "Apply category weight"; _applyWeightButton.TabIndex = 15;
            _applyWeightButton.Click += (s, e) =>
            {
                if (_settingsService == null || _weightCatSelector.SelectedItem is not CatItem ci) return;
                double mult = (double)_weightValue.Value / 100.0;
                if (mult <= 0) mult = 0.01;
                _settingsService.Settings.CategoryWeights ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                _settingsService.Settings.CategoryWeights[ci.Canonical] = mult;
                _settingsService.Save();
            };
            _clearWeightsButton.Text = "ClearW"; _clearWeightsButton.AutoSize = true; _clearWeightsButton.AccessibleName = "Clear all category weights"; _clearWeightsButton.TabIndex = 16;
            _clearWeightsButton.Click += (s, e) =>
            {
                if (_settingsService == null) return;
                if (MessageBox.Show(this, "Clear all category weights?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _settingsService.Settings.CategoryWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    _settingsService.Save();
                    try { _weightValue.Value = 100; } catch { }
                }
            };

            // Naive Bayes temperature calibration
            _nbTempEnable.Text = "NB Temp"; _nbTempEnable.AutoSize = true; _nbTempEnable.AccessibleName = "Enable Naive Bayes temperature scaling"; _nbTempEnable.TabIndex = 17;
            _nbTempValue.Minimum = 10; // 0.10
            _nbTempValue.Maximum = 500; // 5.00
            _nbTempValue.DecimalPlaces = 2;
            _nbTempValue.Increment = 5; // 0.05
            _nbTempValue.Value = 100; // 1.00
            _nbTempValue.Width = ScaleX(60);
            _nbTempValue.AccessibleName = "Naive Bayes temperature";
            _nbTempValue.TabIndex = 18;
            _nbTempEnable.CheckedChanged += (s, e) =>
            {
                if (_settingsService == null) return;
                if (_nbTempEnable.Checked)
                {
                    double t = (double)_nbTempValue.Value / 100.0;
                    if (t < 0.01) t = 0.01;
                    _settingsService.Settings.NaiveBayesTemperature = t;
                }
                else
                {
                    _settingsService.Settings.NaiveBayesTemperature = null;
                }
                _settingsService.Save();
            };
            _nbTempValue.ValueChanged += (s, e) =>
            {
                if (_settingsService == null) return;
                if (_nbTempEnable.Checked)
                {
                    double t = (double)_nbTempValue.Value / 100.0;
                    if (t < 0.01) t = 0.01;
                    _settingsService.Settings.NaiveBayesTemperature = t;
                    _settingsService.Save();
                }
            };

            _lblLanguage.Text = "Language:"; _lblLanguage.AutoSize = true; _lblLanguage.Padding = new Padding(10, 6, 0, 0);
            _languageSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _languageSelector.Width = ScaleX(100);
            _languageSelector.AccessibleName = "Language selector";
            _languageSelector.TabIndex = 19;
            _languageSelector.SelectedIndexChanged += (s, e) => OnLanguageChanged();

            _darkModeToggle.Text = "Dark";
            _darkModeToggle.AutoSize = true;
            _darkModeToggle.AccessibleName = "Toggle dark mode";
            _darkModeToggle.TabIndex = 20;
            _darkModeToggle.CheckedChanged += (s, e) => { ApplyTheme(); if (_settingsService != null) { _settingsService.Settings.DarkMode = _darkModeToggle.Checked; _settingsService.Save(); } };

            _checkButton.Text = "Check";
            _checkButton.AutoSize = true;
            _checkButton.AccessibleName = "Check";
            _checkButton.AccessibleDescription = "Run the symptom checker";
            // Make the execute button visually distinct
            _checkButton.FlatStyle = FlatStyle.Flat;
            _checkButton.UseVisualStyleBackColor = false;
            _checkButton.Click += CheckButton_Click;
            _checkButton.TabIndex = 21;
            _checkButton.Enabled = false; // disabled until at least one symptom selected

            _exitButton.Text = "Exit";
            _exitButton.AutoSize = true;
            _exitButton.AccessibleName = "Exit application";
            _exitButton.TabIndex = 17;
            _exitButton.Click += (s, e) => this.Close();

            _syncButton.Text = "Sync";
            _syncButton.AutoSize = true;
            _syncButton.AccessibleName = "Sync from Wikidata";
            _syncButton.TabIndex = 18;
            _syncButton.Click += async (s, e) => await SyncFromWikidataAsync();

            // Missing translations report UI
            _missingTransButton.Text = "Missing Translations";
            _missingTransButton.AutoSize = true;
            _missingTransButton.AccessibleName = "Show missing translations report";
            _missingTransButton.TabIndex = 19;
            _missingTransButton.Click += (s, e) => ShowMissingTranslationsDialog();

            // Help/About
            _helpButton.Text = "Help";
            _helpButton.AutoSize = true;
            _helpButton.AccessibleName = "Help and about";
            _helpButton.TabIndex = 20;
            _helpButton.Click += (s, e) => ShowHelpDialog();
            _openLogsButton.Text = "Logs"; _openLogsButton.AutoSize = true; _openLogsButton.AccessibleName = "Open logs folder"; _openLogsButton.TabIndex = 21; _openLogsButton.Click += (s, e) => OpenLogsFolder();
            try { _toolTip.SetToolTip(collapseBtn, "Hide/show left panel"); } catch { }

            _resultsList.Dock = DockStyle.Fill;
            _resultsList.DrawMode = DrawMode.OwnerDrawVariable;
            _resultsList.ItemHeight = 22;
            _resultsList.AccessibleName = "Results";
            _resultsList.AccessibleDescription = "List of matching conditions";
            _resultsList.DrawItem += ResultsList_DrawItem;
            _resultsList.TabIndex = 30;
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
            _numTempC.DecimalPlaces = 1; _numTempC.Increment = 0.1M; _numTempC.Minimum = 30; _numTempC.Maximum = 45; _numTempC.Width = ScaleX(60); _numTempC.AccessibleName = "Temperature Celsius"; _numTempC.TabIndex = 40;
            _numTempC.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.TempC = (double)_numTempC.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblHR.Text = "HR (bpm):"; _lblHR.AutoSize = true; _lblHR.Padding = new Padding(10, 6, 0, 0);
            _numHR.Minimum = 20; _numHR.Maximum = 240; _numHR.Width = ScaleX(60); _numHR.AccessibleName = "Heart rate"; _numHR.TabIndex = 41;
            _numHR.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.HeartRate = (int)_numHR.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblRR.Text = "RR (/min):"; _lblRR.AutoSize = true; _lblRR.Padding = new Padding(10, 6, 0, 0);
            _numRR.Minimum = 4; _numRR.Maximum = 80; _numRR.Width = ScaleX(60); _numRR.AccessibleName = "Respiratory rate"; _numRR.TabIndex = 42;
            _numRR.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.RespRate = (int)_numRR.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblBP.Text = "BP (SBP/DBP):"; _lblBP.AutoSize = true; _lblBP.Padding = new Padding(10, 6, 0, 0);
            _numSBP.Minimum = 50; _numSBP.Maximum = 260; _numSBP.Width = ScaleX(60); _numSBP.AccessibleName = "Systolic blood pressure"; _numSBP.TabIndex = 43;
            _numSBP.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.SystolicBP = (int)_numSBP.Value; _settingsService.Save(); } UpdateDecisionRules(); };
            _numDBP.Minimum = 30; _numDBP.Maximum = 160; _numDBP.Width = ScaleX(60); _numDBP.AccessibleName = "Diastolic blood pressure"; _numDBP.TabIndex = 44;
            _numDBP.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.DiastolicBP = (int)_numDBP.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblSpO2.Text = "SpO₂ (%):"; _lblSpO2.AutoSize = true; _lblSpO2.Padding = new Padding(10, 6, 0, 0);
            _numSpO2.Minimum = 50; _numSpO2.Maximum = 100; _numSpO2.Width = ScaleX(60); _numSpO2.AccessibleName = "Oxygen saturation"; _numSpO2.TabIndex = 45;
            _numSpO2.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.SpO2 = (int)_numSpO2.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            _lblWeight.Text = "Weight (kg):"; _lblWeight.AutoSize = true; _lblWeight.Padding = new Padding(10, 6, 0, 0);
            _numWeightKg.DecimalPlaces = 1; _numWeightKg.Increment = 0.5M; _numWeightKg.Minimum = 2; _numWeightKg.Maximum = 350; _numWeightKg.Width = ScaleX(65); _numWeightKg.AccessibleName = "Weight kilograms"; _numWeightKg.TabIndex = 46;
            _numWeightKg.ValueChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.WeightKg = (double)_numWeightKg.Value; _settingsService.Save(); } UpdateDecisionRules(); };

            // We'll host filterBar inside a panel that enforces a maximum height with scrollbar if needed
            var filterHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            filterHost.Controls.Add(filterBar);
            // After layout we can cap height dynamically
            filterHost.Resize += (s, e) =>
            {
                int maxH = ScaleY(120); // max visible area for filter controls (DPI-scaled)
                if (filterBar.Height > maxH)
                {
                    filterHost.AutoScrollMinSize = new Size(filterBar.Width, filterBar.Height + 4);
                }
            };
            leftPanel.Controls.Add(filterHost, 0, 0);
            leftPanel.Controls.Add(_symptomList, 0, 1);
            try
            {
                leftPanel.RowStyles.Clear();
                leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            }
            catch { }
            split.Panel1.Controls.Add(leftPanel);
            // Add collapse control first for visibility
            topControls.Controls.Add(collapseBtn);
            topControls.Controls.Add(_lblModel);
            topControls.Controls.Add(_modelSelector);
            topControls.Controls.Add(_lblThresh);
            topControls.Controls.Add(_threshold);
            topControls.Controls.Add(_lblMinMatch);
            topControls.Controls.Add(_minMatch);
            topControls.Controls.Add(_lblTopK);
            topControls.Controls.Add(_topK);
            topControls.Controls.Add(_weightCatSelector);
            topControls.Controls.Add(_weightValue);
            topControls.Controls.Add(_applyWeightButton);
            topControls.Controls.Add(_clearWeightsButton);
            topControls.Controls.Add(_nbTempEnable);
            topControls.Controls.Add(_nbTempValue);
            topControls.Controls.Add(_lblLanguage);
            topControls.Controls.Add(_languageSelector);
            topControls.Controls.Add(_darkModeToggle);
            topControls.Controls.Add(_checkButton);
            topControls.Controls.Add(_exitButton);
            topControls.Controls.Add(_syncButton);
            topControls.Controls.Add(_missingTransButton);
            topControls.Controls.Add(_helpButton);
            topControls.Controls.Add(_openLogsButton);
            _lblPerf.AutoSize = true; _lblPerf.Padding = new Padding(10,6,0,0); _lblPerf.Text = ""; _lblPerf.AccessibleName = "Performance timing"; topControls.Controls.Add(_lblPerf);
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
            _numAge.Minimum = 0; _numAge.Maximum = 120; _numAge.Value = 25; _numAge.Width = ScaleX(60); _numAge.AccessibleName = "Age years"; _numAge.TabIndex = 50;
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
            _percHemoptysis.Text = "Hemoptysis"; _percHemoptysis.AutoSize = true; _percHemoptysis.AccessibleName = "PERC hemoptysis"; _percHemoptysis.TabIndex = 60;
            _percHemoptysis.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercHemoptysis = _percHemoptysis.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percEstrogen.Text = "Estrogen use"; _percEstrogen.AutoSize = true; _percEstrogen.AccessibleName = "PERC estrogen use"; _percEstrogen.TabIndex = 61;
            _percEstrogen.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercEstrogenUse = _percEstrogen.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percPriorDvtPe.Text = "Prior DVT/PE"; _percPriorDvtPe.AutoSize = true; _percPriorDvtPe.AccessibleName = "PERC prior DVT or PE"; _percPriorDvtPe.TabIndex = 62;
            _percPriorDvtPe.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercPriorDvtPe = _percPriorDvtPe.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percUnilateralLeg.Text = "Unilateral leg swelling"; _percUnilateralLeg.AutoSize = true; _percUnilateralLeg.AccessibleName = "PERC unilateral leg swelling"; _percUnilateralLeg.TabIndex = 63;
            _percUnilateralLeg.CheckedChanged += (s, e) => { if (_settingsService != null) { _settingsService.Settings.PercUnilateralLegSwelling = _percUnilateralLeg.Checked; _settingsService.Save(); } UpdatePercRule(); };
            _percRecentSurgery.Text = "Recent surgery/trauma"; _percRecentSurgery.AutoSize = true; _percRecentSurgery.AccessibleName = "PERC recent surgery or trauma"; _percRecentSurgery.TabIndex = 64;
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
            // Place results list in the percent row (index 3) so it expands; triage banner becomes auto row below
            rightPanel.Controls.Add(_resultsList, 0, 3);
            rightPanel.Controls.Add(_triageBanner, 0, 4);
            rightPanel.Controls.Add(_disclaimer, 0, 5);
            split.Panel2.Controls.Add(rightPanel);

            // AI / Ollama panel below the main split (bottom section)
            var mainLayout = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Name = "_mainVerticalSplit"
            };
            try
            {
                int p1MinV = ScaleY(200);
                int p2MinV = ScaleY(140);
                int splWV = ScaleY(5);
                int requiredHeight = p1MinV + p2MinV + splWV + 1;
                if (mainLayout.Height < requiredHeight)
                    mainLayout.Height = Math.Max(requiredHeight, this.ClientSize.Height);
                mainLayout.Panel1MinSize = p1MinV;
                mainLayout.Panel2MinSize = p2MinV;
                mainLayout.SplitterWidth = splWV;
                int desiredV = Math.Max(p1MinV, (int)(this.ClientSize.Height * 0.60));
                int maxDistV = mainLayout.Height - p2MinV - splWV;
                mainLayout.SplitterDistance = Math.Max(p1MinV, Math.Min(desiredV, maxDistV));
            }
            catch { try { mainLayout.SplitterDistance = 400; } catch { } }
            // Add the existing horizontal split into the top panel
            mainLayout.Panel1.Controls.Add(split);
            // Bottom panel: TabControl with AI Diagnosis + Image Analysis tabs
            var aiTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "_aiTabControl"
            };
            var tabAiDiag = new TabPage("🤖 AI Diagnosis") { Name = "_tabAiDiag" };
            var tabImageAnalysis = new TabPage("📷 Image Analysis") { Name = "_tabImageAnalysis" };
            var tabBloodAnalysis = new TabPage("🔬 Blood Microscope") { Name = "_tabBloodAnalysis" };
            InitializeOllamaPanel(tabAiDiag);
            InitializeImageAnalysisPanel(tabImageAnalysis);
            InitializeBloodAnalysisPanel(tabBloodAnalysis);
            aiTabControl.TabPages.Add(tabAiDiag);
            aiTabControl.TabPages.Add(tabImageAnalysis);
            aiTabControl.TabPages.Add(tabBloodAnalysis);
            mainLayout.Panel2.Controls.Add(aiTabControl);
            Controls.Add(mainLayout);

            // Settings path label placed at bottom-left of main form
            _lblSettingsPath.Dock = DockStyle.Bottom;
            _lblSettingsPath.AutoSize = true;
            _lblSettingsPath.Padding = new Padding(4);
            _lblSettingsPath.ForeColor = Color.DimGray;
            _lblSettingsPath.Font = new Font(Font.FontFamily, 7f);
            _lblSettingsPath.AccessibleName = "Settings file path";
            Controls.Add(_lblSettingsPath);

            // Context menu for results (Copy details, Print)
            var miCopy = new ToolStripMenuItem("Copy Details", null, (s, e) => CopySelectedDetailsToClipboard());
            var miPrint = new ToolStripMenuItem("Print Details", null, (s, e) => PrintSelectedDetails());
            var miExportCsv = new ToolStripMenuItem("Export Results (CSV)", null, (s, e) => ExportResultsCsv());
            var miExportMd = new ToolStripMenuItem("Export Results (Markdown)", null, (s, e) => ExportResultsMarkdown());
            var miExportHtml = new ToolStripMenuItem("Export Results (HTML)", null, (s, e) => ExportResultsHtml());
            var miExportSelectedOnly = new ToolStripMenuItem("Export Selected Only");
            miExportSelectedOnly.CheckOnClick = true;
            miExportSelectedOnly.ToolTipText = "Toggle exporting only currently selected condition row (if any).";
            miExportSelectedOnly.Click += (s, e) => { _exportSelectedOnly = !_exportSelectedOnly; miExportSelectedOnly.Checked = _exportSelectedOnly; };
            _resultsContext.Items.Add(miCopy);
            _resultsContext.Items.Add(miPrint);
            _resultsContext.Items.Add(new ToolStripSeparator());
            _resultsContext.Items.Add(miExportCsv);
            _resultsContext.Items.Add(miExportMd);
            _resultsContext.Items.Add(miExportHtml);
            _resultsContext.Items.Add(new ToolStripSeparator());
            _resultsContext.Items.Add(miExportSelectedOnly);
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            // Validate JSON schemas (non-blocking for user convenience)
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    string baseDir = AppContext.BaseDirectory;
                    var conditionsData = Path.Combine(baseDir, "data", "conditions.json");
                    var conditionsSchema = Path.Combine(baseDir, "schemas", "conditions.schema.json");
                    var categoriesData = Path.Combine(baseDir, "data", "categories.json");
                    var categoriesSchema = Path.Combine(baseDir, "schemas", "categories.schema.json");
                    var translationsData = Path.Combine(baseDir, "data", "translations.json");
                    var translationsSchema = Path.Combine(baseDir, "schemas", "translations.schema.json");
                    var synonymsData = Path.Combine(baseDir, "data", "synonyms.json");
                    var synonymsSchema = Path.Combine(baseDir, "schemas", "synonyms.schema.json");

                    var condErr = await SchemaValidator.ValidateAsync(conditionsData, conditionsSchema);
                    var catErr = await SchemaValidator.ValidateAsync(categoriesData, categoriesSchema);
                    var transErr = await SchemaValidator.ValidateAsync(translationsData, translationsSchema);
                    string? synErr = null;
                    if (File.Exists(synonymsData) && File.Exists(synonymsSchema))
                        synErr = await SchemaValidator.ValidateAsync(synonymsData, synonymsSchema);
                    var all = new List<string>();
                    if (!string.IsNullOrEmpty(condErr)) all.Add(condErr);
                    if (!string.IsNullOrEmpty(catErr)) all.Add(catErr);
                    if (!string.IsNullOrEmpty(transErr)) all.Add(transErr);
                    if (!string.IsNullOrEmpty(synErr)) all.Add(synErr);
                    if (all.Count > 0)
                    {
                        var msg = string.Join("\n\n", all);
                        this.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(this, msg, "Data Validation Warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, RightToLeftLayout ? MessageBoxOptions.RtlReading : 0);
                        }));
                    }
                }
                catch { }
            });
            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "data", "conditions.json");
                _service = new SymptomCheckerService(jsonPath);
                var symptoms = _service.GetAllUniqueSymptoms();
                _allSymptoms = symptoms.ToList();
                // Load settings
                var settingsPath = Path.Combine(AppContext.BaseDirectory, "data", "settings.json");
                _settingsService = new SettingsService(settingsPath);
                try { _lblSettingsPath.Text = $"Settings: {settingsPath}"; } catch { }
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
                    _categorySetsCache.Clear();
                    _weightCatSelector.Items.Clear();
                    foreach (var c in cats)
                    {
                        string disp = _translationService?.Category(c.Name) ?? c.Name;
                        _categorySelector.Items.Add(new CatItem(c.Name, disp));
                        _weightCatSelector.Items.Add(new CatItem(c.Name, disp));
                        try { _categorySetsCache[c.Name] = _categoriesService.BuildCategorySet(c, _allSymptoms); } catch { }
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
                    if (_weightCatSelector.Items.Count > 0 && _weightCatSelector.SelectedIndex < 0) _weightCatSelector.SelectedIndex = 0;
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
                // Restore left panel collapsed state
                try
                {
                    var sc = FindControl<SplitContainer>(this, "_mainSplit");
                    if (sc != null && _settingsService?.Settings.LeftPanelCollapsed == true)
                    {
                        sc.Panel1Collapsed = true; if (_collapseBtn != null) _collapseBtn.Text = "≫";
                    }
                }
                catch { }
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
                // Restore NB temperature & weights
                try
                {
                    if (_settingsService?.Settings.NaiveBayesTemperature.HasValue == true)
                    {
                        double t = _settingsService.Settings.NaiveBayesTemperature.Value;
                        if (t < 0.01) t = 0.01; if (t > 5.0) t = 5.0;
                        _nbTempEnable.Checked = true;
                        var val = (decimal)(t * 100.0);
                        if (val < _nbTempValue.Minimum) val = _nbTempValue.Minimum;
                        if (val > _nbTempValue.Maximum) val = _nbTempValue.Maximum;
                        _nbTempValue.Value = val;
                    }
                    else
                    {
                        _nbTempEnable.Checked = false;
                    }
                }
                catch { }
                try
                {
                    if (_settingsService?.Settings.CategoryWeights != null && _weightCatSelector.SelectedItem is CatItem ci)
                    {
                        if (_settingsService.Settings.CategoryWeights.TryGetValue(ci.Canonical, out var w) && w > 0)
                        {
                            var pct = (decimal)(w * 100.0);
                            if (pct < _weightValue.Minimum) pct = _weightValue.Minimum;
                            if (pct > _weightValue.Maximum) pct = _weightValue.Maximum;
                            _weightValue.Value = pct;
                        }
                    }
                }
                catch { }
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
                // Restore Ollama settings
                try
                {
                    if (!string.IsNullOrEmpty(_settingsService?.Settings.OllamaUrl))
                        _aiUrlBox.Text = _settingsService.Settings.OllamaUrl;
                    _autoAiCheck.Checked = _settingsService?.Settings.AutoAi ?? false;
                }
                catch { }
                ApplyTheme();
                RefreshSymptomList();
                UpdateDecisionRules();
                UpdatePercRule();
                // Initialize Ollama connection (non-blocking)
                _ = InitializeOllamaAsync();
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
                _selectAllButton.Text = t.T("SelectAll") ?? _selectAllButton.Text;
                _deselectAllButton.Text = t.T("DeselectAll") ?? _deselectAllButton.Text;
                _lblCategory.Text = t.T("Category");
                _selectCategoryButton.Text = t.T("SelectCategory");
                _clearCategoryButton.Text = t.T("ClearCategory");
                _showOnlyCategory.Text = t.T("ShowOnlyCategory");
                _lblModel.Text = t.T("Model");
                _lblThresh.Text = t.T("Threshold");
                _lblMinMatch.Text = t.T("MinMatch");
                _lblTopK.Text = t.T("TopK");
                // Model tuning labels/buttons (fallback to existing text if key missing)
                _applyWeightButton.Text = t.T("ApplyWeight") ?? _applyWeightButton.Text;
                _clearWeightsButton.Text = t.T("ClearWeights") ?? _clearWeightsButton.Text;
                _nbTempEnable.Text = t.T("NaiveBayesTemp") ?? _nbTempEnable.Text;
                _checkButton.Text = t.T("Check");
                _exitButton.Text = t.T("Exit");
                _syncButton.Text = t.T("Sync");
                _missingTransButton.Text = t.T("MissingTranslationsTitle");
                _helpButton.Text = t.T("Help") ?? _helpButton.Text;
                _openLogsButton.Text = t.T("Logs") ?? _openLogsButton.Text;
                _lblLanguage.Text = t.T("Language");
                _disclaimer.Text = "⚠️ " + t.T("Disclaimer");
                _darkModeToggle.Text = t.T("DarkMode");
                _saveSessionButton.Text = t.T("Save") ?? _saveSessionButton.Text;
                _loadSessionButton.Text = t.T("Load") ?? _loadSessionButton.Text;
                _resetSettingsButton.Text = t.T("ResetSettings") ?? _resetSettingsButton.Text;
                _saveSettingsProfileButton.Text = t.T("SaveSettingsProfile") ?? _saveSettingsProfileButton.Text;
                _loadSettingsProfileButton.Text = t.T("LoadSettingsProfile") ?? _loadSettingsProfileButton.Text;
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
                    if (_resultsContext.Items.Count >= 5)
                    {
                        _resultsContext.Items[3].Text = t.T("ExportCsv");
                        _resultsContext.Items[4].Text = t.T("ExportMarkdown");
                    }
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
                // Apply Ollama panel translations
                ApplyOllamaTranslations();
                // Apply Image Analysis panel translations
                ApplyImageAnalysisTranslations();
                // Apply Blood Analysis panel translations
                ApplyBloodAnalysisTranslations();
            }
            else
            {
                Text = TitleText;
                _disclaimer.Text = DisclaimerText;
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

            var sw = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyDictionary<string, double>? catWeights = _settingsService?.Settings.CategoryWeights;
            double? nbTemp = _settingsService?.Settings.NaiveBayesTemperature;
            // Delegate using cached category sets
            IEnumerable<string> GetCats(string conditionName)
            {
                if (_service != null && _categoriesService != null && _service.TryGetCondition(conditionName, out var cond) && cond != null)
                {
                    foreach (var kvp in _categorySetsCache)
                    {
                        if (cond.Symptoms.Any(s => kvp.Value.Contains(s))) yield return kvp.Key;
                    }
                }
            }
            var matches = _service.GetMatches(selectedSymptoms, model, threshold: thr, topK: topKOpt, minMatchCount: minMatch, categoryWeights: catWeights, naiveBayesTemperature: nbTemp, getConditionCategories: GetCats);
            sw.Stop();
            _lastResults = matches;
            RebuildResultsListItems();
            UpdateTriageBanner();
            try { _lblPerf.Text = $"{sw.ElapsedMilliseconds} ms"; } catch { }

            // Auto-run AI diagnosis if enabled and Ollama is available
            if (_autoAiCheck.Checked && _ollamaService?.IsAvailable == true && matches.Count > 0)
            {
                _ = RunAiDiagnosisAsync();
            }
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
                    if (!_categorySetsCache.TryGetValue(cat.Name, out var set))
                    {
                        try { set = _categoriesService.BuildCategorySet(cat, _allSymptoms); _categorySetsCache[cat.Name] = set; } catch { set = new HashSet<string>(); }
                    }
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
            if (!_categorySetsCache.TryGetValue(cat.Name, out var set))
            {
                try { set = _categoriesService.BuildCategorySet(cat, _allSymptoms); _categorySetsCache[cat.Name] = set; } catch { set = new HashSet<string>(); }
            }
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
            if (!_categorySetsCache.TryGetValue(cat.Name, out var set))
            {
                try { set = _categoriesService.BuildCategorySet(cat, _allSymptoms); _categorySetsCache[cat.Name] = set; } catch { set = new HashSet<string>(); }
            }
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

        private void SelectAllSymptoms()
        {
            foreach (var s in _allSymptoms)
            {
                _checkedSymptoms.Add(s);
            }
            // Update visible checkboxes
            for (int i = 0; i < _symptomList.Items.Count; i++)
            {
                _symptomList.SetItemChecked(i, true);
            }
            UpdateCheckButtonEnabled();
        }

        private void DeselectAllSymptoms()
        {
            _checkedSymptoms.Clear();
            for (int i = 0; i < _symptomList.Items.Count; i++)
            {
                _symptomList.SetItemChecked(i, false);
            }
            UpdateCheckButtonEnabled();
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

        /// <summary>Recursively finds a control by name and type within a parent's control tree.</summary>
        private static T? FindControl<T>(Control parent, string name) where T : Control
        {
            foreach (Control c in parent.Controls)
            {
                if (c is T match && c.Name == name) return match;
                var found = FindControl<T>(c, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Scale a horizontal pixel value by the current DPI factor.</summary>
        private int ScaleX(int px)
        {
            using var g = CreateGraphics();
            return (int)(px * g.DpiX / 96.0);
        }

        /// <summary>Scale a vertical pixel value by the current DPI factor.</summary>
        private int ScaleY(int px)
        {
            using var g = CreateGraphics();
            return (int)(px * g.DpiY / 96.0);
        }
    }
}

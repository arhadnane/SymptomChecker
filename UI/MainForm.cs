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
    private SymptomCheckerService? _service;
    private CategoriesService? _categoriesService;
    private SynonymService? _synonymService;
    private TranslationService? _translationService;
    private SettingsService? _settingsService;
    private System.Collections.Generic.List<ConditionMatch> _lastResults = new System.Collections.Generic.List<ConditionMatch>();
    private List<string> _allSymptoms = new List<string>();
    private HashSet<string> _checkedSymptoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                RowCount = 5
            };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var topControls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

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
            _resultsList.DrawMode = DrawMode.OwnerDrawFixed;
            _resultsList.ItemHeight = 22;
            _resultsList.AccessibleName = "Results";
            _resultsList.AccessibleDescription = "List of matching conditions";
            _resultsList.DrawItem += ResultsList_DrawItem;
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
            rightPanel.Controls.Add(topControls, 0, 0);
            rightPanel.Controls.Add(_triageBanner, 0, 1);
            rightPanel.Controls.Add(_resultsList, 0, 2);
            rightPanel.Controls.Add(_disclaimer, 0, 3);
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
                            var code = ((LangItem)_languageSelector.Items[i]).Code;
                            if (!string.IsNullOrEmpty(_settingsService?.Settings.Language) && code.Equals(_settingsService!.Settings.Language, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
                            if (string.IsNullOrEmpty(_settingsService?.Settings.Language) && code.Equals("en", StringComparison.OrdinalIgnoreCase)) { idx = i; }
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
                                if (((CatItem)_categorySelector.Items[i]).Canonical.Equals(desired, StringComparison.OrdinalIgnoreCase))
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
                _darkModeToggle.Checked = _settingsService.Settings.DarkMode;
                // Restore other UI state from settings
                if (!string.IsNullOrEmpty(_settingsService.Settings.Model))
                {
                    for (int i = 0; i < _modelSelector.Items.Count; i++)
                    {
                        if (string.Equals(_modelSelector.Items[i]?.ToString(), _settingsService.Settings.Model, StringComparison.OrdinalIgnoreCase))
                        { _modelSelector.SelectedIndex = i; break; }
                    }
                }
                // Restore numeric values
                try { _threshold.Value = Math.Max(_threshold.Minimum, Math.Min(_threshold.Maximum, _settingsService.Settings.ThresholdPercent)); } catch { }
                try { _minMatch.Value = Math.Max(_minMatch.Minimum, Math.Min(_minMatch.Maximum, _settingsService.Settings.MinMatch)); } catch { }
                try { _topK.Value = Math.Max(_topK.Minimum, Math.Min(_topK.Maximum, _settingsService.Settings.TopK)); } catch { }
                // Restore filter and category visibility
                try { _filterBox.Text = _settingsService.Settings.FilterText ?? string.Empty; } catch { }
                try { _showOnlyCategory.Checked = _settingsService.Settings.ShowOnlyCategory; } catch { }
                ApplyTheme();
                RefreshSymptomList();
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
                                if (((CatItem)_categorySelector.Items[i]).Canonical.Equals(selectedCanonical, StringComparison.OrdinalIgnoreCase))
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

                // Rebuild triage banner for current language
                UpdateTriageBanner();
            }
            else
            {
                Text = TitleText;
                _disclaimer.Text = DisclaimerText;
            }
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
            var bullet = string.Join(Environment.NewLine + " • ", messages);
            _triageBanner.Text = header + Environment.NewLine + " • " + bullet + Environment.NewLine + notice;
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
                case Button btn:
                    btn.BackColor = dark ? Color.FromArgb(60, 60, 60) : SystemColors.Control;
                    btn.ForeColor = fore;
                    break;
                case CheckBox chk:
                    chk.BackColor = panel; chk.ForeColor = fore;
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
            var t = _translationService;
            if (_lastResults == null || _lastResults.Count == 0)
            {
                _resultsList.Items.Add(t?.T("NoMatches") ?? "No matching conditions found based on the current selection.");
            }
            else
            {
                foreach (var m in _lastResults)
                {
                    string name = t?.Condition(m.Name) ?? m.Name;
                    string scoreLabel = t?.T("Score") ?? "Score:";
                    string matchesLabel = t?.T("Matches") ?? "matches:";
                    _resultsList.Items.Add($"{name} — {scoreLabel} {m.Score:F2} ({matchesLabel} {m.MatchCount})");
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

            bool isTop = false;
            // Only highlight if we have actual results and the item corresponds to a scored match
            if (_lastResults != null && _lastResults.Count > 0 && e.Index < _lastResults.Count)
            {
                double max = _lastResults[0].Score;
                double sc = _lastResults[e.Index].Score;
                isTop = Math.Abs(sc - max) < 1e-9 && max > 0;
            }

            Color backColor = isTop ? Color.FromArgb(230, 255, 230) : e.BackColor; // light green for top
            Color foreColor = isTop ? Color.DarkGreen : e.ForeColor;

            using (var backBrush = new SolidBrush(backColor))
            using (var foreBrush = new SolidBrush(foreColor))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
                var font = e.Font ?? SystemFonts.DefaultFont;
                bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                if (rtl) flags |= TextFormatFlags.RightToLeft | TextFormatFlags.Right;
                var rect = e.Bounds;
                rect.Inflate(-4, -2);
                TextRenderer.DrawText(e.Graphics, text, font, rect, foreColor, flags);
            }

            e.DrawFocusRectangle();
        }

        private void ResultsList_DoubleClick(object? sender, EventArgs e)
        {
            if (_service == null) return;
            int idx = _resultsList.SelectedIndex;
            if (idx < 0 || idx >= _lastResults.Count) return;

            var match = _lastResults[idx];
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
                e.Graphics.MeasureString(text.Substring(charFrom), font, e.MarginBounds.Size, StringFormat.GenericTypographic, out int chars, out int lines);
                e.Graphics.DrawString(text.Substring(charFrom, chars), font, Brushes.Black, e.MarginBounds, StringFormat.GenericTypographic);
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
                        if (((LangItem)_languageSelector.Items[i]).Code.Equals(data.Language, StringComparison.OrdinalIgnoreCase))
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

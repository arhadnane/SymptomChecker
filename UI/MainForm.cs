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
    private readonly Button _syncButton = new Button();
    private readonly ComboBox _languageSelector = new ComboBox();
    private readonly ToolTip _toolTip = new ToolTip();
    private readonly Label _lblLanguage = new Label();
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
            _filterBox.TextChanged += (s, e) => RefreshSymptomList();

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
            _categorySelector.SelectedIndexChanged += (s, e) => { _showOnlyCategory.Checked = true; RefreshSymptomList(); };
            _selectCategoryButton.Text = "Select Category";
            _selectCategoryButton.AutoSize = true;
            _selectCategoryButton.Click += (s, e) => SelectByCategory();
            _clearCategoryButton.Text = "Clear Category";
            _clearCategoryButton.AutoSize = true;
            _clearCategoryButton.Click += (s, e) => ClearByCategory();
            _showOnlyCategory.Text = "Show only category";
            _showOnlyCategory.AutoSize = true;
            _showOnlyCategory.CheckedChanged += (s, e) => RefreshSymptomList();

            filterBar.Controls.Add(_lblFilter);
            filterBar.Controls.Add(_filterBox);
            filterBar.Controls.Add(_selectVisibleButton);
            filterBar.Controls.Add(_clearVisibleButton);
            filterBar.Controls.Add(_lblCategory);
            filterBar.Controls.Add(_categorySelector);
            filterBar.Controls.Add(_selectCategoryButton);
            filterBar.Controls.Add(_clearCategoryButton);
            filterBar.Controls.Add(_showOnlyCategory);

            _symptomList.Dock = DockStyle.Fill;
            _symptomList.CheckOnClick = true;
            _symptomList.ItemCheck += SymptomList_ItemCheck;

            var rightPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var topControls = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };

            _modelSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _modelSelector.Items.AddRange(new object[] { DetectionModel.Jaccard, DetectionModel.Cosine, DetectionModel.NaiveBayes });
            _modelSelector.SelectedIndex = 0;

            _threshold.Minimum = 0;
            _threshold.Maximum = 100;
            _threshold.DecimalPlaces = 0;
            _threshold.Value = 0;
            _threshold.Width = 80;
            _lblModel.Text = "Model:";
            _lblModel.AutoSize = true; _lblModel.Padding = new Padding(0, 6, 0, 0);
            _lblThresh.Text = "Threshold (%):";
            _lblThresh.AutoSize = true; _lblThresh.Padding = new Padding(10, 6, 0, 0);

            _minMatch.Minimum = 0; // 0 means any match count
            _minMatch.Maximum = 10;
            _minMatch.Value = 1;
            _minMatch.Width = 60;
            _lblMinMatch.Text = "Min match:"; _lblMinMatch.AutoSize = true; _lblMinMatch.Padding = new Padding(10, 6, 0, 0);

            _topK.Minimum = 0; // 0 = unlimited
            _topK.Maximum = 1000;
            _topK.Value = 0;
            _topK.Width = 70;
            _lblTopK.Text = "Top-K:"; _lblTopK.AutoSize = true; _lblTopK.Padding = new Padding(10, 6, 0, 0);

            _lblLanguage.Text = "Language:"; _lblLanguage.AutoSize = true; _lblLanguage.Padding = new Padding(10, 6, 0, 0);
            _languageSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _languageSelector.Width = 120;
            _languageSelector.SelectedIndexChanged += (s, e) => OnLanguageChanged();

            _checkButton.Text = "Check";
            _checkButton.AutoSize = true;
            _checkButton.Click += CheckButton_Click;
            _checkButton.Enabled = false; // disabled until at least one symptom selected

            _exitButton.Text = "Exit";
            _exitButton.AutoSize = true;
            _exitButton.Click += (s, e) => this.Close();

            _syncButton.Text = "Sync";
            _syncButton.AutoSize = true;
            _syncButton.Click += async (s, e) => await SyncFromWikidataAsync();

            _resultsList.Dock = DockStyle.Fill;
            _resultsList.DrawMode = DrawMode.OwnerDrawFixed;
            _resultsList.ItemHeight = 22;
            _resultsList.DrawItem += ResultsList_DrawItem;
            _resultsList.DoubleClick += ResultsList_DoubleClick;
            // Configure tooltip (infobulle) to guide user to double-click for details
            _toolTip.ShowAlways = true; _toolTip.IsBalloon = true; _toolTip.AutoPopDelay = 8000; _toolTip.InitialDelay = 500; _toolTip.ReshowDelay = 200;
            _toolTip.SetToolTip(_resultsList, "Tip: Double-click a result to view details (educational treatments, OTC examples, self-care).");

            _disclaimer.Text = "⚠️ Educational only. Not medical advice.";
            _disclaimer.AutoSize = true;
            _disclaimer.Padding = new Padding(5);

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
            topControls.Controls.Add(_checkButton);
            topControls.Controls.Add(_exitButton);
            topControls.Controls.Add(_syncButton);
            rightPanel.Controls.Add(topControls, 0, 0);
            rightPanel.Controls.Add(_resultsList, 0, 1);
            rightPanel.Controls.Add(_disclaimer, 0, 2);
            split.Panel2.Controls.Add(rightPanel);

            Controls.Add(split);
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
                    if (_categorySelector.Items.Count > 0) _categorySelector.SelectedIndex = 0;
                }
                    // Load synonyms
                    var synPath = Path.Combine(AppContext.BaseDirectory, "data", "synonyms.json");
                    if (File.Exists(synPath))
                    {
                        _synonymService = new SynonymService(synPath);
                    }
                ApplyTranslations();
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
                _lblLanguage.Text = t.T("Language");
                _disclaimer.Text = "⚠️ " + t.T("Disclaimer");

                // Localize tooltip hint on results list
                _toolTip.SetToolTip(_resultsList, t.T("DoubleClickHint"));

                // RTL for Arabic
                bool rtl = string.Equals(t.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
                ApplyRtl(this, rtl);
            }
            else
            {
                Text = TitleText;
                _disclaimer.Text = DisclaimerText;
            }
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

            MessageBox.Show(this, sb.ToString(), t?.T("DetailsTitle") ?? "Condition Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

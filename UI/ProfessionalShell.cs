using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SymptomChecker.Services;
using SymptomCheckerApp.UI.Controls;

namespace SymptomCheckerApp.UI
{
    public sealed class ProfessionalShell : UserControl
    {
        private readonly SplitContainer _mainSplit;
        private readonly TableLayoutPanel _rightLayout = new();
        private readonly Dictionary<string, CollapsibleSection> _sections = new(StringComparer.OrdinalIgnoreCase);
        private readonly RowStyle _resultsRowStyle = new(SizeType.Percent, 100f);

        public event Action<string, bool>? SectionCollapsedChanged;

        public SplitContainer MainSplit => _mainSplit;

        public ProfessionalShell(
            SplitContainer mainSplit,
            Control symptomsPanel,
            Control modelPanel,
            Control vitalsPanel,
            Control rulesPanel,
            Control resultsHost,
            Control triageBanner,
            Control disclaimer,
            TranslationService? translationService,
            bool darkMode)
        {
            _mainSplit = mainSplit;

            Dock = DockStyle.Fill;
            AccessibleName = "Professional shell";

            _mainSplit.Dock = DockStyle.Fill;
            _mainSplit.Panel1.Controls.Clear();
            _mainSplit.Panel2.Controls.Clear();

            symptomsPanel.Dock = DockStyle.Fill;
            _mainSplit.Panel1.Controls.Add(symptomsPanel);

            var resultsContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            resultsContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            resultsContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            resultsContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            resultsContainer.Controls.Add(resultsHost, 0, 0);
            resultsContainer.Controls.Add(triageBanner, 0, 1);
            resultsContainer.Controls.Add(disclaimer, 0, 2);

            _rightLayout.Dock = DockStyle.Fill;
            _rightLayout.ColumnCount = 1;
            _rightLayout.RowCount = 4;
            _rightLayout.Margin = new Padding(0);
            _rightLayout.Padding = new Padding(0);
            _rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _rightLayout.RowStyles.Add(_resultsRowStyle);

            var modelSection = CreateSection("pro.model", modelPanel);
            var vitalsSection = CreateSection("pro.vitals", vitalsPanel);
            var rulesSection = CreateSection("pro.rules", rulesPanel);
            var resultsSection = CreateSection("pro.results", resultsContainer, fill: true);

            _rightLayout.Controls.Add(modelSection, 0, 0);
            _rightLayout.Controls.Add(vitalsSection, 0, 1);
            _rightLayout.Controls.Add(rulesSection, 0, 2);
            _rightLayout.Controls.Add(resultsSection, 0, 3);

            _mainSplit.Panel2.Controls.Add(_rightLayout);
            Controls.Add(_mainSplit);

            UpdatePresentation(translationService, darkMode);
        }

        public void UpdatePresentation(TranslationService? translationService, bool darkMode)
        {
            bool rtl = string.Equals(translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;

            BackColor = darkMode ? Color.FromArgb(24, 24, 24) : SystemColors.Control;
            _rightLayout.BackColor = BackColor;

            UpdateSectionPresentation("pro.model", translationService?.T("Pro_Section_Model") ?? "Professional section: model and settings", darkMode, rtl);
            UpdateSectionPresentation("pro.vitals", translationService?.T("Pro_Section_Vitals") ?? "Professional section: vitals", darkMode, rtl);
            UpdateSectionPresentation("pro.rules", translationService?.T("Pro_Section_Rules") ?? "Professional section: decision rules", darkMode, rtl);
            UpdateSectionPresentation("pro.results", translationService?.T("Pro_Section_Results") ?? "Professional section: results", darkMode, rtl);
        }

        public void ApplyCollapsedState(IReadOnlyDictionary<string, bool>? collapsedSections)
        {
            foreach (var entry in _sections)
            {
                bool collapsed = collapsedSections != null && collapsedSections.TryGetValue(entry.Key, out var value) && value;
                entry.Value.SetCollapsed(collapsed, raiseEvent: false);
            }
            ApplyResultsRowCollapse();
        }

        public void EnsureExpanded(params string[] sectionKeys)
        {
            if (sectionKeys == null || sectionKeys.Length == 0) return;

            foreach (var key in sectionKeys)
            {
                if (_sections.TryGetValue(key, out var section))
                {
                    section.SetCollapsed(false, raiseEvent: false);
                }
            }

            ApplyResultsRowCollapse();
        }

        private CollapsibleSection CreateSection(string key, Control content, bool fill = false)
        {
            var section = new CollapsibleSection(key, fillContent: fill)
            {
                Dock = fill ? DockStyle.Fill : DockStyle.Top,
                AutoSize = !fill,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            section.SetContent(content);
            section.CollapsedChanged += collapsed =>
            {
                if (string.Equals(key, "pro.results", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyResultsRowCollapse();
                }
                SectionCollapsedChanged?.Invoke(key, collapsed);
            };
            _sections[key] = section;
            return section;
        }

        private void UpdateSectionPresentation(string key, string title, bool darkMode, bool rtl)
        {
            if (_sections.TryGetValue(key, out var section))
            {
                section.UpdatePresentation(title, darkMode, rtl);
            }
        }

        private void ApplyResultsRowCollapse()
        {
            if (!_sections.TryGetValue("pro.results", out var resultsSection)) return;

            if (resultsSection.Collapsed)
            {
                _resultsRowStyle.SizeType = SizeType.AutoSize;
                _resultsRowStyle.Height = resultsSection.HeaderHeight + 4;
            }
            else
            {
                _resultsRowStyle.SizeType = SizeType.Percent;
                _resultsRowStyle.Height = 100f;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using SymptomChecker.Services;

namespace SymptomCheckerApp.UI.Controls
{
    /// <summary>
    /// One condition rendered as a self-contained, scrollable card:
    ///   header (name + confidence badge + match count)
    ///   Home care section (Treatments)
    ///   OTC examples section (Medications)
    ///   When-to-seek-care section (CareAdvice)
    ///   Non-dismissable red "Important" footer
    ///
    /// All sections are localized via TranslationService. Per spec
    /// 001-guided-diagnosis-ux contracts/results-card-rendering.md.
    /// Educational only — never presents itself as a clinical diagnosis.
    /// </summary>
    public sealed class ConditionResultCard : Panel
    {
        private readonly TranslationService? _t;
        private readonly bool _darkMode;
        private readonly string _lang;

        public ConditionResultCard(
            ConditionMatch match,
            Condition condition,
            SymptomCheckerService.DetectionModel model,
            TranslationService? translation,
            bool darkMode)
        {
            _t = translation;
            _darkMode = darkMode;
            _lang = (_t?.CurrentLanguage ?? "en").ToLowerInvariant();

            DoubleBuffered = true;
            Margin = new Padding(8);
            Padding = new Padding(12);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BorderStyle = BorderStyle.FixedSingle;
            MinimumSize = new Size(360, 0);

            if (_lang == "ar")
            {
                RightToLeft = RightToLeft.Yes;
            }

            ApplyTheme();

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = BackColor,
                Margin = new Padding(0),
                Padding = new Padding(0),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // 1. Header
            stack.Controls.Add(BuildHeader(match, model), 0, 0);

            // Resolve localized fields with fallback to English
            var locTreatments = LocalizedList(condition.Treatments, condition.Treatments_Fr, condition.Treatments_Ar);
            var locMeds = LocalizedList(condition.Medications, condition.Medications_Fr, condition.Medications_Ar);
            var locAdvice = LocalizedString(condition.CareAdvice, condition.CareAdvice_Fr, condition.CareAdvice_Ar);

            // 2. Home care (hidden if no data)
            if (locTreatments.Count > 0)
            {
                stack.Controls.Add(BuildSection(T("Card_HomeCare"), locTreatments, accent: AccentTreatment()), 0, stack.RowCount);
            }

            // 3. OTC examples (always shown — fallback message if empty)
            if (locMeds.Count > 0)
            {
                stack.Controls.Add(BuildSection(T("Card_OTC"), locMeds, accent: AccentMedication()), 0, stack.RowCount);
            }
            else
            {
                stack.Controls.Add(BuildSection(T("Card_OTC"),
                    new List<string> { T("Card_NoMedications") }, accent: AccentMedication(), italic: true), 0, stack.RowCount);
            }

            // 4. When to seek care (hidden if missing)
            if (!string.IsNullOrWhiteSpace(locAdvice))
            {
                stack.Controls.Add(BuildSection(T("Card_WhenToSeek"),
                    new List<string> { locAdvice! }, accent: AccentAdvice()), 0, stack.RowCount);
            }

            // 5. Non-dismissable red Important footer
            stack.Controls.Add(BuildImportantFooter(), 0, stack.RowCount);

            AccessibleName = _t?.Condition(match.Name) ?? match.Name;
            AccessibleDescription = "Educational result card with home care, OTC medication examples, and when-to-seek-care guidance.";

            Controls.Add(stack);
        }

        // ------------------------------------------------------------------
        // Theme helpers
        // ------------------------------------------------------------------

        private void ApplyTheme()
        {
            if (_darkMode)
            {
                BackColor = Color.FromArgb(40, 40, 44);
                ForeColor = Color.FromArgb(232, 232, 232);
            }
            else
            {
                BackColor = Color.FromArgb(252, 252, 252);
                ForeColor = Color.FromArgb(28, 28, 28);
            }
        }

        private Color AccentTreatment() => _darkMode ? Color.FromArgb(100, 175, 200) : Color.FromArgb(21, 101, 192);
        private Color AccentMedication() => _darkMode ? Color.FromArgb(120, 190, 130) : Color.FromArgb(46, 125, 50);
        private Color AccentAdvice() => _darkMode ? Color.FromArgb(220, 175, 110) : Color.FromArgb(145, 94, 0);

        // ------------------------------------------------------------------
        // Section builders
        // ------------------------------------------------------------------

        private Control BuildHeader(ConditionMatch match, SymptomCheckerService.DetectionModel model)
        {
            var headerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                BackColor = BackColor,
                Margin = new Padding(0, 0, 0, 6)
            };
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            string displayName = _t?.Condition(match.Name) ?? match.Name;
            var title = new Label
            {
                Text = displayName,
                AutoSize = true,
                Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
                ForeColor = ForeColor,
                Margin = new Padding(0, 2, 0, 0),
                AccessibleName = displayName,
                MaximumSize = new Size(520, 0)
            };

            var confidence = ConfidenceBadgeService.FromScore(model, match.Score);
            string confLabel = T(ConfidenceBadgeService.TranslationKey(confidence));
            var badge = new Label
            {
                Text = $"  {confLabel}  ",
                AutoSize = true,
                Padding = new Padding(0),
                Margin = new Padding(8, 4, 0, 0),
                BackColor = BadgeColor(confidence),
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold),
            };
            var tip = new ToolTip { IsBalloon = true, AutoPopDelay = 6000, InitialDelay = 400 };
            tip.SetToolTip(badge, T("Confidence_Tooltip"));

            string matchLabel = T("Card_MatchCount");
            var matchCnt = new Label
            {
                // Keep this compact so long localized labels do not collapse the condition title.
                Text = $"  #{match.MatchCount}",
                AutoSize = true,
                ForeColor = ForeColor,
                Margin = new Padding(10, 4, 0, 0),
                Font = new Font(Font.FontFamily, 9f, FontStyle.Regular),
            };
            tip.SetToolTip(matchCnt, $"{matchLabel}: {match.MatchCount}");

            headerPanel.Controls.Add(title, 0, 0);
            headerPanel.Controls.Add(badge, 1, 0);
            headerPanel.Controls.Add(matchCnt, 2, 0);
            return headerPanel;
        }

        private Color BadgeColor(Confidence c) => c switch
        {
            Confidence.High => Color.FromArgb(46, 125, 50),       // green
            Confidence.Moderate => Color.FromArgb(145, 94, 0),    // amber
            _ => Color.FromArgb(110, 110, 118)                    // muted
        };

        private Control BuildSection(string heading, IReadOnlyList<string> items, Color accent, bool italic = false)
        {
            var holder = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Margin = new Padding(0, 4, 0, 4),
                BackColor = BackColor,
            };
            holder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var head = new Label
            {
                Text = heading,
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = accent,
                Margin = new Padding(0, 2, 0, 2),
            };
            holder.Controls.Add(head, 0, 0);

            var body = new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9.25f, italic ? FontStyle.Italic : FontStyle.Regular),
                ForeColor = ForeColor,
                Margin = new Padding(8, 0, 0, 0),
                Text = string.Join(Environment.NewLine, items.Select(i => "• " + i)),
                MaximumSize = new Size(540, 0),
                UseCompatibleTextRendering = true,
            };
            holder.Controls.Add(body, 0, 1);
            return holder;
        }

        private Control BuildImportantFooter()
        {
            var footer = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = _darkMode ? Color.FromArgb(95, 30, 30) : Color.FromArgb(255, 235, 235),
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(8, 6, 8, 6),
                BorderStyle = BorderStyle.FixedSingle,
            };
            var lbl = new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = _darkMode ? Color.FromArgb(255, 220, 220) : Color.FromArgb(150, 30, 30),
                Text = T("Card_Important") + " — " + T("Card_AlwaysConsult"),
                MaximumSize = new Size(540, 0),
                AccessibleName = "Important educational disclaimer"
            };
            footer.Controls.Add(lbl);
            return footer;
        }

        // ------------------------------------------------------------------
        // Localization helpers
        // ------------------------------------------------------------------

        private string T(string key) => _t?.T(key) ?? key;

        private List<string> LocalizedList(List<string>? en, List<string>? fr, List<string>? ar)
        {
            List<string>? chosen = _lang switch
            {
                "fr" => (fr != null && fr.Count > 0) ? fr : en,
                "ar" => (ar != null && ar.Count > 0) ? ar : en,
                _ => en
            };
            return chosen ?? new List<string>();
        }

        private string? LocalizedString(string? en, string? fr, string? ar)
        {
            string? chosen = _lang switch
            {
                "fr" => !string.IsNullOrEmpty(fr) ? fr : en,
                "ar" => !string.IsNullOrEmpty(ar) ? ar : en,
                _ => en
            };
            return chosen;
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomChecker.Services;

namespace SymptomCheckerApp.UI.Controls
{
    /// <summary>
    /// First-run overlay for choosing between guided patient mode and advanced
    /// professional mode. Educational only.
    /// </summary>
    public sealed class ModeSelector : UserControl
    {
        private readonly Panel _card = new();
        private readonly Label _title = new();
        private readonly Label _subtitle = new();
        private readonly Label _hint = new();
        private readonly Button _patientButton = new();
        private readonly Label _patientDescription = new();
        private readonly Button _professionalButton = new();
        private readonly Label _professionalDescription = new();

        private TranslationService? _translationService;
        private bool _darkMode;

        public event Action<UiMode>? ModeSelected;

        public ModeSelector(TranslationService? translationService, bool darkMode)
        {
            _translationService = translationService;
            _darkMode = darkMode;

            AccessibleName = "Mode selector";
            Dock = DockStyle.Fill;
            Padding = new Padding(24);

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            _card.AutoSize = true;
            _card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _card.Padding = new Padding(20);
            _card.Margin = new Padding(0);
            _card.MinimumSize = new Size(Scale(560), 0);
            _card.MaximumSize = new Size(Scale(760), 0);
            _card.BorderStyle = BorderStyle.FixedSingle;

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _title.AutoSize = true;
            _title.Font = new Font(Font.FontFamily, 13f, FontStyle.Bold);
            _title.Margin = new Padding(0, 0, 0, 8);

            _subtitle.AutoSize = true;
            _subtitle.MaximumSize = new Size(Scale(680), 0);
            _subtitle.Font = new Font(Font.FontFamily, 9.25f, FontStyle.Regular);
            _subtitle.Margin = new Padding(0, 0, 0, 14);

            var options = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            options.Controls.Add(BuildOptionPanel(_patientButton, _patientDescription, UiMode.Patient), 0, 0);
            options.Controls.Add(BuildOptionPanel(_professionalButton, _professionalDescription, UiMode.Professional), 1, 0);

            _hint.AutoSize = true;
            _hint.MaximumSize = new Size(Scale(680), 0);
            _hint.Font = new Font(Font.FontFamily, 8.75f, FontStyle.Regular);
            _hint.Margin = new Padding(0);

            content.Controls.Add(_title, 0, 0);
            content.Controls.Add(_subtitle, 0, 1);
            content.Controls.Add(options, 0, 2);
            content.Controls.Add(_hint, 0, 3);

            _card.Controls.Add(content);
            outer.Controls.Add(_card, 1, 1);
            Controls.Add(outer);

            UpdatePresentation(_translationService, _darkMode);
        }

        public void UpdatePresentation(TranslationService? translationService, bool darkMode)
        {
            _translationService = translationService;
            _darkMode = darkMode;

            _title.Text = T("Mode_Title", "Choose how you'd like to use Symptom Checker");
            _subtitle.Text = T("Mode_Subtitle", "Educational only — always consult a healthcare professional.");
            _patientButton.Text = T("Mode_Patient", "Patient (guided)");
            _patientDescription.Text = T("Mode_Patient_Description", "A simple step-by-step assistant to explore possible educational explanations for your symptoms.");
            _professionalButton.Text = T("Mode_Professional", "Professional (advanced)");
            _professionalDescription.Text = T("Mode_Professional_Description", "Full controls: detection model, thresholds, decision rules, vitals, AI modules.");
            _hint.Text = T("Mode_Switch_Hint", "You can switch mode anytime (Ctrl+M).");

            bool rtl = string.Equals(_translationService?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;

            BackColor = _darkMode ? Color.FromArgb(24, 26, 30) : Color.FromArgb(236, 240, 245);
            _card.BackColor = _darkMode ? Color.FromArgb(36, 39, 45) : Color.White;
            _card.ForeColor = _darkMode ? Color.WhiteSmoke : Color.FromArgb(28, 28, 28);

            _title.ForeColor = _darkMode ? Color.WhiteSmoke : Color.FromArgb(28, 28, 28);
            _subtitle.ForeColor = _darkMode ? Color.Gainsboro : Color.DimGray;
            _hint.ForeColor = _darkMode ? Color.Gainsboro : Color.DimGray;

            StyleButton(_patientButton, darkAccent: Color.FromArgb(56, 102, 164), lightAccent: Color.FromArgb(25, 118, 210));
            StyleButton(_professionalButton, darkAccent: Color.FromArgb(70, 138, 89), lightAccent: Color.FromArgb(46, 125, 50));

            _patientDescription.ForeColor = _darkMode ? Color.Gainsboro : Color.FromArgb(52, 52, 52);
            _professionalDescription.ForeColor = _darkMode ? Color.Gainsboro : Color.FromArgb(52, 52, 52);
        }

        private Control BuildOptionPanel(Button button, Label description, UiMode mode)
        {
            button.AutoSize = true;
            button.Dock = DockStyle.Top;
            button.FlatStyle = FlatStyle.Flat;
            button.Margin = new Padding(0, 0, 0, 8);
            button.Padding = new Padding(12, 10, 12, 10);
            button.MinimumSize = new Size(Scale(220), Scale(40));
            button.Click += (s, e) => ModeSelected?.Invoke(mode);

            description.AutoSize = true;
            description.MaximumSize = new Size(Scale(280), 0);
            description.Margin = new Padding(0);

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Margin = new Padding(6),
                MinimumSize = new Size(Scale(260), 0),
                BorderStyle = BorderStyle.FixedSingle
            };

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stack.Controls.Add(button, 0, 0);
            stack.Controls.Add(description, 0, 1);
            panel.Controls.Add(stack);

            return panel;
        }

        private void StyleButton(Button button, Color darkAccent, Color lightAccent)
        {
            button.BackColor = _darkMode ? darkAccent : lightAccent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
        }

        private int Scale(int value)
        {
            using var graphics = CreateGraphics();
            return (int)Math.Round(value * graphics.DpiX / 96f);
        }

        private string T(string key, string fallback) => _translationService?.T(key) ?? fallback;
    }
}
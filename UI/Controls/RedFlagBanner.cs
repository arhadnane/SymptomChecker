using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomChecker.Services;

namespace SymptomCheckerApp.UI.Controls
{
    /// <summary>
    /// Non-dismissable red banner that surfaces the list of triggered RedFlag
    /// codes (severity-ordered) with a localized "When to call emergency
    /// services" button. Per spec 001-guided-diagnosis-ux contracts/
    /// red-flags-triage.md. Educational only.
    /// </summary>
    public sealed class RedFlagBanner : Panel
    {
        private readonly Label _title = new();
        private readonly Label _body = new();
        private readonly Button _emergencyButton = new();
        private readonly TableLayoutPanel _stack = new();
        private TranslationService? _t;
        private bool _darkMode;

        /// <summary>
        /// Provide an updated translation service / theme. Safe to call after
        /// construction — re-applies all localized labels and re-themes.
        /// </summary>
        public void UpdateTranslation(TranslationService? translation, bool darkMode)
        {
            _t = translation;
            _darkMode = darkMode;
            ApplyTheme();
            _title.Text = T("RedFlag_Banner_Title");
            _emergencyButton.Text = T("RedFlag_WhenToCall_Button");
            string lang = (_t?.CurrentLanguage ?? "en").ToLowerInvariant();
            RightToLeft = lang == "ar" ? RightToLeft.Yes : RightToLeft.No;
        }

        private void ApplyTheme()
        {
            Color bannerBackground = _darkMode
                ? Color.FromArgb(140, 30, 30)
                : Color.FromArgb(255, 214, 214);
            Color textColor = _darkMode ? Color.White : Color.Black;

            BackColor = bannerBackground;
            ForeColor = textColor;
            _stack.BackColor = bannerBackground;
            _title.ForeColor = textColor;
            _body.ForeColor = textColor;
            _emergencyButton.BackColor = Color.White;
            _emergencyButton.ForeColor = Color.FromArgb(176, 0, 32);
        }

        public RedFlagBanner(TranslationService? translation, bool darkMode)
        {
            _t = translation;
            _darkMode = darkMode;

            Dock = DockStyle.Top;
            AutoSize = true;
            Padding = new Padding(10, 8, 10, 8);
            Margin = new Padding(4, 4, 4, 6);
            DoubleBuffered = true;
            Visible = false;
            BorderStyle = BorderStyle.FixedSingle;

            string lang = (_t?.CurrentLanguage ?? "en").ToLowerInvariant();
            if (lang == "ar") RightToLeft = RightToLeft.Yes;

            _title.AutoSize = true;
            _title.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            _title.Text = T("RedFlag_Banner_Title");
            _title.Margin = new Padding(0, 0, 0, 4);

            _body.AutoSize = true;
            _body.Font = new Font(Font.FontFamily, 9.25f, FontStyle.Regular);
            _body.MaximumSize = new Size(620, 0);

            _emergencyButton.Text = T("RedFlag_WhenToCall_Button");
            _emergencyButton.AutoSize = true;
            _emergencyButton.FlatStyle = FlatStyle.Flat;
            _emergencyButton.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            _emergencyButton.Margin = new Padding(0, 6, 0, 0);
            _emergencyButton.AccessibleName = T("RedFlag_WhenToCall_Button");
            _emergencyButton.Click += (s, e) =>
            {
                MessageBox.Show(this,
                    T("RedFlag_WhenToCall_Body"),
                    T("RedFlag_Banner_Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    RightToLeft == RightToLeft.Yes ? MessageBoxOptions.RtlReading : 0);
            };

            _stack.Dock = DockStyle.Fill;
            _stack.AutoSize = true;
            _stack.ColumnCount = 1;
            _stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _stack.Controls.Add(_title, 0, 0);
            _stack.Controls.Add(_body, 0, 1);
            _stack.Controls.Add(_emergencyButton, 0, 2);
            Controls.Add(_stack);

            ApplyTheme();

            AccessibleName = "Red flag banner";
            AccessibleDescription = "Possible urgent symptoms — educational guidance only";
        }

        /// <summary>
        /// Updates the banner. Empty list hides the banner.
        /// </summary>
        public void SetFlags(IReadOnlyList<RedFlag> flags)
        {
            if (flags == null || flags.Count == 0)
            {
                Visible = false;
                return;
            }

            var sb = new StringBuilder();
            foreach (var f in flags)
            {
                sb.AppendLine("• " + T(f.MessageKey));
            }
            _body.Text = sb.ToString().TrimEnd();
            Visible = true;
        }

        private string T(string key) => _t?.T(key) ?? key;
    }
}

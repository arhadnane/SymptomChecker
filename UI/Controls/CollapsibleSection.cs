using System;
using System.Drawing;
using System.Windows.Forms;

namespace SymptomCheckerApp.UI.Controls
{
    public sealed class CollapsibleSection : UserControl
    {
        private readonly TableLayoutPanel _layout = new();
        private readonly Button _toggleButton = new();
        private readonly Panel _contentHost = new();
        private readonly bool _fillContent;
        private bool _collapsed;
        private string _title = string.Empty;

        public event Action<bool>? CollapsedChanged;

        public string SectionKey { get; }

        public bool Collapsed => _collapsed;

        public int HeaderHeight => _toggleButton.Height + _layout.Padding.Vertical;

        public CollapsibleSection(string sectionKey, bool fillContent = false)
        {
            SectionKey = sectionKey;
            _fillContent = fillContent;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 0, 8);

            _layout.Dock = DockStyle.Fill;
            _layout.ColumnCount = 1;
            _layout.RowCount = 2;
            _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _layout.RowStyles.Add(new RowStyle(_fillContent ? SizeType.Percent : SizeType.AutoSize, _fillContent ? 100 : 0));
            _layout.Padding = new Padding(0);

            _toggleButton.Dock = DockStyle.Top;
            _toggleButton.AutoSize = false;
            _toggleButton.Height = 36;
            _toggleButton.FlatStyle = FlatStyle.Flat;
            _toggleButton.TextAlign = ContentAlignment.MiddleLeft;
            _toggleButton.Padding = new Padding(10, 0, 10, 0);
            _toggleButton.Click += (s, e) => SetCollapsed(!_collapsed);

            _contentHost.Dock = _fillContent ? DockStyle.Fill : DockStyle.Top;
            _contentHost.Margin = new Padding(0);
            _contentHost.Padding = new Padding(0);
            _contentHost.AutoSize = !_fillContent;
            _contentHost.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            _layout.Controls.Add(_toggleButton, 0, 0);
            _layout.Controls.Add(_contentHost, 0, 1);
            Controls.Add(_layout);
            UpdateButtonText();
        }

        public void SetContent(Control content)
        {
            _contentHost.Controls.Clear();
            content.Dock = _fillContent ? DockStyle.Fill : DockStyle.Top;
            _contentHost.Controls.Add(content);
        }

        public void UpdatePresentation(string title, bool darkMode, bool rtl)
        {
            _title = title;
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;

            BackColor = darkMode ? Color.FromArgb(24, 24, 24) : SystemColors.Control;
            _layout.BackColor = BackColor;
            _contentHost.BackColor = darkMode ? Color.FromArgb(24, 24, 24) : SystemColors.Control;

            _toggleButton.BackColor = darkMode ? Color.FromArgb(44, 48, 54) : Color.White;
            _toggleButton.ForeColor = darkMode ? Color.WhiteSmoke : Color.FromArgb(28, 28, 28);
            _toggleButton.FlatAppearance.BorderColor = darkMode ? Color.FromArgb(72, 78, 84) : Color.FromArgb(210, 214, 220);
            _toggleButton.FlatAppearance.BorderSize = 1;
            _toggleButton.TextAlign = rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;

            UpdateButtonText();
        }

        public void SetCollapsed(bool collapsed, bool raiseEvent = true)
        {
            if (_collapsed == collapsed) return;

            _collapsed = collapsed;
            _contentHost.Visible = !collapsed;
            UpdateButtonText();

            if (raiseEvent)
            {
                CollapsedChanged?.Invoke(_collapsed);
            }
        }

        private void UpdateButtonText()
        {
            string caret = _collapsed ? "▸" : "▾";
            _toggleButton.Text = string.IsNullOrWhiteSpace(_title) ? caret : caret + " " + _title;
        }
    }
}
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SymptomCheckerApp.UI
{
    // Theme application logic
    public partial class MainForm
    {
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
                    if (btn == _checkButton)
                    {
                        btn.BackColor = dark ? Color.FromArgb(40, 167, 69) : Color.MediumSeaGreen;
                        btn.ForeColor = Color.White;
                        try { btn.FlatAppearance.BorderSize = 1; btn.FlatAppearance.BorderColor = dark ? Color.FromArgb(30, 120, 50) : Color.SeaGreen; } catch { }
                    }
                    else
                    {
                        btn.BackColor = dark ? Color.FromArgb(60, 60, 60) : SystemColors.Control;
                        btn.ForeColor = fore;
                    }
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

        private void ApplyRtl(Control root, bool rtl)
        {
            if (root is Form f)
            {
                try { f.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No; } catch { }
                try { f.RightToLeftLayout = rtl; } catch { }
            }
        }
    }
}

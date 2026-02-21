using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.UI
{
    // Results rendering: rebuild, draw, measure
    public partial class MainForm
    {
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
            var categories = _categoriesService?.GetAllCategories()?.ToList() ?? new List<SymptomCategory>();
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

            // Use cached sets
            var catSets = _categorySetsCache;

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
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.UI
{
    // Export functionality (CSV, Markdown, HTML)
    public partial class MainForm
    {
        private bool _exportSelectedOnly = false;

        private static string EscapeCsv(string? input)
        {
            if (input == null) return string.Empty;
            bool needQuotes = input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r');
            string s = input.Replace("\"", "\"\"");
            return needQuotes ? "\"" + s + "\"" : s;
        }

        private string DetermineBestCategoryDisplay(string conditionCanonical)
        {
            if (_categoriesService == null || _service == null) return string.Empty;
            if (!_service.TryGetCondition(conditionCanonical, out var cond) || cond == null) return string.Empty;
            var cats = _categoriesService.GetAllCategories()?.ToList() ?? new List<SymptomCategory>();
            if (cats.Count == 0) return string.Empty;
            var catSets = _categorySetsCache;

            string bestCat = string.Empty; int best = -1;
            foreach (var kvp in catSets)
            {
                int overlap = 0;
                foreach (var s in cond.Symptoms)
                {
                    if (kvp.Value.Contains(s)) overlap++;
                }
                if (overlap > best)
                {
                    best = overlap; bestCat = kvp.Key;
                }
            }
            if (string.IsNullOrEmpty(bestCat)) return string.Empty;
            return _translationService?.Category(bestCat) ?? bestCat;
        }

        private IEnumerable<ConditionMatch> GetExportTargetMatches()
        {
            if (_exportSelectedOnly && _resultsList.SelectedIndex >= 0)
            {
                int idx = _resultsList.SelectedIndex;
                int resultIdx = (idx >= 0 && idx < _resultIndexMap.Count) ? _resultIndexMap[idx] : -1;
                if (resultIdx >= 0 && resultIdx < _lastResults.Count)
                {
                    return new[] { _lastResults[resultIdx] };
                }
            }
            return _lastResults ?? Enumerable.Empty<ConditionMatch>();
        }

        private void RememberExportFolder(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && _settingsService != null)
                {
                    _settingsService.Settings.LastExportFolder = dir;
                    _settingsService.Save();
                }
            }
            catch { }
        }

        private void ExportResultsCsv()
        {
            try
            {
                if (_lastResults == null || _lastResults.Count == 0)
                {
                    MessageBox.Show(this, _translationService?.T("NoMatches") ?? "No matching conditions found based on the current selection.",
                        _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var t = _translationService;
                string hCond = t?.T("ExportHeader_Condition") ?? "Condition";
                string hScore = t?.T("ExportHeader_Score") ?? "Score";
                string hMatches = t?.T("ExportHeader_Matches") ?? "Matches";
                string hCategory = t?.T("ExportHeader_Category") ?? "Category";

                string? initDir = _settingsService?.Settings.LastExportFolder;
                var sfd = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "results.csv", InitialDirectory = Directory.Exists(initDir) ? initDir : null };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                RememberExportFolder(sfd.FileName);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(string.Join(",", new[] { EscapeCsv(hCond), EscapeCsv(hScore), EscapeCsv(hMatches), EscapeCsv(hCategory) }));

                var rows = GetExportTargetMatches();
                foreach (var m in rows)
                {
                    string condDisp = _translationService?.Condition(m.Name) ?? m.Name;
                    string scoreStr = m.Score.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    string catDisp = DetermineBestCategoryDisplay(m.Name);
                    sb.AppendLine(string.Join(",", new[]
                    {
                        EscapeCsv(condDisp),
                        EscapeCsv(scoreStr),
                        EscapeCsv(m.MatchCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        EscapeCsv(catDisp)
                    }));
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportResultsMarkdown()
        {
            try
            {
                if (_lastResults == null || _lastResults.Count == 0)
                {
                    MessageBox.Show(this, _translationService?.T("NoMatches") ?? "No matching conditions found based on the current selection.",
                        _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var t = _translationService;
                string hCond = t?.T("ExportHeader_Condition") ?? "Condition";
                string hScore = t?.T("ExportHeader_Score") ?? "Score";
                string hMatches = t?.T("ExportHeader_Matches") ?? "Matches";
                string hCategory = t?.T("ExportHeader_Category") ?? "Category";

                string? initDir = _settingsService?.Settings.LastExportFolder;
                var sfd = new SaveFileDialog { Filter = "Markdown (*.md)|*.md|Text (*.txt)|*.txt", FileName = "results.md", InitialDirectory = Directory.Exists(initDir) ? initDir : null };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                RememberExportFolder(sfd.FileName);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# " + (t?.T("Title") ?? "Symptom Checker (Educational)"));
                if (_checkedSymptoms.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**" + (t?.T("SymptomsLabel") ?? "Symptoms:") + "** " + string.Join(", ", _checkedSymptoms.Select(s => _translationService?.Symptom(s) ?? s)));
                }
                sb.AppendLine();
                sb.AppendLine($"| {hCond} | {hScore} | {hMatches} | {hCategory} |");
                sb.AppendLine("| --- | ---: | ---: | --- |");
                var rows = GetExportTargetMatches();
                foreach (var m in rows)
                {
                    string condDisp = _translationService?.Condition(m.Name) ?? m.Name;
                    string scoreStr = m.Score.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    string catDisp = DetermineBestCategoryDisplay(m.Name);
                    string matched = string.Join(", ", m.MatchedSymptoms.Select(s => _translationService?.Symptom(s) ?? s));
                    sb.AppendLine($"| {condDisp} | {scoreStr} | {m.MatchCount} | {catDisp} | <!-- {matched} -->");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportResultsHtml()
        {
            try
            {
                if (_lastResults == null || _lastResults.Count == 0)
                {
                    MessageBox.Show(this, _translationService?.T("NoMatches") ?? "No matching conditions found based on the current selection.",
                        _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var t = _translationService;
                string hCond = t?.T("ExportHeader_Condition") ?? "Condition";
                string hScore = t?.T("ExportHeader_Score") ?? "Score";
                string hMatches = t?.T("ExportHeader_Matches") ?? "Matches";
                string hCategory = t?.T("ExportHeader_Category") ?? "Category";
                string hMatched = t?.T("ExportHeader_MatchedSymptoms") ?? "Matched Symptoms";
                string? initDir = _settingsService?.Settings.LastExportFolder;
                var sfd = new SaveFileDialog { Filter = "HTML (*.html)|*.html|HTM (*.htm)|*.htm", FileName = "results.html", InitialDirectory = Directory.Exists(initDir) ? initDir : null };
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                RememberExportFolder(sfd.FileName);
                var rows = GetExportTargetMatches();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>" + (t?.T("Title") ?? "Symptom Checker") + "</title><style>body{font-family:Segoe UI,Arial,sans-serif;font-size:14px;}table{border-collapse:collapse;width:100%;}th,td{border:1px solid #ccc;padding:4px 6px;text-align:left;}th{background:#f4f4f4;}code{font-size:12px;color:#555;} .meta{font-size:11px;color:#666;margin-top:8px;} .badge{background:#1976d2;color:#fff;border-radius:4px;padding:2px 6px;font-size:11px;} </style></head><body>");
                sb.AppendLine("<h1>" + (t?.T("Title") ?? "Symptom Checker (Educational)") + "</h1>");
                if (_checkedSymptoms.Count > 0)
                {
                    sb.AppendLine("<p><strong>" + (t?.T("SymptomsLabel") ?? "Symptoms:") + "</strong> " + string.Join(", ", _checkedSymptoms.Select(s => _translationService?.Symptom(s) ?? s)) + "</p>");
                }
                sb.AppendLine("<table><thead><tr><th>" + hCond + "</th><th>" + hScore + "</th><th>" + hMatches + "</th><th>" + hCategory + "</th><th>" + hMatched + "</th></tr></thead><tbody>");
                foreach (var m in rows)
                {
                    string condDisp = _translationService?.Condition(m.Name) ?? m.Name;
                    string scoreStr = m.Score.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                    string catDisp = DetermineBestCategoryDisplay(m.Name);
                    string matched = string.Join(", ", m.MatchedSymptoms.Select(s => _translationService?.Symptom(s) ?? s));
                    sb.AppendLine("<tr><td>" + System.Net.WebUtility.HtmlEncode(condDisp) + "</td><td>" + scoreStr + "</td><td>" + m.MatchCount + "</td><td>" + System.Net.WebUtility.HtmlEncode(catDisp) + "</td><td>" + System.Net.WebUtility.HtmlEncode(matched) + "</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
                sb.AppendLine("<p class=\"meta\">Generated " + DateTime.Now.ToString("u") + " – " + (t?.T("Disclaimer") ?? "Educational only. Not medical advice.") + "</p>");
                sb.AppendLine("</body></html>");
                File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

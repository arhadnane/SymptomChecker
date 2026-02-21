using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;
using SymptomChecker.Services;

namespace SymptomCheckerApp.UI
{
    // Dialogs: details, help, missing translations, print
    public partial class MainForm
    {
        private void ResultsList_DoubleClick(object? sender, EventArgs e)
        {
            if (_service == null) return;
            int idx = _resultsList.SelectedIndex;
            if (idx < 0) return;
            int resultIdx = (idx >= 0 && idx < _resultIndexMap.Count) ? _resultIndexMap[idx] : -1;
            if (resultIdx == -1) return;
            if (resultIdx < 0 || resultIdx >= _lastResults.Count) return;

            var match = _lastResults[resultIdx];
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

        private void BuildDetailsText(System.Text.StringBuilder sb, TranslationService? t, ConditionMatch match, Condition condition)
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

            var model = (SymptomCheckerService.DetectionModel)_modelSelector.SelectedItem!;
            sb.AppendLine();
            sb.AppendLine(t?.T("ExplainabilityHeader") ?? "How this score was computed:");
            if (model == SymptomCheckerService.DetectionModel.Jaccard || model == SymptomCheckerService.DetectionModel.Cosine)
            {
                sb.AppendLine($" • {(t?.T("Explain_MatchedOverlap") ?? "Matched overlap")}: {match.MatchCount}");
                sb.AppendLine($" • {(t?.T("Explain_Similarity") ?? "Similarity")}: {match.Score:F3}");
            }
            else if (model == SymptomCheckerService.DetectionModel.NaiveBayes)
            {
                sb.AppendLine($" • {(t?.T("Explain_Prob") ?? "Estimated probability")}: {match.Score:F3}");
            }

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
            if (rtl)
            {
                text = TransformBulletsForRtl(text);
            }
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
                try { tb.TextAlign = HorizontalAlignment.Right; } catch { }
            }
            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var btnClose = new Button { Text = _translationService?.T("Close") ?? "Close", AutoSize = true };
            btnClose.Click += (s, e) => dlg.Close();
            var btnCopy = new Button { Text = _translationService?.T("Copy") ?? "Copy", AutoSize = true };
            btnCopy.Click += (s, e) => { try { Clipboard.SetText(text); } catch { } };
            var btnAskAi = new Button
            {
                Text = _translationService?.T("AiMedications") ?? "💊 Ask AI for Medications",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(230, 255, 230),
                Enabled = _ollamaService?.IsAvailable ?? false
            };
            btnAskAi.Click += async (s, e) =>
            {
                btnAskAi.Enabled = false;
                btnAskAi.Text = "⏳ ...";
                try { await RunAiMedicationAdviceAsync(); } catch { }
                btnAskAi.Text = _translationService?.T("AiMedications") ?? "💊 Ask AI for Medications";
                btnAskAi.Enabled = _ollamaService?.IsAvailable ?? false;
            };
            btnPanel.Controls.Add(btnClose);
            btnPanel.Controls.Add(btnCopy);
            btnPanel.Controls.Add(btnAskAi);
            dlg.Controls.Add(tb);
            dlg.Controls.Add(btnPanel);
            dlg.ShowDialog(this);
        }

        private string TransformBulletsForRtl(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var lines = input.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                var trimmed = l.TrimStart();
                if (trimmed.StartsWith("• ") || trimmed.StartsWith("•\t") || trimmed.StartsWith("•"))
                {
                    int idx = l.IndexOf('•');
                    if (idx >= 0)
                    {
                        var after = l.Substring(idx + 1).TrimStart();
                        lines[i] = after + "  •";
                    }
                }
            }
            return string.Join(Environment.NewLine, lines);
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

        private void ShowHelpDialog()
        {
            var t = _translationService;
            bool rtl = string.Equals(t?.CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);
            string title = t?.T("Help_Title") ?? "About & Help";
            using var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Width = 780,
                Height = 620
            };
            ApplyRtl(dlg, rtl);

            string body = string.Empty;
            string nl = Environment.NewLine;
            body += (t?.T("Help_Header") ?? "Symptom Checker (Educational)") + nl + nl;
            body += (t?.T("Help_WhatItDoes") ?? "Select symptoms from a list to see suggested conditions. No free text.") + nl + nl;
            body += (t?.T("Help_Models") ?? "Models: Jaccard, Cosine (binary), Naive Bayes (Bernoulli).") + nl;
            body += (t?.T("Help_Params") ?? "Parameters: Threshold (%), Min Match, Top‑K.") + nl + nl;
            body += (t?.T("Help_VitalsRules") ?? "Vitals and decision rules are educational approximations (Centor/McIsaac, PERC).") + nl;
            body += (t?.T("Help_TriageV2") ?? "Triage v2 highlights possible red flags using symptoms + vitals + PERC context.") + nl + nl;
            body += (t?.T("Help_TriageThresholds") ?? "Thresholds: SpO₂<92, SBP<90 or ≥180/DBP≥120, HR≥120, RR≥30, Temp≥40°C.") + nl + nl;
            body += (t?.T("Help_DataFiles") ?? "Data files in data/: conditions.json, categories.json, translations.json, synonyms.json.") + nl;
            body += (t?.T("Help_Translations") ?? "Use 'Missing Translations' to review absent keys and export a report.") + nl + nl;
            body += (t?.T("Help_Disclaimer") ?? "Educational only. Not medical advice.") + nl;

            if (rtl) body = TransformBulletsForRtl(body);

            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                Text = body
            };
            if (rtl)
            {
                try { tb.RightToLeft = RightToLeft.Yes; } catch { }
                try { tb.TextAlign = HorizontalAlignment.Right; } catch { }
            }

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var btnClose = new Button { Text = t?.T("Close") ?? "Close", AutoSize = true };
            btnClose.Click += (s, e) => dlg.Close();
            btnPanel.Controls.Add(btnClose);

            dlg.Controls.Add(tb);
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
                var g = e.Graphics;
                if (g == null) { e.HasMorePages = false; return; }
                g.MeasureString(text.Substring(charFrom), font, e.MarginBounds.Size, StringFormat.GenericTypographic, out int chars, out int lines);
                g.DrawString(text.Substring(charFrom, chars), font, Brushes.Black, e.MarginBounds, StringFormat.GenericTypographic);
                charFrom += chars;
                e.HasMorePages = charFrom < text.Length;
            };
            try { using var dlg = new PrintPreviewDialog { Document = pd, Width = 800, Height = 600 }; dlg.ShowDialog(this); }
            catch { try { pd.Print(); } catch { } }
        }

        private void OpenLogsFolder()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, _translationService?.T("Error") ?? "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

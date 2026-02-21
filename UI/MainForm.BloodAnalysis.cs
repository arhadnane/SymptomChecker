using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SymptomCheckerApp.Models;

namespace SymptomCheckerApp.UI
{
    /// <summary>
    /// Blood microscope image analysis feature: upload a blood smear / blood under microscope
    /// image and use a multimodal AI model (e.g. LLaVA) to identify cell morphology,
    /// abnormalities, and possible haematological conditions.
    /// </summary>
    public partial class MainForm
    {
        // Blood analysis UI controls
        private readonly GroupBox _grpBloodAnalysis = new GroupBox();
        private readonly Button _bloodUploadButton = new Button();
        private readonly Button _bloodAnalyzeButton = new Button();
        private readonly Button _bloodClearButton = new Button();
        private readonly Button _bloodApplySymptomsButton = new Button();
        private readonly PictureBox _bloodPreview = new PictureBox();
        private readonly RichTextBox _bloodResultBox = new RichTextBox();
        private readonly ProgressBar _bloodProgress = new ProgressBar();
        private readonly Label _bloodTimingLabel = new Label();
        private readonly Label _bloodInfoLabel = new Label();

        private string? _currentBloodImageBase64;
        private BloodAnalysisResult? _lastBloodAnalysisResult;
        private CancellationTokenSource? _bloodAnalysisCts;

        /// <summary>Build and wire the Blood Analysis panel. Called from InitializeLayout.</summary>
        private void InitializeBloodAnalysisPanel(Control parent)
        {
            _grpBloodAnalysis.Text = "🔬 Blood Microscope Analysis (Vision AI)";
            _grpBloodAnalysis.Dock = DockStyle.Fill;
            _grpBloodAnalysis.Padding = new Padding(6);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // toolbar
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // progress
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // content
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // timing

            // Toolbar row
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            _bloodUploadButton.Text = "📁 Upload Blood Image";
            _bloodUploadButton.AutoSize = true;
            _bloodUploadButton.FlatStyle = FlatStyle.Flat;
            _bloodUploadButton.BackColor = Color.FromArgb(240, 230, 230);
            _bloodUploadButton.AccessibleName = "Upload a blood microscope image for analysis";
            _bloodUploadButton.Click += (s, e) => UploadBloodImage();

            _bloodAnalyzeButton.Text = "🔬 Analyze Blood";
            _bloodAnalyzeButton.AutoSize = true;
            _bloodAnalyzeButton.FlatStyle = FlatStyle.Flat;
            _bloodAnalyzeButton.BackColor = Color.FromArgb(255, 230, 230);
            _bloodAnalyzeButton.AccessibleName = "Analyze the uploaded blood image with AI";
            _bloodAnalyzeButton.Enabled = false;
            _bloodAnalyzeButton.Click += async (s, e) => await RunBloodAnalysisAsync();

            _bloodApplySymptomsButton.Text = "✅ Apply Symptoms";
            _bloodApplySymptomsButton.AutoSize = true;
            _bloodApplySymptomsButton.FlatStyle = FlatStyle.Flat;
            _bloodApplySymptomsButton.BackColor = Color.FromArgb(230, 255, 230);
            _bloodApplySymptomsButton.AccessibleName = "Select deduced symptoms in the symptom list";
            _bloodApplySymptomsButton.Enabled = false;
            _bloodApplySymptomsButton.Click += (s, e) => ApplyBloodDeducedSymptoms();

            _bloodClearButton.Text = "🗑 Clear";
            _bloodClearButton.AutoSize = true;
            _bloodClearButton.FlatStyle = FlatStyle.Flat;
            _bloodClearButton.AccessibleName = "Clear uploaded blood image";
            _bloodClearButton.Enabled = false;
            _bloodClearButton.Click += (s, e) => ClearBloodAnalysis();

            _bloodInfoLabel.Text = "Supported: peripheral blood smear, thick/thin film, bone marrow smear — use a vision model (llava, bakllava, etc.)";
            _bloodInfoLabel.AutoSize = true;
            _bloodInfoLabel.ForeColor = Color.DimGray;
            _bloodInfoLabel.Padding = new Padding(6, 6, 0, 0);
            _bloodInfoLabel.Font = new Font(Font.FontFamily, 8f);

            toolbar.Controls.Add(_bloodUploadButton);
            toolbar.Controls.Add(_bloodAnalyzeButton);
            toolbar.Controls.Add(_bloodApplySymptomsButton);
            toolbar.Controls.Add(_bloodClearButton);
            toolbar.Controls.Add(_bloodInfoLabel);

            layout.Controls.Add(toolbar, 0, 0);
            layout.SetColumnSpan(toolbar, 2);

            // Progress bar
            _bloodProgress.Dock = DockStyle.Top;
            _bloodProgress.Style = ProgressBarStyle.Marquee;
            _bloodProgress.MarqueeAnimationSpeed = 30;
            _bloodProgress.Height = 4;
            _bloodProgress.Visible = false;
            layout.Controls.Add(_bloodProgress, 0, 1);
            layout.SetColumnSpan(_bloodProgress, 2);

            // Image preview (left column)
            _bloodPreview.Dock = DockStyle.Fill;
            _bloodPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _bloodPreview.BorderStyle = BorderStyle.FixedSingle;
            _bloodPreview.BackColor = Color.FromArgb(245, 240, 240);
            _bloodPreview.AccessibleName = "Blood image preview";
            _bloodPreview.Paint += (s, e) =>
            {
                if (_bloodPreview.Image == null)
                {
                    var text = "Drop or upload\na blood smear\nmicroscope image";
                    using var font = new Font("Segoe UI", 10f, FontStyle.Italic);
                    using var brush = new SolidBrush(Color.FromArgb(150, 130, 130));
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(text, font, brush, _bloodPreview.ClientRectangle, sf);
                }
            };
            _bloodPreview.AllowDrop = true;
            _bloodPreview.DragEnter += (s, e) =>
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            _bloodPreview.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    LoadBloodImageFile(files[0]);
            };
            layout.Controls.Add(_bloodPreview, 0, 2);

            // Results box (right column)
            _bloodResultBox.Dock = DockStyle.Fill;
            _bloodResultBox.ReadOnly = true;
            _bloodResultBox.BorderStyle = BorderStyle.FixedSingle;
            _bloodResultBox.BackColor = SystemColors.Window;
            _bloodResultBox.Font = new Font("Segoe UI", 9.5f);
            _bloodResultBox.AccessibleName = "Blood analysis results";
            _bloodResultBox.Text =
                "Upload a blood microscope image (peripheral blood smear, thick/thin film) and click 'Analyze Blood'.\n\n" +
                "The AI vision model will:\n" +
                "  • Identify stain type and magnification\n" +
                "  • Describe red blood cell morphology\n" +
                "  • Identify white blood cell types & differential count\n" +
                "  • Assess platelet count and morphology\n" +
                "  • Detect parasites (malaria, babesia, etc.)\n" +
                "  • List morphological abnormalities\n" +
                "  • Suggest possible haematological conditions\n\n" +
                "You can then apply deduced symptoms to the symptom checker.\n\n" +
                "⚠ Requires a multimodal model in Ollama (e.g. llava, bakllava, llava-llama3).";
            layout.Controls.Add(_bloodResultBox, 1, 2);

            // Timing label
            _bloodTimingLabel.Text = "";
            _bloodTimingLabel.AutoSize = true;
            _bloodTimingLabel.Padding = new Padding(4);
            _bloodTimingLabel.ForeColor = Color.DimGray;
            _bloodTimingLabel.AccessibleName = "Blood analysis timing";
            layout.Controls.Add(_bloodTimingLabel, 0, 3);
            layout.SetColumnSpan(_bloodTimingLabel, 2);

            _grpBloodAnalysis.Controls.Add(layout);
            parent.Controls.Add(_grpBloodAnalysis);
        }

        /// <summary>Open a file dialog to pick a blood microscope image.</summary>
        private void UploadBloodImage()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select a blood microscope image",
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff|All files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
                LoadBloodImageFile(dlg.FileName);
        }

        /// <summary>Load a blood image file, display preview, and prepare base64.</summary>
        private void LoadBloodImageFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var validExts = new HashSet<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff" };
                if (!validExts.Contains(ext))
                {
                    MessageBox.Show(this, "Unsupported image format. Please use JPG, PNG, BMP, GIF, TIFF or WebP.",
                        "Invalid Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 20 * 1024 * 1024)
                {
                    MessageBox.Show(this, "Image file is too large. Maximum size is 20 MB.",
                        "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var imageBytes = File.ReadAllBytes(filePath);
                _currentBloodImageBase64 = Convert.ToBase64String(imageBytes);

                using var ms = new MemoryStream(imageBytes);
                var oldImage = _bloodPreview.Image;
                _bloodPreview.Image = Image.FromStream(ms);
                oldImage?.Dispose();

                _bloodAnalyzeButton.Enabled = true;
                _bloodClearButton.Enabled = true;
                _bloodApplySymptomsButton.Enabled = false;
                _lastBloodAnalysisResult = null;

                _bloodResultBox.Clear();
                _bloodResultBox.Text = $"Blood image loaded: {Path.GetFileName(filePath)}\n" +
                    $"Size: {fileInfo.Length / 1024} KB\n\n" +
                    "Click 'Analyze Blood' to start AI haematology analysis.\n" +
                    "Make sure a vision model (llava, bakllava) is selected in the AI panel.";
                _bloodTimingLabel.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading image: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Clear the uploaded blood image and results.</summary>
        private void ClearBloodAnalysis()
        {
            var oldImage = _bloodPreview.Image;
            _bloodPreview.Image = null;
            oldImage?.Dispose();

            _currentBloodImageBase64 = null;
            _lastBloodAnalysisResult = null;
            _bloodAnalyzeButton.Enabled = false;
            _bloodClearButton.Enabled = false;
            _bloodApplySymptomsButton.Enabled = false;
            _bloodTimingLabel.Text = "";

            _bloodResultBox.Clear();
            _bloodResultBox.Text = "Upload a blood microscope image to analyze.";
            _bloodPreview.Invalidate();
        }

        /// <summary>Run the AI blood microscope analysis using the Ollama vision model.</summary>
        private async Task RunBloodAnalysisAsync()
        {
            if (_ollamaService == null || string.IsNullOrEmpty(_currentBloodImageBase64))
            {
                _bloodResultBox.Text = "Please upload a blood image and ensure Ollama is connected.";
                return;
            }

            var selectedModel = _ollamaModelSelector.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedModel))
            {
                _ollamaService.SetModel(selectedModel);
            }
            else
            {
                _bloodResultBox.Text = "Please select a vision model (e.g. llava) in the AI panel.";
                return;
            }

            _bloodAnalysisCts?.Cancel();
            _bloodAnalysisCts = new CancellationTokenSource();

            SetBloodAnalysisLoading(true);

            try
            {
                var lang = _translationService?.CurrentLanguage ?? "en";
                var result = await _ollamaService.AnalyzeBloodMicroscopeAsync(
                    _currentBloodImageBase64, lang, _bloodAnalysisCts.Token);

                _lastBloodAnalysisResult = result;
                DisplayBloodAnalysisResult(result);

                _bloodApplySymptomsButton.Enabled = result.DeducedSymptoms.Count > 0;
            }
            catch (OperationCanceledException)
            {
                _bloodResultBox.Text = "Blood analysis cancelled.";
            }
            catch (Exception ex)
            {
                _bloodResultBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetBloodAnalysisLoading(false);
            }
        }

        private void SetBloodAnalysisLoading(bool loading)
        {
            _bloodProgress.Visible = loading;
            _bloodAnalyzeButton.Enabled = !loading && !string.IsNullOrEmpty(_currentBloodImageBase64);
            _bloodUploadButton.Enabled = !loading;
            _bloodClearButton.Enabled = !loading && _bloodPreview.Image != null;
            if (loading)
            {
                _bloodResultBox.Clear();
                _bloodResultBox.Text = "🔬 AI is analyzing the blood smear... please wait...\n\n" +
                    "(This may take 30-120 seconds depending on the model and hardware)";
                _bloodTimingLabel.Text = "";
            }
        }

        /// <summary>Render the blood analysis result with rich formatting.</summary>
        private void DisplayBloodAnalysisResult(BloodAnalysisResult result)
        {
            _bloodResultBox.Clear();
            var rtb = _bloodResultBox;

            void AppendHeader(string text)
            {
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 11f, FontStyle.Bold);
                rtb.SelectionColor = Color.FromArgb(140, 20, 20);
                rtb.AppendText(text + "\n");
            }

            void AppendBody(string text)
            {
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9.5f, FontStyle.Regular);
                rtb.SelectionColor = rtb.ForeColor;
                rtb.AppendText(text + "\n");
            }

            void AppendBullet(string text, Color color)
            {
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9.5f, FontStyle.Regular);
                rtb.SelectionColor = color;
                rtb.AppendText($"  • {text}\n");
            }

            // Stain & Magnification
            if (!string.IsNullOrWhiteSpace(result.StainType) || !string.IsNullOrWhiteSpace(result.Magnification))
            {
                AppendHeader("🔬 Slide Information");
                if (!string.IsNullOrWhiteSpace(result.StainType))
                    AppendBody($"  Stain: {result.StainType}");
                if (!string.IsNullOrWhiteSpace(result.Magnification))
                    AppendBody($"  Magnification: {result.Magnification}");
                rtb.AppendText("\n");
            }

            // RBC Findings
            if (result.RbcFindings.Count > 0)
            {
                AppendHeader("🔴 Red Blood Cells (Erythrocytes)");
                foreach (var f in result.RbcFindings)
                    AppendBullet(f, Color.FromArgb(180, 40, 40));
                rtb.AppendText("\n");
            }

            // WBC Findings
            if (result.WbcFindings.Count > 0)
            {
                AppendHeader("⚪ White Blood Cells (Leukocytes)");
                foreach (var f in result.WbcFindings)
                    AppendBullet(f, Color.FromArgb(40, 80, 160));
                rtb.AppendText("\n");
            }

            // Platelet Findings
            if (result.PlateletFindings.Count > 0)
            {
                AppendHeader("🟣 Platelets");
                foreach (var f in result.PlateletFindings)
                    AppendBullet(f, Color.FromArgb(120, 60, 140));
                rtb.AppendText("\n");
            }

            // Other Findings
            if (result.OtherFindings.Count > 0)
            {
                AppendHeader("🔎 Other Findings");
                foreach (var f in result.OtherFindings)
                    AppendBullet(f, Color.FromArgb(100, 100, 40));
                rtb.AppendText("\n");
            }

            // Differential Count
            if (result.DifferentialCount.Count > 0)
            {
                AppendHeader("📊 Differential Count");
                foreach (var d in result.DifferentialCount)
                    AppendBullet(d, Color.FromArgb(60, 60, 60));
                rtb.AppendText("\n");
            }

            // Abnormalities
            if (result.Abnormalities.Count > 0)
            {
                AppendHeader("⚠️ Abnormalities");
                foreach (var a in result.Abnormalities)
                    AppendBullet(a, Color.FromArgb(200, 80, 0));
                rtb.AppendText("\n");
            }

            // Possible Conditions
            if (result.PossibleConditions.Count > 0)
            {
                AppendHeader("🏥 Possible Haematological Conditions");
                foreach (var c in result.PossibleConditions)
                    AppendBullet(c, Color.FromArgb(160, 60, 0));
                rtb.AppendText("\n");
            }

            // Deduced Symptoms
            if (result.DeducedSymptoms.Count > 0)
            {
                AppendHeader("🩺 Deduced Symptoms");
                foreach (var symptom in result.DeducedSymptoms)
                {
                    bool isKnown = _allSymptoms.Any(s =>
                        s.Contains(symptom, StringComparison.OrdinalIgnoreCase) ||
                        symptom.Contains(s, StringComparison.OrdinalIgnoreCase));
                    var color = isKnown ? Color.FromArgb(0, 120, 60) : Color.FromArgb(180, 100, 0);
                    var suffix = isKnown ? " ✓ (in database)" : " (not in database)";
                    AppendBullet(symptom + suffix, color);
                }
                rtb.AppendText("\n");

                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 8.5f, FontStyle.Italic);
                rtb.SelectionColor = Color.FromArgb(0, 100, 150);
                rtb.AppendText("  💡 Click 'Apply Symptoms' to auto-select matching symptoms in the checker.\n\n");
            }

            // Severity
            if (!string.IsNullOrWhiteSpace(result.Severity))
            {
                AppendHeader("⚡ Severity");
                var sevColor = result.Severity.ToLowerInvariant() switch
                {
                    var s when s.Contains("severe") => Color.FromArgb(200, 0, 0),
                    var s when s.Contains("moderate") => Color.FromArgb(200, 140, 0),
                    var s when s.Contains("normal") => Color.FromArgb(0, 128, 0),
                    _ => Color.FromArgb(0, 128, 0)
                };
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 10f, FontStyle.Bold);
                rtb.SelectionColor = sevColor;
                rtb.AppendText($"  {result.Severity}\n\n");
            }

            // Recommendation
            if (!string.IsNullOrWhiteSpace(result.Recommendation))
            {
                AppendHeader("💡 Recommendation");
                AppendBody($"  {result.Recommendation}");
                rtb.AppendText("\n");
            }

            // Disclaimer
            rtb.SelectionFont = new Font(rtb.Font.FontFamily, 8.5f, FontStyle.Italic);
            rtb.SelectionColor = Color.Gray;
            string disclaimer = !string.IsNullOrWhiteSpace(result.Disclaimer)
                ? result.Disclaimer
                : "⚠️ This blood analysis is for educational purposes only. Always consult a haematologist or pathologist for proper interpretation.";
            rtb.AppendText($"\n{disclaimer}\n");

            // Timing
            _bloodTimingLabel.Text = $"Blood analysis: {result.ElapsedMs} ms | Model: {_ollamaService?.ModelName ?? "?"}";

            rtb.SelectionStart = 0;
            rtb.ScrollToCaret();
        }

        /// <summary>
        /// Apply the deduced symptoms from blood analysis to the symptom checker.
        /// Uses fuzzy matching against known symptoms in the database.
        /// </summary>
        private void ApplyBloodDeducedSymptoms()
        {
            if (_lastBloodAnalysisResult == null || _lastBloodAnalysisResult.DeducedSymptoms.Count == 0)
            {
                MessageBox.Show(this, "No symptoms deduced from blood analysis yet.",
                    "No Symptoms", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int matched = 0;
            var matchedNames = new List<string>();
            var unmatchedNames = new List<string>();

            foreach (var deducedSymptom in _lastBloodAnalysisResult.DeducedSymptoms)
            {
                string? bestMatch = null;
                int bestScore = 0;

                foreach (var knownSymptom in _allSymptoms)
                {
                    if (string.Equals(knownSymptom, deducedSymptom, StringComparison.OrdinalIgnoreCase))
                    {
                        bestMatch = knownSymptom;
                        bestScore = 100;
                        break;
                    }

                    if (knownSymptom.Contains(deducedSymptom, StringComparison.OrdinalIgnoreCase) ||
                        deducedSymptom.Contains(knownSymptom, StringComparison.OrdinalIgnoreCase))
                    {
                        int score = 80;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMatch = knownSymptom;
                        }
                    }

                    var deducedWords = deducedSymptom.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var knownWords = knownSymptom.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var commonWords = deducedWords.Intersect(knownWords).Where(w => w.Length > 3).ToList();
                    if (commonWords.Count > 0)
                    {
                        int score = 40 + (commonWords.Count * 15);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMatch = knownSymptom;
                        }
                    }
                }

                if (bestScore < 60 && _synonymService != null)
                {
                    var aliasMap = _synonymService.BuildAliasToCanonical(_allSymptoms);
                    if (aliasMap.TryGetValue(deducedSymptom, out var canonical) &&
                        _allSymptoms.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                    {
                        bestMatch = canonical;
                        bestScore = 90;
                    }
                }

                if (bestMatch != null && bestScore >= 40)
                {
                    if (_checkedSymptoms.Add(bestMatch))
                    {
                        matched++;
                        matchedNames.Add($"{deducedSymptom} → {bestMatch}");
                    }
                }
                else
                {
                    unmatchedNames.Add(deducedSymptom);
                }
            }

            RefreshSymptomList();
            _checkButton.Enabled = _checkedSymptoms.Count > 0;

            var msg = $"Applied {matched} symptom(s) from blood analysis.\n\n";
            if (matchedNames.Count > 0)
                msg += "Matched:\n" + string.Join("\n", matchedNames.Select(m => $"  ✓ {m}")) + "\n\n";
            if (unmatchedNames.Count > 0)
                msg += "Not matched (not in database):\n" + string.Join("\n", unmatchedNames.Select(u => $"  ✗ {u}"));

            MessageBox.Show(this, msg, "Blood Symptoms Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Apply translations to blood analysis panel labels.</summary>
        private void ApplyBloodAnalysisTranslations()
        {
            var t = _translationService;
            if (t == null) return;

            _grpBloodAnalysis.Text = t.T("BloodAnalysisTitle") ?? "🔬 Blood Microscope Analysis (Vision AI)";
            _bloodUploadButton.Text = t.T("BloodUpload") ?? "📁 Upload Blood Image";
            _bloodAnalyzeButton.Text = t.T("BloodAnalyze") ?? "🔬 Analyze Blood";
            _bloodApplySymptomsButton.Text = t.T("BloodApplySymptoms") ?? "✅ Apply Symptoms";
            _bloodClearButton.Text = t.T("BloodClear") ?? "🗑 Clear";
        }
    }
}

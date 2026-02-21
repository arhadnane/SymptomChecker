using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;

namespace SymptomCheckerApp.UI
{
    /// <summary>
    /// Image analysis feature: upload a medical image and use a multimodal AI model
    /// (e.g. LLaVA) to deduce symptoms and possible conditions.
    /// </summary>
    public partial class MainForm
    {
        // Image analysis UI controls
        private readonly GroupBox _grpImageAnalysis = new GroupBox();
        private readonly Button _imgUploadButton = new Button();
        private readonly Button _imgAnalyzeButton = new Button();
        private readonly Button _imgClearButton = new Button();
        private readonly Button _imgApplySymptomsButton = new Button();
        private readonly PictureBox _imgPreview = new PictureBox();
        private readonly RichTextBox _imgResultBox = new RichTextBox();
        private readonly ProgressBar _imgProgress = new ProgressBar();
        private readonly Label _imgTimingLabel = new Label();
        private readonly Label _imgInfoLabel = new Label();

        private string? _currentImageBase64;
        private ImageAnalysisResult? _lastImageAnalysisResult;
        private CancellationTokenSource? _imgAnalysisCts;

        /// <summary>Build and wire the Image Analysis panel. Called from InitializeLayout.</summary>
        private void InitializeImageAnalysisPanel(Control parent)
        {
            _grpImageAnalysis.Text = "📷 Image Analysis (Vision AI)";
            _grpImageAnalysis.Dock = DockStyle.Fill;
            _grpImageAnalysis.Padding = new Padding(6);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));   // image preview column
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));   // results column
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // toolbar
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // progress
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // content
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // timing

            // Toolbar row (spans both columns)
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            _imgUploadButton.Text = "📁 Upload Image";
            _imgUploadButton.AutoSize = true;
            _imgUploadButton.FlatStyle = FlatStyle.Flat;
            _imgUploadButton.BackColor = Color.FromArgb(230, 240, 255);
            _imgUploadButton.AccessibleName = "Upload a medical image for analysis";
            _imgUploadButton.Click += (s, e) => UploadImage();

            _imgAnalyzeButton.Text = "🔍 Analyze Image";
            _imgAnalyzeButton.AutoSize = true;
            _imgAnalyzeButton.FlatStyle = FlatStyle.Flat;
            _imgAnalyzeButton.BackColor = Color.FromArgb(255, 240, 230);
            _imgAnalyzeButton.AccessibleName = "Analyze the uploaded image with AI";
            _imgAnalyzeButton.Enabled = false;
            _imgAnalyzeButton.Click += async (s, e) => await RunImageAnalysisAsync();

            _imgApplySymptomsButton.Text = "✅ Apply Symptoms";
            _imgApplySymptomsButton.AutoSize = true;
            _imgApplySymptomsButton.FlatStyle = FlatStyle.Flat;
            _imgApplySymptomsButton.BackColor = Color.FromArgb(230, 255, 230);
            _imgApplySymptomsButton.AccessibleName = "Select deduced symptoms in the symptom list";
            _imgApplySymptomsButton.Enabled = false;
            _imgApplySymptomsButton.Click += (s, e) => ApplyDeducedSymptoms();

            _imgClearButton.Text = "🗑 Clear";
            _imgClearButton.AutoSize = true;
            _imgClearButton.FlatStyle = FlatStyle.Flat;
            _imgClearButton.AccessibleName = "Clear uploaded image";
            _imgClearButton.Enabled = false;
            _imgClearButton.Click += (s, e) => ClearImageAnalysis();

            _imgInfoLabel.Text = "Supported: skin, throat, eye, mouth, nail — use a vision model (llava, bakllava, etc.)";
            _imgInfoLabel.AutoSize = true;
            _imgInfoLabel.ForeColor = Color.DimGray;
            _imgInfoLabel.Padding = new Padding(6, 6, 0, 0);
            _imgInfoLabel.Font = new Font(Font.FontFamily, 8f);

            toolbar.Controls.Add(_imgUploadButton);
            toolbar.Controls.Add(_imgAnalyzeButton);
            toolbar.Controls.Add(_imgApplySymptomsButton);
            toolbar.Controls.Add(_imgClearButton);
            toolbar.Controls.Add(_imgInfoLabel);

            layout.Controls.Add(toolbar, 0, 0);
            layout.SetColumnSpan(toolbar, 2);

            // Progress bar (spans both columns)
            _imgProgress.Dock = DockStyle.Top;
            _imgProgress.Style = ProgressBarStyle.Marquee;
            _imgProgress.MarqueeAnimationSpeed = 30;
            _imgProgress.Height = 4;
            _imgProgress.Visible = false;
            layout.Controls.Add(_imgProgress, 0, 1);
            layout.SetColumnSpan(_imgProgress, 2);

            // Image preview (left column)
            _imgPreview.Dock = DockStyle.Fill;
            _imgPreview.SizeMode = PictureBoxSizeMode.Zoom;
            _imgPreview.BorderStyle = BorderStyle.FixedSingle;
            _imgPreview.BackColor = Color.FromArgb(245, 245, 245);
            _imgPreview.AccessibleName = "Image preview";
            // Placeholder text via Paint event
            _imgPreview.Paint += (s, e) =>
            {
                if (_imgPreview.Image == null)
                {
                    var text = "Drop or upload\nan image here";
                    var font = new Font("Segoe UI", 10f, FontStyle.Italic);
                    var brush = new SolidBrush(Color.FromArgb(150, 150, 150));
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(text, font, brush, _imgPreview.ClientRectangle, sf);
                    font.Dispose();
                    brush.Dispose();
                }
            };
            // Enable drag and drop
            _imgPreview.AllowDrop = true;
            _imgPreview.DragEnter += (s, e) =>
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
            };
            _imgPreview.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    LoadImageFile(files[0]);
            };
            layout.Controls.Add(_imgPreview, 0, 2);

            // Results box (right column)
            _imgResultBox.Dock = DockStyle.Fill;
            _imgResultBox.ReadOnly = true;
            _imgResultBox.BorderStyle = BorderStyle.FixedSingle;
            _imgResultBox.BackColor = SystemColors.Window;
            _imgResultBox.Font = new Font("Segoe UI", 9.5f);
            _imgResultBox.AccessibleName = "Image analysis results";
            _imgResultBox.Text = "Upload a medical image (photo of skin condition, throat, eye, etc.) and click 'Analyze Image'.\n\n" +
                "The AI vision model will:\n" +
                "  • Identify the body region\n" +
                "  • Describe visual observations\n" +
                "  • Deduce possible symptoms\n" +
                "  • Suggest possible conditions\n\n" +
                "You can then apply the deduced symptoms to the symptom checker.\n\n" +
                "⚠ Requires a multimodal model in Ollama (e.g. llava, bakllava, llava-llama3).";
            layout.Controls.Add(_imgResultBox, 1, 2);

            // Timing label (spans both columns)
            _imgTimingLabel.Text = "";
            _imgTimingLabel.AutoSize = true;
            _imgTimingLabel.Padding = new Padding(4);
            _imgTimingLabel.ForeColor = Color.DimGray;
            _imgTimingLabel.AccessibleName = "Image analysis timing";
            layout.Controls.Add(_imgTimingLabel, 0, 3);
            layout.SetColumnSpan(_imgTimingLabel, 2);

            _grpImageAnalysis.Controls.Add(layout);
            parent.Controls.Add(_grpImageAnalysis);
        }

        /// <summary>Open a file dialog to pick an image.</summary>
        private void UploadImage()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select a medical image",
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                LoadImageFile(dlg.FileName);
            }
        }

        /// <summary>Load an image file, display preview, and prepare base64.</summary>
        private void LoadImageFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var validExts = new HashSet<string> { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                if (!validExts.Contains(ext))
                {
                    MessageBox.Show(this, "Unsupported image format. Please use JPG, PNG, BMP, GIF or WebP.",
                        "Invalid Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Check file size (limit to 20MB)
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 20 * 1024 * 1024)
                {
                    MessageBox.Show(this, "Image file is too large. Maximum size is 20 MB.",
                        "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Load image for preview
                var imageBytes = File.ReadAllBytes(filePath);
                _currentImageBase64 = Convert.ToBase64String(imageBytes);

                using var ms = new MemoryStream(imageBytes);
                var oldImage = _imgPreview.Image;
                _imgPreview.Image = Image.FromStream(ms);
                oldImage?.Dispose();

                _imgAnalyzeButton.Enabled = true;
                _imgClearButton.Enabled = true;
                _imgApplySymptomsButton.Enabled = false;
                _lastImageAnalysisResult = null;

                _imgResultBox.Clear();
                _imgResultBox.Text = $"Image loaded: {Path.GetFileName(filePath)}\n" +
                    $"Size: {fileInfo.Length / 1024} KB\n\n" +
                    "Click 'Analyze Image' to start AI analysis.\n" +
                    "Make sure a vision model (llava, bakllava) is selected in the AI panel above.";
                _imgTimingLabel.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading image: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Clear the uploaded image and results.</summary>
        private void ClearImageAnalysis()
        {
            var oldImage = _imgPreview.Image;
            _imgPreview.Image = null;
            oldImage?.Dispose();

            _currentImageBase64 = null;
            _lastImageAnalysisResult = null;
            _imgAnalyzeButton.Enabled = false;
            _imgClearButton.Enabled = false;
            _imgApplySymptomsButton.Enabled = false;
            _imgTimingLabel.Text = "";

            _imgResultBox.Clear();
            _imgResultBox.Text = "Upload a medical image to analyze.";
            _imgPreview.Invalidate(); // force repaint for placeholder text
        }

        /// <summary>Run the AI image analysis using the Ollama vision model.</summary>
        private async Task RunImageAnalysisAsync()
        {
            if (_ollamaService == null || string.IsNullOrEmpty(_currentImageBase64))
            {
                _imgResultBox.Text = "Please upload an image and ensure Ollama is connected.";
                return;
            }

            // Ensure correct model is set from the main Ollama panel selector
            var selectedModel = _ollamaModelSelector.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedModel))
            {
                _ollamaService.SetModel(selectedModel);
            }
            else
            {
                _imgResultBox.Text = "Please select a vision model (e.g. llava) in the AI panel above.";
                return;
            }

            _imgAnalysisCts?.Cancel();
            _imgAnalysisCts = new CancellationTokenSource();

            SetImageAnalysisLoading(true);

            try
            {
                var lang = _translationService?.CurrentLanguage ?? "en";
                var result = await _ollamaService.AnalyzeImageAsync(
                    _currentImageBase64, lang, _imgAnalysisCts.Token);

                _lastImageAnalysisResult = result;
                DisplayImageAnalysisResult(result);

                _imgApplySymptomsButton.Enabled = result.DeducedSymptoms.Count > 0;
            }
            catch (OperationCanceledException)
            {
                _imgResultBox.Text = "Image analysis cancelled.";
            }
            catch (Exception ex)
            {
                _imgResultBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetImageAnalysisLoading(false);
            }
        }

        private void SetImageAnalysisLoading(bool loading)
        {
            _imgProgress.Visible = loading;
            _imgAnalyzeButton.Enabled = !loading && !string.IsNullOrEmpty(_currentImageBase64);
            _imgUploadButton.Enabled = !loading;
            _imgClearButton.Enabled = !loading && _imgPreview.Image != null;
            if (loading)
            {
                _imgResultBox.Clear();
                _imgResultBox.Text = "🔍 AI is analyzing the image... please wait...\n\n" +
                    "(This may take 30-90 seconds depending on the model and hardware)";
                _imgTimingLabel.Text = "";
            }
        }

        /// <summary>Render the image analysis result with formatting.</summary>
        private void DisplayImageAnalysisResult(ImageAnalysisResult result)
        {
            _imgResultBox.Clear();
            var rtb = _imgResultBox;

            void AppendHeader(string text)
            {
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 11f, FontStyle.Bold);
                rtb.SelectionColor = Color.FromArgb(30, 80, 160);
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

            // Body Region
            if (!string.IsNullOrWhiteSpace(result.BodyRegion))
            {
                AppendHeader("🔬 Body Region");
                AppendBody($"  {result.BodyRegion}");
                rtb.AppendText("\n");
            }

            // Observations
            if (result.Observations.Count > 0)
            {
                AppendHeader("👁 Visual Observations");
                foreach (var obs in result.Observations)
                    AppendBullet(obs, Color.FromArgb(60, 60, 60));
                rtb.AppendText("\n");
            }

            // Deduced Symptoms
            if (result.DeducedSymptoms.Count > 0)
            {
                AppendHeader("🩺 Deduced Symptoms");
                foreach (var symptom in result.DeducedSymptoms)
                {
                    // Check if symptom matches one in our database
                    bool isKnown = _allSymptoms.Any(s =>
                        s.Contains(symptom, StringComparison.OrdinalIgnoreCase) ||
                        symptom.Contains(s, StringComparison.OrdinalIgnoreCase));
                    var color = isKnown ? Color.FromArgb(0, 120, 60) : Color.FromArgb(180, 100, 0);
                    var suffix = isKnown ? " ✓ (in database)" : " (not in database)";
                    AppendBullet(symptom + suffix, color);
                }
                rtb.AppendText("\n");

                // Show apply button hint
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 8.5f, FontStyle.Italic);
                rtb.SelectionColor = Color.FromArgb(0, 100, 150);
                rtb.AppendText("  💡 Click 'Apply Symptoms' to auto-select matching symptoms in the checker.\n\n");
            }

            // Possible Conditions
            if (result.PossibleConditions.Count > 0)
            {
                AppendHeader("🏥 Possible Conditions");
                foreach (var cond in result.PossibleConditions)
                    AppendBullet(cond, Color.FromArgb(160, 60, 0));
                rtb.AppendText("\n");
            }

            // Severity
            if (!string.IsNullOrWhiteSpace(result.Severity))
            {
                AppendHeader("⚡ Severity");
                var sevColor = result.Severity.ToLowerInvariant() switch
                {
                    var s when s.Contains("severe") => Color.FromArgb(200, 0, 0),
                    var s when s.Contains("moderate") => Color.FromArgb(200, 140, 0),
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
                : "⚠️ This image analysis is for educational purposes only. Always consult a healthcare professional for proper diagnosis.";
            rtb.AppendText($"\n{disclaimer}\n");

            // Timing
            _imgTimingLabel.Text = $"Image analysis: {result.ElapsedMs} ms | Model: {_ollamaService?.ModelName ?? "?"}";

            rtb.SelectionStart = 0;
            rtb.ScrollToCaret();
        }

        /// <summary>
        /// Apply the deduced symptoms from image analysis to the symptom checker.
        /// Uses fuzzy matching against known symptoms in the database.
        /// </summary>
        private void ApplyDeducedSymptoms()
        {
            if (_lastImageAnalysisResult == null || _lastImageAnalysisResult.DeducedSymptoms.Count == 0)
            {
                MessageBox.Show(this, "No symptoms deduced from image analysis yet.",
                    "No Symptoms", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int matched = 0;
            var matchedNames = new List<string>();
            var unmatchedNames = new List<string>();

            foreach (var deducedSymptom in _lastImageAnalysisResult.DeducedSymptoms)
            {
                // Try exact match first, then partial/fuzzy match
                string? bestMatch = null;
                int bestScore = 0;

                foreach (var knownSymptom in _allSymptoms)
                {
                    // Exact match
                    if (string.Equals(knownSymptom, deducedSymptom, StringComparison.OrdinalIgnoreCase))
                    {
                        bestMatch = knownSymptom;
                        bestScore = 100;
                        break;
                    }

                    // Check if known symptom contains the deduced symptom or vice versa
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

                    // Word-level match: check if any significant word from deduced symptom appears
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

                // Also check synonyms if available
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
                    // Select this symptom in the CheckedListBox
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

            // Refresh the symptom list display to reflect new selections
            RefreshSymptomList();

            // Update check button state
            _checkButton.Enabled = _checkedSymptoms.Count > 0;

            // Show feedback
            var msg = $"Applied {matched} symptom(s) from image analysis.\n\n";
            if (matchedNames.Count > 0)
                msg += "Matched:\n" + string.Join("\n", matchedNames.Select(m => $"  ✓ {m}")) + "\n\n";
            if (unmatchedNames.Count > 0)
                msg += "Not matched (not in database):\n" + string.Join("\n", unmatchedNames.Select(u => $"  ✗ {u}"));

            MessageBox.Show(this, msg, "Symptoms Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Apply translations to image analysis panel labels.</summary>
        private void ApplyImageAnalysisTranslations()
        {
            var t = _translationService;
            if (t == null) return;

            _grpImageAnalysis.Text = t.T("ImageAnalysisTitle") ?? "📷 Image Analysis (Vision AI)";
            _imgUploadButton.Text = t.T("ImageUpload") ?? "📁 Upload Image";
            _imgAnalyzeButton.Text = t.T("ImageAnalyze") ?? "🔍 Analyze Image";
            _imgApplySymptomsButton.Text = t.T("ImageApplySymptoms") ?? "✅ Apply Symptoms";
            _imgClearButton.Text = t.T("ImageClear") ?? "🗑 Clear";
        }
    }
}

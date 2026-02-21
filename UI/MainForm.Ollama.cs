using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SymptomCheckerApp.Models;
using SymptomCheckerApp.Services;

namespace SymptomCheckerApp.UI
{
    // AI-powered diagnosis reinforcement and medication proposals via Ollama
    public partial class MainForm
    {
        private OllamaService? _ollamaService;
        private CancellationTokenSource? _ollamaCts;
        private AiDiagnosisResult? _lastAiResult;

        // Ollama UI controls
        private readonly GroupBox _grpAi = new GroupBox();
        private readonly Button _aiDiagnoseButton = new Button();
        private readonly Button _aiMedsButton = new Button();
        private readonly ComboBox _ollamaModelSelector = new ComboBox();
        private readonly Button _ollamaRefreshButton = new Button();
        private readonly Label _ollamaStatus = new Label();
        private readonly TextBox _aiUrlBox = new TextBox();
        private readonly RichTextBox _aiOutputBox = new RichTextBox();
        private readonly ProgressBar _aiProgress = new ProgressBar();
        private readonly Label _aiTimingLabel = new Label();
        private readonly CheckBox _autoAiCheck = new CheckBox();

        /// <summary>Build and wire the AI / Ollama panel controls. Called from InitializeLayout.</summary>
        private void InitializeOllamaPanel(Control parent)
        {
            _grpAi.Text = "🤖 AI Diagnostic Assistant (Ollama)";
            _grpAi.Dock = DockStyle.Fill;
            _grpAi.Padding = new Padding(6);

            var aiLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            aiLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // toolbar
            aiLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // progress
            aiLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // output
            aiLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // timing

            // Toolbar row
            var aiToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2)
            };

            var lblUrl = new Label { Text = "URL:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            _aiUrlBox.Text = "http://localhost:11434";
            _aiUrlBox.Width = ScaleX(160);
            _aiUrlBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _aiUrlBox.AccessibleName = "Ollama server URL";

            var lblModel = new Label { Text = "Model:", AutoSize = true, Padding = new Padding(6, 6, 0, 0) };
            _ollamaModelSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _ollamaModelSelector.Width = ScaleX(140);
            _ollamaModelSelector.AccessibleName = "Ollama model selector";

            _ollamaRefreshButton.Text = "⟳";
            _ollamaRefreshButton.Width = ScaleX(32);
            _ollamaRefreshButton.FlatStyle = FlatStyle.Flat;
            _ollamaRefreshButton.AccessibleName = "Refresh Ollama models";
            _ollamaRefreshButton.Click += async (s, e) => await RefreshOllamaModelsAsync();

            _ollamaStatus.Text = "⬤ Disconnected";
            _ollamaStatus.ForeColor = Color.Gray;
            _ollamaStatus.AutoSize = true;
            _ollamaStatus.Padding = new Padding(6, 6, 0, 0);
            _ollamaStatus.AccessibleName = "Ollama connection status";

            _aiDiagnoseButton.Text = "🧠 AI Diagnosis";
            _aiDiagnoseButton.AutoSize = true;
            _aiDiagnoseButton.FlatStyle = FlatStyle.Flat;
            _aiDiagnoseButton.BackColor = Color.FromArgb(230, 240, 255);
            _aiDiagnoseButton.AccessibleName = "Run AI-powered diagnostic analysis";
            _aiDiagnoseButton.Click += async (s, e) => await RunAiDiagnosisAsync();

            _aiMedsButton.Text = "💊 AI Medications";
            _aiMedsButton.AutoSize = true;
            _aiMedsButton.FlatStyle = FlatStyle.Flat;
            _aiMedsButton.BackColor = Color.FromArgb(230, 255, 230);
            _aiMedsButton.AccessibleName = "Get AI medication recommendations";
            _aiMedsButton.Click += async (s, e) => await RunAiMedicationAdviceAsync();

            _autoAiCheck.Text = "Auto AI";
            _autoAiCheck.AutoSize = true;
            _autoAiCheck.AccessibleName = "Automatically run AI after check";
            _autoAiCheck.CheckedChanged += (s, e) =>
            {
                if (_settingsService != null) { _settingsService.Settings.AutoAi = _autoAiCheck.Checked; _settingsService.Save(); }
            };

            _aiUrlBox.Leave += (s, e) =>
            {
                if (_settingsService != null) { _settingsService.Settings.OllamaUrl = _aiUrlBox.Text?.Trim(); _settingsService.Save(); }
            };

            _ollamaModelSelector.SelectedIndexChanged += (s, e) =>
            {
                if (_settingsService != null && _ollamaModelSelector.SelectedItem != null)
                {
                    _settingsService.Settings.OllamaModel = _ollamaModelSelector.SelectedItem.ToString();
                    _settingsService.Save();
                }
            };

            aiToolbar.Controls.Add(lblUrl);
            aiToolbar.Controls.Add(_aiUrlBox);
            aiToolbar.Controls.Add(lblModel);
            aiToolbar.Controls.Add(_ollamaModelSelector);
            aiToolbar.Controls.Add(_ollamaRefreshButton);
            aiToolbar.Controls.Add(_ollamaStatus);
            aiToolbar.Controls.Add(_aiDiagnoseButton);
            aiToolbar.Controls.Add(_aiMedsButton);
            aiToolbar.Controls.Add(_autoAiCheck);

            // Progress bar
            _aiProgress.Dock = DockStyle.Top;
            _aiProgress.Style = ProgressBarStyle.Marquee;
            _aiProgress.MarqueeAnimationSpeed = 30;
            _aiProgress.Height = 4;
            _aiProgress.Visible = false;

            // Output box
            _aiOutputBox.Dock = DockStyle.Fill;
            _aiOutputBox.ReadOnly = true;
            _aiOutputBox.BorderStyle = BorderStyle.FixedSingle;
            _aiOutputBox.BackColor = SystemColors.Window;
            _aiOutputBox.Font = new Font("Segoe UI", 9.5f);
            _aiOutputBox.AccessibleName = "AI diagnostic output";
            _aiOutputBox.Text = "AI analysis will appear here after you run a symptom check and click 'AI Diagnosis'.\n\nRequires Ollama running locally (https://ollama.com).";

            // Timing
            _aiTimingLabel.Text = "";
            _aiTimingLabel.AutoSize = true;
            _aiTimingLabel.Padding = new Padding(4);
            _aiTimingLabel.ForeColor = Color.DimGray;
            _aiTimingLabel.AccessibleName = "AI response timing";

            aiLayout.Controls.Add(aiToolbar, 0, 0);
            aiLayout.Controls.Add(_aiProgress, 0, 1);
            aiLayout.Controls.Add(_aiOutputBox, 0, 2);
            aiLayout.Controls.Add(_aiTimingLabel, 0, 3);

            _grpAi.Controls.Add(aiLayout);
            parent.Controls.Add(_grpAi);
        }

        /// <summary>Initialize or re-initialize the OllamaService and check availability.</summary>
        private async Task InitializeOllamaAsync()
        {
            var url = _aiUrlBox.Text?.Trim();
            if (string.IsNullOrEmpty(url)) url = "http://localhost:11434";

            _ollamaService?.Dispose();
            _ollamaService = new OllamaService(url);

            var available = await _ollamaService.CheckAvailabilityAsync();
            UpdateOllamaStatusUI(available);

            if (available)
            {
                await RefreshOllamaModelsAsync();
            }
        }

        private async Task RefreshOllamaModelsAsync()
        {
            try
            {
                var url = _aiUrlBox.Text?.Trim();
                if (string.IsNullOrEmpty(url)) url = "http://localhost:11434";

                if (_ollamaService == null || _ollamaService.BaseUrl != url.TrimEnd('/'))
                {
                    _ollamaService?.Dispose();
                    _ollamaService = new OllamaService(url);
                }

                var available = await _ollamaService.CheckAvailabilityAsync();
                UpdateOllamaStatusUI(available);

                if (!available) return;

                var models = await _ollamaService.ListModelsAsync();
                _ollamaModelSelector.Items.Clear();
                foreach (var m in models)
                    _ollamaModelSelector.Items.Add(m);

                if (_ollamaModelSelector.Items.Count > 0)
                {
                    // Prefer models with "llama" or "mistral" in the name
                    int bestIdx = 0;
                    for (int i = 0; i < _ollamaModelSelector.Items.Count; i++)
                    {
                        var name = _ollamaModelSelector.Items[i]?.ToString() ?? "";
                        if (name.Contains("llama", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("mistral", StringComparison.OrdinalIgnoreCase))
                        {
                            bestIdx = i;
                            break;
                        }
                    }

                    // Restore from settings if available
                    if (_settingsService?.Settings.OllamaModel is string savedModel && !string.IsNullOrEmpty(savedModel))
                    {
                        for (int i = 0; i < _ollamaModelSelector.Items.Count; i++)
                        {
                            if (string.Equals(_ollamaModelSelector.Items[i]?.ToString(), savedModel, StringComparison.OrdinalIgnoreCase))
                            {
                                bestIdx = i;
                                break;
                            }
                        }
                    }

                    _ollamaModelSelector.SelectedIndex = bestIdx;
                }
            }
            catch (Exception ex)
            {
                UpdateOllamaStatusUI(false);
                _aiOutputBox.Text = $"Error listing models: {ex.Message}";
            }
        }

        private void UpdateOllamaStatusUI(bool available)
        {
            if (available)
            {
                _ollamaStatus.Text = "⬤ Connected";
                _ollamaStatus.ForeColor = Color.FromArgb(0, 128, 0);
                _aiDiagnoseButton.Enabled = true;
                _aiMedsButton.Enabled = true;
            }
            else
            {
                _ollamaStatus.Text = "⬤ Disconnected";
                _ollamaStatus.ForeColor = Color.FromArgb(180, 0, 0);
                _aiDiagnoseButton.Enabled = false;
                _aiMedsButton.Enabled = false;
            }
        }

        /// <summary>Run full AI diagnostic assessment with medication suggestions.</summary>
        private async Task RunAiDiagnosisAsync()
        {
            if (_ollamaService == null || _lastResults == null || _lastResults.Count == 0)
            {
                _aiOutputBox.Text = _translationService?.T("AiNoResults") ??
                    "Please run a symptom check first (select symptoms and click Check).";
                return;
            }

            // Ensure correct model is set
            var selectedModel = _ollamaModelSelector.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedModel))
            {
                _ollamaService.SetModel(selectedModel);
                if (_settingsService != null)
                {
                    _settingsService.Settings.OllamaModel = selectedModel;
                    _settingsService.Save();
                }
            }

            _ollamaCts?.Cancel();
            _ollamaCts = new CancellationTokenSource();

            SetAiLoading(true);

            try
            {
                var lang = _translationService?.CurrentLanguage ?? "en";
                double? age = _numAge.Value > 0 ? (double)_numAge.Value : null;
                double? tempC = _numTempC.Value > 30 ? (double)_numTempC.Value : null;
                int? hr = (int)_numHR.Value > 20 ? (int)_numHR.Value : null;
                int? rr = (int)_numRR.Value > 4 ? (int)_numRR.Value : null;
                int? spo2 = (int)_numSpO2.Value > 50 ? (int)_numSpO2.Value : null;

                var result = await _ollamaService.GetDiagnosisReinforcementAsync(
                    _checkedSymptoms.ToList(),
                    _lastResults,
                    lang, age, tempC, hr, rr, spo2,
                    _ollamaCts.Token);

                _lastAiResult = result;
                DisplayAiResult(result);
            }
            catch (OperationCanceledException)
            {
                _aiOutputBox.Text = "AI analysis cancelled.";
            }
            catch (Exception ex)
            {
                _aiOutputBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetAiLoading(false);
            }
        }

        /// <summary>Run AI medication recommendations for the selected condition.</summary>
        private async Task RunAiMedicationAdviceAsync()
        {
            if (_ollamaService == null) return;

            // Get selected condition from results list
            string? conditionName = null;
            var matchedSymptoms = new System.Collections.Generic.List<string>();

            int idx = _resultsList.SelectedIndex;
            if (idx >= 0 && idx < _resultIndexMap.Count)
            {
                int ri = _resultIndexMap[idx];
                if (ri >= 0 && ri < _lastResults.Count)
                {
                    conditionName = _lastResults[ri].Name;
                    matchedSymptoms = _lastResults[ri].MatchedSymptoms;
                }
            }

            if (string.IsNullOrEmpty(conditionName) && _lastResults?.Count > 0)
            {
                conditionName = _lastResults[0].Name;
                matchedSymptoms = _lastResults[0].MatchedSymptoms;
            }

            if (string.IsNullOrEmpty(conditionName))
            {
                _aiOutputBox.Text = _translationService?.T("AiSelectCondition") ??
                    "Please run a symptom check and select a condition for medication advice.";
                return;
            }

            var selectedModel = _ollamaModelSelector.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedModel))
                _ollamaService.SetModel(selectedModel);

            _ollamaCts?.Cancel();
            _ollamaCts = new CancellationTokenSource();

            SetAiLoading(true);

            try
            {
                var lang = _translationService?.CurrentLanguage ?? "en";
                double? age = _numAge.Value > 0 ? (double)_numAge.Value : null;

                var result = await _ollamaService.GetMedicationAdviceAsync(
                    conditionName, matchedSymptoms, lang, age, _ollamaCts.Token);

                _lastAiResult = result;
                DisplayAiResult(result);
            }
            catch (OperationCanceledException)
            {
                _aiOutputBox.Text = "Cancelled.";
            }
            catch (Exception ex)
            {
                _aiOutputBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetAiLoading(false);
            }
        }

        private void SetAiLoading(bool loading)
        {
            _aiProgress.Visible = loading;
            _aiDiagnoseButton.Enabled = !loading && (_ollamaService?.IsAvailable ?? false);
            _aiMedsButton.Enabled = !loading && (_ollamaService?.IsAvailable ?? false);
            if (loading)
            {
                _aiOutputBox.Text = _translationService?.T("AiThinking") ?? "🤖 AI is analyzing... please wait...";
                _aiTimingLabel.Text = "";
            }
        }

        /// <summary>Render the AI result with color formatting in the RichTextBox.</summary>
        private void DisplayAiResult(AiDiagnosisResult result)
        {
            _aiOutputBox.Clear();
            var rtb = _aiOutputBox;

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

            void AppendMedication(MedicationProposal med)
            {
                // Name in bold
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9.5f, FontStyle.Bold);
                bool isOtc = med.Category.Contains("OTC", StringComparison.OrdinalIgnoreCase);
                rtb.SelectionColor = isOtc ? Color.FromArgb(0, 120, 60) : Color.FromArgb(160, 80, 0);
                rtb.AppendText($"  💊 {med.Name}");

                // Category badge
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 8f, FontStyle.Italic);
                rtb.SelectionColor = isOtc ? Color.FromArgb(0, 100, 50) : Color.FromArgb(180, 60, 0);
                rtb.AppendText($"  [{med.Category}]\n");

                // Details
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9f, FontStyle.Regular);
                rtb.SelectionColor = rtb.ForeColor;
                if (!string.IsNullOrEmpty(med.Purpose))
                    rtb.AppendText($"     Purpose: {med.Purpose}\n");
                if (!string.IsNullOrEmpty(med.Dosage))
                    rtb.AppendText($"     Dosage: {med.Dosage}\n");

                if (!string.IsNullOrEmpty(med.Warning))
                {
                    rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9f, FontStyle.Italic);
                    rtb.SelectionColor = Color.FromArgb(180, 0, 0);
                    rtb.AppendText($"     ⚠ {med.Warning}\n");
                }

                rtb.AppendText("\n");
            }

            void AppendRedFlag(string flag)
            {
                rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9.5f, FontStyle.Bold);
                rtb.SelectionColor = Color.FromArgb(200, 0, 0);
                rtb.AppendText($"  🚩 {flag}\n");
            }

            // Diagnostic Assessment
            if (!string.IsNullOrWhiteSpace(result.DiagnosticAssessment))
            {
                AppendHeader("🧠 Diagnostic Assessment");
                AppendBody(result.DiagnosticAssessment);
                rtb.AppendText("\n");
            }

            // Confidence reinforcement
            if (result.ConfidenceReinforcement.HasValue)
            {
                AppendHeader("📊 AI Confidence");
                double conf = result.ConfidenceReinforcement.Value;
                string confBar = new string('█', (int)(conf * 20)) + new string('░', 20 - (int)(conf * 20));
                rtb.SelectionFont = new Font("Consolas", 10f, FontStyle.Regular);
                rtb.SelectionColor = conf > 0.7 ? Color.FromArgb(0, 128, 0) :
                                    conf > 0.4 ? Color.FromArgb(180, 140, 0) :
                                    Color.FromArgb(180, 0, 0);
                rtb.AppendText($"  [{confBar}] {conf:P0}\n\n");
            }

            // Medications
            if (result.Medications.Count > 0)
            {
                AppendHeader("💊 Medication Proposals");
                // Separate OTC and prescription
                var otcMeds = result.Medications.Where(m =>
                    m.Category.Contains("OTC", StringComparison.OrdinalIgnoreCase) ||
                    m.Category.Contains("over", StringComparison.OrdinalIgnoreCase)).ToList();
                var rxMeds = result.Medications.Where(m =>
                    !m.Category.Contains("OTC", StringComparison.OrdinalIgnoreCase) &&
                    !m.Category.Contains("over", StringComparison.OrdinalIgnoreCase)).ToList();

                if (otcMeds.Count > 0)
                {
                    rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9.5f, FontStyle.Italic);
                    rtb.SelectionColor = Color.FromArgb(0, 100, 50);
                    rtb.AppendText("  Over-the-Counter:\n");
                    foreach (var med in otcMeds) AppendMedication(med);
                }

                if (rxMeds.Count > 0)
                {
                    rtb.SelectionFont = new Font(rtb.Font.FontFamily, 9.5f, FontStyle.Italic);
                    rtb.SelectionColor = Color.FromArgb(180, 80, 0);
                    rtb.AppendText("  Prescription (requires doctor):\n");
                    foreach (var med in rxMeds) AppendMedication(med);
                }

                rtb.AppendText("\n");
            }

            // Red Flags
            if (result.RedFlags.Count > 0)
            {
                AppendHeader("🚩 Red Flags — Seek Immediate Care");
                foreach (var flag in result.RedFlags) AppendRedFlag(flag);
                rtb.AppendText("\n");
            }

            // Self-Care
            if (!string.IsNullOrWhiteSpace(result.SelfCareAdvice))
            {
                AppendHeader("🏠 Self-Care Advice");
                AppendBody(result.SelfCareAdvice);
                rtb.AppendText("\n");
            }

            // Disclaimer
            rtb.SelectionFont = new Font(rtb.Font.FontFamily, 8.5f, FontStyle.Italic);
            rtb.SelectionColor = Color.Gray;
            string disclaimer = !string.IsNullOrWhiteSpace(result.Disclaimer)
                ? result.Disclaimer
                : "⚠️ This information is for educational purposes only. Always consult a healthcare professional.";
            rtb.AppendText($"\n{disclaimer}\n");

            // Timing
            _aiTimingLabel.Text = $"AI response: {result.ElapsedMs} ms | Model: {_ollamaService?.ModelName ?? "?"}";

            // Scroll to top
            rtb.SelectionStart = 0;
            rtb.ScrollToCaret();
        }

        /// <summary>Apply Ollama translations.</summary>
        private void ApplyOllamaTranslations()
        {
            var t = _translationService;
            if (t == null) return;

            _grpAi.Text = t.T("AiAssistantTitle") ?? "🤖 AI Diagnostic Assistant (Ollama)";
            _aiDiagnoseButton.Text = t.T("AiDiagnose") ?? "🧠 AI Diagnosis";
            _aiMedsButton.Text = t.T("AiMedications") ?? "💊 AI Medications";
            _autoAiCheck.Text = t.T("AutoAi") ?? "Auto AI";
        }
    }
}
